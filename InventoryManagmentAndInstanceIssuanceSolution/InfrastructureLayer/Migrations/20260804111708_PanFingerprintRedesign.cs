using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class PanFingerprintRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "EncryptedPan",
                table: "Cards");

            migrationBuilder.AddColumn<byte[]>(
                name: "PanFingerprint",
                table: "Cards",
                type: "binary(32)",
                fixedLength: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_PanFingerprint",
                table: "Cards",
                columns: new[] { "TenantId", "PanFingerprint" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_PanFingerprint",
                table: "Cards",
                columns: new[] { "TenantId", "ProductId", "BranchID", "PanFingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_PanFingerprint",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_PanFingerprint",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PanFingerprint",
                table: "Cards");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedPan",
                table: "Cards",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "EncryptedPan" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "ProductId", "BranchID", "EncryptedPan" });
        }
    }
}
