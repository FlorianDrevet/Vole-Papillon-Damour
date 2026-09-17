using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRareBookWatchlistAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WatchlistItems_ExactlyOneTarget",
                table: "WatchlistItems");

            migrationBuilder.AddColumn<Guid>(
                name: "RareBookId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Isbn13",
                table: "UserAlertHistory",
                type: "char(13)",
                unicode: false,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(13)",
                oldUnicode: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RareBookId",
                table: "UserAlertHistory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RareBookId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_RareBookId",
                table: "WatchlistItems",
                column: "RareBookId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_UserId_RareBookId",
                table: "WatchlistItems",
                columns: new[] { "UserId", "RareBookId" },
                unique: true,
                filter: "[RareBookId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WatchlistItems_ExactlyOneTarget",
                table: "WatchlistItems",
                sql: "(([Scope] = 0 AND [WorkId] IS NOT NULL AND [Isbn13] IS NULL AND [RareBookId] IS NULL) OR ([Scope] = 1 AND [WorkId] IS NULL AND [Isbn13] IS NOT NULL AND [RareBookId] IS NULL) OR ([Scope] = 2 AND [WorkId] IS NULL AND [Isbn13] IS NULL AND [RareBookId] IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_UserAlertHistory_UserId_RareBookId_SentAt",
                table: "UserAlertHistory",
                columns: new[] { "UserId", "RareBookId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_RareBookId",
                table: "OutboxMessages",
                column: "RareBookId");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchlistItems_RareBooks_RareBookId",
                table: "WatchlistItems",
                column: "RareBookId",
                principalTable: "RareBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchlistItems_RareBooks_RareBookId",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_RareBookId",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_UserId_RareBookId",
                table: "WatchlistItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WatchlistItems_ExactlyOneTarget",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_UserAlertHistory_UserId_RareBookId_SentAt",
                table: "UserAlertHistory");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_RareBookId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RareBookId",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "RareBookId",
                table: "UserAlertHistory");

            migrationBuilder.DropColumn(
                name: "RareBookId",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Isbn13",
                table: "UserAlertHistory",
                type: "char(13)",
                unicode: false,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "char(13)",
                oldUnicode: false,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WatchlistItems_ExactlyOneTarget",
                table: "WatchlistItems",
                sql: "(([Scope] = 0 AND [WorkId] IS NOT NULL AND [Isbn13] IS NULL) OR ([Scope] = 1 AND [WorkId] IS NULL AND [Isbn13] IS NOT NULL))");
        }
    }
}
