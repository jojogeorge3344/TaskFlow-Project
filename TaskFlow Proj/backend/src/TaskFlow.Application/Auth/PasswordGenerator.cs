using System.Security.Cryptography;

namespace TaskFlow.Application.Auth;

/// <summary>Generates a crypto-random password for an administrator-initiated reset. The alphabet omits look-alike
/// characters (0/O, 1/l/I) so the password can be typed from the email that carries it.</summary>
public static class PasswordGenerator
{
    public const int Length = 16;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string Generate()
    {
        var result = new char[Length];
        for (var index = 0; index < Length; index++)
        {
            result[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(result);
    }
}
