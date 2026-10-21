using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCatalogSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Publisher",
                table: "Books",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_PublicCatalog_Search",
                table: "Books",
                column: "Title",
                filter: "[IsHiddenFromCatalog] = 0 AND [RedirectedToIsbn13] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Authors", "Publisher", "MetadataStatus", "QuantityAvailable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_PublicCatalog_Search",
                table: "Books");

            migrationBuilder.Sql(
                """
                DECLARE @databaseCollation sysname =
                    CONVERT(sysname, DATABASEPROPERTYEX(DB_NAME(), N'Collation'));
                DECLARE @sql nvarchar(max) =
                    N'ALTER TABLE [Books] ALTER COLUMN [Publisher] nvarchar(200) COLLATE ' +
                    @databaseCollation +
                    N' NULL;';
                EXEC sys.sp_executesql @sql;
                """);
        }
    }
}
