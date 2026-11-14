using Bank.Api.Security;
using Bank.Application.Features.Savings;
using Bank.Contracts.Savings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bank.Api.Controllers;

/// <summary>Savings goals: each one a savings account of its own, filled by hand or by an auto-save rule.</summary>
[ApiController]
[Route("api/goals")]
[Authorize]
public sealed class GoalsController : ControllerBase
{
    private readonly ISender _sender;

    public GoalsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoalResponse>>> List(CancellationToken ct)
        => Ok(await _sender.Send(new ListGoalsQuery(), ct));

    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost]
    public async Task<ActionResult<GoalResponse>> Create([FromBody] GoalRequest request, CancellationToken ct)
    {
        var goal = await _sender.Send(new CreateGoalCommand(request), ct);
        return CreatedAtAction(nameof(List), null, goal);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<GoalResponse>> Update(long id, [FromBody] GoalRequest request, CancellationToken ct)
        => Ok(await _sender.Send(new UpdateGoalCommand(id, request), ct));

    /// <summary>Puts money into the goal. Needs an <c>Idempotency-Key</c>, so a retried request moves the money once.</summary>
    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost("{id:long}/deposits")]
    public async Task<ActionResult<GoalResponse>> Deposit(
        long id, [FromBody] GoalMoneyRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
        => Ok(await _sender.Send(new DepositToGoalCommand(id, request, idempotencyKey), ct));

    /// <summary>Takes money out of the goal into one of the customer's own accounts.</summary>
    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost("{id:long}/withdrawals")]
    public async Task<ActionResult<GoalResponse>> Withdraw(
        long id, [FromBody] GoalMoneyRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
        => Ok(await _sender.Send(new WithdrawFromGoalCommand(id, request, idempotencyKey), ct));

    [HttpPut("{id:long}/auto-save")]
    public async Task<ActionResult<GoalResponse>> SetAutoSave(long id, [FromBody] AutoSaveRequest request, CancellationToken ct)
        => Ok(await _sender.Send(new SetAutoSaveCommand(id, request), ct));

    [HttpDelete("{id:long}/auto-save")]
    public async Task<ActionResult<GoalResponse>> ClearAutoSave(long id, CancellationToken ct)
        => Ok(await _sender.Send(new SetAutoSaveCommand(id, null), ct));

    /// <summary>Closes an empty goal and its savings account.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Close(long id, CancellationToken ct)
    {
        await _sender.Send(new CloseGoalCommand(id), ct);
        return NoContent();
    }
}
