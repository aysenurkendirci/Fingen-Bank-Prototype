using Bank.Api.Security;
using Bank.Application.Features.Accounts;
using Bank.Contracts.Finance;
using Bank.Contracts.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bank.Api.Controllers;

/// <summary>The customer's bank accounts: opening, closing an empty one, and the statement read from the ledger.</summary>
[ApiController]
[Route("api/accounts")]
[Authorize]
public sealed class AccountsController : ControllerBase
{
    private readonly ISender _sender;

    public AccountsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken ct)
        => Ok(await _sender.Send(new ListAccountsQuery(), ct));

    /// <summary>Opens an empty account with a newly generated IBAN.</summary>
    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Open([FromBody] OpenAccountRequest request, CancellationToken ct)
    {
        var opened = await _sender.Send(new OpenAccountCommand(request), ct);
        return Created($"/api/accounts/{opened.Id}/statement", opened);
    }

    /// <summary>Closes an empty account. The last lira demand account and goals' savings accounts stay open.</summary>
    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, CancellationToken ct)
    {
        await _sender.Send(new CloseAccountCommand(id), ct);
        return NoContent();
    }

    /// <summary>The account's movements as recorded in the ledger, newest first.</summary>
    [HttpGet("{id:long}/statement")]
    public async Task<ActionResult<PagedResponse<StatementEntryResponse>>> Statement(
        long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetAccountStatementQuery(id, page, pageSize), ct));
}
