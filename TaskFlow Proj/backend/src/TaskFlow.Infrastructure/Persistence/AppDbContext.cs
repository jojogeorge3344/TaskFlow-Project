using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PasswordResetGrant> PasswordResetGrants => Set<PasswordResetGrant>();
    public DbSet<MfaBackupCode> MfaBackupCodes => Set<MfaBackupCode>();

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ManagerProject> ManagerProjects => Set<ManagerProject>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentManager> DepartmentManagers => Set<DepartmentManager>();
    public DbSet<Domain.Entities.Task> Tasks => Set<Domain.Entities.Task>();
    public DbSet<Domain.Entities.TaskRecipient> TaskRecipients => Set<Domain.Entities.TaskRecipient>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskActivityLog> TaskActivityLogs => Set<TaskActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReminderHistory> ReminderHistory => Set<ReminderHistory>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();
    public DbSet<MailMessage> MailMessages => Set<MailMessage>();
    public DbSet<MailRecipient> MailRecipients => Set<MailRecipient>();
    public DbSet<MailboxItem> MailboxItems => Set<MailboxItem>();
    public DbSet<MailAttachment> MailAttachments => Set<MailAttachment>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<AssistantConversation> AssistantConversations => Set<AssistantConversation>();
    public DbSet<AssistantMessage> AssistantMessages => Set<AssistantMessage>();
    public DbSet<AssistantPendingAction> AssistantPendingActions => Set<AssistantPendingAction>();
    public DbSet<AssistantAudit> AssistantAudits => Set<AssistantAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.EmployeeCode).HasMaxLength(32);
            entity.Property(user => user.FullName).IsRequired().HasMaxLength(200);
            entity.Property(user => user.Username).IsRequired().HasMaxLength(100);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(256);
            entity.Property(user => user.MobileNumber).HasMaxLength(20);
            entity.Property(user => user.MfaSecretProtected).HasMaxLength(512);
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.SecurityStamp).IsRequired().HasMaxLength(64);

            entity
                .HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();


            entity.HasQueryFilter(user => !user.IsDeleted);

            entity
                .HasOne(user => user.Project)
                .WithMany(project => project.Users)
                .HasForeignKey(user => user.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(user => user.Department)
                .WithMany(department => department.Users)
                .HasForeignKey(user => user.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(user => user.Manager)
                .WithMany(manager => manager.Reports)
                .HasForeignKey(user => user.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ManagerProject>(entity =>
        {
            entity.HasKey(link => new { link.ManagerUserId, link.ProjectId });

            entity
                .HasOne(link => link.Manager)
                .WithMany(manager => manager.ManagerProjects)
                .HasForeignKey(link => link.ManagerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(link => link.Project)
                .WithMany()
                .HasForeignKey(link => link.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(link => link.ProjectId);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(department => department.Id);
            entity.Property(department => department.Name).IsRequired().HasMaxLength(200);
            entity.Property(department => department.Description).HasMaxLength(500);

            // A soft-deleted department keeps its name, so only live rows compete for it.
            entity.HasIndex(department => department.Name).IsUnique().HasFilter("\"IsDeleted\" = 0");

            entity.HasQueryFilter(department => !department.IsDeleted);
        });

        modelBuilder.Entity<DepartmentManager>(entity =>
        {
            entity.HasKey(link => new { link.ManagerUserId, link.DepartmentId });

            entity
                .HasOne(link => link.Manager)
                .WithMany(manager => manager.ManagedDepartments)
                .HasForeignKey(link => link.ManagerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(link => link.Department)
                .WithMany(department => department.ManagerLinks)
                .HasForeignKey(link => link.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(link => link.DepartmentId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(role => role.Id);
            // NOCASE makes the unique index reject "team lead" when "Team Lead" exists.
            entity.Property(role => role.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE");
            entity.HasIndex(role => role.Name).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Key).IsRequired().HasMaxLength(100);
            entity.Property(permission => permission.Name).IsRequired().HasMaxLength(200);
            entity.Property(permission => permission.Description).IsRequired().HasMaxLength(500);
            entity.Property(permission => permission.Group).IsRequired().HasMaxLength(100);
            entity.HasIndex(permission => permission.Key).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(grant => new { grant.RoleId, grant.PermissionId });

            entity
                .HasOne(grant => grant.Role)
                .WithMany(role => role.Permissions)
                .HasForeignKey(grant => grant.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(grant => grant.Permission)
                .WithMany(permission => permission.Roles)
                .HasForeignKey(grant => grant.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).IsRequired().HasMaxLength(200);
            entity.Property(project => project.Description).HasMaxLength(500);

            // A soft-deleted project keeps its name, so only live rows compete for it.
            entity.HasIndex(project => project.Name).IsUnique().HasFilter("\"IsDeleted\" = 0");

            entity.HasQueryFilter(project => !project.IsDeleted);
        });

        modelBuilder.Entity<Domain.Entities.Task>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.TaskCode).IsRequired().HasMaxLength(32);
            entity.Property(task => task.Title).IsRequired().HasMaxLength(200);
            entity.Property(task => task.Description).HasMaxLength(4000);

            entity.HasIndex(task => task.TaskCode).IsUnique();
            entity.HasIndex(task => task.Deadline);
            entity.HasIndex(task => task.AssignedToUserId);

            entity
                .HasOne(task => task.AssignedTo)
                .WithMany()
                .HasForeignKey(task => task.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(task => task.CreatedBy)
                .WithMany()
                .HasForeignKey(task => task.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(task => task.Project)
                .WithMany()
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Domain.Entities.TaskRecipient>(entity =>
        {
            entity.HasKey(recipient => recipient.Id);

            // A user can be a recipient of a given task only once (TDD §7.3).
            entity.HasIndex(recipient => new { recipient.TaskId, recipient.UserId }).IsUnique();

            entity
                .HasOne(recipient => recipient.Task)
                .WithMany()
                .HasForeignKey(recipient => recipient.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(recipient => recipient.User)
                .WithMany()
                .HasForeignKey(recipient => recipient.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).IsRequired();

            entity.HasIndex(token => token.TokenHash);

            entity.HasQueryFilter(token => !token.User!.IsDeleted);

            entity
                .HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).IsRequired();

            entity.HasIndex(token => token.TokenHash);
            entity.HasIndex(token => new { token.UserId, token.Used });

            entity
                .HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MfaBackupCode>(entity =>
        {
            entity.HasKey(code => code.Id);
            entity.Property(code => code.CodeHash).IsRequired().HasMaxLength(64);

            entity.HasIndex(code => new { code.UserId, code.CodeHash });

            entity
                .HasOne(code => code.User)
                .WithMany(user => user.MfaBackupCodes)
                .HasForeignKey(code => code.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetGrant>(entity =>
        {
            entity.HasKey(grant => grant.Id);
            entity.Property(grant => grant.TokenHash).IsRequired().HasMaxLength(64);

            entity.HasIndex(grant => grant.TokenHash);
            entity.HasIndex(grant => new { grant.UserId, grant.UsedAt });

            entity
                .HasOne(grant => grant.User)
                .WithMany()
                .HasForeignKey(grant => grant.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.Body).IsRequired();

            entity.HasIndex(comment => new { comment.TaskId, comment.CreatedAt });

            entity
                .HasOne(comment => comment.Task)
                .WithMany()
                .HasForeignKey(comment => comment.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(comment => comment.Author)
                .WithMany()
                .HasForeignKey(comment => comment.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskActivityLog>(entity =>
        {
            entity.HasKey(activity => activity.Id);
            entity.Property(activity => activity.EventType).HasConversion<string>().HasMaxLength(32);
            entity.Property(activity => activity.Message).IsRequired();

            entity.HasIndex(activity => new { activity.TaskId, activity.CreatedAt });

            entity
                .HasOne(activity => activity.Task)
                .WithMany()
                .HasForeignKey(activity => activity.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(activity => activity.Actor)
                .WithMany()
                .HasForeignKey(activity => activity.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(notification => notification.Message).IsRequired();

            entity.HasIndex(notification => new { notification.UserId, notification.IsRead });

            entity
                .HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(notification => notification.Task)
                .WithMany()
                .HasForeignKey(notification => notification.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReminderHistory>(entity =>
        {
            entity.HasKey(reminder => reminder.Id);
            entity.Property(reminder => reminder.ReminderType).HasConversion<string>().HasMaxLength(32);
            entity.Property(reminder => reminder.Status).HasConversion<string>().HasMaxLength(32);

            // BRD §3.5: one reminder event per task, recipient, type and slot, however often the scheduler runs.
            entity.HasIndex(reminder => new
            {
                reminder.TaskId,
                reminder.RecipientUserId,
                reminder.ReminderType,
                reminder.ScheduledFor
            }).IsUnique();

            entity
                .HasOne(reminder => reminder.Task)
                .WithMany()
                .HasForeignKey(reminder => reminder.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(reminder => reminder.RecipientUser)
                .WithMany()
                .HasForeignKey(reminder => reminder.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationSetting>(entity =>
        {
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Key).IsRequired().HasMaxLength(128);
            entity.Property(setting => setting.Value).IsRequired().HasMaxLength(1024);
            entity.Property(setting => setting.Category).IsRequired().HasMaxLength(64);

            entity.HasIndex(setting => setting.Key).IsUnique();
        });

        modelBuilder.Entity<MailMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Subject).IsRequired().HasMaxLength(200);
            entity.Property(message => message.Body).IsRequired().HasMaxLength(20000);
            entity.Property(message => message.DraftTo).HasMaxLength(4000);
            entity.Property(message => message.DraftCc).HasMaxLength(4000);

            entity.HasIndex(message => message.ConversationId);

            // A reminder event yields at most one mail, however often the scheduler runs.
            entity.HasIndex(message => message.ReminderHistoryId)
                .IsUnique()
                .HasFilter("\"ReminderHistoryId\" IS NOT NULL");

            entity
                .HasOne(message => message.ParentMessage)
                .WithMany()
                .HasForeignKey(message => message.ParentMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(message => message.SenderUser)
                .WithMany()
                .HasForeignKey(message => message.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(message => message.Task)
                .WithMany()
                .HasForeignKey(message => message.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(message => message.ReminderHistory)
                .WithMany()
                .HasForeignKey(message => message.ReminderHistoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MailRecipient>(entity =>
        {
            entity.HasKey(recipient => recipient.Id);
            entity.Property(recipient => recipient.Kind).HasConversion<string>().HasMaxLength(8);
            entity.HasIndex(recipient => new { recipient.MessageId, recipient.UserId }).IsUnique();

            entity
                .HasOne(recipient => recipient.Message)
                .WithMany(message => message.Recipients)
                .HasForeignKey(recipient => recipient.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(recipient => recipient.User)
                .WithMany()
                .HasForeignKey(recipient => recipient.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MailboxItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Folder).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(item => new { item.MessageId, item.OwnerUserId, item.Folder }).IsUnique();
            entity.HasIndex(item => new { item.OwnerUserId, item.Folder, item.PurgedAt, item.TrashedAt, item.SortAt });

            entity
                .HasOne(item => item.Message)
                .WithMany()
                .HasForeignKey(item => item.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(item => item.OwnerUser)
                .WithMany()
                .HasForeignKey(item => item.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MailAttachment>(entity =>
        {
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.FileName).IsRequired().HasMaxLength(255);
            entity.Property(attachment => attachment.ContentType).IsRequired().HasMaxLength(128);
            entity.HasIndex(attachment => attachment.MessageId);

            entity
                .HasOne(attachment => attachment.Message)
                .WithMany(message => message.Attachments)
                .HasForeignKey(attachment => attachment.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(attachment => attachment.UploadedByUser)
                .WithMany()
                .HasForeignKey(attachment => attachment.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiProviderConfig>(entity =>
        {
            entity.HasKey(config => config.Id);
            entity.Property(config => config.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(config => config.ModelName).IsRequired().HasMaxLength(100);
            entity.Property(config => config.BaseUrl).HasMaxLength(300);
            entity.Property(config => config.KeyLast4).HasMaxLength(4);
            entity.HasIndex(config => config.Kind).IsUnique();
        });

        modelBuilder.Entity<AiModel>(entity =>
        {
            entity.HasKey(model => model.Id);
            entity.Property(model => model.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(model => model.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(model => new { model.Kind, model.Name }).IsUnique();
        });

        modelBuilder.Entity<AssistantConversation>(entity =>
        {
            entity.HasKey(conversation => conversation.Id);
            entity.HasIndex(conversation => conversation.UserId);

            entity
                .HasOne(conversation => conversation.User)
                .WithMany()
                .HasForeignKey(conversation => conversation.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssistantMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(message => message.ProviderKind).HasConversion<string>().HasMaxLength(16);
            entity.Property(message => message.Content).IsRequired();
            entity.HasIndex(message => message.ConversationId);

            entity
                .HasOne(message => message.Conversation)
                .WithMany(conversation => conversation.Messages)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssistantPendingAction>(entity =>
        {
            entity.HasKey(action => action.Id);
            entity.Property(action => action.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(action => action.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(action => action.PayloadJson).IsRequired();
            entity.HasIndex(action => action.UserId);

            entity
                .HasOne(action => action.User)
                .WithMany()
                .HasForeignKey(action => action.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssistantAudit>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(audit => audit.Outcome).IsRequired().HasMaxLength(32);
            entity.HasIndex(audit => new { audit.UserId, audit.At });
        });
    }
}
