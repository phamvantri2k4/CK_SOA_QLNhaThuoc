using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugService.Migrations
{
    /// <inheritdoc />
    public partial class AddSellPricePerPill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SellPricePerPill",
                table: "Drugs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellPricePerPill",
                table: "Drugs");
        }
    }
}
