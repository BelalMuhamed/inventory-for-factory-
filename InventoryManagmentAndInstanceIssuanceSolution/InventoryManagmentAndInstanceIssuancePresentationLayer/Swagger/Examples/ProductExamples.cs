using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>ProductController</c> (<c>api/products/*</c>, excluding the
    /// <c>print-config</c> sub-resource — see <c>ProductPrintConfigExamples</c>). Bodies mirror
    /// <c>ProductDtos.cs</c> and the outcomes actually returned by <c>ProductService</c>
    /// (InfrastructureLayer/Services/ProductService.cs) and <c>ProductErrors</c>/<c>PrintingErrors</c>.
    /// </summary>
    internal static class ProductExamples
    {
        private static readonly object SampleProduct = new
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
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("ProductController", "GetAll")] = GetAll(),
                [new EndpointKey("ProductController", "GetById")] = GetById(),
                [new EndpointKey("ProductController", "Create")] = Create(),
                [new EndpointKey("ProductController", "Update")] = Update(),
                [new EndpointKey("ProductController", "Delete")] = Delete(),
                [new EndpointKey("ProductController", "Restore")] = Restore(),
                [new EndpointKey("ProductController", "Activate")] = Activate(),
                [new EndpointKey("ProductController", "Deactivate")] = Deactivate()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "First page of products for the caller's tenant.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleProduct },
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
            .Response(200, "found", "An active, Known-way product printed on an Evolis machine.",
                new { success = true, data = SampleProduct, error = (object?)null })
            .Response(404, "notFound", "No product exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Create() => new EndpointExampleSetBuilder()
            .Request("plainProduct", "A tenant creating a product with no print configuration attached.",
                new { name = "Gold Debit Card", productTransactionWay = 0, usingPrinterType = 1, activationStatus = 0, lowProductThreshold = 50 })
            .Request("systemAdminWithEvolisConfig", "A system admin creating a product and attaching its " +
                "Evolis print configuration in the same call — only a system admin may supply Matica/Evolis here.",
                new
                {
                    name = "Gold Debit Card",
                    productTransactionWay = 0,
                    usingPrinterType = 1,
                    activationStatus = 0,
                    lowProductThreshold = 50,
                    tenantId = 42,
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
            .Response(200, "success", "The created product.",
                new { success = true, data = SampleProduct, error = (object?)null })
            .Response(403, "printConfigNotAllowedForTenant", "A tenant caller (not a system admin) " +
                "supplied a Matica or Evolis payload — only product creation itself is allowed for a " +
                "tenant; attaching print configuration in the same call is system-admin only.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductPrintConfig.OnlySystemAdmin", message = "Only a system administrator can create or update a product's print configuration.", category = "Forbidden" }
                })
            .Response(409, "nameTaken", "A product with this name already exists for the tenant.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NameAlreadyExists", message = "A product named 'Gold Debit Card' already exists for this tenant.", category = "Conflict" }
                })
            .Response(422, "targetTenantNotFound", "A system-admin caller supplied a tenantId that doesn't exist.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.TargetTenantNotFound", message = "No tenant exists with id 999.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Update() => new EndpointExampleSetBuilder()
            .Request("rename", "Adjusting a product's name, status, and low-stock threshold. " +
                "Printer family is not editable here — use PUT /api/products/{id}/print-config.",
                new { name = "Gold Debit Card (Premium)", productTransactionWay = 0, activationStatus = 0, lowProductThreshold = 75 })
            .Response(200, "success", "The updated product.",
                new { success = true, data = SampleProduct, error = (object?)null })
            .Response(404, "notFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Response(409, "transactionWayImmutable", "Attempted to flip Known/Unknown on a product " +
                "that already has cards in inventory — the value is snapshotted onto every transfer " +
                "line and cannot change once cards exist.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.TransactionWayImmutable", message = "The transaction way of product 15 cannot be changed because cards already exist for it.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Delete() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The product was soft-deleted; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(404, "notFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Response(409, "hasOpenRequest", "The product is part of an open branch stock request line.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.HasOpenRequest", message = "Product 15 is part of an open stock request line and cannot be deleted.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Restore() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The product was restored; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(404, "notFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notDeleted", "The product is not currently deleted, so it can't be restored.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotDeleted", message = "Product 15 is not deleted.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Activate() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The product is now active. Idempotent.",
                new { success = true, data = SampleProduct, error = (object?)null })
            .Response(404, "notFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Deactivate() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The product is now inactive. Idempotent.",
                new { success = true, data = SampleProduct, error = (object?)null })
            .Response(404, "notFound", "No product exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Product.NotFound", message = "No product was found with id 999.", category = "NotFound" }
                })
            .Build();
    }
}
