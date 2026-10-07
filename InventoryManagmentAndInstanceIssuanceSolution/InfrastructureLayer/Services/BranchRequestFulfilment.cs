using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// EF Core implementation of <see cref="IBranchRequestFulfilment"/> (API §4.9, decisions
    /// D-01/D-04). Covers every settlement path since the Unknown-way Maker-Checker workflow —
    /// see the interface's own doc comment.
    /// </summary>
    public sealed class BranchRequestFulfilment : IBranchRequestFulfilment
    {
        private readonly IUnitOfWork _unitOfWork;

        public BranchRequestFulfilment(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ApplyReceiptAsync(
            long branchRequestId, long targetBranchId,
            IReadOnlyDictionary<long, int> receivedByProductId,
            CancellationToken cancellationToken = default)
        {
            // tenantScopeId: null. This is an internal, service-to-service call triggered by the
            // settling transfer's own BranchRequestId column — not a caller-facing read — so it
            // is trusted by primary key rather than re-scoped to a tenant the caller already
            // implicitly established when TransferService loaded and validated the transfer
            // itself. A branchRequestId sourced this way can only ever belong to the same tenant
            // that created the transfer, by construction of BranchRequestService.ConfirmAsync.
            BranchRequest? request = await _unitOfWork.BranchRequests.GetForUpdateAsync(
                branchRequestId, null, cancellationToken);
            if (request is null) return;

            // D-04: a return transfer carries its parent's BranchRequestId but heads away from
            // the requesting branch, so it must never credit.
            if (request.RequestingBranchId != targetBranchId) return;

            foreach (BranchRequestItem item in request.Items)
            {
                // D-05: a product settled here that is not one of the request's own lines is
                // silently ignored — only quantities against products the request actually asked
                // for are credited.
                if (receivedByProductId.TryGetValue(item.ProductId, out int received) && received > 0)
                    item.CreditReceived(received);
            }

            request.RecomputeStatus();
        }
    }
}
