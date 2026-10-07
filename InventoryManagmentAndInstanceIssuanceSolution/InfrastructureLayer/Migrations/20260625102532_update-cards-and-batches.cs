using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class updatecardsandbatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BatchCardAmount = table.Column<int>(type: "int", nullable: false),
                    fileMac = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatchStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessedRowCount = table.Column<int>(type: "int", nullable: false),
                    ProcessingError = table.Column<int>(type: "int", nullable: false),
                    UploadedByTenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Batches_Tenants_BankId",
                        column: x => x.BankId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Batches_Tenants_UploadedByTenantId",
                        column: x => x.UploadedByTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    EncryptedPan = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    CardHolderName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.EncryptedPan);
                    table.ForeignKey(
                        name: "FK_Cards_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cards_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cards_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_batch_name",
                table: "Batches",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_batch_status",
                table: "Batches",
                column: "BatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_BankId",
                table: "Batches",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_UploadedByTenantId",
                table: "Batches",
                column: "UploadedByTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_card_holder_name",
                table: "Cards",
                column: "CardHolderName");

            migrationBuilder.CreateIndex(
                name: "IX_card_status_name",
                table: "Cards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BatchId",
                table: "Cards",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_ProductId",
                table: "Cards",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TenantId",
                table: "Cards",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Batches");
        }
    }
}
