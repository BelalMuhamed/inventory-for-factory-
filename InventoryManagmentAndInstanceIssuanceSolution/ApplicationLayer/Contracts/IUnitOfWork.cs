using System;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Common;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Coordinates the work of one or more repositories and commits it as a single transaction.
    /// Repositories are exposed as properties and share the same change tracker, so services
    /// never call <c>SaveChanges</c> directly and never open ad-hoc transactions.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>Repository for tenant accounts (the authentication identity).</summary>
        ITenantRepo Tenants { get; }

        /// <summary>Repository for the bootstrap system-administrator account.</summary>
        ISystemAdminRepo SystemAdmins { get; }

        /// <summary>Repository for persisted refresh tokens.</summary>
        IRefreshTokenRepo RefreshTokens { get; }

        /// <summary>
        /// Persists all changes tracked in the current unit of work.
        /// </summary>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The number of state entries written to the underlying store.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        /// <summary>Repository for tenant branches.</summary>
        IBranchRepo Branches { get; }
        /// <summary>Repository for tenant products (catalog).</summary>
        IProductRepo Products { get; }
        IStockRepo Stocks { get; }
        /// <summary>Repository for the Evolis-only 1:1 machine configuration (Evolis Integration Plan §6).</summary>
        IEvolisPrinterConfigRepo EvolisPrinterConfigs { get; }
        IProductItemRepo ProductItems { get; }
        IBatchRepo BatchRepo { get; }

        /// <summary>Repository for card transfers between branches (ERD §4.3–§4.5, API §4.10).</summary>
        ICardTransferRepo CardTransfers { get; }

        /// <summary>Repository for card write-offs (API §4.10, Addendum A).</summary>
        ICardDisposalRepo CardDisposals { get; }

        /// <summary>Repository for branch stock requests (ERD §4.1–§4.2, API §4.9).</summary>
        IBranchRequestRepo BranchRequests { get; }

        /// <summary>Repository for registered printers (ERD §6.1, Printing Module Q-01).</summary>
        IPrinterRepo Printers { get; }

        /// <summary>Repository for the Matica-only 1:1 machine configuration (ERD §6.2, Printing Module Q-01).</summary>
        IMaticaPrinterConfigRepo MaticaPrinterConfigs { get; }

        /// <summary>Repository for the global ribbon-type reference table (Printing Module Q-05).</summary>
        IRibbonTypeRepo RibbonTypes { get; }

        /// <summary>Repository for Matica product print configurations, one row per product (ERD §7.2, Printing Module Q-02/Q-03/Q-04).</summary>
        IMaticaProductPrintConfigRepo MaticaProductPrintConfigs { get; }

        /// <summary>Repository for Evolis product print configurations, one row per product (ERD §7.1, Printing Module Q-02/Q-05).</summary>
        IEvolisProductPrintConfigRepo EvolisProductPrintConfigs { get; }

        /// <summary>Repository for uploaded print-configuration image metadata (module requirements §5–§7, Printing Module Q-10).</summary>
        IPrintImageRepo PrintImages { get; }

        /// <summary>
        /// Repository for reconciliation service accounts (Matica Print Flow, reconciliation-
        /// credential phase) — the dedicated, revocable credential the background outbox
        /// reconciliation job authenticates with, distinct from any user-delegated token.
        /// </summary>
        IPrintAgentServiceAccountRepo ServiceAccounts { get; }

        /// <summary>
        /// Runs <paramref name="work"/> inside an explicit DB transaction (ERD §3.1 invariant /
        /// Batch Upload Phased Plan §3.6 &amp; §4.8): <paramref name="work"/> stages changes via
        /// repository calls only — it must not call <see cref="SaveChangesAsync"/> itself. This
        /// method calls it exactly once after <paramref name="work"/> returns a success
        /// <see cref="Result"/>, then commits. On a failure <see cref="Result"/>, or on any thrown
        /// exception, the transaction is rolled back — nothing is partially applied. An exception
        /// is rethrown after rollback (this method does not log or translate it; the caller owns
        /// that, since it has the tenant/trace/batch context to log meaningfully).
        /// </summary>
        /// <param name="work">
        /// The unit of work: stages changes through repository properties on this
        /// <see cref="IUnitOfWork"/> and returns the business outcome as a <see cref="Result"/>.
        /// </param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>
        /// The <see cref="Result"/> returned by <paramref name="work"/> (success only after a
        /// successful commit).
        /// </returns>
        Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> work, CancellationToken cancellationToken = default);

        /// <summary>
        /// Value-returning overload of <see cref="ExecuteInTransactionAsync(Func{Task{Result}}, CancellationToken)"/>
        /// (Transactions §4.10, fix F5). Identical transaction/rollback/commit semantics; the only
        /// difference is that <paramref name="work"/> hands back a <see cref="Result{TValue}"/>, so
        /// a caller that needs to return something out of the transaction — the settlement summary
        /// from a receive, the id of an auto-generated return — does not have to smuggle it out
        /// through a captured local variable the way <c>BatchUploadService</c> does today.
        /// </summary>
        /// <typeparam name="TValue">Type of the value produced on success.</typeparam>
        /// <param name="work">
        /// The unit of work: stages changes through repository properties on this
        /// <see cref="IUnitOfWork"/> and returns the business outcome as a <see cref="Result{TValue}"/>.
        /// </param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>
        /// The <see cref="Result{TValue}"/> returned by <paramref name="work"/> (success, with its
        /// value, only after a successful commit).
        /// </returns>
        Task<Result<TValue>> ExecuteInTransactionAsync<TValue>(
            Func<Task<Result<TValue>>> work, CancellationToken cancellationToken = default);
    }
}
