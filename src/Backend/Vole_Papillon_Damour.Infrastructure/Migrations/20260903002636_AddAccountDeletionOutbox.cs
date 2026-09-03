using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ClaimedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScanSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ScanSessionId",
                table: "OutboxMessages",
                column: "ScanSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_DueAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
