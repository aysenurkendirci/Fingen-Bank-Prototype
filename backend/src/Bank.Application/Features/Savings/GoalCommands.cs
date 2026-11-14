using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common;
using Bank.Application.Common.Behaviors;
using Bank.Application.Common.Exceptions;
using Bank.Application.Features.Accounts;
using Bank.Contracts.Savings;
using Bank.Domain.Accounts;
using FluentValidation;
using MediatR;

namespace Bank.Application.Features.Savings;

/// <summary>Creates a goal together with its own savings account.</summary>
public sealed record CreateGoalCommand(GoalRequest Goal) : IRequest<GoalResponse>, ITransactionalCommand;

/// <summary>Renames a goal or changes its target, date or new year.</summary>
public sealed record UpdateGoalCommand(long GoalId, GoalRequest Goal) : IRequest<GoalResponse>, ITransactionalCommand;

/// <summary>Moves money from one of the customer's accounts into the goal.</summary>
public sealed record DepositToGoalCommand(long GoalId, GoalMoneyRequest Money, string? IdempotencyKey) : IRequest<GoalResponse>, ITransactionalCommand;

/// <summary>Moves money out of the goal back to one of the customer's accounts.</summary>
public sealed record WithdrawFromGoalCommand(long GoalId, GoalMoneyRequest Money, string? IdempotencyKey) : IRequest<GoalResponse>, ITransactionalCommand;

/// <summary>Sets the goal's auto-save rule, or removes it when the rule is null.</summary>
public sealed record SetAutoSaveCommand(long GoalId, AutoSaveRequest? Rule) : IRequest<GoalResponse>, ITransactionalCommand;

/// <summary>Closes a goal whose savings account is empty.</summary>
public sealed record CloseGoalCommand(long GoalId) : IRequest, ITransactionalCommand;

public sealed class GoalRequestValidator : AbstractValidator<GoalRequest>
{
    public GoalRequestValidator(TimeProvider time)
    {
        RuleFor(g => g.TargetDate)
            .Must(date => date is null || date > BusinessDate.Today(time))
            .WithMessage("Hedef tarihi bugünden sonra olmalıdır.");

        RuleFor(g => g.NewYearOf)
            .Must(year => year is null || (year >= BusinessDate.Today(time).Year && year <= BusinessDate.Today(time).Year + 5))
            .WithMessage("Yılbaşı hedefi bu yıl ile beş yıl sonrası arasında bir yıl için olabilir.");
    }
}

public sealed class CreateGoalCommandValidator : AbstractValidator<CreateGoalCommand>
{
    public CreateGoalCommandValidator(TimeProvider time) => RuleFor(c => c.Goal).SetValidator(new GoalRequestValidator(time));
}

public sealed class UpdateGoalCommandValidator : AbstractValidator<UpdateGoalCommand>
{
    public UpdateGoalCommandValidator(TimeProvider time) => RuleFor(c => c.Goal).SetValidator(new GoalRequestValidator(time));
}

internal static class IdempotencyKeyRules
{
    public static void Apply<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string?>> key)
        => validator.RuleFor(key)
            .NotEmpty().WithMessage("Idempotency-Key başlığı zorunludur.")
            .MaximumLength(48).WithMessage("Idempotency-Key en fazla 48 karakter olabilir.")
            .Matches("^[A-Za-z0-9._:-]+$").WithMessage("Idempotency-Key yalnızca harf, rakam ve . _ : - içerebilir.");
}

public sealed class DepositToGoalCommandValidator : AbstractValidator<DepositToGoalCommand>
{
    public DepositToGoalCommandValidator() => IdempotencyKeyRules.Apply(this, c => c.IdempotencyKey);
}

public sealed class WithdrawFromGoalCommandValidator : AbstractValidator<WithdrawFromGoalCommand>
{
    public WithdrawFromGoalCommandValidator() => IdempotencyKeyRules.Apply(this, c => c.IdempotencyKey);
}

public sealed class SetAutoSaveCommandValidator : AbstractValidator<SetAutoSaveCommand>
{
    public SetAutoSaveCommandValidator()
    {
        RuleFor(c => c.Rule!.Value)
            .LessThanOrEqualTo(100).WithMessage("Oran en fazla %100 olabilir.")
            .When(c => c.Rule?.Mode == AutoSaveRule.Percent)
            .OverridePropertyName("value");
    }
}

