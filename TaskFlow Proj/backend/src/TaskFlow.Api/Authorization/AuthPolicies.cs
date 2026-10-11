namespace TaskFlow.Api.Authorization;

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";

    public const string ManagerOrAdmin = "ManagerOrAdmin";
}
