namespace Bank.Infrastructure.MarketData.VakifBank;

/// <summary>
/// VakıfBank API Portal (Yatırım Servisleri) settings, section "MarketData:VakifBank". The client id and secret come
/// from user-secrets or the environment, never from a file in the repository; without them the source is not
/// registered and TCMB answers alone.
/// </summary>
public sealed class VakifBankOptions
{
    public const string Section = "MarketData:VakifBank";

    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    /// <summary>The API gateway the services are published on.</summary>
    public Uri BaseUrl { get; init; } = new("https://inbound.apigateway.vakifbank.com.tr:8443/");

    /// <summary>Where a client credentials token is asked for; check it against the portal's "Yetkilendirme" page.</summary>
    public Uri TokenUrl { get; init; } = new("https://apigw.vakifbank.com.tr:8443/auth/oauth/v2/token");

    /// <summary>The scopes the portal lists: "oob public" for the rates, "public" for the calculator.</summary>
    public string Scope { get; init; } = "oob public";

    public bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
