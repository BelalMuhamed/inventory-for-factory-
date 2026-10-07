using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>CardFilesController</c> (<c>api/card-files</c>). Bodies mirror
    /// <c>CardFileDtos.cs</c> and the outcomes actually returned by
    /// <c>CardFileGenerationService</c> and <c>CardFileErrors</c>/<c>TenantErrors</c>. No example
    /// is registered for 200 — the success response is the raw encrypted <c>.dat</c> file
    /// (<c>application/octet-stream</c>), not JSON; see the action's own XML doc for the
    /// <c>X-File-Mac</c>/<c>X-Card-Count</c>/<c>X-Expected-Row-Count</c> response headers that
    /// carry the hand-off metadata instead.
    /// </summary>
    internal static class CardFileExamples
    {
        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("CardFilesController", "Generate")] = Generate()
            };

        private static EndpointExampleSet Generate() => new EndpointExampleSetBuilder()
            .Request("twoCards", "Generating a small file for two cards, matched against the target tenant's catalog by name.",
                new
                {
                    tenantId = 42,
                    cards = new object[]
                    {
                        new { clearPan = "4111111111111111", productName = "Gold Debit Card", branchName = "Downtown Branch" },
                        new { clearPan = "4222222222222222", productName = "Gold Debit Card", branchName = "Downtown Branch" }
                    }
                })
            .Response(401, "actorNotResolved", "Rare edge case: the token passed the system-admin " +
                "policy check but the acting principal couldn't be resolved from it — a defensive " +
                "re-check, not an expected client-facing scenario. See the controller-level 401 " +
                "doc for the far more common empty-body case (no/invalid token).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "CardFile.ActorNotResolved", message = "The acting principal could not be resolved.", category = "Unauthorized" }
                })
            .Response(404, "tenantNotFound", "No tenant exists with the supplied id (reuses Tenant.NotFound rather than a separate code).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Tenant.NotFound", message = "No tenant was found with id 999.", category = "NotFound" }
                })
            .Response(409, "tenantUnavailable", "The target tenant is inactive or soft-deleted and cannot be issued a card file.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "CardFile.TenantUnavailable", message = "Tenant 42 is inactive or deleted and cannot be issued a card file.", category = "Conflict" }
                })
            .Response(422, "noCards", "The cards array is empty.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "CardFile.NoCards", message = "At least one card is required.", category = "Validation" }
                })
            .Response(422, "cardsRejected", "One or more cards failed validation — all-or-nothing, " +
                "so nothing is generated. Per-card reasons ride in error.validationErrors, keyed by " +
                "each card's index in the request (machine-readable enum names — this caller is an " +
                "admin tool, not an end user).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "CardFile.CardsRejected",
                        message = "2 card(s) failed validation. No file was generated.",
                        category = "Validation",
                        validationErrors = new Dictionary<string, string[]>
                        {
                            ["cards[0]"] = new[] { "InvalidPan" },
                            ["cards[1]"] = new[] { "UnknownProduct" }
                        }
                    }
                })
            .Build();
    }
}
