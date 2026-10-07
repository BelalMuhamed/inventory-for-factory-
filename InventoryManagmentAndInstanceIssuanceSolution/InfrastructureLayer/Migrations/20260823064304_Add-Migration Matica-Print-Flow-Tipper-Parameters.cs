using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationMaticaPrintFlowTipperParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipperConsumption",
                table: "MaticaPrinterConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipperPressure",
                table: "MaticaPrinterConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipperTemperature",
                table: "MaticaPrinterConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipperTime",
                table: "MaticaPrinterConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipperConsumption",
                table: "MaticaPrinterConfigurations");

            migrationBuilder.DropColumn(
                name: "TipperPressure",
                table: "MaticaPrinterConfigurations");

            migrationBuilder.DropColumn(
                name: "TipperTemperature",
                table: "MaticaPrinterConfigurations");

            migrationBuilder.DropColumn(
                name: "TipperTime",
                table: "MaticaPrinterConfigurations");
        }
    }
}
