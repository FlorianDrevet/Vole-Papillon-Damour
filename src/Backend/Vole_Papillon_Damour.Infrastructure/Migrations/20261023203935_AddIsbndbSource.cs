using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsbndbSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "Books",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAbsentFromIsbndb",
                table: "Books",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIsbndbSourced",
                table: "Books",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "IsbndbCheckedAt",
                table: "Books",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IsbndbCompletedAt",
                table: "Books",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IsbndbCompletedBy",
                table: "Books",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pages",
                table: "Books",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkKey",
                table: "Books",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookIsbndbRecords",
                columns: table => new
                {
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    TitleLong = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Isbn10 = table.Column<string>(type: "char(10)", unicode: false, maxLength: 10, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Binding = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DatePublished = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Edition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EditionNumber = table.Column<int>(type: "int", nullable: true),
                    Dimensions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeightValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    HeightUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WidthValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    WidthUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LengthValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LengthUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WeightValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeweyDecimal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Synopsis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Overview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Excerpt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subjects = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Authors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtherIsbns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Related = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Msrp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ListPriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ListPriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookIsbndbRecords", x => x.Isbn13);
                    table.ForeignKey(
                        name: "FK_BookIsbndbRecords_Books_Isbn13",
                        column: x => x.Isbn13,
                        principalTable: "Books",
                        principalColumn: "Isbn13",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IsbndbCatchUpRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Filled = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Absent = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Rechecked = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Remaining = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StopReason = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbndbCatchUpRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsbndbDailyUsages",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Usage = table.Column<byte>(type: "tinyint", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbndbDailyUsages", x => new { x.Day, x.Usage });
                });

            migrationBuilder.CreateTable(
                name: "IsbndbQuotaStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DailyLimit = table.Column<int>(type: "int", nullable: false),
                    DailyRemaining = table.Column<int>(type: "int", nullable: false),
                    ResetsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExhaustedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsbndbQuotaStates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "IsbndbQuotaStates",
                columns: new[] { "Id", "DailyLimit", "DailyRemaining", "ExhaustedUntil", "LastFailureAt", "LastFailureReason", "ObservedAt", "ResetsAt" },
                values: new object[] { 1, 5000, 5000, null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsbndbSourceFlags",
                table: "Books",
                columns: new[] { "IsIsbndbSourced", "IsAbsentFromIsbndb" },
                filter: "[IsIsbndbSourced] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Books_WorkKey",
                table: "Books",
                column: "WorkKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookIsbndbRecords");

            migrationBuilder.DropTable(
                name: "IsbndbCatchUpRuns");

            migrationBuilder.DropTable(
                name: "IsbndbDailyUsages");

            migrationBuilder.DropTable(
                name: "IsbndbQuotaStates");

            migrationBuilder.DropIndex(
                name: "IX_Books_IsbndbSourceFlags",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_WorkKey",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Edition",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsAbsentFromIsbndb",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsIsbndbSourced",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsbndbCheckedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsbndbCompletedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsbndbCompletedBy",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Pages",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "WorkKey",
                table: "Books");
        }
    }
}
