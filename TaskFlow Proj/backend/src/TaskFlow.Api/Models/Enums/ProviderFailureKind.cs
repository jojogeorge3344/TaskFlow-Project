namespace TaskFlow.Api.Models.Enums;

public enum ProviderFailureKind
{
    RateLimited,
    InvalidKey,
    Timeout,
    Unreachable,
    ModelUnavailable,
    Unexpected
}
