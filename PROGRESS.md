# Progress

## Done
- **Phase 0 — Plan:** `PLAN.md`, `ASSUMPTIONS.md`; CoFID 2021 converted to `data/cofid/cofid-2021.csv` (2,887 foods) via `tools/cofid/convert.py`.
- **Phase 1 — Skeleton:** .NET 10 API (minimal APIs, EF Core + Npgsql, migrations on startup with retry, `pg_trgm`), `/healthz` with DB check, SPA served from `wwwroot` with fallback (unknown `/api/*` → 404), ForwardedHeaders. React + TS + Vite app. xUnit v3 + Testcontainers and Vitest + RTL projects running. Multi-stage Dockerfile, compose with healthchecks on both services, `.env.example`.

- **Phase 2 — Profiles:** profile CRUD API (rename, delete with typed-name confirmation), `X-Profile-Id` endpoint filter validating the profile on every scoped route; Netflix-style picker with add/manage (emoji + colour), choice remembered in localStorage, header switcher, bottom tab bar shell.

- **Phase 3 — Food data:** Food/FoodServing/Restaurant model with nullable UK nutrient set (EF complex type), generated `tsvector` + trigram indexes; CoFID seeded on first start (`import-cofid` command to re-run); OFF client (API v3 product, Search-a-licious search) with User-Agent, token-bucket rate limits, exponential back-off, Postgres product cache with background refresh after TTL, negative barcode cache, search-result cache, UK-first ranking and restaurant-brand queries; unified local search (FTS prefix + trigram + source boost, barcode de-dup); barcode endpoint (custom foods → OFF cache → OFF live) with EAN/UPC normalisation.

- **Phase 4 — Diary:** DiaryEntry with nutrition snapshot (per-basis values + quantity; edits never consult the live food); log by g/ml, serving × n or portions; move/edit/delete; copy meal from yesterday or a chosen date; day view with per-meal subtotals and totals flagged when they include unknowns; recent/frequent quick picks with last-used amount. Frontend: diary day view with date navigation (DD/MM/YYYY, "Today" in the server timezone), add-food page with debounced local + online search merged and de-duplicated by barcode, source badges, OFF attribution, log sheet with live nutrition preview, entry edit sheet, barcode scanner (BarcodeDetector → ZXing fallback, rear camera, torch, manual entry, HTTPS explanation, stream stopped on read/close), "not found → Add this product" prefill link.

## Next
- Phase 5 — Custom foods and restaurants (manual entry, sharing, copy-to-mine, CSV/JSON import).

## Known issues
- None yet.
