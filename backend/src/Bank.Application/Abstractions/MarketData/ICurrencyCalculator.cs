namespace Bank.Application.Abstractions.MarketData;

/// <summary>A bank's own calculator: how much of one currency a given amount of another buys, at the bank's rates.</summary>
public interface ICurrencyCalculator
{
    string Name { get; }

    /// <exception cref="Exception">The bank did not answer, or answered with something unusable.</exception>
    Task<ConversionQuote> QuoteAsync(string sourceCurrency, decimal sourceAmount, string targetCurrency, CancellationToken ct);
}

/// <param name="Rate">Lira per unit of the currency the customer buys or sells, as the quote used it.</param>
/// <param name="Source">Who calculated it, shown next to the result.</param>
public sealed record ConversionQuote(
    string SourceCurrency,
    decimal SourceAmount,
    string TargetCurrency,
    decimal TargetAmount,
    decimal Rate,
    DateTime? RateDate,
    string Source);
