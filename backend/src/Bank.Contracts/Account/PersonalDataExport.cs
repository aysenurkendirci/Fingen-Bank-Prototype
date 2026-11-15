using Bank.Contracts.Finance;
using Bank.Contracts.Notifications;
using Bank.Contracts.Savings;

namespace Bank.Contracts.Account;

/// <summary>
/// Everything the platform stores about the requesting user, for the KVKK right of access (article 11).
/// </summary>
public sealed record PersonalDataExport(
    DateTime ExportedAt,
    PersonalProfile Profile,
    PreferencesResponse Preferences,
    IReadOnlyList<CreditCardResponse> CreditCards,
    IReadOnlyList<RecurringCashFlowResponse> RecurringCashFlows,
    IReadOnlyList<InstallmentPlanResponse> InstallmentPlans,
    IReadOnlyList<HistoryTransactionResponse> Transactions,
    IReadOnlyList<GoalResponse> SavingsGoals,
    IReadOnlyList<NotificationResponse> Notifications,
    IReadOnlyList<GoalPhotoExport> GoalPhotos
);

/// <summary>A photo the customer put on a goal, as uploaded.</summary>
public sealed record GoalPhotoExport(long GoalId, string ContentType, string Base64, DateTime UploadedAt);

public sealed record PersonalProfile(
    string TcNo,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Membership,
    DateTime RegisteredAt,
    DateTime? LastLoginAt
);
