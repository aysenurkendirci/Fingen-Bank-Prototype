using Bank.Application.Abstractions.Repositories;
using Bank.Infrastructure.Persistence;
using Dapper;

namespace Bank.Infrastructure.Repositories;

public sealed class UserSecurityRepository : IUserSecurityRepository
{
    // Creates the row on first use, so every statement below can assume it exists.
    private const string EnsureRow = """
        IF NOT EXISTS (SELECT 1 FROM dbo.UserSecurity WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @UserId)
            INSERT INTO dbo.UserSecurity (UserId) VALUES (@UserId);
        """;

    private readonly DbSession _session;

    public UserSecurityRepository(DbSession session) => _session = session;

    public async Task<UserSecurityRow> GetAsync(long userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT FailedSignIns, LockedUntil, TotpPendingSecret, TotpSecret, TotpEnabledAt
            FROM dbo.UserSecurity WHERE UserId = @UserId
            """;

        var connection = await _session.GetConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<UserSecurityRow>(
                   new CommandDefinition(sql, new { UserId = userId }, _session.Transaction, cancellationToken: ct))
               ?? new UserSecurityRow();
    }

    public async Task<DateTime?> RecordFailureAsync(long userId, int maxFailures, DateTime now, TimeSpan lockFor, CancellationToken ct = default)
    {
        // One statement counts and, at the limit, locks: two wrong passwords at the same moment are both counted.
        const string sql = EnsureRow + """
            UPDATE dbo.UserSecurity
            SET FailedSignIns = CASE WHEN FailedSignIns + 1 >= @Max THEN 0 ELSE FailedSignIns + 1 END,
                LockedUntil   = CASE WHEN FailedSignIns + 1 >= @Max THEN @LockedUntil ELSE LockedUntil END
            OUTPUT CASE WHEN inserted.FailedSignIns = 0 THEN inserted.LockedUntil END
            WHERE UserId = @UserId;
            """;

        var connection = await _session.GetConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            sql, new { UserId = userId, Max = maxFailures, LockedUntil = now + lockFor }, _session.Transaction, cancellationToken: ct));
    }

    public Task ClearFailuresAsync(long userId, CancellationToken ct = default)
        => ExecuteAsync("UPDATE dbo.UserSecurity SET FailedSignIns = 0 WHERE UserId = @UserId AND FailedSignIns > 0", new { UserId = userId }, ct);

    public Task SavePendingSecretAsync(long userId, byte[] protectedSecret, CancellationToken ct = default)
        => ExecuteAsync(EnsureRow + "UPDATE dbo.UserSecurity SET TotpPendingSecret = @Secret WHERE UserId = @UserId;",
            new { UserId = userId, Secret = protectedSecret }, ct);

    public async Task<bool> EnableSecretAsync(long userId, long step, DateTime now, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.UserSecurity
            SET TotpSecret = TotpPendingSecret, TotpPendingSecret = NULL, TotpEnabledAt = @Now, TotpLastStep = @Step
            WHERE UserId = @UserId AND TotpPendingSecret IS NOT NULL
            """;

        return await ExecuteAsync(sql, new { UserId = userId, Step = step, Now = now }, ct) == 1;
    }

    public Task DisableSecretAsync(long userId, CancellationToken ct = default)
        => ExecuteAsync("""
            UPDATE dbo.UserSecurity
            SET TotpSecret = NULL, TotpPendingSecret = NULL, TotpEnabledAt = NULL, TotpLastStep = NULL
            WHERE UserId = @UserId
            """, new { UserId = userId }, ct);

    public async Task<bool> TryUseStepAsync(long userId, long step, CancellationToken ct = default)
    {
        // Only a step later than the last one used passes, and claiming it is the same statement as checking it.
        const string sql = """
            UPDATE dbo.UserSecurity SET TotpLastStep = @Step
            WHERE UserId = @UserId AND (TotpLastStep IS NULL OR TotpLastStep < @Step)
            """;

        return await ExecuteAsync(sql, new { UserId = userId, Step = step }, ct) == 1;
    }

    private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken ct)
    {
        var connection = await _session.GetConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, _session.Transaction, cancellationToken: ct));
    }
}
