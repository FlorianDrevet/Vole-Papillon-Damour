using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRareBookClientGestureId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientGestureId",
                table: "RareBooks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_ClientGestureId",
                table: "RareBooks",
                column: "ClientGestureId",
                unique: true,
                filter: "[ClientGestureId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RareBooks_ClientGestureId",
                table: "RareBooks");

            migrationBuilder.DropColumn(
                name: "ClientGestureId",
                table: "RareBooks");
        }
    }
}
