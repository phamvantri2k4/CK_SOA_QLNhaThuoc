using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugService.Migrations
{
    /// <inheritdoc />
    public partial class AddPackSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackSize",
                table: "Drugs",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackSize",
                table: "Drugs");
        }
    }
}
