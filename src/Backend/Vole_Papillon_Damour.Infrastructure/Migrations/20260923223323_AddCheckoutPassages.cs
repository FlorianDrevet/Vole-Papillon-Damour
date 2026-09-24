using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutPassages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutPassageId",
                table: "BookMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckoutPassages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssociatedByVolunteerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssociatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnresolvedReason = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    DissociatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DissociatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutPassages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckoutPassages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutPassageLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckoutPassageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RareBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    RequestedIsbn13 = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Authors = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicationYear = table.Column<int>(type: "int", nullable: true),
                    PhysicalFormat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssoEventsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutPassageLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckoutPassageLines_CheckoutPassages_CheckoutPassageId",
                        column: x => x.CheckoutPassageId,
                        principalTable: "CheckoutPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_CheckoutPassageId",
                table: "BookMovements",
                column: "CheckoutPassageId",
                filter: "[CheckoutPassageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutPassageLines_CheckoutPassageId_RareBookId",
                table: "CheckoutPassageLines",
                columns: new[] { "CheckoutPassageId", "RareBookId" },
                unique: true,
                filter: "[RareBookId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutPassageLines_SaleMovementId",
                table: "CheckoutPassageLines",
                column: "SaleMovementId",
                unique: true,
                filter: "[SaleMovementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutPassages_UserId_OccurredAt",
                table: "CheckoutPassages",
                columns: new[] { "UserId", "OccurredAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutPassageLines");

            migrationBuilder.DropTable(
                name: "CheckoutPassages");

            migrationBuilder.DropIndex(
                name: "IX_BookMovements_CheckoutPassageId",
                table: "BookMovements");

            migrationBuilder.DropColumn(
                name: "CheckoutPassageId",
                table: "BookMovements");
        }
    }
}
