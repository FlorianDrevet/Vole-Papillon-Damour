using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCatalogAndAdminReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_IsbndbSourceFlags",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_FirstSeenAt",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_UpdatedAt",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookMovements_ScanSessionId",
                table: "BookMovements");

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsbndbSourceFlags",
                table: "Books",
                columns: new[] { "IsIsbndbSourced", "IsAbsentFromIsbndb" },
                filter: "[IsIsbndbSourced] = 0")
                .Annotation("SqlServer:Include", new[] { "IsbndbCompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_FirstSeenAt",
                table: "Books",
                columns: new[] { "FirstSeenAt", "Title" },
                descending: new[] { true, false },
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL")
                .Annotation("SqlServer:Include", new[] { "QuantityAvailable", "MetadataStatus", "IsFavoriteReference" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_UpdatedAt",
                table: "Books",
                columns: new[] { "UpdatedAt", "Title" },
                descending: new[] { true, false },
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL")
                .Annotation("SqlServer:Include", new[] { "QuantityAvailable", "MetadataStatus", "IsFavoriteReference" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_ScanSessionId",
                table: "BookMovements",
                column: "ScanSessionId")
                .Annotation("SqlServer:Include", new[] { "Isbn13" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_IsbndbSourceFlags",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_FirstSeenAt",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_UpdatedAt",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookMovements_ScanSessionId",
                table: "BookMovements");

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsbndbSourceFlags",
                table: "Books",
                columns: new[] { "IsIsbndbSourced", "IsAbsentFromIsbndb" },
                filter: "[IsIsbndbSourced] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_FirstSeenAt",
                table: "Books",
                columns: new[] { "FirstSeenAt", "Title" },
                descending: new[] { true, false },
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_UpdatedAt",
                table: "Books",
                columns: new[] { "UpdatedAt", "Title" },
                descending: new[] { true, false },
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_ScanSessionId",
                table: "BookMovements",
                column: "ScanSessionId");
        }
    }
}