public sealed class GoalCommandHandlers :
    IRequestHandler<CreateGoalCommand, GoalResponse>,
    IRequestHandler<UpdateGoalCommand, GoalResponse>,
    IRequestHandler<DepositToGoalCommand, GoalResponse>,
    IRequestHandler<WithdrawFromGoalCommand, GoalResponse>,
    IRequestHandler<SetAutoSaveCommand, GoalResponse>,
    IRequestHandler<CloseGoalCommand>
{
    // Goals are planned in lira, like the projection and the scenarios.
    private static readonly string GoalCurrency = ReportingCurrency.Value.Code;

    private readonly ISavingsGoalRepository _goals;
    private readonly IAccountRepository _accounts;
    private readonly SavingsTransfers _transfers;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public GoalCommandHandlers(
        ISavingsGoalRepository goals, IAccountRepository accounts, SavingsTransfers transfers, ICurrentUser currentUser, TimeProvider time)
    {
        _goals = goals;
        _accounts = accounts;
        _transfers = transfers;
        _currentUser = currentUser;
        _time = time;
    }

    private long UserId => _currentUser.UserId;

    public async Task<GoalResponse> Handle(CreateGoalCommand command, CancellationToken ct)
    {
        var iban = Iban.For(OpenAccountCommandHandler.BankCode, await _accounts.NextAccountNumberAsync(ct));
        var goalId = await _goals.CreateAsync(UserId, iban, Details(command.Goal), _time.GetUtcNow().UtcDateTime, ct);
        return await ReadAsync(goalId, ct);
    }

    public async Task<GoalResponse> Handle(UpdateGoalCommand command, CancellationToken ct)
    {
        if (!await _goals.UpdateAsync(UserId, command.GoalId, Details(command.Goal), ct))
            throw new NotFoundException(GoalNotFound);
        return await ReadAsync(command.GoalId, ct);
    }

    public async Task<GoalResponse> Handle(DepositToGoalCommand command, CancellationToken ct)
    {
        var goal = await GoalAsync(command.GoalId, ct);
        await _transfers.MoveAsync(
            UserId, command.Money.AccountId!.Value, goal.AccountId, command.Money.Amount,
            $"Kumbara: {goal.Name}", $"goal-in:{command.IdempotencyKey}", ct);
        return await ReadAsync(goal.GoalId, ct);
    }

    public async Task<GoalResponse> Handle(WithdrawFromGoalCommand command, CancellationToken ct)
    {
        var goal = await GoalAsync(command.GoalId, ct);
        await _transfers.MoveAsync(
            UserId, goal.AccountId, command.Money.AccountId!.Value, command.Money.Amount,
            $"Kumbaradan: {goal.Name}", $"goal-out:{command.IdempotencyKey}", ct);
        return await ReadAsync(goal.GoalId, ct);
    }

    public async Task<GoalResponse> Handle(SetAutoSaveCommand command, CancellationToken ct)
    {
        AutoSaveRule? rule = null;
        if (command.Rule is { } r)
        {
            var source = (await _accounts.ListAsync(UserId, ct)).SingleOrDefault(a => a.AccountId == r.SourceAccountId)
                         ?? throw new NotFoundException("Kaynak hesap bulunamadı.");
            if (source.AccountType == "Savings")
                throw new BadRequestException("Otomatik birikim bir kumbara hesabını izleyemez; maaşın geldiği hesabı seçin.");
            if (source.CurrencyCode != GoalCurrency)
                throw new BadRequestException("Otomatik birikim yalnızca TL hesaplardan yapılabilir.");

            rule = new AutoSaveRule(source.AccountId, r.Mode, r.Value, r.MinIncoming);
        }

        if (!await _goals.SetAutoSaveAsync(UserId, command.GoalId, rule, ct))
            throw new NotFoundException(GoalNotFound);
        return await ReadAsync(command.GoalId, ct);
    }

    public async Task Handle(CloseGoalCommand command, CancellationToken ct)
    {
        switch (await _goals.CloseAsync(UserId, command.GoalId, ct))
        {
            case CloseGoalOutcome.NotFound:
                throw new NotFoundException(GoalNotFound);
            case CloseGoalOutcome.NotEmpty:
                throw new ConflictException("Kumbarada para varken hedef kapatılamaz. Önce parayı hesabınıza aktarın.");
        }
    }

    private const string GoalNotFound = "Hedef bulunamadı.";

    private static GoalDetails Details(GoalRequest g) => new(g.Name.Trim(), g.TargetAmount, g.TargetDate, g.NewYearOf);

    private async Task<GoalRow> GoalAsync(long goalId, CancellationToken ct)
        => await _goals.GetAsync(UserId, goalId, ct) ?? throw new NotFoundException(GoalNotFound);

    private async Task<GoalResponse> ReadAsync(long goalId, CancellationToken ct)
        => GoalMapping.ToResponse(await GoalAsync(goalId, ct), BusinessDate.Today(_time));
}
