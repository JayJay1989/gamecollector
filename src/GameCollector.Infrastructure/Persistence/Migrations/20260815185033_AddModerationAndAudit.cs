using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated migration uses composite-column arrays.

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationComment",
                table: "Games",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    BeforeJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_UserProfiles_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposedChangesJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AdminComment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameChangeRequests_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameChangeRequests_UserProfiles_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameChangeRequests_UserProfiles_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "ApprovedAtUtc", "ApprovedByUserId", "ModerationComment" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Games_ApprovedByUserId",
                table: "Games",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameChangeRequests_GameId_ProposedByUserId",
                table: "GameChangeRequests",
                columns: new[] { "GameId", "ProposedByUserId" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GameChangeRequests_ProposedByUserId",
                table: "GameChangeRequests",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameChangeRequests_ReviewedByUserId",
                table: "GameChangeRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameChangeRequests_Status",
                table: "GameChangeRequests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_UserProfiles_ApprovedByUserId",
                table: "Games",
                column: "ApprovedByUserId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_UserProfiles_ApprovedByUserId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "GameChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_Games_ApprovedByUserId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ModerationComment",
                table: "Games");
        }
    }
}
