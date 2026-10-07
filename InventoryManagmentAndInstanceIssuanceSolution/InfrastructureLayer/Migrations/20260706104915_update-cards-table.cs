using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class updatecardstable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Cards",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId",
                table: "Cards");

            migrationBuilder.AddColumn<long>(
                name: "ID",
                table: "Cards",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "BranchID",
                table: "Cards",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cards",
                table: "Cards",
                column: "ID");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BranchID",
                table: "Cards",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards",
                columns: new[] { "TenantId", "EncryptedPan" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Branches_BranchID",
                table: "Cards",
                column: "BranchID",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Branches_BranchID",
                table: "Cards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cards",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_BranchID",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TenantId_EncryptedPan",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BranchID",
                table: "Cards");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cards",
                table: "Cards",
                column: "EncryptedPan");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId",
                table: "Cards",
                column: "TenantId");
        }
    }
}
