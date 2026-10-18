# Tally

A self-hosted food diary and nutrition tracker for the UK — think MyFitnessPal's diary, on your own server, for your household. Mobile-first, installable as an app (PWA), with barcode scanning.

- **UK nutrition label set:** energy (kcal and kJ), fat, saturates, carbohydrate, sugars, fibre, protein, salt. Unknown values show as “–”, never as 0.
- **Food data from free sources:** your own and shared foods, UK generic foods from **CoFID** (McCance and Widdowson's), and **Open Food Facts** for packaged products (UK products ranked first).
- **Profiles, not accounts:** a Netflix-style picker. There are **no passwords by design** — run it only for people you trust, on a private network or behind your proxy.
- Recipes, saved meals, copy meals between days, goals based on UK Reference Intakes, weekly/monthly history and CSV export.

Stack: ASP.NET Core (.NET 10) + EF Core, PostgreSQL 18, React + TypeScript + Vite. One app container and one database container.

---

## First run

Requirements: Docker with the Compose plugin.

```bash
git clone <this repo> tally && cd tally
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD and OFF_CONTACT_EMAIL at least
docker compose up -d --build
```

Then open `http://<server>:8080`. On first start Tally applies its database migrations and loads ~2,900 CoFID generic foods (a few seconds). Check status with:

```bash
docker compose ps
```

Both services should show `healthy`. Logs: `docker compose logs -f app`.

### Configuration (`.env`)

| Variable | Default | Purpose |
|---|---|---|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | `tally` / — / `tally` | Database credentials (password required). |
| `APP_PORT` | `8080` | HTTP port the app listens on (inside and outside the container). |
| `TALLY_TIMEZONE` | `Europe/London` | Decides which calendar day “today” is. Diary days are stored as local dates, so entries don't jump days at midnight or when the clocks change. |
| `OFF_CONTACT_EMAIL` | — | Sent to Open Food Facts in the User-Agent (`Tally/<version> (<email>)`), as they ask every app to do. |
| `OFF_ENABLED` | `true` | `false` turns off all online lookups (local and CoFID foods only). |
| `OFF_PRODUCT_CACHE_DAYS` | `30` | Cached products are refreshed in the background after this many days. |
| `OFF_NOT_FOUND_CACHE_HOURS` | `24` | How long a “barcode not found” answer is remembered. |
| `OFF_SEARCH_CACHE_HOURS` | `24` | How long online search results are remembered. |

## Putting it behind Nginx Proxy Manager

Tally speaks plain HTTP; Nginx Proxy Manager (NPM) handles TLS.

1. In NPM, **Hosts → Proxy Hosts → Add Proxy Host**.
2. **Domain name:** e.g. `tally.example.com`.
3. **Scheme:** `http`. **Forward hostname / IP:** the Docker host's LAN IP (or the container name `tally-app-1` if NPM shares a Docker network with Tally). **Forward port:** your `APP_PORT` (8080).
4. Tick **Block common exploits** and **Websockets support** (harmless, not required).
5. **SSL tab:** request a Let's Encrypt certificate (or pick an existing one), tick **Force SSL** and **HTTP/2 support**.

Tally reads `X-Forwarded-For`/`X-Forwarded-Proto` from the proxy and does not add its own HTTPS redirect or HSTS — NPM is responsible for that. Because the forwarded headers are trusted from any address, **don't expose port 8080 directly to the internet**; only NPM should reach it (firewall the port or bind it to the LAN).

Optional hardening: NPM's **Access List** feature can add HTTP basic auth or IP allow-listing in front of Tally if you want a gate before the profile picker.

### Why HTTPS is required for barcode scanning

Browsers only allow camera access on a **secure context**: HTTPS, or `http://localhost` on the device itself. Opening Tally at `http://192.168.x.x:8080` on a phone works for everything except the camera — the scanner will explain that and offer manual barcode entry instead. Use your NPM `https://` address on phones. Installing Tally as an app (“Add to Home Screen”) also needs HTTPS.

Scanning uses the browser's built-in `BarcodeDetector` where available (Chrome on Android) and falls back to a JavaScript decoder (ZXing) on iPhone/Safari. EAN-13, EAN-8, UPC-A and UPC-E are supported.

## Installing on a phone

- **Android (Chrome):** open the HTTPS address, then menu → **Install app** / **Add to Home screen**.
- **iPhone (Safari):** Share → **Add to Home Screen**.

The app shell is cached so it opens instantly, but diary data always comes from your server — there's no offline logging. If the phone is offline, Tally says so.

## Backups

All data lives in the `tally-db` Docker volume (Postgres). Back it up with `pg_dump`, which is consistent while Tally is running:

```bash
docker compose exec -T db pg_dump -U tally -d tally -Fc > tally-$(date +%F).dump
```

(Replace `tally` with your `POSTGRES_USER`/`POSTGRES_DB` if you changed them.) Schedule that with cron and copy the files somewhere off the server.

### Restore

```bash
docker compose stop app
docker compose exec -T db pg_restore -U tally -d tally --clean --if-exists < tally-2026-10-18.dump
docker compose start app
```

To restore onto a fresh machine: `docker compose up -d db`, wait for it to be healthy, run the `pg_restore` line, then `docker compose up -d`.

Alternatively, stop everything and archive the raw volume (must be stopped for consistency):

```bash
docker compose down
docker run --rm -v tally_tally-db:/data -v "$PWD":/backup alpine tar czf /backup/tally-db.tgz -C /data .
docker compose up -d
```

## Updating

```bash
git pull
docker compose up -d --build
```

Database migrations run automatically on start. Take a backup first. Installed phone apps pick up the new version the next time they're opened (the service worker updates itself).

To refresh CoFID data after the converted file changes:

```bash
docker compose exec app dotnet Tally.Api.dll import-cofid
```

## Data sources and licences

- **CoFID** — *McCance and Widdowson's Composition of Foods Integrated Dataset 2021*, Office for Health Improvement and Disparities, © Crown copyright, [Open Government Licence v3.0](https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/). Converted from the published spreadsheet by `tools/cofid/convert.py` into `data/cofid/cofid-2021.csv` (see that script for the conventions: trace → 0, not measured → unknown, salt from sodium, AOAC fibre with NSP fallback).
- **Open Food Facts** — product data © Open Food Facts contributors, [Open Database Licence](https://opendatacommons.org/licenses/odbl/1-0/). Tally uses the product API (v3) and Search-a-licious, respects the published rate limits (15 product reads and 10 searches per minute), and caches results in your database. Attribution is shown wherever OFF data appears.

## Custom foods, restaurants and imports

Anyone can add foods (per 100 g, per 100 ml, or per portion for restaurant dishes), optionally under a restaurant such as “Nando's”. Foods are private unless **Share with everyone** is ticked; only the creator can edit or unshare a shared food, and others can **Copy to my foods** to make their own editable version. Restaurant menus and lists of foods can be imported from CSV or JSON with a validated preview — see [docs/IMPORT.md](docs/IMPORT.md) and the files in [`examples/`](examples/).

Diary entries keep the nutrition values from the moment they were logged, so editing a food (or Open Food Facts updating a product) never changes your history.

## Development

Prerequisites: .NET 10 SDK, Node 24, Docker (for Postgres and the tests).

```bash
# database
docker run -d --name tally-dev-db -e POSTGRES_USER=tally -e POSTGRES_PASSWORD=tally -e POSTGRES_DB=tally -p 5432:5432 postgres:18-alpine

# API on :5080 (matches the Vite proxy)
ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/Tally.Api

# web app on :5173
cd web && npm install && npm run dev
```

Tests:

```bash
dotnet test                 # backend: xUnit v3 + Testcontainers (starts its own Postgres)
cd web && npm test          # frontend: Vitest + React Testing Library
cd web && npm run typecheck
```

New migration: `dotnet tool restore && dotnet ef migrations add <Name> -p src/Tally.Api -o Data/Migrations`.

Project docs: [PLAN.md](PLAN.md) (architecture, data model, API), [ASSUMPTIONS.md](ASSUMPTIONS.md) (decisions), [PROGRESS.md](PROGRESS.md) (status and known limitations).

## Troubleshooting

- **App container unhealthy:** `docker compose logs app`. Most often the database password in `.env` changed after the volume was created — Postgres keeps the original password.
- **“Online results unavailable”:** Open Food Facts is slow, down, or Tally is backing off after errors/rate limits. Local, CoFID and previously cached products keep working; try again in a minute.
- **Scanner says HTTPS is needed:** open Tally through your NPM `https://` address.
- **Wrong “today”:** set `TALLY_TIMEZONE` and restart.
