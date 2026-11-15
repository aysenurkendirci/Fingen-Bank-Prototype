using System.Text;
using Bank.Application.Features.Savings;

namespace Bank.UnitTests.Savings;

public sealed class GoalPhotoRulesTests
{
    [Fact]
    public void AJpeg_IsRecognisedByItsFirstBytes()
        => Assert.Equal("image/jpeg", GoalPhotoRules.DetectType([0xFF, 0xD8, 0xFF, 0xE0, 0x00]));

    [Fact]
    public void APng_IsRecognisedByItsSignature()
        => Assert.Equal("image/png", GoalPhotoRules.DetectType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));

    [Fact]
    public void AWebP_NeedsBothRiffAndWebp()
    {
        Assert.Equal("image/webp", GoalPhotoRules.DetectType(Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 ")));
        Assert.Null(GoalPhotoRules.DetectType(Encoding.ASCII.GetBytes("RIFF\0\0\0\0WAVEfmt ")));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<html><body>photo.jpg</body></html>")]
    [InlineData("GIF89a")]
    [InlineData("")]
    public void AnythingElse_IsNotAPhoto(string content)
        => Assert.Null(GoalPhotoRules.DetectType(Encoding.UTF8.GetBytes(content)));
}
