using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookNeighbors",
                columns: table => new
                {
                    GenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    Rank = table.Column<byte>(type: "tinyint", nullable: false),
                    NeighborIsbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    Score = table.Column<float>(type: "real", nullable: false),
                    Reason = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookNeighbors", x => new { x.GenerationId, x.Isbn13, x.Rank });
                });

            migrationBuilder.CreateTable(
                name: "BookSimilarityProfiles",
                columns: table => new
                {
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    NoticeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoticeFound = table.Column<bool>(type: "bit", nullable: false),
                    NoticeFetchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProfileText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProfileTextHash = table.Column<string>(type: "char(64)", unicode: false, nullable: true),
                    Embedding = table.Column<byte[]>(type: "varbinary(2048)", nullable: true),
                    EmbeddedTextHash = table.Column<string>(type: "char(64)", unicode: false, nullable: true),
                    EmbeddedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookSimilarityProfiles", x => x.Isbn13);
                });

            migrationBuilder.CreateTable(
                name: "MemberRecommendationPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRecommendationPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MemberRecommendationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationGenerations",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    CurrentGenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BookCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationGenerations", x => x.Id);
                    table.CheckConstraint("CK_RecommendationGenerations_Singleton", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                table: "RecommendationGenerations",
                columns: new[] { "Id", "BookCount", "ComputedAt", "CurrentGenerationId" },
                values: new object[] { (byte)1, 0, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_BookSimilarityProfiles_NoticeFetchedAt",
                table: "BookSimilarityProfiles",
                column: "NoticeFetchedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookNeighbors");

            migrationBuilder.DropTable(
                name: "BookSimilarityProfiles");

            migrationBuilder.DropTable(
                name: "MemberRecommendationPreferences");

            migrationBuilder.DropTable(
                name: "RecommendationGenerations");
        }
    }
}
