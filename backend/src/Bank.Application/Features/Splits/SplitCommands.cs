using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common;
using Bank.Application.Common.Behaviors;
using Bank.Application.Common.Exceptions;
using Bank.Application.Features.Notifications;
using Bank.Contracts.Splits;
using Bank.Domain.Accounts;
using Bank.Domain.Common;
using Bank.Domain.Splits;
using FluentValidation;
using MediatR;

namespace Bank.Application.Features.Splits;

/// <summary>Records an expense the customer paid and asks each friend for their share.</summary>
public sealed record CreateSplitCommand(SplitRequest Split) : IRequest<SplitResponse>, ITransactionalCommand;

/// <summary>Withdraws the open shares of a split; shares already paid stay paid.</summary>
public sealed record CancelSplitCommand(long SplitId) : IRequest, ITransactionalCommand;

/// <summary>The payer got a share outside the app, e.g. in cash, and closes it.</summary>
public sealed record MarkSharePaidCommand(long SplitId, long ShareId) : IRequest<SplitResponse>, ITransactionalCommand;

/// <summary>A friend settles the share asked of them with a transfer they made to the payer.</summary>
public sealed record SettleShareCommand(long ShareId, long TransferId) : IRequest<ShareRequestResponse>, ITransactionalCommand;

/// <summary>A friend says they will not pay the share asked of them.</summary>
public sealed record DeclineShareCommand(long ShareId) : IRequest<ShareRequestResponse>, ITransactionalCommand;

public sealed class CreateSplitCommandValidator : AbstractValidator<CreateSplitCommand>
{
    public CreateSplitCommandValidator()
    {
        RuleFor(c => c.Split.Shares).NotEmpty().WithMessage("En az bir kişi ekleyin.").OverridePropertyName("shares");
        RuleForEach(c => c.Split.Shares)
            .Must(s => string.IsNullOrWhiteSpace(s.Iban) || Iban.TryParse(s.Iban, out _))
            .WithMessage("IBAN geçerli değil.")
            .OverridePropertyName("shares");
    }
}

