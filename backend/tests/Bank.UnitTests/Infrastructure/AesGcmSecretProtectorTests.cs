using System.Security.Cryptography;
using Bank.Infrastructure.Security;

namespace Bank.UnitTests.Infrastructure;

public sealed class AesGcmSecretProtectorTests
{
    private const string Key = "unit-tests-encryption-key-0123456789abcdef";
    private static readonly byte[] Secret = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];

    [Fact]
    public void ASecret_ComesBackAsItWent_AndIsNotStoredInTheClear()
    {
        var protector = new AesGcmSecretProtector(Key);

        var stored = protector.Protect(Secret);

        Assert.Equal(Secret, protector.Unprotect(stored));
        Assert.True(stored.AsSpan().IndexOf(Secret) < 0);
    }

    [Fact]
    public void TheSameSecret_IsStoredDifferentlyEachTime()
    {
        var protector = new AesGcmSecretProtector(Key);

        Assert.NotEqual(protector.Protect(Secret), protector.Protect(Secret));
    }

    [Fact]
    public void AChangedByte_IsCaught()
    {
        var protector = new AesGcmSecretProtector(Key);
        var stored = protector.Protect(Secret);
        stored[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(stored));
    }

    [Fact]
    public void AnotherKey_CannotOpenIt()
    {
        var stored = new AesGcmSecretProtector(Key).Protect(Secret);

        Assert.ThrowsAny<CryptographicException>(() => new AesGcmSecretProtector(Key + "-other").Unprotect(stored));
    }

    [Fact]
    public void AKeyShorterThan32Bytes_IsRefused()
        => Assert.Throws<InvalidOperationException>(() => new AesGcmSecretProtector("too-short"));
}
