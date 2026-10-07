using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class R1BranchRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    RequestingBranchId = table.Column<long>(type: "bigint", nullable: false),
                    RequestDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ActionTakenByTenantId = table.Column<long>(type: "bigint", nullable: true),
                    ActionTakenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchRequests_Branches_RequestingBranchId",
                        column: x => x.RequestingBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BranchRequests_Tenants_ActionTakenByTenantId",
                        column: x => x.ActionTakenByTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BranchRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BranchRequestItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    AskedQuantity = table.Column<int>(type: "int", nullable: false),
                    DispatchedQuantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRequestItems", x => x.Id);
                    table.CheckConstraint("CK_BranchRequestItems_AskedQuantity_Positive", "[AskedQuantity] > 0");
                    table.CheckConstraint("CK_BranchRequestItems_DispatchedQuantity_NonNegative", "[DispatchedQuantity] >= 0");
                    table.CheckConstraint("CK_BranchRequestItems_ReceivedQuantity_NonNegative", "[ReceivedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_BranchRequestItems_BranchRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "BranchRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchRequestItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BranchRequestItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardsTransferHistory_BranchRequestId",
                table: "CardsTransferHistory",
                column: "BranchRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequestItems_ProductId",
                table: "BranchRequestItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequestItems_TenantId_RequestId",
                table: "BranchRequestItems",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "UX_BranchRequestItems_RequestId_ProductId",
                table: "BranchRequestItems",
                columns: new[] { "RequestId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_ActionTakenByTenantId",
                table: "BranchRequests",
                column: "ActionTakenByTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_RequestingBranchId",
                table: "BranchRequests",
                column: "RequestingBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_TenantId_RequestDateTime",
                table: "BranchRequests",
                columns: new[] { "TenantId", "RequestDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_TenantId_RequestingBranchId",
                table: "BranchRequests",
                columns: new[] { "TenantId", "RequestingBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_TenantId_RequestStatus",
                table: "BranchRequests",
                columns: new[] { "TenantId", "RequestStatus" });

            migrationBuilder.AddForeignKey(
                name: "FK_CardsTransferHistory_BranchRequests_BranchRequestId",
                table: "CardsTransferHistory",
                column: "BranchRequestId",
                principalTable: "BranchRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardsTransferHistory_BranchRequests_BranchRequestId",
                table: "CardsTransferHistory");

            migrationBuilder.DropTable(
                name: "BranchRequestItems");

            migrationBuilder.DropTable(
                name: "BranchRequests");

            migrationBuilder.DropIndex(
                name: "IX_CardsTransferHistory_BranchRequestId",
                table: "CardsTransferHistory");
        }
    }
}
