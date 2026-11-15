using System.Net;
using System.Net.Http.Json;
using Bank.Contracts.Account;
using Bank.Contracts.Auth;
using Bank.Contracts.Notifications;
using Bank.Contracts.Payments;
using Bank.Infrastructure.Housekeeping;
using Bank.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class NotificationTests
{
    private const string NewPassword = "NewP@ssw0rd2026";

    private readonly ApiFactory _factory;

    public NotificationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ATransferToSomeoneElse_TellsBothSides()
    {
        var (sender, source, _) = await UserWithAccountAsync(1_000m);
        var (receiver, _, targetIban) = await UserWithAccountAsync(0m);

        var sent = await TransferAsync(sender, source, targetIban, 250.50m);
        var senderFeed = await FeedAsync(sender);
        var receiverFeed = await FeedAsync(receiver);

        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var outgoing = Assert.Single(senderFeed.Items);
        Assert.Equal("money-out", outgoing.Kind);
        Assert.Contains("250,50 TL", outgoing.Body);
        var incoming = Assert.Single(receiverFeed.Items);
        Assert.Equal("money-in", incoming.Kind);
        Assert.Contains("Test Kullanıcı gönderdi: 250,50 TL", incoming.Body);
        Assert.EndsWith(targetIban[^4..], incoming.Body);
        Assert.Equal(1, receiverFeed.UnreadCount);
    }

    [Fact]
    public async Task ARefusedTransfer_LeavesNoNotification()
    {
        var (sender, source, _) = await UserWithAccountAsync(10m);
        var (receiver, _, targetIban) = await UserWithAccountAsync(0m);

        var refused = await TransferAsync(sender, source, targetIban, 500m);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Empty((await FeedAsync(sender)).Items);
        Assert.Empty((await FeedAsync(receiver)).Items);
    }

    [Fact]
    public async Task Notifications_AreMarkedRead_OneByOneOrAllAtOnce()
    {
        var (client, _) = await _factory.CreateUserAsync();
        await client.PutAsJsonAsync("/api/me/password", new ChangePasswordRequest(TestUsers.Password, NewPassword));
        await client.PutAsJsonAsync("/api/me/password", new ChangePasswordRequest(NewPassword, "Other#Passw0rd2026"));

        var before = await FeedAsync(client);
        var one = await client.PostAsync($"/api/notifications/{before.Items[0].NotificationId}/read", null);
        var afterOne = await FeedAsync(client);
        var all = await client.PostAsync("/api/notifications/read-all", null);
        var afterAll = await FeedAsync(client);

        Assert.Equal(2, before.UnreadCount);
        Assert.All(before.Items, n => Assert.Equal("security", n.Kind));
        Assert.Equal(HttpStatusCode.NoContent, one.StatusCode);
        Assert.Equal(1, afterOne.UnreadCount);
        Assert.Equal(HttpStatusCode.NoContent, all.StatusCode);
        Assert.Equal(0, afterAll.UnreadCount);
        Assert.All(afterAll.Items, n => Assert.NotNull(n.ReadAt));
    }

    [Fact]
    public async Task SomeoneElsesNotification_CannotBeTouched()
    {
        var (owner, _) = await _factory.CreateUserAsync();
        var (stranger, _) = await _factory.CreateUserAsync();
        await owner.PutAsJsonAsync("/api/me/password", new ChangePasswordRequest(TestUsers.Password, NewPassword));
        var id = (await FeedAsync(owner)).Items.Single().NotificationId;

        var response = await stranger.PostAsync($"/api/notifications/{id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await FeedAsync(stranger)).Items);
        Assert.Equal(1, (await FeedAsync(owner)).UnreadCount);
    }

    [Fact]
    public async Task BlockingACard_IsNews_PressingItAgainIsNot()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, _) = await TestData.AddTryAccountAsync(userId, 0m);
        var cardId = await TestData.AddCardAsync(userId, accountId);

        await client.PostAsync($"/api/cards/{cardId}/block", null);
        await client.PostAsync($"/api/cards/{cardId}/block", null);

        var item = Assert.Single((await FeedAsync(client)).Items);
        Assert.Equal("Kartınız geçici olarak kapatıldı", item.Title);
    }

    [Fact]
    public async Task Notifications_AreInTheDataExport_AndGoWithErasure()
    {
        var registration = TestUsers.NewRegistration();
        var (client, userId) = await _factory.CreateUserAsync(registration);
        await client.PutAsJsonAsync("/api/me/password", new ChangePasswordRequest(registration.Password, NewPassword));

        var export = await client.GetFromJsonAsync<PersonalDataExport>("/api/me/data-export");
        var erased = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me")
        {
            Content = JsonContent.Create(new EraseAccountRequest(NewPassword)),
        });

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        var left = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId = @Id", new { Id = userId });

        Assert.Equal("Şifreniz değiştirildi", Assert.Single(export!.Notifications).Title);
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Equal(0, left);
    }

    [Fact]
    public async Task Housekeeping_RemovesLongReadNotifications_NeverUnreadOnes()
    {
        var now = ApiFactory.Now.UtcDateTime;
        var (_, userId) = await _factory.CreateUserAsync();
        await using var connection = new SqlConnection(ApiFactory.ConnectionString);

        const string insert = """
            INSERT INTO dbo.Notifications (UserId, Kind, Title, Body, CreatedAt, ReadAt)
            OUTPUT INSERTED.NotificationId
            VALUES (@UserId, 'security', N'Başlık', N'Metin', @CreatedAt, @ReadAt)
            """;
        var oldRead = await connection.QuerySingleAsync<long>(
            insert, new { UserId = userId, CreatedAt = now.AddDays(-200), ReadAt = (DateTime?)now.AddDays(-120) });
        var oldUnread = await connection.QuerySingleAsync<long>(
            insert, new { UserId = userId, CreatedAt = now.AddDays(-200), ReadAt = (DateTime?)null });
        var recentRead = await connection.QuerySingleAsync<long>(
            insert, new { UserId = userId, CreatedAt = now.AddDays(-10), ReadAt = (DateTime?)now.AddDays(-5) });

        await _factory.Services.GetRequiredService<HousekeepingRunner>().RunOnceAsync(now);

        var left = (await connection.QueryAsync<long>(
            "SELECT NotificationId FROM dbo.Notifications WHERE UserId = @UserId", new { UserId = userId })).ToHashSet();
        Assert.DoesNotContain(oldRead, left);
        Assert.Contains(oldUnread, left);
        Assert.Contains(recentRead, left);
    }

    private async Task<(HttpClient Client, long AccountId, string Iban)> UserWithAccountAsync(decimal balance)
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, iban) = await TestData.AddAccountAsync(userId, balance);
        return (client, accountId, iban);
    }

    private static Task<HttpResponseMessage> TransferAsync(HttpClient client, long source, string targetIban, decimal amount)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(source, targetIban, "TEST KULLANICI", amount, "Kira payı")),
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client.SendAsync(message);
    }

    private static async Task<NotificationFeedResponse> FeedAsync(HttpClient client)
        => (await client.GetFromJsonAsync<NotificationFeedResponse>("/api/notifications"))!;
}
