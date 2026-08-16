using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated migration uses composite-column arrays.

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumAge = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumPlayingTimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumPlayingTimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    ModerationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_UserProfiles_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameBarcodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 14, nullable: false),
                    NormalizedBarcode = table.Column<string>(type: "TEXT", maxLength: 14, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameBarcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameBarcodes_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameLanguages",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LanguageId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameLanguages", x => new { x.GameId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_GameLanguages_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameLanguages_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameTags",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTags", x => new { x.GameId, x.TagId });
                    table.ForeignKey(
                        name: "FK_GameTags_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "MaximumPlayers", "MaximumPlayingTimeMinutes", "MinimumAge", "MinimumPlayers", "MinimumPlayingTimeMinutes", "ModerationStatus", "Publisher", "ReleaseYear", "Revision", "SubmittedByUserId", "Title", "UpdatedAtUtc" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 15, 0, 0, 0, 0, DateTimeKind.Utc), "The classic matching game with a two-sided deck.", 10, 30, 7, 2, 15, 2, "Mattel", 2019, 1L, null, "UNO Flip!", new DateTime(2026, 8, 15, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "en", "English" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "nl", "Dutch" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "fr", "French" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "de", "German" }
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Card Game" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Family" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Party" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Strategy" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "Cooperative" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "Fast" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "Two Player" }
                });

            migrationBuilder.InsertData(
                table: "GameBarcodes",
                columns: new[] { "Id", "Barcode", "GameId", "NormalizedBarcode" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000002"), "887961751062", new Guid("30000000-0000-0000-0000-000000000001"), "887961751062" });

            migrationBuilder.InsertData(
                table: "GameLanguages",
                columns: new[] { "GameId", "LanguageId" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.InsertData(
                table: "GameTags",
                columns: new[] { "GameId", "TagId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameBarcodes_GameId",
                table: "GameBarcodes",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBarcodes_NormalizedBarcode",
                table: "GameBarcodes",
                column: "NormalizedBarcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameLanguages_LanguageId",
                table: "GameLanguages",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ModerationStatus",
                table: "Games",
                column: "ModerationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SubmittedByUserId",
                table: "Games",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Title",
                table: "Games",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_GameTags_TagId",
                table: "GameTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Name",
                table: "Languages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameBarcodes");

            migrationBuilder.DropTable(
                name: "GameLanguages");

            migrationBuilder.DropTable(
                name: "GameTags");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
