using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>TransactionsController</c> (<c>api/inventory/transactions/*</c>).
    /// Bodies mirror <c>TransferDtos.cs</c> and the outcomes actually returned by
    /// <c>TransferService</c>/<c>ITransferComposer</c> and <c>TransferErrors</c> — the largest
    /// error catalogue in the API (30+ codes; only the most illustrative scenario per response
    /// code is shown here, not an exhaustive enumeration — see TransferErrors.cs source for the
    /// full list).
    /// </summary>
    internal static class TransactionsExamples
    {
        private static readonly object KnownWayTransferDetail = new
        {
            id = 88,
            tenantId = 42,
            sourceBranchId = 7,
            sourceBranchName = "Downtown Branch",
            targetBranchId = 9,
            targetBranchName = "Airport Branch",
            transactionStatus = 0,
            origin = 0,
            parentTransferId = (long?)null,
            branchRequestId = (long?)null,
            actionNotes = "Restocking ahead of the holiday weekend.",
            createdAt = "2026-08-10T09:00:00Z",
            createdByTenantId = 42,
            createdByUsername = "acme-bank",
            statusChangedAt = (string?)null,
            checkedByUsername = (string?)null,
            rowVersion = "AAAAAAAAB9E=",
            products = new object[]
            {
                new
                {
                    productId = 15,
                    productName = "Gold Debit Card",
                    transactedQuantity = 50,
                    realQuantityReceived = (int?)null,
                    disposedQuantity = (int?)null,
                    returnedQuantity = 0,
                    productTransactionWay = 0,
                    outcome = 0,
                    differenceAction = (int?)null
                }
            },
            items = new object[]
            {
                new { productItemId = 501, maskedPan = "**********123456", productId = 15, receiveStatus = 0 },
                new { productItemId = 502, maskedPan = "**********654321", productId = 15, receiveStatus = 0 }
            }
        };

        private static readonly object UnknownWayTransferDetail = new
        {
            id = 90,
            tenantId = 42,
            sourceBranchId = 7,
            sourceBranchName = "Downtown Branch",
            targetBranchId = 9,
            targetBranchName = "Airport Branch",
            transactionStatus = 0,
            origin = 0,
            parentTransferId = (long?)null,
            branchRequestId = (long?)null,
            actionNotes = (string?)null,
            createdAt = "2026-08-11T10:00:00Z",
            createdByTenantId = 42,
            createdByUsername = "acme-bank",
            statusChangedAt = (string?)null,
            checkedByUsername = (string?)null,
            rowVersion = "AAAAAAAAB9F=",
            products = new object[]
            {
                new
                {
                    productId = 16,
                    productName = "Silver Prepaid Card",
                    transactedQuantity = 100,
                    realQuantityReceived = (int?)null,
                    disposedQuantity = (int?)null,
                    returnedQuantity = 0,
                    productTransactionWay = 1,
                    outcome = 0,
                    differenceAction = (int?)null
                }
            },
            // Unknown-way lines never get item rows — the transfer moves entitlement, not
            // individually tracked cards.
            items = new object[0]
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("TransactionsController", "GetAll")] = GetAll(),
                [new EndpointKey("TransactionsController", "GetById")] = GetById(),
                [new EndpointKey("TransactionsController", "Create")] = Create(),
                [new EndpointKey("TransactionsController", "Receive")] = Receive(),
                [new EndpointKey("TransactionsController", "Dispose")] = Dispose()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "Newest-first transfer list — identical shape/scope to InventoryHistoryController.GetAll.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[]
                        {
                            new
                            {
                                id = 88, tenantId = 42, sourceBranchId = 7, sourceBranchName = "Downtown Branch",
                                targetBranchId = 9, targetBranchName = "Airport Branch", transactionStatus = 0,
                                origin = 0, parentTransferId = (long?)null, branchRequestId = (long?)null,
                                productLineCount = 1, totalTransactedQuantity = 50,
                                createdAt = "2026-08-10T09:00:00Z", statusChangedAt = (string?)null
                            }
                        },
                        pageNumber = 1, pageSize = 20, totalCount = 1, totalPages = 1,
                        hasNextPage = false, hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Build();

        private static EndpointExampleSet GetById() => new EndpointExampleSetBuilder()
            .Response(200, "knownWay", "A Known-way transfer, still in progress — items lists the exact cards in flight.",
                new { success = true, data = KnownWayTransferDetail, error = (object?)null })
            .Response(200, "unknownWay", "An Unknown-way transfer, still in progress — items is always empty for Unknown-way lines.",
                new { success = true, data = UnknownWayTransferDetail, error = (object?)null })
            .Response(404, "notFound", "No transfer exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.NotFound", message = "No transfer was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Create() => new EndpointExampleSetBuilder()
            .Request("knownWayLine", "Moving two specific, individually-tracked cards — Known-way " +
                "products must name the exact cards being sent, and the count must match TransactedQuantity.",
                new
                {
                    sourceBranchId = 7,
                    targetBranchId = 9,
                    items = new object[]
                    {
                        new { productId = 15, transactedQuantity = 2, productItemIds = new long[] { 501, 502 } }
                    },
                    actionNotes = "Restocking ahead of the holiday weekend."
                })
            .Request("unknownWayLine", "Moving a quantity of an Unknown-way product — no card ids: " +
                "the system selects which cards back the entitlement.",
                new
                {
                    sourceBranchId = 7,
                    targetBranchId = 9,
                    items = new object[]
                    {
                        new { productId = 16, transactedQuantity = 100 }
                    },
                    actionNotes = (string?)null
                })
            .Response(200, "success", "The created transfer — opens InProgress; no stock has moved yet.",
                new { success = true, data = KnownWayTransferDetail, error = (object?)null })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to create a " +
                "transfer — admin access to this module is read-only (decision Q7).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.SystemAdminNotAllowed", message = "A system administrator cannot create or settle transfers.", category = "Forbidden" }
                })
            .Response(404, "branchNotFound", "The source or target branch doesn't exist (or belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.BranchNotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Response(409, "itemNotAvailable", "One of the named cards is already in flight, printed, expired, spoiled, or written off.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.ItemNotAvailable", message = "Card **********123456 is not available to transfer.", category = "Conflict" }
                })
            .Response(422, "itemIdsRequired", "A Known-way line was submitted with no productItemIds — the exact cards must be named.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.ItemIdsRequired", message = "Product 15 is tracked per card, so the specific cards being transferred must be listed.", category = "Validation" }
                })
            .Response(422, "itemIdsNotAllowedForUnknown", "productItemIds were supplied for an Unknown-way line — rejected rather than silently ignored.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.ItemIdsNotAllowedForUnknown", message = "Product 16 is not tracked per card, so individual cards cannot be selected for it.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Receive() => new EndpointExampleSetBuilder()
            .Request("knownWayFullyReceived", "Every card on a Known-way line received cleanly.",
                new
                {
                    items = new object[]
                    {
                        new
                        {
                            productId = 15,
                            realQuantityReceived = 2,
                            disposedQuantity = 0,
                            itemDispositions = new object[]
                            {
                                new { productItemId = 501, disposition = 1 },
                                new { productItemId = 502, disposition = 1 }
                            },
                            differenceAction = (int?)null
                        }
                    },
                    disposeReason = (string?)null,
                    disposingBranchId = (long?)null,
                    actionNotes = "All cards received in good condition."
                })
            .Request("unknownWayReturnedToSource", "An Unknown-way line partially received; the " +
                "remainder is credited straight back to the source via an auto-generated return.",
                new
                {
                    items = new object[]
                    {
                        new { productId = 16, realQuantityReceived = 80, disposedQuantity = 0, itemDispositions = (object?)null, differenceAction = 0 }
                    },
                    disposeReason = (string?)null,
                    disposingBranchId = (long?)null,
                    actionNotes = (string?)null
                })
            .Request("unknownWayKeptAtDestination", "An Unknown-way line partially received; the " +
                "target is credited in full anyway (e.g. the rest was verbally confirmed on-site) — " +
                "the gap stays visible via differenceAction rather than being expressed as stock in transit.",
                new
                {
                    items = new object[]
                    {
                        new { productId = 16, realQuantityReceived = 80, disposedQuantity = 0, itemDispositions = (object?)null, differenceAction = 1 }
                    },
                    disposeReason = (string?)null,
                    disposingBranchId = (long?)null,
                    actionNotes = "Remaining 20 confirmed by phone; kept at destination pending paperwork."
                })
            .Response(200, "success", "Settlement outcome — returnTransferId is set only when a remainder actually moved.",
                new
                {
                    success = true,
                    data = new
                    {
                        transferId = 88,
                        transactionStatus = 1,
                        returnTransferId = (long?)null,
                        disposalId = (long?)null,
                        totalReceived = 2,
                        totalDisposed = 0,
                        totalReturned = 0
                    },
                    error = (object?)null
                })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to settle a transfer.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.SystemAdminNotAllowed", message = "A system administrator cannot create or settle transfers.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No transfer exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.NotFound", message = "No transfer was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notInProgress", "The transfer was already settled — settlement cannot run twice.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.NotInProgress", message = "Transfer 88 has already been settled.", category = "Conflict" }
                })
            .Response(422, "missingProductInSettlement", "A product carried by the transfer was left " +
                "out of the settlement — omission is never read as an implicit zero.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.MissingProductInSettlement", message = "Product 15 is part of this transfer and must be settled explicitly.", category = "Validation" }
                })
            .Response(422, "differenceActionRequired", "An Unknown-way line has a remainder but no differenceAction was supplied.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.DifferenceActionRequired", message = "Product 16 was not fully received, so a difference action must be specified.", category = "Validation" }
                })
            .Response(422, "dispositionsRequired", "A Known-way line was settled without a per-card outcome for every card.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.DispositionsRequired", message = "Product 15 is tracked per card, so every card must be settled individually.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Dispose() => new EndpointExampleSetBuilder()
            .Request("writeOffRemainder", "Writing off everything the transfer still carries, without receiving any of it.",
                new { branchId = 9, reason = "Cards damaged in transit; entire shipment written off." })
            .Response(200, "success", "Equivalent to Receive with every line fully disposed.",
                new
                {
                    success = true,
                    data = new
                    {
                        transferId = 88,
                        transactionStatus = 4,
                        returnTransferId = (long?)null,
                        disposalId = 12,
                        totalReceived = 0,
                        totalDisposed = 2,
                        totalReturned = 0
                    },
                    error = (object?)null
                })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to dispose of a transfer's cards.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.SystemAdminNotAllowed", message = "A system administrator cannot create or settle transfers.", category = "Forbidden" }
                })
            .Response(404, "notFound", "No transfer exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.NotFound", message = "No transfer was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notInProgress", "The transfer was already settled.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.NotInProgress", message = "Transfer 88 has already been settled.", category = "Conflict" }
                })
            .Response(422, "disposingBranchRequired", "The branchId was omitted — required because settled cards sit at no branch of their own.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Transfer.DisposingBranchRequired", message = "A disposing branch is required when any quantity is disposed of.", category = "Validation" }
                })
            .Build();
    }
}
