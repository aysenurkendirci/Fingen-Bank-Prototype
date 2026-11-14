using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.Account;

/// <summary>What the profile screen shows. The identity number arrives masked; only the data export has it in full.</summary>
public sealed record ProfileResponse(
    string FirstName,
    string LastName,
    string MaskedTcNo,
    string Email,
    string Phone,
    string Membership,
    DateTime RegisteredAt,
    DateTime? LastLoginAt
);

/// <summary>The details a customer may change themselves. Name and identity number are verified facts and stay fixed.</summary>
public sealed record UpdateProfileRequest(
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(256)]
    string Email,

    [Required(ErrorMessage = "Telefon zorunludur.")]
    [RegularExpression(@"^(\+90|0)?\s*5\d{2}\s*\d{3}\s*\d{2}\s*\d{2}$", ErrorMessage = "Telefon 5xx xxx xx xx biçiminde bir cep telefonu olmalıdır.")]
    [StringLength(20)]
    string Phone
);

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
    [StringLength(128)]
    string CurrentPassword,

    [Required(ErrorMessage = "Yeni şifre zorunludur.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [RegularExpression(@"^(?=.*\p{L})(?=.*\d).+$", ErrorMessage = "Şifre en az bir harf ve bir rakam içermelidir.")]
    string NewPassword
);
