# Tally — plan

Self-hosted UK nutrition tracker. One Docker image (`app`: ASP.NET Core API + built React SPA) and one Postgres container (`db`), behind Nginx Proxy Manager.

## Architecture

```
phone ──HTTPS──▶ Nginx Proxy Manager ──HTTP:8080──▶ app (ASP.NET Core, .NET 10)
                                                    ├─ /api/*        minimal-API endpoints
                                                    ├─ /healthz      liveness + DB check
                                                    └─ /*            static SPA (wwwroot) + index.html fallback
                                                         │
                                                         ├──▶ db (PostgreSQL 18, pg_trgm, FTS)
                                                         └──▶ Open Food Facts (rate limited, cached in db)
```

Repository layout:

```
src/Tally.Api/            ASP.NET Core API (feature folders)
  Data/                   DbContext, entities, migrations
  Features/<Feature>/     endpoints + services per feature
  OpenFoodFacts/          OFF HTTP client, mapper, rate limiter, background refresher
tests/Tally.Api.Tests/    xUnit v3 + Testcontainers (real Postgres) + WebApplicationFactory
web/                      React + TypeScript + Vite PWA (Vitest + RTL)
data/cofid/               converted CoFID 2021 CSV (OGL v3), seeded on startup
tools/cofid/              stdlib-only Python converter xlsx → csv
examples/                 bulk import examples (CSV + JSON)
```

Backend style: minimal APIs grouped per feature (`MapGroup`), EF Core code-first, migrations applied at startup, ProblemDetails for errors. Every profile-scoped endpoint group has an endpoint filter that reads `X-Profile-Id`, checks the profile exists (400 if missing, 404 if unknown) and exposes it via `ProfileContext`.

Frontend: React Router, a small typed `fetch` wrapper, plain CSS with custom properties (no UI kit), Recharts for charts, `@zxing/browser` fallback for barcode scanning, `vite-plugin-pwa` for the service worker.

## Data model

```
Profile ─┬─< DiaryEntry >─┐ (FoodId / RecipeId nullable, SET NULL; nutrition snapshotted)
         ├─< Food (OwnerProfileId, private/shared custom foods)
         ├─< Recipe ─< RecipeIngredient >── Food
         ├─< SavedMeal ─< SavedMealItem >── Food | Recipe
         └── Goal (1:1)

Restaurant ─< Food (RestaurantId)
Food ─< FoodServing
OffBarcodeMiss (negative cache)     OffSearchCache (query → barcodes)
```

- **Profile**: Id, Name, Avatar (emoji), Colour, CreatedAt.
- **Food**: Id, Source (`Custom|Cofid|OpenFoodFacts`), OwnerProfileId?, IsShared, Name, Brand?, RestaurantId?, Barcode?, ExternalId? (CoFID code / OFF code), Basis (`Per100g|Per100ml|PerPortion`), PortionName? (e.g. "1 burger"), `Nutrients` (complex type, all nullable doubles: EnergyKcal, EnergyKj, Fat, Saturates, Carbohydrate, Sugars, Fibre, Protein, Salt), CarbsMayIncludeFibre, Countries[], IsUk, ImageUrl?, FetchedAt?, CopiedFromFoodId?, CreatedAt, UpdatedAt, DeletedAt? (soft delete), SearchVector (generated tsvector over Name + Brand).
  Indexes: GIN(SearchVector), GIN trigram(Name), unique(Source, ExternalId), Barcode.
