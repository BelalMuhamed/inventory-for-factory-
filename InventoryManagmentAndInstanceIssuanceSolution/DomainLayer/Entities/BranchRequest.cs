using DomainLayer.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A branch's recorded need for stock (ERD §4.1, table <c>BranchRequests</c>; API §4.9).
    /// <para>
    /// <b>Not soft-deletable</b> (decision Q-09): deliberately does <em>not</em> derive from
    /// <c>AuditableEntity</c>. Follows the <see cref="CardTransfer"/> precedent instead —
    /// append-only-with-status, its own <see cref="RequestDateTime"/> rather than an inherited
    /// audit block, no global query filter, and no restore endpoint. The
    /// <c>AuditSaveChangesInterceptor</c> never sees it; <c>Created</c>,
    /// <see cref="BranchRequestStatus.Confirmed"/>, <see cref="BranchRequestStatus.Refused"/> and
    /// <see cref="BranchRequestStatus.Cancelled"/> actions are logged explicitly through
    /// <c>IAuditLogger</c>.
    /// </para>
    /// <para>
    /// <b>Status is recomputed, not assigned ad hoc</b> (decision D-03): every transition except
    /// the two terminal closures (<see cref="BranchRequestStatus.Refused"/>,
    /// <see cref="BranchRequestStatus.Cancelled"/>) flows through <see cref="RecomputeStatus"/>,
    /// a pure function of the line counters on <see cref="Items"/>.
    /// </para>
    /// </summary>
    public class BranchRequest
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        public long TenantId { get; set; }

        /// <summary>
        /// Navigation to the owning tenant. Present so that this and
        /// <see cref="ActionTakenByTenant"/> configure as two distinct relationships — EF Core
        /// identifies a relationship by its navigations, so two navigation-less
        /// <c>HasOne&lt;Tenant&gt;().WithMany()</c> calls would silently reconfigure the same one
        /// and leave the second foreign key unmapped (the same pitfall <see cref="CardTransfer"/>
        /// already documents for its own two <c>Tenant</c> references).
        /// </summary>
        public Tenant Tenant { get; set; } = null!;

        /// <summary>The branch asking for stock (FK → Branches.Id).</summary>
        public long RequestingBranchId { get; set; }

        /// <summary>Navigation to the requesting branch.</summary>
        public Branch RequestingBranch { get; set; } = null!;

        /// <summary>UTC instant the request was raised.</summary>
        public DateTime RequestDateTime { get; set; }

        /// <summary>Lifecycle state. See <see cref="BranchRequestStatus"/> for the full state machine.</summary>
        public BranchRequestStatus RequestStatus { get; set; } = BranchRequestStatus.InProgress;

        /// <summary>
        /// Tenant that most recently confirmed, refused, or cancelled this request (FK →
        /// Tenants.Id), or <c>null</c> until the first such action.
        /// <para>
        /// Under the single-account-per-tenant auth model this always equals
        /// <see cref="TenantId"/> — tautological today, retained per ERD §4.1 for forward
        /// compatibility should a per-user identity ever return.
        /// </para>
        /// </summary>
        public long? ActionTakenByTenantId { get; set; }

        /// <summary>Navigation to the acting tenant, or <c>null</c> until the first action.</summary>
        public Tenant? ActionTakenByTenant { get; set; }

        /// <summary>UTC instant of the most recent confirm/refuse/cancel action, or <c>null</c>.</summary>
        public DateTime? ActionTakenAt { get; set; }

        /// <summary>
        /// Free-text note captured at confirm, refuse, or cancel. Optional in every case
        /// (decision Q-10) — including refuse, where the API specification's mandatory reason is
        /// deliberately relaxed.
        /// </summary>
        public string? ActionNotes { get; set; }

        /// <summary>
        /// Optimistic-concurrency token guarding the status flip.
        /// <para>
        /// <b>Schema note:</b> not in ERD §4.1; added with decision Q-07 (approved). Same
        /// rationale as <see cref="CardTransfer.RowVersion"/>: without it, two concurrent confirm
        /// calls could both read a non-terminal status and both generate transfers. Flagged for
        /// DBA review.
        /// </para>
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        /// <summary>Requested product lines. At least one always exists.</summary>
        public List<BranchRequestItem> Items { get; set; } = new();

        /// <summary>
        /// Recomputes <see cref="RequestStatus"/> from the line counters on <see cref="Items"/>
        /// (decision D-03). Terminal statuses (<see cref="BranchRequestStatus.Refused"/>,
        /// <see cref="BranchRequestStatus.Cancelled"/>) are left untouched — a closed request
        /// never reopens by recomputation. Pure: no I/O, no service dependencies, safe to call as
        /// many times as the caller likes.
        /// <para>
        /// Evaluated strongest condition first: every line fully received beats every line fully
        /// dispatched, which beats any line dispatched at all. An empty <see cref="Items"/>
        /// collection (never expected on a persisted request — creation always writes at least
        /// one line) resolves to <see cref="BranchRequestStatus.InProgress"/> rather than
        /// vacuously satisfying "every line," so a corrupted or not-yet-populated aggregate
        /// cannot recompute itself straight to <see cref="BranchRequestStatus.Fulfilled"/>.
        /// </para>
        /// </summary>
        public void RecomputeStatus()
        {
            if (RequestStatus is BranchRequestStatus.Refused or BranchRequestStatus.Cancelled)
                return;

            if (Items.Count == 0)
            {
                RequestStatus = BranchRequestStatus.InProgress;
                return;
            }

            bool everyLineReceived = Items.All(i => i.ReceivedQuantity >= i.AskedQuantity);
            bool anyLineReceived = Items.Any(i => i.ReceivedQuantity > 0);
            bool everyLineDispatched = Items.All(i => i.DispatchedQuantity >= i.AskedQuantity);
            bool anyLineDispatched = Items.Any(i => i.DispatchedQuantity > 0);

            RequestStatus =
                everyLineReceived ? BranchRequestStatus.Fulfilled :
                anyLineReceived ? BranchRequestStatus.PartiallyFulfilled :
                everyLineDispatched ? BranchRequestStatus.Confirmed :
                anyLineDispatched ? BranchRequestStatus.PartiallyConfirmed :
                BranchRequestStatus.InProgress;
        }
    }
}
