using TaskFlow.Api.Common.Exceptions;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Common.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class UserServiceSelfServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserService _service;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public UserServiceSelfServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);
        _service = new UserService(_db, _hasher, new UserAllocationService(_db));
    }

    private User AddUser(string username, string email, string role = SystemRoles.Employee)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = email,
            RoleId = TestAccessControl.IdFor(role),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = "stamp-1"
        };
        user.PasswordHash = _hasher.HashPassword(user, "P@ssw0rd!");
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task UpdateOwnProfile_ChangesNameAndEmail()
    {
        var user = AddUser("self1", "self1@taskflow.com");

        var dto = await _service.UpdateOwnProfileAsync(user.Id, new UpdateOwnProfileRequest("New Name", "new@taskflow.com"));

        Assert.NotNull(dto);
        Assert.Equal("New Name", dto!.FullName);
        Assert.Equal("new@taskflow.com", dto.Email);
    }

    [Fact]
    public async Task UpdateOwnProfile_RejectsDuplicateEmail()
    {
        var user = AddUser("self2", "self2@taskflow.com");
        AddUser("other", "other@taskflow.com");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateOwnProfileAsync(user.Id, new UpdateOwnProfileRequest("Self 2", "other@taskflow.com")));
    }

    [Fact]
    public async Task UpdateOwnProfile_LeavesRestrictedFieldsUntouched()
    {
        var user = AddUser("self3", "self3@taskflow.com", SystemRoles.Employee);

        await _service.UpdateOwnProfileAsync(user.Id, new UpdateOwnProfileRequest("Renamed", "self3b@taskflow.com"));

        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal("self3", reloaded.Username);
        Assert.Equal(SystemRoles.EmployeeId, reloaded.RoleId);
        Assert.Equal(await _db.UserProjectAllocations.AsNoTracking().CountAsync(a => a.UserId == user.Id), await _db.UserProjectAllocations.AsNoTracking().CountAsync(a => a.UserId == reloaded.Id));
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task ChangeOwnPassword_UpdatesPasswordAndEndsSessions()
    {
        var user = AddUser("self5", "self5@taskflow.com");
        var originalStamp = user.SecurityStamp;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "refresh-token-hash-1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var dto = await _service.ChangeOwnPasswordAsync(user.Id, new ChangePasswordRequest("N3wP@ss!"));

        Assert.NotNull(dto);
        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.NotEqual(originalStamp, reloaded.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(reloaded, reloaded.PasswordHash, "N3wP@ss!"));

        var token = await _db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == user.Id);
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task SelfService_OnUnknownUser_ReturnsNull()
    {
        Assert.Null(await _service.UpdateOwnProfileAsync(9999, new UpdateOwnProfileRequest("X", "x@taskflow.com"), CancellationToken.None));
        Assert.Null(await _service.ChangeOwnPasswordAsync(9999, new ChangePasswordRequest("bbbbbb"), CancellationToken.None));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
