using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Bank.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Bank.IntegrationTests;

/// <summary>Changing the signing key must not sign everybody out, and only the keys still listed are accepted.</summary>
[Collection(ApiCollection.Name)]
public sealed class KeyRotationTests
{
    private const string RetiredKey = "integration-tests-signing-key-0000000000000000";
    private const string UnknownKey = "integration-tests-signing-key-ffffffffffffffff";

    private readonly ApiFactory _factory;

    public KeyRotationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ATokenSignedWithThePreviousKey_StillWorks_WhileItIsListed()
    {
        var (_, userId) = await _factory.CreateUserAsync();
        var rotated = _factory.WithWebHostBuilder(b => b.UseSetting("Jwt:PreviousKeys:0", RetiredKey));

        var withRotation = await GetProfileAsync(rotated.CreateClient(), Token(userId, RetiredKey));
        var withoutRotation = await GetProfileAsync(_factory.CreateClient(), Token(userId, RetiredKey));
        var unknown = await GetProfileAsync(rotated.CreateClient(), Token(userId, UnknownKey));

        Assert.Equal(HttpStatusCode.OK, withRotation);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutRotation);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown);
    }

    private static async Task<HttpStatusCode> GetProfileAsync(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync("/api/me")).StatusCode;
    }

    // Shaped like the API's own tokens: the same issuer, audience and claim, and like them timed by the real clock.
    private static string Token(long userId, string key)
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "BankApi",
            audience: "BankClient",
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
}
