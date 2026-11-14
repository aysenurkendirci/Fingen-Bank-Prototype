using Bank.Application.Abstractions.Repositories;

namespace Bank.Application.Features.Savings;

/// <summary>How much an auto-save rule takes from a piece of incoming money.</summary>
public static class AutoSavePlan
{
    /// <param name="incoming">The amount that just arrived on the watched account.</param>
    /// <param name="stillNeeded">What is left to reach the goal; nothing is saved past the target.</param>
    /// <returns>The amount to move, rounded down to the kuruş; zero when the rule does not apply.</returns>
    public static decimal AmountFor(decimal incoming, string mode, decimal value, decimal minIncoming, decimal stillNeeded)
    {
        if (incoming <= 0 || incoming < minIncoming || stillNeeded <= 0)
            return 0m;

        var wanted = mode == AutoSaveRule.Percent
            ? Math.Floor(incoming * value) / 100m
            : value;

        return Math.Max(0m, Math.Min(Math.Min(wanted, incoming), stillNeeded));
    }

    /// <summary>What would have to go in each month, counting the current month, to reach the target by its date.</summary>
    public static decimal? MonthlyNeeded(decimal target, decimal saved, DateOnly? targetDate, DateOnly today)
    {
        var remaining = target - saved;
        if (targetDate is not { } date || remaining <= 0)
            return null;

        var months = Math.Max(1, (date.Year - today.Year) * 12 + date.Month - today.Month + 1);
        return Math.Ceiling(remaining / months * 100m) / 100m;
    }
}
