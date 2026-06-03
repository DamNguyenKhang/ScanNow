using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanNow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemStatusAndExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderItems_KitchenStatus",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_OrderId_KitchenStatus",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "KitchenNote",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "KitchenStatus",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SpecialRequest",
                table: "OrderItems");

            migrationBuilder.RenameColumn(
                name: "PreparedAt",
                table: "OrderItems",
                newName: "ReadyAt");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PendingConfirmation",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "PENDING");

            migrationBuilder.AddColumn<DateTime>(
                name: "ServedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CookingStartedAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedCookingMinutes",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "OrderItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OrderItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_Status",
                table: "OrderItems",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_Status",
                table: "OrderItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderItems_OrderId_Status",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_Status",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ServedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "CookingStartedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "EstimatedCookingMinutes",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderItems");

            migrationBuilder.RenameColumn(
                name: "ReadyAt",
                table: "OrderItems",
                newName: "PreparedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "PendingConfirmation");

            migrationBuilder.AddColumn<string>(
                name: "KitchenNote",
                table: "OrderItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KitchenStatus",
                table: "OrderItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.AddColumn<string>(
                name: "SpecialRequest",
                table: "OrderItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_KitchenStatus",
                table: "OrderItems",
                column: "KitchenStatus");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_KitchenStatus",
                table: "OrderItems",
                columns: new[] { "OrderId", "KitchenStatus" });
        }
    }
}
