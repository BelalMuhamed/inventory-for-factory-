using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>ProductPrintConfigController</c>
    /// (<c>api/products/{productId}/print-config/*</c>). Bodies mirror <c>PrintingDtos.cs</c> and
    /// the outcomes actually returned by <c>ProductPrintConfigurationService</c>
    /// (InfrastructureLayer/Services/ProductPrintConfigurationService.cs) and
    /// <c>PrintingErrors</c>. <c>Get</c> is open to both roles (tenant-scoped); <c>Update</c> and
    /// <c>GetFull</c> are system-admin only (decision Q-09) — enforced in the service, so their
    /// 403 is the standard enveloped body, not an empty one.
    /// </summary>
    internal static class ProductPrintConfigExamples
    {
        private static readonly object SampleEvolisConfig = new
        {
            productId = 15,
            usingPrinterType = 1,
            matica = (object?)null,
            evolis = new
            {
                ribbonTypeId = 3,
                ribbonTypeName = "YMCKO",
                printWay = 0,
                x = 10,
                y = 20,
                printedFace = 0,
                fontFamily = "Arial",
                fontSize = 12,
                printColor = "#000000",
                backgroundColor = "#FFFFFF",
                fontStyle = "Bold",
                imageId = (long?)null
            }
        };

        private static readonly object SampleMaticaConfig = new
        {
            productId = 15,
            usingPrinterType = 0,
            matica = new { cpi = 10, fontSize = 12, offsetX = 5, offsetY = 5, imageId = (long?)null },
            evolis = (object?)null
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("ProductPrintConfigController", "Get")] = Get(),
                [new EndpointKey("ProductPrintConfigController", "Update")] = Update(),
                [new EndpointKey("ProductPrintConfigController", "GetFull")] = GetFull()
            };

        private static EndpointExampleSet Get() => new EndpointExampleSetBuilder()
            .Response(200, "evolis", "The product prints via Evolis — only evolis is populated.",
                new { success = true, data = SampleEvolisConfig, error = (object?)null })
            .Response(200, "matica", "The product prints via Matica — only matica is populated.",
                new { success = true, data = SampleMaticaConfig, error = (object?)null })
            .Response(404, "productNotFound", "No product exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Update() => new EndpointExampleSetBuilder()
            .Request("evolisConfig", "Setting/replacing an Evolis print configuration.",
                new
                {
                    usingPrinterType = 1,
                    matica = (object?)null,
                    evolis = new
                    {
                        ribbonTypeId = 3,
                        printWay = 0,
                        x = 10,
                        y = 20,
                        printedFace = 0,
                        fontFamily = "Arial",
                        fontSize = 12,
                        printColor = "#000000",
                        backgroundColor = "#FFFFFF",
                        fontStyle = "Bold",
                        imageId = (long?)null
                    }
                })
            .Request("switchToMatica", "Switching a product from Evolis to Matica (decision Q-08): " +
                "supplying a different usingPrinterType than the product currently has hard-deletes " +
                "the old configuration row and inserts the new one in the same transaction.",
                new
                {
                    usingPrinterType = 0,
                    matica = new { cpi = 10, fontSize = 12, offsetX = 5, offsetY = 5, imageId = (long?)null },
                    evolis = (object?)null
                })
            .Response(200, "success", "The saved configuration.",
                new { success = true, data = SampleEvolisConfig, error = (object?)null })
            .Response(403, "tenantAttempted", "A tenant caller attempted to set a print configuration.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductPrintConfig.OnlySystemAdmin", message = "Only a system administrator can create or update a product's print configuration.", category = "Forbidden" }
                })
            .Response(404, "productNotFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Response(422, "evolisPayloadRequired", "usingPrinterType is Evolis but the evolis payload was omitted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductPrintConfig.EvolisPayloadRequired", message = "An Evolis print configuration payload is required when the printer type is Evolis.", category = "Validation" }
                })
            .Response(422, "invalidHexColor", "printColor/backgroundColor is not a valid 6- or 8-digit hex value.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductPrintConfig.InvalidHexColor", message = "'blue' is not a valid HEX color. Use the form #RRGGBB or #RRGGBBAA.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet GetFull() => new EndpointExampleSetBuilder()
            .Response(200, "withConfig", "Product plus its print configuration.",
                new
                {
                    success = true,
                    data = new
                    {
                        product = new
                        {
                            id = 15,
                            tenantId = 42,
                            name = "Gold Debit Card",
                            activationStatus = 0,
                            lowProductThreshold = 50,
                            productTransactionWay = 0,
                            usingPrinterType = 1,
                            isDeleted = false,
                            createdAt = "2026-01-20T10:00:00Z",
                            updatedAt = (string?)null,
                            deletedAt = (string?)null
                        },
                        printConfig = SampleEvolisConfig
                    },
                    error = (object?)null
                })
            .Response(200, "withoutConfig", "The product has no print configuration yet — this is " +
                "surfaced as printConfig: null, not an error, since this endpoint is an " +
                "administrative overview.",
                new
                {
                    success = true,
                    data = new
                    {
                        product = new
                        {
                            id = 16,
                            tenantId = 42,
                            name = "Silver Prepaid Card",
                            activationStatus = 0,
                            lowProductThreshold = 25,
                            productTransactionWay = 1,
                            usingPrinterType = 0,
                            isDeleted = false,
                            createdAt = "2026-02-10T10:00:00Z",
                            updatedAt = (string?)null,
                            deletedAt = (string?)null
                        },
                        printConfig = (object?)null
                    },
                    error = (object?)null
                })
            .Response(403, "tenantAttempted", "A tenant caller attempted to use this administrative overview endpoint.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductPrintConfig.OnlySystemAdmin", message = "Only a system administrator can create or update a product's print configuration.", category = "Forbidden" }
                })
            .Response(404, "productNotFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Build();
    }
}
