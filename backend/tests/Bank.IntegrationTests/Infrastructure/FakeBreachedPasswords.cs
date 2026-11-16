using Bank.Application.Abstractions.Security;

namespace Bank.IntegrationTests.Infrastructure;

/// <summary>Stands in for the public breach list, so the suite never needs the internet.</summary>
public sealed class FakeBreachedPasswords : IBreachedPasswords
{
    /// <summary>The one password the fake knows from a breach.</summary>
    public const string Breached = "Breached#Passw0rd1";

    public Task<bool?> IsBreachedAsync(string password, CancellationToken ct = default)
        => Task.FromResult<bool?>(password == Breached);
}
