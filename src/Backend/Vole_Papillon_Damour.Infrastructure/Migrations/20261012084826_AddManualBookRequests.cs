using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBookRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualRequesters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    ConsentRecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsentRecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErasedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualRequesters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManualBookRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<byte>(type: "tinyint", nullable: false),
                    WorkId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Authors = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicationYear = table.Column<int>(type: "int", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    FoundOrigin = table.Column<byte>(type: "tinyint", nullable: true),
                    FoundIsbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    FoundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FoundBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FoundScanSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PickedFromFairId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HoldMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleaseMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualBookRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualBookRequests_ManualRequesters_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "ManualRequesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBookRequests_HoldMovementId",
                table: "ManualBookRequests",
                column: "HoldMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualBookRequests_RequesterId",
                table: "ManualBookRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualBookRequests_Status_Isbn13",
                table: "ManualBookRequests",
                columns: new[] { "Status", "Isbn13" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBookRequests_Status_WorkId",
                table: "ManualBookRequests",
                columns: new[] { "Status", "WorkId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualBookRequests");

            migrationBuilder.DropTable(
                name: "ManualRequesters");
        }
    }
}
