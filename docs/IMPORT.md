# Bulk import: custom foods and restaurant menus

Foods → **Import** accepts a CSV or JSON file. Tally shows a preview first: every row is validated, errors are listed per row, and possible duplicates are flagged. Nothing is saved until you press **Import**; rows with errors are skipped, and duplicates are skipped unless you untick "Skip duplicates". You choose at import time whether the imported items are private or shared with everyone.

Examples: [`examples/restaurant-menu.csv`](../examples/restaurant-menu.csv), [`examples/custom-foods.json`](../examples/custom-foods.json). The nutrition figures in the examples are illustrative only — use the values published by the restaurant or printed on the pack.

## Fields

| Field | Required | Notes |
|---|---|---|
| `name` | yes | Up to 300 characters. |
| `restaurant` | no | e.g. `Nando's`. Creates the restaurant if it doesn't exist yet. |
| `brand` | no | Manufacturer brand for packaged foods. |
| `barcode` | no | EAN-13, EAN-8 or UPC-A digits. |
| `basis` | no | `per_100g` (default), `per_100ml` or `per_portion`. |
| `portion_name` | no | For `per_portion` foods, e.g. `1 burger`. Defaults to `1 portion`. |
| `energy_kcal`, `energy_kj` | one of them | The other is derived (1 kcal = 4.184 kJ). |
| `fat`, `saturates`, `carbohydrate`, `sugars`, `fibre`, `protein` | no | Grams per basis. Leave blank if unknown — blank is stored as unknown, not zero. Carbohydrate should exclude fibre (UK labelling). |
| `salt` | no | Grams of **salt**. |
| `sodium` | no | Grams of sodium; used only when `salt` is blank (salt = sodium × 2.5). |
| `servings` | no | Named servings in basis units. CSV: `1 slice=36; 2 slices=72`. JSON: `[{"name": "1 slice", "amount": 36}]`. For `per_portion` foods the amount is a number of portions (e.g. `large=1.9`). |

Numbers use a dot as the decimal separator. CSV files must have a header row; column order doesn't matter and unknown columns are rejected (so typos don't silently lose data). JSON is an array of objects (or `{ "foods": [...] }`) with the same field names. Up to 2,000 rows per import.

## Duplicates

A row is flagged as a duplicate when a food you can see (your own or a shared one) already has the same restaurant and name (ignoring case and extra spaces), or the same barcode, or when the same restaurant + name or barcode appears earlier in the file.
