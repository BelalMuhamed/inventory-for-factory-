using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>CardDisposalsController</c> (<c>api/inventory/cards/dispose</c>,
    /// <c>api/inventory/disposals/*</c>). Bodies mirror <c>DisposalDtos.cs</c> and the outcomes
    /// actually returned by <c>DisposalService</c> and <c>DisposalErrors</c>.
    /// </summary>
    internal static class CardDisposalExamples
    {
        private static readonly object SampleDisposal = new
        {
            id = 12,
            tenantId = 42,
            branchId = 7,
            branchName = "Downtown Branch",
            cardTransferId = (long?)null,
            disposedByTenantId = 42,
            reason = "Water damage discovered during quarterly audit.",
            disposedAt = "2026-08-12T10:00:00Z",
            items = new object[]
            {
                new { productItemId = 601, maskedPan = "**********112233", productId = 15, productName = "Gold Debit Card" },
                new { productItemId = 602, maskedPan = "**********445566", productId = 15, productName = "Gold Debit Card" }
            }
        };

        private static readonly object SampleDisposalListItem = new
        {
            id = 12,
            tenantId = 42,
            branchId = 7,
            branchName = "Downtown Branch",
            cardTransferId = (long?)null,
            reason = "Water damage discovered during quarterly audit.",
            cardCount = 2,
            disposedAt = "2026-08-12T10:00:00Z"
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("CardDisposalsController", "Dispose")] = Dispose(),
                [new EndpointKey("CardDisposalsController", "GetAll")] = GetAll(),
                [new EndpointKey("CardDisposalsController", "GetById")] = GetById()
            };

        private static EndpointExampleSet Dispose() => new EndpointExampleSetBuilder()
            .Request("byExplicitCards", "Writing off specific, named cards.",
                new
                {
                    branchId = 7,
                    reason = "Water damage discovered during quarterly audit.",
                    productItemIds = new long[] { 601, 602 },
                    items = (object?)null
                })
            .Request("byQuantityFifo", "Writing off a quantity per product — the system picks the " +
                "oldest available cards first, but still records exactly which ones on the disposal.",
                new
                {
                    branchId = 7,
                    reason = "End-of-life stock, discontinued product line.",
                    productItemIds = (object?)null,
                    items = new object[]
                    {
                        new { productId = 15, quantity = 10 }
                    }
                })
            .Response(200, "success", "The disposal record, including every card written off under it.",
                new { success = true, data = SampleDisposal, error = (object?)null })
            .Response(403, "systemAdminNotAllowed", "A system-admin token attempted to dispose of " +
                "cards — never permitted; disposal is a tenant-only concept end to end (unlike " +
                "GetAll/GetById on this same controller, which do allow a read-only admin path).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.SystemAdminNotAllowed", message = "A system administrator cannot dispose of cards.", category = "Forbidden" }
                })
            .Response(404, "branchNotFound", "The disposing branch doesn't exist (or belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.BranchNotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Response(409, "alreadyDisposed", "One of the named cards has already been written off.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.AlreadyDisposed", message = "Card **********112233 has already been disposed.", category = "Conflict" }
                })
            .Response(409, "cardInTransfer", "One of the named cards is committed to an in-flight " +
                "transfer — it must be disposed of when that transfer is settled, not standalone.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.CardInTransfer", message = "Card **********112233 is part of an active transfer and must be disposed of when that transfer is settled.", category = "Conflict" }
                })
            .Response(422, "selectionAmbiguous", "Both productItemIds and items were supplied — refused rather than resolved by precedence.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.SelectionAmbiguous", message = "Specify either the cards to dispose of or the quantities per product, not both.", category = "Validation" }
                })
            .Response(422, "reasonRequired", "The reason was empty or omitted — mandatory by design; disposal cannot be undone.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.ReasonRequired", message = "A reason is required in order to dispose of cards.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "Newest-first disposal list.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleDisposalListItem },
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
            .Response(200, "found", "A standalone disposal (cardTransferId is null) with two cards written off.",
                new { success = true, data = SampleDisposal, error = (object?)null })
            .Response(404, "notFound", "No disposal exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Disposal.NotFound", message = "No disposal was found with id 999.", category = "NotFound" }
                })
            .Build();
    }
}
