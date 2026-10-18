using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Foods;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Import;

public sealed record ImportRequest(string Format, string Content, bool Shared, bool SkipDuplicates = true);

public sealed record ImportRow(int Row, FoodInput? Food, IReadOnlyList<string> Errors, string? Duplicate);

public sealed record ImportPreview(IReadOnlyList<ImportRow> Rows, int Valid, int Invalid, int Duplicates);

public sealed record ImportResult(int Created, int SkippedDuplicates, int SkippedInvalid);

/// <summary>
/// Bulk import of custom foods and restaurant menus from CSV or JSON. The schema
/// is documented in docs/IMPORT.md, with examples in examples/. Preview and
/// commit both parse the same content, so nothing is stored between the two.
/// </summary>
public sealed class FoodImport(TallyDbContext db)
{
    public const int MaxRows = 2000;

    public static readonly string[] Columns =
    [
        "name", "restaurant", "brand", "barcode", "basis", "portion_name",
        "energy_kcal", "energy_kj", "fat", "saturates", "carbohydrate", "sugars", "fibre", "protein", "salt", "sodium",
        "servings",
    ];

    public async Task<ImportPreview> PreviewAsync(Guid profileId, ImportRequest req, CancellationToken ct)
    {
        var rows = Parse(req);
        var visible = await db.Foods.AsNoTracking().VisibleTo(profileId)
            .Where(f => f.Source == FoodSource.Custom)
            .Select(f => new { f.Name, Restaurant = f.Restaurant != null ? f.Restaurant.Name : null, f.Barcode })
            .ToListAsync(ct);

        var keys = visible.Select(f => Key(f.Restaurant, f.Name)).ToHashSet();
        var barcodes = visible.Where(f => f.Barcode != null).Select(f => f.Barcode!).ToHashSet();
        var seenKeys = new HashSet<string>();
        var seenBarcodes = new HashSet<string>();

        var checkedRows = rows.Select(r =>
        {
            if (r.Food is null || r.Errors.Count > 0) return r;
            var key = Key(r.Food.Restaurant, r.Food.Name);
            var barcode = Barcodes.Normalize(r.Food.Barcode);
            string? duplicate =
                keys.Contains(key) ? "Already exists" :
                barcode is not null && barcodes.Contains(barcode) ? "Barcode already exists" :
                !seenKeys.Add(key) ? "Repeated in this file" :
                barcode is not null && !seenBarcodes.Add(barcode) ? "Barcode repeated in this file" :
                null;
            return r with { Duplicate = duplicate };
        }).ToList();

        return new ImportPreview(
            checkedRows,
            checkedRows.Count(r => r.Errors.Count == 0 && r.Duplicate is null),
            checkedRows.Count(r => r.Errors.Count > 0),
            checkedRows.Count(r => r.Errors.Count == 0 && r.Duplicate is not null));
    }

    /// <summary>Imports every valid row (rows with errors are skipped, as shown in the preview).</summary>
    public async Task<ImportResult> CommitAsync(Guid profileId, ImportRequest req, CancellationToken ct)
    {
        var preview = await PreviewAsync(profileId, req, ct);
        var owner = await db.Profiles.FindAsync([profileId], ct);
        var created = 0;
        foreach (var row in preview.Rows)
        {
            if (row.Errors.Count > 0 || row.Food is null) continue;
            if (row.Duplicate is not null && req.SkipDuplicates) continue;
            var food = new Food { Name = "", Source = FoodSource.Custom, OwnerProfileId = profileId, CreatedByName = owner!.Name };
            await FoodInputs.ApplyAsync(db, food, row.Food! with { IsShared = req.Shared }, ct);
            db.Foods.Add(food);
            created++;
        }
        await db.SaveChangesAsync(ct);
        return new ImportResult(created, req.SkipDuplicates ? preview.Duplicates : 0, preview.Invalid);
    }

    private static string Key(string? restaurant, string name) =>
        $"{Restaurant.Normalize(restaurant ?? "")}|{Restaurant.Normalize(name)}";

    // ---- parsing ---------------------------------------------------------

    public static List<ImportRow> Parse(ImportRequest req)
    {
        var content = req.Content ?? "";
        if (content.Length > 0 && content[0] == '﻿') content = content[1..];
        try
        {
            return req.Format?.ToLowerInvariant() switch
            {
                "json" => ParseJson(content),
                "csv" => ParseCsv(content),
                _ => [new ImportRow(0, null, ["Format must be \"csv\" or \"json\"."], null)],
            };
        }
        catch (JsonException ex)
        {
            return [new ImportRow(0, null, [$"Invalid JSON: {ex.Message}"], null)];
        }
    }

