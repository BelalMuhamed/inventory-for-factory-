namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Aggregates the application's service contracts behind a single injectable façade,
    /// keeping controller constructors small. Concrete service properties are added here as
    /// features are implemented.
    /// </summary>
    public interface IServiceManager
    {
        /// <summary>Authentication use cases (login, refresh, logout).</summary>
        IAuthService Auth { get; }
        /// <summary>Tenant management use cases (list, detail, create, update, password, delete, restore).</summary>
        ITenantService Tenants { get; }
        /// <summary>Branch management service.</summary>
        IBranchService Branches { get; }
        /// <summary>Product (catalog) management service.</summary>
        IProductService Products { get; }
        IStockService Stocks { get; }
        IProductItemService ProductItems { get; }
        /// <summary>Batch card-upload use case (Batch Upload Phased Plan, Phase 6).</summary>
        IBatchUploadService BatchUpload { get; }
        /// <summary>Card-file generation use case (Card File Generation, Phase 9.5).</summary>
        ICardFileGenerationService CardFiles { get; }
        /// <summary>Card-transfer use cases: create, receive (disposition model), dispose (API §4.10).</summary>
        ITransferService Transfers { get; }
        /// <summary>Standalone card disposal use case (API §4.10, Addendum A).</summary>
        IDisposalService Disposals { get; }
        /// <summary>Branch stock request use cases: raise, confirm, refuse, cancel (API §4.9).</summary>
        IBranchRequestService BranchRequests { get; }
        /// <summary>Print-configuration image upload use case (module requirements §5–§7, Printing Module Q-10).</summary>
        IPrintImageService PrintImages { get; }
        /// <summary>Printer registry management use cases (ERD §6, Printing Module Q-01/Q-09).</summary>
        IPrinterConfigurationService Printers { get; }
        /// <summary>Product print-configuration sub-resource use case (decision Q-07).</summary>
        IProductPrintConfigurationService ProductPrintConfigs { get; }
        /// <summary>Ribbon type reference-table read use case (Printing Module Q-05/Q-19).</summary>
        IRibbonTypeService RibbonTypes { get; }
    }
}
