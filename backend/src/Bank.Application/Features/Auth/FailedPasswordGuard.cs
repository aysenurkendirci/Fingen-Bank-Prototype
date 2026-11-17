using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Features.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Features.Auth;

/// <summary>
/// Counts a wrong password or code wherever one is asked for: at sign-in, and inside a session before changing the
/// password, switching the second factor off or erasing the account. Someone holding a stolen session therefore
/// cannot guess the password at leisure either. At the limit the account locks, every session ends, and the
/// customer is told.
/// </summary>
public sealed class FailedPasswordGuard
{
    private readonly IUserSecurityRepository _security;
    private readonly IRefreshTokenRepository _sessions;
    private readonly Notices _notices;
    private readonly SignInPolicy _policy;
    private readonly TimeProvider _time;
    private readonly ILogger<FailedPasswordGuard> _logger;

    public FailedPasswordGuard(
        IUserSecurityRepository security,
        IRefreshTokenRepository sessions,
        Notices notices,
        IOptions<SignInPolicy> policy,
        TimeProvider time,
        ILogger<FailedPasswordGuard> logger)
    {
        _security = security;
        _sessions = sessions;
        _notices = notices;
        _policy = policy.Value;
        _time = time;
        _logger = logger;
    }

    public async Task RecordAsync(long userId, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        if (await _security.RecordFailureAsync(userId, _policy.MaxFailures, now, _policy.LockFor, ct) is not { } lockedUntil)
            return;

        _logger.LogWarning("User {UserId} locked until {LockedUntil} after {Failures} wrong passwords or codes", userId, lockedUntil, _policy.MaxFailures);
        await _sessions.RevokeAllAsync(userId, now, ct);
        await _notices.AccountLockedAsync(userId, ct);
    }

    public Task ClearAsync(long userId, CancellationToken ct) => _security.ClearFailuresAsync(userId, ct);

    /// <summary>Refuses to check another password while the account is locked.</summary>
    public async Task EnsureNotLockedAsync(long userId, CancellationToken ct)
    {
        if ((await _security.GetAsync(userId, ct)).LockedUntil is { } lockedUntil && lockedUntil > _time.GetUtcNow().UtcDateTime)
            throw new Common.Exceptions.AccountLockedException(lockedUntil);
    }
}
