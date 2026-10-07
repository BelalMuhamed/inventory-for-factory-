using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Stocks;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Stock read service (API Spec §4.7). A tenant principal is scoped to its own tenant; a system
    /// admin bypasses scoping and may filter by tenant.
    /// </summary>
    public sealed class StockService : IStockService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;

        public StockService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
        }

        public async Task<Result<PaginatedResponse<StockRowResponse>>> GetAllAsync(
            StockListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<StockRowResponse>>(error);

            (IReadOnlyList<Stock> items, int total) =
                await _unitOfWork.Stocks.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<StockRowResponse> data = items.Select(Map).ToList();
            return PaginatedResponse<StockRowResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public Task<Result<PaginatedResponse<StockRowResponse>>> GetBranchStockAsync(
            long branchId, StockListFilter filter, CancellationToken cancellationToken = default)
            => GetAllAsync(filter with { BranchId = branchId }, cancellationToken);

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = AuthErrors.ActorNotResolved();
            return null;
        }

        private static StockRowResponse Map(Stock s) => new(
            s.TenantId,
            s.BranchId,
            s.SettledBranch?.Name ?? string.Empty,
            s.ProductId,
            s.CardType?.Name ?? string.Empty,
            s.AvailableQuantity,
            s.HoldQuantity,
            s.CardType?.LowProductThreshold ?? 0,
            s.AvailableQuantity <= (s.CardType?.LowProductThreshold ?? 0),
            s.RowVersion is null ? string.Empty : Convert.ToBase64String(s.RowVersion),
            s.UpdatedAt);
    }
}