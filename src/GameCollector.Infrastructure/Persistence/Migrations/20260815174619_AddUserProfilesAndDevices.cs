using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilesAndDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdentitySubject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceRegistrations",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FcmToken = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistrations", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_DeviceRegistrations_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_DeviceRegistrations_UserId",
                table: "DeviceRegistrations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserProfiles_IdentitySubject",
                table: "UserProfiles",
                column: "IdentitySubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserProfiles_NormalizedUsername",
                table: "UserProfiles",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceRegistrations");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
