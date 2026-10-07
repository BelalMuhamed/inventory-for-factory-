using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using InfrastructureLayer.Storage;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Print-image management use case: upload, retrieve, explicit replace, and the one-time
    /// legacy migration (revision, "Print Images &amp; Product Print Configuration" change
    /// request). Pairs <see cref="IPrintImageStorage"/> (physical I/O) with
    /// <see cref="IUnitOfWork.PrintImages"/> (metadata) — mirrors how <c>BatchUploadService</c>
    /// pairs its cipher and repository.
    /// </summary>
    public sealed class PrintImageService : IPrintImageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IPrintImageStorage _storage;
        private readonly ILogger<PrintImageService> _logger;

        public PrintImageService(
            IUnitOfWork unitOfWork,
            ICurrentTenant currentTenant,
            IPrintImageStorage storage,
            ILogger<PrintImageService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Result<UploadPrintImageResult>> UploadAsync(
            UploadPrintImageRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<UploadPrintImageResult>(PrintingErrors.PrintImageOnlySystemAdmin());
            }

            if (request?.File is null || request.File.Length == 0)
            {
                return Result.Failure<UploadPrintImageResult>(PrintingErrors.PrintImageFileMissing());
            }

            if (request.TenantId is not long targetTenantId)
            {
                return Result.Failure<UploadPrintImageResult>(PrintingErrors.PrintImageTenantRequired());
            }

            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(targetTenantId, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure<UploadPrintImageResult>(PrintingErrors.PrintImageTargetTenantNotFound(targetTenantId));
            }

            string originalFileName = Path.GetFileName(request.File.FileName);

            // Create-only: a duplicate is reported back, never silently replaced. Checked before
            // any physical write, so a duplicate costs nothing but a query.
            PrintImage? existing = await _unitOfWork.PrintImages.GetByOriginalFileNameAsync(
                targetTenantId, originalFileName, cancellationToken);
            if (existing is not null)
            {
                return Result.Success(new UploadPrintImageResult(false, MapToResponse(existing)));
            }

            string tenantFolder = FileSystemNameSanitizer.SanitizeTenantFolder(tenant.Username, tenant.Id);

            // Outside any transaction: a physical write cannot be rolled back by a DB transaction
            // regardless of where it's called from, so it is not nested inside one.
            Result<StoredImage> saveResult = await _storage.SaveAsync(tenantFolder, request.File, cancellationToken);
            if (saveResult.IsFailure)
            {
                return Result.Failure<UploadPrintImageResult>(saveResult.Error);
            }

            StoredImage saved = saveResult.Value;

            // Declared here, outside the transaction lambda, for the same reason as
            // PrinterConfigurationService.CreateAsync: printImage.Id is 0 until SaveChanges runs,
            // which happens after the lambda returns, so the response must be built from this
            // same instance after the transaction completes.
            var printImage = new PrintImage
            {
                TenantId = targetTenantId,
                OriginalFileName = originalFileName,
                StoredFileName = saved.StoredFileName,
                StoredPath = saved.StoredPath,
                ContentType = saved.ContentType,
                SizeBytes = saved.SizeBytes,
                UploadedAt = DateTime.UtcNow,
            };

            Result transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _unitOfWork.PrintImages.AddAsync(printImage, cancellationToken);
                return Result.Success();
            }, cancellationToken);

            if (transactionResult.IsFailure)
            {
                return Result.Failure<UploadPrintImageResult>(transactionResult.Error);
            }

            return Result.Success(new UploadPrintImageResult(true, MapToResponse(printImage)));
        }

        /// <inheritdoc />
        public async Task<Result<PrintImageResponse>> ReplaceAsync(
            long id, ReplacePrintImageRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<PrintImageResponse>(PrintingErrors.PrintImageOnlySystemAdmin());
            }

            PrintImage? existing = await _unitOfWork.PrintImages.GetByIdAsync(id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<PrintImageResponse>(PrintingErrors.PrintImageNotFound(id));
            }

            if (request?.File is null || request.File.Length == 0)
            {
                return Result.Failure<PrintImageResponse>(PrintingErrors.PrintImageFileMissing());
            }

            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(existing.TenantId, cancellationToken);
            if (tenant is null)
            {
                // The owning tenant existed when this image was uploaded; a hard failure here
                // would mean the tenant row itself is gone, which this service has no business
                // deciding how to handle — surface it as a not-found on the image rather than
                // inventing tenant-repair behavior.
                return Result.Failure<PrintImageResponse>(PrintingErrors.PrintImageNotFound(id));
            }

            string newOriginalFileName = Path.GetFileName(request.File.FileName);

            // Only check for a collision if the name is actually changing — replacing "front.png"
            // with new bytes still named "front.png" can never collide with itself.
            if (!string.Equals(newOriginalFileName, existing.OriginalFileName, StringComparison.Ordinal))
            {
                PrintImage? collision = await _unitOfWork.PrintImages.GetByOriginalFileNameAsync(
                    existing.TenantId, newOriginalFileName, cancellationToken);
                if (collision is not null && collision.Id != existing.Id)
                {
                    return Result.Failure<PrintImageResponse>(PrintingErrors.PrintImageNameConflict(newOriginalFileName));
                }
            }

            string tenantFolder = FileSystemNameSanitizer.SanitizeTenantFolder(tenant.Username, tenant.Id);
            string oldStoredPath = existing.StoredPath;

            Result<StoredImage> saveResult = await _storage.SaveAsync(tenantFolder, request.File, cancellationToken);
            if (saveResult.IsFailure)
            {
                return Result.Failure<PrintImageResponse>(saveResult.Error);
            }

            StoredImage saved = saveResult.Value;

            existing.OriginalFileName = newOriginalFileName;
            existing.StoredFileName = saved.StoredFileName;
            existing.StoredPath = saved.StoredPath;
            existing.ContentType = saved.ContentType;
            existing.SizeBytes = saved.SizeBytes;
            existing.UploadedAt = DateTime.UtcNow;
            _unitOfWork.PrintImages.Update(existing);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Only after the DB commit succeeds, and only if the physical path actually changed —
            // deleting it earlier and then failing to commit would leave the database pointing at
            // a file that no longer exists.
            if (!string.Equals(oldStoredPath, saved.StoredPath, StringComparison.Ordinal))
            {
                await _storage.DeleteAsync(oldStoredPath, cancellationToken);
            }

            return MapToResponse(existing);
        }

        /// <inheritdoc />
        public async Task<Result<PrintImageContent>> GetContentAsync(
            long id, CancellationToken cancellationToken = default)
        {
            PrintImage? image = await _unitOfWork.PrintImages.GetByIdAsync(id, cancellationToken);
            if (image is null)
            {
                return Result.Failure<PrintImageContent>(PrintingErrors.PrintImageNotFound(id));
            }

            if (!_currentTenant.IsSystemAdmin)
            {
                if (_currentTenant.TenantId is not long tenantId || image.TenantId != tenantId)
                {
                    // Same NotFound as a genuinely missing id — no existence leak across tenants.
                    return Result.Failure<PrintImageContent>(PrintingErrors.PrintImageNotFound(id));
                }
            }

            string physicalPath = _storage.GetPhysicalPath(image.StoredPath);
            if (!File.Exists(physicalPath))
            {
                _logger.LogError(
                    "PrintImage {Id} has no file at its stored path {StoredPath}", image.Id, image.StoredPath);
                return Result.Failure<PrintImageContent>(PrintingErrors.PrintImageNotFound(id));
            }

            return new PrintImageContent(physicalPath, image.ContentType, image.OriginalFileName);
        }

        /// <inheritdoc />
        public async Task<Result<MigrateLegacyImagesResult>> MigrateLegacyImagesAsync(
            CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<MigrateLegacyImagesResult>(PrintingErrors.PrintImageOnlySystemAdmin());
            }

            IReadOnlyList<PrintImage> allImages = await _unitOfWork.PrintImages.GetAllAsync(cancellationToken);

            int migrated = 0;
            int alreadyCurrent = 0;
            int failed = 0;
            var notes = new List<string>();

            foreach (PrintImage image in allImages)
            {
                Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(image.TenantId, cancellationToken);
                if (tenant is null)
                {
                    failed++;
                    notes.Add($"Image {image.Id}: owning tenant {image.TenantId} no longer exists.");
                    continue;
                }

                string newTenantFolder = FileSystemNameSanitizer.SanitizeTenantFolder(tenant.Username, tenant.Id);

                // "Already current" means the row already lives in the tenant's current-scheme
                // folder — checked by folder alone, not by recomputing and comparing the exact
                // expected file name. If an earlier run resolved a sanitization collision with a
                // numeric suffix (e.g. "front-2.png"), recomputing from OriginalFileName would
                // produce "front.png" again on a second run, which no longer matches this row's
                // actual name — but the row is still correctly migrated, just under a
                // disambiguated name. Comparing by folder avoids re-churning it every re-run.
                string currentFolder = GetFolderSegment(image.StoredPath);
                if (string.Equals(currentFolder, newTenantFolder, StringComparison.Ordinal))
                {
                    alreadyCurrent++;
                    continue;
                }

                string? newFileName = FileSystemNameSanitizer.SanitizeFileName(image.OriginalFileName);
                if (newFileName is null)
                {
                    failed++;
                    notes.Add($"Image {image.Id}: original file name '{image.OriginalFileName}' can no longer be sanitized.");
                    continue;
                }

                string newRelativePath = $"{newTenantFolder}/{newFileName}";

                bool moved = await _storage.MoveAsync(image.StoredPath, newRelativePath, cancellationToken);

                // A destination collision (rare: two different original names sanitizing to the
                // same string within the same tenant) is disambiguated with a numeric suffix
                // rather than aborting the run — this is a bulk, automatic, one-time operation,
                // not a single explicit action, so auto-resolving and reporting it is preferable
                // to halting on an edge case.
                for (int attempt = 2; !moved && attempt <= 5; attempt++)
                {
                    string candidateName = InsertSuffix(newFileName, attempt);
                    string candidatePath = $"{newTenantFolder}/{candidateName}";
                    moved = await _storage.MoveAsync(image.StoredPath, candidatePath, cancellationToken);
                    if (moved)
                    {
                        newFileName = candidateName;
                        newRelativePath = candidatePath;
                        notes.Add($"Image {image.Id}: renamed to '{candidateName}' to avoid a name collision after sanitization.");
                    }
                }

                if (!moved)
                {
                    failed++;
                    notes.Add($"Image {image.Id}: could not move to '{newRelativePath}' (source missing or destination still occupied).");
                    continue;
                }

                image.StoredFileName = newFileName;
                image.StoredPath = newRelativePath;
                _unitOfWork.PrintImages.Update(image);

                try
                {
                    // Saved per row rather than batched: each row's move is already durable on
                    // disk once MoveAsync returns, so its database update should be durable
                    // immediately too, independent of whether a later row in this run fails.
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    migrated++;
                }
                catch (Exception ex)
                {
                    failed++;
                    notes.Add($"Image {image.Id}: moved on disk but the database update failed ({ex.Message}).");
                }
            }

            return new MigrateLegacyImagesResult(migrated, alreadyCurrent, failed, notes);
        }

        private static string GetFolderSegment(string relativePath)
        {
            int slash = relativePath.IndexOf('/');
            return slash < 0 ? string.Empty : relativePath[..slash];
        }

        private static string InsertSuffix(string fileName, int suffix)
        {
            string extension = Path.GetExtension(fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            return $"{baseName}-{suffix}{extension}";
        }

        private static PrintImageResponse MapToResponse(PrintImage image) => new(
            image.Id, image.TenantId, image.OriginalFileName, image.ContentType, image.SizeBytes, image.UploadedAt);
    }
}
