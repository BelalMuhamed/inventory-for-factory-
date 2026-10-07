using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Stocks;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>Stock read use cases (API Spec §4.7). Tenant callers see their own tenant only.</summary>
    public interface IStockService
    {
        /// <summary>Returns a page of stock rows (Product × Branch) the caller may see.</summary>
        Task<Result<PaginatedResponse<StockRowResponse>>> GetAllAsync(
            StockListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Returns a page of stock rows for a single branch.</summary>
        Task<Result<PaginatedResponse<StockRowResponse>>> GetBranchStockAsync(
            long branchId, StockListFilter filter, CancellationToken cancellationToken = default);
    }
}