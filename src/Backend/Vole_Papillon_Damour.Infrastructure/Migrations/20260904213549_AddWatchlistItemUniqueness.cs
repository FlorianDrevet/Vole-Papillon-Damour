using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistItemUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_UserId_Isbn13",
                table: "WatchlistItems",
                columns: new[] { "UserId", "Isbn13" },
                unique: true,
                filter: "[Isbn13] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_UserId_WorkId",
                table: "WatchlistItems",
                columns: new[] { "UserId", "WorkId" },
                unique: true,
                filter: "[WorkId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_UserId_Isbn13",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_UserId_WorkId",
                table: "WatchlistItems");
        }
    }
}
