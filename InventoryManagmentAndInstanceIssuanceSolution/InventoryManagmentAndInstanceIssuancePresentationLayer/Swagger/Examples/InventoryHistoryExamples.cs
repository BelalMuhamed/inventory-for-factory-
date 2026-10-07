using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>InventoryHistoryController</c> (<c>api/inventory/history</c>). A
    /// reporting alias over the exact same data, scope, and filter as
    /// <c>TransactionsController.GetAll</c> (see that controller's own remarks) — bodies mirror
    /// <c>TransferDtos.cs</c>'s <c>TransferListItemResponse</c>. Read-only; this endpoint's only
    /// failure mode is the class-level 401, so no error examples are registered here.
    /// </summary>
    internal static class InventoryHistoryExamples
    {
        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("InventoryHistoryController", "GetAll")] = GetAll()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "Newest-first transfer history, including a settled direct " +
                "transfer and the auto-generated return it produced.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new object[]
                        {
                            new
                            {
                                id = 88,
                                tenantId = 42,
                                sourceBranchId = 7,
                                sourceBranchName = "Downtown Branch",
                                targetBranchId = 9,
                                targetBranchName = "Airport Branch",
                                transactionStatus = 3,
                                origin = 0,
                                parentTransferId = (long?)null,
                                branchRequestId = (long?)null,
                                productLineCount = 1,
                                totalTransactedQuantity = 50,
                                createdAt = "2026-08-10T09:00:00Z",
                                statusChangedAt = "2026-08-10T15:20:00Z"
                            },
                            new
                            {
                                id = 89,
                                tenantId = 42,
                                sourceBranchId = 9,
                                sourceBranchName = "Airport Branch",
                                targetBranchId = 7,
                                targetBranchName = "Downtown Branch",
                                transactionStatus = 0,
                                origin = 1,
                                parentTransferId = 88,
                                branchRequestId = (long?)null,
                                productLineCount = 1,
                                totalTransactedQuantity = 5,
                                createdAt = "2026-08-10T15:20:00Z",
                                statusChangedAt = (string?)null
                            }
                        },
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
