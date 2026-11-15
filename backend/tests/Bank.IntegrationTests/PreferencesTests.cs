using System.Net;
using System.Net.Http.Json;
using Bank.Contracts.Account;
using Bank.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PreferencesTests
{
    private readonly ApiFactory _factory;

    public PreferencesTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ANewCustomer_FollowsTheSystemInTheDefaultColour()
    {
        var (client, _) = await _factory.CreateUserAsync();

        var preferences = await client.GetFromJsonAsync<PreferencesResponse>("/api/me/preferences");

        Assert.Equal(PreferencesResponse.Default, preferences);
    }

    [Fact]
    public async Task SavedPreferences_FollowTheCustomerToAnotherSignIn()
    {
        var registration = TestUsers.NewRegistration();
        var (client, _) = await _factory.CreateUserAsync(registration);

        await client.PutAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest("dark", "ocean"));
        var changed = await client.PutAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest("light", "plum"));
        var otherDevice = await _factory.SignInAsync(registration.TcNo, registration.Password);

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(new PreferencesResponse("light", "plum"), await otherDevice.GetFromJsonAsync<PreferencesResponse>("/api/me/preferences"));
    }

    [Fact]
    public async Task TheYoungMode_IsSaved_AndAClientThatDoesNotSendAModeKeepsTheStandardOne()
    {
        var (client, _) = await _factory.CreateUserAsync();

        var young = await client.PutAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest("light", "spruce", "young"));
        var saved = await client.GetFromJsonAsync<PreferencesResponse>("/api/me/preferences");
        var older = await client.PutAsJsonAsync("/api/me/preferences", new { theme = "dark", accent = "plum" });

        Assert.Equal(HttpStatusCode.OK, young.StatusCode);
        Assert.Equal("young", saved!.Mode);
        Assert.Equal("standard", (await older.Content.ReadFromJsonAsync<PreferencesResponse>())!.Mode);
    }

    [Fact]
    public async Task AnUnknownMode_Returns400()
    {
        var (client, _) = await _factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest("light", "spruce", "kids"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("sepia", "spruce")]
    [InlineData("dark", "neon")]
    public async Task AnUnknownValue_Returns400(string theme, string accent)
    {
        var (client, _) = await _factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest(theme, accent));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preferences_AreInTheDataExport_AndGoWithErasure()
    {
        var registration = TestUsers.NewRegistration();
        var (client, userId) = await _factory.CreateUserAsync(registration);
        await client.PutAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest("dark", "graphite"));

        var export = await client.GetFromJsonAsync<PersonalDataExport>("/api/me/data-export");
        var erased = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me")
        {
            Content = JsonContent.Create(new EraseAccountRequest(registration.Password)),
        });

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        var left = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.UserPreferences WHERE UserId = @Id", new { Id = userId });

        Assert.Equal(new PreferencesResponse("dark", "graphite"), export!.Preferences);
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Equal(0, left);
    }
}
