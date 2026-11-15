using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.Savings;

/// <summary>A savings goal and the money already put aside for it, which sits in the goal's own savings account.</summary>
/// <param name="NewYearOf">Set when the goal is a new year's resolution: the year it is meant for.</param>
/// <param name="MonthlyNeeded">What would have to go in each month from now to reach the target on time; null without a date or once reached.</param>
/// <param name="Category">What kind of wish it is, e.g. <c>car</c> or <c>travel</c>; null if not said.</param>
/// <param name="Details">The wish in the customer's own words: the model, the colour, where to.</param>
/// <param name="PhotoUpdatedAt">When the photo was last changed; null without a photo. Also tells the app when to fetch it again.</param>
public sealed record GoalResponse(
    long Id,
    string Name,
    decimal TargetAmount,
    decimal SavedAmount,
    DateOnly? TargetDate,
    int? NewYearOf,
    string Iban,
    decimal? MonthlyNeeded,
    AutoSaveResponse? AutoSave,
    string? Category,
    string? Details,
    DateTime? PhotoUpdatedAt
);

/// <param name="Mode"><c>Fixed</c>: Value is an amount. <c>Percent</c>: Value is a share of the incoming money.</param>
/// <param name="MinIncoming">Only money arriving in at least this amount triggers the rule, e.g. a salary.</param>
public sealed record AutoSaveResponse(long SourceAccountId, string SourceIban, string Mode, decimal Value, decimal MinIncoming);

public sealed record GoalRequest(
    [Required(ErrorMessage = "Hedefin adı zorunludur.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Hedefin adı 2 ile 80 karakter arasında olmalıdır.")]
    string Name,

    [Range(typeof(decimal), "1", "1000000000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Hedef tutar 1 ile 1.000.000.000 arasında olmalıdır.")]
    decimal TargetAmount,

    DateOnly? TargetDate,

    int? NewYearOf,

    [AllowedValues(null, "car", "home", "travel", "education", "tech", "wedding", "other", ErrorMessage = "Hedef türü geçerli değil.")]
    string? Category = null,

    [StringLength(300, ErrorMessage = "Ayrıntılar en fazla 300 karakter olabilir.")]
    string? Details = null
);

/// <summary>Money moved between one of the customer's accounts and a goal.</summary>
public sealed record GoalMoneyRequest(
    [Required(ErrorMessage = "Hesap zorunludur.")]
    long? AccountId,

    [Range(typeof(decimal), "0.01", "999999999",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    decimal Amount
);

public sealed record AutoSaveRequest(
    [Required(ErrorMessage = "Kaynak hesap zorunludur.")]
    long? SourceAccountId,

    [Required(ErrorMessage = "Kural türü zorunludur.")]
    [AllowedValues("Fixed", "Percent", ErrorMessage = "Kural türü Fixed veya Percent olmalıdır.")]
    string Mode,

    [Range(typeof(decimal), "0.01", "1000000000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Tutar ya da oran sıfırdan büyük olmalıdır.")]
    decimal Value,

    [Range(typeof(decimal), "0", "1000000000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Tetikleyen en düşük tutar sıfır ya da daha büyük olmalıdır.")]
    decimal MinIncoming
);
