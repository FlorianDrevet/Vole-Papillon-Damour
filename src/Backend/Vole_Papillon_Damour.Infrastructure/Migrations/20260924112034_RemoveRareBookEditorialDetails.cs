using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRareBookEditorialDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Binding",
                table: "RareBooks");

            migrationBuilder.DropColumn(
                name: "Dimensions",
                table: "RareBooks");

            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "RareBooks");

            migrationBuilder.DropColumn(
                name: "PriceSetBy",
                table: "RareBooks");

            migrationBuilder.DropColumn(
                name: "Shelf",
                table: "RareBooks");

            migrationBuilder.DropColumn(
                name: "ShelfLocation",
                table: "RareBooks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Binding",
                table: "RareBooks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dimensions",
                table: "RareBooks",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "RareBooks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceSetBy",
                table: "RareBooks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shelf",
                table: "RareBooks",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShelfLocation",
                table: "RareBooks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }
    }
}
