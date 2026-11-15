using System.Net;
using System.Net.Http.Json;
using Bank.Contracts.Account;
using Bank.Contracts.Notifications;
using Bank.Contracts.Payments;
using Bank.Contracts.Splits;
using Bank.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SplitTests
{
    private const string Url = "/api/splits";

    // Every test user is "Test Kullanıcı"; the name is typed differently on purpose, as in a transfer.
    private const string FriendName = "test kullanici";

    private static readonly DateOnly SpentOn = DateOnly.FromDateTime(ApiFactory.Now.UtcDateTime).AddDays(-1);

    private readonly ApiFactory _factory;

    public SplitTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AFriendWhoBanksHere_IsAsked_PaysWithATransfer_AndThePayerIsTold()
    {
        var payer = await PersonAsync(0m);
        var friend = await PersonAsync(1_000m);

        var split = await CreateAsync(payer, new ShareRequest(FriendName, friend.Iban, 200m), new ShareRequest("Can", null, 100m));
        var request = Assert.Single((await friend.Client.GetFromJsonAsync<List<ShareRequestResponse>>($"{Url}/requests"))!);
        var friendWasTold = (await FeedAsync(friend.Client)).Items.Single();

        var transfer = await TransferAsync(friend, request.ReceivingIban, request.Amount);
        var settled = await friend.Client.PostAsJsonAsync($"{Url}/requests/{request.ShareId}/settle", new SettleShareRequest(transfer.Id));
        var after = (await payer.Client.GetFromJsonAsync<List<SplitResponse>>(Url))!.Single();
        var payerFeed = await FeedAsync(payer.Client);

        Assert.Equal(200m, split.OwnShare);
        Assert.Equal(200m, request.Amount);
        Assert.Equal("Test Kullanıcı", request.OwnerName);
        Assert.Equal("split", friendWasTold.Kind);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        Assert.Equal(200m, after.Collected);
        Assert.Equal(["Paid", "Open"], after.Shares.Select(s => s.Status));
        Assert.Equal([true, false], after.Shares.Select(s => s.InApp));
        Assert.Contains(payerFeed.Items, n => n.Kind == "split" && n.Title == "Payını ödedi");
        Assert.Contains(payerFeed.Items, n => n.Kind == "money-in");
    }

    [Fact]
    public async Task AFriendsName_MustMatchTheIbanHolder()
    {
        var payer = await PersonAsync(0m);
        var friend = await PersonAsync(0m);

        var response = await payer.Client.PostAsJsonAsync(Url, Split(new ShareRequest("Başka Biri", friend.Iban, 100m)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await FeedAsync(friend.Client)).Items);
    }

    [Fact]
    public async Task ThePayer_CannotAskThemselves()
    {
        var payer = await PersonAsync(0m);

        var response = await payer.Client.PostAsJsonAsync(Url, Split(new ShareRequest(FriendName, payer.Iban, 100m)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SharesOverTheTotal_AreRefused()
    {
        var payer = await PersonAsync(0m);

        var response = await payer.Client.PostAsJsonAsync(Url, Split(new ShareRequest("Can", null, 300m), new ShareRequest("Ece", null, 300m)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ATransferThatDoesNotMatchTheShare_DoesNotSettleIt_NorDoesOneUsedTwice()
    {
        var payer = await PersonAsync(0m);
        var friend = await PersonAsync(1_000m);
        await CreateAsync(payer, new ShareRequest(FriendName, friend.Iban, 100m));
        await CreateAsync(payer, new ShareRequest(FriendName, friend.Iban, 100m));
        var requests = (await friend.Client.GetFromJsonAsync<List<ShareRequestResponse>>($"{Url}/requests"))!;

        var tooLittle = await TransferAsync(friend, payer.Iban, 99m);
        var refused = await friend.Client.PostAsJsonAsync($"{Url}/requests/{requests[0].ShareId}/settle", new SettleShareRequest(tooLittle.Id));

        var right = await TransferAsync(friend, payer.Iban, 100m);
        var first = await friend.Client.PostAsJsonAsync($"{Url}/requests/{requests[0].ShareId}/settle", new SettleShareRequest(right.Id));
        var reused = await friend.Client.PostAsJsonAsync($"{Url}/requests/{requests[1].ShareId}/settle", new SettleShareRequest(right.Id));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
    }

    [Fact]
    public async Task SomeoneElsesRequest_OrTransfer_CannotBeUsed()
    {
        var payer = await PersonAsync(0m);
        var friend = await PersonAsync(1_000m);
        var stranger = await PersonAsync(1_000m);
        await CreateAsync(payer, new ShareRequest(FriendName, friend.Iban, 100m));
        var request = (await friend.Client.GetFromJsonAsync<List<ShareRequestResponse>>($"{Url}/requests"))!.Single();
        var strangersTransfer = await TransferAsync(stranger, payer.Iban, 100m);

        var byStranger = await stranger.Client.PostAsync($"{Url}/requests/{request.ShareId}/decline", null);
        var withStrangersTransfer = await friend.Client.PostAsJsonAsync(
            $"{Url}/requests/{request.ShareId}/settle", new SettleShareRequest(strangersTransfer.Id));

        Assert.Equal(HttpStatusCode.NotFound, byStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withStrangersTransfer.StatusCode);
        Assert.Empty((await stranger.Client.GetFromJsonAsync<List<SplitResponse>>(Url))!);
    }

    [Fact]
    public async Task ADeclinedShare_IsTold_AndCannotBeSettledAfterwards()
    {
        var payer = await PersonAsync(0m);
        var friend = await PersonAsync(1_000m);
        await CreateAsync(payer, new ShareRequest(FriendName, friend.Iban, 100m));
        var request = (await friend.Client.GetFromJsonAsync<List<ShareRequestResponse>>($"{Url}/requests"))!.Single();

        var declined = await friend.Client.PostAsync($"{Url}/requests/{request.ShareId}/decline", null);
        var transfer = await TransferAsync(friend, payer.Iban, 100m);
        var late = await friend.Client.PostAsJsonAsync($"{Url}/requests/{request.ShareId}/settle", new SettleShareRequest(transfer.Id));

        Assert.Equal("Declined", (await declined.Content.ReadFromJsonAsync<ShareRequestResponse>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains((await FeedAsync(payer.Client)).Items, n => n.Title == "Pay isteği reddedildi");
    }

    [Fact]
    public async Task CashIsMarkedByThePayer_AndCancellingWithdrawsOnlyWhatIsOpen()
    {
        var payer = await PersonAsync(0m);
        var friend = await PersonAsync(0m);
        var split = await CreateAsync(payer, new ShareRequest("Can", null, 100m), new ShareRequest(FriendName, friend.Iban, 100m));

        var cash = await payer.Client.PostAsync($"{Url}/{split.Id}/shares/{split.Shares[0].Id}/paid", null);
        var cancelled = await payer.Client.DeleteAsync($"{Url}/{split.Id}");
        var after = (await payer.Client.GetFromJsonAsync<List<SplitResponse>>(Url))!.Single();

        Assert.Equal(HttpStatusCode.OK, cash.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal("Cancelled", after.Status);
        Assert.Equal(["Paid", "Cancelled"], after.Shares.Select(s => s.Status));
        Assert.Contains((await FeedAsync(friend.Client)).Items, n => n.Title == "Pay isteği geri çekildi");
    }

    [Fact]
    public async Task Splits_AreInTheDataExport_AndGoWithErasure_WhileFriendsKeepTheirSide()
    {
        var registration = TestUsers.NewRegistration();
        var (payerClient, payerId) = await _factory.CreateUserAsync(registration);
        var (payerAccount, _) = await TestData.AddAccountAsync(payerId, 0m);
        var friend = await PersonAsync(0m);
        var other = await PersonAsync(0m);
        await CreateAsync(new Person(payerClient, payerAccount, ""), new ShareRequest("Can", null, 50m));
        // A split someone else made, asking the soon-erased customer for a share.
        await CreateAsync(other, new ShareRequest(FriendName, await IbanOfAsync(payerAccount), 30m));

        var export = await payerClient.GetFromJsonAsync<PersonalDataExport>("/api/me/data-export");
        var erased = await payerClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me")
        {
            Content = JsonContent.Create(new EraseAccountRequest(registration.Password)),
        });

        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        var owned = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM splits.Splits WHERE OwnerUserId = @Id", new { Id = payerId });
        var linked = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM splits.Shares WHERE DebtorUserId = @Id", new { Id = payerId });

        Assert.Equal("Market", Assert.Single(export!.Splits).Title);
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Equal(0, owned);
        Assert.Equal(0, linked);
        Assert.Single((await other.Client.GetFromJsonAsync<List<SplitResponse>>(Url))!.Single().Shares);
        Assert.Empty((await FeedAsync(friend.Client)).Items);
    }

    private sealed record Person(HttpClient Client, long AccountId, string Iban);

    private async Task<Person> PersonAsync(decimal balance)
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, iban) = await TestData.AddAccountAsync(userId, balance);
        return new Person(client, accountId, iban);
    }

    private static async Task<string> IbanOfAsync(long accountId)
    {
        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        return await connection.QuerySingleAsync<string>("SELECT Iban FROM dbo.Accounts WHERE AccountId = @Id", new { Id = accountId });
    }

    private static SplitRequest Split(params ShareRequest[] shares)
        => new("Market", 500m, SpentOn, null, shares);

    private static async Task<SplitResponse> CreateAsync(Person payer, params ShareRequest[] shares)
    {
        var response = await payer.Client.PostAsJsonAsync(Url, Split(shares) with { ReceivingAccountId = payer.AccountId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SplitResponse>())!;
    }

    private static async Task<TransferResponse> TransferAsync(Person from, string targetIban, decimal amount)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(from.AccountId, targetIban, "TEST KULLANICI", amount, "Market payı")),
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await from.Client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransferResponse>())!;
    }

    private static async Task<NotificationFeedResponse> FeedAsync(HttpClient client)
        => (await client.GetFromJsonAsync<NotificationFeedResponse>("/api/notifications"))!;
}
