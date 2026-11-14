using Bank.Domain.Accounts;

namespace Bank.Application.Common;

/// <summary>
/// The bank's operating limits, read from configuration ("Banking"). The defaults are typical of Turkish retail
/// internet banking; a deployment sets its own.
/// </summary>
public sealed class BankingRules
{
    public const string Section = "Banking";

    /// <summary>Limits on money sent to other people, per currency. A currency without an entry uses the TRY one.</summary>
    public Dictionary<string, TransferLimit> TransferLimits { get; init; } = new()
    {
        ["TRY"] = new TransferLimit(PerTransfer: 250_000m, Daily: 750_000m),
        ["USD"] = new TransferLimit(PerTransfer: 25_000m, Daily: 75_000m),
        ["EUR"] = new TransferLimit(PerTransfer: 25_000m, Daily: 75_000m),
    };

    /// <summary>Open accounts a customer may hold in one currency, savings accounts of goals not counted.</summary>
    public int MaxAccountsPerCurrency { get; init; } = 5;

    public TransferLimit LimitFor(string currencyCode)
        => TransferLimits.TryGetValue(currencyCode, out var limit) ? limit : TransferLimits["TRY"];
}
