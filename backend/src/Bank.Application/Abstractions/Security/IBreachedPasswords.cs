namespace Bank.Application.Abstractions.Security;

/// <summary>Whether a password is known from public data breaches (OWASP ASVS 2.1.7).</summary>
public interface IBreachedPasswords
{
    /// <returns>
    /// True when the password appears in a breach, false when it does not, null when that could not be found out.
    /// A check that cannot run must not stop anyone from signing up, so callers treat null as "not known".
    /// </returns>
    Task<bool?> IsBreachedAsync(string password, CancellationToken ct = default);
}

public static class BreachedPasswordMessages
{
    public const string Refused =
        "Bu şifre daha önce sızdırılmış şifre listelerinde yer alıyor, başkaları da deneyebilir. Başka bir şifre seçin.";
}
