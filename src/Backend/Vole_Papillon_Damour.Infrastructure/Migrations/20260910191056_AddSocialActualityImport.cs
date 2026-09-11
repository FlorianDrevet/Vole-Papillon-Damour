using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialActualityImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImportedAt",
                table: "Actualities",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Status",
                table: "Actualities",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<bool>(
                name: "TitleNeedsReview",
                table: "Actualities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SocialPostImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    ExternalId = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    Permalink = table.Column<string>(type: "varchar(2048)", unicode: false, maxLength: 2048, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPostImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialPostImports_Actualities_ActualityId",
                        column: x => x.ActualityId,
                        principalTable: "Actualities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actualities_Status_Date",
                table: "Actualities",
                columns: new[] { "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostImports_ActualityId",
                table: "SocialPostImports",
                column: "ActualityId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPostImports_Source_ExternalId",
                table: "SocialPostImports",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialPostImports");

            migrationBuilder.DropIndex(
                name: "IX_Actualities_Status_Date",
                table: "Actualities");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "Actualities");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Actualities");

            migrationBuilder.DropColumn(
                name: "TitleNeedsReview",
                table: "Actualities");
        }
    }
}
