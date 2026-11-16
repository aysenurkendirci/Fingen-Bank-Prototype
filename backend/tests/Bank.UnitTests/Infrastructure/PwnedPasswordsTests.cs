using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bank.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Infrastructure;

public sealed class PwnedPasswordsTests
{
    private const string Password = "Breached#Passw0rd1";
    private static readonly string Hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Password)));

    [Fact]
    public async Task OnlyTheFirstFiveCharactersOfTheHashLeave_AndPaddingIsAskedFor()
    {
        var handler = new FakeRange(HttpStatusCode.OK, "");

        await Check(handler);

        Assert.Equal($"/range/{Hash[..5]}", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("true", handler.Request.Headers.GetValues("Add-Padding").Single());
        Assert.DoesNotContain(Hash[5..], handler.Request.RequestUri.ToString());
    }

    [Fact]
    public async Task APasswordInTheList_IsBreached()
        => Assert.True(await Check(new FakeRange(HttpStatusCode.OK, $"0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n{Hash[5..]}:42\r\n")));

    [Fact]
    public async Task APaddingLine_IsNotABreach()
        => Assert.False(await Check(new FakeRange(HttpStatusCode.OK, $"{Hash[5..]}:0\r\n")));

    [Fact]
    public async Task APasswordNotInTheList_IsNotBreached()
        => Assert.False(await Check(new FakeRange(HttpStatusCode.OK, "0018A45C4D1DEF81644B54AB7F969B88D65:3\r\n")));

    [Fact]
    public async Task AnUnavailableList_AnswersUnknown()
        => Assert.Null(await Check(new FakeRange(HttpStatusCode.ServiceUnavailable, "")));

    private static Task<bool?> Check(FakeRange handler)
        => new PwnedPasswords(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, NullLogger<PwnedPasswords>.Instance)
            .IsBreachedAsync(Password);

    private sealed class FakeRange(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
