using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeBookExchangeQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookMovements_Isbn13_OccurredAt",
                table: "BookMovements");

            migrationBuilder.DropIndex(
                name: "IX_BookMovements_Isbn13_Type_OccurredAt",
                table: "BookMovements");

            migrationBuilder.DropIndex(
                name: "IX_BookAnnouncements_Isbn13",
                table: "BookAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_FirstSeenAt",
                table: "Books",
                columns: new[] { "FirstSeenAt", "Title" },
                descending: new[] { true, false },
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_Genre",
                table: "Books",
                column: "Genre",
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_UpdatedAt",
                table: "Books",
                columns: new[] { "UpdatedAt", "Title" },
                descending: new[] { true, false },
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Books_RowVersion",
                table: "Books",
                column: "RowVersion");

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_Isbn13_Type_OccurredAt",
                table: "BookMovements",
                columns: new[] { "Isbn13", "Type", "OccurredAt" })
                .Annotation("SqlServer:Include", new[] { "Quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_Type_OccurredAt",
                table: "BookMovements",
                columns: new[] { "Type", "OccurredAt" })
                .Annotation("SqlServer:Include", new[] { "Quantity", "Isbn13" });

            migrationBuilder.CreateIndex(
                name: "IX_BookAnnouncements_Isbn13_Status",
                table: "BookAnnouncements",
                columns: new[] { "Isbn13", "Status" })
                .Annotation("SqlServer:Include", new[] { "Quantity", "AssoEventsId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_FirstSeenAt",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_Genre",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_UpdatedAt",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_RowVersion",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookMovements_Isbn13_Type_OccurredAt",
                table: "BookMovements");

            migrationBuilder.DropIndex(
                name: "IX_BookMovements_Type_OccurredAt",
                table: "BookMovements");

            migrationBuilder.DropIndex(
                name: "IX_BookAnnouncements_Isbn13_Status",
                table: "BookAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_Isbn13_OccurredAt",
                table: "BookMovements",
                columns: new[] { "Isbn13", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_Isbn13_Type_OccurredAt",
                table: "BookMovements",
                columns: new[] { "Isbn13", "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookAnnouncements_Isbn13",
                table: "BookAnnouncements",
                column: "Isbn13");
        }
    }
}
