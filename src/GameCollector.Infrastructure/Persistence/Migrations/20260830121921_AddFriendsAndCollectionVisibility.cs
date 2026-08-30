using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendsAndCollectionVisibility : Migration
    {
        private static readonly string[] AddresseeStatusIndexColumns = ["AddresseeUserId", "Status"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Collections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddresseeUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PairKey = table.Column<string>(type: "TEXT", maxLength: 65, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Friendships_UserProfiles_AddresseeUserId",
                        column: x => x.AddresseeUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Friendships_UserProfiles_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_AddresseeUserId_Status",
                table: "Friendships",
                columns: AddresseeStatusIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_PairKey",
                table: "Friendships",
                column: "PairKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterUserId",
                table: "Friendships",
                column: "RequesterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Collections");
        }
    }
}
