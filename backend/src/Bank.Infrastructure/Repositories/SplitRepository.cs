using Bank.Application.Abstractions.Repositories;
using Bank.Infrastructure.Persistence;
using Dapper;

namespace Bank.Infrastructure.Repositories;

public sealed class SplitRepository : ISplitRepository
{
    private const string SplitSelect = """
        SELECT s.SplitId, s.Title, s.TotalAmount, s.CurrencyCode, s.SpentOn, a.Iban AS ReceivingIban, s.Status, s.CreatedAt
        FROM splits.Splits s
        JOIN dbo.Accounts a ON a.AccountId = s.ReceivingAccountId
        WHERE s.OwnerUserId = @OwnerUserId
        """;

    private const string RequestSelect = """
        SELECT sh.ShareId, sh.SplitId, s.OwnerUserId, CONCAT(u.FirstName, N' ', u.LastName) AS OwnerName, s.Title,
               sh.Amount, s.CurrencyCode, s.ReceivingAccountId, a.Iban AS ReceivingIban, s.SpentOn, sh.Status,
               s.Status AS SplitStatus, s.CreatedAt
        FROM splits.Shares sh
        JOIN splits.Splits s ON s.SplitId = sh.SplitId
        JOIN dbo.Users u ON u.UserId = s.OwnerUserId
        JOIN dbo.Accounts a ON a.AccountId = s.ReceivingAccountId
        WHERE sh.DebtorUserId = @DebtorUserId
        """;

    private readonly DbSession _session;

    public SplitRepository(DbSession session) => _session = session;

    public async Task<AccountHolderRow?> FindHolderAsync(string iban, CancellationToken ct = default)
    {
        const string sql = """
            SELECT a.AccountId, a.UserId, CONCAT(u.FirstName, N' ', u.LastName) AS HolderName, a.Status
            FROM dbo.Accounts a
            JOIN dbo.Users u ON u.UserId = a.UserId
            WHERE a.Iban = @Iban
            """;

        return (await QueryAsync<AccountHolderRow>(sql, new { Iban = new DbString { Value = iban, IsAnsi = true, IsFixedLength = true, Length = 26 } }, ct))
            .SingleOrDefault();
    }

    public async Task<long> CreateAsync(NewSplit split, CancellationToken ct = default)
    {
        const string insertSplit = """
            INSERT INTO splits.Splits (OwnerUserId, ReceivingAccountId, Title, TotalAmount, CurrencyCode, SpentOn, CreatedAt)
            OUTPUT INSERTED.SplitId
            VALUES (@OwnerUserId, @ReceivingAccountId, @Title, @TotalAmount, 'TRY', @SpentOn, @CreatedAt)
            """;

        const string insertShare = """
            INSERT INTO splits.Shares (SplitId, Name, DebtorUserId, Amount)
            VALUES (@SplitId, @Name, @DebtorUserId, @Amount)
            """;

        var splitId = await ScalarAsync<long>(insertSplit, new
        {
            split.OwnerUserId,
            split.ReceivingAccountId,
            split.Title,
            split.TotalAmount,
            SpentOn = split.SpentOn.ToDateTime(TimeOnly.MinValue),
            split.CreatedAt,
        }, ct);

        var connection = await _session.GetConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            insertShare,
            split.Shares.Select(s => new { SplitId = splitId, s.Name, s.DebtorUserId, s.Amount }),
            _session.Transaction,
            cancellationToken: ct));

        return splitId;
    }

    public async Task<IReadOnlyList<SplitRow>> ListOwnedAsync(long ownerUserId, CancellationToken ct = default)
        => await WithSharesAsync((await QueryAsync<SplitRow>(SplitSelect + " ORDER BY s.SplitId DESC", new { OwnerUserId = ownerUserId }, ct)).AsList(), ct);

    public async Task<SplitRow?> GetOwnedAsync(long ownerUserId, long splitId, CancellationToken ct = default)
    {
        var rows = (await QueryAsync<SplitRow>(SplitSelect + " AND s.SplitId = @SplitId", new { OwnerUserId = ownerUserId, SplitId = splitId }, ct)).AsList();
        return (await WithSharesAsync(rows, ct)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<ShareRequestRow>> ListRequestsAsync(long debtorUserId, CancellationToken ct = default)
        => (await QueryAsync<ShareRequestRow>(RequestSelect + " ORDER BY sh.ShareId DESC", new { DebtorUserId = debtorUserId }, ct)).AsList();

    public async Task<ShareRequestRow?> GetRequestAsync(long debtorUserId, long shareId, CancellationToken ct = default)
        => (await QueryAsync<ShareRequestRow>(
                // Locked, so two settlements of the same share (two tabs) are answered one after the other.
                RequestSelect.Replace("FROM splits.Shares sh", "FROM splits.Shares sh WITH (UPDLOCK, ROWLOCK)") + " AND sh.ShareId = @ShareId",
                new { DebtorUserId = debtorUserId, ShareId = shareId }, ct))
            .SingleOrDefault();

    public async Task<SentTransferRow?> GetSentTransferAsync(long userId, long transferId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TargetAccountId, Amount, CurrencyCode
            FROM payments.Transfers
            WHERE TransferId = @TransferId AND UserId = @UserId
            """;

        return (await QueryAsync<SentTransferRow>(sql, new { TransferId = transferId, UserId = userId }, ct)).SingleOrDefault();
    }

    public async Task<bool> IsTransferUsedAsync(long transferId, CancellationToken ct = default)
        => await ScalarAsync<int>("SELECT COUNT(*) FROM splits.Shares WHERE TransferId = @TransferId", new { TransferId = transferId }, ct) > 0;

    public async Task<bool> SettleAsync(long shareId, string status, long? transferId, DateTime at, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE splits.Shares SET Status = @Status, TransferId = @TransferId, SettledAt = @At
            WHERE ShareId = @ShareId AND Status = 'Open'
            """;

        return await ExecuteAsync(sql, new
        {
            ShareId = shareId,
            Status = new DbString { Value = status, IsAnsi = true, Length = 10 },
            TransferId = transferId,
            At = at,
        }, ct) == 1;
    }

    public async Task<IReadOnlyList<long>> CancelAsync(long splitId, DateTime at, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE splits.Splits SET Status = 'Cancelled' WHERE SplitId = @SplitId;

            UPDATE splits.Shares SET Status = 'Cancelled', SettledAt = @At
            OUTPUT INSERTED.DebtorUserId
            WHERE SplitId = @SplitId AND Status = 'Open';
            """;

        var debtors = await QueryAsync<long?>(sql, new { SplitId = splitId, At = at }, ct);
        return debtors.Where(d => d is not null).Select(d => d!.Value).Distinct().ToList();
    }

    private async Task<IReadOnlyList<SplitRow>> WithSharesAsync(List<SplitRow> splits, CancellationToken ct)
    {
        if (splits.Count == 0)
            return splits;

        const string sql = """
            SELECT ShareId, SplitId, Name, DebtorUserId, Amount, Status, SettledAt
            FROM splits.Shares
            WHERE SplitId IN @SplitIds
            ORDER BY ShareId
            """;

        var byId = splits.ToDictionary(s => s.SplitId);
        foreach (var share in await QueryAsync<ShareRow>(sql, new { SplitIds = byId.Keys.ToArray() }, ct))
            byId[share.SplitId].Shares.Add(share);
        return splits;
    }

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
