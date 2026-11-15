using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Application.Common.Exceptions;
using MediatR;

namespace Bank.Application.Features.Savings;

/// <summary>Puts a photo on a goal, or replaces the one there. The bytes decide the type, never the file name.</summary>
public sealed record SetGoalPhotoCommand(long GoalId, byte[] Content) : IRequest;

public sealed record GetGoalPhotoQuery(long GoalId) : IRequest<GoalPhoto>;

public sealed record DeleteGoalPhotoCommand(long GoalId) : IRequest;

/// <summary>
/// What a goal photo may be. The app shrinks a picture before sending it, so the limit is generous for a photo
/// and still small for a database row.
/// </summary>
public static class GoalPhotoRules
{
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>JPEG, PNG or WebP, recognised by their first bytes; null for anything else.</summary>
    public static string? DetectType(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return "image/jpeg";
        if (content.Length >= 8 && content[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}

public sealed class GoalPhotoHandlers :
    IRequestHandler<SetGoalPhotoCommand>,
    IRequestHandler<GetGoalPhotoQuery, GoalPhoto>,
    IRequestHandler<DeleteGoalPhotoCommand>
{
    private const string GoalNotFound = "Hedef bulunamadı.";

    private readonly ISavingsGoalRepository _goals;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public GoalPhotoHandlers(ISavingsGoalRepository goals, ICurrentUser currentUser, TimeProvider time)
    {
        _goals = goals;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task Handle(SetGoalPhotoCommand command, CancellationToken ct)
    {
        if (command.Content.Length == 0)
            throw new BadRequestException("Fotoğraf boş.");
        if (command.Content.Length > GoalPhotoRules.MaxBytes)
            throw new BadRequestException("Fotoğraf en fazla 2 MB olabilir.");

        var type = GoalPhotoRules.DetectType(command.Content)
                   ?? throw new BadRequestException("Yalnızca JPEG, PNG ya da WebP fotoğraf eklenebilir.");

        var photo = new GoalPhoto(type, command.Content, _time.GetUtcNow().UtcDateTime);
        if (!await _goals.SavePhotoAsync(_currentUser.UserId, command.GoalId, photo, ct))
            throw new NotFoundException(GoalNotFound);
    }

    public async Task<GoalPhoto> Handle(GetGoalPhotoQuery query, CancellationToken ct)
        => await _goals.GetPhotoAsync(_currentUser.UserId, query.GoalId, ct) ?? throw new NotFoundException("Fotoğraf bulunamadı.");

    public async Task Handle(DeleteGoalPhotoCommand command, CancellationToken ct)
    {
        if (!await _goals.DeletePhotoAsync(_currentUser.UserId, command.GoalId, ct))
            throw new NotFoundException(GoalNotFound);
    }
}
