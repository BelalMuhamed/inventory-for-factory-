using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Stable, localizable <see cref="Error"/> catalogue for the stock module.</summary>
    public static class StockErrors
    {
        /// <summary>A required stock row is missing while decrementing availability (→ 404).</summary>
        public static Error RowNotFound(long branchId, long productId) =>
            Error.NotFound("Stock.RowNotFound",
                $"No stock row exists for branch {branchId} and product {productId}.");

        /// <summary>The change would drive AvailableQuantity below zero (→ 409).</summary>
        public static Error InsufficientAvailable(long branchId, long productId) =>
            Error.Conflict("Stock.InsufficientAvailable",
                $"Available stock for branch {branchId} and product {productId} cannot go below zero.");

        /// <summary>A concurrent update changed the stock row's RowVersion (→ 409).</summary>
        public static Error ConcurrencyConflict() =>
            Error.Conflict("Stock.ConcurrencyConflict",
                "The stock row was modified by another operation. Please retry.");

        /// <summary>Stock cannot be created because no product with this name exists for the tenant.</summary>
        public static Error ProductNotFound(string productName) =>
            Error.NotFound(
                "Stock.ProductNotFound",
                $"No product named '{productName}' exists for this tenant; stock cannot be created.")
                .WithArg(productName);

        /// <summary>Stock cannot be created because no branch with this name exists for the tenant.</summary>
        public static Error BranchNotFound(string branchName) =>
            Error.NotFound(
                "Stock.BranchNotFound",
                $"No branch named '{branchName}' exists for this tenant; stock cannot be created.")
                .WithArg(branchName);

        /// <summary>The Stock row failed to persist despite the product and branch both existing.</summary>
        public static Error CreateFailed(string branchName, string productName) =>
            Error.Conflict(
                "Stock.CreateFailed",
                $"Failed to create a stock row for branch '{branchName}' / product '{productName}'.")
                .WithArg($"{branchName}/{productName}");
    }
}