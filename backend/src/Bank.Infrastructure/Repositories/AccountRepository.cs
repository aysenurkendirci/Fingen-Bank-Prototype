using Bank.Application.Abstractions.Repositories;
using Bank.Domain.Accounts;
using Bank.Infrastructure.Persistence;
using Dapper;

namespace Bank.Infrastructure.Repositories;

/// <summary>Reads accounts and opens new ones. Moving money is TransferRepository's job.</summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly SqlConnectionFactory _connections;
    // Closing an account is a command and joins its unit of work; the reads open their own connection.
    private readonly DbSession _session;

    public AccountRepository(SqlConnectionFactory connections, DbSession session)
    {
        _connections = connections;
        _session = session;
    }

    public async Task<decimal> GetAvailableBalanceAsync(long userId, string currencyCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COALESCE(SUM(Balance), 0)
            FROM dbo.Accounts
            WHERE UserId = @UserId AND Status = 'Active' AND CurrencyCode = @CurrencyCode
            """;

        await using var connection = await _connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, new { UserId = userId, CurrencyCode = currencyCode }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AccountRow>> ListAsync(long userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.AccountId, a.Iban, a.AccountType, a.CurrencyCode, a.Balance, a.Status, g.Name AS GoalName
            FROM dbo.Accounts a
            LEFT JOIN savings.Goals g ON g.AccountId = a.AccountId
            WHERE a.UserId = @UserId
            ORDER BY a.AccountId
            """;

        await using var connection = await _connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<AccountRow>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<long> NextAccountNumberAsync(CancellationToken ct = default)
    {
        await using var connection = await _connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("SELECT NEXT VALUE FOR payments.AccountNumbers", cancellationToken: ct));
    }

    public async Task<AccountRow> OpenAsync(
        long userId, string accountType, string currencyCode, Iban iban, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Accounts (UserId, AccountType, CurrencyCode, Iban, Balance)
            OUTPUT INSERTED.AccountId, INSERTED.Iban, INSERTED.AccountType, INSERTED.CurrencyCode, INSERTED.Balance, INSERTED.Status
            VALUES (@UserId, @AccountType, @CurrencyCode, @Iban, 0)
            """;

        await using var connection = await _connections.OpenAsync(ct);
        return await connection.QuerySingleAsync<AccountRow>(new CommandDefinition(
            sql,
            new { UserId = userId, AccountType = accountType, CurrencyCode = currencyCode, Iban = iban.Value },
            cancellationToken: ct));
    }

    public async Task<CloseAccountOutcome> CloseAsync(long userId, long accountId, CancellationToken ct = default)
    {
        // One batch under an update lock on the customer's accounts, so no transfer can land between the checks and
        // the close. An auto-save rule watching the account is dropped with it: nothing will arrive there any more.
        const string sql = """
            DECLARE @Type VARCHAR(20), @Currency CHAR(3), @Status VARCHAR(10), @Balance DECIMAL(19, 4);

            SELECT @Type = AccountType, @Currency = CurrencyCode, @Status = Status, @Balance = Balance
            FROM dbo.Accounts WITH (UPDLOCK, ROWLOCK)
            WHERE AccountId = @AccountId AND UserId = @UserId;

            IF @Type IS NULL SELECT 1;
            ELSE IF @Status <> 'Active' SELECT 2;
            ELSE IF @Type = 'Savings' SELECT 4;
            ELSE IF @Balance <> 0 SELECT 3;
            ELSE IF @Type = 'Demand' AND @Currency = 'TRY' AND NOT EXISTS (
                SELECT 1 FROM dbo.Accounts WITH (UPDLOCK)
                WHERE UserId = @UserId AND AccountId <> @AccountId AND AccountType = 'Demand' AND CurrencyCode = 'TRY' AND Status = 'Active')
                SELECT 5;
            ELSE
            BEGIN
                UPDATE dbo.Accounts SET Status = 'Closed' WHERE AccountId = @AccountId;
                UPDATE savings.Goals
                SET AutoSaveSourceAccountId = NULL, AutoSaveMode = NULL, AutoSaveValue = NULL, AutoSaveMinIncoming = NULL
                WHERE AutoSaveSourceAccountId = @AccountId;
                SELECT 0;
            END
            """;

        var connection = await _session.GetConnectionAsync(ct);
        var outcome = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { AccountId = accountId, UserId = userId }, _session.Transaction, cancellationToken: ct));

        return outcome switch
        {
            0 => CloseAccountOutcome.Closed,
            1 => CloseAccountOutcome.NotFound,
            2 => CloseAccountOutcome.NotActive,
            3 => CloseAccountOutcome.HasBalance,
            4 => CloseAccountOutcome.IsSavings,
            _ => CloseAccountOutcome.LastDemandAccount,
        };
    }

    public async Task<bool> BelongsToAsync(long userId, long accountId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.Accounts WHERE AccountId = @AccountId AND UserId = @UserId";

        await using var connection = await _connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { AccountId = accountId, UserId = userId }, cancellationToken: ct)) > 0;
    }

    public async Task<(IReadOnlyList<StatementEntryRow> Items, int TotalCount)> ListStatementAsync(
        long accountId, int page, int pageSize, CancellationToken ct = default)
    {
        // The counterparty is the other account of the transfer: the target for a debit, the source for a credit.
        const string sql = """
            SELECT COUNT(*)
            FROM payments.LedgerEntries
            WHERE AccountId = @AccountId;

            SELECT l.EntryId, l.TransferId, l.Side, l.Amount, l.CurrencyCode, l.BalanceAfter, t.Description,
                   other.Iban AS CounterpartyIban, l.CreatedAt
            FROM payments.LedgerEntries l
            JOIN payments.Transfers t ON t.TransferId = l.TransferId
            JOIN dbo.Accounts other
              ON other.AccountId = CASE l.Side WHEN 'Debit' THEN t.TargetAccountId ELSE t.SourceAccountId END
            WHERE l.AccountId = @AccountId
            ORDER BY l.EntryId DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        var parameters = new { AccountId = accountId, Skip = (page - 1) * pageSize, Take = pageSize };

        await using var connection = await _connections.OpenAsync(ct);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));

        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<StatementEntryRow>()).AsList();
        return (items, total);
    }
}
