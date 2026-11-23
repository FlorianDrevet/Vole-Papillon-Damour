using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsbndbUnmatchedSubjectsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IsbndbUnmatchedSubjectsComputedAt",
                table: "AdminDashboardSnapshots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IsbndbUnmatchedSubjectsJson",
                table: "AdminDashboardSnapshots",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsbndbUnmatchedSubjectsComputedAt",
                table: "AdminDashboardSnapshots");

            migrationBuilder.DropColumn(
                name: "IsbndbUnmatchedSubjectsJson",
                table: "AdminDashboardSnapshots");
        }
    }
}
