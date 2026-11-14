using Bank.Application.Abstractions.MarketData;

namespace Bank.Infrastructure.MarketData;

/// <summary>
/// One place rates can be fetched from. Sources are tried in the order they are registered, so a bank's API can
/// sit in front of TCMB and TCMB still answers when the bank does not.
/// </summary>
public interface IExchangeRateSource
{
    string Name { get; }

    Task<ExchangeRateSnapshot> FetchAsync(CancellationToken ct);
}
