using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanNow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBranchPaymentRedirectUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayOsCancelUrl",
                table: "BranchPaymentConfigs");

            migrationBuilder.DropColumn(
                name: "PayOsReturnUrl",
                table: "BranchPaymentConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayOsCancelUrl",
                table: "BranchPaymentConfigs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayOsReturnUrl",
                table: "BranchPaymentConfigs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
