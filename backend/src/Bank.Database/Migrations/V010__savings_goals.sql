-- Savings goals ("kumbara"): each goal owns a savings account of its own, so money put aside moves through the
-- same ledger as every other transfer and every statement explains it.
--
-- A goal may carry an auto-save rule: when money of at least MinIncoming arrives on the source account (a salary,
-- say), a fixed amount or a percentage of it is moved to the goal in the same transaction. AutoSaveRuns records
-- which incoming transfer already triggered which goal, so a rule can never fire twice for the same money.

CREATE SCHEMA savings;
GO

ALTER TABLE dbo.Accounts DROP CONSTRAINT CK_Accounts_AccountType;
ALTER TABLE dbo.Accounts ADD CONSTRAINT CK_Accounts_AccountType CHECK (AccountType IN ('Demand', 'Time', 'ForeignCurrency', 'Savings'));
GO

CREATE TABLE savings.Goals
(
    GoalId                   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Goals PRIMARY KEY,
    UserId                   BIGINT          NOT NULL CONSTRAINT FK_Goals_Users REFERENCES dbo.Users (UserId),
    AccountId                BIGINT          NOT NULL CONSTRAINT FK_Goals_Accounts REFERENCES dbo.Accounts (AccountId),
    Name                     NVARCHAR(80)    NOT NULL,
    TargetAmount             DECIMAL(19, 2)  NOT NULL,
    TargetDate               DATE            NULL,
    -- Set for a new year's resolution: the year it is meant for.
    NewYearOf                SMALLINT        NULL,
    Status                   VARCHAR(10)     NOT NULL CONSTRAINT DF_Goals_Status DEFAULT 'Active',
    CreatedAt                DATETIME2(3)    NOT NULL,
    AutoSaveSourceAccountId  BIGINT          NULL CONSTRAINT FK_Goals_AutoSaveSource REFERENCES dbo.Accounts (AccountId),
    AutoSaveMode             VARCHAR(8)      NULL,
    AutoSaveValue            DECIMAL(19, 2)  NULL,
    AutoSaveMinIncoming      DECIMAL(19, 2)  NULL,

    CONSTRAINT UQ_Goals_Account UNIQUE (AccountId),
    CONSTRAINT CK_Goals_Target CHECK (TargetAmount > 0),
    CONSTRAINT CK_Goals_Status CHECK (Status IN ('Active', 'Closed')),
    CONSTRAINT CK_Goals_AutoSaveMode CHECK (AutoSaveMode IN ('Fixed', 'Percent')),
    -- A rule is either fully there or not at all.
    CONSTRAINT CK_Goals_AutoSaveComplete CHECK (
        (AutoSaveSourceAccountId IS NULL AND AutoSaveMode IS NULL AND AutoSaveValue IS NULL AND AutoSaveMinIncoming IS NULL)
        OR (AutoSaveSourceAccountId IS NOT NULL AND AutoSaveMode IS NOT NULL AND AutoSaveValue > 0 AND AutoSaveMinIncoming >= 0)),
    CONSTRAINT CK_Goals_AutoSavePercent CHECK (AutoSaveMode <> 'Percent' OR AutoSaveValue <= 100)
);

CREATE INDEX IX_Goals_User ON savings.Goals (UserId) INCLUDE (Status);
-- Every incoming transfer looks up the rules that watch its target account.
CREATE INDEX IX_Goals_AutoSaveSource ON savings.Goals (AutoSaveSourceAccountId) WHERE AutoSaveSourceAccountId IS NOT NULL AND Status = 'Active';

CREATE TABLE savings.AutoSaveRuns
(
    GoalId             BIGINT        NOT NULL CONSTRAINT FK_AutoSaveRuns_Goals REFERENCES savings.Goals (GoalId),
    TriggerTransferId  BIGINT        NOT NULL CONSTRAINT FK_AutoSaveRuns_Trigger REFERENCES payments.Transfers (TransferId),
    -- Empty when the rule fired but moved nothing (goal reached, not enough money left).
    TransferId         BIGINT        NULL CONSTRAINT FK_AutoSaveRuns_Transfer REFERENCES payments.Transfers (TransferId),
    CreatedAt          DATETIME2(3)  NOT NULL,

    CONSTRAINT PK_AutoSaveRuns PRIMARY KEY (GoalId, TriggerTransferId)
);
GO

-- Erasure also removes goals and their rule history. The procedure is otherwise unchanged from V009.
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
