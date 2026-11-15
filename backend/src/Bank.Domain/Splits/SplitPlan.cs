using Bank.Domain.Common;

namespace Bank.Domain.Splits;

/// <summary>
/// The rules of splitting an expense. The payer's own part is what is left of the total once the others' shares
/// are taken off, so it can be nothing (they paid for everyone else) but never less than nothing.
/// </summary>
public static class SplitPlan
{
    public const int MaxShares = 20;

    /// <summary>How far back an expense can be split; older debts belong to a conversation, not to an app.</summary>
    public const int MaxAgeDays = 365;

    /// <returns>The payer's own part.</returns>
    public static decimal Check(decimal total, IReadOnlyCollection<decimal> shares, DateOnly spentOn, DateOnly today)
    {
        if (total <= 0)
            throw new DomainException("Toplam tutar sıfırdan büyük olmalıdır.");
        if (decimal.Round(total, 2) != total || shares.Any(s => decimal.Round(s, 2) != s))
            throw new DomainException("Tutarlar en fazla iki ondalık basamak içerebilir.");
        if (shares.Count is 0 or > MaxShares)
            throw new DomainException($"Harcama en az 1, en fazla {MaxShares} kişiyle bölüşülebilir.");
        if (shares.Any(s => s <= 0))
            throw new DomainException("Her payın tutarı sıfırdan büyük olmalıdır.");
        if (spentOn > today)
            throw new DomainException("Harcama tarihi ileri bir gün olamaz.");
        if (spentOn < today.AddDays(-MaxAgeDays))
            throw new DomainException("Bir yıldan eski bir harcama bölüşülemez.");

        var ownPart = total - shares.Sum();
        if (ownPart < 0)
            throw new DomainException("Payların toplamı harcamanın tutarını aşıyor.");
        return ownPart;
    }

    /// <summary>
    /// An even split between the payer and the others, in kuruş. What does not divide evenly stays with the payer,
    /// so the others never pay more than an even share.
    /// </summary>
    public static IReadOnlyList<decimal> Evenly(decimal total, int others)
    {
        if (others <= 0)
            throw new ArgumentOutOfRangeException(nameof(others));
        var each = Math.Floor(total * 100m / (others + 1)) / 100m;
        return Enumerable.Repeat(each, others).ToList();
    }
}
