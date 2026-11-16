using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Bank.Domain.Security;

/// <summary>
/// Time-based one-time codes (RFC 6238 over RFC 4226): HMAC-SHA1, 30-second steps, six digits, the settings every
/// authenticator app uses by default.
/// </summary>
public static class Totp
{
    public const int SecretBytes = 20;
    public const int Digits = 6;
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>One step either side is accepted, for a phone clock a little off and a code typed near the edge.</summary>
    public const int Tolerance = 1;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / (long)Step.TotalSeconds;

    public static string CodeAt(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);

        // RFC 4226 dynamic truncation.
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash.Slice(offset, 4)) & 0x7FFFFFFF) % 1_000_000;
        return binary.ToString("D6");
    }

    /// <returns>The step the code belongs to, or null when it matches none within the tolerance.</returns>
    public static long? Verify(byte[] secret, string? code, DateTimeOffset now)
    {
        var typed = (code ?? string.Empty).Replace(" ", string.Empty);
        if (typed.Length != Digits || !typed.All(char.IsAsciiDigit))
            return null;

        var current = StepAt(now);
        for (var step = current - Tolerance; step <= current + Tolerance; step++)
        {
            // Constant time, so how much of a code was right cannot be measured.
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeAt(secret, step)), Encoding.ASCII.GetBytes(typed)))
                return step;
        }
        return null;
    }

    /// <summary>The address an authenticator app reads from a QR code or a link.</summary>
    public static string SetupUri(byte[] secret, string issuer, string account)
        => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
           $"?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={(int)Step.TotalSeconds}";
}

/// <summary>RFC 4648 Base32 without padding, the form authenticator apps take a secret in.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }
}
