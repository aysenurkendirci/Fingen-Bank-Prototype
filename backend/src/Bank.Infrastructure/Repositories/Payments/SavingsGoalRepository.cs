using Bank.Application.Abstractions.Repositories;
using Bank.Domain.Accounts;
using Bank.Infrastructure.Persistence;
using Dapper;

namespace Bank.Infrastructure.Repositories.Payments;

/// <remarks>
/// Works on the scope's <see cref="DbSession"/>: inside a command every statement joins its transaction, so a
/// goal and its savings account appear together and an auto-save commits or rolls back with its trigger.
/// </remarks>
public sealed class SavingsGoalRepository : ISavingsGoalRepository
{
    private const string GoalSelect = """
        SELECT g.GoalId, g.AccountId, g.Name, g.TargetAmount, a.Balance AS SavedAmount, g.TargetDate, g.NewYearOf,
               a.Iban, g.AutoSaveSourceAccountId, s.Iban AS AutoSaveSourceIban, g.AutoSaveMode, g.AutoSaveValue,
               g.AutoSaveMinIncoming
        FROM savings.Goals g
        JOIN dbo.Accounts a ON a.AccountId = g.AccountId
        LEFT JOIN dbo.Accounts s ON s.AccountId = g.AutoSaveSourceAccountId
        WHERE g.UserId = @UserId AND g.Status = 'Active'
        """;

    private readonly DbSession _session;

    public SavingsGoalRepository(DbSession session) => _session = session;

    public async Task<IReadOnlyList<GoalRow>> ListAsync(long userId, CancellationToken ct = default)
        => (await QueryAsync<GoalRow>(GoalSelect + " ORDER BY g.GoalId", new { UserId = userId }, ct)).AsList();

    public async Task<GoalRow?> GetAsync(long userId, long goalId, CancellationToken ct = default)
        => (await QueryAsync<GoalRow>(GoalSelect + " AND g.GoalId = @GoalId", new { UserId = userId, GoalId = goalId }, ct))
            .SingleOrDefault();

    public async Task<long> CreateAsync(long userId, Iban savingsIban, GoalDetails details, DateTime createdAt, CancellationToken ct = default)
    {
        const string sql = """
            DECLARE @AccountId BIGINT;

            INSERT INTO dbo.Accounts (UserId, AccountType, CurrencyCode, Iban, Balance)
            VALUES (@UserId, 'Savings', 'TRY', @Iban, 0);
            SET @AccountId = SCOPE_IDENTITY();

            INSERT INTO savings.Goals (UserId, AccountId, Name, TargetAmount, TargetDate, NewYearOf, CreatedAt)
            OUTPUT INSERTED.GoalId
            VALUES (@UserId, @AccountId, @Name, @TargetAmount, @TargetDate, @NewYearOf, @CreatedAt);
            """;

        return await ScalarAsync<long>(sql, new
        {
            UserId = userId,
            Iban = savingsIban.Value,
            details.Name,
            details.TargetAmount,
            TargetDate = details.TargetDate?.ToDateTime(TimeOnly.MinValue),
            details.NewYearOf,
            CreatedAt = createdAt,
        }, ct);
    }

    public async Task<bool> UpdateAsync(long userId, long goalId, GoalDetails details, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE savings.Goals
            SET Name = @Name, TargetAmount = @TargetAmount, TargetDate = @TargetDate, NewYearOf = @NewYearOf
            WHERE GoalId = @GoalId AND UserId = @UserId AND Status = 'Active'
            """;

        return await ExecuteAsync(sql, new
        {
            GoalId = goalId,
            UserId = userId,
            details.Name,
            details.TargetAmount,
            TargetDate = details.TargetDate?.ToDateTime(TimeOnly.MinValue),
            details.NewYearOf,
        }, ct) == 1;
    }

    public async Task<bool> SetAutoSaveAsync(long userId, long goalId, AutoSaveRule? rule, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE savings.Goals
            SET AutoSaveSourceAccountId = @SourceAccountId, AutoSaveMode = @Mode, AutoSaveValue = @Value, AutoSaveMinIncoming = @MinIncoming
            WHERE GoalId = @GoalId AND UserId = @UserId AND Status = 'Active'
            """;

        return await ExecuteAsync(sql, new
        {
            GoalId = goalId,
            UserId = userId,
            rule?.SourceAccountId,
            rule?.Mode,
            rule?.Value,
            rule?.MinIncoming,
        }, ct) == 1;
    }

