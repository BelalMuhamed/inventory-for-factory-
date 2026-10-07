using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Branches;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Branch management service (API Spec §4.5). Resolves the caller from <see cref="ICurrentTenant"/>:
    /// a tenant principal is scoped to its own tenant; a system admin bypasses scoping and may target
    /// any tenant. Business failures surface as categorized <see cref="Error"/>s.
    /// </summary>
    public sealed class BranchService : IBranchService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;

        public BranchService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
        }

        public async Task<Result<PaginatedResponse<BranchResponse>>> GetAllAsync(
            BranchListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<BranchResponse>>(error);

            (IReadOnlyList<Branch> items, int total) =
                await _unitOfWork.Branches.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<BranchResponse> data = items.Select(Map).ToList();
            return PaginatedResponse<BranchResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<BranchResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            (Branch? branch, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<BranchResponse>(error);
            return Map(branch!);
        }

        public async Task<Result<BranchResponse>> CreateAsync(
            CreateBranchRequest request, CancellationToken cancellationToken = default)
        {
            long targetTenantId;

            if (_currentTenant.IsSystemAdmin)
            {
                if (request.TenantId is not long requested) return Result.Failure<BranchResponse>(BranchErrors.TenantRequired());
                if (await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(requested, cancellationToken) is null)
                    return Result.Failure<BranchResponse>(BranchErrors.TargetTenantNotFound(requested));
                targetTenantId = requested;
            }
            else
            {
                if (_currentTenant.TenantId is not long callerTenant) return Result.Failure<BranchResponse>(BranchErrors.ActorNotResolved());
                targetTenantId = callerTenant;
            }

            if (await _unitOfWork.Branches.NameExistsAsync(targetTenantId, request.Name, null, cancellationToken))
                return Result.Failure<BranchResponse>(BranchErrors.NameAlreadyExists(request.Name));

            var branch = new Branch
            {
                TenantId = targetTenantId,
                Name = request.Name,
                Location = request.Location,
                IsActive = request.IsActive
            };

            await _unitOfWork.Branches.AddAsync(branch, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);   // CreatedAt + audit row written by the interceptor
            return Map(branch);
        }

        public async Task<Result<BranchResponse>> UpdateAsync(
            long id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
        {
            (Branch? branch, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<BranchResponse>(error);

            if (!string.Equals(branch!.Name, request.Name, StringComparison.Ordinal) &&
                await _unitOfWork.Branches.NameExistsAsync(branch.TenantId, request.Name, id, cancellationToken))
                return Result.Failure<BranchResponse>(BranchErrors.NameAlreadyExists(request.Name));

            branch.Name = request.Name;
            branch.Location = request.Location;
            branch.IsActive = request.IsActive;

            _unitOfWork.Branches.Update(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(branch);
        }

        public async Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            (Branch? branch, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (branch!.IsDeleted) return Result.Failure(BranchErrors.AlreadyDeleted(id));

            // Transactions §4.10, fix F3. API §4.5 already documented this rule ("Blocked if
            // branch holds non-zero stock") but nothing enforced it until now — a branch could be
            // deleted while still holding stock, or while cards were physically in flight to or
            // from it. Both checks run before the actor lookup below: there is no point resolving
            // who is deleting a branch that cannot be deleted.
            if (await _unitOfWork.Stocks.HasNonZeroStockAsync(branch.TenantId, id, cancellationToken))
                return Result.Failure(BranchErrors.HasStock(id));
            if (await _unitOfWork.CardTransfers.HasInProgressTransferAsync(branch.TenantId, id, cancellationToken))
                return Result.Failure(BranchErrors.HasInProgressTransfer(id));

            // API §4.9 guard (EC-R36): a branch with an open stock request of its own cannot be
            // deleted while that request is still open.
            if (await _unitOfWork.BranchRequests.HasOpenRequestForBranchAsync(branch.TenantId, id, cancellationToken))
                return Result.Failure(BranchErrors.HasOpenRequest(id));

            (long? actorId, Error? actorError) = await ResolveActorIdAsync(cancellationToken);
            if (actorError is not null) return Result.Failure(actorError);

            branch.IsDeleted = true;
            branch.DeletedAt = DateTime.UtcNow;
            branch.DeletedBy = actorId;

            _unitOfWork.Branches.Update(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default)
        {
            (Branch? branch, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (!branch!.IsDeleted) return Result.Failure(BranchErrors.NotDeleted(id));

            branch.IsDeleted = false;
            branch.DeletedAt = null;
            branch.DeletedBy = null;

            _unitOfWork.Branches.Update(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public Task<Result<BranchResponse>> ActivateAsync(long id, CancellationToken cancellationToken = default) =>
            SetActiveAsync(id, true, cancellationToken);

        public Task<Result<BranchResponse>> DeactivateAsync(long id, CancellationToken cancellationToken = default) =>
            SetActiveAsync(id, false, cancellationToken);

        private async Task<Result<BranchResponse>> SetActiveAsync(long id, bool isActive, CancellationToken cancellationToken)
        {
            (Branch? branch, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<BranchResponse>(error);

            if (branch!.IsActive == isActive) return Map(branch);   // idempotent no-op

            branch.IsActive = isActive;
            _unitOfWork.Branches.Update(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(branch);
        }

        // Loads a branch and enforces caller scope: a tenant caller may only touch its own tenant's
        // branches, and a missing/out-of-scope branch both return NotFound (no existence leak).
        private async Task<(Branch? Branch, Error? Error)> LoadScopedAsync(long id, CancellationToken cancellationToken)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return (null, scopeError);

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (branch is null) return (null, BranchErrors.NotFound(id));
            if (scope is long s && branch.TenantId != s) return (null, BranchErrors.NotFound(id));
            return (branch, null);
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = BranchErrors.ActorNotResolved();
            return null;
        }

        private async Task<(long? ActorId, Error? Error)> ResolveActorIdAsync(CancellationToken cancellationToken)
        {
            if (!_currentTenant.IsSystemAdmin)
                return (_currentTenant.TenantId, _currentTenant.TenantId is null ? BranchErrors.ActorNotResolved() : null);

            SystemAdmin? admin = await _unitOfWork.SystemAdmins.GetActiveByUsernameAsync(_currentTenant.Username!, cancellationToken);
            return admin is null ? (null, BranchErrors.ActorNotResolved()) : (admin.Id, null);
        }

        private static BranchResponse Map(Branch b) => new(
            b.Id, b.TenantId, b.Name, b.Location, b.IsActive, b.IsDeleted, b.CreatedAt, b.UpdatedAt, b.DeletedAt);
    }
}