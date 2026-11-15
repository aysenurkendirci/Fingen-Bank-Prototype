-- Goals become a wish list: what kind of thing is wanted (a car, a home, a trip), the details in the customer's own
-- words (the model, the colour, where to) and, if they like, a photo of it. The photo sits in a table of its own so
-- listing goals never reads image data, and it is limited in size and type by the database as well as the API.

ALTER TABLE savings.Goals ADD
    Category VARCHAR(16)   NULL CONSTRAINT CK_Goals_Category CHECK (Category IN ('car', 'home', 'travel', 'education', 'tech', 'wedding', 'other')),
    Details  NVARCHAR(300) NULL;
GO

CREATE TABLE savings.GoalPhotos
(
    GoalId      BIGINT          NOT NULL CONSTRAINT PK_GoalPhotos PRIMARY KEY CONSTRAINT FK_GoalPhotos_Goals REFERENCES savings.Goals (GoalId),
    ContentType VARCHAR(16)     NOT NULL CONSTRAINT CK_GoalPhotos_Type CHECK (ContentType IN ('image/jpeg', 'image/png', 'image/webp')),
    Content     VARBINARY(MAX)  NOT NULL CONSTRAINT CK_GoalPhotos_Size CHECK (DATALENGTH(Content) BETWEEN 1 AND 2097152),
    UploadedAt  DATETIME2(3)    NOT NULL
);
GO

-- Erasure also removes the photos. The procedure is otherwise unchanged from V011.
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
