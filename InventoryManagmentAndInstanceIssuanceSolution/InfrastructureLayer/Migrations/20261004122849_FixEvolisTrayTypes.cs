using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class FixEvolisTrayTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Translate any pre-existing rows from the old shared EvolisTray byte scheme
            // (FEEDER=0, MANUAL=1, STANDARD=2, ERROR=3, REAR=4) to the new per-column
            // InputTray/OutputTray values *before* widening the column type, since the two
            // schemes share no numeric values and an untranslated row would silently change
            // meaning (e.g. old OutputTray=2 meant STANDARD; new OutputTray=2 means
            // STANDARDSTANDBY). Any old value with no sensible mapping for a given column
            // falls back to that column's new default.
            migrationBuilder.Sql(@"
                UPDATE [EvolisPrinterConfigurations] SET [InputTray] = CASE [InputTray]
                    WHEN 0 THEN 1   -- FEEDER -> InputTray.FEEDER
                    WHEN 1 THEN 2   -- MANUAL -> InputTray.MANUAL
                    WHEN 4 THEN 64  -- REAR -> InputTray.REAR
                    ELSE 1          -- STANDARD/ERROR never made sense for InputTray -> FEEDER
                END;
                UPDATE [EvolisPrinterConfigurations] SET [OutputTray] = CASE [OutputTray]
                    WHEN 2 THEN 1   -- STANDARD -> OutputTray.STANDARD
                    WHEN 1 THEN 4   -- MANUAL -> OutputTray.MANUAL
                    WHEN 3 THEN 8   -- ERROR -> OutputTray.ERROR
                    ELSE 1          -- FEEDER/REAR never made sense for OutputTray -> STANDARD
                END;
                UPDATE [EvolisPrinterConfigurations] SET [ErrorTray] = CASE [ErrorTray]
                    WHEN 3 THEN 8   -- ERROR -> OutputTray.ERROR
                    ELSE 8          -- anything else -> ERROR, the only sensible error tray
                END;
            ");

            migrationBuilder.AlterColumn<short>(
                name: "OutputTray",
                table: "EvolisPrinterConfigurations",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldDefaultValue: (byte)2);

            migrationBuilder.AlterColumn<short>(
                name: "InputTray",
                table: "EvolisPrinterConfigurations",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldDefaultValue: (byte)0);

            migrationBuilder.AlterColumn<short>(
                name: "ErrorTray",
                table: "EvolisPrinterConfigurations",
                type: "smallint",
                nullable: false,
                defaultValue: (short)8,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldDefaultValue: (byte)3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the Up() translation *before* narrowing the column type below — the same
            // translate-then-resize order Up() uses, just in reverse. Narrowing first would try
            // to cast a value like OutputTray.LOCKED (256) straight into a tinyint (max 255)
            // before this translation ever gets a chance to bring it back into range, which SQL
            // Server rejects outright with an arithmetic-overflow error.
            migrationBuilder.Sql(@"
                UPDATE [EvolisPrinterConfigurations] SET [InputTray] = CASE [InputTray]
                    WHEN 1 THEN 0   -- InputTray.FEEDER -> FEEDER
                    WHEN 2 THEN 1   -- InputTray.MANUAL -> MANUAL
                    WHEN 64 THEN 4  -- InputTray.REAR -> REAR
                    ELSE 0          -- MANUALANDCMD/BEZEL/BOTH/NOFEEDER have no old counterpart -> FEEDER
                END;
                UPDATE [EvolisPrinterConfigurations] SET [OutputTray] = CASE [OutputTray]
                    WHEN 1 THEN 2   -- OutputTray.STANDARD -> STANDARD
                    WHEN 4 THEN 1   -- OutputTray.MANUAL -> MANUAL
                    WHEN 8 THEN 3   -- OutputTray.ERROR -> ERROR
                    ELSE 2          -- STANDARDSTANDBY/ERRORSTANDBY/EJECT/BEZEL/ERRORSLOT/LOCKED -> STANDARD
                END;
                UPDATE [EvolisPrinterConfigurations] SET [ErrorTray] = CASE [ErrorTray]
                    WHEN 8 THEN 3   -- OutputTray.ERROR -> ERROR
                    ELSE 3          -- anything else -> ERROR
                END;
            ");

            migrationBuilder.AlterColumn<byte>(
                name: "OutputTray",
                table: "EvolisPrinterConfigurations",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)2,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)1);

            migrationBuilder.AlterColumn<byte>(
                name: "InputTray",
                table: "EvolisPrinterConfigurations",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)1);

            migrationBuilder.AlterColumn<byte>(
                name: "ErrorTray",
                table: "EvolisPrinterConfigurations",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)3,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)8);
        }
    }
}
