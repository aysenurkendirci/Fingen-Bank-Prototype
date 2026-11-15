using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Contracts.Account;
using MediatR;

namespace Bank.Application.Features.Preferences;

/// <summary>The signed-in customer's display preferences, or the defaults if they never changed any.</summary>
public sealed record GetPreferencesQuery : IRequest<PreferencesResponse>;

public sealed class GetPreferencesQueryHandler : IRequestHandler<GetPreferencesQuery, PreferencesResponse>
{
    private readonly IUserPreferencesRepository _preferences;
    private readonly ICurrentUser _currentUser;

    public GetPreferencesQueryHandler(IUserPreferencesRepository preferences, ICurrentUser currentUser)
    {
        _preferences = preferences;
        _currentUser = currentUser;
    }

    public async Task<PreferencesResponse> Handle(GetPreferencesQuery query, CancellationToken ct)
        => PreferencesMapping.ToResponse(await _preferences.GetAsync(_currentUser.UserId, ct));
}

/// <summary>Saves the display preferences. The request's allowed values are checked by its contract.</summary>
public sealed record UpdatePreferencesCommand(UpdatePreferencesRequest Preferences) : IRequest<PreferencesResponse>;

public sealed class UpdatePreferencesCommandHandler : IRequestHandler<UpdatePreferencesCommand, PreferencesResponse>
{
    private readonly IUserPreferencesRepository _preferences;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public UpdatePreferencesCommandHandler(IUserPreferencesRepository preferences, ICurrentUser currentUser, TimeProvider time)
    {
        _preferences = preferences;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<PreferencesResponse> Handle(UpdatePreferencesCommand command, CancellationToken ct)
    {
        var (theme, accent, mode) = (command.Preferences.Theme, command.Preferences.Accent, command.Preferences.Mode);
        await _preferences.SaveAsync(_currentUser.UserId, theme, accent, mode, _time.GetUtcNow().UtcDateTime, ct);
        return new PreferencesResponse(theme, accent, mode);
    }
}

internal static class PreferencesMapping
{
    public static PreferencesResponse ToResponse(UserPreferencesRow? row)
        => row is null ? PreferencesResponse.Default : new PreferencesResponse(row.Theme, row.Accent, row.Mode);
}
