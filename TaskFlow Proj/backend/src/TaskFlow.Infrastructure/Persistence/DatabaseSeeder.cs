using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly SeedOptions _options;
    private readonly TimeProvider _timeProvider;

    public DatabaseSeeder(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IOptions<SeedOptions> options,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async System.Threading.Tasks.Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await EnsureUserAsync(
            "ADM-001",
            "System Administrator",
            "admin",
            "admin@taskflow.local",
            _options.AdministratorPassword,
            Role.Administrator,
            cancellationToken);

        await EnsureUserAsync(
            "MGR-001",
            "Team Manager",
            "manager",
            "manager@taskflow.local",
            _options.ManagerPassword,
            Role.Manager,
            cancellationToken);

        await EnsureUserAsync(
            "EMP-001",
            "Team Member",
            "employee",
            "employee@taskflow.local",
            _options.EmployeePassword,
            Role.Employee,
            cancellationToken);
    }

    private async System.Threading.Tasks.Task EnsureUserAsync(
        string employeeCode,
        string fullName,
        string username,
        string email,
        string password,
        Role role,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Username == username || user.Email == email, cancellationToken);

        if (exists)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = new User
        {
            EmployeeCode = employeeCode,
            FullName = fullName,
            Username = username,
            Email = email,
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
