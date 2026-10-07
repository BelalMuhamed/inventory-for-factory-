using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Data
{
    /// <summary>
    /// EF Core unit-of-work boundary and model owner. Holds the auth aggregates introduced so
    /// far and applies cross-cutting model rules in <see cref="OnModelCreating"/>:
    /// per-ERD indexes, soft-delete query filters, and the tenant scoping filter.
    /// <para>
    /// The tenant scoping filter uses <see cref="ICurrentTenant"/> so every query on a
    /// tenant-owned table is constrained to the caller's tenant, except for the system admin,
    /// who bypasses it.
    /// </para>
    /// </summary>
    public sealed class AppDbContext : DbContext
    {
        private readonly ICurrentTenant _currentTenant;

        /// <summary>Creates the context with EF options and the ambient principal accessor.</summary>
        /// <param name="options">EF Core options (provider, connection string).</param>
        /// <param name="currentTenant">Accessor for the authenticated principal's claims.</param>
        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
            : base(options)
        {
            _currentTenant = currentTenant;
        }

        /// <summary>Tenant accounts (the authentication identity).</summary>
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Product> Products => Set<Product>();        // alongside Branches


        /// <summary>Bootstrap system-administrator accounts.</summary>
        public DbSet<SystemAdmin> SystemAdmins => Set<SystemAdmin>();

        /// <summary>Persisted refresh tokens.</summary>
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        /// <summary>Persisted Audit log.</summary>
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<EvolisPrinterConfiguration> EvolisPrinterConfigurations { get; set; } = null!;
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Stock> Stocks => Set<Stock>();
        public DbSet<ProductItem> Cards => Set<ProductItem>();
        public DbSet<Batch> Batches => Set<Batch>();

        /// <summary>Card transfers between branches (ERD §4.3, table CardsTransferHistory).</summary>
        public DbSet<CardTransfer> CardTransfers => Set<CardTransfer>();

        /// <summary>Per-product lines on a transfer (ERD §4.4).</summary>
        public DbSet<CardTransferProduct> CardTransferProducts => Set<CardTransferProduct>();

        /// <summary>Individually tracked cards on a Known-way transfer (ERD §4.5).</summary>
        public DbSet<CardTransferItem> CardTransferItems => Set<CardTransferItem>();

        /// <summary>Card write-offs (API §4.10, Addendum A).</summary>
        public DbSet<CardDisposal> CardDisposals => Set<CardDisposal>();

        /// <summary>Cards written off under a disposal (API §4.10, Addendum A).</summary>
        public DbSet<CardDisposalItem> CardDisposalItems => Set<CardDisposalItem>();

        /// <summary>Branch stock requests (ERD §4.1, API §4.9).</summary>
        public DbSet<BranchRequest> BranchRequests => Set<BranchRequest>();

        /// <summary>Requested product lines on a branch request (ERD §4.2, API §4.9).</summary>
        public DbSet<BranchRequestItem> BranchRequestItems => Set<BranchRequestItem>();

        /// <summary>Registered physical printers (ERD §6.1, Printing Module Q-01).</summary>
        public DbSet<Printer> Printers => Set<Printer>();

        /// <summary>Matica-only 1:1 machine configuration (ERD §6.2, Printing Module Q-01).</summary>
        public DbSet<MaticaPrinterConfiguration> MaticaPrinterConfigurations => Set<MaticaPrinterConfiguration>();

        /// <summary>Ribbon type reference table (Printing Module Q-05).</summary>
        public DbSet<RibbonType> RibbonTypes => Set<RibbonType>();

        /// <summary>Matica printing parameters, one row per product (ERD §7.2, Printing Module Q-02/Q-03/Q-04).</summary>
        public DbSet<MaticaProductPrintConfiguration> MaticaProductPrintConfigurations => Set<MaticaProductPrintConfiguration>();

        /// <summary>Evolis printing parameters, one row per product (ERD §7.1, Printing Module Q-02/Q-05).</summary>
        public DbSet<EvolisProductPrintConfiguration> EvolisProductPrintConfigurations => Set<EvolisProductPrintConfiguration>();

        /// <summary>Uploaded print-configuration image metadata (module requirements §5–§7, Printing Module Q-10).</summary>
        public DbSet<PrintImage> PrintImages => Set<PrintImage>();

        /// <summary>
        /// Reconciliation service accounts (Matica Print Flow, reconciliation-credential phase) —
        /// the dedicated, revocable credential the background outbox reconciliation job
        /// authenticates with.
        /// </summary>
        public DbSet<PrintAgentServiceAccount> PrintAgentServiceAccounts => Set<PrintAgentServiceAccount>();


        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureTenant(modelBuilder);
            ConfigureSystemAdmin(modelBuilder);
            ConfigureRefreshToken(modelBuilder);
            ConfigureBranch(modelBuilder);
            ConfigureProduct(modelBuilder);
            ConfigureStock(modelBuilder);
            ConfigureCards(modelBuilder);
            ConfigureBatches(modelBuilder);
            ConfigureCardTransfers(modelBuilder);
            ConfigureCardDisposals(modelBuilder);
            ConfigureBranchRequests(modelBuilder);
            ConfigurePrinterRegistry(modelBuilder);
            ConfigureProductPrintConfigurations(modelBuilder);
            ConfigureReconciliationCredentials(modelBuilder);

        }

        /// <inheritdoc />
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void StampAuditFields()
        {
            DateTime utcNow = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = utcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = utcNow;
                        break;
                }
            }
        }

        // InfrastructureLayer/Data/AppDbContext.cs  (ConfigureTenant body — replace)
        private static void ConfigureTenant(ModelBuilder modelBuilder)
        {
            #region tenant scoping filter
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Code).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Username).IsRequired().HasMaxLength(100);
                entity.Property(t => t.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(t => t.IsActive).HasDefaultValue(true);

                // Code and Username are unique across ALL tenants (including soft-deleted), so a
                // deleted tenant's identifiers stay reserved. IsDeleted is indexed (filtered) to
                // keep the common "active only" queries cheap.
                entity.HasIndex(t => t.Code).IsUnique();
                entity.HasIndex(t => t.Username).IsUnique();
                entity.HasIndex(t => t.IsDeleted).HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(t => !t.IsDeleted);
            });
            #endregion

            #region audit log 
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.ActorUsername).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(50);
                entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(a => a.EntityId).IsRequired().HasMaxLength(50);
                entity.Property(a => a.IpAddress).HasMaxLength(45);

                entity.HasIndex(a => new { a.TenantId, a.Timestamp });
                entity.HasIndex(a => new { a.EntityName, a.EntityId });
                entity.HasIndex(a => new { a.ActorTenantId, a.Timestamp });
                // No soft-delete filter, no DeletedBy: audit rows are immutable.
            });
            #endregion

        }

        private static void ConfigureSystemAdmin(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemAdmin>(entity =>
            {
                entity.ToTable("SystemAdmins");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Username).IsRequired().HasMaxLength(100);
                entity.Property(a => a.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(a => a.IsActive).HasDefaultValue(true);

                entity.HasIndex(a => a.Username)
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(a => !a.IsDeleted);
            });
        }

        private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(128);
                entity.Property(r => r.ReplacedByTokenHash).HasMaxLength(128);
                entity.Property(r => r.IsSystemAdmin).HasDefaultValue(false);

                entity.HasIndex(r => r.TokenHash).IsUnique();
                entity.HasIndex(r => r.userName);
            });
        }

        /// <summary>
        /// Reconciliation service accounts (Matica Print Flow, reconciliation-credential phase) —
        /// same shape as <see cref="ConfigureRefreshToken"/>'s treatment of a credential-adjacent
        /// table: a unique index on the lookup key (<c>ClientId</c>, used at token-mint time), no
        /// soft delete (revocation is the deletion-equivalent here, tracked via <c>RevokedAt</c>
        /// so the audit trail — who was provisioned, when, and when revoked — is never lost).
        /// </summary>
        private static void ConfigureReconciliationCredentials(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PrintAgentServiceAccount>(entity =>
            {
                entity.ToTable("PrintAgentServiceAccounts");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.ClientSecretHash).IsRequired().HasMaxLength(256);
                entity.Property(a => a.Label).IsRequired().HasMaxLength(200);

                entity.HasIndex(a => a.ClientId)
                      .IsUnique()
                      .HasDatabaseName("UX_PrintAgentServiceAccounts_ClientId");

                entity.HasIndex(a => a.TenantId)
                      .HasDatabaseName("IX_PrintAgentServiceAccounts_TenantId");

                entity.HasIndex(a => a.BranchId)
                      .HasDatabaseName("IX_PrintAgentServiceAccounts_BranchId");
            });
        }

        private static void ConfigureBranch(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branches");
                entity.HasKey(b => b.Id);

                entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
                entity.Property(b => b.Location).HasMaxLength(500);
                entity.Property(b => b.IsActive).HasDefaultValue(true);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(b => b.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);
                entity.HasIndex(x => x.Name)
               .HasDatabaseName("IX_Category_Name");
                entity.HasIndex(b => b.TenantId);
                // UNIQUE (TenantId, Name) among non-deleted rows (ERD §2.1).
                entity.HasIndex(b => new { b.TenantId, b.Name })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(b => !b.IsDeleted);
                entity.HasIndex(x => new
                {
                    x.TenantId,
                    x.Name
                })
              .HasDatabaseName("IX_Branch_TenantId_Name");
            });
        }

        private static void ConfigureProduct(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.ActivationStatus).HasConversion<byte>().IsRequired();
                entity.Property(p => p.LowProductThreshold).IsRequired().HasDefaultValue(0);
                entity.Property(p => p.ProductTransactionWay).HasConversion<byte>().IsRequired();
                entity.Property(p => p.UsingPrinterType).HasConversion<byte>().IsRequired();

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(p => p.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(p => p.TenantId);
                // UNIQUE (TenantId, Name) among non-deleted rows (ERD §2.2).
                entity.HasIndex(p => new { p.TenantId, p.Name })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(p => !p.IsDeleted);
                entity.HasIndex(x => new
                {
                 x.TenantId,
                 x.Name
                })
                .HasDatabaseName("IX_Product_TenantId_Name");
            });
        }
        private static void ConfigureStock(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Stock>(entity =>
            {
                // Composite primary key
                entity.HasKey(e => new { e.TenantId, e.BranchId, e.ProductId });

                // Relationships (if not already inferred)
                entity.HasOne(e => e.Bank)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict); // choose appropriate

                entity.HasOne(e => e.SettledBranch)
                      .WithMany()
                      .HasForeignKey(e => e.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CardType)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                // RowVersion is already configured via [Timestamp]
                // Optionally set UpdatedAt default value in SQL
                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                // Product-level rollup queries (ERD §3.1) — PK already covers (TenantId, BranchId, ProductId).
                entity.HasIndex(e => new { e.TenantId, e.ProductId })
                      .HasDatabaseName("IX_Stocks_TenantId_ProductId");

                // Invariant guards (ERD §3.1): never let the aggregate go negative.
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Stocks_AvailableQuantity_NonNegative", "[AvailableQuantity] >= 0");
                    t.HasCheckConstraint("CK_Stocks_HoldQuantity_NonNegative", "[HoldQuantity] >= 0");
                });
            });
        }
        private static void ConfigureCards(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductItem>(entity =>
            {
                entity.Property(x => x.MaskedPan).IsRequired().HasMaxLength(32);

                entity.Property(x => x.PanFingerprint)
                    .IsRequired()
                    .HasColumnType("binary(32)")
                    .IsFixedLength();

                entity.HasIndex(u => u.CardHolderName)
            .HasDatabaseName("IX_card_holder_name")
            ;

                entity.HasIndex(u => u.Status)
            .HasDatabaseName("IX_card_status_name")
           ;

                // Item identity per tenant (PAN Storage Redesign). Filtered so a soft-deleted
                // item's PAN can be re-issued.
                entity.HasIndex(x => new
                {
                    x.TenantId,
                    x.PanFingerprint
                })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Cards_TenantId_PanFingerprint");

                // Non-unique covering index for the batch/stock query paths (§4.8).
                entity.HasIndex(x => new
                {
                    x.TenantId,
                    x.ProductId,
                    x.BranchID,
                    x.PanFingerprint
                })
                .HasDatabaseName("IX_Cards_TenantId_ProductId_BranchId_PanFingerprint");

                // BatchId is required: an item always belongs to the batch that introduced it.
                // Deleting a batch cascades and removes its items.
                entity.HasOne(x => x.Batch)
                      .WithMany(b => b.CardsInBatch)
                      .HasForeignKey(x => x.BatchId)
                      .OnDelete(DeleteBehavior.Cascade);

                // BranchID is optional (Transactions §4.10, Q4): null = in transit or unassigned.
                // Declared explicitly rather than left to convention for two reasons: an optional
                // relationship would otherwise default to ClientSetNull, and NoAction is what the
                // ERD (§3.3) specifies — deleting a branch must never delete or silently detach
                // the cards that were sitting at it.
                entity.HasOne(x => x.Branch)
                      .WithMany()
                      .HasForeignKey(x => x.BranchID)
                      .OnDelete(DeleteBehavior.NoAction);

                // Supports the unassigned-pool query (BranchID IS NULL) that the transfer and
                // print paths both need, and the per-branch availability count.
                entity.HasIndex(x => new { x.TenantId, x.BranchID, x.Status })
                      .HasDatabaseName("IX_Cards_TenantId_BranchId_Status");
            });
        }
        private static void ConfigureBatches(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Batch>(entity =>
            {
                entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
                entity.Property(b => b.FileMac).IsRequired().HasMaxLength(128);
                entity.Property(b => b.OriginalFileName).IsRequired().HasMaxLength(300);
                entity.Property(b => b.BatchStatus).HasDefaultValue(UploadStatus.Failed);

                entity.HasIndex(u => u.Name)
            .HasDatabaseName("IX_batch_name")
           ;

                entity.HasIndex(u => u.BatchStatus)
            .HasDatabaseName("IX_batch_status")
           ;

                // Duplicate-file guard (§4.8), filtered so a soft-deleted batch doesn't block re-upload.
                entity.HasIndex(b => new { b.UploadedByTenantId, b.FileMac })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0")
                      .HasDatabaseName("IX_Batches_UploadedByTenantId_FileMac");

                // Batch list queries, newest first (API §4.8 GET /api/inventory/batches).
                entity.HasIndex(b => new { b.UploadedByTenantId, b.UploadedTime })
                      .HasDatabaseName("IX_Batches_UploadedByTenantId_UploadedTime");
            });
        }

        /// <summary>
        /// Configures the transfer aggregate (ERD §4.3–§4.5, API §4.10).
        /// <para>
        /// Every foreign key out of this aggregate is <c>NoAction</c> except the two that point at
        /// the transfer header itself. That is not caution for its own sake: <c>SourceBranchId</c>
        /// and <c>TargetBranchId</c> both reference <c>Branches</c>, so anything other than
        /// <c>NoAction</c> is a guaranteed multiple-cascade-path error at migration time. It is
        /// also correct on the merits — deleting a branch or a product must never silently erase
        /// the movement history that references it.
        /// </para>
        /// <para>
        /// No soft-delete query filter and no <c>AuditableEntity</c>: these tables are append-only
        /// (ERD §6.5).
        /// </para>
        /// </summary>
        private static void ConfigureCardTransfers(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CardTransfer>(entity =>
            {
                // Name and check constraint in one ToTable call: a second, name-less ToTable
                // overload would reconfigure the table mapping rather than add to it.
                entity.ToTable("CardsTransferHistory", t => t.HasCheckConstraint(   // ERD §4.3 table name
                    "CK_CardsTransferHistory_SourceNotTarget",
                    "[SourceBranchId] <> [TargetBranchId]"));
                entity.HasKey(t => t.Id);

                entity.Property(t => t.TransactionStatus).HasConversion<byte>().IsRequired();
                entity.Property(t => t.Origin).HasConversion<byte>().IsRequired();
                entity.Property(t => t.ActionNotes).HasMaxLength(500);

                // Maker-Checker workflow (Q1): identity is always recorded, even though it is
                // fine for the same account to be both Maker and Checker. CreatedByUsername has a
                // default so existing historical rows (this table is append-only, ERD §6.5) don't
                // need a data migration; CheckedByUsername stays nullable, matching
                // StatusChangedAt's own null-until-settled shape.
                entity.Property(t => t.CreatedByUsername).IsRequired().HasMaxLength(100).HasDefaultValue("unknown");
                entity.Property(t => t.CheckedByUsername).HasMaxLength(100);

                entity.HasOne(t => t.Tenant)
                      .WithMany()
                      .HasForeignKey(t => t.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.CreatedByTenant)
                      .WithMany()
                      .HasForeignKey(t => t.CreatedByTenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.SourceBranch)
                      .WithMany()
                      .HasForeignKey(t => t.SourceBranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.TargetBranch)
                      .WithMany()
                      .HasForeignKey(t => t.TargetBranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                // API §4.9: the branch request this transfer fulfils, or null for a direct
                // transfer. Every existing row is NULL — the column already existed (decision Q2
                // of the original Transfers workstream); this adds only the constraint and
                // navigation, so no data migration is needed.
                entity.HasOne(t => t.BranchRequest)
                      .WithMany()
                      .HasForeignKey(t => t.BranchRequestId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Self-reference: an auto-generated return points back at the transfer whose
                // partial receipt produced it.
                entity.HasOne(t => t.ParentTransfer)
                      .WithMany()
                      .HasForeignKey(t => t.ParentTransferId)
                      .OnDelete(DeleteBehavior.NoAction);

                // ERD §4.3 index set, plus two for the origin/parent queries added by decision Q5.
                entity.HasIndex(t => new { t.TenantId, t.CreatedAt })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_CreatedAt");
                entity.HasIndex(t => new { t.TenantId, t.BranchRequestId })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_BranchRequestId");
                entity.HasIndex(t => t.SourceBranchId)
                      .HasDatabaseName("IX_CardsTransferHistory_SourceBranchId");
                entity.HasIndex(t => t.TargetBranchId)
                      .HasDatabaseName("IX_CardsTransferHistory_TargetBranchId");
                entity.HasIndex(t => new { t.TenantId, t.TransactionStatus })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_TransactionStatus");
                entity.HasIndex(t => new { t.TenantId, t.Origin })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_Origin");
                entity.HasIndex(t => t.ParentTransferId)
                      .HasDatabaseName("IX_CardsTransferHistory_ParentTransferId");
            });

            modelBuilder.Entity<CardTransferProduct>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.ProductTransactionWay).HasConversion<byte>().IsRequired();

                // Unknown-way Maker-Checker workflow: nullable (meaningful only once a remainder
                // exists on an Unknown-way line), no IsRequired() call - matches
                // RealQuantityReceived/DisposedQuantity's own nullable-until-settled shape.
                entity.Property(p => p.DifferenceAction).HasConversion<byte>();

                entity.HasOne(p => p.CardTransfer)
                      .WithMany(t => t.Products)
                      .HasForeignKey(p => p.CardTransferId)
                      .OnDelete(DeleteBehavior.Cascade);   // lines belong to the transfer (ERD §4.4)

                entity.HasOne(p => p.Product)
                      .WithMany()
                      .HasForeignKey(p => p.ProductId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(p => p.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(p => new { p.TenantId, p.CardTransferId })
                      .HasDatabaseName("IX_CardTransferProducts_TenantId_CardTransferId");
                entity.HasIndex(p => new { p.CardTransferId, p.ProductId })
                      .IsUnique()
                      .HasDatabaseName("UX_CardTransferProducts_CardTransferId_ProductId");

                entity.ToTable("CardTransferProducts", t =>
                {
                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_TransactedQuantity_Positive",
                        "[TransactedQuantity] > 0");

                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_RealQuantityReceived_NonNegative",
                        "[RealQuantityReceived] IS NULL OR [RealQuantityReceived] >= 0");

                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_DisposedQuantity_NonNegative",
                        "[DisposedQuantity] IS NULL OR [DisposedQuantity] >= 0");

                    // The settlement identity (Addendum A §2.3): what was received plus what was
                    // written off can never exceed what was sent. The returned remainder is
                    // whatever is left over, so this one constraint keeps all three honest and
                    // makes an arithmetic slip in the service layer fail loudly at the database
                    // rather than quietly manufacturing stock.
                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_SettlementWithinTransacted",
                        "ISNULL([RealQuantityReceived], 0) + ISNULL([DisposedQuantity], 0) <= [TransactedQuantity]");
                });
            });

            modelBuilder.Entity<CardTransferItem>(entity =>
            {
                entity.ToTable("CardTransferItems");
                entity.HasKey(i => i.Id);

                entity.Property(i => i.ReceiveStatus).HasConversion<byte>().IsRequired();

                entity.HasOne(i => i.CardTransfer)
                      .WithMany(t => t.Items)
                      .HasForeignKey(i => i.CardTransferId)
                      .OnDelete(DeleteBehavior.Cascade);   // items belong to the transfer (ERD §4.5)

                // NoAction, which also means a batch cannot be deleted while its cards are in
                // flight: Batch → ProductItem cascades, and this constraint blocks that cascade.
                // That is the intended behaviour, not an accident of configuration.
                entity.HasOne(i => i.ProductItem)
                      .WithMany()
                      .HasForeignKey(i => i.ProductItemId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(i => new { i.TenantId, i.CardTransferId })
                      .HasDatabaseName("IX_CardTransferItems_TenantId_CardTransferId");
                entity.HasIndex(i => i.ProductItemId)
                      .HasDatabaseName("IX_CardTransferItems_ProductItemId");
                entity.HasIndex(i => new { i.CardTransferId, i.ProductItemId })
                      .IsUnique()
                      .HasDatabaseName("UX_CardTransferItems_CardTransferId_ProductItemId");
            });
        }

        /// <summary>
        /// Configures the disposal aggregate (API §4.10, Addendum A). Append-only, same rationale
        /// as transfers: no audit block, no soft delete, no query filter.
        /// </summary>
        private static void ConfigureCardDisposals(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CardDisposal>(entity =>
            {
                entity.ToTable("CardDisposals");
                entity.HasKey(d => d.Id);

                // Required at the database level too, not only in validation: a write-off with no
                // stated reason is the exact scenario this table exists to make impossible.
                entity.Property(d => d.Reason).IsRequired().HasMaxLength(500);

                entity.HasOne(d => d.Tenant)
                      .WithMany()
                      .HasForeignKey(d => d.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.DisposedByTenant)
                      .WithMany()
                      .HasForeignKey(d => d.DisposedByTenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.Branch)
                      .WithMany()
                      .HasForeignKey(d => d.BranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.CardTransfer)
                      .WithMany()
                      .HasForeignKey(d => d.CardTransferId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(d => new { d.TenantId, d.DisposedAt })
                      .HasDatabaseName("IX_CardDisposals_TenantId_DisposedAt");
                entity.HasIndex(d => new { d.TenantId, d.BranchId })
                      .HasDatabaseName("IX_CardDisposals_TenantId_BranchId");
                entity.HasIndex(d => d.CardTransferId)
                      .HasDatabaseName("IX_CardDisposals_CardTransferId");
            });

            modelBuilder.Entity<CardDisposalItem>(entity =>
            {
                entity.ToTable("CardDisposalItems");
                entity.HasKey(i => i.Id);

                entity.HasOne(i => i.CardDisposal)
                      .WithMany(d => d.Items)
                      .HasForeignKey(i => i.CardDisposalId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.ProductItem)
                      .WithMany()
                      .HasForeignKey(i => i.ProductItemId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(i => new { i.TenantId, i.CardDisposalId })
                      .HasDatabaseName("IX_CardDisposalItems_TenantId_CardDisposalId");

                // A card can only ever be written off once, so this unique index is a safety net
                // rather than a real constraint on the domain — the service layer refuses to
                // dispose an already-disposed card long before this fires.
                entity.HasIndex(i => new { i.CardDisposalId, i.ProductItemId })
                      .IsUnique()
                      .HasDatabaseName("UX_CardDisposalItems_CardDisposalId_ProductItemId");
            });
        }

        /// <summary>
        /// Configures branch stock requests and their lines (ERD §4.1–§4.2, tables
        /// <c>BranchRequests</c> / <c>BranchRequestItems</c>; API §4.9).
        /// <para>
        /// <c>BranchRequest</c> does not derive from <c>AuditableEntity</c> (decision Q-09): no
        /// soft delete, no query filter, no restore endpoint — the same append-only-with-status
        /// shape <see cref="ConfigureCardTransfers"/> already established for
        /// <see cref="CardTransfer"/>.
        /// </para>
        /// <para>
        /// Cascade-path check: <see cref="BranchRequestItem"/> has exactly one cascade parent
        /// (<c>RequestId</c> → <c>BranchRequests</c>) and two NoAction parents (<c>ProductId</c>,
        /// the denormalized <c>TenantId</c>) — the same shape as <see cref="CardTransferProduct"/>.
        /// <see cref="BranchRequest"/> itself has zero cascade edges into it (every FK out of it
        /// is NoAction), so there is no multiple-cascade-path risk anywhere in this aggregate.
        /// </para>
        /// </summary>
        private static void ConfigureBranchRequests(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BranchRequest>(entity =>
            {
                entity.ToTable("BranchRequests");   // ERD §4.1 table name
                entity.HasKey(r => r.Id);

                entity.Property(r => r.RequestStatus).HasConversion<byte>().IsRequired();
                entity.Property(r => r.ActionNotes).HasMaxLength(500);

                // Two distinct relationships to Tenants — both need their own navigation, or EF
                // Core silently reconfigures the same one and leaves the second FK unmapped (the
                // same pitfall CardTransfer's Tenant/CreatedByTenant pair already documents).
                entity.HasOne(r => r.Tenant)
                      .WithMany()
                      .HasForeignKey(r => r.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.ActionTakenByTenant)
                      .WithMany()
                      .HasForeignKey(r => r.ActionTakenByTenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.RequestingBranch)
                      .WithMany()
                      .HasForeignKey(r => r.RequestingBranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                // ERD §4.1 index set.
                entity.HasIndex(r => new { r.TenantId, r.RequestStatus })
                      .HasDatabaseName("IX_BranchRequests_TenantId_RequestStatus");
                entity.HasIndex(r => new { r.TenantId, r.RequestingBranchId })
                      .HasDatabaseName("IX_BranchRequests_TenantId_RequestingBranchId");
                entity.HasIndex(r => new { r.TenantId, r.RequestDateTime })
                      .HasDatabaseName("IX_BranchRequests_TenantId_RequestDateTime");
            });

            modelBuilder.Entity<BranchRequestItem>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.HasOne(i => i.Request)
                      .WithMany(r => r.Items)
                      .HasForeignKey(i => i.RequestId)
                      .OnDelete(DeleteBehavior.Cascade);   // lines belong to the request (ERD §4.2)

                entity.HasOne(i => i.Product)
                      .WithMany()
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(i => new { i.TenantId, i.RequestId })
                      .HasDatabaseName("IX_BranchRequestItems_TenantId_RequestId");
                entity.HasIndex(i => new { i.RequestId, i.ProductId })
                      .IsUnique()
                      .HasDatabaseName("UX_BranchRequestItems_RequestId_ProductId");

                entity.ToTable("BranchRequestItems", t =>
                {
                    t.HasCheckConstraint(
                        "CK_BranchRequestItems_AskedQuantity_Positive",
                        "[AskedQuantity] > 0");

                    t.HasCheckConstraint(
                        "CK_BranchRequestItems_DispatchedQuantity_NonNegative",
                        "[DispatchedQuantity] >= 0");

                    t.HasCheckConstraint(
                        "CK_BranchRequestItems_ReceivedQuantity_NonNegative",
                        "[ReceivedQuantity] >= 0");
                });
            });
        }

        /// <summary>
        /// Configures the printer registry (ERD §6, tables <c>Printers</c> /
        /// <c>MaticaPrinterConfigurations</c>; Printing Module decision Q-01).
        /// <para>
        /// <see cref="Printer"/> holds the fields common to both printer families; only Matica
        /// printers extend it with a 1:1 <see cref="MaticaPrinterConfiguration"/> row. Evolis
        /// needs no extension table at all (module requirement §1), so no row in
        /// <c>MaticaPrinterConfigurations</c> ever points at an Evolis printer.
        /// </para>
        /// <para>
        /// The <c>Printer</c> → <c>MaticaPrinterConfiguration</c> edge uses
        /// <see cref="DeleteBehavior.Cascade"/> rather than this codebase's usual cross-aggregate
        /// <see cref="DeleteBehavior.NoAction"/> convention, because the two rows are the same
        /// aggregate (the Matica row is a detail extension of its printer, not a reference to a
        /// separate aggregate root) — the same reasoning already applied to
        /// <c>BranchRequestItem.RequestId</c> and <c>CardDisposalItem.CardDisposalId</c>.
        /// <para>
        /// <b>P5 addition:</b> <see cref="Printer.Branch"/> and
        /// <see cref="MaticaPrinterConfiguration.Printer"/> are navigation properties added after
        /// P1 shipped, to let <c>PrinterRepo</c> eager-load branch names and to let
        /// <c>PrinterConfigurationService</c> insert a printer and its Matica configuration in
        /// one <c>SaveChanges</c> call (EF Core fixes up the configuration's FK from the
        /// printer's generated identity via the navigation). Both foreign keys already existed as
        /// scalar columns since P1 — this changes only in-memory relationship metadata, not the
        /// schema, so it produces no new migration.
        /// </para>
        /// </summary>
        private static void ConfigurePrinterRegistry(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Printer>(entity =>
            {
                entity.ToTable("Printers");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.UsingPrinterType).HasConversion<byte>().IsRequired();
                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Model).IsRequired().HasMaxLength(100);
                entity.Property(p => p.UniqueNumber).IsRequired().HasMaxLength(50);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(p => p.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(p => p.Branch)
                      .WithMany()
                      .HasForeignKey(p => p.BranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Decision Q-09: tenant users list/filter printers by type and branch — both
                // need to be cheap.
                entity.HasIndex(p => new { p.TenantId, p.BranchId })
                      .HasDatabaseName("IX_Printers_TenantId_BranchId");

                entity.HasIndex(p => new { p.TenantId, p.UsingPrinterType })
                      .HasDatabaseName("IX_Printers_TenantId_UsingPrinterType");

                // A printer's serial (Evolis) / IP (Matica) is unique per tenant among active
                // rows, so the same physical device cannot be registered twice by mistake.
                entity.HasIndex(p => new { p.TenantId, p.UniqueNumber })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0")
                      .HasDatabaseName("UX_Printers_TenantId_UniqueNumber");

                entity.HasQueryFilter(p => !p.IsDeleted);
            });

            modelBuilder.Entity<MaticaPrinterConfiguration>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.Property(m => m.Port).IsRequired().HasMaxLength(50);
                entity.Property(m => m.IpAddress).IsRequired().HasMaxLength(45);

                entity.HasOne(m => m.Printer)
                      .WithOne()
                      .HasForeignKey<MaticaPrinterConfiguration>(m => m.PrinterId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(m => m.PrinterId)
                      .IsUnique()
                      .HasDatabaseName("UX_MaticaPrinterConfigurations_PrinterId");

                entity.HasQueryFilter(m => !m.IsDeleted);

                // No CHECK constraints on FeederId/HopperId/RejectedId (decision Q-03 follow-up):
                // FontSize is the only numeric field in this module that gets a DB-level guard.
                entity.ToTable("MaticaPrinterConfigurations");
            });
            modelBuilder.Entity<EvolisPrinterConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.SupervisorHostName).HasMaxLength(100);

                entity.Property(e => e.MagCoercivity).HasConversion<byte>().IsRequired().HasDefaultValue(MagCoercivity.PRINTER);
                // smallint, not tinyint: OutputTray's largest real SDK member (LOCKED = 256)
                // does not fit in a byte.
                entity.Property(e => e.InputTray).HasConversion<short>().IsRequired().HasDefaultValue(InputTray.FEEDER);
                entity.Property(e => e.OutputTray).HasConversion<short>().IsRequired().HasDefaultValue(OutputTray.STANDARD);
                entity.Property(e => e.ErrorTray).HasConversion<short>().IsRequired().HasDefaultValue(OutputTray.ERROR);
                entity.Property(e => e.OpenMode).HasConversion<byte>().IsRequired().HasDefaultValue(EvolisOpenMode.SUPERVISED);
                entity.Property(e => e.ErrorManagement).HasConversion<byte>().IsRequired().HasDefaultValue(EvolisErrorManagement.SOFTWARE);

                entity.HasOne(e => e.Printer)
                      .WithOne()
                      .HasForeignKey<EvolisPrinterConfiguration>(e => e.PrinterId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<RibbonType>()
                      .WithMany()
                      .HasForeignKey(e => e.DefaultRibbonTypeId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => e.PrinterId)
                      .IsUnique()
                      .HasDatabaseName("UX_EvolisPrinterConfigurations_PrinterId");

                entity.HasIndex(e => new { e.TenantId, e.DeviceName })
                      .HasDatabaseName("IX_EvolisPrinterConfigurations_TenantId_DeviceName");

                // The real integrity rule (plan §6): one physical device name may legitimately
                // repeat across different supervising machines, but must not be registered twice
                // against the same one. SupervisorHostName is null until Phase 2's
                // self-registration route writes it, so this index is inert until then.
                entity.HasIndex(e => new { e.TenantId, e.SupervisorHostName, e.DeviceName })
                      .IsUnique()
                      .HasFilter("[SupervisorHostName] IS NOT NULL AND [IsDeleted] = 0")
                      .HasDatabaseName("UX_EvolisPrinterConfigurations_TenantId_SupervisorHostName_DeviceName");

                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.ToTable("EvolisPrinterConfigurations");
            });
        }

        /// <summary>
        /// Configures the product print-configuration extension (ERD §7, tables
        /// <c>RibbonTypes</c> / <c>MaticaProductPrintConfigurations</c> /
        /// <c>EvolisProductPrintConfigurations</c>) and uploaded print images (module
        /// requirements §5–§7).
        /// <para>
        /// <see cref="MaticaProductPrintConfiguration"/> and
        /// <see cref="EvolisProductPrintConfiguration"/> each carry a filtered unique index on
        /// <c>(TenantId, ProductId)</c> — decision Q-02 locks exactly one configuration row per
        /// product, never one per face. Both use <see cref="DeleteBehavior.NoAction"/> on their
        /// <c>ProductId</c> FK, matching every other cross-aggregate reference into
        /// <c>Products</c> in this codebase (e.g. <c>BranchRequestItem.ProductId</c>): the
        /// product/config lifecycle pairing (module requirement §2) is enforced in the service
        /// layer, inside the same <c>IUnitOfWork.ExecuteInTransactionAsync</c> call that writes
        /// the product itself — never by a database cascade. The one deliberate exception is the
        /// printer-family switch (decision Q-08), where the old configuration row is explicitly
        /// hard-deleted by the service, not soft-deleted.
        /// </para>
        /// <para>
        /// <b>P6 addition:</b> <see cref="MaticaProductPrintConfiguration.Product"/> and
        /// <see cref="EvolisProductPrintConfiguration.Product"/> are navigation properties added
        /// after P1 shipped, for the same reason as <see cref="Printer.Branch"/> /
        /// <see cref="MaticaPrinterConfiguration.Printer"/> (P5): a product created in the same
        /// transaction as its print configuration has no real id yet when the configuration
        /// object is built, and EF Core needs the navigation to fix up the foreign key once that
        /// id is generated. Metadata only — no schema change, no new migration.
        /// </para>
        /// </summary>
        private static void ConfigureProductPrintConfigurations(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RibbonType>(entity =>
            {
                entity.ToTable("RibbonTypes");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);

                entity.HasIndex(r => r.Name)
                      .IsUnique()
                      .HasDatabaseName("UX_RibbonTypes_Name");
                entity.Property(r => r.SdkCode).IsRequired().HasMaxLength(30);
                entity.HasIndex(r => r.SdkCode).IsUnique().HasDatabaseName("UX_RibbonTypes_SdkCode");

                // Seed values: SdkCode is the exact Evolis.RibbonType enum member name, confirmed
                // via reflection against the installed Evolis.SDK 9.4.1 assembly
                // (evolis-sdk-cs.dll) — not guessed. KWHITE was additionally confirmed live
                // against a connected Evolis Primacy 2 via Connection.GetRibbonInfo(). The rest
                // are the other real enum members forming the common color (RC_) and monochrome
                // (RM_) ribbon families documented on SettingsKey.GRibbonType.
                entity.HasData(
                    new RibbonType { Id = 1, Name = "YMCKO Color", SdkCode = "YMCKO" },
                    new RibbonType { Id = 2, Name = "YMCK Color", SdkCode = "YMCK" },
                    new RibbonType { Id = 3, Name = "YMCKOK Color", SdkCode = "YMCKOK" },
                    new RibbonType { Id = 4, Name = "Monochrome Black", SdkCode = "KBLACK" },
                    new RibbonType { Id = 5, Name = "Monochrome White", SdkCode = "KWHITE" },
                    new RibbonType { Id = 6, Name = "Monochrome Red", SdkCode = "KRED" },
                    new RibbonType { Id = 7, Name = "Monochrome Blue", SdkCode = "KBLUE" },
                    new RibbonType { Id = 8, Name = "Monochrome Green", SdkCode = "KGREEN" },
                    new RibbonType { Id = 9, Name = "Holographic Overlay", SdkCode = "HOLO" },
                    new RibbonType { Id = 10, Name = "Clear Overlay", SdkCode = "CLEAR" });
            });

            modelBuilder.Entity<MaticaProductPrintConfiguration>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(m => m.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(m => m.Product)
                      .WithMany()
                      .HasForeignKey(m => m.ProductId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Single source of truth for image data (revision, "Print Images & Product Print
                // Configuration" change request, point 6): references PrintImages.Id instead of
                // carrying a bare path string. NoAction, matching this codebase's cross-aggregate
                // FK convention — replacing an image happens in place on the same PrintImage row
                // (PUT /api/print-images/{id}), so this FK is never left dangling by a replace.
                entity.HasOne<PrintImage>()
                      .WithMany()
                      .HasForeignKey(m => m.ImageId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(m => m.ImageId)
                      .HasDatabaseName("IX_MaticaProductPrintConfigurations_ImageId");

                // Decision Q-02: exactly one row per product among non-deleted rows.
                entity.HasIndex(m => new { m.TenantId, m.ProductId })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0")
                      .HasDatabaseName("UX_MaticaProductPrintConfigurations_TenantId_ProductId");

                entity.HasQueryFilter(m => !m.IsDeleted);

                // Revised (was "> 0"): 0 must be a valid FontSize, per explicit correction to the
                // original Q-03 follow-up decision. Cpi/OffsetX/OffsetY remain unconstrained.
                entity.ToTable("MaticaProductPrintConfigurations", t =>
                {
                    t.HasCheckConstraint("CK_MaticaProductPrintConfigurations_FontSize_NonNegative", "[FontSize] >= 0");
                });
            });

            modelBuilder.Entity<EvolisProductPrintConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PrintWay).HasConversion<byte>().IsRequired();
                entity.Property(e => e.PrintedFace).HasConversion<byte>().IsRequired();
                entity.Property(e => e.FontFamily).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PrintColor).IsRequired().HasMaxLength(9);
                entity.Property(e => e.BackgroundColor).IsRequired().HasMaxLength(9);
                entity.Property(e => e.FontStyle).IsRequired().HasMaxLength(50);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<RibbonType>()
                      .WithMany()
                      .HasForeignKey(e => e.RibbonTypeId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Single source of truth for image data — see the parallel comment on
                // MaticaProductPrintConfiguration above.
                entity.HasOne<PrintImage>()
                      .WithMany()
                      .HasForeignKey(e => e.ImageId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => e.ImageId)
                      .HasDatabaseName("IX_EvolisProductPrintConfigurations_ImageId");

                // Decision Q-02: exactly one row per product among non-deleted rows.
                entity.HasIndex(e => new { e.TenantId, e.ProductId })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0")
                      .HasDatabaseName("UX_EvolisProductPrintConfigurations_TenantId_ProductId");

                entity.HasIndex(e => e.RibbonTypeId)
                      .HasDatabaseName("IX_EvolisProductPrintConfigurations_RibbonTypeId");

                entity.HasQueryFilter(e => !e.IsDeleted);

                // Revised (was "> 0"): 0 must be a valid FontSize, per explicit correction to the
                // original Q-03 follow-up decision. HEX-format validation for PrintColor/
                // BackgroundColor is left entirely to the Application-layer validator, not
                // enforced here.
                entity.ToTable("EvolisProductPrintConfigurations", t =>
                {
                    t.HasCheckConstraint("CK_EvolisProductPrintConfigurations_FontSize_NonNegative", "[FontSize] >= 0");
                });
            });

            modelBuilder.Entity<PrintImage>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.Property(i => i.OriginalFileName).IsRequired().HasMaxLength(260);
                entity.Property(i => i.StoredFileName).IsRequired().HasMaxLength(260);
                entity.Property(i => i.StoredPath).IsRequired().HasMaxLength(500);
                entity.Property(i => i.ContentType).IsRequired().HasMaxLength(100);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Decision Q-10: duplicate detection is scoped to (TenantId, OriginalFileName).
                // A second upload with the same name replaces this row (the service deletes then
                // inserts inside one transaction), so at most one row can ever exist per pair —
                // enforced here, not just assumed by the service.
                entity.HasIndex(i => new { i.TenantId, i.OriginalFileName })
                      .IsUnique()
                      .HasDatabaseName("UX_PrintImages_TenantId_OriginalFileName");

                // No CHECK constraint on SizeBytes (decision Q-03 follow-up): FontSize is the
                // only numeric field in this module that gets a DB-level guard.
                entity.ToTable("PrintImages");
            });
        }
    }
}
