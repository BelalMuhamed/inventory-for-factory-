using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class U1UnknownTransferMakerChecker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Maker-Checker identity (Q1): CreatedByUsername gets a default so this
            // append-only table's existing historical rows don't need a data backfill;
            // CheckedByUsername stays nullable, matching StatusChangedAt's own
            // null-until-settled shape.
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUsername",
                table: "CardsTransferHistory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "CheckedByUsername",
                table: "CardsTransferHistory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Unknown-way remainder resolution (Q2/Q3): nullable, per line - meaningful only
            // once an Unknown-way line has a remainder to resolve.
            migrationBuilder.AddColumn<byte>(
                name: "DifferenceAction",
                table: "CardTransferProducts",
                type: "tinyint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DifferenceAction",
                table: "CardTransferProducts");

            migrationBuilder.DropColumn(
                name: "CheckedByUsername",
                table: "CardsTransferHistory");

            migrationBuilder.DropColumn(
                name: "CreatedByUsername",
                table: "CardsTransferHistory");
        }
    }
}