    public async Task<CloseGoalOutcome> CloseAsync(long userId, long goalId, CancellationToken ct = default)
    {
        // The balance check and both updates happen under one lock on the savings account, so no deposit can land
        // between "it is empty" and "it is closed".
        const string sql = """
            DECLARE @AccountId BIGINT, @Balance DECIMAL(19, 2);

            SELECT @AccountId = a.AccountId, @Balance = a.Balance
            FROM savings.Goals g
            JOIN dbo.Accounts a WITH (UPDLOCK, ROWLOCK) ON a.AccountId = g.AccountId
            WHERE g.GoalId = @GoalId AND g.UserId = @UserId AND g.Status = 'Active';

            IF @AccountId IS NULL SELECT 1;
            ELSE IF @Balance <> 0 SELECT 2;
            ELSE
            BEGIN
                UPDATE savings.Goals SET Status = 'Closed', AutoSaveSourceAccountId = NULL, AutoSaveMode = NULL,
                       AutoSaveValue = NULL, AutoSaveMinIncoming = NULL
                WHERE GoalId = @GoalId;
                UPDATE dbo.Accounts SET Status = 'Closed' WHERE AccountId = @AccountId;
                SELECT 0;
            END
            """;

        return await ScalarAsync<int>(sql, new { GoalId = goalId, UserId = userId }, ct) switch
        {
            0 => CloseGoalOutcome.Closed,
            1 => CloseGoalOutcome.NotFound,
            _ => CloseGoalOutcome.NotEmpty,
        };
    }

    public async Task<IReadOnlyList<AutoSaveTarget>> ListAutoSaveTargetsAsync(long sourceAccountId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT g.GoalId, g.UserId, g.AccountId AS GoalAccountId, g.Name, g.TargetAmount, a.Balance AS SavedAmount,
                   g.AutoSaveSourceAccountId AS SourceAccountId, g.AutoSaveMode AS Mode, g.AutoSaveValue AS Value,
                   g.AutoSaveMinIncoming AS MinIncoming
            FROM savings.Goals g
            JOIN dbo.Accounts a ON a.AccountId = g.AccountId
            WHERE g.AutoSaveSourceAccountId = @SourceAccountId AND g.Status = 'Active'
            ORDER BY g.GoalId
            """;

        return (await QueryAsync<AutoSaveTarget>(sql, new { SourceAccountId = sourceAccountId }, ct)).AsList();
    }

    public async Task<bool> TryStartAutoSaveAsync(long goalId, long triggerTransferId, DateTime at, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO savings.AutoSaveRuns (GoalId, TriggerTransferId, CreatedAt)
            SELECT @GoalId, @TriggerTransferId, @At
            WHERE NOT EXISTS (SELECT 1 FROM savings.AutoSaveRuns WITH (UPDLOCK, HOLDLOCK)
                              WHERE GoalId = @GoalId AND TriggerTransferId = @TriggerTransferId)
            """;

        return await ExecuteAsync(sql, new { GoalId = goalId, TriggerTransferId = triggerTransferId, At = at }, ct) == 1;
    }

    public Task CompleteAutoSaveAsync(long goalId, long triggerTransferId, long transferId, CancellationToken ct = default)
        => ExecuteAsync(
            "UPDATE savings.AutoSaveRuns SET TransferId = @TransferId WHERE GoalId = @GoalId AND TriggerTransferId = @TriggerTransferId",
            new { GoalId = goalId, TriggerTransferId = triggerTransferId, TransferId = transferId },
            ct);

    public async Task<bool> IsSavingsAccountAsync(long accountId, CancellationToken ct = default)
        => await ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Accounts WHERE AccountId = @AccountId AND AccountType = 'Savings'",
            new { AccountId = accountId },
            ct) > 0;

    private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters, CancellationToken ct)
    {
        var connection = await _session.GetConnectionAsync(ct);
        return await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, _session.Transaction, cancellationToken: ct));
    }

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken ct)
    {
        var connection = await _session.GetConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, _session.Transaction, cancellationToken: ct))
               ?? throw new InvalidOperationException("Expected a value from the database.");
    }

    private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken ct)
    {
        var connection = await _session.GetConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, _session.Transaction, cancellationToken: ct));
    }
}