    private static List<ImportRow> ParseCsv(string content)
    {
        var table = Csv.Parse(content);
        if (table.Count == 0) return [new ImportRow(0, null, ["The file is empty."], null)];

        var header = table[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        if (!header.Contains("name")) return [new ImportRow(1, null, ["The header row must include a \"name\" column."], null)];
        var unknown = header.Where(h => h.Length > 0 && !Columns.Contains(h)).ToList();
        if (unknown.Count > 0) return [new ImportRow(1, null, [$"Unknown column(s): {string.Join(", ", unknown)}."], null)];
        if (table.Count - 1 > MaxRows) return [new ImportRow(0, null, [$"Import at most {MaxRows} rows at a time."], null)];

        return table.Skip(1).Select((cells, i) =>
        {
            var values = header.Select((h, c) => (h, v: c < cells.Length ? cells[c].Trim() : ""))
                .Where(x => x.h.Length > 0)
                .ToDictionary(x => x.h, x => x.v);
            return Build(i + 2, k => values.GetValueOrDefault(k), servingsText: values.GetValueOrDefault("servings"), servingsJson: null);
        }).ToList();
    }

    private static List<ImportRow> ParseJson(string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("foods", out var foods)) root = foods;
        if (root.ValueKind != JsonValueKind.Array) return [new ImportRow(0, null, ["JSON must be an array of foods (or { \"foods\": [...] })."], null)];
        if (root.GetArrayLength() > MaxRows) return [new ImportRow(0, null, [$"Import at most {MaxRows} rows at a time."], null)];

        return root.EnumerateArray().Select((item, i) =>
        {
            if (item.ValueKind != JsonValueKind.Object) return new ImportRow(i + 1, null, ["Each item must be an object."], null);
            var props = item.EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);
            var unknown = props.Keys.Where(k => !Columns.Contains(k)).ToList();
            if (unknown.Count > 0) return new ImportRow(i + 1, null, [$"Unknown field(s): {string.Join(", ", unknown)}."], null);

            string? Get(string k) => props.TryGetValue(k, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.GetRawText(),
                    JsonValueKind.Null => null,
                    _ => v.GetRawText(),
                }
                : null;
            props.TryGetValue("servings", out var servings);
            return Build(i + 1, Get,
                servingsText: servings.ValueKind == JsonValueKind.String ? servings.GetString() : null,
                servingsJson: servings.ValueKind == JsonValueKind.Array ? servings : null);
        }).ToList();
    }

    private static ImportRow Build(int row, Func<string, string?> get, string? servingsText, JsonElement? servingsJson)
    {
        var errors = new List<string>();

        double? Num(string key)
        {
            var raw = get(key)?.Trim();
            if (string.IsNullOrEmpty(raw)) return null;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            errors.Add($"\"{key}\" must be a number (got \"{raw}\").");
            return null;
        }

        var basisRaw = get("basis")?.Trim().ToLowerInvariant();
        NutritionBasis? basis = basisRaw switch
        {
            null or "" or "per_100g" or "100g" => NutritionBasis.Per100g,
            "per_100ml" or "100ml" => NutritionBasis.Per100ml,
            "per_portion" or "portion" => NutritionBasis.PerPortion,
            _ => null,
        };
        if (basis is null) errors.Add($"\"basis\" must be per_100g, per_100ml or per_portion (got \"{basisRaw}\").");

        var salt = Num("salt");
        var sodium = Num("sodium");
        var nutrients = new Nutrients
        {
            EnergyKcal = Num("energy_kcal"),
            EnergyKj = Num("energy_kj"),
            Fat = Num("fat"),
            Saturates = Num("saturates"),
            Carbohydrate = Num("carbohydrate"),
            Sugars = Num("sugars"),
            Fibre = Num("fibre"),
            Protein = Num("protein"),
            Salt = salt ?? Nutrients.SaltFromSodium(sodium),
        }.WithDerivedEnergy();
        if (nutrients.EnergyKcal is null) errors.Add("Give energy_kcal or energy_kj.");

        var servings = ParseServings(servingsText, servingsJson, errors);
        var input = new FoodInput(
            get("name")?.Trim() ?? "",
            get("brand"),
            get("restaurant"),
            get("barcode"),
            basis ?? NutritionBasis.Per100g,
            get("portion_name"),
            nutrients,
            servings,
            IsShared: false);
        errors.AddRange(FoodInputs.Validate(input));
        return new ImportRow(row, input, errors, null);
    }

    /// <summary>CSV: "1 slice=36; 2 slices=72". JSON: [{ "name": "1 slice", "amount": 36 }].</summary>
    private static List<ServingInput> ParseServings(string? text, JsonElement? json, List<string> errors)
    {
        var result = new List<ServingInput>();
        if (json is { } array)
        {
            foreach (var s in array.EnumerateArray())
            {
                var name = s.TryGetProperty("name", out var n) ? n.GetString() : null;
                var amount = s.TryGetProperty("amount", out var a) && a.TryGetDouble(out var d) ? d : (double?)null;
                if (name is null || amount is null) errors.Add("Each serving needs \"name\" and \"amount\".");
                else result.Add(new ServingInput(name, amount.Value));
            }
            return result;
        }

        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.LastIndexOf('=');
            if (eq <= 0 || !double.TryParse(part[(eq + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            {
                errors.Add($"Serving \"{part}\" should look like \"1 slice=36\".");
                continue;
            }
            result.Add(new ServingInput(part[..eq].Trim(), amount));
        }
        return result;
    }
}

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/import");

        g.MapPost("/preview", async (ImportRequest req, FoodImport import, ProfileContext profile, CancellationToken ct) =>
            await import.PreviewAsync(profile.Id, req, ct));

        g.MapPost("/commit", async (ImportRequest req, FoodImport import, ProfileContext profile, CancellationToken ct) =>
            await import.CommitAsync(profile.Id, req, ct));
    }
}
