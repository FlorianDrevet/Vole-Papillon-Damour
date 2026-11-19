using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ScanSessions_StartedAt",
                table: "ScanSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanSessions_Status",
                table: "ScanSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Kind_DueAt",
                table: "OutboxMessages",
                columns: new[] { "Kind", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_MemberId",
                table: "OutboxMessages",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_BookNotFoundReports_Status_ClosedAt",
                table: "BookNotFoundReports",
                columns: new[] { "Status", "ClosedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScanSessions_StartedAt",
                table: "ScanSessions");

            migrationBuilder.DropIndex(
                name: "IX_ScanSessions_Status",
                table: "ScanSessions");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Kind_DueAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_MemberId",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_BookNotFoundReports_Status_ClosedAt",
                table: "BookNotFoundReports");
        }
    }
}
