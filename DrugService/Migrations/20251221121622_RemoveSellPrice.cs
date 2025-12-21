using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSellPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellPrice",
                table: "Drugs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SellPrice",
                table: "Drugs",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
