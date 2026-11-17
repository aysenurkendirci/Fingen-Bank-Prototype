using System.Globalization;
using Bank.Application.Abstractions.Repositories;
using Bank.Domain.Accounts;
using Bank.Domain.ValueObjects;

namespace Bank.Application.Features.Notifications;

/// <summary>
/// Writes the notification centre's messages. Every text a customer can receive is here, in one place, worded
/// the way the bank speaks. Amounts are written the Turkish way ("1.250,00 TL"); account numbers only by their
/// last four digits.
/// </summary>
public sealed class Notices
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly INotificationRepository _notifications;
    private readonly TimeProvider _time;

    public Notices(INotificationRepository notifications, TimeProvider time)
    {
        _notifications = notifications;
        _time = time;
    }

    public Task MoneyReceivedAsync(long userId, Money amount, Iban account, string senderName, CancellationToken ct)
        => AddAsync(userId, "money-in", "Hesabınıza para geldi",
            $"{senderName} gönderdi: {Format(amount)}. Hesap: ...{LastFour(account)}", ct);

    public Task MoneySentAsync(long userId, Money amount, string recipientName, CancellationToken ct)
        => AddAsync(userId, "money-out", "Para gönderildi",
            $"{recipientName} adlı alıcıya {Format(amount)} gönderildi. Bu işlemi siz yapmadıysanız hemen şifrenizi değiştirin.", ct);

    public Task AutoSavedAsync(long userId, decimal amount, string goalName, CancellationToken ct)
        => AddAsync(userId, "savings", "Otomatik birikim",
            $"{Format(new Money(amount, Currency.TRY))} \"{goalName}\" kumbaranıza ayrıldı.", ct);

    public Task GoalReachedAsync(long userId, string goalName, decimal targetAmount, CancellationToken ct)
        => AddAsync(userId, "goal", "Hedefinize ulaştınız",
            $"\"{goalName}\" kumbaranız {Format(new Money(targetAmount, Currency.TRY))} hedefine ulaştı.", ct);

    public Task PasswordChangedAsync(long userId, CancellationToken ct)
        => AddAsync(userId, "security", "Şifreniz değiştirildi",
            "Diğer cihazlardaki oturumlarınız kapatıldı. Bu değişikliği siz yapmadıysanız bankamızla iletişime geçin.", ct);

    public Task CardBlockedAsync(long userId, bool blocked, CancellationToken ct)
        => AddAsync(userId, "security",
            blocked ? "Kartınız geçici olarak kapatıldı" : "Kartınız yeniden kullanıma açıldı",
            blocked
                ? "Kartınızla alışveriş ve nakit çekimi yapılamaz. İstediğiniz zaman Kartlarım ekranından açabilirsiniz."
                : "Kartınızla yeniden alışveriş ve nakit çekimi yapabilirsiniz.", ct);

    public Task ShareRequestedAsync(long debtorUserId, string ownerName, string splitTitle, decimal amount, CancellationToken ct)
        => AddAsync(debtorUserId, "split", "Sizden pay istendi",
            $"{ownerName}, \"{splitTitle}\" harcaması için sizden {Format(new Money(amount, Currency.TRY))} istiyor. Harcama bölme ekranından ödeyebilirsiniz.", ct);

    public Task SharePaidAsync(long ownerUserId, string name, string splitTitle, decimal amount, CancellationToken ct)
        => AddAsync(ownerUserId, "split", "Payını ödedi",
            $"{name}, \"{splitTitle}\" için payı olan {Format(new Money(amount, Currency.TRY))} tutarı gönderdi.", ct);

    public Task ShareDeclinedAsync(long ownerUserId, string name, string splitTitle, CancellationToken ct)
        => AddAsync(ownerUserId, "split", "Pay isteği reddedildi",
            $"{name}, \"{splitTitle}\" için istediğiniz payı ödemeyeceğini bildirdi.", ct);

    public Task SplitCancelledAsync(long debtorUserId, string ownerName, string splitTitle, CancellationToken ct)
        => AddAsync(debtorUserId, "split", "Pay isteği geri çekildi",
            $"{ownerName}, \"{splitTitle}\" için sizden istediği payı artık istemiyor.", ct);

    public Task SignedInAsync(long userId, CancellationToken ct)
        => AddAsync(userId, "security", "Hesabınıza giriş yapıldı",
            "Bu giriş siz değilseniz hemen şifrenizi değiştirin ve iki adımlı doğrulamayı açın.", ct);

    public Task AccountLockedAsync(long userId, CancellationToken ct)
        => AddAsync(userId, "security", "Hesabınız geçici olarak kilitlendi",
            "Art arda hatalı şifre ya da kod girildi. Kilit kısa süre sonra kendiliğinden açılır. Bu denemeler size ait değilse şifrenizi değiştirin.", ct);

    public Task SecondFactorChangedAsync(long userId, bool enabled, CancellationToken ct)
        => AddAsync(userId, "security",
            enabled ? "İki adımlı doğrulama açıldı" : "İki adımlı doğrulama kapatıldı",
            enabled
                ? "Girişlerde şifrenizle birlikte doğrulayıcı uygulamanızdaki kod da istenecek."
                : "Girişlerde artık yalnızca şifreniz isteniyor. Bunu siz yapmadıysanız hemen şifrenizi değiştirin.", ct);

    private Task AddAsync(long userId, string kind, string title, string body, CancellationToken ct)
        => _notifications.AddAsync(new NewNotification(userId, kind, title, body, _time.GetUtcNow().UtcDateTime), ct);

    internal static string Format(Money money)
        => $"{money.Amount.ToString("N2", Turkish)} {(money.Currency.Code == "TRY" ? "TL" : money.Currency.Code)}";

    private static string LastFour(Iban iban) => iban.Value[^4..];
}
