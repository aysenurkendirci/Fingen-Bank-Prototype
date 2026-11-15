using System.ComponentModel.DataAnnotations;

namespace Bank.Contracts.Account;

/// <summary>How the customer wants the app to look, the same on every device they sign in from.</summary>
/// <param name="Theme"><c>system</c> follows the device; <c>light</c> and <c>dark</c> are fixed.</param>
/// <param name="Accent">The colour of actions and highlights.</param>
/// <param name="Mode"><c>standard</c>, or <c>young</c>: brighter, rounder, on first-name terms and led by savings.</param>
public sealed record PreferencesResponse(string Theme, string Accent, string Mode = "standard")
{
    public static readonly PreferencesResponse Default = new(PreferenceValues.Themes[0], PreferenceValues.Accents[0], PreferenceValues.Modes[0]);
}

public sealed record UpdatePreferencesRequest(
    [Required(ErrorMessage = "Görünüm zorunludur.")]
    [AllowedValues("system", "light", "dark", ErrorMessage = "Görünüm system, light veya dark olmalıdır.")]
    string Theme,

    [Required(ErrorMessage = "Vurgu rengi zorunludur.")]
    [AllowedValues("spruce", "ocean", "plum", "graphite", ErrorMessage = "Vurgu rengi spruce, ocean, plum veya graphite olmalıdır.")]
    string Accent,

    // Optional, so a client that does not know about modes keeps the standard one.
    [AllowedValues("standard", "young", ErrorMessage = "Görünüm türü standard veya young olmalıdır.")]
    string Mode = "standard"
);

/// <summary>The accepted values, first one being the default. The database checks the same lists.</summary>
public static class PreferenceValues
{
    public static readonly IReadOnlyList<string> Themes = ["system", "light", "dark"];
    public static readonly IReadOnlyList<string> Accents = ["spruce", "ocean", "plum", "graphite"];
    public static readonly IReadOnlyList<string> Modes = ["standard", "young"];
}