- **FoodServing**: Id, FoodId, Name ("1 slice"), Amount (in basis units: g, ml or portions), SortOrder.
- **Restaurant**: Id, Name, NormalizedName (unique).
- **DiaryEntry**: Id, ProfileId, Date (`date`, local calendar day), Meal (`Breakfast|Lunch|Dinner|Snacks`), FoodId?, RecipeId?, snapshot: Name, Brand, SourceLabel, Basis, `Nutrients` per basis; quantity: Amount (basis units), Unit (`g|ml|portion|serving`), ServingName?, ServingAmount?, Quantity; CreatedAt. Totals = per-basis × Amount ÷ 100 (or × Amount for portion bases). Editing quantity re-uses the snapshot, never the live food.
- **Recipe**: Id, ProfileId, Name, Servings, CookedWeightGrams?; **RecipeIngredient**: FoodId, Amount (basis units), ServingName?, Quantity.
- **SavedMeal**: Id, ProfileId, Name; **SavedMealItem**: FoodId? | RecipeId?, Amount, Unit, ServingName?, ServingAmount?, Quantity.
- **Goal**: ProfileId (PK), nullable targets for kcal, fat, saturates, carbohydrate, sugars, fibre, protein, salt.
- **OffBarcodeMiss**: Barcode (PK), CheckedAt. **OffSearchCache**: QueryKey (PK), Barcodes[], FetchedAt.

## API outline

All `/api/*` except `/api/profiles` (list/create) and `/api/config` require `X-Profile-Id`.

| Method | Path | Purpose |
|---|---|---|
| GET | /healthz | health (DB reachable) |
| GET | /api/config | timezone, version, OFF availability |
| GET/POST | /api/profiles | list / create |
| PUT/DELETE | /api/profiles/{id} | rename / delete (body confirms name) |
| GET | /api/foods/search?q= | local: mine, shared, restaurants, CoFID, cached OFF |
| GET | /api/foods/search/online?q= | OFF (cached, rate-limited) |
| GET | /api/foods/recent, /api/foods/frequent | quick picks |
| GET | /api/foods/barcode/{code} | local → OFF cache → OFF live, negative-cached |
| GET/POST/PUT/DELETE | /api/foods, /api/foods/{id} | custom foods (owner-only edits) |
| POST | /api/foods/{id}/copy | copy to my foods |
| GET | /api/restaurants?q= , /api/restaurants/{id}/foods | brands |
| POST | /api/import/preview, /api/import/commit | CSV/JSON bulk import |
| GET | /api/diary/{date} | day: meals, entries, subtotals, totals, goals |
| POST | /api/diary/entries | log food or recipe |
| PUT/DELETE | /api/diary/entries/{id} | change quantity/meal, delete |
| POST | /api/diary/copy | copy a meal (or whole day) from a date to a date |
| GET/POST/PUT/DELETE | /api/recipes… , POST /api/recipes/{id}/log | recipes |
| GET/POST/PUT/DELETE | /api/saved-meals… , POST /api/saved-meals/{id}/log, POST /api/saved-meals/from-diary | saved meals |
| GET/PUT | /api/goals | targets |
| GET | /api/history?from=&to= | per-day totals, averages, macro split, days on target |
| GET | /api/export/diary.csv?from=&to= | CSV export |

## Search ranking

Local search (one SQL query): match on `SearchVector @@ to_tsquery('english', 'tok1:* & tok2:*')` OR `word_similarity(q, Name) > 0.4`. Score = `ts_rank + similarity + source boost` where boosts are Mine 3 > Shared/Restaurant 2.5 > CoFID 2 > cached OFF 1 (+0.5 if UK). Online search goes to OFF Search-a-licious: first query filtered to `countries_tags:"en:united-kingdom"`, then an unfiltered top-up if results are thin; when the query matches a known restaurant, a `brands_tags:"<slug>"` query is used. Client merges local + online, de-duplicates by barcode, and keeps local first.

## Phases

0. Plan (this file, ASSUMPTIONS.md)
1. Skeleton: layout, Dockerfile, compose, Postgres, migrations on startup, /healthz, SPA serving, test projects
2. Profiles: picker, CRUD, profile scoping filter
3. Food data: model, CoFID import, OFF client + cache + UA + rate limiting, unified search, barcode lookup
4. Diary: logging, editing, totals, date navigation, scanner UI, recent/frequent
5. Custom foods & restaurants: manual entry, sharing, copy-to-mine, CSV/JSON import with preview
6. Recipes & saved meals; copy meals between days
7. Goals with UK RI defaults; progress colouring
8. History charts, averages, CSV export
9. PWA, icons, mobile polish, empty/error states, README

Each phase: implement → tests → `docker compose up --build` + healthcheck → PROGRESS.md → commit.
