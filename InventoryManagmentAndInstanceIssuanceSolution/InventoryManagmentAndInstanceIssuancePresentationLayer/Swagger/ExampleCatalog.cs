using System;
using System.Collections.Generic;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// Identifies one controller action by its controller and method name — matches how
    /// <see cref="ExamplesOperationFilter"/> looks entries up from <c>MethodInfo</c>.
    /// </summary>
    public readonly record struct EndpointKey(string Controller, string Action);

    /// <summary>
    /// Merges every module's example provider into one lookup table consumed by
    /// <see cref="ExamplesOperationFilter"/>. Each Swagger documentation phase (S1, S2, ...) adds
    /// exactly one line here for the module(s) it covers — the per-endpoint content itself lives
    /// in the matching <c>Swagger/Examples/{Module}Examples.cs</c> file, mirroring how
    /// <c>Errors/{Module}Errors.cs</c> is organized one file per module.
    /// </summary>
    public static class ExampleCatalog
    {
        /// <summary>All registered endpoint example sets, keyed by controller + action name.</summary>
        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> All { get; } = Build();

        private static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build()
        {
            var catalog = new Dictionary<EndpointKey, EndpointExampleSet>();

            // Phase S1 — Auth & Tenants.
            Merge(catalog, AuthExamples.Build());
            Merge(catalog, TenantExamples.Build());

            // Phase S2 — Catalog & Printing.
            Merge(catalog, BranchExamples.Build());
            Merge(catalog, ProductExamples.Build());
            Merge(catalog, ProductPrintConfigExamples.Build());
            Merge(catalog, PrintersExamples.Build());
            Merge(catalog, PrintImagesExamples.Build());

            // Phase S3 — Stock & Cards.
            Merge(catalog, StockExamples.Build());
            Merge(catalog, ProductItemExamples.Build());
            Merge(catalog, InventoryExamples.Build());
            Merge(catalog, InventoryHistoryExamples.Build());
            Merge(catalog, CardFileExamples.Build());

            // Phase S4 — Transfers & Disposal.
            Merge(catalog, TransactionsExamples.Build());
            Merge(catalog, CardDisposalExamples.Build());

            // Phase S5 — Branch Requests.
            Merge(catalog, BranchRequestExamples.Build());

            return catalog;
        }

        private static void Merge(
            Dictionary<EndpointKey, EndpointExampleSet> catalog,
            IReadOnlyDictionary<EndpointKey, EndpointExampleSet> module)
        {
            foreach (KeyValuePair<EndpointKey, EndpointExampleSet> entry in module)
            {
                if (!catalog.TryAdd(entry.Key, entry.Value))
                {
                    throw new InvalidOperationException(
                        $"Duplicate Swagger example registration for {entry.Key.Controller}.{entry.Key.Action} — " +
                        "each action must be registered by exactly one module's Examples file.");
                }
            }
        }
    }
}
