using TaskFlow.Api.Services.Auth;
using TaskFlow.Api.Integrations.Security;
using TaskFlow.Api.Config;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Common.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.UnitTests.Auth;

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

        var mfaProtector = new TaskFlow.Api.Integrations.Security.MfaSecretProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), TimeProvider.System);
        var mfaService = TestServices.Mfa(db, passwordHasher, mfaProtector, TestServices.NotificationPublisher(db, TimeProvider.System), Options.Create(new TaskFlow.Api.Config.MfaOptions()), TimeProvider.System);
        var service = TestServices.Auth(db, tokenService, passwordHasher, TimeProvider.System, mfaService, mfaProtector, Options.Create(new TaskFlow.Api.Config.MfaOptions()));

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
