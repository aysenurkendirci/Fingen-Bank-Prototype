using Bank.Api.Security;
using Bank.Application.Features.Splits;
using Bank.Contracts.Splits;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bank.Api.Controllers;

/// <summary>
/// Expenses split with friends. The payer records the expense and the shares; friends who bank here see what is
/// asked of them under <c>requests</c> and pay it with an ordinary transfer.
/// </summary>
[ApiController]
[Route("api/splits")]
[Authorize]
public sealed class SplitsController : ControllerBase
{
    private readonly ISender _sender;

    public SplitsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SplitResponse>>> List(CancellationToken ct)
        => Ok(await _sender.Send(new ListSplitsQuery(), ct));

    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost]
    public async Task<ActionResult<SplitResponse>> Create([FromBody] SplitRequest request, CancellationToken ct)
    {
        var split = await _sender.Send(new CreateSplitCommand(request), ct);
        return CreatedAtAction(nameof(List), null, split);
    }

    /// <summary>Withdraws what is still open; shares already paid stay paid.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await _sender.Send(new CancelSplitCommand(id), ct);
        return NoContent();
    }

    /// <summary>The payer got this share outside the app, e.g. in cash.</summary>
    [HttpPost("{id:long}/shares/{shareId:long}/paid")]
    public async Task<ActionResult<SplitResponse>> MarkPaid(long id, long shareId, CancellationToken ct)
        => Ok(await _sender.Send(new MarkSharePaidCommand(id, shareId), ct));

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<ShareRequestResponse>>> Requests(CancellationToken ct)
        => Ok(await _sender.Send(new ListShareRequestsQuery(), ct));

    /// <summary>Settles a share with a transfer the customer made to the payer's account for exactly the share.</summary>
    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost("requests/{shareId:long}/settle")]
    public async Task<ActionResult<ShareRequestResponse>> Settle(long shareId, [FromBody] SettleShareRequest request, CancellationToken ct)
        => Ok(await _sender.Send(new SettleShareCommand(shareId, request.TransferId!.Value), ct));

    [HttpPost("requests/{shareId:long}/decline")]
    public async Task<ActionResult<ShareRequestResponse>> Decline(long shareId, CancellationToken ct)
        => Ok(await _sender.Send(new DeclineShareCommand(shareId), ct));
}
