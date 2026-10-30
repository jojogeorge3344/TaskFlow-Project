using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The SMTP email delivery (AddEmailDelivery) is replaced by internal mail; drop its table and settings.
            migrationBuilder.DropTable(
                name: "EmailDeliveryLogs");

            migrationBuilder.Sql(
                "DELETE FROM \"ApplicationSettings\" WHERE \"Key\" LIKE 'Email.%' OR \"Key\" = 'Notification.EmailEnabled';");

            migrationBuilder.CreateTable(
                name: "MailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentMessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    SenderUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReminderHistoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftTo = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    DraftCc = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailMessages_MailMessages_ParentMessageId",
                        column: x => x.ParentMessageId,
                        principalTable: "MailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MailMessages_ReminderHistory_ReminderHistoryId",
                        column: x => x.ReminderHistoryId,
                        principalTable: "ReminderHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MailMessages_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MailMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MailAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    UploadedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailAttachments_MailMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "MailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MailboxItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsStarred = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsImportant = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrashedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PurgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SortAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailboxItems_MailMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "MailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailboxItems_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MailRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailRecipients_MailMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "MailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailAttachments_MessageId",
                table: "MailAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MailAttachments_UploadedByUserId",
                table: "MailAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MailboxItems_MessageId_OwnerUserId_Folder",
                table: "MailboxItems",
                columns: new[] { "MessageId", "OwnerUserId", "Folder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailboxItems_OwnerUserId_Folder_PurgedAt_TrashedAt_SortAt",
                table: "MailboxItems",
                columns: new[] { "OwnerUserId", "Folder", "PurgedAt", "TrashedAt", "SortAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_ConversationId",
                table: "MailMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_ParentMessageId",
                table: "MailMessages",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_ReminderHistoryId",
                table: "MailMessages",
                column: "ReminderHistoryId",
                unique: true,
                filter: "\"ReminderHistoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_SenderUserId",
                table: "MailMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MailMessages_TaskId",
                table: "MailMessages",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_MailRecipients_MessageId_UserId",
                table: "MailRecipients",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailRecipients_UserId",
                table: "MailRecipients",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailAttachments");

            migrationBuilder.DropTable(
                name: "MailboxItems");

            migrationBuilder.DropTable(
                name: "MailRecipients");

            migrationBuilder.DropTable(
                name: "MailMessages");

            migrationBuilder.CreateTable(
                name: "EmailDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipientUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReminderHistoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TaskDeadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ToAddress = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryLogs_ReminderHistory_ReminderHistoryId",
                        column: x => x.ReminderHistoryId,
                        principalTable: "ReminderHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryLogs_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryLogs_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_CreatedAt",
                table: "EmailDeliveryLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_RecipientUserId",
                table: "EmailDeliveryLogs",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_ReminderHistoryId",
                table: "EmailDeliveryLogs",
                column: "ReminderHistoryId",
                unique: true,
                filter: "\"ReminderHistoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_Status_NextAttemptAt",
                table: "EmailDeliveryLogs",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryLogs_TaskId",
                table: "EmailDeliveryLogs",
                column: "TaskId");
        }
    }
}
