using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class T0NullableCardBranchDisposedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Branches_BranchID",
                table: "Cards");

            migrationBuilder.AlterColumn<long>(
                name: "BranchID",
                table: "Cards",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_BranchId_Status",
                table: "Cards",
                columns: new[] { "TenantId", "BranchID", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Branches_BranchID",
                table: "Cards",
                column: "BranchID",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Branches_BranchID",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_BranchId_Status",
                table: "Cards");

            migrationBuilder.AlterColumn<long>(
                name: "BranchID",
                table: "Cards",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Branches_BranchID",
                table: "Cards",
                column: "BranchID",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
