using System.Globalization;
using Bank.Domain.Common;
using Bank.Domain.ValueObjects;

namespace Bank.Domain.Accounts;

/// <summary>How much may leave a customer's accounts for someone else: in one transfer, and in one business day.</summary>
public sealed record TransferLimit(decimal PerTransfer, decimal Daily);

/// <summary>
/// The internet banking limits on money sent to other people. They cap what a stolen session or a mistake can
/// cost before anyone notices. Moves between the customer's own accounts and goals are not limited, as at a bank.
/// </summary>
public static class TransferLimits
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <param name="sentToday">What the customer already sent to others today in the same currency.</param>
    public static void Ensure(Money amount, decimal sentToday, TransferLimit limit)
    {
        var code = amount.Currency.Code;

        if (amount.Amount > limit.PerTransfer)
            throw new DomainException(
                $"Tek seferde en fazla {Format(limit.PerTransfer)} {code} gönderebilirsiniz.");

        var left = Math.Max(0m, limit.Daily - sentToday);
        if (amount.Amount > left)
            throw new DomainException(
                $"Günlük transfer limitiniz {Format(limit.Daily)} {code}. Bugün en fazla {Format(left)} {code} daha gönderebilirsiniz.");
    }

    private static string Format(decimal value) => value.ToString("N2", Turkish);
}
