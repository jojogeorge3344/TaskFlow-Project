using Microsoft.AspNetCore.Identity;
using TaskFlow.Domain.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
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
        _service = new UserService(_db, _hasher);
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
        var user = AddUser("self1", "self1@taskflow.local");

        var dto = await _service.UpdateOwnProfileAsync(user.Id, new UpdateOwnProfileRequest("New Name", "new@taskflow.local"));

        Assert.NotNull(dto);
        Assert.Equal("New Name", dto!.FullName);
        Assert.Equal("new@taskflow.local", dto.Email);
    }

    [Fact]
    public async Task UpdateOwnProfile_RejectsDuplicateEmail()
    {
        var user = AddUser("self2", "self2@taskflow.local");
        AddUser("other", "other@taskflow.local");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateOwnProfileAsync(user.Id, new UpdateOwnProfileRequest("Self 2", "other@taskflow.local")));
    }

    [Fact]
    public async Task UpdateOwnProfile_LeavesRestrictedFieldsUntouched()
    {
        var user = AddUser("self3", "self3@taskflow.local", SystemRoles.Employee);

        await _service.UpdateOwnProfileAsync(user.Id, new UpdateOwnProfileRequest("Renamed", "self3b@taskflow.local"));

        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal("self3", reloaded.Username);
        Assert.Equal(SystemRoles.EmployeeId, reloaded.RoleId);
        Assert.Equal(user.DepartmentId, reloaded.DepartmentId);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task ChangeOwnPassword_WrongCurrentPassword_IsRejected()
    {
        var user = AddUser("self4", "self4@taskflow.local");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ChangeOwnPasswordAsync(user.Id, new ChangePasswordRequest("wrong", "N3wP@ss!")));

        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(reloaded, reloaded.PasswordHash, "P@ssw0rd!"));
    }

    [Fact]
    public async Task ChangeOwnPassword_CorrectCurrentPassword_UpdatesAndEndsSessions()
    {
        var user = AddUser("self5", "self5@taskflow.local");
        var originalStamp = user.SecurityStamp;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "refresh-token-hash-1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var dto = await _service.ChangeOwnPasswordAsync(user.Id, new ChangePasswordRequest("P@ssw0rd!", "N3wP@ss!"));

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
        Assert.Null(await _service.UpdateOwnProfileAsync(9999, new UpdateOwnProfileRequest("X", "x@taskflow.local"), CancellationToken.None));
        Assert.Null(await _service.ChangeOwnPasswordAsync(9999, new ChangePasswordRequest("a", "bbbbbb"), CancellationToken.None));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
