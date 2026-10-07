using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using DomainLayer.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Storage
{
    /// <summary>
    /// Local-disk <see cref="IPrintImageStorage"/> implementation. This is the first component in
    /// this codebase that writes application content to the filesystem (everything else — card
    /// files, batch reports — stays in memory or streams straight to the HTTP response), so it
    /// owns the full validate-then-save contract itself rather than following an existing
    /// file-I/O precedent.
    /// <para>
    /// <b>Revision:</b> the physical file name is now the sanitized original client-supplied
    /// name, not a generated GUID, and the tenant subdirectory is named after the tenant's
    /// sanitized username, not their numeric id — see <see cref="FileSystemNameSanitizer"/>.
    /// </para>
    /// </summary>
    public sealed class LocalDiskPrintImageStorage : IPrintImageStorage
    {
        /// <summary>
        /// Known image-format signatures ("magic bytes"), keyed by the extension they must be
        /// declared under. Deliberately hardcoded, not driven by
        /// <see cref="PrintImageOptions.AllowedExtensions"/> — an operator adding an extension to
        /// configuration without a matching entry here simply can never pass content validation
        /// (fails closed), rather than silently trusting an unverified format.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, byte[]> SignaturesByExtension =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
                // JPEG's SOI + APPn marker bytes are constant across every JFIF/Exif variant;
                // the marker segments that follow differ, so only these three bytes are checked.
                [".jpg"] = new byte[] { 0xFF, 0xD8, 0xFF },
                [".jpeg"] = new byte[] { 0xFF, 0xD8, 0xFF },
            };

        private static readonly IReadOnlyDictionary<string, string> ContentTypesByExtension =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = "image/png",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
            };

        private readonly PrintImageOptions _options;
        private readonly string _physicalRoot;
        private readonly ILogger<LocalDiskPrintImageStorage> _logger;

        public LocalDiskPrintImageStorage(
            IOptions<PrintImageOptions> options, IHostEnvironment environment, ILogger<LocalDiskPrintImageStorage> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _physicalRoot = ResolvePhysicalRoot(environment, _options);
        }

        /// <summary>
        /// Resolves <see cref="PrintImageOptions.RootPath"/> to an absolute path, relative to
        /// <paramref name="environment"/>'s content root when not already absolute — the same
        /// base Program.cs already resolves <c>LogFileOptions.Directory</c> against.
        /// </summary>
        public static string ResolvePhysicalRoot(IHostEnvironment environment, PrintImageOptions options) =>
            Path.IsPathRooted(options.RootPath)
                ? options.RootPath
                : Path.Combine(environment.ContentRootPath, options.RootPath);

        /// <inheritdoc />
        public async Task<Result<StoredImage>> SaveAsync(
            string tenantFolder, IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0)
            {
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageFileMissing());
            }

            if (file.Length > _options.MaxSizeBytes)
            {
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageFileTooLarge(_options.MaxSizeBytes));
            }

            // Path.GetFileName strips any directory component a hostile or careless client
            // attaches, matching BatchUploadService.SanitizeFileName's exact reasoning.
            string clientFileName = Path.GetFileName(file.FileName);
            string extension = Path.GetExtension(clientFileName);
            bool isAllowedExtension = !string.IsNullOrEmpty(extension) &&
                Array.Exists(_options.AllowedExtensions, e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));

            if (!isAllowedExtension)
            {
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageInvalidExtension());
            }

            byte[] bytes = await ReadAllBytesAsync(file, cancellationToken);

            if (!MatchesSignature(bytes, extension))
            {
                // Covers both "extension lies about the content" and "extension has no known
                // signature at all" (an operator-added AllowedExtensions entry with nothing in
                // SignaturesByExtension) — both fail the same way, on purpose.
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageUnsupportedContent());
            }

            string? sanitizedFileName = FileSystemNameSanitizer.SanitizeFileName(clientFileName);
            if (sanitizedFileName is null)
            {
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageInvalidFileName());
            }

            string physicalDirectory = Path.Combine(_physicalRoot, tenantFolder);
            string physicalPath = Path.Combine(physicalDirectory, sanitizedFileName);
            // Forward slashes always, regardless of OS: this is stored as PrintImage.StoredPath
            // and later re-resolved by GetPhysicalPath — never treated as a URL, since images are
            // no longer served as static files.
            string storedPath = $"{tenantFolder}/{sanitizedFileName}";

            // Defensive: the caller (PrintImageService) already checked for a duplicate
            // OriginalFileName in the database before calling this, but sanitization could in
            // principle collapse two different original names to the same physical name (e.g.
            // "café.png" and "cafe.png") — the database's uniqueness constraint is on the
            // unsanitized name, so it would not catch that. Refuse to silently overwrite.
            if (File.Exists(physicalPath))
            {
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageNameConflict(sanitizedFileName));
            }

            try
            {
                Directory.CreateDirectory(physicalDirectory);
                await File.WriteAllBytesAsync(physicalPath, bytes, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Failed to save print image under tenant folder {TenantFolder}", tenantFolder);
                return Result.Failure<StoredImage>(PrintingErrors.PrintImageSaveFailed());
            }

            string contentType = ContentTypesByExtension[extension.ToLowerInvariant()];
            return Result.Success(new StoredImage(sanitizedFileName, storedPath, contentType, bytes.LongLength));
        }

        /// <inheritdoc />
        public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return Task.CompletedTask;
            }

            string physicalPath = GetPhysicalPath(storedPath);

            try
            {
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort by contract (see the interface doc comment): logged, never thrown.
                _logger.LogWarning(ex, "Failed to delete print image at {StoredPath}", storedPath);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public string GetPhysicalPath(string storedPath) =>
            // storedPath is always a value this class generated (or the migration produced) and
            // the caller read back from PrintImage.StoredPath — never client input — so no
            // path-traversal guard is needed beyond the normal Path.Combine behavior.
            Path.Combine(_physicalRoot, storedPath.Replace('/', Path.DirectorySeparatorChar));

        /// <inheritdoc />
        public Task<bool> MoveAsync(
            string oldRelativePath, string newRelativePath, CancellationToken cancellationToken = default)
        {
            string oldPhysicalPath = GetPhysicalPath(oldRelativePath);
            string newPhysicalPath = GetPhysicalPath(newRelativePath);

            if (!File.Exists(oldPhysicalPath) || File.Exists(newPhysicalPath))
            {
                return Task.FromResult(false);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newPhysicalPath)!);
                File.Move(oldPhysicalPath, newPhysicalPath);
                return Task.FromResult(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to move print image from {Old} to {New}", oldRelativePath, newRelativePath);
                return Task.FromResult(false);
            }
        }

        private static bool MatchesSignature(byte[] bytes, string extension)
        {
            if (!SignaturesByExtension.TryGetValue(extension, out byte[]? signature))
            {
                return false;
            }

            return bytes.Length >= signature.Length &&
                   bytes.AsSpan(0, signature.Length).SequenceEqual(signature);
        }

        private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
    }
}
