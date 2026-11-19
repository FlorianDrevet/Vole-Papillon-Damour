using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Généralisation de Book : la clé devient un BookCode (ISBN-13 ou code interne, RG-170).
    // Les valeurs stockées restent des chaînes de 13 chiffres : aucune donnée existante ne change.
    // Seule colonne ajoutée : IsInternalCode, à false pour tous les livres existants (tous ont un ISBN).
    // Les contraintes de clé étrangère Books.RedirectedToIsbn13 et BookIsbndbRecords.Isbn13 et leur index
    // sont volontairement conservés : elles ne sont plus déclarées dans le modèle EF (type de clé différent),
    // mais restent appliquées en base.
    public partial class BookCodeInternalFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInternalCode",
                table: "Books",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInternalCode",
                table: "Books");
        }
    }
}
