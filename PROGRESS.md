# Progress

## Done
- **Phase 0 — Plan:** `PLAN.md`, `ASSUMPTIONS.md`; CoFID 2021 converted to `data/cofid/cofid-2021.csv` (2,887 foods) via `tools/cofid/convert.py`.
- **Phase 1 — Skeleton:** .NET 10 API (minimal APIs, EF Core + Npgsql, migrations on startup with retry, `pg_trgm`), `/healthz` with DB check, SPA served from `wwwroot` with fallback (unknown `/api/*` → 404), ForwardedHeaders. React + TS + Vite app. xUnit v3 + Testcontainers and Vitest + RTL projects running. Multi-stage Dockerfile, compose with healthchecks on both services, `.env.example`.

- **Phase 2 — Profiles:** profile CRUD API (rename, delete with typed-name confirmation), `X-Profile-Id` endpoint filter validating the profile on every scoped route; Netflix-style picker with add/manage (emoji + colour), choice remembered in localStorage, header switcher, bottom tab bar shell.

## Next
- Phase 3 — Food data (CoFID import, OFF client and cache, unified search, barcode lookup).

## Known issues
- None yet.
