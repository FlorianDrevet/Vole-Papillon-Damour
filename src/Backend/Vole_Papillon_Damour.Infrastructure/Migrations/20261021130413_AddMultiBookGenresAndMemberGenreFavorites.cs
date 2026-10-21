using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiBookGenresAndMemberGenreFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GenresRefreshedAt",
                table: "Books",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookGenres",
                columns: table => new
                {
                    BookIsbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookGenres", x => new { x.BookIsbn13, x.Name });
                    table.ForeignKey(
                        name: "FK_BookGenres_Books_BookIsbn13",
                        column: x => x.BookIsbn13,
                        principalTable: "Books",
                        principalColumn: "Isbn13",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberGenreFavorites",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Genre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberGenreFavorites", x => new { x.UserId, x.Genre });
                    table.ForeignKey(
                        name: "FK_MemberGenreFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO [BookGenres] ([BookIsbn13], [Name])
                SELECT [Isbn13], LTRIM(RTRIM([Genre]))
                FROM [Books]
                WHERE [Genre] IS NOT NULL AND LTRIM(RTRIM([Genre])) <> '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Books_MetadataStatus_GenresRefreshedAt",
                table: "Books",
                columns: new[] { "MetadataStatus", "GenresRefreshedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookGenres_Name_BookIsbn13",
                table: "BookGenres",
                columns: new[] { "Name", "BookIsbn13" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookGenres");

            migrationBuilder.DropTable(
                name: "MemberGenreFavorites");

            migrationBuilder.DropIndex(
                name: "IX_Books_MetadataStatus_GenresRefreshedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "GenresRefreshedAt",
                table: "Books");
        }
    }
}
