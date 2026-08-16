using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated migration uses composite-column arrays.

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChangedAtUtc",
                table: "WishlistItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsPresent",
                table: "WishlistItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "LastServerSequence",
                table: "WishlistItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChangedAtUtc",
                table: "CollectionGames",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsOwned",
                table: "CollectionGames",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "LastServerSequence",
                table: "CollectionGames",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("UPDATE WishlistItems SET IsPresent = 1, ChangedAtUtc = CreatedAtUtc;");
            migrationBuilder.Sql("UPDATE CollectionGames SET IsOwned = 1, ChangedAtUtc = AddedAtUtc;");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMutations",
                columns: table => new
                {
                    MutationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMutations", x => new { x.UserId, x.MutationId });
                });

            migrationBuilder.CreateTable(
                name: "SyncEvents",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ScopeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncEvents", x => x.Sequence);
                });

            migrationBuilder.CreateTable(
                name: "SyncRetentionStates",
                columns: table => new
                {
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MinimumCursor = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRetentionStates", x => x.ScopeKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId_IsPresent",
                table: "WishlistItems",
                columns: new[] { "UserId", "IsPresent" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionGames_CollectionId_IsOwned",
                table: "CollectionGames",
                columns: new[] { "CollectionId", "IsOwned" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_NextAttemptAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMutations_ProcessedAtUtc",
                table: "ProcessedMutations",
                column: "ProcessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SyncEvents_ScopeType_ScopeId_Sequence",
                table: "SyncEvents",
                columns: new[] { "ScopeType", "ScopeId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ProcessedMutations");

            migrationBuilder.DropTable(
                name: "SyncEvents");

            migrationBuilder.DropTable(
                name: "SyncRetentionStates");

            migrationBuilder.DropIndex(
                name: "IX_WishlistItems_UserId_IsPresent",
                table: "WishlistItems");

            migrationBuilder.DropIndex(
                name: "IX_CollectionGames_CollectionId_IsOwned",
                table: "CollectionGames");

            migrationBuilder.DropColumn(
                name: "ChangedAtUtc",
                table: "WishlistItems");

            migrationBuilder.DropColumn(
                name: "IsPresent",
                table: "WishlistItems");

            migrationBuilder.DropColumn(
                name: "LastServerSequence",
                table: "WishlistItems");

            migrationBuilder.DropColumn(
                name: "ChangedAtUtc",
                table: "CollectionGames");

            migrationBuilder.DropColumn(
                name: "IsOwned",
                table: "CollectionGames");

            migrationBuilder.DropColumn(
                name: "LastServerSequence",
                table: "CollectionGames");
        }
    }
}
