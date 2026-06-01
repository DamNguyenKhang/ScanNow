using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanNow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchPaymentConfigAndPaperVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchPaymentConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PayOsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PayOsClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PayOsApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayOsChecksumKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayOsReturnUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PayOsCancelUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "CASH"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchPaymentConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchPaymentConfigs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaperVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MinOrderAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    MaxDiscountAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UsedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperVouchers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchPaymentConfigs_BranchId",
                table: "BranchPaymentConfigs",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperVouchers_BranchId",
                table: "PaperVouchers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperVouchers_BranchId_Code",
                table: "PaperVouchers",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperVouchers_IsActive",
                table: "PaperVouchers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PaperVouchers_ValidUntil",
                table: "PaperVouchers",
                column: "ValidUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchPaymentConfigs");

            migrationBuilder.DropTable(
                name: "PaperVouchers");
        }
    }
}
