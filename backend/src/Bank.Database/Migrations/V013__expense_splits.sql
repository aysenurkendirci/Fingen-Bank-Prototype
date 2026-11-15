-- Splitting an expense with friends. The customer who paid records the expense and who owes what. A friend who
-- banks here is named by IBAN and their name must match the account holder, as in a transfer; they see the
-- request and pay it with an ordinary transfer to the payer's account, which settles their share. A friend who
-- does not bank here is a name only, and the payer marks the share as paid once they get the money.
--
-- The payer's own part is what is left of the total and has no row. A share is settled by at most one transfer,
-- and a transfer settles at most one share.

CREATE SCHEMA splits;
GO

CREATE TABLE splits.Splits
(
    SplitId            BIGINT IDENTITY (1, 1) NOT NULL CONSTRAINT PK_Splits PRIMARY KEY,
    OwnerUserId        BIGINT         NOT NULL CONSTRAINT FK_Splits_Users REFERENCES dbo.Users (UserId),
    -- Where the friends' payments go: one of the payer's own lira accounts.
    ReceivingAccountId BIGINT         NOT NULL CONSTRAINT FK_Splits_Accounts REFERENCES dbo.Accounts (AccountId),
    Title              NVARCHAR(80)   NOT NULL,
    TotalAmount        DECIMAL(19, 2) NOT NULL CONSTRAINT CK_Splits_Total CHECK (TotalAmount > 0),
    CurrencyCode       CHAR(3)        NOT NULL CONSTRAINT CK_Splits_Currency CHECK (CurrencyCode = 'TRY'),
    SpentOn            DATE           NOT NULL,
    Status             VARCHAR(10)    NOT NULL CONSTRAINT DF_Splits_Status DEFAULT 'Open'
                                      CONSTRAINT CK_Splits_Status CHECK (Status IN ('Open', 'Cancelled')),
    CreatedAt          DATETIME2(3)   NOT NULL
);

CREATE INDEX IX_Splits_Owner ON splits.Splits (OwnerUserId, SplitId DESC);

CREATE TABLE splits.Shares
(
    ShareId      BIGINT IDENTITY (1, 1) NOT NULL CONSTRAINT PK_Shares PRIMARY KEY,
    SplitId      BIGINT         NOT NULL CONSTRAINT FK_Shares_Splits REFERENCES splits.Splits (SplitId),
    -- The name as the payer wrote it; for a customer of this bank it matched the holder of DebtorAccountIban.
    Name         NVARCHAR(100)  NOT NULL,
    DebtorUserId BIGINT         NULL CONSTRAINT FK_Shares_Users REFERENCES dbo.Users (UserId),
    Amount       DECIMAL(19, 2) NOT NULL CONSTRAINT CK_Shares_Amount CHECK (Amount > 0),
    Status       VARCHAR(10)    NOT NULL CONSTRAINT DF_Shares_Status DEFAULT 'Open'
                                CONSTRAINT CK_Shares_Status CHECK (Status IN ('Open', 'Paid', 'Declined', 'Cancelled')),
    -- Set when the share was paid in the app; a share paid in cash has none.
    TransferId   BIGINT         NULL CONSTRAINT FK_Shares_Transfers REFERENCES payments.Transfers (TransferId),
    SettledAt    DATETIME2(3)   NULL,

    CONSTRAINT CK_Shares_Settled CHECK ((Status = 'Open') OR SettledAt IS NOT NULL),
    CONSTRAINT CK_Shares_TransferOnlyWhenPaid CHECK (TransferId IS NULL OR Status = 'Paid')
);

CREATE INDEX IX_Shares_Split ON splits.Shares (SplitId);
-- "What do others ask of me" is read on every visit to the screen.
CREATE INDEX IX_Shares_Debtor ON splits.Shares (DebtorUserId, Status) WHERE DebtorUserId IS NOT NULL;
-- One transfer settles one share at most.
CREATE UNIQUE INDEX UX_Shares_Transfer ON splits.Shares (TransferId) WHERE TransferId IS NOT NULL;
GO

-- Requests to pay a share, and their answers, reach the notification centre too.
ALTER TABLE dbo.Notifications DROP CONSTRAINT CK_Notifications_Kind;
ALTER TABLE dbo.Notifications ADD CONSTRAINT CK_Notifications_Kind
    CHECK (Kind IN ('money-in', 'money-out', 'savings', 'goal', 'security', 'split'));
GO

-- Erasure also removes the splits the person made. Shares others asked of them lose the link to the person and
-- stay with the payer as a name, because they are part of someone else's records. Otherwise unchanged from V012.
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
