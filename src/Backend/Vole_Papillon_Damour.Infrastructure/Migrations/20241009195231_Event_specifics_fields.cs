using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Event_specifics_fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Date",
                table: "AssoEvents",
                newName: "DateStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateEnd",
                table: "AssoEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HourOpenDoors",
                table: "AssoEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlImageMap",
                table: "AssoEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlRegistration",
                table: "AssoEvents",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateEnd",
                table: "AssoEvents");

            migrationBuilder.DropColumn(
                name: "HourOpenDoors",
                table: "AssoEvents");

            migrationBuilder.DropColumn(
                name: "UrlImageMap",
                table: "AssoEvents");

            migrationBuilder.DropColumn(
                name: "UrlRegistration",
                table: "AssoEvents");

            migrationBuilder.RenameColumn(
                name: "DateStart",
                table: "AssoEvents",
                newName: "Date");
        }
    }
}
