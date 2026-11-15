using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.Splits;

/// <summary>An expense the customer paid and splits with friends.</summary>
/// <param name="ReceivingAccountId">The customer's own lira account the friends' payments go to.</param>
public sealed record SplitRequest(
    [Required(ErrorMessage = "Harcamanın adı zorunludur.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Harcamanın adı 2 ile 80 karakter arasında olmalıdır.")]
    string Title,

    [Range(typeof(decimal), "0.01", "1000000000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Toplam tutar sıfırdan büyük olmalıdır.")]
    decimal TotalAmount,

    DateOnly SpentOn,

    [Required(ErrorMessage = "Ödemelerin geleceği hesap zorunludur.")]
    long? ReceivingAccountId,

    [Required(ErrorMessage = "En az bir kişi ekleyin.")]
    IReadOnlyList<ShareRequest> Shares
);

/// <param name="Iban">For a friend who banks here: their account. Without it the friend is a name only and pays outside the app.</param>
public sealed record ShareRequest(
    [Required(ErrorMessage = "Kişinin adı zorunludur.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Kişinin adı 2 ile 100 karakter arasında olmalıdır.")]
    string Name,

    string? Iban,

    [Range(typeof(decimal), "0.01", "1000000000",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Pay sıfırdan büyük olmalıdır.")]
    decimal Amount
);

/// <param name="OwnShare">The payer's own part: the total less the others' shares.</param>
/// <param name="Collected">What came back so far, in the app or marked as paid.</param>
/// <param name="Status"><c>Open</c> or <c>Cancelled</c>.</param>
public sealed record SplitResponse(
    long Id,
    string Title,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly SpentOn,
    string ReceivingIban,
    decimal OwnShare,
    decimal Collected,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<ShareResponse> Shares
);

/// <param name="Status"><c>Open</c>, <c>Paid</c>, <c>Declined</c> or <c>Cancelled</c>.</param>
/// <param name="InApp">True for a friend who banks here and can pay in the app.</param>
public sealed record ShareResponse(long Id, string Name, decimal Amount, string Status, bool InApp, DateTime? SettledAt);

/// <summary>A share someone else asked of the signed-in customer.</summary>
/// <param name="ReceivingIban">Where to send the money; the payer's name is the recipient name.</param>
public sealed record ShareRequestResponse(
    long ShareId,
    string Title,
    string OwnerName,
    decimal Amount,
    string CurrencyCode,
    string ReceivingIban,
    DateOnly SpentOn,
    string Status,
    DateTime CreatedAt
);

/// <summary>Settles a share with a transfer the customer already made to the payer.</summary>
public sealed record SettleShareRequest(
    [Required(ErrorMessage = "Transfer zorunludur.")]
    long? TransferId
);
