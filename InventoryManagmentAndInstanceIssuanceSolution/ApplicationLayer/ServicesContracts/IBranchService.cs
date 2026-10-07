using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Branches;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Branch management use cases (API Spec §4.5). Tenant callers are scoped to their own tenant;
    /// a system admin may manage any tenant's branches and supplies the target tenant on create.
    /// Every operation returns a <see cref="Result"/>; hard delete is intentionally omitted.
    /// </summary>
    public interface IBranchService
    {
        /// <summary>Returns a page of branches the caller may see.</summary>
        Task<Result<PaginatedResponse<BranchResponse>>> GetAllAsync(
            BranchListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Returns a single branch by id, scoped to the caller.</summary>
        Task<Result<BranchResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Creates a branch. Admin callers must supply <see cref="CreateBranchRequest.TenantId"/>.</summary>
        Task<Result<BranchResponse>> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

        /// <summary>Updates a branch's name, location, and active state.</summary>
        Task<Result<BranchResponse>> UpdateAsync(long id, UpdateBranchRequest request, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes a branch, recording the acting principal as the deleter.</summary>
        Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Restores a soft-deleted branch.</summary>
        Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Sets the branch active (idempotent) and returns it.</summary>
        Task<Result<BranchResponse>> ActivateAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Sets the branch inactive (idempotent) and returns it.</summary>
        Task<Result<BranchResponse>> DeactivateAsync(long id, CancellationToken cancellationToken = default);
    }
}