using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>PrintersController</c> (<c>api/printers/*</c>). Bodies mirror
    /// <c>PrintingDtos.cs</c> and the outcomes actually returned by
    /// <c>PrinterConfigurationService</c> (InfrastructureLayer/Services/PrinterConfigurationService.cs)
    /// and <c>PrintingErrors</c>. Every write here is system-admin only (decision Q-09), enforced
    /// in the service (not via an authorization policy) — so unlike <c>TenantsController</c>, the
    /// 403 on this module <em>is</em> the standard enveloped body, not an empty one.
    /// </summary>
    internal static class PrintersExamples
    {
        private static readonly object SampleMaticaPrinter = new
        {
            id = 3,
            tenantId = 42,
            branchId = 7,
            branchName = "Downtown Branch",
            usingPrinterType = 0,
            name = "Matica XID8300 - Front Desk",
            model = "XID8300",
            uniqueNumber = "MTC-0007",
            maticaConfig = new { feederId = 1, hopperId = 2, rejectedId = 3, port = "COM3" },
            isDeleted = false,
            createdAt = "2026-01-05T08:00:00Z",
            updatedAt = (string?)null,
            deletedAt = (string?)null
        };
        private static readonly object SampleEvolisPrinter = new
        {
            id = 16,
            tenantId = 42,
            branchId = 3,
            branchName = "Main Branch",
            usingPrinterType = 2, // Evolis
            name = "Front Desk Evolis",
            model = "Evolis Primacy 2",
            uniqueNumber = "10001483405",
            maticaConfig = (object?)null,
            evolisConfig = new
            {
                deviceName = "Evolis Primacy 02",
                supervisorHostName = (string?)null,
                supervisorRegisteredAt = (string?)null,
                defaultRibbonTypeId = 5,
                magCoercivity = 0,
                inputTray = 0,
                outputTray = 2,
                errorTray = 3,
                openMode = 2,   // SUPERVISED
                errorManagement = 1 // SOFTWARE
            },
            isDeleted = false,
            createdAt = "2026-09-23T10:00:00Z",
            updatedAt = (string?)null,
            deletedAt = (string?)null
        };
        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("PrintersController", "GetAll")] = GetAll(),
                [new EndpointKey("PrintersController", "GetById")] = GetById(),
                [new EndpointKey("PrintersController", "Create")] = Create(),
                [new EndpointKey("PrintersController", "Update")] = Update(),
                [new EndpointKey("PrintersController", "Delete")] = Delete(),
                [new EndpointKey("PrintersController", "Restore")] = Restore()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "First page of printers, read-only for a tenant caller.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleMaticaPrinter },
                        pageNumber = 1,
                        pageSize = 20,
                        totalCount = 1,
                        totalPages = 1,
                        hasNextPage = false,
                        hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Build();

        private static EndpointExampleSet GetById() => new EndpointExampleSetBuilder()
            .Response(200, "found", "A Matica printer, including its machine configuration.",
                new { success = true, data = SampleMaticaPrinter, error = (object?)null })
            .Response(404, "notFound", "No printer exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.NotFound", message = "No printer was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Create() => new EndpointExampleSetBuilder()
            .Request("matica", "Registering a Matica printer — the machine configuration is required.",
                new
                {
                    branchId = 7,
                    usingPrinterType = 0,
                    name = "Matica XID8300 - Front Desk",
                    model = "XID8300",
                    uniqueNumber = "MTC-0007",
                    maticaConfig = new { feederId = 1, hopperId = 2, rejectedId = 3, port = "COM3" },
                    tenantId = 42
                })
            .Request("evolis", "Registering an Evolis printer — no machine configuration block; " +
                "Evolis needs no server-side machine configuration.",
                new
                {
                    branchId = 7,
                    usingPrinterType = 1,
                    name = "Evolis Primacy 2 - Back Office",
                    model = "Primacy 2",
                    uniqueNumber = "EVL-0012",
                    maticaConfig = (object?)null,
                    tenantId = 42
                })
            .Response(200, "success", "The registered printer.",
                new { success = true, data = SampleMaticaPrinter, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller (not a system admin) attempted to register a printer.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.OnlySystemAdmin", message = "Only a system administrator can create, update, delete, or restore printers.", category = "Forbidden" }
                })
            .Response(409, "duplicateUniqueNumber", "Another non-deleted printer for this tenant already has this serial/IP.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.DuplicateUniqueNumber", message = "A printer with serial/IP 'MTC-0007' is already registered for this tenant.", category = "Conflict" }
                })
            .Response(422, "maticaConfigRequired", "usingPrinterType is Matica but maticaConfig was omitted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.MaticaConfigRequired", message = "A Matica printer requires its machine configuration (feeder, hopper, reject bin, port).", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Update() => new EndpointExampleSetBuilder()
            .Request("relocate", "Moving a printer to a different branch and updating its model/serial. " +
                "usingPrinterType is not editable here — a printer's hardware family doesn't change after registration.",
                new
                {
                    branchId = 9,
                    name = "Matica XID8300 - Relocated",
                    model = "XID8300",
                    uniqueNumber = "MTC-0007",
                    maticaConfig = new { feederId = 1, hopperId = 2, rejectedId = 3, port = "COM4" }
                })
            .Response(200, "success", "The updated printer.",
                new { success = true, data = SampleMaticaPrinter, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller attempted to update a printer.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.OnlySystemAdmin", message = "Only a system administrator can create, update, delete, or restore printers.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No printer exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.NotFound", message = "No printer was found with id 999.", category = "NotFound" }
                })
            .Response(422, "branchDeleted", "The target branch is soft-deleted and cannot have a printer registered to it.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.BranchDeleted", message = "Branch 9 is deleted and cannot have a printer registered to it.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Delete() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The printer was soft-deleted; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller attempted to delete a printer.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.OnlySystemAdmin", message = "Only a system administrator can create, update, delete, or restore printers.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No printer exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.NotFound", message = "No printer was found with id 999.", category = "NotFound" }
                })
            .Response(409, "alreadyDeleted", "The printer is already soft-deleted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.AlreadyDeleted", message = "Printer 3 is already deleted.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Restore() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The printer was restored; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller attempted to restore a printer.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.OnlySystemAdmin", message = "Only a system administrator can create, update, delete, or restore printers.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No printer exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.NotFound", message = "No printer was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notDeleted", "The printer is not currently deleted, so it can't be restored.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Printer.NotDeleted", message = "Printer 3 is not deleted.", category = "Conflict" }
                })
            .Build();
    }
}
