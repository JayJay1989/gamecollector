using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameChangeRequestImages : Migration
    {
        private static readonly string[] ChangeRequestImageIndexColumns = ["ChangeRequestId", "ImageType"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameChangeRequestImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImageType = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameChangeRequestImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameChangeRequestImages_GameChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "GameChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameChangeRequestImages_ChangeRequestId_ImageType",
                table: "GameChangeRequestImages",
                columns: ChangeRequestImageIndexColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameChangeRequestImages");
        }
    }
}
