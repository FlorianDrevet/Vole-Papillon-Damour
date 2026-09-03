using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vole_Papillon_Damour.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookExchangeCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssociationSettings",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    DuplicateThreshold = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    DemandSalesThreshold = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DeadStockMinAgeDays = table.Column<int>(type: "int", nullable: false),
                    DeadStockMinQuantity = table.Column<int>(type: "int", nullable: false),
                    WatchlistMaxItems = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    AlertCooldownDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    SessionIdleTimeoutMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 120),
                    AlertDelayMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 120),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationSettings", x => x.Id);
                    table.CheckConstraint("CK_AssociationSettings_SingletonId", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_AssociationSettings_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    RedirectedToIsbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: true),
                    WorkId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Authors = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Publisher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicationYear = table.Column<int>(type: "int", nullable: true),
                    PhysicalFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Genre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SalesCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RejectionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsRare = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsHiddenFromCatalog = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CoverBlobRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetadataStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    MetadataSource = table.Column<byte>(type: "tinyint", nullable: true),
                    MetadataFetchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolveAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManuallyEditedFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAvailableAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Isbn13);
                    table.CheckConstraint("CK_Books_NoSelfRedirect", "[RedirectedToIsbn13] IS NULL OR [RedirectedToIsbn13] <> [Isbn13]");
                    table.ForeignKey(
                        name: "FK_Books_Books_RedirectedToIsbn13",
                        column: x => x.RedirectedToIsbn13,
                        principalTable: "Books",
                        principalColumn: "Isbn13",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VolunteerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetAssoEventsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastScanAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LateArrivals = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseReason = table.Column<byte>(type: "tinyint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ScannedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    KeptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RejectedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanSessions_AssoEvents_TargetAssoEventsId",
                        column: x => x.TargetAssoEventsId,
                        principalTable: "AssoEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanSessions_Users_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookAnnouncements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    AssoEventsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScanSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookAnnouncements", x => x.Id);
                    table.CheckConstraint("CK_BookAnnouncements_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_BookAnnouncements_AssoEvents_AssoEventsId",
                        column: x => x.AssoEventsId,
                        principalTable: "AssoEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookAnnouncements_Books_Isbn13",
                        column: x => x.Isbn13,
                        principalTable: "Books",
                        principalColumn: "Isbn13",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookAnnouncements_ScanSessions_ScanSessionId",
                        column: x => x.ScanSessionId,
                        principalTable: "ScanSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn13 = table.Column<string>(type: "char(13)", unicode: false, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClockSuspect = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScanSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VolunteerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssoEventsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClientGestureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookMovements", x => x.Id);
                    table.CheckConstraint("CK_BookMovements_Quantity_NonZero", "[Quantity] <> 0");
                    table.ForeignKey(
                        name: "FK_BookMovements_AssoEvents_AssoEventsId",
                        column: x => x.AssoEventsId,
                        principalTable: "AssoEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookMovements_Books_Isbn13",
                        column: x => x.Isbn13,
                        principalTable: "Books",
                        principalColumn: "Isbn13",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookMovements_ScanSessions_ScanSessionId",
                        column: x => x.ScanSessionId,
                        principalTable: "ScanSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookMovements_Users_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssociationSettings_UpdatedBy",
                table: "AssociationSettings",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BookAnnouncements_AssoEventsId",
                table: "BookAnnouncements",
                column: "AssoEventsId",
                filter: "[AssoEventsId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookAnnouncements_AssoEventsId_Status",
                table: "BookAnnouncements",
                columns: new[] { "AssoEventsId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookAnnouncements_Isbn13",
                table: "BookAnnouncements",
                column: "Isbn13");

            migrationBuilder.CreateIndex(
                name: "IX_BookAnnouncements_ScanSessionId",
                table: "BookAnnouncements",
                column: "ScanSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_AssoEventsId_Type",
                table: "BookMovements",
                columns: new[] { "AssoEventsId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_ClientGestureId",
                table: "BookMovements",
                column: "ClientGestureId",
                unique: true,
                filter: "[ClientGestureId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_Isbn13_OccurredAt",
                table: "BookMovements",
                columns: new[] { "Isbn13", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_ScanSessionId",
                table: "BookMovements",
                column: "ScanSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BookMovements_VolunteerId",
                table: "BookMovements",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_MetadataStatus_LastAttemptAt",
                table: "Books",
                columns: new[] { "MetadataStatus", "LastAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_RedirectedToIsbn13",
                table: "Books",
                column: "RedirectedToIsbn13");

            migrationBuilder.CreateIndex(
                name: "IX_Books_UpdatedAt",
                table: "Books",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Books_WorkId",
                table: "Books",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanSessions_TargetAssoEventsId",
                table: "ScanSessions",
                column: "TargetAssoEventsId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanSessions_VolunteerId",
                table: "ScanSessions",
                column: "VolunteerId",
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssociationSettings");

            migrationBuilder.DropTable(
                name: "BookAnnouncements");

            migrationBuilder.DropTable(
                name: "BookMovements");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "ScanSessions");
        }
    }
}
