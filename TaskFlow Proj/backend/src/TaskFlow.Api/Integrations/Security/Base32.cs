namespace TaskFlow.Api.Integrations.Security;

/// <summary>RFC 4648 Base32 (upper-case, no padding), the encoding authenticator apps expect for a shared key.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var result = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    /// <summary>Decodes Base32, ignoring spaces, dashes and padding and accepting lower case. Returns null for any
    /// other character.</summary>
    public static byte[]? Decode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var bytes = new List<byte>(text.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;

        foreach (var raw in text)
        {
            if (raw is ' ' or '-' or '=')
            {
                continue;
            }

            var index = Alphabet.IndexOf(char.ToUpperInvariant(raw));
            if (index < 0)
            {
                return null;
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
