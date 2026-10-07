using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>BranchRequestsController</c> (<c>api/inventory/requests/*</c>).
    /// Bodies mirror <c>BranchRequestDtos.cs</c> and the outcomes actually returned by
    /// <c>BranchRequestService</c> and <c>BranchRequestErrors</c>. Confirm's generated transfers
    /// reuse <c>TransferErrors</c>/<c>StockErrors</c> for anything specific to the transfer itself
    /// (Known/Unknown item-id shape, insufficient stock) — see <c>TransactionsExamples.cs</c> for
    /// those; only failures specific to the request live here.
    /// </summary>
    internal static class BranchRequestExamples
    {
        private static readonly object InProgressRequest = new
        {
            id = 21,
            tenantId = 42,
            requestingBranchId = 9,
            requestingBranchName = "Airport Branch",
            requestStatus = 0,
            requestDateTime = "2026-08-11T08:00:00Z",
            actionTakenByTenantId = (long?)null,
            actionTakenAt = (string?)null,
            actionNotes = "Running low ahead of the long weekend.",
            rowVersion = "AAAAAAAACAE=",
            items = new object[]
            {
                new
                {
                    productId = 15, productName = "Gold Debit Card", askedQuantity = 100,
                    dispatchedQuantity = 0, receivedQuantity = 0, outstandingQuantity = 100,
                    productTransactionWay = 0
                }
            },
            unrequestedProducts = new object[0],
            transferIds = new long[0]
        };

        private static readonly object PartiallyFulfilledRequest = new
        {
            id = 21,
            tenantId = 42,
            requestingBranchId = 9,
            requestingBranchName = "Airport Branch",
            requestStatus = 5,
            requestDateTime = "2026-08-11T08:00:00Z",
            actionTakenByTenantId = 42,
            actionTakenAt = "2026-08-11T10:00:00Z",
            actionNotes = "Running low ahead of the long weekend.",
            rowVersion = "AAAAAAAACAI=",
            items = new object[]
            {
                new
                {
                    productId = 15, productName = "Gold Debit Card", askedQuantity = 100,
                    dispatchedQuantity = 100, receivedQuantity = 60, outstandingQuantity = 40,
                    productTransactionWay = 0
                }
            },
            unrequestedProducts = new object[]
            {
                new { productId = 16, productName = "Silver Prepaid Card", dispatchedQuantity = 20, receivedQuantity = 20 }
            },
            transferIds = new long[] { 91 }
        };

        private static readonly object GeneratedTransfer = new
        {
            id = 91,
            tenantId = 42,
            sourceBranchId = 7,
            sourceBranchName = "Downtown Branch",
            targetBranchId = 9,
            targetBranchName = "Airport Branch",
            transactionStatus = 0,
            origin = 0,
            parentTransferId = (long?)null,
            branchRequestId = 21,
            actionNotes = "Covering the Gold Debit Card shortfall.",
            createdAt = "2026-08-11T10:00:00Z",
            createdByTenantId = 42,
            createdByUsername = "acme-bank",
            statusChangedAt = (string?)null,
            checkedByUsername = (string?)null,
            rowVersion = "AAAAAAAACAM=",
            products = new object[]
            {
                new
                {
                    productId = 15, productName = "Gold Debit Card", transactedQuantity = 100,
                    realQuantityReceived = (int?)null, disposedQuantity = (int?)null, returnedQuantity = 0,
                    productTransactionWay = 0, outcome = 0, differenceAction = (int?)null
                }
            },
            items = new object[0]
        };

        private static readonly object RefusedRequest = new
        {
            id = 22,
            tenantId = 42,
            requestingBranchId = 9,
            requestingBranchName = "Airport Branch",
            requestStatus = 2,
            requestDateTime = "2026-08-09T08:00:00Z",
            actionTakenByTenantId = 42,
            actionTakenAt = "2026-08-09T09:30:00Z",
            actionNotes = "Product discontinued; requesting branch notified to substitute.",
            rowVersion = "AAAAAAAACAQ=",
            items = new object[]
            {
                new
                {
                    productId = 18, productName = "Legacy Prepaid Card", askedQuantity = 30,
                    dispatchedQuantity = 0, receivedQuantity = 0, outstandingQuantity = 30,
                    productTransactionWay = 1
                }
            },
            unrequestedProducts = new object[0],
            transferIds = new long[0]
        };

        private static readonly object CancelledRequest = new
        {
            id = 23,
            tenantId = 42,
            requestingBranchId = 9,
            requestingBranchName = "Airport Branch",
            requestStatus = 3,
            requestDateTime = "2026-08-09T08:00:00Z",
            actionTakenByTenantId = 42,
            actionTakenAt = "2026-08-09T08:15:00Z",
            actionNotes = "No longer needed — branch received stock through a different request.",
            rowVersion = "AAAAAAAACAU=",
            items = new object[]
            {
                new
                {
                    productId = 15, productName = "Gold Debit Card", askedQuantity = 50,
                    dispatchedQuantity = 0, receivedQuantity = 0, outstandingQuantity = 50,
                    productTransactionWay = 0
                }
            },
            unrequestedProducts = new object[0],
            transferIds = new long[0]
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("BranchRequestsController", "GetAll")] = GetAll(),
                [new EndpointKey("BranchRequestsController", "GetById")] = GetById(),
                [new EndpointKey("BranchRequestsController", "Create")] = Create(),
                [new EndpointKey("BranchRequestsController", "Confirm")] = Confirm(),
                [new EndpointKey("BranchRequestsController", "Refuse")] = Refuse(),
                [new EndpointKey("BranchRequestsController", "Cancel")] = Cancel()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "Newest-first branch requests, scoped to the caller's tenant (or any tenant, for a system admin).",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[]
                        {
                            new
                            {
                                id = 21, tenantId = 42, requestingBranchId = 9, requestingBranchName = "Airport Branch",
                                requestStatus = 5, lineCount = 1, totalAskedQuantity = 100, totalReceivedQuantity = 60,
                                requestDateTime = "2026-08-11T08:00:00Z", actionTakenAt = "2026-08-11T10:00:00Z"
                            }
                        },
                        pageNumber = 1, pageSize = 20, totalCount = 1, totalPages = 1,
                        hasNextPage = false, hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Build();

        private static EndpointExampleSet GetById() => new EndpointExampleSetBuilder()
            .Response(200, "inProgress", "A freshly-created request — nothing dispatched yet.",
                new { success = true, data = InProgressRequest, error = (object?)null })
            .Response(200, "partiallyFulfilled", "The same request after one confirm+receive cycle: " +
                "60 of 100 asked actually received, plus 20 units of a product that was never asked " +
                "for (unrequestedProducts) dispatched in the same shipment.",
                new { success = true, data = PartiallyFulfilledRequest, error = (object?)null })
            .Response(404, "notFound", "No branch request exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotFound", message = "No branch request was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Create() => new EndpointExampleSetBuilder()
            .Request("singleProduct", "A branch raising a need for one product. Reserves nothing and moves no stock.",
                new
                {
                    requestingBranchId = 9,
                    items = new object[] { new { productId = 15, askedQuantity = 100 } },
                    actionNotes = "Running low ahead of the long weekend."
                })
            .Response(200, "success", "The created request — opens InProgress.",
                new { success = true, data = InProgressRequest, error = (object?)null })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to create a " +
                "request — admin access to this module is read-only (decision Q7).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.SystemAdminNotAllowed", message = "A system administrator cannot create, confirm, refuse, or cancel branch requests.", category = "Forbidden" }
                })
            .Response(404, "branchNotFound", "The requesting branch doesn't exist (or belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.BranchNotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Response(409, "duplicateOpenRequest", "The requesting branch already has a non-terminal " +
                "request covering this product — add to the existing open request instead of raising a duplicate.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.DuplicateOpenRequest", message = "Product 15 already has an open request from this branch.", category = "Conflict" }
                })
            .Response(422, "branchInactive", "The requesting branch is inactive — a request from it " +
                "could never be confirmed (an inactive branch always fails as a transfer target), so creation fails early.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.BranchInactive", message = "Branch 9 is inactive and cannot request stock.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Confirm() => new EndpointExampleSetBuilder()
            .Request("singleSource", "Fulfilling the request from one source branch — Items reuses " +
                "the same shape a direct transfer create uses (Known-way lines need explicit card ids).",
                new
                {
                    transfers = new object[]
                    {
                        new
                        {
                            sourceBranchId = 7,
                            items = new object[] { new { productId = 15, transactedQuantity = 100 } },
                            actionNotes = "Covering the Gold Debit Card shortfall."
                        }
                    },
                    actionNotes = (string?)null
                })
            .Request("multiSource", "Splitting one product across two source branches — a single " +
                "product split across sources becomes two separate generated transfers (decision Q-14).",
                new
                {
                    transfers = new object[]
                    {
                        new
                        {
                            sourceBranchId = 7,
                            items = new object[] { new { productId = 15, transactedQuantity = 60 } },
                            actionNotes = "Partial fulfilment from Downtown."
                        },
                        new
                        {
                            sourceBranchId = 11,
                            items = new object[] { new { productId = 15, transactedQuantity = 40 } },
                            actionNotes = "Remaining balance from Uptown."
                        }
                    },
                    actionNotes = "Split across two branches to fulfil in full."
                })
            .Response(200, "success", "Confirm succeeded — carries the full detail of every " +
                "generated transfer, not just their ids, so no second round trip is needed.",
                new
                {
                    success = true,
                    data = new { request = PartiallyFulfilledRequest, transfers = new[] { GeneratedTransfer } },
                    error = (object?)null
                })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to confirm a request.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.SystemAdminNotAllowed", message = "A system administrator cannot create, confirm, refuse, or cancel branch requests.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No branch request exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotFound", message = "No branch request was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notOpenForConfirmation", "The request is already Fulfilled, Refused, or Cancelled.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotOpenForConfirmation", message = "Branch request 21 is not open for confirmation.", category = "Conflict" }
                })
            .Response(422, "sourceIsRequestingBranch", "A plan named the request's own requesting " +
                "branch as its source — checked here rather than left to fail later as a database constraint violation.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.SourceIsRequestingBranch", message = "Branch 9 is the requesting branch and cannot also be the source of a transfer that fulfils it.", category = "Validation" }
                })
            .Response(422, "itemIdsRequired", "One plan's line is for a Known-way product but named " +
                "no cards — reuses Transfer.ItemIdsRequired since the generated transfer follows the same rule a direct create would.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.ItemIdsRequired", message = "Product 15 is tracked per card, so the specific cards being transferred must be listed.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Refuse() => new EndpointExampleSetBuilder()
            .Request("withReason", "Closing the request without generating anything.",
                new { actionNotes = "Product discontinued; requesting branch notified to substitute." })
            .Response(200, "success", "The refused request.",
                new { success = true, data = RefusedRequest, error = (object?)null })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to refuse a request.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.SystemAdminNotAllowed", message = "A system administrator cannot create, confirm, refuse, or cancel branch requests.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No branch request exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotFound", message = "No branch request was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notOpenForClosure", "Something has already been received against this " +
                "request — once fulfilment has started it can't be walked back by refusing; settle the " +
                "generated transfers instead.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotOpenForClosure", message = "Branch request 21 is not open for refusal or cancellation.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Cancel() => new EndpointExampleSetBuilder()
            .Request("withNote", "The requester withdrawing its own request before it's confirmed.",
                new { actionNotes = "No longer needed — branch received stock through a different request." })
            .Response(200, "success", "The cancelled request.",
                new { success = true, data = CancelledRequest, error = (object?)null })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to cancel a request.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.SystemAdminNotAllowed", message = "A system administrator cannot create, confirm, refuse, or cancel branch requests.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No branch request exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotFound", message = "No branch request was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notOpenForClosure", "Something has already been received against this request.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "BranchRequest.NotOpenForClosure", message = "Branch request 21 is not open for refusal or cancellation.", category = "Conflict" }
                })
            .Build();
    }
}
