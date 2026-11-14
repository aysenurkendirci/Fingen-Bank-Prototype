using Bank.Application.Features.Savings;

namespace Bank.UnitTests.Savings;

public sealed class AutoSavePlanTests
{
    [Fact]
    public void APercentage_IsTakenFromTheIncomingMoney_RoundedDown()
        => Assert.Equal(1_234.56m, AutoSavePlan.AmountFor(12_345.67m, "Percent", 10m, 0m, 1_000_000m));

    [Fact]
    public void AFixedAmount_IsTakenAsIs()
        => Assert.Equal(1_000m, AutoSavePlan.AmountFor(20_000m, "Fixed", 1_000m, 0m, 1_000_000m));

    [Fact]
    public void MoneyBelowTheThreshold_IsLeftAlone()
        => Assert.Equal(0m, AutoSavePlan.AmountFor(9_999.99m, "Fixed", 1_000m, 10_000m, 1_000_000m));

    [Fact]
    public void NothingIsSavedPastTheTarget()
        => Assert.Equal(250m, AutoSavePlan.AmountFor(20_000m, "Fixed", 1_000m, 0m, 250m));

    [Fact]
    public void AFixedAmountLargerThanTheIncomingMoney_TakesOnlyWhatArrived()
        => Assert.Equal(300m, AutoSavePlan.AmountFor(300m, "Fixed", 1_000m, 0m, 1_000_000m));

    [Fact]
    public void TheMonthlyNeed_CountsTheCurrentMonth()
        => Assert.Equal(1_000m, AutoSavePlan.MonthlyNeeded(12_000m, 0m, new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 15)));

    [Fact]
    public void AReachedGoal_NeedsNothingMore()
        => Assert.Null(AutoSavePlan.MonthlyNeeded(5_000m, 5_000m, new DateOnly(2027, 1, 1), new DateOnly(2026, 11, 14)));
}
