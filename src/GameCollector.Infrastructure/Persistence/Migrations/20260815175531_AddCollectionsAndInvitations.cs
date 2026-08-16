using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861 // Generated migration uses composite-column arrays.

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionsAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCollectionId",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_UserProfiles_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionInvitations_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionInvitations_UserProfiles_InviteeUserId",
                        column: x => x.InviteeUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionInvitations_UserProfiles_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionMembers_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionMembers_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_DefaultCollectionId",
                table: "UserProfiles",
                column: "DefaultCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionInvitations_CollectionId_InviteeUserId",
                table: "CollectionInvitations",
                columns: new[] { "CollectionId", "InviteeUserId" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionInvitations_InviteeUserId",
                table: "CollectionInvitations",
                column: "InviteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionInvitations_InviterUserId",
                table: "CollectionInvitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMembers_CollectionId_UserId",
                table: "CollectionMembers",
                columns: new[] { "CollectionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionMembers_UserId",
                table: "CollectionMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_OwnerUserId",
                table: "Collections",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Collections_DefaultCollectionId",
                table: "UserProfiles",
                column: "DefaultCollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Collections_DefaultCollectionId",
                table: "UserProfiles");

            migrationBuilder.DropTable(
                name: "CollectionInvitations");

            migrationBuilder.DropTable(
                name: "CollectionMembers");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_DefaultCollectionId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultCollectionId",
                table: "UserProfiles");
        }
    }
}
