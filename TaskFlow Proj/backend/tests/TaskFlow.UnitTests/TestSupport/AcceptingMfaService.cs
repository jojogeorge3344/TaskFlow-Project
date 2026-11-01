using TaskFlow.Api.Dtos.Auth.Mfa;
using User = TaskFlow.Api.Models.Entities.User;
using TaskFlow.Api.Services.Auth.Mfa;

namespace TaskFlow.UnitTests.TestSupport;

/// <summary>An MFA service that accepts every step-up code, for tests of what happens after the code check.</summary>
internal sealed class AcceptingMfaService : IMfaService
{
    public System.Threading.Tasks.Task RequireCodeAsync(int userId, string? code, CancellationToken cancellationToken = default) =>
        System.Threading.Tasks.Task.CompletedTask;

    public Task<MfaVerifyOutcome> CheckCodeAsync(int userId, string? code, CancellationToken cancellationToken = default) =>
        Task.FromResult(MfaVerifyOutcome.Success);

    public Task<MfaVerifyOutcome> VerifyAsync(User user, string? code, bool allowBackupCode, bool countFailure = true, CancellationToken cancellationToken = default) =>
        Task.FromResult(MfaVerifyOutcome.Success);

    public Task<MfaSetupResult?> BeginSetupAsync(int userId, string currentPassword, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<string>?> EnableAsync(int userId, string code, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<string>?> RegenerateBackupCodesAsync(int userId, string currentPassword, string code, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> ResetAsync(int userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
