using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Assistant;

namespace TaskFlow.Api.Repositories.Assistant;

public class AssistantConversationRepository : BaseRepository<AssistantConversation>, IAssistantConversationRepository
{
    public AssistantConversationRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<AssistantConversation?> FindLatestForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

public class AssistantMessageRepository : BaseRepository<AssistantMessage>, IAssistantMessageRepository
{
    public AssistantMessageRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AssistantMessage>> GetNewestAsync(int conversationId, int take, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public class AssistantPendingActionRepository : BaseRepository<AssistantPendingAction>, IAssistantPendingActionRepository
{
    public AssistantPendingActionRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AssistantPendingAction>> GetForUserAsync(
        int userId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(action => action.UserId == userId && ids.Contains(action.Id))
            .ToListAsync(cancellationToken);

    public Task<AssistantPendingAction?> FindForUserAsync(Guid id, int userId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

    public Task<AssistantPendingAction?> FindForUserForUpdateAsync(Guid id, int userId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

    public async Task<bool> TryClaimAsync(Guid id, DateTime now, CancellationToken cancellationToken = default) =>
        await Set
            .Where(candidate => candidate.Id == id && candidate.Status == AssistantActionStatus.Pending)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(candidate => candidate.Status, AssistantActionStatus.Confirmed)
                    .SetProperty(candidate => candidate.ResolvedAt, now),
                cancellationToken) > 0;

    public System.Threading.Tasks.Task SetStatusAsync(Guid id, AssistantActionStatus status, DateTime now, CancellationToken cancellationToken = default) =>
        Set.Where(candidate => candidate.Id == id)
            .ExecuteUpdateAsync(
                set => set.SetProperty(candidate => candidate.Status, status).SetProperty(candidate => candidate.ResolvedAt, now),
                cancellationToken);
}

public class AssistantAuditRepository : BaseRepository<AssistantAudit>, IAssistantAuditRepository
{
    public AssistantAuditRepository(AppDbContext db)
        : base(db)
    {
    }
}

public class AssistantDirectoryRepository : BaseRepository<User>, IAssistantDirectoryRepository
{
    public AssistantDirectoryRepository(AppDbContext db)
        : base(db)
    {
    }

    private IQueryable<User> Active => Set.AsNoTracking().Where(user => user.IsActive);

    public Task<string?> GetFullNameAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(string FullName, string Email)?> GetNameAndEmailAsync(int userId, CancellationToken cancellationToken = default)
    {
        var me = await Set.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.FullName, user.Email })
            .FirstOrDefaultAsync(cancellationToken);
        return me is null ? null : (me.FullName, me.Email);
    }

    public Task<bool> EmailUsedByOtherAsync(string email, int exceptUserId, CancellationToken cancellationToken = default)
    {
        var lowered = email.ToLowerInvariant();
        return Set.AnyAsync(user => user.Id != exceptUserId && user.Email.ToLower() == lowered, cancellationToken);
    }

    public async Task<(int Id, string Name)?> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var role = await Db.Roles
            .AsNoTracking()
            .Where(candidate => candidate.Name == name)
            .Select(candidate => new { candidate.Id, candidate.Name })
            .FirstOrDefaultAsync(cancellationToken);
        return role is null ? null : (role.Id, role.Name);
    }

    public Task<ResolvedUser?> FindActiveAsync(int userId, CancellationToken cancellationToken = default) =>
        Active.Where(user => user.Id == userId)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ResolvedUser?> FindActiveByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var lower = email.ToLowerInvariant();
        return Active.Where(user => user.Email.ToLower() == lower)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResolvedUser>> FindActiveByExactNameAsync(string lowerName, int take, CancellationToken cancellationToken = default) =>
        await Active.Where(user => user.FullName.ToLower() == lowerName || user.Username.ToLower() == lowerName)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ResolvedUser>> SearchActiveByNameAsync(string lowerTerm, int take, CancellationToken cancellationToken = default)
    {
        var escaped = lowerTerm.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return await Active
            .Where(user => EF.Functions.Like(user.FullName, $"%{escaped}%", "\\") || EF.Functions.Like(user.Username, $"%{escaped}%", "\\"))
            .OrderBy(user => user.FullName)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetDepartmentIdsAsync(int userId, CancellationToken cancellationToken = default) =>
        await Db.UserDepartments
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.DepartmentId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ResolvedUser>> GetActiveManagersOfDepartmentsAsync(
        IReadOnlyCollection<int> departmentIds,
        int exceptUserId,
        CancellationToken cancellationToken = default) =>
        await Active
            .Where(user => user.RoleId == SystemRoles.ManagerId && user.Id != exceptUserId
                && user.ManagedDepartments.Any(link => departmentIds.Contains(link.DepartmentId)))
            .OrderBy(user => user.FullName)
            .Select(user => new ResolvedUser(user.Id, user.FullName, user.Email, user.Role.Name, user.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MailRecipientRef>> GetActiveRecipientsAsync(
        IReadOnlyCollection<string> lowerEmails,
        CancellationToken cancellationToken = default) =>
        await Active
            .Where(user => lowerEmails.Contains(user.Email.ToLower()))
            .Select(user => new MailRecipientRef(user.Id, user.FullName, user.Email))
            .ToListAsync(cancellationToken);
}
