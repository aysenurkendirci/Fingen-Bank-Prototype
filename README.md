# FinGen

A retail bank with a personal finance planner built in. Customers hold accounts and cards, send money by IBAN and save towards
goals; the planner shows the months ahead day by day, so a short month is visible before it arrives. A portfolio project, not a
live service.

The planner only shows the customer's own obligations. It never recommends a product or a decision.

## What it does

Banking
- Accounts in TRY, USD and EUR with generated IBANs, statements read from a double-entry ledger, closing an empty account.
- Transfers by IBAN with a review step and a receipt. Each transfer carries an Idempotency-Key, so a retry never sends the money
  twice.
- Debit and credit cards: contactless and online use, daily and monthly limits, a temporary block.

Savings
- Goals with their own savings account. Money goes in and out through the ledger like any other transfer.
- Auto-save: when at least a set amount arrives on an account (a salary), a percentage or a fixed amount moves to the goal in the
  same transaction.
- New year's resolutions as goals for a given year, shown in their own area.

Planning and insight
- Cash-flow projection from known income, expenses, subscriptions, card statements and installments.
- Installment scenarios compared side by side before a purchase.
- Statement import from other banks and analytics on the history: category trends, recurring payments, unusual spending.
- Exchange rates from TCMB's public daily rates, a converter, and foreign currency balances estimated in lira.

Customer
- Profile, contact details, password change, data export and account erasure under KVKK.
- Light, dark or system theme and an accent colour, saved with the account.

## Banking rules

| Area | Rule |
|---|---|
| Transfers | Same currency only. The recipient's name must match the IBAN holder. The sender's balance must cover the amount. |
| Limits | Money sent to other customers is capped per transfer and per business day, per currency (TRY 250,000 and 750,000 by default). Moves between a customer's own accounts and goals are not limited. |
| Accounts | A frozen or closed account neither sends nor receives. At most 5 open accounts per currency. Only an empty account closes, and the last lira demand account stays open. |
| Savings | A goal's savings account only moves money to the owner's own accounts. A goal closes once its savings account is empty. Auto-save never goes past the target and never blocks the payment that triggered it. |
| Cards | The daily limit cannot exceed the monthly limit. A temporary block can be lifted; cancellation is final, and a cancelled card can no longer be changed. |
| Customers | The T.C. identity number is checked with its check digits. Phone numbers must be Turkish mobile numbers. Passwords need 12 characters with a letter and a digit. |
| Ledger | Every transfer is two postings that balance. Balances never go negative, enforced by the database as a last line of defence. |

Limits are read from the `Banking` section of `appsettings.json`. The reasoning is in [ADR-0013](docs/adr/0013-banking-rules.md).

## Security

Short-lived access tokens kept in memory, rotating refresh cookies with reuse detection, rate limits on sign-in and on money
movements, a strict Content-Security-Policy, and secret scanning, CodeQL and Dependabot in CI. The threat model is in
[SECURITY.md](SECURITY.md).

## Stack

.NET 10, ASP.NET Core, MediatR, FluentValidation, Dapper, SQL Server (DbUp migrations), RabbitMQ, Serilog, Angular 21,
Tailwind, ECharts. xUnit, Vitest and Playwright for tests, GitHub Actions for CI.

## Layout

```
backend/src/
  Bank.Domain/          business rules: accounts, transfers, limits, cards, projection, scenarios
  Bank.Application/     commands and queries (Features/), pipeline behaviours
  Bank.Infrastructure/  repositories, outbox, RabbitMQ, market data, security
  Bank.Api/             controllers, middleware, rate limits
  Bank.Contracts/       DTOs and events
  Bank.Database/        DbUp migrator and SQL scripts
backend/tests/          unit and integration tests
frontend/               Angular app; design tokens in projects/bank-app/src/design
docs/                   decisions (adr/), notes, analytics evaluation
```

## Run

With Docker:

```bash
cp .env.example .env    # fill in the secrets
docker compose up --build
```

The web app is on http://localhost:8080.

Without Docker you need the .NET 10 SDK, Node 22 and a local SQL Server:

```bash
cd backend
dotnet run --project src/Bank.Database      # creates BankDb, applies migrations (the API does not run them itself)

cd src/Bank.Api
dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>"
dotnet run                                  # http://localhost:5164

cd ../../../frontend
npm install
npm start                                   # http://localhost:4200
```

After signing up, İşlem geçmişi > 12 aylık demo verisi oluştur fills the account with sample data.

## Test

```bash
cd backend && dotnet test                   # integration tests need SQL Server
cd frontend && npx ng test --watch=false
cd frontend && npm run e2e                  # Playwright, against the API on :5164
```

Integration tests run against a real database, `BankDb_IntegrationTests`, rebuilt from the migrations on every run. Set
`INTEGRATION_TESTS_DB` to use another server. The RabbitMQ test only runs when `RABBITMQ_HOST` is set. Outside services such as
TCMB are replaced by fakes, so the suite never needs the internet.

The end-to-end tests in `frontend/e2e` sign up their own customers and walk through accounts, transfers, planning, goals,
the profile and erasure in a real browser. CI runs them against a fresh database, and also starts the whole system with
`docker compose` and checks it through nginx.

## Docs

- [Decisions](docs/adr/)
- [Security](SECURITY.md)
- [Notes](docs/ENGINEERING_LOG.md): problems that took a while to solve
- [TODO](docs/TODO.md)
- [Analytics evaluation](docs/analytics/EVALUATION.md)
