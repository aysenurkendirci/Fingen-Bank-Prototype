using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bank.Contracts.Account;
using Bank.Contracts.Savings;
using Bank.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bank.IntegrationTests;

/// <summary>Goals as wishes: what is wanted, in the customer's words, with a photo if they like.</summary>
[Collection(ApiCollection.Name)]
public sealed class WishListTests
{
    private const string Url = "/api/goals";

    // The smallest PNG there is: a 1x1 transparent pixel.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly ApiFactory _factory;

    public WishListTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AWish_KeepsWhatKindItIsAndTheDetails()
    {
        var (client, _) = await _factory.CreateUserAsync();
        var request = new GoalRequest("Araba", 900_000m, new DateOnly(2029, 12, 31), 2027, "car", "  Elektrikli, beyaz, otomatik vites  ");

        var created = await (await client.PostAsJsonAsync(Url, request)).Content.ReadFromJsonAsync<GoalResponse>();
        var updated = await (await client.PutAsJsonAsync($"{Url}/{created!.Id}", request with { Details = "" }))
            .Content.ReadFromJsonAsync<GoalResponse>();

        Assert.Equal("car", created.Category);
        Assert.Equal("Elektrikli, beyaz, otomatik vites", created.Details);
        Assert.Null(created.PhotoUpdatedAt);
        Assert.Null(updated!.Details);
    }

    [Fact]
    public async Task AnUnknownKindOfWish_Returns400()
    {
        var (client, _) = await _factory.CreateUserAsync();

        var response = await client.PostAsJsonAsync(Url, new GoalRequest("Yat", 1_000m, null, null, "yacht"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task APhoto_IsStored_AndServedAsAnImageOnly()
    {
        var (client, _) = await _factory.CreateUserAsync();
        var goal = await CreateAsync(client);

        var put = await PutPhotoAsync(client, goal.Id, Png, "araba.png");
        var listed = (await client.GetFromJsonAsync<List<GoalResponse>>(Url))!.Single();
        var get = await client.GetAsync($"{Url}/{goal.Id}/photo");

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.NotNull(listed.PhotoUpdatedAt);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("default-src 'none'", get.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", get.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Png, await get.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task SomethingThatIsNotAPhoto_IsRefused_WhateverItIsCalled()
    {
        var (client, _) = await _factory.CreateUserAsync();
        var goal = await CreateAsync(client);
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

        var response = await PutPhotoAsync(client, goal.Id, svg, "araba.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{goal.Id}/photo")).StatusCode);
    }

    [Fact]
    public async Task APhotoOverTwoMegabytes_IsRefused()
    {
        var (client, _) = await _factory.CreateUserAsync();
        var goal = await CreateAsync(client);
        var big = new byte[2 * 1024 * 1024 + 1];
        Png.CopyTo(big, 0);

        var response = await PutPhotoAsync(client, goal.Id, big, "buyuk.png");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task AnotherCustomersPhoto_IsNotFound_AndCannotBeReplaced()
    {
        var (owner, _) = await _factory.CreateUserAsync();
        var (stranger, _) = await _factory.CreateUserAsync();
        var goal = await CreateAsync(owner);
        await PutPhotoAsync(owner, goal.Id, Png, "araba.png");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Url}/{goal.Id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutPhotoAsync(stranger, goal.Id, Png, "baska.png")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"{Url}/{goal.Id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"{Url}/{goal.Id}/photo")).StatusCode);
    }

    [Fact]
    public async Task APhoto_GoesWhenRemoved_AndWhenTheGoalCloses()
    {
        var (client, _) = await _factory.CreateUserAsync();
        var removed = await CreateAsync(client);
        var closed = await CreateAsync(client);
        await PutPhotoAsync(client, removed.Id, Png, "a.png");
        await PutPhotoAsync(client, closed.Id, Png, "b.png");

        var delete = await client.DeleteAsync($"{Url}/{removed.Id}/photo");
        await client.DeleteAsync($"{Url}/{closed.Id}");

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        var left = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM savings.GoalPhotos WHERE GoalId IN @Ids", new { Ids = new[] { removed.Id, closed.Id } });

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, left);
    }

    [Fact]
    public async Task Photos_AreInTheDataExport_AndGoWithErasure()
    {
        var registration = TestUsers.NewRegistration();
        var (client, userId) = await _factory.CreateUserAsync(registration);
        var goal = await CreateAsync(client);
        await PutPhotoAsync(client, goal.Id, Png, "araba.png");

        var export = await client.GetFromJsonAsync<PersonalDataExport>("/api/me/data-export");
        var erased = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me")
        {
            Content = JsonContent.Create(new EraseAccountRequest(registration.Password)),
        });

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        var left = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM savings.GoalPhotos WHERE GoalId = @Id", new { goal.Id });

        var photo = Assert.Single(export!.GoalPhotos);
        Assert.Equal(Png, Convert.FromBase64String(photo.Base64));
        Assert.Equal("car", export.SavingsGoals.Single().Category);
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Equal(0, left);
    }

    private static async Task<GoalResponse> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(Url, new GoalRequest("Araba", 900_000m, null, 2027, "car", "Elektrikli"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalResponse>())!;
    }

    private static Task<HttpResponseMessage> PutPhotoAsync(HttpClient client, long goalId, byte[] content, string fileName)
    {
        var file = new ByteArrayContent(content);
        // The claimed type is a lie on purpose in some tests; the server must look at the bytes.
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "photo", fileName } };
        return client.PutAsync($"{Url}/{goalId}/photo", form);
    }
}
