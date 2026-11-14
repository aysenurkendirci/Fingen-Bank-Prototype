using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.Account;

/// <summary>How the customer wants the app to look, the same on every device they sign in from.</summary>
/// <param name="Theme"><c>system</c> follows the device; <c>light</c> and <c>dark</c> are fixed.</param>
/// <param name="Accent">The colour of actions and highlights.</param>
public sealed record PreferencesResponse(string Theme, string Accent)
{
    public static readonly PreferencesResponse Default = new(PreferenceValues.Themes[0], PreferenceValues.Accents[0]);
}

public sealed record UpdatePreferencesRequest(
    [Required(ErrorMessage = "Görünüm zorunludur.")]
    [AllowedValues("system", "light", "dark", ErrorMessage = "Görünüm system, light veya dark olmalıdır.")]
    string Theme,

    [Required(ErrorMessage = "Vurgu rengi zorunludur.")]
    [AllowedValues("spruce", "ocean", "plum", "graphite", ErrorMessage = "Vurgu rengi spruce, ocean, plum veya graphite olmalıdır.")]
    string Accent
);

/// <summary>The accepted values, first one being the default. The database checks the same lists.</summary>
public static class PreferenceValues
{
    public static readonly IReadOnlyList<string> Themes = ["system", "light", "dark"];
    public static readonly IReadOnlyList<string> Accents = ["spruce", "ocean", "plum", "graphite"];
}
