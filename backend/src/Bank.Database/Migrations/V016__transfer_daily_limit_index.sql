-- The daily transfer limit adds up what a customer sent to others today. Without this index the query read every
-- transfer of the customer through the idempotency key index; with it, it reads only today's, and the columns it
-- needs come from the index itself.

CREATE INDEX IX_Transfers_User_CreatedAt
    ON payments.Transfers (UserId, CreatedAt)
    INCLUDE (CurrencyCode, Amount, TargetAccountId);
