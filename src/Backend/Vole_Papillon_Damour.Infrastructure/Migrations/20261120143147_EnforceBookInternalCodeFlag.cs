using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceBookInternalCodeFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La clé fait foi (RG-170) : un préfixe 2 est un code interne, tout autre livre a un ISBN. Le drapeau
            // existant est recalculé depuis la clé avant d'ajouter la contrainte. Seules les lignes incohérentes
            // sont touchées ; aucune ligne n'est supprimée.
            migrationBuilder.Sql(
                """
                UPDATE [Books]
                SET [IsInternalCode] = CASE WHEN [Isbn13] LIKE '2%' THEN 1 ELSE 0 END
                WHERE [IsInternalCode] <> CASE WHEN [Isbn13] LIKE '2%' THEN 1 ELSE 0 END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Books_IsInternalCode_MatchesCode",
                table: "Books",
                sql: "([IsInternalCode] = 1 AND [Isbn13] LIKE '2%') OR ([IsInternalCode] = 0 AND [Isbn13] NOT LIKE '2%')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Books_IsInternalCode_MatchesCode",
                table: "Books");
        }
    }
}
