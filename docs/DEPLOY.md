# Deploying InvestHorizon to production

InvestHorizon runs on the shared `evolve11.com` Linux server alongside other Dockerized
apps. TLS and domain routing are handled automatically by the server's existing
**`nginx-proxy` + acme-companion** stack — the app simply joins the shared Docker network
and advertises its hostname via environment variables (same pattern as `thermocron`).

- **Subdomain:** `https://investhorizon.evolve11.com`
- **Registry:** Docker Hub — `thomasthiry/investhorizon-api`, `thomasthiry/investhorizon-web`
- **Images built by:** GitHub Actions (`.github/workflows/ci.yml`)

---

## Architecture

```
Internet ──HTTPS──> nginx-proxy + acme-companion ──HTTP──> web (investhorizon_web_prod, :80)
            (TLS + routing via env vars)                    │  network-shared-with-proxy
                                                            ├─> api (investhorizon_api_prod, :80)
                                                            └─> db  (investhorizon_db_prod, postgres:16)
```

Only the `web` container advertises `VIRTUAL_HOST` / `LETSENCRYPT_HOST`. Its nginx proxies
`/api/` to the `api` container, so the app is same-origin (no CORS). The `api` and `db`
containers publish no proxied hostname; `db` exposes host port `15433` only for admin access.
EF Core migrations and the user seeder run automatically on `api` startup.

---

## One-time server setup

1. **Ensure the shared proxy network exists** (it already does if other apps run):
   ```bash
   docker network inspect network-shared-with-proxy >/dev/null 2>&1 \
     || docker network create network-shared-with-proxy
   ```

2. **Create the deployment directory and copy files:**
   ```bash
   mkdir -p ~/investhorizon && cd ~/investhorizon
   # copy docker-compose.prod.yml and .env.prod.example here
   cp .env.prod.example .env
   ```

3. **Fill in `.env`** with real secrets (this file is git-ignored and lives only on the server):
   ```bash
   # strong values:
   openssl rand -base64 48   # -> investhorizon_jwt_key
   openssl rand -base64 24   # -> investhorizon_postgres_password_prod
   ```
   Set `investhorizon_image_tag` to the tag you want to run (see below), and set
   `investhorizon_seed_user_email` / `investhorizon_seed_user_password` to your admin login.

4. **DNS:** point `investhorizon.evolve11.com` at the server's IP (A record).

---

## Releasing a new version

1. Push to `master`. The `ci.yml` workflow runs the backend + frontend tests first, and **only if
   they pass** builds and pushes both images to Docker Hub, tagged with a date-based version like
   `2026-06-14.3` (plus `latest`). The tag is printed in the Actions run summary.
   *(Requires repo secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`.)*

2. On the server, set `investhorizon_image_tag` in `.env` to that tag (pin it — avoid `latest`
   in production so rollbacks are deterministic), then:
   ```bash
   cd ~/investhorizon
   docker compose -f docker-compose.prod.yml pull
   docker compose -f docker-compose.prod.yml up -d
   ```

3. **Verify:**
   ```bash
   docker compose -f docker-compose.prod.yml ps          # db healthy, api + web up
   docker compose -f docker-compose.prod.yml logs api     # migrations applied + seeder ran
   ```
   Then browse `https://investhorizon.evolve11.com` (valid Let's Encrypt cert on first run may
   take a minute), log in with the seeded credentials, and create a transaction to confirm the
   `web → api → db` round-trip.

### Rollback
Set `investhorizon_image_tag` back to the previous tag and re-run `pull` + `up -d`.

---

## Notes

- **Secrets** never live in git. Only `.env.prod.example` is tracked; the real `.env` is created
  on the server.
- **Database** persists in the named volume `investhorizon_sqldata_prod`. Back it up with
  `docker exec investhorizon_db_prod pg_dump -U <user> investhorizon_prod`.
- **Optional automation** (not set up): a GitHub Actions SSH deploy step, or Watchtower watching
  `latest`. Manual `pull` + `up -d` is the current process, matching the other apps on the server.
