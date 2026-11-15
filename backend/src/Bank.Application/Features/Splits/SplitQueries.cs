using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Contracts.Splits;
using MediatR;

namespace Bank.Application.Features.Splits;

/// <summary>The expenses the customer split, newest first.</summary>
public sealed record ListSplitsQuery : IRequest<IReadOnlyList<SplitResponse>>;

/// <summary>The shares others asked of the customer, newest first.</summary>
public sealed record ListShareRequestsQuery : IRequest<IReadOnlyList<ShareRequestResponse>>;

public sealed class SplitQueryHandlers :
    IRequestHandler<ListSplitsQuery, IReadOnlyList<SplitResponse>>,
    IRequestHandler<ListShareRequestsQuery, IReadOnlyList<ShareRequestResponse>>
{
    private readonly ISplitRepository _splits;
    private readonly ICurrentUser _currentUser;

    public SplitQueryHandlers(ISplitRepository splits, ICurrentUser currentUser)
    {
        _splits = splits;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SplitResponse>> Handle(ListSplitsQuery query, CancellationToken ct)
        => (await _splits.ListOwnedAsync(_currentUser.UserId, ct)).Select(SplitMapping.ToResponse).ToList();

    public async Task<IReadOnlyList<ShareRequestResponse>> Handle(ListShareRequestsQuery query, CancellationToken ct)
        => (await _splits.ListRequestsAsync(_currentUser.UserId, ct)).Select(SplitMapping.ToResponse).ToList();
}

internal static class SplitMapping
{
    public static SplitResponse ToResponse(SplitRow s)
    {
        // The payer's part is fixed when the split is made; a declined or withdrawn share does not move it.
        var others = s.Shares.Sum(x => x.Amount);
        return new SplitResponse(
            s.SplitId,
            s.Title,
            s.TotalAmount,
            s.CurrencyCode,
            DateOnly.FromDateTime(s.SpentOn),
            s.ReceivingIban,
            s.TotalAmount - others,
            s.Shares.Where(x => x.Status == ShareStatus.Paid).Sum(x => x.Amount),
            s.Status,
            s.CreatedAt,
            s.Shares.Select(x => new ShareResponse(x.ShareId, x.Name, x.Amount, x.Status, x.DebtorUserId is not null, x.SettledAt)).ToList());
    }

    public static ShareRequestResponse ToResponse(ShareRequestRow r)
        => new(r.ShareId, r.Title, r.OwnerName, r.Amount, r.CurrencyCode, r.ReceivingIban, DateOnly.FromDateTime(r.SpentOn), r.Status, r.CreatedAt);
}
