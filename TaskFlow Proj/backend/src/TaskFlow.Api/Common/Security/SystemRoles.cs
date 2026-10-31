namespace TaskFlow.Api.Common.Security;

/// <summary>Names and ids of the roles the application creates itself. They cannot be renamed, changed or deleted.</summary>
public static class SystemRoles
{
    public const string Administrator = "Administrator";

    public const string Manager = "Manager";

    public const string Employee = "Employee";

    public const int AdministratorId = 1;

    public const int ManagerId = 2;

    public const int EmployeeId = 3;

    public static readonly IReadOnlyList<string> Names = [Administrator, Manager, Employee];

    public static bool IsSystemName(string? name) =>
        Names.Any(systemName => string.Equals(systemName, name, StringComparison.OrdinalIgnoreCase));
}
