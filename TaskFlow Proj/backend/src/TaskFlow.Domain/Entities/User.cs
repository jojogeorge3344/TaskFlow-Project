namespace TaskFlow.Domain.Entities;

public class User
{
    public int Id { get; set; }

    public string? EmployeeCode { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

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

    public int? ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>The department this employee belongs to, if any. Set from the Department Management screen or the employee form.</summary>
    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    /// <summary>The manager this user reports to, if any. Recorded data only.</summary>
    public int? ManagerId { get; set; }

    public User? Manager { get; set; }

    public ICollection<User> Reports { get; set; } = new List<User>();

    /// <summary>For a manager: the projects they oversee.</summary>
    public ICollection<ManagerProject> ManagerProjects { get; set; } = new List<ManagerProject>();

    /// <summary>For a manager: the departments they manage.</summary>
    public ICollection<DepartmentManager> ManagedDepartments { get; set; } = new List<DepartmentManager>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
