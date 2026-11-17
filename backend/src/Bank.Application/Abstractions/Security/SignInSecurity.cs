namespace Bank.Application.Abstractions.Security;

/// <summary>Encrypts small secrets the application must read back, such as a customer's second factor key.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);

    /// <exception cref="System.Security.Cryptography.CryptographicException">The data was changed or another key wrote it.</exception>
    byte[] Unprotect(byte[] protectedData);
}

/// <summary>How sign-in is guarded, read from configuration ("Security:SignIn").</summary>
public sealed class SignInPolicy
{
    public const string Section = "Security:SignIn";

    /// <summary>Wrong passwords or codes in a row before the account is locked.</summary>
    public int MaxFailures { get; init; } = 5;

    /// <summary>How long a locked account stays locked. It opens by itself; nobody has to call the bank.</summary>
    public TimeSpan LockFor { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>The name an authenticator app shows next to the code.</summary>
    public string Issuer { get; init; } = "FinGen";
}
