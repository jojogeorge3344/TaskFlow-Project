using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

public class PasswordResetPersistenceTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public PasswordResetPersistenceTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SecondNoticeForTheSameUser_IsRejectedByTheUniqueIndex()
    {
        var username = $"pwd-notice-{Guid.NewGuid():N}"[..24];
        await _factory.SeedUserAsync(username, $"{username}@example.com", "Passw0rd!", SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var userId = await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();

        var request = new PasswordResetRequest
        {
            ReferenceId = Guid.NewGuid(),
            UserId = userId,
            Origin = PasswordResetOrigin.LoginScreen,
            Status = PasswordResetStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        db.PasswordResetRequests.Add(request);
        await db.SaveChangesAsync();

        db.LoginPasswordResetNotices.Add(new LoginPasswordResetNotice
        {
            UserId = userId,
            PasswordResetRequestId = request.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.LoginPasswordResetNotices.Add(new LoginPasswordResetNotice
        {
            UserId = userId,
            PasswordResetRequestId = request.Id,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TwoRequests_AreDistinctRows_WithTheirOwnReferenceAndTimestamps()
    {
        var username = $"pwd-req-{Guid.NewGuid():N}"[..24];
        await _factory.SeedUserAsync(username, $"{username}@example.com", "Passw0rd!", SystemRoles.Employee);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var userId = await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
        var createdAt = DateTime.UtcNow;

        db.PasswordResetRequests.Add(new PasswordResetRequest
        {
            ReferenceId = Guid.NewGuid(),
            UserId = userId,
            Origin = PasswordResetOrigin.LoginScreen,
            Status = PasswordResetStatus.Pending,
            CreatedAt = createdAt
        });
        db.PasswordResetRequests.Add(new PasswordResetRequest
        {
            ReferenceId = Guid.NewGuid(),
            UserId = userId,
            Origin = PasswordResetOrigin.ProfileScreen,
            Status = PasswordResetStatus.Pending,
            CreatedAt = createdAt.AddMinutes(1)
        });
        await db.SaveChangesAsync();

        var requests = await db.PasswordResetRequests
            .Where(request => request.UserId == userId)
            .OrderBy(request => request.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, requests.Count);
        Assert.NotEqual(requests[0].ReferenceId, requests[1].ReferenceId);
        Assert.Equal(createdAt, requests[0].CreatedAt);
        Assert.Equal(PasswordResetStatus.Pending, requests[0].Status);
        Assert.Equal(PasswordResetOrigin.LoginScreen, requests[0].Origin);
        Assert.Equal(PasswordResetOrigin.ProfileScreen, requests[1].Origin);
    }
}
