using System.Security.Cryptography;
using System.Text;
using Bank.Application.Abstractions.Security;

namespace Bank.Infrastructure.Security;

/// <summary>
/// AES-256-GCM with a key derived (HKDF) from the configured "Security:EncryptionKey". Each value gets a fresh
/// nonce and carries its own tag, so a changed or swapped value fails to open instead of opening wrong. The
/// stored form is a version byte, the nonce, the tag and the ciphertext.
/// </summary>
/// <remarks>
/// The key lives in user-secrets or the environment, never next to the data: a copy of the database alone does
/// not reveal anyone's second factor. To rotate it, move the old key to Security:PreviousEncryptionKeys and set a
/// new one: data written with the old key still opens, and is written again with the new key the next time it is
/// used. Once every secret has moved, the old key can be dropped.
/// </remarks>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;
    private readonly byte[][] _previousKeys;

    public AesGcmSecretProtector(string configuredKey, IEnumerable<string>? previousKeys = null)
    {
        _key = Derive(configuredKey, "Security:EncryptionKey");
        _previousKeys = (previousKeys ?? []).Select(k => Derive(k, "Security:PreviousEncryptionKeys")).ToArray();
    }

    private static byte[] Derive(string configuredKey, string setting)
    {
        if (Encoding.UTF8.GetByteCount(configuredKey) < 32)
            throw new InvalidOperationException($"{setting} must be at least 32 bytes.");

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(configuredKey), 32,
            info: "FinGen secret protection v1"u8.ToArray());
    }

    public byte[] Protect(byte[] plaintext)
    {
        var output = new byte[1 + NonceSize + TagSize + plaintext.Length];
        output[0] = Version;
        var nonce = output.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, output.AsSpan(1 + NonceSize + TagSize), output.AsSpan(1 + NonceSize, TagSize));
        return output;
    }

    public byte[] Unprotect(byte[] protectedData, out bool writtenWithOldKey)
    {
        if (protectedData.Length < 1 + NonceSize + TagSize || protectedData[0] != Version)
            throw new CryptographicException("Unknown protected data format.");

        // The tag is checked with every key, so a wrong key fails cleanly rather than returning noise.
        writtenWithOldKey = false;
        if (TryDecrypt(_key, protectedData, out var plaintext))
            return plaintext;

        foreach (var previous in _previousKeys)
        {
            if (TryDecrypt(previous, protectedData, out plaintext))
            {
                writtenWithOldKey = true;
                return plaintext;
            }
        }

        throw new CryptographicException("No configured key opens the protected data.");
    }

    private static bool TryDecrypt(byte[] key, byte[] protectedData, out byte[] plaintext)
    {
        plaintext = new byte[protectedData.Length - 1 - NonceSize - TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                protectedData.AsSpan(1, NonceSize),
                protectedData.AsSpan(1 + NonceSize + TagSize),
                protectedData.AsSpan(1 + NonceSize, TagSize),
                plaintext);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }
}
