using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SupplierService.Migrations
{
    [Migration("20251218160600_AddExpiryDateToPurchaseOrderDetails")]
    public partial class AddExpiryDateToPurchaseOrderDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "PurchaseOrderDetails",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "PurchaseOrderDetails");
        }
    }
}
