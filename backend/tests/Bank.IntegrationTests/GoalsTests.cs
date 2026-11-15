using System.Net;
using System.Net.Http.Json;
using Bank.Contracts.Notifications;
using Bank.Contracts.Payments;
using Bank.Contracts.Savings;
using Bank.IntegrationTests.Infrastructure;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class GoalsTests
{
    private const string Url = "/api/goals";

    private readonly ApiFactory _factory;

    public GoalsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ANewGoal_GetsAnEmptySavingsAccountOfItsOwn()
    {
        var (client, _) = await _factory.CreateUserAsync();

        var goal = await CreateAsync(client, new GoalRequest("Araba", 400_000m, new DateOnly(2027, 12, 31), null));
        var accounts = await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts");

        Assert.Equal(0m, goal.SavedAmount);
        Assert.Contains(accounts!, a => a.Iban == goal.Iban && a.AccountType == "Savings");
        Assert.NotNull(goal.MonthlyNeeded);
    }

    [Fact]
    public async Task MoneyMovesInAndOut_ThroughTheLedger_AndARetryMovesItOnce()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, _) = await TestData.AddAccountAsync(userId, 5_000m);
        var goal = await CreateAsync(client, new GoalRequest("Tatil", 10_000m, null, null));

        await MoveAsync(client, goal.Id, "deposits", accountId, 1_000m, "same-key");
        var repeated = await MoveAsync(client, goal.Id, "deposits", accountId, 1_000m, "same-key");
        var afterWithdrawal = await MoveAsync(client, goal.Id, "withdrawals", accountId, 300m, "out-1");

        Assert.Equal(1_000m, repeated.SavedAmount);
        Assert.Equal(700m, afterWithdrawal.SavedAmount);
        Assert.Equal(4_300m, await BalanceAsync(client, accountId));
    }

    [Fact]
    public async Task AGoal_ClosesOnlyOnceItIsEmpty()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, _) = await TestData.AddAccountAsync(userId, 500m);
        var goal = await CreateAsync(client, new GoalRequest("Telefon", 30_000m, null, null));
        await MoveAsync(client, goal.Id, "deposits", accountId, 200m, "in");

        var refused = await client.DeleteAsync($"{Url}/{goal.Id}");
        await MoveAsync(client, goal.Id, "withdrawals", accountId, 200m, "out");
        var closed = await client.DeleteAsync($"{Url}/{goal.Id}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<GoalResponse>>(Url))!);
    }

    [Fact]
    public async Task IncomingMoney_FeedsTheGoal_OnlyAboveTheThreshold()
    {
        var (saver, saverId) = await _factory.CreateUserAsync();
        var (salaryAccount, salaryIban) = await TestData.AddAccountAsync(saverId, 0m);
        var goal = await CreateAsync(saver, new GoalRequest("Ev", 1_000_000m, null, 2027));
        await saver.PutAsJsonAsync($"{Url}/{goal.Id}/auto-save", new AutoSaveRequest(salaryAccount, "Percent", 10m, 10_000m));

        var (employer, employerId) = await _factory.CreateUserAsync();
        var (payroll, _) = await TestData.AddAccountAsync(employerId, 100_000m);
        await SendAsync(employer, payroll, salaryIban, 20_000m, "salary");
        await SendAsync(employer, payroll, salaryIban, 500m, "small");

        var saved = (await saver.GetFromJsonAsync<List<GoalResponse>>(Url))!.Single();
        Assert.Equal(2_000m, saved.SavedAmount);
        Assert.Equal(18_500m, await BalanceAsync(saver, salaryAccount));
        Assert.Equal(2027, saved.NewYearOf);
    }

    [Fact]
    public async Task AutoSave_StopsAtTheTarget()
    {
        var (saver, saverId) = await _factory.CreateUserAsync();
        var (salaryAccount, salaryIban) = await TestData.AddAccountAsync(saverId, 0m);
        var goal = await CreateAsync(saver, new GoalRequest("Bilgisayar", 1_500m, null, null));
        await saver.PutAsJsonAsync($"{Url}/{goal.Id}/auto-save", new AutoSaveRequest(salaryAccount, "Fixed", 1_000m, 0m));

        var (payer, payerId) = await _factory.CreateUserAsync();
        var (source, _) = await TestData.AddAccountAsync(payerId, 10_000m);
        await SendAsync(payer, source, salaryIban, 3_000m, "first");
        await SendAsync(payer, source, salaryIban, 3_000m, "second");

        Assert.Equal(1_500m, (await saver.GetFromJsonAsync<List<GoalResponse>>(Url))!.Single().SavedAmount);

        // Newest first: the second salary, its auto-save and the goal reached, then the first salary and its auto-save.
        var kinds = (await saver.GetFromJsonAsync<NotificationFeedResponse>("/api/notifications"))!.Items.Select(n => n.Kind);
        Assert.Equal(["goal", "savings", "money-in", "savings", "money-in"], kinds);
    }

    [Fact]
    public async Task ReachingTheTarget_IsAnnouncedOnce_EvenWhenTheDepositIsRetried()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (account, _) = await TestData.AddAccountAsync(userId, 2_000m);
        var goal = await CreateAsync(client, new GoalRequest("Tatil", 1_000m, null, null));

        await MoveAsync(client, goal.Id, "deposits", account, 600m, "first");
        await MoveAsync(client, goal.Id, "deposits", account, 400m, "second");
        await MoveAsync(client, goal.Id, "deposits", account, 400m, "second");

        var feed = (await client.GetFromJsonAsync<NotificationFeedResponse>("/api/notifications"))!;
        var reached = Assert.Single(feed.Items);
        Assert.Equal("goal", reached.Kind);
        Assert.Contains("\"Tatil\" kumbaranız 1.000,00 TL hedefine ulaştı.", reached.Body);
    }

    [Fact]
    public async Task MoneyInAGoal_CannotBeSentStraightToSomeoneElse()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, _) = await TestData.AddAccountAsync(userId, 1_000m);
        var goal = await CreateAsync(client, new GoalRequest("Saat", 5_000m, null, null));
        await MoveAsync(client, goal.Id, "deposits", accountId, 500m, "in");
        var savingsAccount = (await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts"))!.Single(a => a.Iban == goal.Iban);

        var (_, otherId) = await _factory.CreateUserAsync();
        var (_, otherIban) = await TestData.AddAccountAsync(otherId, 0m);
        var response = await SendAsync(client, savingsAccount.Id, otherIban, 100m, "out");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnotherCustomersGoal_IsNotFound()
    {
        var (owner, _) = await _factory.CreateUserAsync();
        var goal = await CreateAsync(owner, new GoalRequest("Gizli", 1_000m, null, null));
        var (stranger, strangerId) = await _factory.CreateUserAsync();
        var (accountId, _) = await TestData.AddAccountAsync(strangerId, 1_000m);

        var response = await Post(stranger, $"{Url}/{goal.Id}/deposits", new GoalMoneyRequest(accountId, 10m), "k");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ATargetDateInThePast_Returns400()
    {
        var (client, _) = await _factory.CreateUserAsync();

        var response = await client.PostAsJsonAsync(Url, new GoalRequest("Geçmiş", 1_000m, new DateOnly(2020, 1, 1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<GoalResponse> CreateAsync(HttpClient client, GoalRequest request)
    {
        var response = await client.PostAsJsonAsync(Url, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalResponse>())!;
    }

    private static async Task<GoalResponse> MoveAsync(HttpClient client, long goalId, string direction, long accountId, decimal amount, string key)
    {
        var response = await Post(client, $"{Url}/{goalId}/{direction}", new GoalMoneyRequest(accountId, amount), key);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalResponse>())!;
    }

    private static Task<HttpResponseMessage> Post<T>(HttpClient client, string url, T body, string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(message);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, long sourceAccountId, string targetIban, decimal amount, string key)
        => Post(client, "/api/transfers", new TransferRequest(sourceAccountId, targetIban, "TEST KULLANICI", amount, "Test ödemesi"), key);

    private static async Task<decimal> BalanceAsync(HttpClient client, long accountId)
        => (await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts"))!.Single(a => a.Id == accountId).Balance;
}
