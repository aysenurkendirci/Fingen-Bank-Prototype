using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common.Exceptions;
using Bank.Contracts.Notifications;
using MediatR;

namespace Bank.Application.Features.Notifications;

/// <summary>The newest notifications of the signed-in customer and the unread count.</summary>
public sealed record GetNotificationsQuery(int Take) : IRequest<NotificationFeedResponse>
{
    public const int MaxTake = 50;
}

public sealed record MarkNotificationReadCommand(long NotificationId) : IRequest;

public sealed record MarkAllNotificationsReadCommand : IRequest;

public sealed class NotificationRequestHandlers :
    IRequestHandler<GetNotificationsQuery, NotificationFeedResponse>,
    IRequestHandler<MarkNotificationReadCommand>,
    IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public NotificationRequestHandlers(INotificationRepository notifications, ICurrentUser currentUser, TimeProvider time)
    {
        _notifications = notifications;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<NotificationFeedResponse> Handle(GetNotificationsQuery query, CancellationToken ct)
    {
        var take = Math.Clamp(query.Take, 1, GetNotificationsQuery.MaxTake);
        var rows = await _notifications.ListAsync(_currentUser.UserId, take, ct);
        var unread = await _notifications.CountUnreadAsync(_currentUser.UserId, ct);

        return new NotificationFeedResponse(
            rows.Select(NotificationMapping.ToResponse).ToList(),
            unread);
    }

    public async Task Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        if (!await _notifications.MarkReadAsync(_currentUser.UserId, command.NotificationId, _time.GetUtcNow().UtcDateTime, ct))
            throw new NotFoundException("Bildirim bulunamadı.");
    }

    public Task Handle(MarkAllNotificationsReadCommand command, CancellationToken ct)
        => _notifications.MarkAllReadAsync(_currentUser.UserId, _time.GetUtcNow().UtcDateTime, ct);
}

internal static class NotificationMapping
{
    public static NotificationResponse ToResponse(NotificationRow n)
        => new(n.NotificationId, n.Kind, n.Title, n.Body, n.CreatedAt, n.ReadAt);
}
