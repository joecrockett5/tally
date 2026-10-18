"""Convert the CoFID 2021 workbook into the CSV that Tally seeds from.

Usage:
    python3 -I tools/cofid/convert.py <path-to-CoFID-2021.xlsx> data/cofid/cofid-2021.csv

Source: McCance and Widdowson's Composition of Foods Integrated Dataset 2021,
Public Health England / OHID, Open Government Licence v3.0.

Value conventions in CoFID: "Tr" = trace (stored as 0), "N" = present but
not measured (stored as empty / null), blank = not determined (null).
"""
import csv
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from xlsx import read_sheet  # noqa: E402

FIRST_DATA_ROW = 3


def num(raw):
    raw = (raw or "").strip()
    if raw == "" or raw == "N":
        return None
    if raw.lower() == "tr":
        return 0.0
    try:
        return float(raw)
    except ValueError:
        return None


def by_code_and_name(rows):
    """Keyed by (code, name): CoFID 2021 reuses code 13-669 for two different foods."""
    header = rows[0]
    out = {}
    for row in rows[FIRST_DATA_ROW:]:
        if not row or not row[0].strip():
            continue
        out[(row[0].strip(), row[1].strip())] = {h.strip(): (row[i] if i < len(row) else "") for i, h in enumerate(header)}
    return out


def fmt(v):
    if v is None:
        return ""
    return f"{v:.4f}".rstrip("0").rstrip(".") if v != int(v) else str(int(v))


def main(src, dest):
    prox_rows = read_sheet(src, "1.3 Proximates")
    inorg = by_code_and_name(read_sheet(src, "1.4 Inorganics"))
    seen = set()
    header = [h.strip() for h in prox_rows[0]]
    col = {h: i for i, h in enumerate(header)}

    def get(row, name):
        i = col[name]
        return row[i] if i < len(row) else ""

    os.makedirs(os.path.dirname(dest), exist_ok=True)
    count = 0
    with open(dest, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["code", "name", "description", "group", "energy_kcal", "energy_kj", "fat",
                    "saturates", "carbohydrate", "sugars", "fibre", "fibre_method", "protein", "salt"])
        for row in prox_rows[FIRST_DATA_ROW:]:
            if not row or not row[0].strip():
                continue
            code = row[0].strip()
            name = get(row, "Food Name").strip()
            inorganics = inorg.get((code, name), {})
            # Keep codes unique; a reused code gets a suffix.
            unique_code = code
            n = 2
            while unique_code in seen:
                unique_code = f"{code}-{n}"
                n += 1
            seen.add(unique_code)
            aoac = num(get(row, "AOAC fibre (g)"))
            nsp = num(get(row, "NSP (g)"))
            fibre, method = (aoac, "AOAC") if aoac is not None else ((nsp, "NSP") if nsp is not None else (None, ""))
            sodium_mg = num(inorganics.get("Sodium (mg)", ""))
            salt = None if sodium_mg is None else sodium_mg / 1000 * 2.5
            w.writerow([
                unique_code,
                name,
                get(row, "Description").strip(),
                get(row, "Group").strip(),
                fmt(num(get(row, "Energy (kcal) (kcal)"))),
                fmt(num(get(row, "Energy (kJ) (kJ)"))),
                fmt(num(get(row, "Fat (g)"))),
                fmt(num(get(row, "Satd FA /100g fd (g)"))),
                fmt(num(get(row, "Carbohydrate (g)"))),
                fmt(num(get(row, "Total sugars (g)"))),
                fmt(fibre),
                method,
                fmt(num(get(row, "Protein (g)"))),
                fmt(salt),
            ])
            count += 1
    print(f"wrote {count} foods to {dest}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
