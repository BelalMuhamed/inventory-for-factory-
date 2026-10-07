using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>StockController</c> (<c>api/stock/*</c>). Read-only — no request
    /// bodies. Bodies mirror <c>StockDtos.cs</c>; this module's only failure mode reachable from
    /// these two GETs is the class-level 401 (no per-action business errors), so no error
    /// examples are registered here.
    /// </summary>
    internal static class StockExamples
    {
        private static readonly object SampleStockRow = new
        {
            tenantId = 42,
            branchId = 7,
            branchName = "Downtown Branch",
            productId = 15,
            productName = "Gold Debit Card",
            availableQuantity = 120,
            holdQuantity = 15,
            lowProductThreshold = 50,
            isLow = false,
            rowVersion = "AAAAAAAAB9E=",
            updatedAt = "2026-08-01T14:30:00Z"
        };

        private static readonly object SampleLowStockRow = new
        {
            tenantId = 42,
            branchId = 7,
            branchName = "Downtown Branch",
            productId = 16,
            productName = "Silver Prepaid Card",
            availableQuantity = 8,
            holdQuantity = 0,
            lowProductThreshold = 25,
            isLow = true,
            rowVersion = "AAAAAAAAB9F=",
            updatedAt = "2026-08-05T09:00:00Z"
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("StockController", "GetAll")] = GetAll(),
                [new EndpointKey("StockController", "GetByBranch")] = GetByBranch()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "First page of stock rows across all of the tenant's branches, " +
                "including one below its low-stock threshold.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleStockRow, SampleLowStockRow },
                        pageNumber = 1,
                        pageSize = 20,
                        totalCount = 2,
                        totalPages = 1,
                        hasNextPage = false,
                        hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Response(200, "lowStockOnly", "Filtered with ?lowStockOnly=true — only rows where " +
                "availableQuantity <= lowProductThreshold.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleLowStockRow },
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

        private static EndpointExampleSet GetByBranch() => new EndpointExampleSetBuilder()
            .Response(200, "page", "All stock rows for one branch. An unknown or out-of-scope " +
                "branchId simply yields an empty page — this endpoint does not 404.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleStockRow, SampleLowStockRow },
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
    }
}
