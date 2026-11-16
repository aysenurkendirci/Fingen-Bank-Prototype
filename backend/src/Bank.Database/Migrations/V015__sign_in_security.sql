-- Sign-in protection per customer: failed attempts lock the account for a while, and an optional second factor
-- (a time-based code from an authenticator app, RFC 6238). The factor's secret is stored encrypted by the
-- application; the database never holds it in the clear. TotpLastStep remembers the last code used, so one code
-- cannot sign in twice. A customer without a row has no failures and no second factor.

CREATE TABLE dbo.UserSecurity
(
    UserId             BIGINT          NOT NULL CONSTRAINT PK_UserSecurity PRIMARY KEY
                                                CONSTRAINT FK_UserSecurity_Users REFERENCES dbo.Users (UserId),
    FailedSignIns      INT             NOT NULL CONSTRAINT DF_UserSecurity_Failed DEFAULT 0
                                                CONSTRAINT CK_UserSecurity_Failed CHECK (FailedSignIns >= 0),
    LockedUntil        DATETIME2(3)    NULL,
    -- Set while the customer is setting the factor up and has not yet proved it works.
    TotpPendingSecret  VARBINARY(128)  NULL,
    TotpSecret         VARBINARY(128)  NULL,
    TotpEnabledAt      DATETIME2(3)    NULL,
    TotpLastStep       BIGINT          NULL,

    -- A secret is in use exactly when the factor is switched on.
    CONSTRAINT CK_UserSecurity_TotpEnabled CHECK ((TotpSecret IS NULL AND TotpEnabledAt IS NULL) OR (TotpSecret IS NOT NULL AND TotpEnabledAt IS NOT NULL))
);
GO

-- Erasure also removes the sign-in protection. The procedure is otherwise unchanged from V013.
CREATE OR ALTER PROCEDURE compliance.EraseUser
    @UserId BIGINT,
    @Now    DATETIME2(3)
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @UserId)
        THROW 50001, 'User does not exist.', 1;

    -- Erasing twice is harmless.
    IF EXISTS (SELECT 1 FROM compliance.ErasedIdentities WHERE UserId = @UserId)
        RETURN;

    -- Money must be moved out first; closing an account that still holds some would make it disappear from view.
    IF EXISTS (SELECT 1 FROM dbo.Accounts WHERE UserId = @UserId AND Balance <> 0)
        THROW 50002, 'Accounts still hold a balance.', 1;

    BEGIN TRANSACTION;

    ALTER TABLE finance.CreditCards SET (SYSTEM_VERSIONING = OFF);
    ALTER TABLE finance.RecurringCashFlows SET (SYSTEM_VERSIONING = OFF);
    ALTER TABLE finance.InstallmentPlans SET (SYSTEM_VERSIONING = OFF);

    -- History first, then the rows that reference cards, then the cards themselves. The history deletes are
    -- dynamic SQL on purpose: while a table is system-versioned SQL Server refuses to even compile a DELETE
    -- against its history table, and the tables are only unversioned while this procedure runs.
    DECLARE @deleteHistory NVARCHAR(MAX) = N'
        DELETE FROM finance.InstallmentPlansHistory WHERE UserId = @UserId;
        DELETE FROM finance.RecurringCashFlowsHistory WHERE UserId = @UserId;
        DELETE FROM finance.CreditCardsHistory WHERE UserId = @UserId;';
    EXEC sp_executesql @deleteHistory, N'@UserId BIGINT', @UserId = @UserId;

    DELETE FROM finance.InstallmentPlans WHERE UserId = @UserId;
    DELETE FROM finance.RecurringCashFlows WHERE UserId = @UserId;
    DELETE FROM finance.CreditCards WHERE UserId = @UserId;
    DELETE FROM finance.Transactions WHERE UserId = @UserId;
    DELETE FROM finance.ImportBatches WHERE UserId = @UserId;

    ALTER TABLE finance.CreditCards
        SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = finance.CreditCardsHistory, DATA_CONSISTENCY_CHECK = OFF));
    ALTER TABLE finance.RecurringCashFlows
        SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = finance.RecurringCashFlowsHistory, DATA_CONSISTENCY_CHECK = OFF));
    ALTER TABLE finance.InstallmentPlans
        SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = finance.InstallmentPlansHistory, DATA_CONSISTENCY_CHECK = OFF));

    -- Every open session ends. (One statement, so the self-reference between replaced tokens is not a problem.)
    DELETE FROM dbo.RefreshTokens WHERE UserId = @UserId;

    -- The second factor's secret and the sign-in counters go with the person.
    DELETE FROM dbo.UserSecurity WHERE UserId = @UserId;

    -- How the person liked the app to look is still about the person.
    DELETE FROM dbo.UserPreferences WHERE UserId = @UserId;

    -- Goals are the person's own plans. Their savings accounts are closed with the other accounts below; the
    -- transfers into and out of them stay in the ledger like every other transfer.
    DELETE r FROM savings.AutoSaveRuns r JOIN savings.Goals g ON g.GoalId = r.GoalId WHERE g.UserId = @UserId;
    DELETE p FROM savings.GoalPhotos p JOIN savings.Goals g ON g.GoalId = p.GoalId WHERE g.UserId = @UserId;
    DELETE FROM savings.Goals WHERE UserId = @UserId;

    -- Notifications describe what happened to the person's money and sign-in.
    DELETE FROM dbo.Notifications WHERE UserId = @UserId;

    -- Splits the person made go with their shares. Shares others asked of the person stay with those others,
    -- unlinked from the erased user.
    DELETE s FROM splits.Shares s JOIN splits.Splits p ON p.SplitId = s.SplitId WHERE p.OwnerUserId = @UserId;
    DELETE FROM splits.Splits WHERE OwnerUserId = @UserId;
    UPDATE splits.Shares SET DebtorUserId = NULL WHERE DebtorUserId = @UserId;

    UPDATE dbo.Accounts SET Status = 'Closed' WHERE UserId = @UserId;
    UPDATE dbo.Cards SET Status = 'Cancelled' WHERE UserId = @UserId;

    INSERT INTO compliance.ErasedIdentities (UserId, TcNo, ErasedAt)
    SELECT UserId, TcNo, @Now FROM dbo.Users WHERE UserId = @UserId;

    -- The row stays because accounts and the ledger point at it. No field is left that identifies the person,
    -- and the password hash is not a valid hash, so nothing can sign in as this user.
    UPDATE dbo.Users
    SET TcNo         = CONCAT('E', RIGHT(CONCAT('0000000000', UserId), 10)),
        FirstName    = N'Silinmiş',
        LastName     = N'Kullanıcı',
        Email        = CONCAT('erased-', UserId, '@erased.invalid'),
        Phone        = '0',
        PasswordHash = '!',
        LastLoginAt  = NULL
    WHERE UserId = @UserId;

    COMMIT TRANSACTION;
END
