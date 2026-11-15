using Bank.Application.Abstractions.Repositories;
using Bank.Application.Features.Notifications;
using Bank.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Features.Savings;

/// <summary>Money arrived on an account. Raised inside the transfer's transaction.</summary>
public sealed record IncomingTransferPosted(long TransferId, long TargetAccountId, decimal Amount) : INotification;

/// <summary>
/// Puts part of incoming money aside as soon as it arrives, for every goal whose rule watches the account. Runs in
/// the same transaction as the transfer, so the money is never seen as spendable first. A rule that cannot be
/// carried out (the goal is reached, the account was emptied meanwhile) is skipped; it never blocks the payment.
/// </summary>
public sealed class AutoSaveOnIncomingTransfer : INotificationHandler<IncomingTransferPosted>
{
    private readonly ISavingsGoalRepository _goals;
    private readonly SavingsTransfers _transfers;
    private readonly Notices _notices;
    private readonly TimeProvider _time;
    private readonly ILogger<AutoSaveOnIncomingTransfer> _logger;

    public AutoSaveOnIncomingTransfer(
        ISavingsGoalRepository goals, SavingsTransfers transfers, Notices notices, TimeProvider time, ILogger<AutoSaveOnIncomingTransfer> logger)
    {
        _goals = goals;
        _transfers = transfers;
        _notices = notices;
        _time = time;
        _logger = logger;
    }

    public async Task Handle(IncomingTransferPosted posted, CancellationToken ct)
    {
        foreach (var goal in await _goals.ListAutoSaveTargetsAsync(posted.TargetAccountId, ct))
        {
            var amount = AutoSavePlan.AmountFor(posted.Amount, goal.Mode, goal.Value, goal.MinIncoming, goal.TargetAmount - goal.SavedAmount);
            if (amount <= 0)
                continue;

            // One incoming transfer can feed a goal once, however often this runs.
            if (!await _goals.TryStartAutoSaveAsync(goal.GoalId, posted.TransferId, _time.GetUtcNow().UtcDateTime, ct))
                continue;

            try
            {
                var moved = await _transfers.MoveAsync(
                    goal.UserId, goal.SourceAccountId, goal.GoalAccountId, amount,
                    $"Otomatik birikim: {goal.Name}", $"autosave:{goal.GoalId}:{posted.TransferId}", ct);
                await _goals.CompleteAutoSaveAsync(goal.GoalId, posted.TransferId, moved.TransferId, ct);

                await _notices.AutoSavedAsync(goal.UserId, amount, goal.Name, ct);
                if (goal.SavedAmount + amount >= goal.TargetAmount)
                    await _notices.GoalReachedAsync(goal.UserId, goal.Name, goal.TargetAmount, ct);

                _logger.LogInformation(
                    "Auto-save moved {Amount} to goal {GoalId} after transfer {TransferId}", amount, goal.GoalId, posted.TransferId);
            }
            catch (DomainException ex)
            {
                _logger.LogInformation("Auto-save for goal {GoalId} skipped: {Reason}", goal.GoalId, ex.Message);
            }
        }
    }
}
