using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class User
{
    public int Id { get; set; }

    public string? EmployeeCode { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public Role Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsDeleted { get; set; }

    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
