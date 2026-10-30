using System.Security.Cryptography;

namespace TaskFlow.Api.Integrations.Security;

/// <summary>RFC 6238 time-based one-time passwords as authenticator apps use them: HMAC-SHA1, 6 digits, 30-second
/// step. Implemented here (a few lines) so no dependency is needed.</summary>
public static class Totp
{
    public const int Digits = 6;
    public const int StepSeconds = 30;
    public const int SecretBytes = 20;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static long StepFor(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>The code for one time step (RFC 4226 dynamic truncation of the HMAC).</summary>
    public static string CodeFor(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);

        var hash = HMACSHA1.HashData(secret, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>Checks a submitted code against the steps from one before to one after <paramref name="now"/>, accepting
    /// only a step later than <paramref name="afterStep"/> so a code can never be used twice. Compared in constant time
    /// across every candidate step.</summary>
    public static bool TryValidate(byte[] secret, string? code, DateTimeOffset now, long afterStep, out long matchedStep)
    {
        matchedStep = 0;
        if (code is null || code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        var submitted = System.Text.Encoding.ASCII.GetBytes(code);
        var current = StepFor(now);
        var found = false;

        for (var step = current - 1; step <= current + 1; step++)
        {
            var expected = System.Text.Encoding.ASCII.GetBytes(CodeFor(secret, step));
            var equal = CryptographicOperations.FixedTimeEquals(expected, submitted);
            if (equal && step > afterStep && !found)
            {
                found = true;
                matchedStep = step;
            }
        }

        return found;
    }

    /// <summary>The otpauth URI an authenticator app reads from the QR code.</summary>
    public static string ProvisioningUri(string issuer, string account, byte[] secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
        + $"?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}"
        + $"&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}
