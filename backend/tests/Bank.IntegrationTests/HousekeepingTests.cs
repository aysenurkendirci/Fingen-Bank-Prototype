using System.Security.Cryptography;
using Bank.Database;
using Bank.Infrastructure.Housekeeping;
using Bank.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HousekeepingTests
{
    private static readonly DateTime Now = ApiFactory.Now.UtcDateTime;

    private readonly ApiFactory _factory;

    public HousekeepingTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnErasedCustomersIdentityNumber_GoesAfterEightYears_NotBefore()
    {
        var (_, oldId) = await _factory.CreateUserAsync();
        var (_, recentId) = await _factory.CreateUserAsync();
        await using var connection = new SqlConnection(ApiFactory.ConnectionString);
        const string insert = "INSERT INTO compliance.ErasedIdentities (UserId, TcNo, ErasedAt) VALUES (@UserId, '10000000146', @ErasedAt)";
        await connection.ExecuteAsync(insert, new { UserId = oldId, ErasedAt = Now.AddYears(-8).AddDays(-1) });
        await connection.ExecuteAsync(insert, new { UserId = recentId, ErasedAt = Now.AddYears(-8).AddDays(1) });

        await _factory.Services.GetRequiredService<HousekeepingRunner>().RunOnceAsync(Now);

        var left = (await connection.QueryAsync<long>(
            "SELECT UserId FROM compliance.ErasedIdentities WHERE UserId IN @Ids", new { Ids = new[] { oldId, recentId } })).ToList();
        Assert.Equal([recentId], left);
    }

    [Fact]
    public async Task LongExpiredTokensAndOldPublishedEvents_AreRemoved_RecentOnesStay()
    {
        var (_, userId) = await _factory.CreateUserAsync();
        await using var connection = new SqlConnection(ApiFactory.ConnectionString);

        // An old chain (the first token replaced by the second, both long expired) and one token still valid.
        var replacement = await AddTokenAsync(connection, userId, Now.AddDays(-90), Now.AddDays(-60), replacedBy: null);
        var replaced = await AddTokenAsync(connection, userId, Now.AddDays(-91), Now.AddDays(-61), replacedBy: replacement);
        var current = await AddTokenAsync(connection, userId, Now.AddHours(-1), Now.AddHours(7), replacedBy: null);
        var oldEvent = await AddEventAsync(connection, publishedAt: Now.AddDays(-30));
        var recentEvent = await AddEventAsync(connection, publishedAt: Now.AddHours(-2));
        var pendingEvent = await AddEventAsync(connection, publishedAt: null);

        await _factory.Services.GetRequiredService<HousekeepingRunner>().RunOnceAsync(Now);

        var tokens = (await connection.QueryAsync<long>(
            "SELECT TokenId FROM dbo.RefreshTokens WHERE TokenId IN @Ids", new { Ids = new[] { replacement, replaced, current } })).ToList();
        var events = (await connection.QueryAsync<Guid>(
            "SELECT MessageId FROM messaging.OutboxMessages WHERE MessageId IN @Ids", new { Ids = new[] { oldEvent, recentEvent, pendingEvent } })).ToList();

        Assert.Equal([current], tokens);
        Assert.Equal(2, events.Count);
        Assert.DoesNotContain(oldEvent, events);
    }

    [Fact]
    public void TheTestDatabase_HasNoPendingMigrations()
        => Assert.Empty(DatabaseMigrator.PendingScripts(ApiFactory.ConnectionString));

    private static Task<long> AddTokenAsync(SqlConnection connection, long userId, DateTime created, DateTime expires, long? replacedBy)
        => connection.QuerySingleAsync<long>(
            """
            INSERT INTO dbo.RefreshTokens (UserId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedByTokenId)
            OUTPUT INSERTED.TokenId
            VALUES (@UserId, @Hash, @Created, @Expires, CASE WHEN @ReplacedBy IS NULL THEN NULL ELSE @Created END, @ReplacedBy)
            """,
            new { UserId = userId, Hash = RandomNumberGenerator.GetBytes(32), Created = created, Expires = expires, ReplacedBy = replacedBy });

    private static async Task<Guid> AddEventAsync(SqlConnection connection, DateTime? publishedAt)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO messaging.OutboxMessages (MessageId, Type, Payload, OccurredAt, PublishedAt)
            VALUES (@Id, 'test.event', '{}', @Occurred, @PublishedAt)
            """,
            new { Id = id, Occurred = Now.AddDays(-31), PublishedAt = publishedAt });
        return id;
    }
}
