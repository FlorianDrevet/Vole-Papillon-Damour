using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FairDigestConsentedAt",
                table: "MemberRecommendationPreferences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumHearts",
                table: "MemberRecommendationPreferences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavoriteReference",
                table: "Books",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AnonymousFavoriteCounts",
                columns: table => new
                {
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonymousFavoriteCounts", x => x.Isbn13);
                });

            migrationBuilder.CreateTable(
                name: "FairDigestCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssoEventsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SkippedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FairDigestCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FairDigestCampaigns_AssoEvents_AssoEventsId",
                        column: x => x.AssoEventsId,
                        principalTable: "AssoEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FairDigestCampaigns_Users_SuspendedBy",
                        column: x => x.SuspendedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MemberFavoriteBooks",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberFavoriteBooks", x => new { x.UserId, x.Isbn13 });
                    table.ForeignKey(
                        name: "FK_MemberFavoriteBooks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FairDigestDeliveries",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FairDigestDeliveries", x => new { x.CampaignId, x.UserId });
                    table.ForeignKey(
                        name: "FK_FairDigestDeliveries_FairDigestCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "FairDigestCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FairDigestDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsFavoriteReference",
                table: "Books",
                column: "IsFavoriteReference",
                filter: "[IsFavoriteReference] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FairDigestCampaigns_SuspendedBy",
                table: "FairDigestCampaigns",
                column: "SuspendedBy");

            migrationBuilder.CreateIndex(
                name: "UX_FairDigestCampaigns_AssoEventsId",
                table: "FairDigestCampaigns",
                column: "AssoEventsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FairDigestDeliveries_UserId",
                table: "FairDigestDeliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberFavoriteBooks_Isbn13",
                table: "MemberFavoriteBooks",
                column: "Isbn13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnonymousFavoriteCounts");

            migrationBuilder.DropTable(
                name: "FairDigestDeliveries");

            migrationBuilder.DropTable(
                name: "MemberFavoriteBooks");

            migrationBuilder.DropTable(
                name: "FairDigestCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_Books_IsFavoriteReference",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "FairDigestConsentedAt",
                table: "MemberRecommendationPreferences");

            migrationBuilder.DropColumn(
                name: "MinimumHearts",
                table: "MemberRecommendationPreferences");

            migrationBuilder.DropColumn(
                name: "IsFavoriteReference",
                table: "Books");
        }
    }
}
