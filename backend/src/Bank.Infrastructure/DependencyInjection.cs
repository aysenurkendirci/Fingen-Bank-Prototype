using Bank.Application.Abstractions.Messaging;
using Bank.Application.Abstractions.Persistence;
using Bank.Application.Abstractions.Repositories;
using Bank.Application.Abstractions.Security;
using Bank.Infrastructure.Health;
using Bank.Infrastructure.Housekeeping;
using Bank.Infrastructure.MarketData;
using Bank.Infrastructure.Messaging;
using Bank.Infrastructure.Persistence;
using Bank.Infrastructure.Repositories;
using Bank.Infrastructure.Repositories.Finance;
using Bank.Infrastructure.Repositories.Payments;
using Bank.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BankDb")
            ?? throw new InvalidOperationException("Connection string 'BankDb' is missing.");

        services.AddSingleton(new SqlConnectionFactory(connectionString));

        // Shared by everything in a request that takes part in a unit of work (one connection, one transaction).
        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<IRecurringCashFlowRepository, RecurringCashFlowRepository>();
        services.AddScoped<IInstallmentPlanRepository, InstallmentPlanRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IBankCardRepository, BankCardRepository>();
        services.AddScoped<ITransactionHistoryRepository, TransactionHistoryRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<ISavingsGoalRepository, SavingsGoalRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IErasureRepository, ErasureRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISplitRepository, SplitRepository>();
        services.AddScoped<IUserSecurityRepository, UserSecurityRepository>();

        services.Configure<SignInPolicy>(configuration.GetSection(SignInPolicy.Section));
        // Required like the JWT key: without it a second factor secret could not be stored safely.
        var encryptionKey = configuration["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new InvalidOperationException(
                "Security:EncryptionKey is not set. Locally: dotnet user-secrets set \"Security:EncryptionKey\" \"<at least 32 random characters>\"");
        services.AddSingleton<ISecretProtector>(new AesGcmSecretProtector(
            encryptionKey, configuration.GetSection("Security:PreviousEncryptionKeys").Get<string[]>()));
        services.AddMemoryCache();
        services.AddMarketData(configuration);
        services.AddBreachedPasswords(configuration);

        var housekeeping = configuration.GetSection(HousekeepingOptions.Section);
        services.Configure<HousekeepingOptions>(housekeeping);
        services.AddSingleton<HousekeepingRunner>();
        if (housekeeping.GetValue(nameof(HousekeepingOptions.Enabled), true))
            services.AddHostedService<HousekeepingService>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        AddMessaging(services, configuration);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISessionPolicy, SessionPolicy>();

        // Code asks TimeProvider for the current time instead of DateTime.Now, so tests can pin it.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(MessagingOptions.Section);
        services.Configure<MessagingOptions>(section);

        // Events are always written to the outbox; whether they are forwarded to a broker is a deployment choice.
        services.AddScoped<IOutbox, OutboxWriter>();

        if (!section.GetValue<bool>(nameof(MessagingOptions.Enabled)))
            return;

        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxPublisherService>();
    }
}
