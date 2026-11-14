using System.Net;
using System.Net.Http.Json;
using Bank.Contracts.Auth;
using Bank.Contracts.Cards;
using Bank.Contracts.Payments;
using Bank.Contracts.Savings;
using Bank.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bank.IntegrationTests;

/// <summary>The bank's operating rules: transfer limits, opening and closing accounts, contact details, card limits.</summary>
[Collection(ApiCollection.Name)]
public sealed class BankingRulesTests
{
    private readonly ApiFactory _factory;

    public BankingRulesTests(ApiFactory factory) => _factory = factory;

    private WebApplicationFactory<Program> WithLimits(decimal perTransfer, decimal daily) => _factory.WithWebHostBuilder(b => b
        .UseSetting("Banking:TransferLimits:TRY:PerTransfer", perTransfer.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .UseSetting("Banking:TransferLimits:TRY:Daily", daily.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public async Task MoneyForSomeoneElse_StaysWithinThePerTransferLimit()
    {
        using var bank = WithLimits(perTransfer: 1_000m, daily: 5_000m);
        var (sender, senderId) = await bank.CreateUserAsync();
        var (source, _) = await TestData.AddAccountAsync(senderId, 10_000m);
        var (_, otherId) = await bank.CreateUserAsync();
        var (_, targetIban) = await TestData.AddAccountAsync(otherId, 0m);

        var response = await SendAsync(sender, source, targetIban, 1_000.01m, "too-much");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Tek seferde", problem!.Detail);
    }

    [Fact]
    public async Task MoneyForSomeoneElse_AddsUpAgainstTheDailyLimit()
    {
        using var bank = WithLimits(perTransfer: 1_000m, daily: 1_500m);
        var (sender, senderId) = await bank.CreateUserAsync();
        var (source, _) = await TestData.AddAccountAsync(senderId, 10_000m);
        var (_, otherId) = await bank.CreateUserAsync();
        var (_, targetIban) = await TestData.AddAccountAsync(otherId, 0m);

        var first = await SendAsync(sender, source, targetIban, 1_000m, "first");
        var second = await SendAsync(sender, source, targetIban, 600m, "second");
        var fits = await SendAsync(sender, source, targetIban, 500m, "third");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, fits.StatusCode);
    }

    [Fact]
    public async Task MovesBetweenOwnAccounts_AreNotLimited()
    {
        using var bank = WithLimits(perTransfer: 1_000m, daily: 1_000m);
        var (client, userId) = await bank.CreateUserAsync();
        var (source, _) = await TestData.AddAccountAsync(userId, 10_000m);
        var goal = await (await client.PostAsJsonAsync("/api/goals", new GoalRequest("Ev", 50_000m, null, null))).Content.ReadFromJsonAsync<GoalResponse>();

        var deposit = new HttpRequestMessage(HttpMethod.Post, $"/api/goals/{goal!.Id}/deposits") { Content = JsonContent.Create(new GoalMoneyRequest(source, 5_000m)) };
        deposit.Headers.Add("Idempotency-Key", "own");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(deposit)).StatusCode);
    }

    [Fact]
    public async Task AnAccount_ClosesOnlyWhenEmpty_AndTheLastLiraAccountStays()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (first, _) = await TestData.AddAccountAsync(userId, 0m);
        var (withMoney, _) = await TestData.AddAccountAsync(userId, 10m);

        var refusedWithBalance = await client.PostAsync($"/api/accounts/{withMoney}/close", null);
        var closed = await client.PostAsync($"/api/accounts/{first}/close", null);
        await TestData.SetBalanceAsync(withMoney, 0m);
        var refusedAsLast = await client.PostAsync($"/api/accounts/{withMoney}/close", null);

        Assert.Equal(HttpStatusCode.Conflict, refusedWithBalance.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, refusedAsLast.StatusCode);
    }

    [Fact]
    public async Task AGoalsSavingsAccount_IsNotClosedDirectly()
    {
        var (client, _) = await _factory.CreateUserAsync();
        var goal = await (await client.PostAsJsonAsync("/api/goals", new GoalRequest("Tatil", 10_000m, null, null))).Content.ReadFromJsonAsync<GoalResponse>();
        var savings = (await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts"))!.Single(a => a.Iban == goal!.Iban);

        var response = await client.PostAsync($"/api/accounts/{savings.Id}/close", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlySoManyAccountsCanBeOpenInOneCurrency()
    {
        using var bank = _factory.WithWebHostBuilder(b => b.UseSetting("Banking:MaxAccountsPerCurrency", "2"));
        var (client, _) = await bank.CreateUserAsync();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/accounts", new OpenAccountRequest("USD"))).StatusCode);

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("02121234567")]
    public async Task APhoneThatIsNotATurkishMobile_IsRefused(string phone)
    {
        var registration = TestUsers.NewRegistration() with { Phone = phone };

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", registration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACardsDailyLimit_CannotExceedItsMonthlyLimit()
    {
        var (client, userId) = await _factory.CreateUserAsync();
        var (accountId, _) = await TestData.AddAccountAsync(userId, 0m);
        var cardId = await TestData.AddCardAsync(userId, accountId);

        var response = await client.PutAsJsonAsync($"/api/cards/{cardId}/settings", new CardSettingsRequest(true, true, 10_000m, 5_000m));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("dailyLimit", problem!.Errors.Keys);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, long source, string targetIban, decimal amount, string key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(source, targetIban, "TEST KULLANICI", amount, "Kural testi")),
        };
        message.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(message);
    }
}
