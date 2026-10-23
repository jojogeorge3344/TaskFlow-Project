using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSmsOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropIndex(
                name: "IX_Users_MobileNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MobileNumber",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "PasswordResetGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetGrants_TokenHash",
                table: "PasswordResetGrants",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetGrants_UserId_UsedAt",
                table: "PasswordResetGrants",
                columns: new[] { "UserId", "UsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetGrants");

            migrationBuilder.AddColumn<string>(
                name: "MobileNumber",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PasswordResetOtps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MobileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OtpHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResetTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResetTokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ResetTokenUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Salt = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SmsSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOtps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetOtps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_MobileNumber",
                table: "Users",
                column: "MobileNumber",
                unique: true,
                filter: "\"MobileNumber\" IS NOT NULL AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_CreatedAt",
                table: "PasswordResetOtps",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_MobileHash_CreatedAt",
                table: "PasswordResetOtps",
                columns: new[] { "MobileHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_ResetTokenHash",
                table: "PasswordResetOtps",
                column: "ResetTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_UserId",
                table: "PasswordResetOtps",
                column: "UserId");
        }
    }
}
