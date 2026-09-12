using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistItemReferenceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Authors",
                table: "WatchlistItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.AddColumn<int>(
                name: "PublicationYear",
                table: "WatchlistItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Publisher",
                table: "WatchlistItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "WatchlistItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Latin1_General_100_CI_AI");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Authors",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "PublicationYear",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "Publisher",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "WatchlistItems");
        }
    }
}
