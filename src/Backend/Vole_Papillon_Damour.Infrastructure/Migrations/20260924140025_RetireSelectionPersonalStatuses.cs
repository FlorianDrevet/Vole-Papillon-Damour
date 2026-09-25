using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireSelectionPersonalStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO BookNotFoundReports (Id, UserId, Isbn13, RareBookId, Location, Comment, Status, ReportedAt)
                SELECT NEWID(), s.UserId, s.Isbn13, NULL, NULL, NULL, 0, s.StatusChangedAt
                FROM MemberSelectionItems s
                JOIN Books b ON b.Isbn13 = s.Isbn13
                WHERE s.Status = 2 AND s.Isbn13 IS NOT NULL
                  AND s.StatusChangedAt >= DATEADD(day, -30, SYSUTCDATETIME())
                  AND b.QuantityAvailable > 0 AND b.IsHiddenFromCatalog = 0 AND b.RedirectedToIsbn13 IS NULL;

                UPDATE MemberSelectionItems SET Status = 0 WHERE Status IN (2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Loss of information is intentional, F-11 §10.
        }
    }
}
