using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookNotFoundReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookNotFoundReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    RareBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<byte>(type: "tinyint", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosureNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WithdrawalReason = table.Column<byte>(type: "tinyint", nullable: true),
                    WithdrawnQuantity = table.Column<int>(type: "int", nullable: true),
                    WithdrawalMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookNotFoundReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookNotFoundReports_Status_Isbn13",
                table: "BookNotFoundReports",
                columns: new[] { "Status", "Isbn13" });

            migrationBuilder.CreateIndex(
                name: "IX_BookNotFoundReports_Status_RareBookId",
                table: "BookNotFoundReports",
                columns: new[] { "Status", "RareBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookNotFoundReports_UserId_ReportedAt",
                table: "BookNotFoundReports",
                columns: new[] { "UserId", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_BookNotFoundReports_OpenPerMemberEdition",
                table: "BookNotFoundReports",
                columns: new[] { "UserId", "Isbn13" },
                unique: true,
                filter: "[Status] = 0 AND [Isbn13] IS NOT NULL AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_BookNotFoundReports_OpenPerMemberRareBook",
                table: "BookNotFoundReports",
                columns: new[] { "UserId", "RareBookId" },
                unique: true,
                filter: "[Status] = 0 AND [RareBookId] IS NOT NULL AND [UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookNotFoundReports");
        }
    }
}
