# InvestHorizon — CLAUDE.md

Belgian investment portfolio tracker. Replaces an Excel prototype (`docs/Investissements.xlsx`).
Tracks buy/sell transactions per broker, computes all Belgian costs (TOB, broker fees, custody),
applies FIFO lot matching, and reports annual capital-gains tax.

---

## Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 · ASP.NET Core Web API · EF Core 8 · Npgsql · ASP.NET Identity |
| Database | PostgreSQL 16 |
| Frontend | React 18 · Vite · TypeScript · Mantine v7 · React Query · React Router · axios |
| Containers | Docker Compose (db + api + web/nginx) |
| Tests | xUnit + FluentAssertions (backend) · Playwright (E2E) |
| CI | GitHub Actions (`.github/workflows/ci.yml`) |

---

## Repository layout

```
InvestHorizon/
├── backend/
│   ├── InvestHorizon.sln
│   ├── Dockerfile
│   ├── .dockerignore          # excludes obj/ and bin/ so Windows paths don't break Linux builds
│   ├── src/
│   │   ├── InvestHorizon.Domain/         # entities, enums — no dependencies
│   │   ├── InvestHorizon.Application/    # cost engine, services, repository interfaces
│   │   ├── InvestHorizon.Infrastructure/ # EF Core DbContext, Identity, repositories, seeder
│   │   └── InvestHorizon.Api/            # controllers, DTOs, auth, Program.cs
│   └── tests/
│       └── InvestHorizon.Tests/          # xUnit unit tests for cost engine
├── frontend/
│   ├── src/
│   │   ├── api/               # typed axios wrappers (client.ts, types.ts, portfolios/instruments/transactions)
│   │   ├── auth/              # AuthContext (JWT), RequireAuth guard
│   │   └── pages/             # LoginPage, DashboardPage, HoldingsPage, TransactionsPage,
│   │                          #   TransactionForm (with live cost preview), InstrumentsPage, RealizedPage
│   ├── e2e/                   # Playwright tests (auth.spec.ts, portfolio.spec.ts)
│   ├── Dockerfile
│   ├── nginx.conf             # SPA fallback + /api proxy to api container
│   └── playwright.config.ts
├── docker-compose.yml
├── docker-compose.override.yml   # dev mode: dotnet watch + Vite dev server
├── .github/workflows/ci.yml
└── docs/
    └── Investissements.xlsx       # original Excel prototype
```

---

## Running the project

### Full stack (production-like)
```bash
docker compose up --build
# Web UI: http://localhost:8080
# API:    http://localhost:5000
```

### Local development (recommended)
```bash
# 1. Start only db + api
docker compose up db api -d

# 2. Run frontend dev server with HMR
cd frontend && npm run dev
# → http://localhost:5173 (Vite proxies /api to localhost:5000)
```

### Backend only (no Docker)
```bash
cd backend
dotnet run --project src/InvestHorizon.Api
# Requires a local Postgres instance matching appsettings.json connection string
```

### Default credentials (seeded on first startup)
- Email: `admin@investhorizon.local`
- Password: `Admin1234!`

Configurable via environment variables `Seed__UserEmail` / `Seed__UserPassword` or in `appsettings.json`.

---

## Common commands

```bash
# Backend
cd backend
dotnet build InvestHorizon.sln
dotnet test                          # unit tests
dotnet dotnet-ef migrations add <Name> \
  --project src/InvestHorizon.Infrastructure \
  --startup-project src/InvestHorizon.Api \
  --output-dir Persistence/Migrations

# Frontend
cd frontend
npm run dev          # dev server
npx tsc --noEmit     # type check
npm run build        # production build
npx playwright test  # E2E (requires stack running at BASE_URL)
```

---

## Architecture

### Backend layers (strict dependency direction: Domain ← Application ← Infrastructure ← Api)

**Domain** — pure C# classes, no framework references.
- `Entities/`: `Portfolio`, `Instrument`, `Transaction`, `SaleAllocation`
- `Enums/`: `Broker` (Keytrade, Revolut), `InstrumentType` (Etf, Share, Bond, CapitalizingFund), `TransactionSide` (Buy, Sell)