public sealed class SplitCommandHandlers :
    IRequestHandler<CreateSplitCommand, SplitResponse>,
    IRequestHandler<CancelSplitCommand>,
    IRequestHandler<MarkSharePaidCommand, SplitResponse>,
    IRequestHandler<SettleShareCommand, ShareRequestResponse>,
    IRequestHandler<DeclineShareCommand, ShareRequestResponse>
{
    private const string SplitNotFound = "Harcama bulunamadı.";
    private const string RequestNotFound = "Pay isteği bulunamadı.";

    private readonly ISplitRepository _splits;
    private readonly IAccountRepository _accounts;
    private readonly IAuthRepository _users;
    private readonly Notices _notices;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public SplitCommandHandlers(
        ISplitRepository splits, IAccountRepository accounts, IAuthRepository users, Notices notices, ICurrentUser currentUser,
        TimeProvider time)
    {
        _splits = splits;
        _accounts = accounts;
        _users = users;
        _notices = notices;
        _currentUser = currentUser;
        _time = time;
    }

    private long UserId => _currentUser.UserId;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<SplitResponse> Handle(CreateSplitCommand command, CancellationToken ct)
    {
        var request = command.Split;
        SplitPlan.Check(request.TotalAmount, request.Shares.Select(s => s.Amount).ToList(), request.SpentOn, BusinessDate.Today(_time));

        var receiving = (await _accounts.ListAsync(UserId, ct)).SingleOrDefault(a => a.AccountId == request.ReceivingAccountId)
                        ?? throw new NotFoundException("Ödemelerin geleceği hesap bulunamadı.");
        if (receiving.Status != "Active" || receiving.CurrencyCode != "TRY" || receiving.AccountType == "Savings")
            throw new DomainException("Ödemeler açık bir vadesiz TL hesabınıza gelmelidir.");

        var shares = new List<NewShare>();
        foreach (var share in request.Shares)
            shares.Add(new NewShare(share.Name.Trim(), await DebtorAsync(share, ct), share.Amount));

        if (shares.Where(s => s.DebtorUserId is not null).GroupBy(s => s.DebtorUserId).Any(g => g.Count() > 1))
            throw new DomainException("Aynı kişi bir harcamaya iki kez eklenemez.");

        var splitId = await _splits.CreateAsync(
            new NewSplit(UserId, receiving.AccountId, request.Title.Trim(), request.TotalAmount, request.SpentOn, Now, shares), ct);

        var owner = await MyNameAsync(ct);
        foreach (var share in shares.Where(s => s.DebtorUserId is not null))
            await _notices.ShareRequestedAsync(share.DebtorUserId!.Value, owner, request.Title.Trim(), share.Amount, ct);

        return await ReadAsync(splitId, ct);
    }

    public async Task Handle(CancelSplitCommand command, CancellationToken ct)
    {
        var split = await _splits.GetOwnedAsync(UserId, command.SplitId, ct) ?? throw new NotFoundException(SplitNotFound);
        if (split.Status != "Open")
            return;

        var owner = await MyNameAsync(ct);
        foreach (var debtor in await _splits.CancelAsync(split.SplitId, Now, ct))
            await _notices.SplitCancelledAsync(debtor, owner, split.Title, ct);
    }

    public async Task<SplitResponse> Handle(MarkSharePaidCommand command, CancellationToken ct)
    {
        var split = await _splits.GetOwnedAsync(UserId, command.SplitId, ct) ?? throw new NotFoundException(SplitNotFound);
        var share = split.Shares.SingleOrDefault(s => s.ShareId == command.ShareId) ?? throw new NotFoundException("Pay bulunamadı.");

        if (share.Status == ShareStatus.Paid)
            return SplitMapping.ToResponse(split);
        if (share.Status != ShareStatus.Open || !await _splits.SettleAsync(share.ShareId, ShareStatus.Paid, null, Now, ct))
            throw new ConflictException("Bu pay artık açık değil.");

        return await ReadAsync(split.SplitId, ct);
    }

    public async Task<ShareRequestResponse> Handle(SettleShareCommand command, CancellationToken ct)
    {
        var share = await OpenRequestAsync(command.ShareId, ct);

        // The transfer must be the customer's own, to the account the payer named, for exactly the share.
        var transfer = await _splits.GetSentTransferAsync(UserId, command.TransferId, ct)
                       ?? throw new NotFoundException("Transfer bulunamadı.");
        if (transfer.TargetAccountId != share.ReceivingAccountId || transfer.Amount != share.Amount || transfer.CurrencyCode != share.CurrencyCode)
            throw new DomainException("Bu transfer, istenen pay ile aynı hesaba ve aynı tutarda değil.");
        if (await _splits.IsTransferUsedAsync(command.TransferId, ct))
            throw new ConflictException("Bu transfer başka bir payı kapatmak için kullanıldı.");

        if (!await _splits.SettleAsync(share.ShareId, ShareStatus.Paid, command.TransferId, Now, ct))
            throw new ConflictException("Bu pay artık açık değil.");

        await _notices.SharePaidAsync(share.OwnerUserId, await MyNameAsync(ct), share.Title, share.Amount, ct);
        return await ReadRequestAsync(share.ShareId, ct);
    }

    public async Task<ShareRequestResponse> Handle(DeclineShareCommand command, CancellationToken ct)
    {
        var share = await OpenRequestAsync(command.ShareId, ct);
        if (!await _splits.SettleAsync(share.ShareId, ShareStatus.Declined, null, Now, ct))
            throw new ConflictException("Bu pay artık açık değil.");

        await _notices.ShareDeclinedAsync(share.OwnerUserId, await MyNameAsync(ct), share.Title, ct);
        return await ReadRequestAsync(share.ShareId, ct);
    }

    /// <summary>
    /// The customer behind a friend's IBAN, checked like a transfer: the account must exist and be open, and the
    /// name the payer wrote must be the holder's. A friend without an IBAN is a name only.
    /// </summary>
    private async Task<long?> DebtorAsync(ShareRequest share, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(share.Iban))
            return null;

        var holder = await _splits.FindHolderAsync(Iban.Parse(share.Iban).Value, ct)
                     ?? throw new DomainException($"{share.Name.Trim()} için girilen IBAN'a ait bir hesap bulunamadı.");
        if (holder.Status != "Active")
            throw new DomainException($"{share.Name.Trim()} için girilen hesap işleme kapalı.");
        if (!FundsTransfer.NamesMatch(share.Name, holder.HolderName))
            throw new DomainException($"\"{share.Name.Trim()}\" adı, girilen IBAN'ın sahibiyle eşleşmiyor.");
        if (holder.UserId == UserId)
            throw new DomainException("Kendinizi ekleyemezsiniz; sizin payınız toplamdan geriye kalan tutardır.");

        return holder.UserId;
    }

    private async Task<ShareRequestRow> OpenRequestAsync(long shareId, CancellationToken ct)
    {
        var share = await _splits.GetRequestAsync(UserId, shareId, ct) ?? throw new NotFoundException(RequestNotFound);
        if (share.Status != ShareStatus.Open)
            throw new ConflictException("Bu pay isteği artık açık değil.");
        return share;
    }

    /// <summary>The signed-in customer's name, as the other side of a split sees it.</summary>
    private async Task<string> MyNameAsync(CancellationToken ct)
    {
        var profile = await _users.GetProfileAsync(UserId, ct);
        return $"{profile.FirstName} {profile.LastName}";
    }

    private async Task<SplitResponse> ReadAsync(long splitId, CancellationToken ct)
        => SplitMapping.ToResponse(await _splits.GetOwnedAsync(UserId, splitId, ct) ?? throw new NotFoundException(SplitNotFound));

    private async Task<ShareRequestResponse> ReadRequestAsync(long shareId, CancellationToken ct)
        => SplitMapping.ToResponse(await _splits.GetRequestAsync(UserId, shareId, ct) ?? throw new NotFoundException(RequestNotFound));
}
