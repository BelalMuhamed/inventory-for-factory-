using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Batches;
using ApplicationLayer.Errors;
using ApplicationLayer.Security;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Orchestrates the batch-upload pipeline (Batch Upload Phased Plan, Phase 6): decrypt →
    /// duplicate-file guard → parse/validate → resolve product/branch → one transaction for the
    /// stock/item/batch writes → failed-rows report. Ties together Phases 2–5.
    /// </summary>
    public sealed class BatchUploadService : IBatchUploadService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IBatchFileDecryptor _decryptor;
        private readonly IBatchRowParser _parser;
        private readonly IFailedRowsReportBuilder _reportBuilder;
        private readonly IPanFingerprintGenerator _fingerprintGenerator;
        private readonly ILogger<BatchUploadService> _logger;

        public BatchUploadService(
            IUnitOfWork unitOfWork,
            ICurrentTenant currentTenant,
            IBatchFileDecryptor decryptor,
            IBatchRowParser parser,
            IFailedRowsReportBuilder reportBuilder,
            IPanFingerprintGenerator fingerprintGenerator,
            ILogger<BatchUploadService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _decryptor = decryptor ?? throw new ArgumentNullException(nameof(decryptor));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));
            _fingerprintGenerator = fingerprintGenerator ?? throw new ArgumentNullException(nameof(fingerprintGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Result<BatchUploadResult>> UploadAsync(
            BatchUploadRequest request,
            FailedRowsReportLabels reportLabels,
            CancellationToken cancellationToken = default)
        {
            // Matches every other service's convention (ProductService/BranchService/
            // StockService): resolved here, not passed in by the caller. A system-admin token
            // has no tenant to upload cards for, so it is rejected rather than supported —
            // unlike ProductService.CreateAsync, this endpoint does not offer an "upload on
            // behalf of tenant X" path for admins.
            if (_currentTenant.TenantId is not long tenantId)
            {
                return Result.Failure<BatchUploadResult>(BatchErrors.ActorNotResolved());
            }

            // Cheapest possible rejection: before the stream is read, before any key derivation.
            // A wrong-extension or empty upload never touches the cipher.
            Result fileCheck = ValidateUploadedFile(request.File);
            if (fileCheck.IsFailure)
            {
                return Result.Failure<BatchUploadResult>(fileCheck.Error);
            }

            string originalFileName = SanitizeFileName(request.File.FileName);
            string? fileMac = null;

            try
            {
                // ---- Outside the transaction: reads only, no writes yet. ----------------------

                byte[] rawBytes = await ReadAllBytesAsync(request.File, cancellationToken);

                Result<string> decryptResult = _decryptor.Decrypt(tenantId, rawBytes);
                if (decryptResult.IsFailure)
                {
                    return Result.Failure<BatchUploadResult>(decryptResult.Error);
                }

                string content = decryptResult.Value;
                fileMac = ComputeSha256Hex(content);

                bool isDuplicate = await _unitOfWork.BatchRepo.ExistsByFileMacAsync(tenantId, fileMac, cancellationToken);
                if (isDuplicate)
                {
                    return Result.Failure<BatchUploadResult>(BatchErrors.DuplicateFile());
                }

                (IReadOnlyList<ParsedBatchRow> parsedRows, IReadOnlyList<FailedBatchRow> parserFailures) = _parser.Parse(content);

                int totalRowCount = parsedRows.Count + parserFailures.Count;
                if (totalRowCount == 0)
                {
                    return Result.Failure<BatchUploadResult>(BatchErrors.FileEmpty());
                }

                if (totalRowCount != request.ExpectedRowCount)
                {
                    return Result.Failure<BatchUploadResult>(
                        BatchErrors.ExpectedRowCountMismatch(request.ExpectedRowCount, totalRowCount));
                }

                // Three lookups total (not 3xN): product map, branch map, existing-PAN set.
                IReadOnlyDictionary<string, Product> productMap =
                    await _unitOfWork.Products.GetTenantMapAsync(tenantId, cancellationToken);
                IReadOnlyDictionary<string, Branch> branchMap =
                    await _unitOfWork.Branches.GetTenantMapAsync(tenantId, cancellationToken);

                // Identity/dedup is keyed on PanFingerprint, never on MaskedPan (PAN Storage
                // Redesign) — MaskedPan is display-only from this point on.
                List<byte[]> candidateFingerprints = parsedRows
                    .Select(r => _fingerprintGenerator.Fingerprint(tenantId, r.Pan))
                    .ToList();
                IReadOnlyDictionary<string, ProductItem> existingItems =
                    await _unitOfWork.ProductItems.GetExistingByFingerprintsAsync(tenantId, candidateFingerprints, cancellationToken);

                var failedRows = new List<FailedBatchRow>(parserFailures);
                var rowsToProcess = new List<(ParsedBatchRow Row, Product Product, Branch Branch, string MaskedPan, byte[] Fingerprint)>();

                foreach (ParsedBatchRow row in parsedRows)
                {
                    string maskedPan = PanMasker.Mask(row.Pan);
                    byte[] fingerprint = _fingerprintGenerator.Fingerprint(tenantId, row.Pan);

                    if (!productMap.TryGetValue(row.ProductName, out Product? product))
                    {
                        failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.UnknownProduct));
                        continue;
                    }

                    if (!branchMap.TryGetValue(row.BranchName, out Branch? branch))
                    {
                        failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.UnknownBranch));
                        continue;
                    }

                    // Transactions §4.10 (T0). A Known-way card that has left inventory, or that is
                    // committed to an in-flight transfer, must not be quietly rewritten by a
                    // re-upload. The re-sight below would otherwise reassign BranchID and reset
                    // Status to Available, stranding the transfer's hold quantity with no card
                    // behind it. Rejected per row so the rest of the file still imports, and
                    // reported to the uploader in the failed-rows workbook.
                    //
                    // Unknown Inventory Refactor (decisions Q1/Q2): for an Unknown-way product, a
                    // null BranchID is the card's normal resting state now - it sits unassigned in
                    // the tenant-wide pool until printed or disposed by PAN, not "in transit." That
                    // rejection therefore only still applies to Known-way cards.
                    if (existingItems.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? sightedItem))
                    {
                        if (sightedItem.Status == CardStatus.Disposed)
                        {
                            failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.CardDisposed));
                            continue;
                        }

                        if (sightedItem.BranchID is null && product.ProductTransactionWay == ProductTransactionWay.Known)
                        {
                            failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.CardInTransit));
                            continue;
                        }
                    }

                    rowsToProcess.Add((row, product, branch, maskedPan, fingerprint));
                }

                int importedCount = 0;

                // ---- Inside the transaction: every write for this batch, one commit. ----------
                Result transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var stockDeltas = new Dictionary<(long BranchId, long ProductId), int>();
                    var newItems = new List<ProductItem>();

                    var batch = new Batch
                    {
                        BankId = tenantId,
                        UploadedByTenantId = tenantId,
                        UploadedTime = DateTime.UtcNow,
                        Name = request.BatchName,
                        BatchCardAmount = request.ExpectedRowCount,
                        // Guaranteed non-null here: we only reach the transaction after the
                        // decrypt/duplicate-file checks above, both of which require fileMac to
                        // already be set. The compiler can't narrow a captured nullable local
                        // across this lambda boundary, hence the null-forgiving operator.
                        FileMac = fileMac!,
                        OriginalFileName = originalFileName,
                    };

                    foreach ((ParsedBatchRow row, Product product, Branch branch, string maskedPan, byte[] fingerprint) in rowsToProcess)
                    {
                        bool isUnknownWay = product.ProductTransactionWay == ProductTransactionWay.Unknown;

                        if (existingItems.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? existingItem))
                        {
                            // Re-sight (§6.4): update Branch/Status only. BatchId is left as the
                            // batch that first introduced the item — not reassigned here.
                            //
                            // Unknown Inventory Refactor (decisions Q1/Q2): an Unknown-way card's
                            // BranchID is never written here - it stays whatever it already was
                            // (null, for every card ingested under the new rule) and this re-sight
                            // only ever refreshes Status. Re-sighting an Unknown-way row therefore
                            // does not move Stock between branches at all; the named branch on the
                            // row is not consulted for an existing Unknown-way card.
                            //
                            // Known-way is unchanged: BranchID is non-null here (rows whose card is
                            // in transit or disposed were filtered into failedRows above and never
                            // reach rowsToProcess), pattern-matched rather than null-forgiven so a
                            // future change to that filter breaks loudly instead of silently.
                            if (!isUnknownWay && existingItem.BranchID is long currentBranchId && currentBranchId != branch.Id)
                            {
                                AddDelta(stockDeltas, currentBranchId, existingItem.ProductId, -1);
                                AddDelta(stockDeltas, branch.Id, product.Id, +1);
                                existingItem.BranchID = branch.Id;
                            }

                            existingItem.Status = CardStatus.Available;
                        }
                        else
                        {
                            // PAN Storage Redesign: the full PAN is never persisted in any form.
                            // MaskedPan is display-only; PanFingerprint is the sole identity/dedup
                            // key, computed once per row from the same normalized PAN.
                            //
                            // Unknown Inventory Refactor (decisions Q1/Q2): BranchID stays null for
                            // a new Unknown-way card - it is only ever pinned to a branch at print
                            // or disposal, keyed by PAN. The row's BranchName still names which
                            // branch's Stock entitlement is credited for the ingestion (decision
                            // Q6), even though it no longer sets the card's physical location.
                            var newItem = new ProductItem
                            {
                                PanFingerprint = fingerprint,
                                MaskedPan = maskedPan,
                                TenantId = tenantId,
                                ProductId = product.Id,
                                BranchID = isUnknownWay ? null : branch.Id,
                                Status = CardStatus.Available,
                                Batch = batch, // relationship fixup populates BatchId on save
                            };
                            newItems.Add(newItem);
                            AddDelta(stockDeltas, branch.Id, product.Id, +1);
                        }

                        importedCount++;
                    }

                    await _unitOfWork.ProductItems.AddRangeAsync(newItems, cancellationToken);

                    foreach (KeyValuePair<(long BranchId, long ProductId), int> delta in stockDeltas)
                    {
                        if (delta.Value == 0)
                        {
                            continue;
                        }

                        Stock stock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                            tenantId, delta.Key.BranchId, delta.Key.ProductId, cancellationToken);
                        stock.AvailableQuantity += delta.Value;
                    }

                    batch.ProcessedRowCount = importedCount;
                    batch.BatchStatus =
                        failedRows.Count == 0 ? UploadStatus.Succeeded :
                        importedCount == 0 ? UploadStatus.Failed :
                        UploadStatus.PartialSuccess;

                    await _unitOfWork.BatchRepo.AddAsync(batch, cancellationToken);

                    return Result.Success();
                }, cancellationToken);

                if (transactionResult.IsFailure)
                {
                    return Result.Failure<BatchUploadResult>(transactionResult.Error);
                }

                // ---- Outside the transaction again: build the report only if needed. ----------
                string? reportFileName = null;
                string? reportBase64 = null;

                if (failedRows.Count > 0)
                {
                    byte[] reportBytes = _reportBuilder.Build(failedRows, reportLabels);
                    reportBase64 = Convert.ToBase64String(reportBytes);
                    reportFileName = $"batch-upload-failures-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                }

                var result = new BatchUploadResult(importedCount, failedRows.Count, reportFileName, reportBase64);
                return Result.Success(result);
            }
            catch (Exception ex)
            {
                // Never swallowed: logged here, with the context this call site has, before
                // surfacing the opaque, client-safe failure. Deliberately does NOT persist a
                // Batch row for this path (unlike the plan's literal "mark Batch Failed") — doing
                // so would record fileMac against a batch that never actually completed, which
                // would permanently block the user from retrying the identical file after what
                // may be a transient failure. See the Phase 6 patch notes.
                _logger.LogError(
                    ex,
                    "Unexpected failure processing batch upload. TenantId={TenantId} BatchName={BatchName} FileMac={FileMac}",
                    tenantId, request.BatchName, fileMac);

                return Result.Failure<BatchUploadResult>(BatchErrors.ProcessingFailed());
            }
        }

        /// <summary>
        /// Pre-flight guard on the uploaded file (Card File Generation, Phase 9.7): a file must be
        /// present, non-empty, and named <c>*.dat</c>.
        /// <para>
        /// Enforced here rather than as a model-validation attribute for two reasons: the failure
        /// has to travel the same localized <see cref="Result"/> path as every other batch error,
        /// and the sanitized name it produces is what gets persisted as
        /// <c>Batch.OriginalFileName</c>.
        /// </para>
        /// </summary>
        private static Result ValidateUploadedFile(IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                return Result.Failure(BatchErrors.FileMissing());
            }

            string fileName = SanitizeFileName(file.FileName);
            string extension = Path.GetExtension(fileName);

            if (!string.Equals(extension, BatchFileFormat.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(BatchErrors.InvalidFileExtension());
            }

            return Result.Success();
        }

        // The client controls FileName entirely, and it lands in the database. Path.GetFileName
        // strips any directory component a hostile or careless client attaches ("..\..\x.dat"),
        // so both the extension check and the stored value work on a bare name.
        private static string SanitizeFileName(string? fileName) =>
            Path.GetFileName(fileName ?? string.Empty);

        private static void AddDelta(Dictionary<(long BranchId, long ProductId), int> deltas, long branchId, long productId, int amount)
        {
            (long BranchId, long ProductId) key = (branchId, productId);
            deltas[key] = deltas.GetValueOrDefault(key) + amount;
        }

        private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }

        private static string ComputeSha256Hex(string content)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash);
        }
    }
}
