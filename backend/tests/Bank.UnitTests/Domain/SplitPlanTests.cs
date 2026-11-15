using Bank.Domain.Common;
using Bank.Domain.Splits;

namespace Bank.UnitTests.Domain;

public sealed class SplitPlanTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    [Fact]
    public void ThePayersPart_IsWhatIsLeftOfTheTotal()
        => Assert.Equal(400m, SplitPlan.Check(1_000m, [300m, 300m], Today, Today));

    [Fact]
    public void ThePayer_CanPayForEveryoneElse()
        => Assert.Equal(0m, SplitPlan.Check(600m, [300m, 300m], Today, Today));

    [Theory]
    [InlineData(100, new[] { 60.0, 50.0 }, "aşıyor")]
    [InlineData(100, new[] { 0.0 }, "sıfırdan büyük")]
    [InlineData(100, new[] { 10.005 }, "iki ondalık")]
    [InlineData(0, new[] { 10.0 }, "Toplam tutar")]
    public void SharesThatDoNotFit_AreRefused(double total, double[] shares, string message)
    {
        var e = Assert.Throws<DomainException>(
            () => SplitPlan.Check((decimal)total, shares.Select(s => (decimal)s).ToList(), Today, Today));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void NobodyToSplitWith_OrTooMany_IsRefused()
    {
        Assert.Throws<DomainException>(() => SplitPlan.Check(100m, [], Today, Today));
        Assert.Throws<DomainException>(() => SplitPlan.Check(1_000m, Enumerable.Repeat(1m, SplitPlan.MaxShares + 1).ToList(), Today, Today));
    }

    [Fact]
    public void AnExpense_IsNeitherInTheFutureNorOverAYearOld()
    {
        Assert.Throws<DomainException>(() => SplitPlan.Check(100m, [50m], Today.AddDays(1), Today));
        Assert.Throws<DomainException>(() => SplitPlan.Check(100m, [50m], Today.AddDays(-SplitPlan.MaxAgeDays - 1), Today));
        Assert.Equal(50m, SplitPlan.Check(100m, [50m], Today.AddDays(-SplitPlan.MaxAgeDays), Today));
    }

    [Fact]
    public void AnEvenSplit_LeavesTheOddKurusWithThePayer()
    {
        var shares = SplitPlan.Evenly(100m, 2);

        Assert.Equal([33.33m, 33.33m], shares);
        Assert.Equal(33.34m, SplitPlan.Check(100m, shares.ToList(), Today, Today));
    }
}