**Application** — business logic, no I/O.
- `CostEngine/`: `TransactionCostEngine` (orchestrator), `KeytradeFeeCalculator`, `RevolutFeeCalculator`, `BelgianTobCalculator`, `FifoMatcher`, `CapitalGainsTaxService`
- `Services/`: `TransactionService` (create + FIFO), `HoldingsService`, `RealizedGainsService`
- `Interfaces/`: repository contracts + `IBrokerFeeCalculator`, `ITobCalculator`, `IFifoMatcher`, `ICapitalGainsTaxService`

**Infrastructure** — EF Core, Identity, repositories.
- `AppDbContext` extends `IdentityDbContext<AppUser>`
- `DatabaseSeeder` — creates the initial user on startup if it doesn't exist
- EF migrations live in `src/InvestHorizon.Infrastructure/Persistence/Migrations/`

**Api** — thin controllers + DTOs.
- All enums serialized as strings (`JsonStringEnumConverter` registered in `Program.cs`)
- JWT bearer auth; all endpoints except `POST /api/auth/login` require `[Authorize]`
- Migrations run automatically on startup before the seeder

### Frontend

- **Auth**: JWT stored in `localStorage`. `AuthContext` exposes `login/logout`. `RequireAuth` wraps protected routes. Axios interceptor attaches the token and redirects to `/login` on 401.
- **API client**: `src/api/client.ts` (axios instance with `/api` base URL). Typed wrappers in `portfolios.ts`, `instruments.ts`, `transactions.ts`.
- **Live cost preview**: `TransactionForm` debounces inputs (400 ms) and calls `POST /api/transactions/preview` to show fee + TOB + total before saving.
- **State**: React Query for server state; no global client state store.

---

## Data model

```
AppUser (Identity)
  └── Portfolio (UserId FK)
        └── Transaction (PortfolioId FK, InstrumentId FK)
              ├── as BuyTransaction  → SaleAllocation
              └── as SellTransaction → SaleAllocation

Instrument (global, not per-user)
```

**Transaction key fields:**
- `UnitPrice`, `Quantity`, `Currency`, `FxRate` (1 EUR = x Currency; always 1 for EUR)
- `ManualBrokerFee` (nullable override; if null, engine computes from broker tier)
- `CustodyFee` (droits de garde, nullable, manual entry)
- Computed & persisted: `AmountNative`, `AmountEur`, `BrokerFee`, `TobAmount`, `TotalCost` (Buy), `NetProceeds` (Sell), `RemainingQuantity`

**SaleAllocation** — produced by FIFO matching when a Sell is saved. Links a sell to one or more buy lots with `Quantity` and `RealizedGainEur` per allocation.

---

## Cost & tax engine

### Broker fees (`IBrokerFeeCalculator`)
Strategy pattern; one calculator per `Broker` enum value, resolved at runtime. The fee is a
function of `(amountEur, side, instrumentType)`. Fees are symmetric on buy and sell.

| Broker | Shape of the rule |
|--------|-------------------|
| Keytrade | Block grid by order value (Euronext) |
| Revolut | Flat percentage with a minimum |
| MeDirect | Per instrument type; ETFs free, otherwise percentage with a minimum |

Exact rates/tiers are hardcoded in each calculator under `Application/CostEngine/` — that code is
the source of truth (scoped to Euronext; other exchanges via `ManualBrokerFee`). Update the rules
there, not here. If `ManualBrokerFee` is set on a transaction, it overrides the computed fee.

### Belgian TOB (`BelgianTobCalculator`)
Symmetric on buy and sell. Applied to `AmountEur`. Each `InstrumentType` has its own rate and a
per-order cap; the exact figures are hardcoded in `BelgianTobCalculator` (the source of truth) —
update them there, not here.

### FIFO matching (`FifoMatcher`)
When a Sell transaction is created, open buy lots for the same (Portfolio, Instrument) are fetched ordered by date ASC, then Id ASC. Sell quantity is consumed from oldest lots first. Each consumed slice produces a `SaleAllocation` with `RealizedGainEur = sellProceedsShare − buyCostBasisShare` (both proportional, EUR, net of costs). `RemainingQuantity` on consumed buy lots is updated atomically.

### Capital-gains tax (`CapitalGainsTaxService`)
Annual aggregation (not per-transaction):
1. Sum all `RealizedGainEur` from `SaleAllocation` records for the year
2. Offset losses against gains → net gain
3. Subtract annual exemption (default **€10,000**, configurable)
4. Apply **10%** on the positive remainder → `TaxDueEur`

