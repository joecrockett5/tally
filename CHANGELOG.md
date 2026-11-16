# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-11-16

### Added

- **Custom meals:** each profile can rename, reorder, add and remove its meals from **Customise meals** on the diary. Changes apply from the day they're made, so earlier days keep the meals and names they had. A removed meal that still has food logged on a day stays visible on that day.
- **Built-in HTTPS:** Tally now serves HTTPS itself on port 8443, so the camera works when you open it straight from the server's address, with no reverse proxy. It creates and keeps a self-signed certificate on first start, or uses your own (`Tally__Https__CertificatePath`, `Tally__Https__KeyPath`). New settings: `HTTPS_PORT` and `TALLY_HOSTS`. Over plain HTTP, the scanner links to the HTTPS address.

### Changed

- The compose file publishes port 8443 as well as 8080 and adds a `tally-tls` volume for the certificate. **Update your `docker-compose.yml`** from the README to get HTTPS; an existing file keeps working over plain HTTP only.
- A reverse proxy is now optional. Existing proxy setups keep working unchanged on port 8080.
- API: diary entries, copies and saved-meal logging identify a meal by `mealId` instead of a `meal` name. Existing entries are converted when the database is migrated.

## [1.0.0] - 2026-11-06

First public release, published as a multi-architecture container image
(`linux/amd64`, `linux/arm64`) on `ghcr.io`.

### Added

- **Diary:** log foods and recipes to breakfast, lunch, dinner and snacks by weight, volume, serving or portion; move, edit and delete entries; copy meals between days. Entries keep the nutrition values from when they were logged, so later edits to a food never change your history.
- **UK nutrition labels:** energy (kcal and kJ), fat, saturates, carbohydrate, sugars, fibre, protein and salt. Unknown values show as "–", never as 0.
- **Food data:** about 2,900 generic foods from CoFID 2021, loaded on first start, plus Open Food Facts search and barcode lookup with UK products ranked first, local caching and rate limiting.
- **Barcode scanning:** EAN-13, EAN-8, UPC-A and UPC-E, using the browser's `BarcodeDetector` with a ZXing fallback, and manual entry when no camera is available.
- **Custom foods and restaurants:** foods per 100 g, per 100 ml or per portion, private or shared with everyone; copy any visible food to your own; validated CSV and JSON bulk import with a preview.
- **Recipes and saved meals:** nutrition worked out from ingredients, total and per serving; log saved meals in one tap.
- **Goals:** optional daily targets for each profile, starting from UK Reference Intakes, with progress on the day view.
- **History:** weekly and monthly views with averages, a daily energy chart, macro split, days on target and CSV export.
- **Profiles:** a picker for everyone in the household, with no passwords by design.
- **Installable app:** a mobile-first PWA whose app shell opens instantly; diary data always comes from your server.
- **Self-hosting:** Docker Compose setup with PostgreSQL 18, automatic database migrations on start, and a `/healthz` endpoint that reports the running version.

[Unreleased]: https://github.com/joecrockett5/tally/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/joecrockett5/tally/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/joecrockett5/tally/releases/tag/v1.0.0
