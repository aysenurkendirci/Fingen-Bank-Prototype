using Bank.Application.Abstractions.MarketData;
using Bank.Application.Common.Exceptions;
using Bank.Contracts.MarketData;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Features.MarketData;

/// <summary>
/// An indicative conversion between two currencies. A bank's own calculator answers when one is set up, with its
/// exact rounding; otherwise, or when it fails, the conversion is worked out here from today's rates.
/// </summary>
public sealed record ConvertCurrencyQuery(ConvertCurrencyRequest Request) : IRequest<ConversionResponse>;

public sealed class ConvertCurrencyQueryValidator : AbstractValidator<ConvertCurrencyQuery>
{
    public ConvertCurrencyQueryValidator()
        => RuleFor(q => q.Request)
            .Must(r => r.SourceCurrencyCode != r.TargetCurrencyCode)
            .WithMessage("Kaynak ve hedef döviz farklı olmalıdır.")
            .OverridePropertyName("targetCurrencyCode");
}

public sealed class ConvertCurrencyQueryHandler : IRequestHandler<ConvertCurrencyQuery, ConversionResponse>
{
    private readonly IEnumerable<ICurrencyCalculator> _calculators;
    private readonly IExchangeRates _rates;
    private readonly ILogger<ConvertCurrencyQueryHandler> _logger;

    public ConvertCurrencyQueryHandler(IEnumerable<ICurrencyCalculator> calculators, IExchangeRates rates, ILogger<ConvertCurrencyQueryHandler> logger)
    {
        _calculators = calculators;
        _rates = rates;
        _logger = logger;
    }

    public async Task<ConversionResponse> Handle(ConvertCurrencyQuery query, CancellationToken ct)
    {
        var (from, amount, to) = (query.Request.SourceCurrencyCode, query.Request.SourceAmount, query.Request.TargetCurrencyCode);

        foreach (var calculator in _calculators)
        {
            try
            {
                return ToResponse(await calculator.QuoteAsync(from, amount, to, ct));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Currency calculator {Calculator} failed; using the published rates", calculator.Name);
            }
        }

        var snapshot = await _rates.GetAsync(ct);
        return ToResponse(RateConversion.Convert(snapshot, from, amount, to));
    }

    private static ConversionResponse ToResponse(ConversionQuote q)
        => new(q.SourceCurrency, q.SourceAmount, q.TargetCurrency, q.TargetAmount, q.Rate, q.RateDate, q.Source);
}

/// <summary>
/// A conversion worked out from published rates, the way a bank would price it for the customer: the lira a
/// customer pays for a currency is at the selling rate, the lira a customer gets for one at the buying rate, and
/// between two foreign currencies the lira sits in the middle. What the customer receives is rounded down to the
/// cent, never up.
/// </summary>
public static class RateConversion
{
    public const string Lira = "TRY";

    public static ConversionQuote Convert(ExchangeRateSnapshot snapshot, string from, decimal amount, string to)
    {
        decimal Rate(string code, Func<ExchangeRate, decimal> side)
            => snapshot.Rates.FirstOrDefault(r => r.CurrencyCode == code) is { } rate
                ? side(rate)
                : throw new BadRequestException($"{code} için güncel kur yok.");

        decimal target, rate;
        if (from == Lira)
        {
            rate = Rate(to, r => r.Selling);
            target = amount / rate;
        }
        else if (to == Lira)
        {
            rate = Rate(from, r => r.Buying);
            target = amount * rate;
        }
        else
        {
            var lira = amount * Rate(from, r => r.Buying);
            rate = Rate(to, r => r.Selling);
            target = lira / rate;
        }

        var published = snapshot.PublishedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new ConversionQuote(from, amount, to, Math.Floor(target * 100m) / 100m, rate, published, snapshot.Source);
    }
}
