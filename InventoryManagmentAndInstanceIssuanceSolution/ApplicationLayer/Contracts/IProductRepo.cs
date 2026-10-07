using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Products;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="Product"/>. All query logic is expressed here as named
    /// methods; raw predicates never reach the service layer.
    /// </summary>
    public interface IProductRepo : IGenericRepo<Product, long>
    {
        /// <summary>
        /// Returns a page of products. When <paramref name="tenantScopeId"/> is supplied (tenant
        /// caller) results are restricted to that tenant; when <c>null</c> (system admin) the
        /// optional <see cref="ProductListFilter.TenantId"/> applies instead.
        /// </summary>
        Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, ProductListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Finds a product by id across all tenants, including soft-deleted rows.</summary>
        Task<Product?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when a non-deleted product with <paramref name="name"/> already exists for the
        /// tenant (optionally excluding <paramref name="excludeId"/>). Matches the filtered
        /// UNIQUE (TenantId, Name) constraint — a soft-deleted name is free to reuse.
        /// </summary>
        Task<bool> NameExistsAsync(long tenantId, string name, long? excludeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// get product by name 
        /// </summary>
        Task<Product?> GetByNameAsync(long tenantId, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads every non-deleted product for the tenant in one query, keyed by name
        /// (case-insensitive — matches SQL Server's default collation and the filtered UNIQUE
        /// (TenantId, Name) constraint that guarantees the keys are unique). Replaces per-row
        /// <see cref="GetByNameAsync"/> lookups in the batch-upload pipeline (Batch Upload Phased
        /// Plan, Phase 3) — one query instead of N.
        /// </summary>
        Task<IReadOnlyDictionary<string, Product>> GetTenantMapAsync(long tenantId, CancellationToken cancellationToken = default);
    }
}
