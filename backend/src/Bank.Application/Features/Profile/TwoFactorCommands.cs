using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common;
using Bank.Application.Common.Exceptions;
using Bank.Application.Features.Auth;
using Bank.Application.Features.Notifications;
using Bank.Contracts.Account;
using Bank.Domain.Security;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Features.Profile;

public sealed record GetTwoFactorStatusQuery : IRequest<TwoFactorStatusResponse>;

/// <summary>Creates a new authenticator key for the customer to add to their app. Nothing changes until it is confirmed.</summary>
public sealed record StartTwoFactorSetupCommand : IRequest<TwoFactorSetupResponse>;

/// <summary>Switches the second factor on once a code from the new key is right.</summary>
public sealed record ConfirmTwoFactorCommand(string Code) : IRequest<TwoFactorStatusResponse>;

/// <summary>Switches the second factor off; needs the password and a current code.</summary>
public sealed record DisableTwoFactorCommand(string Password, string Code) : IRequest<TwoFactorStatusResponse>;

public sealed class TwoFactorHandlers :
    IRequestHandler<GetTwoFactorStatusQuery, TwoFactorStatusResponse>,
    IRequestHandler<StartTwoFactorSetupCommand, TwoFactorSetupResponse>,
    IRequestHandler<ConfirmTwoFactorCommand, TwoFactorStatusResponse>,
    IRequestHandler<DisableTwoFactorCommand, TwoFactorStatusResponse>
{
    private readonly IUserSecurityRepository _security;
    private readonly IAuthRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISecretProtector _secrets;
    private readonly Notices _notices;
    private readonly FailedPasswordGuard _failures;
    private readonly ICurrentUser _currentUser;
    private readonly SignInPolicy _policy;
    private readonly TimeProvider _time;
    private readonly ILogger<TwoFactorHandlers> _logger;

    public TwoFactorHandlers(
        IUserSecurityRepository security,
        IAuthRepository users,
        IPasswordHasher hasher,
        ISecretProtector secrets,
        Notices notices,
        FailedPasswordGuard failures,
        ICurrentUser currentUser,
        IOptions<SignInPolicy> policy,
        TimeProvider time,
        ILogger<TwoFactorHandlers> logger)
    {
        _security = security;
        _users = users;
        _hasher = hasher;
        _secrets = secrets;
        _notices = notices;
        _failures = failures;
        _currentUser = currentUser;
        _policy = policy.Value;
        _time = time;
        _logger = logger;
    }

    private long UserId => _currentUser.UserId;

    public async Task<TwoFactorStatusResponse> Handle(GetTwoFactorStatusQuery query, CancellationToken ct)
        => Status(await _security.GetAsync(UserId, ct));

    public async Task<TwoFactorSetupResponse> Handle(StartTwoFactorSetupCommand command, CancellationToken ct)
    {
        if ((await _security.GetAsync(UserId, ct)).SecondFactorEnabled)
            throw new ConflictException("İki adımlı doğrulama zaten açık. Yeni bir anahtar için önce kapatın.");

        var secret = Totp.NewSecret();
        await _security.SavePendingSecretAsync(UserId, _secrets.Protect(secret), ct);

        // The app shows the account next to the code; a masked identity number says whose it is without giving it away.
        var profile = await _users.GetProfileAsync(UserId, ct);
        return new TwoFactorSetupResponse(Base32.Encode(secret), Totp.SetupUri(secret, _policy.Issuer, PersonalData.MaskTcKimlikNo(profile.TcNo)));
    }

    public async Task<TwoFactorStatusResponse> Handle(ConfirmTwoFactorCommand command, CancellationToken ct)
    {
        var security = await _security.GetAsync(UserId, ct);
        if (security.TotpPendingSecret is not { } pending)
            throw new BadRequestException("Önce yeni bir anahtar oluşturun.");

        await _failures.EnsureNotLockedAsync(UserId, ct);
        if (Totp.Verify(_secrets.Unprotect(pending), command.Code, _time.GetUtcNow()) is not { } step)
        {
            // Six digits can be guessed; wrong codes while setting up count like any other.
            await _failures.RecordAsync(UserId, ct);
            throw new SecondFactorFailedException();
        }
        if (!await _security.EnableSecretAsync(UserId, step, _time.GetUtcNow().UtcDateTime, ct))
            throw new ConflictException("Anahtar bu arada değişti. Kurulumu yeniden başlatın.");

        _logger.LogInformation("User {UserId} switched on the second factor", UserId);
        await _notices.SecondFactorChangedAsync(UserId, enabled: true, ct);
        return Status(await _security.GetAsync(UserId, ct));
    }

    public async Task<TwoFactorStatusResponse> Handle(DisableTwoFactorCommand command, CancellationToken ct)
    {
        var security = await _security.GetAsync(UserId, ct);
        if (security.TotpSecret is not { } secret)
            return Status(security);

        await _failures.EnsureNotLockedAsync(UserId, ct);
        var stored = await _users.GetCredentialsAsync(UserId, ct);
        if (!_hasher.Verify(command.Password, stored.PasswordHash))
        {
            await _failures.RecordAsync(UserId, ct);
            throw new BadRequestException("Mevcut şifre doğrulanamadı.");
        }

        var step = Totp.Verify(_secrets.Unprotect(secret), command.Code, _time.GetUtcNow());
        if (step is null || !await _security.TryUseStepAsync(UserId, step.Value, ct))
        {
            await _failures.RecordAsync(UserId, ct);
            throw new SecondFactorFailedException();
        }

        await _security.DisableSecretAsync(UserId, ct);
        _logger.LogWarning("User {UserId} switched off the second factor", UserId);
        await _notices.SecondFactorChangedAsync(UserId, enabled: false, ct);
        return Status(await _security.GetAsync(UserId, ct));
    }

    private static TwoFactorStatusResponse Status(UserSecurityRow row) => new(row.SecondFactorEnabled, row.TotpEnabledAt);
}
