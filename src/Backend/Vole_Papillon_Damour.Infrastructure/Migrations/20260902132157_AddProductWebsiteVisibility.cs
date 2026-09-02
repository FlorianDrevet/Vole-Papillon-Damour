using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductWebsiteVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VisibleOnWebsite",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Preserve the Website's previous behavior for existing products while
            // keeping legacy cash-currency products out of the public payload.
            migrationBuilder.Sql("""
                UPDATE [Products]
                SET [VisibleOnWebsite] = CASE
                    WHEN [Available] = 1
                        AND LOWER([Name]) NOT LIKE '%euro%'
                        AND LOWER([Name]) NOT LIKE '%centime%'
                        AND [Name] NOT LIKE '%€%'
                    THEN 1
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisibleOnWebsite",
                table: "Products");
        }
    }
}
