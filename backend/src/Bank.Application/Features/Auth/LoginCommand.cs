using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common;
using Bank.Application.Common.Exceptions;
using Bank.Application.Features.Notifications;
using Bank.Contracts.Auth;
using Bank.Domain.Security;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Features.Auth;

public sealed record LoginCommand(LoginRequest Credentials) : IRequest<AuthResult>;

/// <summary>
/// Signs a customer in, in up to two steps: the password, then, for a customer who switched it on, a code from an
/// authenticator app.
/// </summary>
/// <remarks>
/// <para>
/// Every sign-in attempt is logged as a security event so brute force and credential stuffing can be spotted. The
/// client always gets the same "wrong identity number or password" answer, but the log keeps the real reason.
/// Identity numbers are masked before they reach the log.
/// </para>
/// <para>
/// Wrong passwords and wrong codes count towards a lock (<see cref="FailedPasswordGuard"/>): after
/// <see cref="SignInPolicy.MaxFailures"/> in a row the account is locked for <see cref="SignInPolicy.LockFor"/>, and while it is locked even the right password
/// does not sign in. The customer is told in the notification centre. A lock tells a guesser that the identity
/// number exists; banks accept that, because without the lock the password could be guessed at leisure from many
/// addresses.
/// </para>
/// <para>
/// Known limit: the second factor is an authenticator app code and it is optional. BDDK expects every internet
/// banking sign-in to have a second factor; making it mandatory, and offering an SMS code, is a policy switch and
/// an SMS provider away.
/// </para>
/// </remarks>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    // A real hash of a password nobody has, made once. An unknown identity number is checked against it, so it
    // costs the same time as a known one and the answer's timing does not tell which numbers are registered.
    private static string? _decoyHash;

    private readonly IAuthRepository _repo;
    private readonly IUserSecurityRepository _security;
    private readonly IPasswordHasher _hasher;
    private readonly ISecretProtector _secrets;
    private readonly ITokenGenerator _tokens;
    private readonly RefreshTokenIssuer _refreshTokens;
    private readonly Notices _notices;
    private readonly FailedPasswordGuard _failures;
    private readonly TimeProvider _time;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IAuthRepository repo,
        IUserSecurityRepository security,
        IPasswordHasher hasher,
        ISecretProtector secrets,
        ITokenGenerator tokens,
        RefreshTokenIssuer refreshTokens,
        Notices notices,
        FailedPasswordGuard failures,
        TimeProvider time,
        ILogger<LoginCommandHandler> logger)
    {
        _repo = repo;
        _security = security;
        _hasher = hasher;
        _secrets = secrets;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _notices = notices;
        _failures = failures;
        _time = time;
        _logger = logger;
    }

    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken ct)
    {
        var credentials = command.Credentials;
        var maskedTc = PersonalData.MaskTcKimlikNo(credentials.TcNo);
        var now = _time.GetUtcNow();

        var user = await _repo.GetUserByTcOrDefaultAsync(credentials.TcNo, ct);
        if (user is null)
        {
            _hasher.Verify(credentials.Password, _decoyHash ??= _hasher.Hash(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))));
            _logger.LogWarning("Login failed for {TcNo}: unknown identity number", maskedTc);
            throw new AuthenticationFailedException();
        }

        var security = await _security.GetAsync(user.UserId, ct);
        if (security.LockedUntil is { } lockedUntil && lockedUntil > now.UtcDateTime)
        {
            _logger.LogWarning("Login refused for user {UserId} ({TcNo}): locked until {LockedUntil}", user.UserId, maskedTc, lockedUntil);
            throw new AccountLockedException(lockedUntil);
        }

        var stored = await _repo.GetCredentialsAsync(user.UserId, ct);
        if (!_hasher.Verify(credentials.Password, stored.PasswordHash))
        {
            _logger.LogWarning("Login failed for user {UserId} ({TcNo}): wrong password", user.UserId, maskedTc);
            await _failures.RecordAsync(user.UserId, ct);
            throw new AuthenticationFailedException();
        }

        if (security.TotpSecret is { } protectedSecret)
        {
            if (string.IsNullOrWhiteSpace(credentials.Code))
                throw new SecondFactorRequiredException();

            var secret = _secrets.Unprotect(protectedSecret, out var writtenWithOldKey);
            var step = Totp.Verify(secret, credentials.Code, now);
            if (step is null || !await _security.TryUseStepAsync(user.UserId, step.Value, ct))
            {
                _logger.LogWarning("Login failed for user {UserId} ({TcNo}): wrong or reused second factor code", user.UserId, maskedTc);
                await _failures.RecordAsync(user.UserId, ct);
                throw new SecondFactorFailedException();
            }

            // After an encryption key rotation the secret moves to the new key the first time it is used.
            if (writtenWithOldKey)
                await _security.ReplaceSecretAsync(user.UserId, _secrets.Protect(secret), ct);
        }

        await _failures.ClearAsync(user.UserId, ct);
        await _repo.UpdateLastLoginAsync(user.UserId, ct);
        await _notices.SignedInAsync(user.UserId, ct);

        _logger.LogInformation("User {UserId} logged in{Factor}", user.UserId, security.SecondFactorEnabled ? " with a second factor" : string.Empty);
        return await AuthResponses.CreateAsync(user.UserId, $"{user.FirstName} {user.LastName}", _tokens, _refreshTokens, ct);
    }
}
