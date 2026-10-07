using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Products;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Product (catalog) management use cases (API Spec §4.6). Tenant callers are scoped to their own
    /// tenant; a system admin may manage any tenant's products and supplies the target tenant on
    /// create. Every operation returns a <see cref="Result"/>; hard delete is intentionally omitted
    /// (consistent with the Branch/Tenant modules and the locked soft-delete-only decision).
    /// </summary>
    public interface IProductService
    {
        /// <summary>Returns a page of products the caller may see.</summary>
        Task<Result<PaginatedResponse<ProductResponse>>> GetAllAsync(
            ProductListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Returns a single product by id, scoped to the caller.</summary>
        Task<Result<ProductResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a product. Admin callers must supply <see cref="CreateProductRequest.TenantId"/>.
        /// A system admin may also attach a print configuration in the same call (Printing Module,
        /// phase 7); a tenant caller may still create a plain product without one.
        /// </summary>
        Task<Result<ProductResponse>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a product's name, status, threshold, and transaction-way. Printer family is not
        /// changeable here (Printing Module, phase 7, confirmed) — use
        /// <c>PUT /api/products/{id}/print-config</c>, which switches it atomically together with
        /// the matching configuration row.
        /// </summary>
        Task<Result<ProductResponse>> UpdateAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes a product, recording the acting principal as the deleter.</summary>
        Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Restores a soft-deleted product.</summary>
        Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Sets the product active (idempotent) and returns it.</summary>
        Task<Result<ProductResponse>> ActivateAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Sets the product inactive (idempotent) and returns it.</summary>
        Task<Result<ProductResponse>> DeactivateAsync(long id, CancellationToken cancellationToken = default);

    }
}
