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
/// not reveal anyone's second factor. Known limit: there is one key and no rotation yet; the version byte leaves
/// room for a second key to be added.
/// </remarks>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmSecretProtector(string configuredKey)
    {
        if (Encoding.UTF8.GetByteCount(configuredKey) < 32)
            throw new InvalidOperationException("Security:EncryptionKey must be at least 32 bytes.");

        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(configuredKey), 32,
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

    public byte[] Unprotect(byte[] protectedData)
    {
        if (protectedData.Length < 1 + NonceSize + TagSize || protectedData[0] != Version)
            throw new CryptographicException("Unknown protected data format.");

        var plaintext = new byte[protectedData.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            protectedData.AsSpan(1, NonceSize),
            protectedData.AsSpan(1 + NonceSize + TagSize),
            protectedData.AsSpan(1 + NonceSize, TagSize),
            plaintext);
        return plaintext;
    }
}
