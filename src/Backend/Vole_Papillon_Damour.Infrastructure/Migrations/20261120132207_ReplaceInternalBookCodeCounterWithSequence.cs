using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceInternalBookCodeCounterWithSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La séquence doit démarrer après le dernier numéro déjà émis par l'ancien compteur (RG-171) : le
            // démarrage dépend des données, il est donc calculé ici et non dans le modèle. Elle est créée avant
            // la suppression de la table dont elle lit la valeur. MAXVALUE reprend la limite de BookCode (11 chiffres).
            migrationBuilder.Sql(
                """
                DECLARE @start bigint = (SELECT ISNULL(MAX([LastIssuedNumber]), 0) + 1 FROM [InternalBookCodeSequences]);
                DECLARE @sql nvarchar(max) = N'CREATE SEQUENCE [dbo].[InternalBookCodeNumbers] AS bigint START WITH '
                    + CAST(@start AS nvarchar(20))
                    + N' INCREMENT BY 1 MINVALUE 1 MAXVALUE 99999999999 NO CYCLE;';
                EXEC (@sql);
                """);

            migrationBuilder.DropTable(
                name: "InternalBookCodeSequences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalBookCodeSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastIssuedNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalBookCodeSequences", x => x.Id);
                });

            // Le compteur reprend la valeur courante de la séquence : aucun numéro n'est réémis. Les numéros
            // non utilisés sont sautés, ce qui est accepté (RG-171).
            migrationBuilder.Sql(
                """
                INSERT INTO [InternalBookCodeSequences] ([Id], [LastIssuedNumber])
                SELECT 1, ISNULL(CAST([current_value] AS bigint), 0)
                FROM [sys].[sequences]
                WHERE [name] = N'InternalBookCodeNumbers' AND SCHEMA_NAME([schema_id]) = N'dbo';
                """);

            migrationBuilder.DropSequence(
                name: "InternalBookCodeNumbers");
        }
    }
}
