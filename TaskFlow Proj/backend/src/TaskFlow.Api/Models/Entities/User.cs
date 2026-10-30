namespace TaskFlow.Api.Models.Entities;

public class User
{
    public int Id { get; set; }

    public string? EmployeeCode { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Optional contact number in international (E.164) format, for example +919876543210. Not unique: email is the
    /// only unique identity, so colleagues may share a number.</summary>
    public string? MobileNumber { get; set; }

    /// <summary>The authenticator key, encrypted. Set during setup; it only takes effect once <see cref="MfaEnabledAt"/> is set.</summary>
    public string? MfaSecretProtected { get; set; }

    /// <summary>When MFA was confirmed and turned on; null while MFA is off or setup is unconfirmed.</summary>
    public DateTime? MfaEnabledAt { get; set; }

    /// <summary>The highest time step already accepted, so a code can never be used twice.</summary>
    public long MfaLastUsedStep { get; set; }

    public int MfaFailedAttempts { get; set; }

    public DateTime? MfaLockedUntil { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Changes whenever the sign-in credentials change; access tokens carrying an older stamp are rejected.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsDeleted { get; set; }



    /// <summary>The projects this user is assigned to, each with a percentage and a position.</summary>
    public ICollection<UserProjectAllocation> ProjectAllocations { get; set; } = new List<UserProjectAllocation>();

    /// <summary>The departments this user belongs to.</summary>
    public ICollection<UserDepartment> DepartmentMemberships { get; set; } = new List<UserDepartment>();

    /// <summary>For a manager: the projects they oversee.</summary>
    public ICollection<ManagerProject> ManagerProjects { get; set; } = new List<ManagerProject>();

    /// <summary>For a manager: the departments they manage.</summary>
    public ICollection<DepartmentManager> ManagedDepartments { get; set; } = new List<DepartmentManager>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<MfaBackupCode> MfaBackupCodes { get; set; } = new List<MfaBackupCode>();
}
