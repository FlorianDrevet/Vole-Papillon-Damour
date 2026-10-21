using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookBibliographicDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BibliographicDetailsRefreshedAt",
                table: "Books",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Books",
                type: "nvarchar(max)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_MetadataStatus_BibliographicDetailsRefreshedAt",
                table: "Books",
                columns: new[] { "MetadataStatus", "BibliographicDetailsRefreshedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_MetadataStatus_BibliographicDetailsRefreshedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "BibliographicDetailsRefreshedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Books");
        }
    }
}
