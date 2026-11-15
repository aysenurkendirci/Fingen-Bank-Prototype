namespace Bank.Application.Abstractions.Repositories;

/// <summary>
/// Expenses split with friends. Every statement joins the request's unit of work, so a split, its shares and the
/// notifications about them are stored together.
/// </summary>
public interface ISplitRepository
{
    /// <summary>Who holds the account with this IBAN, for the same name check a transfer makes.</summary>
    Task<AccountHolderRow?> FindHolderAsync(string iban, CancellationToken ct = default);

    Task<long> CreateAsync(NewSplit split, CancellationToken ct = default);

    /// <summary>The splits the user made, newest first, with their shares.</summary>
    Task<IReadOnlyList<SplitRow>> ListOwnedAsync(long ownerUserId, CancellationToken ct = default);

    Task<SplitRow?> GetOwnedAsync(long ownerUserId, long splitId, CancellationToken ct = default);

    /// <summary>Shares other customers asked of the user, newest first.</summary>
    Task<IReadOnlyList<ShareRequestRow>> ListRequestsAsync(long debtorUserId, CancellationToken ct = default);

    Task<ShareRequestRow?> GetRequestAsync(long debtorUserId, long shareId, CancellationToken ct = default);

    /// <summary>A transfer the user sent, if there is one with this id.</summary>
    Task<SentTransferRow?> GetSentTransferAsync(long userId, long transferId, CancellationToken ct = default);

    /// <summary>Whether another share was already settled with this transfer.</summary>
    Task<bool> IsTransferUsedAsync(long transferId, CancellationToken ct = default);

    /// <summary>Moves an open share to its final state. False if it was no longer open (answered meanwhile).</summary>
    Task<bool> SettleAsync(long shareId, string status, long? transferId, DateTime at, CancellationToken ct = default);

    /// <summary>Cancels the split and its open shares; returns who still owed something, to tell them.</summary>
    Task<IReadOnlyList<long>> CancelAsync(long splitId, DateTime at, CancellationToken ct = default);
}

public sealed record NewSplit(
    long OwnerUserId,
    long ReceivingAccountId,
    string Title,
    decimal TotalAmount,
    DateOnly SpentOn,
    DateTime CreatedAt,
    IReadOnlyList<NewShare> Shares);

public sealed record NewShare(string Name, long? DebtorUserId, decimal Amount);

public static class ShareStatus
{
    public const string Open = "Open";
    public const string Paid = "Paid";
    public const string Declined = "Declined";
    public const string Cancelled = "Cancelled";
}

public sealed class AccountHolderRow
{
    public long AccountId { get; init; }
    public long UserId { get; init; }
    public string HolderName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class SplitRow
{
    public long SplitId { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime SpentOn { get; init; }
    public string ReceivingIban { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<ShareRow> Shares { get; } = [];
}

public sealed class ShareRow
{
    public long ShareId { get; init; }
    public long SplitId { get; init; }
    public string Name { get; init; } = string.Empty;
    public long? DebtorUserId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? SettledAt { get; init; }
}

public sealed class ShareRequestRow
{
    public long ShareId { get; init; }
    public long SplitId { get; init; }
    public long OwnerUserId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public long ReceivingAccountId { get; init; }
    public string ReceivingIban { get; init; } = string.Empty;
    public DateTime SpentOn { get; init; }
    public string Status { get; init; } = string.Empty;
    public string SplitStatus { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class SentTransferRow
{
    public long TargetAccountId { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
}
