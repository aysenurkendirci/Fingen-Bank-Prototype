using Bank.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Housekeeping;

public sealed class HousekeepingOptions
{
    public const string Section = "Housekeeping";

    public bool Enabled { get; init; } = true;
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Expired refresh tokens are kept this long, so a reused old token can still be recognised.</summary>
    public int RefreshTokenRetentionDays { get; init; } = 30;

    /// <summary>Published events are kept this long, for tracing a delivery after the fact.</summary>
    public int PublishedEventRetentionDays { get; init; } = 7;

    /// <summary>Notifications the customer has read are kept this long; unread ones are never removed.</summary>
    public int ReadNotificationRetentionDays { get; init; } = 90;

    /// <summary>Rows removed per statement, so one run never holds long locks on a busy table.</summary>
    public int BatchSize { get; init; } = 1_000;
}

public sealed record HousekeepingResult(int RefreshTokens, int PublishedEvents, int ReadNotifications);

/// <summary>
/// Removes rows that have done their job: refresh tokens long past expiry, events the broker already received and
/// notifications read long ago. None of them is history a customer relies on; left alone they only grow. Deletes
/// run in small batches.
/// </summary>
public sealed class HousekeepingRunner
{
    private readonly SqlConnectionFactory _connections;
    private readonly HousekeepingOptions _options;

    public HousekeepingRunner(SqlConnectionFactory connections, IOptions<HousekeepingOptions> options)
    {
        _connections = connections;
        _options = options.Value;
    }

    public async Task<HousekeepingResult> RunOnceAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        await using var connection = await _connections.OpenAsync(ct);

        // A token points at the one that replaced it; that link is cleared first so the delete never trips over it.
        const string tokens = """
            DECLARE @Old TABLE (TokenId BIGINT PRIMARY KEY);
            INSERT INTO @Old SELECT TOP (@Batch) TokenId FROM dbo.RefreshTokens WHERE ExpiresAt < @Cutoff;

            UPDATE dbo.RefreshTokens SET ReplacedByTokenId = NULL WHERE ReplacedByTokenId IN (SELECT TokenId FROM @Old);
            DELETE FROM dbo.RefreshTokens WHERE TokenId IN (SELECT TokenId FROM @Old);
            SELECT @@ROWCOUNT;
            """;

        const string events = """
            DELETE TOP (@Batch) FROM messaging.OutboxMessages WHERE PublishedAt < @Cutoff;
            SELECT @@ROWCOUNT;
            """;

        const string notifications = """
            DELETE TOP (@Batch) FROM dbo.Notifications WHERE ReadAt < @Cutoff;
            SELECT @@ROWCOUNT;
            """;

        var removedTokens = await DeleteInBatchesAsync(connection, tokens, nowUtc.AddDays(-_options.RefreshTokenRetentionDays), ct);
        var removedEvents = await DeleteInBatchesAsync(connection, events, nowUtc.AddDays(-_options.PublishedEventRetentionDays), ct);
        var removedNotifications = await DeleteInBatchesAsync(
            connection, notifications, nowUtc.AddDays(-_options.ReadNotificationRetentionDays), ct);
        return new HousekeepingResult(removedTokens, removedEvents, removedNotifications);
    }

    private async Task<int> DeleteInBatchesAsync(System.Data.Common.DbConnection connection, string sql, DateTime cutoff, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var removed = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new { Cutoff = cutoff, Batch = _options.BatchSize }, cancellationToken: ct));
            total += removed;
            if (removed < _options.BatchSize)
                return total;
        }
    }
}

/// <summary>Runs the housekeeping once an hour for as long as the API is up.</summary>
public sealed class HousekeepingService : BackgroundService
{
    private readonly HousekeepingRunner _runner;
    private readonly HousekeepingOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<HousekeepingService> _logger;

    public HousekeepingService(
        HousekeepingRunner runner, IOptions<HousekeepingOptions> options, TimeProvider time, ILogger<HousekeepingService> logger)
    {
        _runner = runner;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _runner.RunOnceAsync(_time.GetUtcNow().UtcDateTime, stoppingToken);
                if (result.RefreshTokens + result.PublishedEvents + result.ReadNotifications > 0)
                    _logger.LogInformation(
                        "Housekeeping removed {RefreshTokens} expired refresh tokens, {PublishedEvents} published events and {ReadNotifications} read notifications",
                        result.RefreshTokens, result.PublishedEvents, result.ReadNotifications);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // An outage only postpones the clean-up; the rows are still there next time.
                _logger.LogError(e, "Housekeeping failed");
            }

            await Task.Delay(_options.Interval, _time, stoppingToken);
        }
    }
}
