using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using TaskFlow.Application.PasswordReset;

namespace TaskFlow.Infrastructure.Services;

/// <summary>The <c>PasswordReset</c> configuration section.</summary>
public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>How many minutes an administrator-generated login link stays valid (it also works only once).</summary>
    public int LinkLifetimeMinutes { get; set; } = 1;
}

/// <summary>Login-link tokens built on the application's persisted Data Protection key ring with their own
/// purpose, so they are encrypted and tamper-evident. The expiry travels inside the encrypted payload and is
/// checked against <see cref="TimeProvider"/>, so a token cannot be extended and expiry is testable.</summary>
public sealed class PasswordResetLinkTokens : IPasswordResetLinkTokens
{
    private const string Purpose = "TaskFlow.PasswordReset.LoginLink.v1";

    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    public PasswordResetLinkTokens(
        IDataProtectionProvider provider,
        IOptions<PasswordResetOptions> options,
        TimeProvider timeProvider)
    {
        _protector = provider.CreateProtector(Purpose);
        _timeProvider = timeProvider;
        _lifetime = TimeSpan.FromMinutes(Math.Max(1, options.Value.LinkLifetimeMinutes));
    }

    public string Create(int userId, int requestId)
    {
        var expires = _timeProvider.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds();
        var payload = string.Create(CultureInfo.InvariantCulture, $"{userId}:{requestId}:{expires}");
        return Base64UrlEncode(_protector.Protect(System.Text.Encoding.UTF8.GetBytes(payload)));
    }

    public (int UserId, int RequestId)? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var payload = System.Text.Encoding.UTF8.GetString(_protector.Unprotect(Base64UrlDecode(token.Trim())));
            var parts = payload.Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var requestId)
                || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                || _timeProvider.GetUtcNow().ToUnixTimeSeconds() >= expires)
            {
                return null;
            }

            return (userId, requestId);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            // Malformed or tampered with: indistinguishable from an expired token to the caller, on purpose.
            return null;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
