namespace Bank.Contracts.Notifications;

/// <summary>One message in the customer's notification centre.</summary>
/// <param name="Kind"><c>money-in</c>, <c>money-out</c>, <c>savings</c>, <c>goal</c>, <c>security</c> or <c>split</c>.</param>
/// <param name="ReadAt">Null while the customer has not seen it.</param>
public sealed record NotificationResponse(
    long NotificationId,
    string Kind,
    string Title,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>The newest notifications and how many of all of them are still unread.</summary>
public sealed record NotificationFeedResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);
