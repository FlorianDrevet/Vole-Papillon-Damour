using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberSelectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    RareBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberSelectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberSelectionItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberSelectionItems_Isbn13",
                table: "MemberSelectionItems",
                column: "Isbn13");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSelectionItems_UserId_Isbn13",
                table: "MemberSelectionItems",
                columns: new[] { "UserId", "Isbn13" },
                unique: true,
                filter: "[Isbn13] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSelectionItems_UserId_RareBookId",
                table: "MemberSelectionItems",
                columns: new[] { "UserId", "RareBookId" },
                unique: true,
                filter: "[RareBookId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberSelectionItems");
        }
    }
}
