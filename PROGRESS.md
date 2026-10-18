# Progress

## Done
- **Phase 0 — Plan:** `PLAN.md`, `ASSUMPTIONS.md`; CoFID 2021 converted to `data/cofid/cofid-2021.csv` (2,887 foods) via `tools/cofid/convert.py`.
- **Phase 1 — Skeleton:** .NET 10 API (minimal APIs, EF Core + Npgsql, migrations on startup with retry, `pg_trgm`), `/healthz` with DB check, SPA served from `wwwroot` with fallback (unknown `/api/*` → 404), ForwardedHeaders. React + TS + Vite app. xUnit v3 + Testcontainers and Vitest + RTL projects running. Multi-stage Dockerfile, compose with healthchecks on both services, `.env.example`.

- **Phase 2 — Profiles:** profile CRUD API (rename, delete with typed-name confirmation), `X-Profile-Id` endpoint filter validating the profile on every scoped route; Netflix-style picker with add/manage (emoji + colour), choice remembered in localStorage, header switcher, bottom tab bar shell.

- **Phase 3 — Food data:** Food/FoodServing/Restaurant model with nullable UK nutrient set (EF complex type), generated `tsvector` + trigram indexes; CoFID seeded on first start (`import-cofid` command to re-run); OFF client (API v3 product, Search-a-licious search) with User-Agent, token-bucket rate limits, exponential back-off, Postgres product cache with background refresh after TTL, negative barcode cache, search-result cache, UK-first ranking and restaurant-brand queries; unified local search (FTS prefix + trigram + source boost, barcode de-dup); barcode endpoint (custom foods → OFF cache → OFF live) with EAN/UPC normalisation.

- **Phase 4 — Diary:** DiaryEntry with nutrition snapshot (per-basis values + quantity; edits never consult the live food); log by g/ml, serving × n or portions; move/edit/delete; copy meal from yesterday or a chosen date; day view with per-meal subtotals and totals flagged when they include unknowns; recent/frequent quick picks with last-used amount. Frontend: diary day view with date navigation (DD/MM/YYYY, "Today" in the server timezone), add-food page with debounced local + online search merged and de-duplicated by barcode, source badges, OFF attribution, log sheet with live nutrition preview, entry edit sheet, barcode scanner (BarcodeDetector → ZXing fallback, rear camera, torch, manual entry, HTTPS explanation, stream stopped on read/close), "not found → Add this product" prefill link.

- **Phase 5 — Custom foods and restaurants:** create/edit/soft-delete custom foods (per 100 g, per 100 ml or portion-only; servings; optional brand, restaurant, barcode), private by default with "Share with everyone"; only the creator can edit/unshare/delete (403 otherwise); "Copy to my foods" for any visible food; restaurants list and per-restaurant items; restaurant names are part of the search vector. CSV/JSON bulk import with stateless preview (per-row validation, duplicate detection by restaurant + name or barcode, within-file repeats), commit of valid rows, private/shared chosen at import; schema in `docs/IMPORT.md`, examples in `examples/`. Frontend: Foods page (mine/shared/restaurants), food detail, food form (blank = unknown, sodium → salt), restaurant page, import page with preview table. Custom foods with a barcode win barcode lookups.

- **Phase 6 — Recipes and saved meals:** recipes (private) built from any food source with quantities, servings and optional cooked weight; nutrition computed live (total, per serving, per 100 g cooked) and snapshotted when logged (by servings, or by grams when a cooked weight is set). Saved meals of foods and/or recipes, created from a diary meal ("Save as meal…") or via the API, logged in one tap with fresh snapshots. Add-food page gains Recipes and Saved meals tabs; recipe editor with ingredient search across all sources; copy meals between days was delivered in phase 4.

- **Phase 7 — Goals:** per-profile daily targets (all optional) created with UK Reference Intakes + 30 g fibre; edit/clear/reset-to-defaults page; day view shows progress bars, kcal left/over, and colours targets (green 90–110%) differently from upper limits for sugars, saturates and salt (amber from 90%, red over). Also fixed entry ordering for copied/saved-meal entries (Postgres µs timestamps).

## Next
- Phase 8 — History, charts and CSV export.

## Known issues
- None yet.
