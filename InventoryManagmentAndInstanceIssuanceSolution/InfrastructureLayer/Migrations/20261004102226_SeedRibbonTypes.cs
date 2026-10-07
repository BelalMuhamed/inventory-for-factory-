using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class SeedRibbonTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RibbonTypes",
                columns: new[] { "Id", "Name", "SdkCode" },
                values: new object[,]
                {
                    { 1L, "YMCKO Color", "YMCKO" },
                    { 2L, "YMCK Color", "YMCK" },
                    { 3L, "YMCKOK Color", "YMCKOK" },
                    { 4L, "Monochrome Black", "KBLACK" },
                    { 5L, "Monochrome White", "KWHITE" },
                    { 6L, "Monochrome Red", "KRED" },
                    { 7L, "Monochrome Blue", "KBLUE" },
                    { 8L, "Monochrome Green", "KGREEN" },
                    { 9L, "Holographic Overlay", "HOLO" },
                    { 10L, "Clear Overlay", "CLEAR" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "RibbonTypes",
                keyColumn: "Id",
                keyValue: 10L);
        }
    }
}
