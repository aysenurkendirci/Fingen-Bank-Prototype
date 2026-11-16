# FinGen web

The Angular 21 app of FinGen: standalone components, signals, zoneless change detection, Tailwind with design tokens.
The whole project is described in the [main README](../README.md).

## Run

```bash
npm install
npm start          # http://localhost:4200, /api is proxied to the API on :5164
```

The API has to run as well; see the main README.

## Test

```bash
npx ng test --watch=false     # unit tests (Vitest)
npm run e2e                   # end-to-end tests (Playwright), against the API on :5164
node scripts/check-layers.mjs # the import rules between layers
```

The end-to-end tests sign up their own customers, so they need no fixed data. Locally they use the installed Chrome.

## Where things are

```
projects/bank-app/src/
  app/core/          session, HTTP interceptors, theme and voice, base page, date and IBAN helpers
  app/data-access/   one API client per resource, and the models it exchanges
  app/shared/        UI pieces, money display, charts, stores shared between screens
  app/features/      one folder per screen; a page component and the display components under it
  app/layout/        the signed-in frame and the sign-in frame
  design/            design-dna.json and theme.cjs, which turns it into CSS variables for Tailwind
  styles/            base styles, building blocks, motion, the young mode
public/              theme-init.js (applies the saved look before the first paint), icons
nginx.conf           what the container serves: security headers, a per-request style nonce, the /api proxy
```

A layer imports only from the layers below it; the table is in the main README and `scripts/check-layers.mjs` enforces it.

## Conventions

- Pages extend `BasePage` and own loading, saving and errors. Display components only take inputs and emit outputs.
- Forms extend `BaseForm` and emit a finished request; the page decides what to do with it.
- Money movements send an `Idempotency-Key` from `IdempotentAttempt`, so a retry never moves money twice.
- Colours come from tokens (`bg-brand`, `text-ink-soft`), never from hex values in templates.
- "Today" is the bank's day in Istanbul (`todayIsoDate`), the same calendar the API uses.
