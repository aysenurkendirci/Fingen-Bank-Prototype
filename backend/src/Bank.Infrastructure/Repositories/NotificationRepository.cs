using Bank.Application.Abstractions.Repositories;
using Bank.Infrastructure.Persistence;
using Dapper;

namespace Bank.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly DbSession _session;

    public NotificationRepository(DbSession session) => _session = session;

    public async Task AddAsync(NewNotification notification, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Notifications (UserId, Kind, Title, Body, CreatedAt)
            VALUES (@UserId, @Kind, @Title, @Body, @CreatedAt)
            """;

        var connection = await _session.GetConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            notification.UserId,
            Kind = new DbString { Value = notification.Kind, IsAnsi = true, Length = 16 },
            notification.Title,
            notification.Body,
            notification.CreatedAt,
        }, _session.Transaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<NotificationRow>> ListAsync(long userId, int take, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@Take) NotificationId, Kind, Title, Body, CreatedAt, ReadAt
            FROM dbo.Notifications
            WHERE UserId = @UserId
            ORDER BY NotificationId DESC
            """;

        var connection = await _session.GetConnectionAsync(ct);
        var rows = await connection.QueryAsync<NotificationRow>(
            new CommandDefinition(sql, new { UserId = userId, Take = take }, _session.Transaction, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<int> CountUnreadAsync(long userId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId = @UserId AND ReadAt IS NULL";

        var connection = await _session.GetConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { UserId = userId }, _session.Transaction, cancellationToken: ct));
    }

    public async Task<bool> MarkReadAsync(long userId, long notificationId, DateTime readAt, CancellationToken ct = default)
    {
        // Reading it twice keeps the first time; a stranger's id matches no row and is reported as not found.
        const string sql = """
            UPDATE dbo.Notifications SET ReadAt = COALESCE(ReadAt, @ReadAt)
            WHERE NotificationId = @NotificationId AND UserId = @UserId
            """;

        var connection = await _session.GetConnectionAsync(ct);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId, NotificationId = notificationId, ReadAt = readAt }, _session.Transaction, cancellationToken: ct));
        return updated == 1;
    }

    public async Task MarkAllReadAsync(long userId, DateTime readAt, CancellationToken ct = default)
    {
        const string sql = "UPDATE dbo.Notifications SET ReadAt = @ReadAt WHERE UserId = @UserId AND ReadAt IS NULL";

        var connection = await _session.GetConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId, ReadAt = readAt }, _session.Transaction, cancellationToken: ct));
    }
}
