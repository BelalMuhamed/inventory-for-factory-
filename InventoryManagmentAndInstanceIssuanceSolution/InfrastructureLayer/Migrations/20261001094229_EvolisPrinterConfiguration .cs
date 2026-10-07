using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class EvolisPrinterConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SdkCode",
                table: "RibbonTypes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EvolisPrinterConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    PrinterId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupervisorHostName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupervisorRegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DefaultRibbonTypeId = table.Column<long>(type: "bigint", nullable: true),
                    MagCoercivity = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    InputTray = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    OutputTray = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)2),
                    ErrorTray = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)3),
                    OpenMode = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)2),
                    ErrorManagement = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolisPrinterConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvolisPrinterConfigurations_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvolisPrinterConfigurations_RibbonTypes_DefaultRibbonTypeId",
                        column: x => x.DefaultRibbonTypeId,
                        principalTable: "RibbonTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvolisPrinterConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_RibbonTypes_SdkCode",
                table: "RibbonTypes",
                column: "SdkCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvolisPrinterConfigurations_DefaultRibbonTypeId",
                table: "EvolisPrinterConfigurations",
                column: "DefaultRibbonTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolisPrinterConfigurations_TenantId_DeviceName",
                table: "EvolisPrinterConfigurations",
                columns: new[] { "TenantId", "DeviceName" });

            migrationBuilder.CreateIndex(
                name: "UX_EvolisPrinterConfigurations_PrinterId",
                table: "EvolisPrinterConfigurations",
                column: "PrinterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EvolisPrinterConfigurations_TenantId_SupervisorHostName_DeviceName",
                table: "EvolisPrinterConfigurations",
                columns: new[] { "TenantId", "SupervisorHostName", "DeviceName" },
                unique: true,
                filter: "[SupervisorHostName] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolisPrinterConfigurations");

            migrationBuilder.DropIndex(
                name: "UX_RibbonTypes_SdkCode",
                table: "RibbonTypes");

            migrationBuilder.DropColumn(
                name: "SdkCode",
                table: "RibbonTypes");
        }
    }
}
