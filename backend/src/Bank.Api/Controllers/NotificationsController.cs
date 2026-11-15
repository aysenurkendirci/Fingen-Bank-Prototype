using Bank.Application.Features.Notifications;
using Bank.Contracts.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers;

/// <summary>The notification centre: what happened to the customer's money and sign-in, newest first.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

    /// <param name="take">How many of the newest to return, at most 50.</param>
    [HttpGet]
    public async Task<ActionResult<NotificationFeedResponse>> List([FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetNotificationsQuery(take), ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await _sender.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _sender.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }
}
