using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bank.Application.Abstractions.Messaging;
using Bank.Application.Abstractions.Repositories;
using Bank.Application.Common.Exceptions;
using Bank.Contracts.Events;
using Bank.Domain.Accounts;
using Bank.Domain.ValueObjects;

namespace Bank.Application.Features.Savings;

/// <summary>
/// Moves money between two accounts of the same customer through the ledger: into a goal, out of it, or by an
/// auto-save rule. Uses the same locking, postings and outbox event as a transfer to someone else, so a goal's
/// money is accounted for exactly like any other.
/// </summary>
public sealed class SavingsTransfers
{
    private readonly ITransferRepository _transfers;
    private readonly IOutbox _outbox;

    public SavingsTransfers(ITransferRepository transfers, IOutbox outbox)
    {
        _transfers = transfers;
        _outbox = outbox;
    }

    /// <param name="idempotencyKey">Repeats of the same move with the same key return the first one instead of moving money again.</param>
    public async Task<StoredTransfer> MoveAsync(
        long userId, long sourceAccountId, long targetAccountId, decimal amount, string description, string idempotencyKey, CancellationToken ct)
    {
        var hash = Hash(sourceAccountId, targetAccountId, amount);

        var earlier = await _transfers.FindByIdempotencyKeyAsync(userId, idempotencyKey, ct);
        if (earlier is not null)
            return earlier.RequestHash.AsSpan().SequenceEqual(hash) ? earlier : throw new IdempotencyKeyReusedException();

        var accounts = await _transfers.LockAccountsAsync([sourceAccountId, targetAccountId], ct);
        var source = accounts.SingleOrDefault(a => a.Id == sourceAccountId && a.UserId == userId)
                     ?? throw new NotFoundException("Hesap bulunamadı.");
        var target = accounts.SingleOrDefault(a => a.Id == targetAccountId && a.UserId == userId)
                     ?? throw new NotFoundException("Hesap bulunamadı.");

        var money = new Money(amount, source.Currency);
        // Both accounts are the customer's own, so the holder name always matches.
        var postings = FundsTransfer.Execute(source, target, money, target.HolderName);

        var stored = await _transfers.AddAsync(
            new NewTransfer(userId, idempotencyKey, hash, source, target, money, description, postings), ct);

        await _outbox.AddAsync(
            new TransferCompleted(stored.TransferId, source.Id, target.Id, money.Amount, money.Currency.Code, stored.CreatedAt), ct);

        return stored;
    }

    private static byte[] Hash(long sourceAccountId, long targetAccountId, decimal amount)
        => SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            "savings",
            sourceAccountId.ToString(CultureInfo.InvariantCulture),
            targetAccountId.ToString(CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture))));
}
