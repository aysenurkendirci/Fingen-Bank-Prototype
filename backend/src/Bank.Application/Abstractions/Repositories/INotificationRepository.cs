namespace Bank.Application.Abstractions.Repositories;

/// <summary>
/// The notification centre's store. <see cref="AddAsync"/> joins the open unit of work if there is one, so a
/// notification is kept exactly when the change it describes is.
/// </summary>
public interface INotificationRepository
{
    Task AddAsync(NewNotification notification, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationRow>> ListAsync(long userId, int take, CancellationToken ct = default);

    Task<int> CountUnreadAsync(long userId, CancellationToken ct = default);

    /// <returns>False when the notification is not the user's.</returns>
    Task<bool> MarkReadAsync(long userId, long notificationId, DateTime readAt, CancellationToken ct = default);

    Task MarkAllReadAsync(long userId, DateTime readAt, CancellationToken ct = default);
}

public sealed record NewNotification(long UserId, string Kind, string Title, string Body, DateTime CreatedAt);

public sealed class NotificationRow
{
    public long NotificationId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}
