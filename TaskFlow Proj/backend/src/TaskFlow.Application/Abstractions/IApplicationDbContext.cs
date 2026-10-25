using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<PasswordResetGrant> PasswordResetGrants { get; }

    DbSet<MfaBackupCode> MfaBackupCodes { get; }

    DbSet<Role> Roles { get; }

    DbSet<RolePermission> RolePermissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
