using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Auth;

public class PasswordResetService : IPasswordResetService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;

    public PasswordResetService(
        IApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<string> ApplyGeneratedPasswordAsync(
        User user,
        PasswordResetToken? token,
        string notificationMessage,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var newPassword = PasswordGenerator.Generate();

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        // An old stamp makes every access token already handed out fail session validation.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = now;

        var activeTokens = await _db.RefreshTokens
            .Where(stored => stored.UserId == user.Id && stored.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = now;
        }

        if (token is not null)
        {
            token.Used = true;
            token.ConsumedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _notificationPublisher.PublishAsync(
            user.Id,
            taskId: null,
            NotificationType.System,
            notificationMessage,
            cancellationToken);

        return newPassword;
    }
}
