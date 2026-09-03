using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookWatchlistsAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAlertHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAlertHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAlertHistory_OutboxMessages_OutboxMessageId",
                        column: x => x.OutboxMessageId,
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserAlertHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watchlists",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    BounceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Watchlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<byte>(type: "tinyint", nullable: false),
                    WorkId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistItems", x => x.Id);
                    table.CheckConstraint("CK_WatchlistItems_ExactlyOneTarget", "(([Scope] = 0 AND [WorkId] IS NOT NULL AND [Isbn13] IS NULL) OR ([Scope] = 1 AND [WorkId] IS NULL AND [Isbn13] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_WatchlistItems_Watchlists_UserId",
                        column: x => x.UserId,
                        principalTable: "Watchlists",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAlertHistory_OutboxMessageId",
                table: "UserAlertHistory",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAlertHistory_UserId_Isbn13_SentAt",
                table: "UserAlertHistory",
                columns: new[] { "UserId", "Isbn13", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_Isbn13",
                table: "WatchlistItems",
                column: "Isbn13");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_UserId",
                table: "WatchlistItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_WorkId",
                table: "WatchlistItems",
                column: "WorkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAlertHistory");

            migrationBuilder.DropTable(
                name: "WatchlistItems");

            migrationBuilder.DropTable(
                name: "Watchlists");
        }
    }
}
