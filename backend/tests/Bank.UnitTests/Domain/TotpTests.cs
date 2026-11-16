using System.Text;
using Bank.Domain.Security;

namespace Bank.UnitTests.Domain;

public sealed class TotpTests
{
    // RFC 6238, appendix B: the SHA-1 secret is the ASCII string "12345678901234567890".
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void Codes_MatchTheRfcTestVectors(long unixSeconds, string expected)
        => Assert.Equal(expected, Totp.CodeAt(RfcSecret, Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))));

    [Fact]
    public void ACodeFromTheStepBeforeOrAfter_IsAccepted_OneFurtherIsNot()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var step = Totp.StepAt(now);

        Assert.Equal(step - 1, Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step - 1), now));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step + 1), now));
        Assert.Null(Totp.Verify(RfcSecret, Totp.CodeAt(RfcSecret, step - 2), now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12345a")]
    [InlineData("1234567")]
    public void SomethingThatIsNotASixDigitCode_IsRefused(string? code)
        => Assert.Null(Totp.Verify(RfcSecret, code, DateTimeOffset.FromUnixTimeSeconds(59)));

    [Fact]
    public void ACodeTypedWithASpace_IsAccepted()
        => Assert.NotNull(Totp.Verify(RfcSecret, "287 082", DateTimeOffset.FromUnixTimeSeconds(59)));

    [Fact]
    public void Base32_MatchesRfc4648()
    {
        Assert.Equal("MZXW6YTBOI", Base32.Encode("foobar"u8));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Encode(RfcSecret));
    }

    [Fact]
    public void TheSetupAddress_CarriesTheSecretAndTheIssuer()
    {
        var uri = Totp.SetupUri(RfcSecret, "FinGen", "123*****901");

        Assert.StartsWith("otpauth://totp/FinGen:123%2A%2A%2A%2A%2A901?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", uri);
        Assert.Contains("issuer=FinGen", uri);
    }
}
