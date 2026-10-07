using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class last : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MaticaProductPrintConfigurations_FontSize_Positive",
                table: "MaticaProductPrintConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EvolisProductPrintConfigurations_FontSize_Positive",
                table: "EvolisProductPrintConfigurations");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "MaticaProductPrintConfigurations");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "EvolisProductPrintConfigurations");

            migrationBuilder.AddColumn<long>(
                name: "ImageId",
                table: "MaticaProductPrintConfigurations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImageId",
                table: "EvolisProductPrintConfigurations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaticaProductPrintConfigurations_ImageId",
                table: "MaticaProductPrintConfigurations",
                column: "ImageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaticaProductPrintConfigurations_FontSize_NonNegative",
                table: "MaticaProductPrintConfigurations",
                sql: "[FontSize] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_EvolisProductPrintConfigurations_ImageId",
                table: "EvolisProductPrintConfigurations",
                column: "ImageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EvolisProductPrintConfigurations_FontSize_NonNegative",
                table: "EvolisProductPrintConfigurations",
                sql: "[FontSize] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_EvolisProductPrintConfigurations_PrintImages_ImageId",
                table: "EvolisProductPrintConfigurations",
                column: "ImageId",
                principalTable: "PrintImages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaticaProductPrintConfigurations_PrintImages_ImageId",
                table: "MaticaProductPrintConfigurations",
                column: "ImageId",
                principalTable: "PrintImages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvolisProductPrintConfigurations_PrintImages_ImageId",
                table: "EvolisProductPrintConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_MaticaProductPrintConfigurations_PrintImages_ImageId",
                table: "MaticaProductPrintConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_MaticaProductPrintConfigurations_ImageId",
                table: "MaticaProductPrintConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaticaProductPrintConfigurations_FontSize_NonNegative",
                table: "MaticaProductPrintConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_EvolisProductPrintConfigurations_ImageId",
                table: "EvolisProductPrintConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EvolisProductPrintConfigurations_FontSize_NonNegative",
                table: "EvolisProductPrintConfigurations");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "MaticaProductPrintConfigurations");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "EvolisProductPrintConfigurations");

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "MaticaProductPrintConfigurations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "EvolisProductPrintConfigurations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaticaProductPrintConfigurations_FontSize_Positive",
                table: "MaticaProductPrintConfigurations",
                sql: "[FontSize] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EvolisProductPrintConfigurations_FontSize_Positive",
                table: "EvolisProductPrintConfigurations",
                sql: "[FontSize] > 0");
        }
    }
}
