using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaleService.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitTypeToSaleInvoiceDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitType",
                table: "SaleInvoiceDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "pill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitType",
                table: "SaleInvoiceDetails");
        }
    }
}
