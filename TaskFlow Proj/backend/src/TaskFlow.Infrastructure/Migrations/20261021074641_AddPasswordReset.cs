using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AdminNotifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserNotifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TemporaryPasswordIssued = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoginPasswordResetNotices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PasswordResetRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginPasswordResetNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginPasswordResetNotices_PasswordResetRequests_PasswordResetRequestId",
                        column: x => x.PasswordResetRequestId,
                        principalTable: "PasswordResetRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoginPasswordResetNotices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginPasswordResetNotices_PasswordResetRequestId",
                table: "LoginPasswordResetNotices",
                column: "PasswordResetRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginPasswordResetNotices_UserId",
                table: "LoginPasswordResetNotices",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ProcessedByUserId",
                table: "PasswordResetRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ReferenceId",
                table: "PasswordResetRequests",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_Status",
                table: "PasswordResetRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserId_CreatedAt",
                table: "PasswordResetRequests",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginPasswordResetNotices");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");
        }
    }
}
