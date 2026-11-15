using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common.Exceptions;
using Bank.Application.Features.Notifications;
using Bank.Contracts.Cards;
using Bank.Domain.Cards;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Features.Cards;

/// <summary>Temporarily blocks a card or opens it again, e.g. while the customer looks for a misplaced card.</summary>
public sealed record SetCardBlockedCommand(long CardId, bool Blocked) : IRequest<CardSettingsResponse>;

public sealed class SetCardBlockedCommandHandler : IRequestHandler<SetCardBlockedCommand, CardSettingsResponse>
{
    private readonly IBankCardRepository _cards;
    private readonly ICurrentUser _currentUser;
    private readonly Notices _notices;
    private readonly ILogger<SetCardBlockedCommandHandler> _logger;

    public SetCardBlockedCommandHandler(IBankCardRepository cards, ICurrentUser currentUser, Notices notices, ILogger<SetCardBlockedCommandHandler> logger)
    {
        _cards = cards;
        _currentUser = currentUser;
        _notices = notices;
        _logger = logger;
    }

    public async Task<CardSettingsResponse> Handle(SetCardBlockedCommand command, CancellationToken ct)
    {
        var card = await _cards.GetAsync(_currentUser.UserId, command.CardId, ct)
                   ?? throw new NotFoundException(BankCardMapping.NotFoundMessage);

        var wasBlocked = card.Status == BankCardStatus.Blocked;
        if (command.Blocked)
            card.Block();
        else
            card.Unblock();

        await _cards.UpdateAsync(card, ct);

        // A card being blocked or reopened is a security event support staff may need to trace.
        _logger.LogInformation("User {UserId} {Action} card {CardId}",
            _currentUser.UserId, command.Blocked ? "blocked" : "unblocked", card.Id);
        // Pressing the same switch twice is not news.
        if (wasBlocked != command.Blocked)
            await _notices.CardBlockedAsync(_currentUser.UserId, command.Blocked, ct);

        return BankCardMapping.ToResponse(card);
    }
}
