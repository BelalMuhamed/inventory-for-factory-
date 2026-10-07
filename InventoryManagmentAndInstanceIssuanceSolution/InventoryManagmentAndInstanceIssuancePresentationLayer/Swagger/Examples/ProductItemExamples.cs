using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>ProductItemsController</c> (<c>api/product-items/*</c>). Bodies
    /// mirror <c>ProductItemResponse.cs</c> and the outcomes actually returned by
    /// <c>ProductItemService</c> (InfrastructureLayer/Services/ProductItemService.cs) and
    /// <c>ProductItemErrors</c>.
    /// </summary>
    internal static class ProductItemExamples
    {
        private static readonly object SampleAvailableItem = new
        {
            id = 501,
            tenantId = 42,
            maskedPan = "**********123456",
            productId = 15,
            productName = "Gold Debit Card",
            branchId = 7,
            batchId = 3,
            status = 1,
            holderName = (string?)null,
            notes = (string?)null,
            isDeleted = false,
            createdAt = "2026-03-01T08:00:00Z",
            updatedAt = (string?)null
        };

        private static readonly object SampleInTransitItem = new
        {
            id = 502,
            tenantId = 42,
            maskedPan = "**********654321",
            productId = 15,
            productName = "Gold Debit Card",
            branchId = (long?)null,
            batchId = 3,
            status = 0,
            holderName = (string?)null,
            notes = (string?)null,
            isDeleted = false,
            createdAt = "2026-03-01T08:00:00Z",
            updatedAt = "2026-08-10T11:00:00Z"
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("ProductItemsController", "GetAll")] = GetAll(),
                [new EndpointKey("ProductItemsController", "GetById")] = GetById(),
                [new EndpointKey("ProductItemsController", "Update")] = Update()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "A page of product items, including one currently in transit " +
                "(branchId: null, status: OnHold).",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleAvailableItem, SampleInTransitItem },
                        pageNumber = 1,
                        pageSize = 20,
                        totalCount = 2,
                        totalPages = 1,
                        hasNextPage = false,
                        hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Build();

        private static EndpointExampleSet GetById() => new EndpointExampleSetBuilder()
            .Response(200, "found", "A card pinned to a branch and available for issue.",
                new { success = true, data = SampleAvailableItem, error = (object?)null })
            .Response(404, "notFound", "No product item exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductItem.NotFound", message = "No product item was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Update() => new EndpointExampleSetBuilder()
            .Request("markPrinted", "Recording that a card was successfully printed and issued.",
                new { status = 2, holderName = "Jane Doe", notes = (string?)null })
            .Request("markFailedPrinting", "Recording a spoiled card.",
                new { status = 3, holderName = (string?)null, notes = "Ribbon jam during print, card discarded." })
            .Response(200, "success", "The updated item; branch stock was recomputed transactionally.",
                new { success = true, data = SampleAvailableItem, error = (object?)null })
            .Response(404, "notFound", "No product item exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductItem.NotFound", message = "No product item was found with id 999.", category = "NotFound" }
                })
            .Response(409, "inTransit", "The card is currently in transit or unassigned " +
                "(branchId is null) and cannot be modified outside the Transactions module until " +
                "the transfer settles.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductItem.InTransit", message = "Product item 502 is in transit or unassigned and cannot be modified until the transfer is settled.", category = "Conflict" }
                })
            .Response(409, "alreadyDisposed", "The card was already written off — disposal is terminal.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductItem.Disposed", message = "Product item 501 has been disposed and can no longer be modified.", category = "Conflict" }
                })
            .Response(422, "disposeNotAllowedHere", "Attempted to set status to Disposed through this " +
                "generic endpoint — disposal requires a mandatory reason and disposing branch, so it " +
                "must go through the dispose endpoints instead (POST /api/inventory/cards/dispose).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "ProductItem.DisposeNotAllowedHere", message = "Cards cannot be disposed through the status endpoint. Use the dispose endpoint, which requires a reason and a branch.", category = "Validation" }
                })
            .Build();
    }
}
