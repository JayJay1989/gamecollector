using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncDiagnostics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastSuccessfulSyncAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastCursor = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedMutations = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadedEvents = table.Column<long>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastErrorAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDiagnostics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncDiagnostics_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncDiagnostics_LastSuccessfulSyncAtUtc",
                table: "SyncDiagnostics",
                column: "LastSuccessfulSyncAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SyncDiagnostics_UserId_DeviceId",
                table: "SyncDiagnostics",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncDiagnostics");
        }
    }
}
