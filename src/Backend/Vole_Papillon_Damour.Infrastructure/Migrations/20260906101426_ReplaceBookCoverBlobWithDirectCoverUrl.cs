using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBookCoverBlobWithDirectCoverUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CoverBlobRef",
                table: "Books",
                newName: "CoverUrl");

            migrationBuilder.AlterColumn<string>(
                name: "CoverUrl",
                table: "Books",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverCheckedAt",
                table: "Books",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "CoverSource",
                table: "Books",
                type: "tinyint",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Books]
                SET [CoverUrl] = NULL,
                    [ManuallyEditedFields] = CASE
                        WHEN [ManuallyEditedFields] = '["CoverBlobRef"]' THEN '[]'
                        ELSE REPLACE(
                            REPLACE([ManuallyEditedFields], '"CoverBlobRef",', ''),
                            ',"CoverBlobRef"',
                            '')
                    END
                WHERE [CoverUrl] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverCheckedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CoverSource",
                table: "Books");

            migrationBuilder.RenameColumn(
                name: "CoverUrl",
                table: "Books",
                newName: "CoverBlobRef");

            migrationBuilder.AlterColumn<string>(
                name: "CoverBlobRef",
                table: "Books",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048);
        }
    }
}
