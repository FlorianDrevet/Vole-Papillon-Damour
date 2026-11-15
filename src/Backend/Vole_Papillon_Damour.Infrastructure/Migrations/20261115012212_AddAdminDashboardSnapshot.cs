using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminDashboardSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminDashboardSnapshots",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    DeadStockCount = table.Column<int>(type: "int", nullable: false),
                    DeadStockMinAgeDays = table.Column<int>(type: "int", nullable: false),
                    DeadStockMinQuantity = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminDashboardSnapshots", x => x.Id);
                    table.CheckConstraint("CK_AdminDashboardSnapshots_Singleton", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminDashboardSnapshots");
        }
    }
}
