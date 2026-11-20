using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Parité modèle / base. Les contraintes Books.RedirectedToIsbn13 (FK_Books_Books_RedirectedToIsbn13, Restrict),
    // BookIsbndbRecords.Isbn13 (FK_BookIsbndbRecords_Books_Isbn13, Cascade) et l'index IX_Books_RedirectedToIsbn13
    // existent déjà en base depuis leurs migrations d'origine. Le modèle les déclare désormais avec les mêmes noms
    // et les mêmes actions : seul le snapshot change. Recréer ces objets ferait échouer la migration ; la base
    // n'est donc pas modifiée, ni à l'application ni au retour arrière.
    public partial class DeclareBookForeignKeysInModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
