using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common.Behaviors;
using Bank.Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Features.Accounts;

/// <summary>
/// Closes one of the customer's accounts. As at a branch: the balance must be zero, a goal's savings account is
/// closed with its goal, and the customer keeps at least one lira demand account for money to arrive on.
/// </summary>
public sealed record CloseAccountCommand(long AccountId) : IRequest, ITransactionalCommand;

public sealed class CloseAccountCommandHandler : IRequestHandler<CloseAccountCommand>
{
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CloseAccountCommandHandler> _logger;

    public CloseAccountCommandHandler(IAccountRepository accounts, ICurrentUser currentUser, ILogger<CloseAccountCommandHandler> logger)
    {
        _accounts = accounts;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(CloseAccountCommand command, CancellationToken ct)
    {
        var outcome = await _accounts.CloseAsync(_currentUser.UserId, command.AccountId, ct);

        switch (outcome)
        {
            case CloseAccountOutcome.NotFound:
                throw new NotFoundException("Hesap bulunamadı.");
            case CloseAccountOutcome.NotActive:
                throw new ConflictException("Hesap zaten işleme kapalı.");
            case CloseAccountOutcome.HasBalance:
                throw new ConflictException("Bakiyesi olan hesap kapatılamaz. Önce parayı başka bir hesabınıza aktarın.");
            case CloseAccountOutcome.IsSavings:
                throw new BadRequestException("Kumbara hesabı, hedefi kapatıldığında kendiliğinden kapanır.");
            case CloseAccountOutcome.LastDemandAccount:
                throw new ConflictException("Son vadesiz TL hesabınız kapatılamaz; gelen paranın yatacağı bir hesap gerekir.");
        }

        _logger.LogInformation("User {UserId} closed account {AccountId}", _currentUser.UserId, command.AccountId);
    }
}
