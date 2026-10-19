using Microsoft.AspNetCore.Identity;
using TaskFlow.Domain.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

internal sealed class AuthServiceHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IPasswordHasher<User> _passwordHasher;

    private AuthServiceHarness(SqliteConnection connection, AppDbContext db, IAuthService service, IPasswordHasher<User> passwordHasher)
    {
        _connection = connection;
        Db = db;
        Service = service;
        _passwordHasher = passwordHasher;
    }

    public AppDbContext Db { get; }

    public IAuthService Service { get; }

    public static AuthServiceHarness Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        TestAccessControl.Seed(db);

        var passwordHasher = new PasswordHasher<User>();
        var tokenService = new JwtTokenService(
            Options.Create(new JwtOptions { Issuer = "TaskFlow", Key = new string('k', 64) }),
            TimeProvider.System);

        var service = new AuthService(db, tokenService, passwordHasher, TimeProvider.System);

        return new AuthServiceHarness(connection, db, service, passwordHasher);
    }

    public User AddUser(
        string username,
        string email,
        string password,
        bool isActive = true,
        string role = SystemRoles.Employee)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = email,
            RoleId = TestAccessControl.IdFor(role),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        Db.Users.Add(user);
        Db.SaveChanges();

        return user;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
