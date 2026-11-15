-- What the customer is told inside the app: money that arrived or left, savings put aside, a goal reached,
-- a security change such as a new password or a blocked card. Written in the same transaction as the change it
-- describes, so a notification never announces something that was rolled back. Read ones are removed by the
-- housekeeping after a while; unread ones stay until the customer sees them.

CREATE TABLE dbo.Notifications
(
    NotificationId BIGINT IDENTITY (1, 1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId         BIGINT        NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES dbo.Users (UserId),
    Kind           VARCHAR(16)   NOT NULL CONSTRAINT CK_Notifications_Kind CHECK (Kind IN ('money-in', 'money-out', 'savings', 'goal', 'security')),
    Title          NVARCHAR(80)  NOT NULL,
    Body           NVARCHAR(300) NOT NULL,
    CreatedAt      DATETIME2(3)  NOT NULL,
    ReadAt         DATETIME2(3)  NULL
);
GO

-- The bell asks for the newest few and the unread count on every page.
CREATE INDEX IX_Notifications_User ON dbo.Notifications (UserId, NotificationId DESC) INCLUDE (ReadAt);
GO

-- Erasure also removes notifications. The procedure is otherwise unchanged from V010.
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

    -- How the person liked the app to look is still about the person.
    DELETE FROM dbo.UserPreferences WHERE UserId = @UserId;

    -- Goals are the person's own plans. Their savings accounts are closed with the other accounts below; the
    -- transfers into and out of them stay in the ledger like every other transfer.
    DELETE r FROM savings.AutoSaveRuns r JOIN savings.Goals g ON g.GoalId = r.GoalId WHERE g.UserId = @UserId;
    DELETE FROM savings.Goals WHERE UserId = @UserId;

    -- Notifications describe what happened to the person's money and sign-in.
    DELETE FROM dbo.Notifications WHERE UserId = @UserId;

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
