using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class inventoryapireconciliationcredentialpatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintAgentServiceAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintAgentServiceAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintAgentServiceAccounts_BranchId",
                table: "PrintAgentServiceAccounts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintAgentServiceAccounts_TenantId",
                table: "PrintAgentServiceAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_PrintAgentServiceAccounts_ClientId",
                table: "PrintAgentServiceAccounts",
                column: "ClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintAgentServiceAccounts");
        }
    }
}
