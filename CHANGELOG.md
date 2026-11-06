# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/joecrockett5/tally/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/joecrockett5/tally/releases/tag/v1.0.0
