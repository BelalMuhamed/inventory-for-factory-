using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Stable, localizable <see cref="Error"/> catalogue for the Printing module: the printer
    /// registry (ERD §6), product print configurations (ERD §7), and print-image uploads (module
    /// requirements §5–§7).
    /// </summary>
    public static class PrintingErrors
    {
        // =================================================================================
        //  Printers (ERD §6, decisions Q-01/Q-09)
        // =================================================================================

        /// <summary>The caller's principal could not be resolved to a tenant (→ 401).</summary>
        public static Error PrinterActorNotResolved() =>
            Error.Unauthorized("Printer.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>A tenant caller attempted to create, update, delete, or restore a printer (→ 403, decision Q-09).</summary>
        public static Error PrinterOnlySystemAdmin() =>
            Error.Forbidden("Printer.OnlySystemAdmin",
                "Only a system administrator can create, update, delete, or restore printers.");

        /// <summary>No printer with that id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error PrinterNotFound(long id) =>
            Error.NotFound("Printer.NotFound", $"No printer was found with id {id}.").WithArg(id.ToString());

        /// <summary>A system-admin create call did not supply a target tenant (→ 422).</summary>
        public static Error PrinterTenantRequired() =>
            Error.Validation("Printer.TenantRequired",
                "A target tenant id is required when registering a printer as a system administrator.");

        /// <summary>The supplied target tenant does not exist (→ 422).</summary>
        public static Error PrinterTargetTenantNotFound(long tenantId) =>
            Error.Validation("Printer.TargetTenantNotFound", $"No tenant exists with id {tenantId}.")
                .WithArg(tenantId.ToString());

        /// <summary>The branch does not exist, or belongs to another tenant (→ 404).</summary>
        public static Error PrinterBranchNotFound(long branchId) =>
            Error.NotFound("Printer.BranchNotFound", $"No branch was found with id {branchId}.")
                .WithArg(branchId.ToString());

        /// <summary>A deleted branch cannot have a printer registered to it (→ 422).</summary>
        public static Error PrinterBranchDeleted(long branchId) =>
            Error.Validation("Printer.BranchDeleted",
                $"Branch {branchId} is deleted and cannot have a printer registered to it.")
                .WithArg(branchId.ToString());

        /// <summary>Another non-deleted printer for this tenant already has this serial/IP (→ 409).</summary>
        public static Error PrinterDuplicateUniqueNumber(string uniqueNumber) =>
            Error.Conflict("Printer.DuplicateUniqueNumber",
                $"A printer with serial/IP '{uniqueNumber}' is already registered for this tenant.")
                .WithArg(uniqueNumber);

        /// <summary>The printer is already soft-deleted (→ 409).</summary>
        public static Error PrinterAlreadyDeleted(long id) =>
            Error.Conflict("Printer.AlreadyDeleted", $"Printer {id} is already deleted.").WithArg(id.ToString());

        /// <summary>The printer is not deleted, so it cannot be restored (→ 409).</summary>
        public static Error PrinterNotDeleted(long id) =>
            Error.Conflict("Printer.NotDeleted", $"Printer {id} is not deleted.").WithArg(id.ToString());

        /// <summary>A Matica printer was registered or updated without its machine configuration (→ 422, module requirement §1).</summary>
        public static Error MaticaPrinterConfigRequired() =>
            Error.Validation("Printer.MaticaConfigRequired",
                "A Matica printer requires its machine configuration (feeder, hopper, reject bin, port).");

        /// <summary>An Evolis printer was registered or updated with a Matica machine configuration (→ 422, module requirement §1).</summary>
        public static Error MaticaPrinterConfigNotApplicable() =>
            Error.Validation("Printer.MaticaConfigNotApplicable",
                "An Evolis printer does not accept a Matica machine configuration.");

        // =================================================================================
        //  Product print configuration (ERD §7, decisions Q-02/Q-03/Q-04/Q-05/Q-07/Q-08/Q-09)
        // =================================================================================

        /// <summary>The caller's principal could not be resolved to a tenant (→ 401).</summary>
        public static Error ProductPrintConfigActorNotResolved() =>
            Error.Unauthorized("ProductPrintConfig.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>A tenant caller attempted to create or update a product's print configuration (→ 403, decision Q-09, confirmed).</summary>
        public static Error ProductPrintConfigOnlySystemAdmin() =>
            Error.Forbidden("ProductPrintConfig.OnlySystemAdmin",
                "Only a system administrator can create or update a product's print configuration.");

        /// <summary>The product has no print configuration row yet (→ 404). Not expected under the single-aggregate design; defensive.</summary>
        public static Error ProductPrintConfigNotFound(long productId) =>
            Error.NotFound("ProductPrintConfig.NotFound",
                $"Product {productId} has no print configuration.")
                .WithArg(productId.ToString());

        /// <summary>The payload's printer type is Matica, but no Matica payload was supplied (→ 422).</summary>
        public static Error ProductPrintConfigMaticaPayloadRequired() =>
            Error.Validation("ProductPrintConfig.MaticaPayloadRequired",
                "A Matica print configuration payload is required when the printer type is Matica.");

        /// <summary>A Matica payload was supplied for an Evolis printer type (→ 422).</summary>
        public static Error ProductPrintConfigMaticaPayloadNotApplicable() =>
            Error.Validation("ProductPrintConfig.MaticaPayloadNotApplicable",
                "A Matica print configuration payload does not apply when the printer type is Evolis.");

        /// <summary>The payload's printer type is Evolis, but no Evolis payload was supplied (→ 422).</summary>
        public static Error ProductPrintConfigEvolisPayloadRequired() =>
            Error.Validation("ProductPrintConfig.EvolisPayloadRequired",
                "An Evolis print configuration payload is required when the printer type is Evolis.");

        /// <summary>An Evolis payload was supplied for a Matica printer type (→ 422).</summary>
        public static Error ProductPrintConfigEvolisPayloadNotApplicable() =>
            Error.Validation("ProductPrintConfig.EvolisPayloadNotApplicable",
                "An Evolis print configuration payload does not apply when the printer type is Matica.");

        /// <summary>The supplied ribbon type does not exist (→ 422, decision Q-05).</summary>
        public static Error ProductPrintConfigRibbonTypeNotFound(long ribbonTypeId) =>
            Error.Validation("ProductPrintConfig.RibbonTypeNotFound",
                $"No ribbon type exists with id {ribbonTypeId}.")
                .WithArg(ribbonTypeId.ToString());

        /// <summary>
        /// <c>PrintColor</c> or <c>BackgroundColor</c> is not a valid HEX value (→ 422, module
        /// requirement §3: 6 or 8 hex digits after '#').
        /// </summary>
        public static Error ProductPrintConfigInvalidHexColor(string value) =>
            Error.Validation("ProductPrintConfig.InvalidHexColor",
                $"'{value}' is not a valid HEX color. Use the form #RRGGBB or #RRGGBBAA.")
                .WithArg(value);

        // =================================================================================
        //  Print images (module requirements §5–§7; revised for admin-only upload, ImageId
        //  references, and explicit replace)
        // =================================================================================

        /// <summary>A tenant caller attempted to upload, replace, or migrate print images (→ 403). Revised: upload is now system-admin only, reversed from the original tenant-only design.</summary>
        public static Error PrintImageOnlySystemAdmin() =>
            Error.Forbidden("PrintImage.OnlySystemAdmin",
                "Only a system administrator can upload, replace, or migrate print images.");

        /// <summary>An upload did not supply a target tenant id (→ 422). The admin caller has no tenant of their own to infer one from.</summary>
        public static Error PrintImageTenantRequired() =>
            Error.Validation("PrintImage.TenantRequired",
                "A target tenant id is required when uploading a print image as a system administrator.");

        /// <summary>The supplied target tenant does not exist (→ 422).</summary>
        public static Error PrintImageTargetTenantNotFound(long tenantId) =>
            Error.Validation("PrintImage.TargetTenantNotFound", $"No tenant exists with id {tenantId}.")
                .WithArg(tenantId.ToString());

        /// <summary>No print image with that id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error PrintImageNotFound(long id) =>
            Error.NotFound("PrintImage.NotFound", $"No print image was found with id {id}.").WithArg(id.ToString());

        /// <summary>No file was supplied, or the supplied file has zero bytes (→ 422).</summary>
        public static Error PrintImageFileMissing() =>
            Error.Validation("PrintImage.FileMissing", "No image file was supplied.");

        /// <summary>The uploaded file exceeds the configured maximum size (→ 422).</summary>
        public static Error PrintImageFileTooLarge(long maxSizeBytes) =>
            Error.Validation("PrintImage.FileTooLarge",
                $"The image exceeds the maximum allowed size of {maxSizeBytes} bytes.")
                .WithArg(maxSizeBytes.ToString());

        /// <summary>The uploaded file's extension is not on the allowed list (→ 422).</summary>
        public static Error PrintImageInvalidExtension() =>
            Error.Validation("PrintImage.InvalidExtension", "This image file type is not supported.");

        /// <summary>
        /// The file's actual content, detected from its magic bytes, does not match a supported
        /// image format (→ 422) — the client-supplied extension and Content-Type are not trusted
        /// on their own.
        /// </summary>
        public static Error PrintImageUnsupportedContent() =>
            Error.Validation("PrintImage.UnsupportedContent",
                "The uploaded file's content does not match a supported image format.");

        /// <summary>
        /// The client-supplied file name could not be made filesystem-safe (→ 422) — sanitization
        /// stripped it down to nothing (e.g. a name built entirely from invalid characters).
        /// </summary>
        public static Error PrintImageInvalidFileName() =>
            Error.Validation("PrintImage.InvalidFileName",
                "The file name could not be used. Please rename the file and try again.");

        /// <summary>
        /// Replacing an image (<c>PUT /api/print-images/{id}</c>) under a new name collides with a
        /// different, existing image for the same tenant (→ 409).
        /// </summary>
        public static Error PrintImageNameConflict(string fileName) =>
            Error.Conflict("PrintImage.NameConflict",
                $"Another image named '{fileName}' already exists for this tenant.")
                .WithArg(fileName);

        /// <summary>The image could not be saved to disk (→ 500). Not caller-driven; logged with full context.</summary>
        public static Error PrintImageSaveFailed() =>
            Error.Internal("PrintImage.SaveFailed", "The image could not be saved. Please try again.");

        /// <summary>
        /// The print configuration references an <c>ImageId</c> that does not exist, or belongs
        /// to a different tenant (→ 422). Mirrors <see cref="ProductPrintConfigRibbonTypeNotFound"/>'s
        /// existence-check pattern.
        /// </summary>
        public static Error ProductPrintConfigImageNotFound(long imageId) =>
            Error.Validation("ProductPrintConfig.ImageNotFound", $"No print image exists with id {imageId}.")
                .WithArg(imageId.ToString());
        /// <summary>A Matica printer was registered or updated with an Evolis machine configuration (→ 422, decision Q-15/R-10).</summary>
        public static Error EvolisPrinterConfigNotApplicable() =>
            Error.Validation("Printer.EvolisConfigNotApplicable",
                "A Matica printer does not accept an Evolis machine configuration.");

        /// <summary>An Evolis printer was registered or updated without its machine configuration (→ 422, decision Q-15).</summary>
        public static Error EvolisPrinterConfigRequired() =>
            Error.Validation("Printer.EvolisConfigRequired",
                "An Evolis printer requires its machine configuration (device name).");

        /// <summary>
        /// <c>EvolisConfig.DeviceName</c> was missing, blank, or exceeded 200 characters (→ 422).
        /// This is the SDK connection key (<c>Evolis.Device.name</c>) — without it the Agent
        /// cannot open a connection to the printer at all.
        /// </summary>
        public static Error DeviceNameRequired() =>
            Error.Validation("Printer.DeviceNameRequired",
                "A device name is required and must be 200 characters or fewer.");

        /// <summary>The supplied default ribbon type does not exist (→ 422).</summary>
        public static Error DefaultRibbonTypeNotFound(long ribbonTypeId) =>
            Error.Validation("Printer.DefaultRibbonTypeNotFound",
                $"No ribbon type exists with id {ribbonTypeId}.")
                .WithArg(ribbonTypeId.ToString());

        /// <summary>
        /// Another non-deleted Evolis printer on the same supervising machine already registered
        /// this device name (→ 409). Enforced by
        /// <c>UX_EvolisPrinterConfigurations_TenantId_SupervisorHostName_DeviceName</c> — a given
        /// device name may legitimately repeat across different supervising machines (e.g. a
        /// staging box reusing a name), but not on the same one. This can only be tripped by
        /// Phase 2's self-registration route, never by this phase's create/update endpoints —
        /// <c>SupervisorHostName</c> is not accepted here and is always null at that point.
        /// </summary>
        public static Error DuplicateDeviceNameOnHost(string deviceName, string hostName) =>
            Error.Conflict("Printer.DuplicateDeviceNameOnHost",
                $"Device '{deviceName}' is already registered on host '{hostName}'.")
                .WithArg(deviceName).WithArg(hostName);

        /// <summary>
        /// The supervisor-registration request's <c>BranchId</c> does not match the authenticated
        /// service token's own <c>branchId</c> claim (→ 403). Same defense-in-depth reasoning as
        /// <c>PrintFlowController.IsOutsideTokenScope</c>: a leaked or reused token cannot be
        /// pointed at a different branch just by editing the request body.
        /// </summary>
        public static Error SupervisorBranchMismatch() =>
            Error.Forbidden("Printer.SupervisorBranchMismatch",
                "The request's branch does not match the authenticated service account's branch.");

        /// <summary>The supervisor-registration request's <c>HostName</c> was missing or blank (→ 422).</summary>
        public static Error SupervisorHostNameRequired() =>
            Error.Validation("Printer.SupervisorHostNameRequired",
                "A host name is required.");
    }
}