---

## API reference

All endpoints prefixed `/api`. All except login require `Authorization: Bearer <token>`.

| Method | Path | Description |
|--------|------|-------------|
| POST | `/auth/login` | Returns JWT. Body: `{ email, password }` |
| GET | `/portfolios` | List user's portfolios |
| POST | `/portfolios` | Create portfolio. Body: `{ name, baseCurrency? }` |
| PUT | `/portfolios/{id}` | Rename portfolio |
| GET | `/instruments` | List all instruments |
| POST | `/instruments` | Create or return existing by ISIN. Body: `{ isin, name, type, currency, ticker? }` |
| GET | `/portfolios/{id}/transactions` | List transactions for portfolio |
| POST | `/portfolios/{id}/transactions` | Create buy or sell. Engine computes costs; FIFO runs for sells |
| PUT | `/portfolios/{id}/transactions/{txId}` | Edit custody fee only |
| POST | `/transactions/preview` | Compute costs without saving. Body: `{ instrumentId, broker, side, unitPrice, quantity, fxRate, manualBrokerFee? }` |
| GET | `/portfolios/{id}/holdings` | Open positions with cached market price/value/P&L (null until first refresh) |
| POST | `/portfolios/{id}/holdings/refresh-prices` | Fetch live quotes from Yahoo Finance for all held instruments; returns enriched holdings |
| GET | `/portfolios/{id}/realized?year=YYYY` | Realized gains + annual tax report |

---

## Configuration

`backend/src/InvestHorizon.Api/appsettings.json` — defaults for local dev. Override via environment variables in Docker or CI.

| Key | Env var | Purpose |
|-----|---------|---------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Postgres connection string |
| `Jwt:Key` | `Jwt__Key` | HS256 signing secret (≥ 32 chars) |
| `Jwt:Issuer` | `Jwt__Issuer` | JWT issuer claim |
| `Jwt:ExpiryMinutes` | `Jwt__ExpiryMinutes` | Token lifetime (default 480 = 8 h) |
| `Seed:UserEmail` | `Seed__UserEmail` | Initial user email |
| `Seed:UserPassword` | `Seed__UserPassword` | Initial user password |
| `Cors:Origins` | — | Allowed CORS origins array |

**Never commit a real `Jwt:Key` to source control.** The value in `appsettings.json` is a placeholder for local dev only.

---

## Design decisions & constraints

- **No registration endpoint** — user accounts are seeded from config. Add registration only when explicitly requested.
- **Computed fields persisted** — `AmountEur`, `BrokerFee`, `TobAmount`, etc. are computed at save time and stored. This preserves the cost snapshot even if fee rules change later. Re-derivable from the raw fields.
- **EUR as reporting currency** — all P/L and tax figures in EUR. Native currency + FxRate stored per transaction.
- **Manual broker fee override** — `ManualBrokerFee` on a transaction overrides the computed tier (needed for historical data with different fee schedules).
- **TOB is symmetric** — same rate and cap on buy and sell. The Excel prototype incorrectly hardcoded 0.35% on all sell-side TOB; this implementation uses correct Belgian rates.
- **Annual cap-gains tax** — the 10% Belgian tax is computed annually with a ~€10,000 exemption and loss offsetting, not per-transaction. The per-sale `RealizedGainEur` on `SaleAllocation` is the pre-tax figure.
- **Live pricing** — `IPriceProvider` / `IFxRateProvider` abstractions in Application; Yahoo Finance (unofficial, no key) is the only implementation. `InstrumentPrice` table caches one row per instrument (upserted on refresh, not immutable history). FX via Frankfurter/ECB API (keyless). `Instrument.PriceSymbol` caches the resolved Yahoo symbol after first ISIN lookup so repeated refreshes skip the search step.
- **Multi-user data model** — all portfolios are scoped by `UserId`. JWT `sub` claim holds the ASP.NET Identity user ID.

---

## Session discipline

**IMPORTANT: Every session must end by running the full test suite and fixing any failures before closing.**

```bash
# Backend (must all be green)
cd backend && dotnet test

# Frontend type check (must be error-free)
cd frontend && npx tsc --noEmit
```

If a test fails due to a change made in the session, either fix the code or update the test — never leave a red suite.
