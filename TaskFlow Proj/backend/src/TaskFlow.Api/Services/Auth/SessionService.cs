using Microsoft.Extensions.Options;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Config;
using TaskFlow.Api.Dtos.Auth;
using TaskFlow.Api.Repositories.Interfaces.Auth;
using TaskFlow.Api.Services.Interfaces.Auth;

namespace TaskFlow.Api.Services.Auth;

public class SessionService : ISessionService
{
    private readonly IAccountRepository _accounts;
    private readonly MfaOptions _mfaOptions;

    public SessionService(IAccountRepository accounts, IOptions<MfaOptions> mfaOptions)
    {
        _accounts = accounts;
        _mfaOptions = mfaOptions.Value;
    }

    public async Task<SessionState?> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _accounts.GetSessionAsync(userId, cancellationToken);
        return user is null
            ? null
            : new SessionState(
                user.SecurityStamp,
                user.IsActive,
                _mfaOptions.Required && user.MfaEnabledAt is null,
                user.RoleName,
                EffectivePermissions.Expand(user.PermissionKeys).ToList());
    }
}
