using System.Globalization;

namespace Bank.Application.Common.Exceptions;

/// <summary>Too many wrong passwords or codes in a row; the account opens again by itself.</summary>
public sealed class AccountLockedException : AppException
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public AccountLockedException(DateTime lockedUntilUtc)
        : base($"Çok fazla hatalı deneme yapıldı. Güvenliğiniz için hesabınız {Local(lockedUntilUtc)} saatine kadar kilitlendi.")
        => LockedUntil = lockedUntilUtc;

    public DateTime LockedUntil { get; }

    private static string Local(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Istanbul).ToString("HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>
/// The password was right and the customer uses a second factor: the sign-in continues with a code. Only said
/// after the password is checked, so whether someone uses a second factor is not given away to a guesser.
/// </summary>
public sealed class SecondFactorRequiredException : AppException
{
    public SecondFactorRequiredException() : base("Doğrulayıcı uygulamanızdaki 6 haneli kodu girin.") { }
}

/// <summary>A wrong or already used second factor code.</summary>
public sealed class SecondFactorFailedException : AppException
{
    public SecondFactorFailedException() : base("Doğrulama kodu hatalı ya da süresi geçmiş.") { }
}
