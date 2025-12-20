using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaleService.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "SaleInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "SaleInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "SaleInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "SaleInvoices");
        }
    }
}
