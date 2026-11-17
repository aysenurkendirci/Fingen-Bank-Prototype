namespace Bank.Application.Abstractions.Repositories;

/// <summary>
/// Failed sign-ins, the lock they lead to, and the second factor. Every change is a single statement, so two
/// sign-ins at the same moment cannot both slip under the failure limit or both use the same code.
/// </summary>
public interface IUserSecurityRepository
{
    /// <summary>The customer's sign-in state; the defaults (no failures, no factor) if nothing was ever stored.</summary>
    Task<UserSecurityRow> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>Counts a failure; at <paramref name="maxFailures"/> the account locks and the count starts over.</summary>
    /// <returns>When the account is now locked until, or null if this failure did not lock it.</returns>
    Task<DateTime?> RecordFailureAsync(long userId, int maxFailures, DateTime now, TimeSpan lockFor, CancellationToken ct = default);

    Task ClearFailuresAsync(long userId, CancellationToken ct = default);

    /// <summary>Stores a new secret that is not in use until the customer proves it with a code.</summary>
    Task SavePendingSecretAsync(long userId, byte[] protectedSecret, CancellationToken ct = default);

    /// <summary>Puts the pending secret in use. <paramref name="step"/> is the code that proved it, so it cannot sign in.</summary>
    Task<bool> EnableSecretAsync(long userId, long step, DateTime now, CancellationToken ct = default);

    Task DisableSecretAsync(long userId, CancellationToken ct = default);

    /// <summary>Claims a code's time step. False when this or a later code was already used: a replayed code.</summary>
    Task<bool> TryUseStepAsync(long userId, long step, CancellationToken ct = default);
}

public sealed class UserSecurityRow
{
    public int FailedSignIns { get; init; }
    public DateTime? LockedUntil { get; init; }
    public byte[]? TotpPendingSecret { get; init; }
    public byte[]? TotpSecret { get; init; }
    public DateTime? TotpEnabledAt { get; init; }

    public bool SecondFactorEnabled => TotpSecret is not null;
}
