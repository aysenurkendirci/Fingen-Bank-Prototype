using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.Account;

/// <summary>Whether the signed-in customer signs in with a second factor.</summary>
public sealed record TwoFactorStatusResponse(bool Enabled, DateTime? EnabledAt);

/// <summary>
/// A new authenticator app key, not yet in use. The app takes it from <paramref name="SetupUri"/> (as a QR code
/// or a link) or typed in as <paramref name="Secret"/>. Shown once: the server keeps it only encrypted.
/// </summary>
public sealed record TwoFactorSetupResponse(string Secret, string SetupUri);

/// <summary>A code from the authenticator app, proving the new key works before it is switched on.</summary>
public sealed record ConfirmTwoFactorRequest(
    [Required(ErrorMessage = "Doğrulama kodu zorunludur.")]
    [RegularExpression(@"^\s*\d{3}\s?\d{3}\s*$", ErrorMessage = "Doğrulama kodu 6 haneli olmalıdır.")]
    string Code
);

/// <summary>Switching the second factor off needs both the password and a current code.</summary>
public sealed record DisableTwoFactorRequest(
    [Required(ErrorMessage = "Şifre zorunludur.")] string Password,
    [Required(ErrorMessage = "Doğrulama kodu zorunludur.")] string Code
);
