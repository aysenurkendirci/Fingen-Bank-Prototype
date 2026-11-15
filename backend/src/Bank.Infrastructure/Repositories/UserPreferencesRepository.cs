using Bank.Application.Abstractions.Repositories;
using Bank.Infrastructure.Persistence;
using Dapper;

namespace Bank.Infrastructure.Repositories;

public sealed class UserPreferencesRepository : IUserPreferencesRepository
{
    private readonly SqlConnectionFactory _connections;

    public UserPreferencesRepository(SqlConnectionFactory connections) => _connections = connections;

    public async Task<UserPreferencesRow?> GetAsync(long userId, CancellationToken ct = default)
    {
        const string sql = "SELECT Theme, Accent, Mode FROM dbo.UserPreferences WHERE UserId = @UserId";

        await using var connection = await _connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<UserPreferencesRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
    }

    public async Task SaveAsync(long userId, string theme, string accent, string mode, DateTime updatedAt, CancellationToken ct = default)
    {
        // HOLDLOCK keeps two first saves from the same customer (two tabs) from both trying to insert the row.
        const string sql = """
            MERGE dbo.UserPreferences WITH (HOLDLOCK) AS target
            USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
            WHEN MATCHED THEN
                UPDATE SET Theme = @Theme, Accent = @Accent, Mode = @Mode, UpdatedAt = @UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (UserId, Theme, Accent, Mode, UpdatedAt) VALUES (@UserId, @Theme, @Accent, @Mode, @UpdatedAt);
            """;

        await using var connection = await _connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                UserId = userId,
                Theme = new DbString { Value = theme, IsAnsi = true, Length = 8 },
                Accent = new DbString { Value = accent, IsAnsi = true, Length = 16 },
                Mode = new DbString { Value = mode, IsAnsi = true, Length = 8 },
                UpdatedAt = updatedAt,
            },
            cancellationToken: ct));
    }
}
