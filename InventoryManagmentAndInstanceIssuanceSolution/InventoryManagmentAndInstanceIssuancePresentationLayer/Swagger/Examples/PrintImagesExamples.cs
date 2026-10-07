using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>PrintImagesController</c> (<c>api/print-images/*</c>). Bodies
    /// mirror <c>PrintingDtos.cs</c>/<c>IPrintImageService.cs</c> and the outcomes actually
    /// returned by <c>PrintImageService</c> (InfrastructureLayer/Services/PrintImageService.cs)
    /// and <c>PrintingErrors</c>. <c>Upload</c> and <c>Replace</c> take
    /// <c>multipart/form-data</c> (a file field plus, for Upload, a <c>TenantId</c> field) — no
    /// request-body examples are registered for them, since a file field can't be meaningfully
    /// represented as inline example data; Swagger UI already renders the form fields with a
    /// native file picker for a <c>multipart/form-data</c> operation.
    /// </summary>
    internal static class PrintImagesExamples
    {
        private static readonly object SampleImage = new
        {
            id = 9,
            tenantId = 42,
            originalFileName = "gold-card-front.png",
            contentType = "image/png",
            sizeBytes = 184320,
            uploadedAt = "2026-03-01T12:00:00Z"
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("PrintImagesController", "Upload")] = Upload(),
                [new EndpointKey("PrintImagesController", "Replace")] = Replace(),
                [new EndpointKey("PrintImagesController", "Get")] = Get(),
                [new EndpointKey("PrintImagesController", "MigrateLegacyStorage")] = MigrateLegacyStorage()
            };

        private static EndpointExampleSet Upload() => new EndpointExampleSetBuilder()
            .Response(200, "created", "A new image was saved. Form fields: file (the image), tenantId (required — the admin caller has no tenant of its own to infer one from).",
                new { success = true, data = SampleImage, error = (object?)null })
            .Response(409, "alreadyExists", "Create-only behavior: a non-deleted image with this " +
                "exact original file name already exists for the target tenant, so nothing was " +
                "saved. Unusually for a 409 in this API, the body is a normal success envelope " +
                "carrying the EXISTING image's metadata, not an ApiError — use the id in data to " +
                "call PUT /api/print-images/{id} if you want to replace it explicitly.",
                new { success = true, data = SampleImage, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller attempted to upload an image — upload " +
                "was reversed to system-admin-only in the Print Images revision.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.OnlySystemAdmin", message = "Only a system administrator can upload, replace, or migrate print images.", category = "Forbidden" }
                })
            .Response(422, "unsupportedContent", "The file's actual content (magic bytes) doesn't " +
                "match a supported image format — the client-supplied extension/Content-Type alone isn't trusted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.UnsupportedContent", message = "The uploaded file's content does not match a supported image format.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Replace() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The image's content was replaced in place — same id, new bytes. " +
                "Any print configuration already referencing this ImageId is unaffected.",
                new { success = true, data = SampleImage, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller attempted to replace an image.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.OnlySystemAdmin", message = "Only a system administrator can upload, replace, or migrate print images.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No print image exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.NotFound", message = "No print image was found with id 999.", category = "NotFound" }
                })
            .Response(409, "nameConflict", "The replacement file's name collides with a different, " +
                "existing image for the same tenant (this is a genuine ApiError, unlike Upload's 409).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.NameConflict", message = "Another image named 'gold-card-front.png' already exists for this tenant.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Get() => new EndpointExampleSetBuilder()
            // 200 is the raw image bytes (image/png or image/jpeg), not a JSON envelope — no
            // example is registered for it; there's nothing meaningful to show as inline JSON.
            .Response(404, "notFound", "No print image exists with that id, or it belongs to another tenant.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.NotFound", message = "No print image was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet MigrateLegacyStorage() => new EndpointExampleSetBuilder()
            .Response(200, "success", "Migration run summary. Safe to call more than once — " +
                "already-migrated rows are left alone and counted under alreadyCurrent.",
                new
                {
                    success = true,
                    data = new
                    {
                        migrated = 12,
                        alreadyCurrent = 340,
                        failed = 0,
                        notes = new[] { "Renamed 'front.png' to 'front (1).png' to avoid a collision for tenant 42." }
                    },
                    error = (object?)null
                })
            .Response(403, "tenantAttempted", "A tenant caller attempted to trigger the migration.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "PrintImage.OnlySystemAdmin", message = "Only a system administrator can upload, replace, or migrate print images.", category = "Forbidden" }
                })
            .Build();
    }
}
