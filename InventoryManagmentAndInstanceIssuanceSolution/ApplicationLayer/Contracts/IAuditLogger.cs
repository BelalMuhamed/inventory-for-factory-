// ApplicationLayer/Contracts/IAuditLogger.cs
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    /// <summary>Records non-CRUD audit actions (e.g. Login) that no entity change would capture.</summary>
    public interface IAuditLogger
    {
        /// <summary>Writes a <c>Login</c> audit row for the given principal.</summary>
        /// <param name="username">Authenticated username.</param>
        /// <param name="isSystemAdmin">Whether the principal is a system admin.</param>
        /// <param name="tenantId">Owning tenant id, or null for a system admin.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogLoginAsync(string username, bool isSystemAdmin, long? tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a non-CRUD audit row without saving (Transactions §4.10). Needed because
        /// <c>CardTransfer</c>, <c>CardTransferProduct</c>, <c>CardTransferItem</c>,
        /// <c>CardDisposal</c> and <c>CardDisposalItem</c> deliberately do not derive from
        /// <c>AuditableEntity</c> (ERD §6.5: append-only, no soft delete), so the
        /// <c>AuditSaveChangesInterceptor</c> never sees them — actions like "Transfer",
        /// "Received", and "Disposed" would otherwise leave no audit trail at all.
        /// <para>
        /// Unlike <see cref="LogLoginAsync"/>, this method does not call <c>SaveChanges</c>. Call
        /// it after committing the entity it describes — the id it needs to record does not exist
        /// until the insert has happened — then persist it with a normal
        /// <see cref="IUnitOfWork.SaveChangesAsync"/> call. That second save is not wrapped in the
        /// same DB transaction as the entity it describes: the entity's own correctness never
        /// depends on the audit row, so a crash in the narrow window between the two would lose an
        /// audit line, not corrupt inventory data — an acceptable trade against the alternative of
        /// writing an audit row with an <c>EntityId</c> of <c>"0"</c>, which the interceptor's own
        /// generic path already does today for every other entity's <c>Created</c> row.
        /// </para>
        /// </summary>
        /// <param name="tenantId">Owning tenant of the affected row, or <c>null</c> if none.</param>
        /// <param name="actorTenantId">Acting tenant, or <c>null</c> for a system admin.</param>
        /// <param name="actorUsername">Acting principal's username.</param>
        /// <param name="action">Action name (e.g. <c>"Transfer"</c>, <c>"Received"</c>, <c>"Disposed"</c>).</param>
        /// <param name="entityName">Name of the affected entity type.</param>
        /// <param name="entityId">Affected entity's key, already known (post-commit).</param>
        /// <param name="newValue">Optional free-text detail — a disposal reason, a settlement summary.</param>
        void StageAction(
            long? tenantId,
            long? actorTenantId,
            string actorUsername,
            string action,
            string entityName,
            string entityId,
            string? newValue = null);
    }
}