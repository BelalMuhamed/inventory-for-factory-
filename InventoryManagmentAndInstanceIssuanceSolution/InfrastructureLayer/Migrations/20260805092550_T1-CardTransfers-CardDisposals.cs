using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class T1CardTransfersCardDisposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardsTransferHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BranchRequestId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByTenantId = table.Column<long>(type: "bigint", nullable: false),
                    SourceBranchId = table.Column<long>(type: "bigint", nullable: false),
                    TargetBranchId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Origin = table.Column<byte>(type: "tinyint", nullable: false),
                    ParentTransferId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardsTransferHistory", x => x.Id);
                    table.CheckConstraint("CK_CardsTransferHistory_SourceNotTarget", "[SourceBranchId] <> [TargetBranchId]");
                    table.ForeignKey(
                        name: "FK_CardsTransferHistory_Branches_SourceBranchId",
                        column: x => x.SourceBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardsTransferHistory_Branches_TargetBranchId",
                        column: x => x.TargetBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardsTransferHistory_CardsTransferHistory_ParentTransferId",
                        column: x => x.ParentTransferId,
                        principalTable: "CardsTransferHistory",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardsTransferHistory_Tenants_CreatedByTenantId",
                        column: x => x.CreatedByTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardsTransferHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CardDisposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    CardTransferId = table.Column<long>(type: "bigint", nullable: true),
                    DisposedByTenantId = table.Column<long>(type: "bigint", nullable: false),
                    DisposedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardDisposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardDisposals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardDisposals_CardsTransferHistory_CardTransferId",
                        column: x => x.CardTransferId,
                        principalTable: "CardsTransferHistory",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardDisposals_Tenants_DisposedByTenantId",
                        column: x => x.DisposedByTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardDisposals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CardTransferItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CardTransferId = table.Column<long>(type: "bigint", nullable: false),
                    ProductItemId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiveStatus = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardTransferItems_CardsTransferHistory_CardTransferId",
                        column: x => x.CardTransferId,
                        principalTable: "CardsTransferHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardTransferItems_Cards_ProductItemId",
                        column: x => x.ProductItemId,
                        principalTable: "Cards",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_CardTransferItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CardTransferProducts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CardTransferId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    TransactedQuantity = table.Column<int>(type: "int", nullable: false),
                    RealQuantityReceived = table.Column<int>(type: "int", nullable: true),
                    DisposedQuantity = table.Column<int>(type: "int", nullable: true),
                    ProductTransactionWay = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTransferProducts", x => x.Id);
                    table.CheckConstraint("CK_CardTransferProducts_DisposedQuantity_NonNegative", "[DisposedQuantity] IS NULL OR [DisposedQuantity] >= 0");
                    table.CheckConstraint("CK_CardTransferProducts_RealQuantityReceived_NonNegative", "[RealQuantityReceived] IS NULL OR [RealQuantityReceived] >= 0");
                    table.CheckConstraint("CK_CardTransferProducts_SettlementWithinTransacted", "ISNULL([RealQuantityReceived], 0) + ISNULL([DisposedQuantity], 0) <= [TransactedQuantity]");
                    table.CheckConstraint("CK_CardTransferProducts_TransactedQuantity_Positive", "[TransactedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_CardTransferProducts_CardsTransferHistory_CardTransferId",
                        column: x => x.CardTransferId,
                        principalTable: "CardsTransferHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardTransferProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CardTransferProducts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CardDisposalItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CardDisposalId = table.Column<long>(type: "bigint", nullable: false),
                    ProductItemId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardDisposalItems_CardDisposals_CardDisposalId",
                        column: x => x.CardDisposalId,
                        principalTable: "CardDisposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardDisposalItems_Cards_ProductItemId",
                        column: x => x.ProductItemId,
                        principalTable: "Cards",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_CardDisposalItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposalItems_ProductItemId",
                table: "CardDisposalItems",
                column: "ProductItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposalItems_TenantId_CardDisposalId",
                table: "CardDisposalItems",
                columns: new[] { "TenantId", "CardDisposalId" });

            migrationBuilder.CreateIndex(
                name: "UX_CardDisposalItems_CardDisposalId_ProductItemId",
                table: "CardDisposalItems",
                columns: new[] { "CardDisposalId", "ProductItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposals_BranchId",
                table: "CardDisposals",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposals_CardTransferId",
                table: "CardDisposals",
                column: "CardTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposals_DisposedByTenantId",
                table: "CardDisposals",
                column: "DisposedByTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposals_TenantId_BranchId",
                table: "CardDisposals",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CardDisposals_TenantId_DisposedAt",
                table: "CardDisposals",
                columns: new[] { "TenantId", "DisposedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_CreatedByTenantId",
                table: "CardsTransferHistory",
                column: "CreatedByTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_ParentTransferId",
                table: "CardsTransferHistory",
                column: "ParentTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_SourceBranchId",
                table: "CardsTransferHistory",
                column: "SourceBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_TargetBranchId",
                table: "CardsTransferHistory",
                column: "TargetBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_TenantId_BranchRequestId",
                table: "CardsTransferHistory",
                columns: new[] { "TenantId", "BranchRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_TenantId_CreatedAt",
                table: "CardsTransferHistory",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_TenantId_Origin",
                table: "CardsTransferHistory",
                columns: new[] { "TenantId", "Origin" });

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_TenantId_TransactionStatus",
                table: "CardsTransferHistory",
                columns: new[] { "TenantId", "TransactionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CardTransferItems_ProductItemId",
                table: "CardTransferItems",
                column: "ProductItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CardTransferItems_TenantId_CardTransferId",
                table: "CardTransferItems",
                columns: new[] { "TenantId", "CardTransferId" });

            migrationBuilder.CreateIndex(
                name: "UX_CardTransferItems_CardTransferId_ProductItemId",
                table: "CardTransferItems",
                columns: new[] { "CardTransferId", "ProductItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardTransferProducts_ProductId",
                table: "CardTransferProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CardTransferProducts_TenantId_CardTransferId",
                table: "CardTransferProducts",
                columns: new[] { "TenantId", "CardTransferId" });

            migrationBuilder.CreateIndex(
                name: "UX_CardTransferProducts_CardTransferId_ProductId",
                table: "CardTransferProducts",
                columns: new[] { "CardTransferId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardDisposalItems");

            migrationBuilder.DropTable(
                name: "CardTransferItems");

            migrationBuilder.DropTable(
                name: "CardTransferProducts");

            migrationBuilder.DropTable(
                name: "CardDisposals");

            migrationBuilder.DropTable(
                name: "CardsTransferHistory");
        }
    }
}
