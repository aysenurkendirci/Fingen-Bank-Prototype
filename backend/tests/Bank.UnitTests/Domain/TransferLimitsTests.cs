using Bank.Domain.Accounts;
using Bank.Domain.Common;
using Bank.Domain.ValueObjects;

namespace Bank.UnitTests.Domain;

public sealed class TransferLimitsTests
{
    private static readonly TransferLimit Limit = new(PerTransfer: 1_000m, Daily: 1_500m);

    private static Money Try(decimal amount) => new(amount, Currency.TRY);

    [Fact]
    public void ATransferWithinBothLimits_IsAllowed()
        => TransferLimits.Ensure(Try(1_000m), sentToday: 500m, Limit);

    [Fact]
    public void MoreThanThePerTransferLimit_IsRefused()
    {
        var error = Assert.Throws<DomainException>(() => TransferLimits.Ensure(Try(1_000.01m), 0m, Limit));

        Assert.Contains("Tek seferde en fazla 1.000,00 TRY", error.Message);
    }

    [Fact]
    public void GoingPastTheDailyLimit_IsRefused_AndSaysWhatIsLeft()
    {
        var error = Assert.Throws<DomainException>(() => TransferLimits.Ensure(Try(600m), sentToday: 1_000m, Limit));

        Assert.Contains("Bugün en fazla 500,00 TRY", error.Message);
    }

    [Fact]
    public void AfterTheDailyLimitIsUsedUp_NothingMoreGoes()
        => Assert.Throws<DomainException>(() => TransferLimits.Ensure(Try(0.01m), sentToday: 1_500m, Limit));
}
