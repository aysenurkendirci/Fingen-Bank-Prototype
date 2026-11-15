namespace Bank.Application.Abstractions.Repositories;

/// <summary>Display preferences per customer. A customer without a row uses the defaults.</summary>
public interface IUserPreferencesRepository
{
    Task<UserPreferencesRow?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>Creates the row on the first change and replaces it afterwards.</summary>
    Task SaveAsync(long userId, string theme, string accent, string mode, DateTime updatedAt, CancellationToken ct = default);
}

public sealed class UserPreferencesRow
{
    public string Theme { get; init; } = string.Empty;
    public string Accent { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
}
