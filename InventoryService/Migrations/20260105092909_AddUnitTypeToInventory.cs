using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryService.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitTypeToInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitType",
                table: "StockTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UnitType",
                table: "InventoryItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitType",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "UnitType",
                table: "InventoryItems");
        }
    }
}
