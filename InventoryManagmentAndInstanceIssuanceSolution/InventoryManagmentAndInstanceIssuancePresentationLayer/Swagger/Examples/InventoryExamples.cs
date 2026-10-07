using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>InventoryController</c> (<c>api/inventory/upload</c>). Bodies
    /// mirror <c>BatchDtos.cs</c> and the outcomes actually returned by <c>BatchUploadService</c>
    /// (InfrastructureLayer/Services/BatchUploadService.cs) and <c>BatchErrors</c>. Takes
    /// <c>multipart/form-data</c> (the encrypted file plus <c>BatchName</c>/<c>ExpectedRowCount</c>
    /// fields) — no request-body example is registered, since a file field can't be meaningfully
    /// represented as inline example data.
    /// </summary>
    internal static class InventoryExamples
    {
        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("InventoryController", "Upload")] = Upload()
            };

        private static EndpointExampleSet Upload() => new EndpointExampleSetBuilder()
            .Response(200, "allImported", "Every row imported or updated successfully — no failed-rows report.",
                new
                {
                    success = true,
                    data = new
                    {
                        importedCount = 500,
                        failedCount = 0,
                        failureReportFileName = (string?)null,
                        failureReportBase64 = (string?)null
                    },
                    error = (object?)null
                })
            .Response(200, "partialFailure", "Some rows failed (unknown product/branch, invalid " +
                "PAN, etc.) — the whole upload still succeeds with 200; failed rows never fail the " +
                "batch, they're collected into an Excel report instead.",
                new
                {
                    success = true,
                    data = new
                    {
                        importedCount = 487,
                        failedCount = 13,
                        failureReportFileName = "spring-batch-2026-08-12-failed-rows.xlsx",
                        failureReportBase64 = "UEsDBBQAAAAIAA=="
                    },
                    error = (object?)null
                })
            .Response(401, "actorNotResolved", "The caller has no resolvable tenant context — e.g. " +
                "a system-admin token, which this endpoint doesn't support since there's no tenant " +
                "to upload cards for. Distinct from the far more common empty-body case (no/invalid " +
                "token), which the controller-level 401 covers.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Batch.ActorNotResolved", message = "The acting principal could not be resolved.", category = "Unauthorized" }
                })
            .Response(409, "duplicateFile", "A file with this exact fingerprint (FileMac) was " +
                "already uploaded for this tenant — no rows are written on a duplicate.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Batch.DuplicateFile", message = "This exact file has already been uploaded.", category = "Conflict" }
                })
            .Response(422, "rowCountMismatch", "The caller-declared ExpectedRowCount doesn't match " +
                "the file's actual row count — the whole file is rejected before any row is processed.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Batch.ExpectedRowCountMismatch", message = "Expected 500 rows but the file contains 487.", category = "Validation" }
                })
            .Response(422, "decryptionFailed", "The file could not be authenticated/decrypted — " +
                "wrong key, or corrupted/tampered ciphertext (AES-GCM tag check failed). A whole-file " +
                "failure, not a per-row one.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Batch.DecryptionFailed", message = "The file could not be decrypted. It may be corrupted or encrypted with the wrong key.", category = "Validation" }
                })
            .Build();
    }
}
