using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common;
using Bank.Contracts.Savings;
using MediatR;

namespace Bank.Application.Features.Savings;

/// <summary>The customer's open goals with what is saved in each.</summary>
public sealed record ListGoalsQuery : IRequest<IReadOnlyList<GoalResponse>>;

public sealed class ListGoalsQueryHandler : IRequestHandler<ListGoalsQuery, IReadOnlyList<GoalResponse>>
{
    private readonly ISavingsGoalRepository _goals;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public ListGoalsQueryHandler(ISavingsGoalRepository goals, ICurrentUser currentUser, TimeProvider time)
    {
        _goals = goals;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<IReadOnlyList<GoalResponse>> Handle(ListGoalsQuery query, CancellationToken ct)
    {
        var today = BusinessDate.Today(_time);
        return (await _goals.ListAsync(_currentUser.UserId, ct)).Select(g => GoalMapping.ToResponse(g, today)).ToList();
    }
}

internal static class GoalMapping
{
    public static GoalResponse ToResponse(GoalRow g, DateOnly today)
    {
        var targetDate = g.TargetDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;

        var autoSave = g.AutoSaveSourceAccountId is { } source
            ? new AutoSaveResponse(source, g.AutoSaveSourceIban ?? string.Empty, g.AutoSaveMode!, g.AutoSaveValue!.Value, g.AutoSaveMinIncoming!.Value)
            : null;

        return new GoalResponse(
            g.GoalId,
            g.Name,
            g.TargetAmount,
            g.SavedAmount,
            targetDate,
            g.NewYearOf,
            g.Iban,
            AutoSavePlan.MonthlyNeeded(g.TargetAmount, g.SavedAmount, targetDate, today),
            autoSave,
            g.Category,
            g.Details,
            g.PhotoUpdatedAt);
    }
}
