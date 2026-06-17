# Local development workflow

How to run InvestHorizon locally so that code changes show up automatically.

## TL;DR

```bash
# 1. Start the backend (Postgres + API). The API runs in auto-reload mode.
docker compose up db api -d

# 2. Start the frontend dev server (hot reload).
cd frontend && npm run dev
```

Then open **http://localhost:5173**. Leave both running while you work. Stop everything with `docker compose down`.

---

## How Compose picks its files (the key thing to understand)

Two compose files are **always merged automatically** — you don't pass `-f` for either:

| File | Role |
|------|------|
| `docker-compose.yml` | Base / production-like setup. The `api` is a **frozen compiled image**. |
| `docker-compose.override.yml` | Dev patch layered on top. Replaces `api` with `dotnet watch` + a live mount of `./backend`. |

Naming services on the command line (`docker compose up db api`) only chooses **which services start** — it does **not** change which files are used. The override is applied either way.

Verify the effective config any time with:

```bash
docker compose config        # shows the merged result; api command should be `dotnet watch`
```

So `docker compose up db api -d` gives you Postgres + the **watch-mode** API. (The `web` container on port 8080 is the production-like full build — ignore it during development.)

---

## When do my changes appear?

| Layer | What you run | Reloads on edit? |
|-------|--------------|------------------|
| Frontend | `npm run dev` (Vite, :5173) | **Instantly** (HMR). Proxies `/api` → API on :5000. |
| Backend (most edits) | watch-mode `api` container | **Automatically** in a few seconds (`dotnet watch` hot reload). |
| Backend (structural edits) | watch-mode `api` container | **Needs a restart** — see below. |

`dotnet watch` hot-reloads small edits in place, but some changes are too structural for hot reload to apply live, e.g.:

- new or changed method/interface signatures
- new enum values
- new classes + new DI registrations
- config / `Program.cs` startup changes

If you make one of those and don't see it reflected, kick the API:

```bash
docker compose restart api
```

Use `--build` when you change the `Dockerfile`, NuGet packages, or `docker-compose.override.yml`:

```bash
docker compose up -d --build api
```

---

## Quick sanity check against the running API

Confirm a backend change took effect without clicking through the UI. Example — the broker-fee preview:

```bash
# get a token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@investhorizon.local","password":"Admin1234!"}' \
  | grep -oE '"token":"[^"]+"' | sed 's/"token":"//;s/"//')

# pick the first instrument id, then preview a Keytrade €50 buy
INSTR=$(curl -s http://localhost:5000/api/instruments -H "Authorization: Bearer $TOKEN" \
  | grep -oE '"id":"[^"]+"' | head -1 | sed 's/"id":"//;s/"//')

curl -s -X POST http://localhost:5000/api/transactions/preview \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"instrumentId\":\"$INSTR\",\"broker\":\"Keytrade\",\"side\":\"Buy\",\"unitPrice\":50,\"quantity\":1,\"fxRate\":1}"
# -> brokerFee 2.45 for a €50 order
```

---

## Other useful commands

```bash
docker compose ps             # what's running
docker compose logs -f api    # follow API logs (migrations, seeder, watch rebuilds)
docker compose restart api    # restart API after a structural change
docker compose down           # stop everything
```

Backend tests and frontend type check (run before committing):

```bash
cd backend && dotnet test
cd frontend && npx tsc --noEmit
```

> For **production** deployment (server, Docker Hub, releases), see [DEPLOY.md](DEPLOY.md).
