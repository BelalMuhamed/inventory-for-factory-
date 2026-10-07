using ApplicationLayer.Contracts;
using DomainLayer.Common;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureLayer
{
    /// <summary>
    /// EF Core unit of work. Owns the repository instances over a single shared
    /// <see cref="AppDbContext"/> so all staged changes commit in one transaction via
    /// <see cref="SaveChangesAsync"/>.
    /// </summary>
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Tenants = new TenantRepo(context);
            SystemAdmins = new SystemAdminRepo(context);
            RefreshTokens = new RefreshTokenRepo(context);
            Branches = new BranchRepo(context);
            Products = new ProductRepo(context);
            Stocks = new StockRepo(context);
            ProductItems = new ProductItemRepo(context);
            BatchRepo= new BatchRepo(context);
            CardTransfers = new CardTransferRepo(context);
            CardDisposals = new CardDisposalRepo(context);
            BranchRequests = new BranchRequestRepo(context);
            Printers = new PrinterRepo(context);
            MaticaPrinterConfigs = new MaticaPrinterConfigRepo(context);
            RibbonTypes = new RibbonTypeRepo(context);
            MaticaProductPrintConfigs = new MaticaProductPrintConfigRepo(context);
            EvolisProductPrintConfigs = new EvolisProductPrintConfigRepo(context);
            PrintImages = new PrintImageRepo(context);
            ServiceAccounts = new PrintAgentServiceAccountRepo(context);
            EvolisPrinterConfigs = new EvolisPrinterConfigRepo(context);
        }

        public ITenantRepo Tenants { get; }
        public ISystemAdminRepo SystemAdmins { get; }
        public IRefreshTokenRepo RefreshTokens { get; }
        public IBranchRepo Branches { get; }
        public IProductRepo Products { get; }
        public IStockRepo Stocks { get; }
        public IEvolisPrinterConfigRepo EvolisPrinterConfigs { get; }
        public IProductItemRepo ProductItems { get; }
        public IBatchRepo BatchRepo { get; }
        public ICardTransferRepo CardTransfers { get; }
        public ICardDisposalRepo CardDisposals { get; }
        public IBranchRequestRepo BranchRequests { get; }
        public IPrinterRepo Printers { get; }
        public IMaticaPrinterConfigRepo MaticaPrinterConfigs { get; }
        public IRibbonTypeRepo RibbonTypes { get; }
        public IMaticaProductPrintConfigRepo MaticaProductPrintConfigs { get; }
        public IEvolisProductPrintConfigRepo EvolisProductPrintConfigs { get; }
        public IPrintImageRepo PrintImages { get; }
        public IPrintAgentServiceAccountRepo ServiceAccounts { get; }


        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                Result result = await work();

                if (result.IsFailure)
                {
                    // A collected business failure (e.g. duplicate file, decryption failure) —
                    // not an exception. Roll back whatever "work" staged and hand the same
                    // Result straight back; the caller decides how to log/surface it.
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                // Never partially apply. Logging/translation is the caller's job (it has the
                // tenant/trace/batch context); this method only guarantees rollback + rethrow.
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Result<TValue>> ExecuteInTransactionAsync<TValue>(
            Func<Task<Result<TValue>>> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                Result<TValue> result = await work();

                if (result.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}