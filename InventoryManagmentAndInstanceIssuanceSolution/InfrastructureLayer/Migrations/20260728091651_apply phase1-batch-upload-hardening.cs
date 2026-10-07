using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class applyphase1batchuploadhardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Batches_UploadedByTenantId",
                table: "Batches");

            migrationBuilder.RenameIndex(
                name: "IX_Products_TenantId_Name",
                table: "Products",
                newName: "IX_Product_TenantId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Branches_TenantId_Name",
                table: "Branches",
                newName: "IX_Branch_TenantId_Name");

            migrationBuilder.RenameColumn(
                name: "fileMac",
                table: "Batches",
                newName: "FileMac");

            migrationBuilder.AddColumn<string>(
                name: "MaskedPan",
                table: "Cards",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "FileMac",
                table: "Batches",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ProcessingError",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Batches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "BatchStatus",
                table: "Batches",
                type: "int",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "Batches",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_TenantId_ProductId",
                table: "Stocks",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_AvailableQuantity_NonNegative",
                table: "Stocks",
                sql: "[AvailableQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_HoldQuantity_NonNegative",
                table: "Stocks",
                sql: "[HoldQuantity] >= 0");

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

            migrationBuilder.CreateIndex(
                name: "IX_Category_Name",
                table: "Branches",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_UploadedByTenantId_FileMac",
                table: "Batches",
                columns: new[] { "UploadedByTenantId", "FileMac" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_UploadedByTenantId_UploadedTime",
                table: "Batches",
                columns: new[] { "UploadedByTenantId", "UploadedTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stocks_TenantId_ProductId",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_AvailableQuantity_NonNegative",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_HoldQuantity_NonNegative",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_ProductId_BranchId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Category_Name",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Batches_UploadedByTenantId_FileMac",
                table: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_Batches_UploadedByTenantId_UploadedTime",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "MaskedPan",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "Batches");

            migrationBuilder.RenameIndex(
                name: "IX_Product_TenantId_Name",
                table: "Products",
                newName: "IX_Products_TenantId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Branch_TenantId_Name",
                table: "Branches",
                newName: "IX_Branches_TenantId_Name");

            migrationBuilder.RenameColumn(
                name: "FileMac",
                table: "Batches",
                newName: "fileMac");

            migrationBuilder.AlterColumn<int>(
                name: "ProcessingError",
                table: "Batches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Batches",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "fileMac",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<int>(
                name: "BatchStatus",
                table: "Batches",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "EncryptedPan" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_UploadedByTenantId",
                table: "Batches",
                column: "UploadedByTenantId");
        }
    }
}
