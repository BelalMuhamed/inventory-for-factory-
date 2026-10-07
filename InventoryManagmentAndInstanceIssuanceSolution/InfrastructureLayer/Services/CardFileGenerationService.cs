using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.CardFiles;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.CardFiles;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using ApplicationLayer.Security;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Orchestrates card-file generation (Card File Generation, Phase 9.5): authorize → resolve
    /// and vet the target tenant → validate every card against that tenant's catalog → serialize
    /// → SHA-256 fingerprint → AES-256-GCM encrypt.
    /// <para>
    /// Nothing is persisted and no transaction is opened; two catalog reads are the only database
    /// contact. The clear PANs supplied by the caller exist in memory for the duration of the call
    /// and nowhere else — not in a log, not in a temp file, not in the response.
    /// </para>
    /// </summary>
    public sealed class CardFileGenerationService : ICardFileGenerationService
    {
        private const string FileNameTimestampFormat = "yyyyMMddHHmmss";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IBatchFileEncryptor _encryptor;
        private readonly ICardFileWriter _writer;
        private readonly CardFileOptions _options;
        private readonly ILogger<CardFileGenerationService> _logger;

        public CardFileGenerationService(
            IUnitOfWork unitOfWork,
            ICurrentTenant currentTenant,
            IBatchFileEncryptor encryptor,
            ICardFileWriter writer,
            IOptions<CardFileOptions> options,
            ILogger<CardFileGenerationService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Result<CardFileGenerationResult>> GenerateAsync(
            CardFileGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            // The SystemAdminOnly policy already gated the route. Re-checking here keeps the rule
            // enforced even if this service is ever called from somewhere other than the
            // controller, and matches how ProductService/BranchService treat admin context.
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<CardFileGenerationResult>(CardFileErrors.ActorNotResolved());
            }

            try
            {
                Tenant? tenant = await _unitOfWork.Tenants
                    .GetByIdIncludingDeletedAsync(request.TenantId, cancellationToken);

                if (tenant is null)
                {
                    return Result.Failure<CardFileGenerationResult>(TenantErrors.NotFound(request.TenantId));
                }

                if (tenant.IsDeleted || !tenant.IsActive)
                {
                    return Result.Failure<CardFileGenerationResult>(
                        CardFileErrors.TenantUnavailable(request.TenantId));
                }

                IReadOnlyList<CardFileEntry> cards = request.Cards ?? Array.Empty<CardFileEntry>();

                if (cards.Count == 0)
                {
                    return Result.Failure<CardFileGenerationResult>(CardFileErrors.NoCards());
                }

                if (cards.Count > _options.MaxCardsPerRequest)
                {
                    return Result.Failure<CardFileGenerationResult>(
                        CardFileErrors.TooManyCards(_options.MaxCardsPerRequest));
                }

                // Two lookups total, not 2xN. Both maps are keyed OrdinalIgnoreCase, so the
                // caller's casing does not have to match what is stored.
                IReadOnlyDictionary<string, Product> productMap =
                    await _unitOfWork.Products.GetTenantMapAsync(request.TenantId, cancellationToken);
                IReadOnlyDictionary<string, Branch> branchMap =
                    await _unitOfWork.Branches.GetTenantMapAsync(request.TenantId, cancellationToken);

                (IReadOnlyList<CardFileLine> lines, IReadOnlyList<RejectedCardEntry> rejections) =
                    ValidateCards(cards, productMap, branchMap);

                if (rejections.Count > 0)
                {
                    _logger.LogWarning(
                        "Card file generation rejected. TenantId={TenantId} CardCount={CardCount} RejectedCount={RejectedCount} Reasons={Reasons}",
                        request.TenantId,
                        cards.Count,
                        rejections.Count,
                        SummarizeReasons(rejections));

                    return Result.Failure<CardFileGenerationResult>(CardFileErrors.CardsRejected(rejections));
                }

                // Built once, then both hashed and encrypted. Hashing a different string than the
                // one encrypted would produce a FileMac the tenant can never reproduce.
                string plaintext = _writer.Write(lines);
                string fileMac = ComputeSha256Hex(plaintext);

                Result<byte[]> encryptResult = _encryptor.Encrypt(request.TenantId, plaintext);
                if (encryptResult.IsFailure)
                {
                    _logger.LogError(
                        "Card file encryption failed. TenantId={TenantId} CardCount={CardCount} FileMac={FileMac}",
                        request.TenantId, lines.Count, fileMac);

                    return Result.Failure<CardFileGenerationResult>(encryptResult.Error);
                }

                byte[] cipherBytes = encryptResult.Value;
                string fileName = BuildFileName(tenant.Code);

                // Card count and fingerprint only. Never the card list, never a PAN.
                _logger.LogInformation(
                    "Card file generated. TenantId={TenantId} CardCount={CardCount} FileMac={FileMac} FileName={FileName} SizeBytes={SizeBytes}",
                    request.TenantId, lines.Count, fileMac, fileName, cipherBytes.LongLength);

                var result = new CardFileGenerationResult(
                    fileName,
                    fileMac,
                    lines.Count,
                    lines.Count,
                    cipherBytes.LongLength,
                    cipherBytes);

                return Result.Success(result);
            }
            catch (Exception ex)
            {
                // Logged where the context is, then surfaced opaquely. The exception message is
                // never returned: with clear PANs in scope, an echoed message is a disclosure risk.
                _logger.LogError(
                    ex,
                    "Unexpected failure generating card file. TenantId={TenantId} CardCount={CardCount}",
                    request.TenantId, request.Cards?.Count ?? 0);

                return Result.Failure<CardFileGenerationResult>(CardFileErrors.GenerationFailed());
            }
        }

        /// <summary>
        /// Validates every card and returns the serializable rows alongside the rejections.
        /// Validation runs to completion rather than stopping at the first bad card, so the caller
        /// gets one complete list to fix instead of discovering problems one request at a time.
        /// </summary>
        private static (IReadOnlyList<CardFileLine> Lines, IReadOnlyList<RejectedCardEntry> Rejections) ValidateCards(
            IReadOnlyList<CardFileEntry> cards,
            IReadOnlyDictionary<string, Product> productMap,
            IReadOnlyDictionary<string, Branch> branchMap)
        {
            var lines = new List<CardFileLine>(cards.Count);
            var rejections = new List<RejectedCardEntry>();
            var seenPans = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < cards.Count; index++)
            {
                // Annotated nullable deliberately: the list is deserialized from JSON, where a
                // null array element is entirely possible however the record is declared.
                CardFileEntry? card = cards[index];

                string pan = BatchFileFormat.NormalizePan(card?.ClearPan);
                string productName = card?.ProductName?.Trim() ?? string.Empty;
                string branchName = card?.BranchName?.Trim() ?? string.Empty;
                string maskedPan = PanMasker.Mask(pan);

                if (!BatchFileFormat.IsValidPan(pan))
                {
                    rejections.Add(new RejectedCardEntry(index, maskedPan, CardRejectionReason.InvalidPan));
                    continue;
                }

                if (!seenPans.Add(pan))
                {
                    rejections.Add(new RejectedCardEntry(index, maskedPan, CardRejectionReason.DuplicatePan));
                    continue;
                }

                if (productName.Length == 0 || branchName.Length == 0)
                {
                    rejections.Add(new RejectedCardEntry(index, maskedPan, CardRejectionReason.MissingField));
                    continue;
                }

                // A name carrying '|' or a line break would silently split the row into the wrong
                // number of fields, and the tenant would be the one to discover it.
                if (BatchFileFormat.ContainsForbiddenCharacter(productName) ||
                    BatchFileFormat.ContainsForbiddenCharacter(branchName))
                {
                    rejections.Add(new RejectedCardEntry(index, maskedPan, CardRejectionReason.ForbiddenCharacter));
                    continue;
                }

                if (!productMap.TryGetValue(productName, out Product? product))
                {
                    rejections.Add(new RejectedCardEntry(index, maskedPan, CardRejectionReason.UnknownProduct));
                    continue;
                }

                if (!branchMap.TryGetValue(branchName, out Branch? branch))
                {
                    rejections.Add(new RejectedCardEntry(index, maskedPan, CardRejectionReason.UnknownBranch));
                    continue;
                }

                // Write the canonical stored names, not the caller's input. This normalizes casing
                // and whitespace so the tenant-side lookup is guaranteed to resolve.
                lines.Add(new CardFileLine(pan, product.Name, branch.Name));
            }

            return (lines, rejections);
        }

        /// <summary>
        /// Builds a <c>.dat</c> file name from the tenant code. The extension is not cosmetic —
        /// the upload endpoint rejects anything else, so a file named here must be uploadable
        /// unchanged.
        /// </summary>
        private static string BuildFileName(string? tenantCode)
        {
            string safeCode = SanitizeForFileName(tenantCode);
            string timestamp = DateTime.UtcNow.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture);

            return $"{safeCode}-cards-{timestamp}{BatchFileFormat.FileExtension}";
        }

        // Tenant codes are URL-safe slugs by contract, but this file name reaches a
        // Content-Disposition header and a client file system, so it is filtered rather than
        // trusted.
        private static string SanitizeForFileName(string? tenantCode)
        {
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                return "tenant";
            }

            var builder = new StringBuilder(tenantCode.Length);

            foreach (char character in tenantCode)
            {
                if (char.IsLetterOrDigit(character) || character is '-' or '_')
                {
                    builder.Append(character);
                }
            }

            return builder.Length == 0 ? "tenant" : builder.ToString();
        }

        // Must match BatchUploadService.ComputeSha256Hex exactly: SHA-256 over the UTF-8 bytes of
        // the plaintext, uppercase hex. That equality is the whole point of returning FileMac.
        private static string ComputeSha256Hex(string content)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash);
        }

        // Aggregated counts per reason, for the log line. Deliberately not per-card: the log must
        // stay free of anything card-identifying.
        private static string SummarizeReasons(IReadOnlyList<RejectedCardEntry> rejections) =>
            string.Join(
                ", ",
                rejections.GroupBy(rejection => rejection.Reason)
                          .Select(group => $"{group.Key}={group.Count()}"));
    }
}
