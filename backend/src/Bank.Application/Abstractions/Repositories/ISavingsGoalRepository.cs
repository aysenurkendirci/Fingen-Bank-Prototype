using Bank.Domain.Accounts;

namespace Bank.Application.Abstractions.Repositories;

/// <remarks>
/// Writes take part in the request's unit of work, so a goal and its savings account are created together and
/// an auto-save moves money in the same transaction as the transfer that triggered it.
/// </remarks>
public interface ISavingsGoalRepository
{
    Task<IReadOnlyList<GoalRow>> ListAsync(long userId, CancellationToken ct = default);

    Task<GoalRow?> GetAsync(long userId, long goalId, CancellationToken ct = default);

    /// <summary>Opens the goal's savings account and stores the goal; returns the new goal id.</summary>
    Task<long> CreateAsync(long userId, Iban savingsIban, GoalDetails details, DateTime createdAt, CancellationToken ct = default);

    /// <returns>False when the goal is not the user's or is closed.</returns>
    Task<bool> UpdateAsync(long userId, long goalId, GoalDetails details, CancellationToken ct = default);

    /// <returns>False when the goal is not the user's or is closed.</returns>
    Task<bool> SetAutoSaveAsync(long userId, long goalId, AutoSaveRule? rule, CancellationToken ct = default);

    /// <summary>Closes the goal and its savings account, but only once the account is empty.</summary>
    Task<CloseGoalOutcome> CloseAsync(long userId, long goalId, CancellationToken ct = default);

    /// <summary>Active goals whose auto-save rule watches the given account.</summary>
    Task<IReadOnlyList<AutoSaveTarget>> ListAutoSaveTargetsAsync(long sourceAccountId, CancellationToken ct = default);

    /// <summary>Claims one incoming transfer for one goal; false if that pair was already handled.</summary>
    Task<bool> TryStartAutoSaveAsync(long goalId, long triggerTransferId, DateTime at, CancellationToken ct = default);

    Task CompleteAutoSaveAsync(long goalId, long triggerTransferId, long transferId, CancellationToken ct = default);

    /// <summary>Whether the account is a goal's savings account; those only move money between the owner's own accounts.</summary>
    Task<bool> IsSavingsAccountAsync(long accountId, CancellationToken ct = default);
}

public sealed record GoalDetails(string Name, decimal TargetAmount, DateOnly? TargetDate, int? NewYearOf);

public sealed record AutoSaveRule(long SourceAccountId, string Mode, decimal Value, decimal MinIncoming)
{
    public const string Fixed = "Fixed";
    public const string Percent = "Percent";
}

public enum CloseGoalOutcome
{
    Closed,
    NotFound,
    NotEmpty,
}

public sealed class GoalRow
{
    public long GoalId { get; init; }
    public long AccountId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal TargetAmount { get; init; }
    public decimal SavedAmount { get; init; }
    public DateTime? TargetDate { get; init; }
    public int? NewYearOf { get; init; }
    public string Iban { get; init; } = string.Empty;
    public long? AutoSaveSourceAccountId { get; init; }
    public string? AutoSaveSourceIban { get; init; }
    public string? AutoSaveMode { get; init; }
    public decimal? AutoSaveValue { get; init; }
    public decimal? AutoSaveMinIncoming { get; init; }
}

public sealed class AutoSaveTarget
{
    public long GoalId { get; init; }
    public long UserId { get; init; }
    public long GoalAccountId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal TargetAmount { get; init; }
    public decimal SavedAmount { get; init; }
    public long SourceAccountId { get; init; }
    public string Mode { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public decimal MinIncoming { get; init; }
}
