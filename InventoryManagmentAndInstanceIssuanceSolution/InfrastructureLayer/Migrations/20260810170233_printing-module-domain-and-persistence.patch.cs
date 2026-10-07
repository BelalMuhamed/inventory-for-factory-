using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class printingmoduledomainandpersistencepatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaticaProductPrintConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Cpi = table.Column<int>(type: "int", nullable: false),
                    FontSize = table.Column<int>(type: "int", nullable: false),
                    OffsetX = table.Column<int>(type: "int", nullable: false),
                    OffsetY = table.Column<int>(type: "int", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaticaProductPrintConfigurations", x => x.Id);
                    table.CheckConstraint("CK_MaticaProductPrintConfigurations_FontSize_Positive", "[FontSize] > 0");
                    table.ForeignKey(
                        name: "FK_MaticaProductPrintConfigurations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaticaProductPrintConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    UsingPrinterType = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UniqueNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Printers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Printers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrintImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintImages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RibbonTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RibbonTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaticaPrinterConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrinterId = table.Column<long>(type: "bigint", nullable: false),
                    FeederId = table.Column<int>(type: "int", nullable: false),
                    HopperId = table.Column<int>(type: "int", nullable: false),
                    RejectedId = table.Column<int>(type: "int", nullable: false),
                    Port = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaticaPrinterConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaticaPrinterConfigurations_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvolisProductPrintConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    RibbonTypeId = table.Column<long>(type: "bigint", nullable: false),
                    PrintWay = table.Column<byte>(type: "tinyint", nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false),
                    PrintedFace = table.Column<byte>(type: "tinyint", nullable: false),
                    FontFamily = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FontSize = table.Column<int>(type: "int", nullable: false),
                    PrintColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    FontStyle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolisProductPrintConfigurations", x => x.Id);
                    table.CheckConstraint("CK_EvolisProductPrintConfigurations_FontSize_Positive", "[FontSize] > 0");
                    table.ForeignKey(
                        name: "FK_EvolisProductPrintConfigurations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvolisProductPrintConfigurations_RibbonTypes_RibbonTypeId",
                        column: x => x.RibbonTypeId,
                        principalTable: "RibbonTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvolisProductPrintConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvolisProductPrintConfigurations_ProductId",
                table: "EvolisProductPrintConfigurations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolisProductPrintConfigurations_RibbonTypeId",
                table: "EvolisProductPrintConfigurations",
                column: "RibbonTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_EvolisProductPrintConfigurations_TenantId_ProductId",
                table: "EvolisProductPrintConfigurations",
                columns: new[] { "TenantId", "ProductId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_MaticaPrinterConfigurations_PrinterId",
                table: "MaticaPrinterConfigurations",
                column: "PrinterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaticaProductPrintConfigurations_ProductId",
                table: "MaticaProductPrintConfigurations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_MaticaProductPrintConfigurations_TenantId_ProductId",
                table: "MaticaProductPrintConfigurations",
                columns: new[] { "TenantId", "ProductId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_BranchId",
                table: "Printers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TenantId_BranchId",
                table: "Printers",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TenantId_UsingPrinterType",
                table: "Printers",
                columns: new[] { "TenantId", "UsingPrinterType" });

            migrationBuilder.CreateIndex(
                name: "UX_Printers_TenantId_UniqueNumber",
                table: "Printers",
                columns: new[] { "TenantId", "UniqueNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_PrintImages_TenantId_OriginalFileName",
                table: "PrintImages",
                columns: new[] { "TenantId", "OriginalFileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RibbonTypes_Name",
                table: "RibbonTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolisProductPrintConfigurations");

            migrationBuilder.DropTable(
                name: "MaticaPrinterConfigurations");

            migrationBuilder.DropTable(
                name: "MaticaProductPrintConfigurations");

            migrationBuilder.DropTable(
                name: "PrintImages");

            migrationBuilder.DropTable(
                name: "RibbonTypes");

            migrationBuilder.DropTable(
                name: "Printers");
        }
    }
}
