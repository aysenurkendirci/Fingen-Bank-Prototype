using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.MarketData;

/// <summary>How much of one currency an amount of another buys. TRY is the lira.</summary>
public sealed record ConvertCurrencyRequest(
    [Required(ErrorMessage = "Kaynak döviz zorunludur.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Döviz kodu üç büyük harf olmalıdır.")]
    string SourceCurrencyCode,

    [Range(typeof(decimal), "0.01", "1000000000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    decimal SourceAmount,

    [Required(ErrorMessage = "Hedef döviz zorunludur.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Döviz kodu üç büyük harf olmalıdır.")]
    string TargetCurrencyCode
);

/// <param name="Rate">Lira per unit of the foreign currency the conversion used.</param>
/// <param name="Source">Who calculated it: the bank's own calculator, or this app from the published rates.</param>
public sealed record ConversionResponse(
    string SourceCurrencyCode,
    decimal SourceAmount,
    string TargetCurrencyCode,
    decimal TargetAmount,
    decimal Rate,
    DateTime? RateDate,
    string Source);
