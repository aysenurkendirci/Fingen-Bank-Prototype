using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bank.Contracts.Account;
using Bank.Contracts.Auth;
using Bank.Contracts.Notifications;
using Bank.Domain.Security;
using Bank.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SignInSecurityTests
{
    private const string Login = "/api/auth/login";

    private readonly ApiFactory _factory;

    public SignInSecurityTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenAgainstTheRightOne_AndTheCustomerIsTold()
    {
        var registration = TestUsers.NewRegistration();
        var (_, userId) = await _factory.CreateUserAsync(registration);
        var client = _factory.CreateClient();

        var wrong = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            wrong.Add((await client.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, "Wrong#Passw0rd99"))).StatusCode);
        var right = await client.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, registration.Password));

        Assert.All(wrong, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal((HttpStatusCode)423, right.StatusCode);
        Assert.Contains("kilitlendi", await right.Content.ReadAsStringAsync());
        Assert.Contains("Hesabınız geçici olarak kilitlendi", await NotificationTitlesAsync(userId));
    }

    [Fact]
    public async Task ALockThatRanOut_OpensByItself_AndASuccessClearsTheCount()
    {
        var registration = TestUsers.NewRegistration();
        var (_, userId) = await _factory.CreateUserAsync(registration);
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, "Wrong#Passw0rd99"));

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        await connection.ExecuteAsync(
            "UPDATE dbo.UserSecurity SET LockedUntil = @Past WHERE UserId = @UserId",
            new { UserId = userId, Past = ApiFactory.Now.UtcDateTime.AddMinutes(-1) });
        await client.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, "Wrong#Passw0rd99"));
        var signedIn = await client.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, registration.Password));
        var failures = await connection.QuerySingleAsync<int>("SELECT FailedSignIns FROM dbo.UserSecurity WHERE UserId = @UserId", new { UserId = userId });

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(0, failures);
    }

    [Fact]
    public async Task ASignIn_IsAnnouncedToTheCustomer()
    {
        var registration = TestUsers.NewRegistration();
        var (_, userId) = await _factory.CreateUserAsync(registration);

        await _factory.SignInAsync(registration.TcNo, registration.Password);

        Assert.Contains("Hesabınıza giriş yapıldı", await NotificationTitlesAsync(userId));
    }

    [Fact]
    public async Task WithASecondFactor_ThePasswordAloneIsNotEnough_AndACodeWorksOnce()
    {
        var registration = TestUsers.NewRegistration();
        var (client, _) = await _factory.CreateUserAsync(registration);
        var secret = await SwitchOnSecondFactorAsync(client, confirmAtStep: Step - 1);
        var anon = _factory.CreateClient();

        var passwordOnly = await anon.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, registration.Password));
        var withCode = await anon.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, registration.Password, Totp.CodeAt(secret, Step)));
        var replayed = await anon.PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, registration.Password, Totp.CodeAt(secret, Step)));

        Assert.Equal(HttpStatusCode.Unauthorized, passwordOnly.StatusCode);
        Assert.True((await passwordOnly.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secondFactorRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    [Fact]
    public async Task AWrongPassword_DoesNotRevealThatASecondFactorIsOn()
    {
        var registration = TestUsers.NewRegistration();
        var (client, _) = await _factory.CreateUserAsync(registration);
        await SwitchOnSecondFactorAsync(client, confirmAtStep: Step);

        var response = await _factory.CreateClient().PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, "Wrong#Passw0rd99"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(body.TryGetProperty("secondFactorRequired", out _));
    }

    [Fact]
    public async Task TheSecret_IsStoredEncrypted_AndGoesWithErasure()
    {
        var registration = TestUsers.NewRegistration();
        var (client, userId) = await _factory.CreateUserAsync(registration);
        var secret = await SwitchOnSecondFactorAsync(client, confirmAtStep: Step);

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        var stored = await connection.QuerySingleAsync<byte[]>("SELECT TotpSecret FROM dbo.UserSecurity WHERE UserId = @UserId", new { UserId = userId });
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me")
        {
            Content = JsonContent.Create(new EraseAccountRequest(registration.Password)),
        });
        var left = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserSecurity WHERE UserId = @UserId", new { UserId = userId });

        Assert.NotEqual(secret, stored);
        Assert.False(stored.AsSpan().IndexOf(secret) >= 0);
        Assert.Equal(0, left);
    }

    [Fact]
    public async Task SwitchingTheFactorOff_NeedsThePasswordAndACurrentCode()
    {
        var registration = TestUsers.NewRegistration();
        var (client, _) = await _factory.CreateUserAsync(registration);
        var secret = await SwitchOnSecondFactorAsync(client, confirmAtStep: Step - 1);

        var withoutCode = await Disable(client, registration.Password, "000000");
        var wrongPassword = await Disable(client, "Wrong#Passw0rd99", Totp.CodeAt(secret, Step));
        var right = await Disable(client, registration.Password, Totp.CodeAt(secret, Step));

        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.False((await right.Content.ReadFromJsonAsync<TwoFactorStatusResponse>())!.Enabled);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().PostAsJsonAsync(Login, new LoginRequest(registration.TcNo, registration.Password))).StatusCode);
    }

    [Fact]
    public async Task AWrongCodeWhileSettingUp_DoesNotSwitchTheFactorOn()
    {
        var (client, _) = await _factory.CreateUserAsync();
        await client.PostAsync("/api/me/two-factor/setup", null);

        var confirm = await client.PostAsJsonAsync("/api/me/two-factor/confirm", new ConfirmTwoFactorRequest("123456"));
        var status = await client.GetFromJsonAsync<TwoFactorStatusResponse>("/api/me/two-factor");

        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
        Assert.False(status!.Enabled);
    }

    private static long Step => Totp.StepAt(ApiFactory.Now);

    private static async Task<byte[]> SwitchOnSecondFactorAsync(HttpClient client, long confirmAtStep)
    {
        var setup = await (await client.PostAsync("/api/me/two-factor/setup", null)).Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secret = Base32Decode(setup!.Secret);
        var confirmed = await client.PostAsJsonAsync("/api/me/two-factor/confirm", new ConfirmTwoFactorRequest(Totp.CodeAt(secret, confirmAtStep)));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return secret;
    }

    private static Task<HttpResponseMessage> Disable(HttpClient client, string password, string code)
        => client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me/two-factor")
        {
            Content = JsonContent.Create(new DisableTwoFactorRequest(password, code)),
        });

    private static async Task<List<string>> NotificationTitlesAsync(long userId)
    {
        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        return (await connection.QueryAsync<string>("SELECT Title FROM dbo.Notifications WHERE UserId = @UserId", new { UserId = userId })).ToList();
    }

    private static byte[] Base32Decode(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return bytes.ToArray();
    }
}
