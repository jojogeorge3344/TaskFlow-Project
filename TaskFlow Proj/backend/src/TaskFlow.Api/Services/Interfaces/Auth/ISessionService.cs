using TaskFlow.Api.Dtos.Auth;

namespace TaskFlow.Api.Services.Interfaces.Auth;

/// <summary>Reads what validating an access token needs on every request.</summary>
public interface ISessionService
{
    /// <summary>The user's current session facts, or null when the user does not exist.</summary>
    Task<SessionState?> GetAsync(int userId, CancellationToken cancellationToken = default);
}
