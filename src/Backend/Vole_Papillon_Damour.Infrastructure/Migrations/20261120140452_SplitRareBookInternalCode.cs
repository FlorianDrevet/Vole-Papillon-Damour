using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitRareBookInternalCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalCode",
                table: "RareBooks",
                type: "char(13)",
                unicode: false,
                nullable: true);

            // Les rares dont Isbn13 porte un code interne (préfixe 2) déplacent ce code dans InternalCode.
            // Aucun ISBN ne commence par 2 (RG-170) : aucune valeur réelle n'est touchée. Les contraintes et
            // l'index créés ensuite ne peuvent donc pas échouer sur les données existantes.
            migrationBuilder.Sql(
                """
                UPDATE [RareBooks]
                SET [InternalCode] = [Isbn13], [Isbn13] = NULL
                WHERE [Isbn13] LIKE '2%';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_InternalCode",
                table: "RareBooks",
                column: "InternalCode",
                unique: true,
                filter: "[InternalCode] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RareBooks_InternalCode_IsInternal",
                table: "RareBooks",
                sql: "[InternalCode] IS NULL OR [InternalCode] LIKE '2%'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RareBooks_Isbn13_IsNotInternal",
                table: "RareBooks",
                sql: "[Isbn13] IS NULL OR [Isbn13] NOT LIKE '2%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RareBooks_InternalCode",
                table: "RareBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RareBooks_InternalCode_IsInternal",
                table: "RareBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RareBooks_Isbn13_IsNotInternal",
                table: "RareBooks");

            // Retour à l'ancien modèle (un seul code par rare). Un rare qui a reçu un ISBN depuis perd son code
            // interne au retour arrière : il ne tient qu'une seule valeur dans Isbn13.
            migrationBuilder.Sql(
                """
                UPDATE [RareBooks]
                SET [Isbn13] = [InternalCode]
                WHERE [Isbn13] IS NULL AND [InternalCode] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "InternalCode",
                table: "RareBooks");
        }
    }
}
