using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRareBooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RareBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AuthorMention = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicationYear = table.Column<int>(type: "int", nullable: true),
                    Shelf = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Condition = table.Column<byte>(type: "tinyint", nullable: false),
                    PublicDescription = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    Binding = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Dimensions = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    ShelfLocation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    IsSold = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SoldAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoldAtFairId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SoldInSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriceSetBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RareBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RareBooks_AssoEvents_SoldAtFairId",
                        column: x => x.SoldAtFairId,
                        principalTable: "AssoEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RareBooks_ScanSessions_SoldInSessionId",
                        column: x => x.SoldInSessionId,
                        principalTable: "ScanSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RareBooks_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RareBooks_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RareBookPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RareBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RareBookPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RareBookPhotos_RareBooks_RareBookId",
                        column: x => x.RareBookId,
                        principalTable: "RareBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RareBookPhotos_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RareBookPhotos_RareBookId_Position",
                table: "RareBookPhotos",
                columns: new[] { "RareBookId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RareBookPhotos_UploadedBy",
                table: "RareBookPhotos",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_CreatedBy",
                table: "RareBooks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_Isbn13",
                table: "RareBooks",
                column: "Isbn13",
                unique: true,
                filter: "[Isbn13] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_Slug",
                table: "RareBooks",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_SoldAtFairId",
                table: "RareBooks",
                column: "SoldAtFairId");

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_SoldInSessionId",
                table: "RareBooks",
                column: "SoldInSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_Status_IsSold_Price",
                table: "RareBooks",
                columns: new[] { "Status", "IsSold", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_RareBooks_UpdatedBy",
                table: "RareBooks",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RareBookPhotos");

            migrationBuilder.DropTable(
                name: "RareBooks");
        }
    }
}
