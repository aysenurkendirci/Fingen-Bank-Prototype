using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Bank.Api.Security;

/// <summary>
/// Caps how many money movements one customer can start per minute: transfers, goal deposits and withdrawals,
/// opening accounts and goals. A stolen session or a runaway script then cannot drain or spam an account at
/// machine speed, and the database is spared bursts of locking work.
/// </summary>
/// <remarks>
/// Partitioned by the signed-in user, so customers behind one office address do not share a budget. The
/// limiter runs after authentication for that reason; an unauthenticated request never reaches these endpoints.
/// </remarks>
public static class MoneyRateLimiting
{
    public const string Policy = "money";

    public static IServiceCollection AddMoneyRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:Money:PermitLimit", 30);
        var window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:Money:WindowSeconds", 60));

        return services.AddRateLimiter(options =>
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 })));
    }
}
