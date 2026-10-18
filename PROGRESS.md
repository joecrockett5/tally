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

- **Phase 8 — History and export:** `/api/history` returns every day in a range with totals, average per nutrient over logged days only, macro energy split (UK factors), and days on target (energy ±10% and no limit exceeded); CSV export (one row per entry: date, meal, food, brand, source, quantity, unit, serving, amount and all nutrients; blanks for unknowns). History page with week (Mon–Sun) / month navigation, summary tiles, daily energy bar chart against the target (Recharts, tooltip), averages vs goals, macro split bar + table, day-by-day table, and CSV download for any date range.

- **Phase 9 — PWA and polish:** web manifest, generated icons (`tools/icons/make_icons.py`, incl. maskable and Apple touch icon), Workbox service worker caching the app shell only, cache headers for shell vs hashed assets, offline banner, top-level error boundary, empty states throughout, mobile layout pass, full README (first run, NPM setup, HTTPS/camera, backup/restore, updating, licences, development).

## Status

All phases complete. **73 backend tests** (xUnit v3, real Postgres via Testcontainers) and **38 frontend tests** (Vitest + RTL) pass; `docker compose up -d --build` from a clean copy with `.env.example` starts healthy with CoFID loaded; the README backup/restore commands were exercised against that fresh stack.

Definition-of-done checks:
- Fresh `cp .env.example .env && docker compose up -d --build` → healthy app with 2,887 CoFID foods. ✔
- "chicken breast" → CoFID generic results first; UK product search (e.g. "heinz baked beans") → OFF results with UK items first (checked live). ✔
- Shared restaurant item visible to a second profile, copyable but not editable (403) — covered by `CustomFoodTests`. ✔
- Editing a food after logging leaves past entries unchanged — covered by `DiaryTests`, `CustomFoodTests`, `RecipeTests`. ✔
- Profile → scan → log → totals: exercised in a desktop browser at phone size with manual barcode entry and search; the live-camera path is covered by component tests only (see limitations).

## Known limitations
- **Not tested on physical phones.** Camera scanning (BarcodeDetector on Android, ZXing on iOS Safari) and PWA install need an HTTPS deployment behind NPM to try on real devices; this has not been done yet.
- The service worker was verified in headless Chromium; the Claude desktop browser pane used for UI checks refuses service worker registration, so install/update behaviour wasn't checked there.
- OFF data quality varies (e.g. some products' English name is odd, or nutrition is missing); Tally shows what OFF has, with "–" for unknowns.
- Search-a-licious has no rate limit of its own documented; Tally applies OFF's 10 searches/minute to it. Heavy simultaneous use by several people can show "online search is rate limited" for a minute; local and cached results are unaffected.
- CoFID fibre falls back to NSP where AOAC isn't published, which reads lower than UK label fibre.
- No authentication by design: anyone who can reach the URL can pick any profile. Use NPM access lists if you need a gate.
- Restaurant example data in `examples/` is illustrative, not official nutrition information.
