using ApplicationLayer.ServicesContracts;
using System;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Concrete service façade. Resolves each service lazily so a controller depending on
    /// <see cref="IServiceManager"/> only constructs the services it actually uses.
    /// </summary>
    public sealed class ServiceManager : IServiceManager
    {
        private readonly Lazy<IAuthService> _auth;
        private readonly Lazy<ITenantService> _tenants;
        private readonly Lazy<IBranchService> _branches;
        private readonly Lazy<IProductService> _products;
        private readonly Lazy<IStockService> _stocks;
        private readonly Lazy<IProductItemService> _productItems;
        private readonly Lazy<IBatchUploadService> _batchUpload;
        private readonly Lazy<ICardFileGenerationService> _cardFiles;
        private readonly Lazy<ITransferService> _transfers;
        private readonly Lazy<IDisposalService> _disposals;
        private readonly Lazy<IBranchRequestService> _branchRequests;
        private readonly Lazy<IPrintImageService> _printImages;
        private readonly Lazy<IPrinterConfigurationService> _printers;
        private readonly Lazy<IProductPrintConfigurationService> _productPrintConfigs;
        private readonly Lazy<IRibbonTypeService> _ribbonTypes;

        public ServiceManager(
            Func<IAuthService> authFactory,
            Func<ITenantService> tenantFactory,
            Func<IBranchService> branchFactory,
            Func<IProductService> productFactory,
            Func<IStockService> stockFactory,
            Func<IProductItemService> productItemFactory,
            Func<IBatchUploadService> batchUploadFactory,
            Func<ICardFileGenerationService> cardFileFactory,
            Func<ITransferService> transferFactory,
            Func<IDisposalService> disposalFactory,
            Func<IBranchRequestService> branchRequestFactory,
            Func<IPrintImageService> printImageFactory,
            Func<IPrinterConfigurationService> printerFactory,
            Func<IProductPrintConfigurationService> productPrintConfigFactory,
            Func<IRibbonTypeService> ribbonTypeFactory)
        {
            _auth = new Lazy<IAuthService>(authFactory);
            _tenants = new Lazy<ITenantService>(tenantFactory);
            _branches = new Lazy<IBranchService>(branchFactory);
            _products = new Lazy<IProductService>(productFactory);
            _stocks = new Lazy<IStockService>(stockFactory);
            _productItems = new Lazy<IProductItemService>(productItemFactory);
            _batchUpload = new Lazy<IBatchUploadService>(batchUploadFactory);
            _cardFiles = new Lazy<ICardFileGenerationService>(cardFileFactory);
            _transfers = new Lazy<ITransferService>(transferFactory);
            _disposals = new Lazy<IDisposalService>(disposalFactory);
            _branchRequests = new Lazy<IBranchRequestService>(branchRequestFactory);
            _printImages = new Lazy<IPrintImageService>(printImageFactory);
            _printers = new Lazy<IPrinterConfigurationService>(printerFactory);
            _productPrintConfigs = new Lazy<IProductPrintConfigurationService>(productPrintConfigFactory);
            _ribbonTypes = new Lazy<IRibbonTypeService>(ribbonTypeFactory);
        }

        public IAuthService Auth => _auth.Value;
        public ITenantService Tenants => _tenants.Value;
        public IBranchService Branches => _branches.Value;
        public IProductService Products => _products.Value;
        public IStockService Stocks => _stocks.Value;
        public IProductItemService ProductItems => _productItems.Value;
        public IBatchUploadService BatchUpload => _batchUpload.Value;
        public ICardFileGenerationService CardFiles => _cardFiles.Value;
        public ITransferService Transfers => _transfers.Value;
        public IDisposalService Disposals => _disposals.Value;
        public IBranchRequestService BranchRequests => _branchRequests.Value;
        public IPrintImageService PrintImages => _printImages.Value;
        public IPrinterConfigurationService Printers => _printers.Value;
        public IProductPrintConfigurationService ProductPrintConfigs => _productPrintConfigs.Value;
        public IRibbonTypeService RibbonTypes => _ribbonTypes.Value;
    }
}