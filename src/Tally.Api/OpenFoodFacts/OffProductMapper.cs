using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tally.Api.Data;

namespace Tally.Api.OpenFoodFacts;

/// <summary>Maps an Open Food Facts product (API v3 or Search-a-licious hit) onto a <see cref="Food"/>.</summary>
public static partial class OffProductMapper
{
    public const string UkTag = "en:united-kingdom";
    private static readonly HashSet<string> NorthAmerica = ["en:united-states", "en:canada"];

    public static string? Code(JsonElement p) => Str(p, "code");

    /// <summary>Applies the product's data to <paramref name="food"/> (new or cached), replacing servings.</summary>
    public static void Apply(JsonElement p, Food food, DateTimeOffset now)
    {
        var code = Code(p)!;
        food.Source = FoodSource.OpenFoodFacts;
        food.ExternalId = code;
        food.Barcode = code;
        // product_name is in the product's main language; prefer it for English-language products.
        var name = Str(p, "lang") == "en"
            ? FirstNonEmpty(Str(p, "product_name"), Str(p, "product_name_en"))
            : FirstNonEmpty(Str(p, "product_name_en"), Str(p, "product_name"));
        food.Name = Truncate(name ?? FirstNonEmpty(Str(p, "generic_name_en"), Str(p, "generic_name")) ?? $"Product {code}", 300);
        food.Brand = FirstBrand(p) is { } brand ? Truncate(brand, 200) : null;
        food.Countries = StrArray(p, "countries_tags");
        food.IsUk = food.Countries.Contains(UkTag);
        food.CarbsMayIncludeFibre = food.Countries.Length > 0 && food.Countries.All(NorthAmerica.Contains);
        food.ImageUrl = Str(p, "image_front_small_url") is { Length: <= 500 } img ? img : null;
        food.FetchedAt = now;
        food.UpdatedAt = now;

        var n = p.TryGetProperty("nutriments", out var nut) && nut.ValueKind == JsonValueKind.Object ? nut : default;
        var per100 = ReadNutrients(n, "_100g");
        var perServing = ReadNutrients(n, "_serving");
        var isLiquid = IsLiquid(p);
        var servingQty = Num(p, "serving_quantity");
        var servingLabel = CleanServingLabel(Str(p, "serving_size"));

        food.Servings.Clear();
        if (per100.All().Any(x => x.Value is not null) || perServing.All().All(x => x.Value is null))
        {
            food.Basis = isLiquid ? NutritionBasis.Per100ml : NutritionBasis.Per100g;
            food.PortionName = null;
            food.Nutrients = per100;

            var order = 0;
            if (servingQty is > 0)
                food.Servings.Add(new FoodServing { Name = servingLabel ?? "1 serving", Amount = servingQty.Value, SortOrder = order++ });
            var packQty = Num(p, "product_quantity");
            if (packQty is > 0 && packQty != servingQty && packQty < 100_000)
                food.Servings.Add(new FoodServing { Name = $"Whole pack ({FormatQty(packQty.Value)} {(isLiquid ? "ml" : "g")})", Amount = packQty.Value, SortOrder = order });
        }
        else
        {
            // Only per-serving values with no usable weight: a portion-only food.
            food.Basis = NutritionBasis.PerPortion;
            food.PortionName = Truncate(servingLabel ?? "1 serving", 100);
            food.Nutrients = perServing;
        }
    }

    private static Nutrients ReadNutrients(JsonElement n, string suffix)
    {
        if (n.ValueKind != JsonValueKind.Object) return Nutrients.Empty;
        double? Get(string key) => Num(n, key + suffix);

        var salt = Get("salt") ?? Nutrients.SaltFromSodium(Get("sodium"));
        return new Nutrients
        {
            EnergyKcal = Get("energy-kcal"),
            EnergyKj = Get("energy-kj") ?? (Get("energy-kcal") is null ? Get("energy") : null),
            Fat = Get("fat"),
            Saturates = Get("saturated-fat"),
            Carbohydrate = Get("carbohydrates"),
            Sugars = Get("sugars"),
            Fibre = Get("fiber"),
            Protein = Get("proteins"),
            Salt = salt,
        }.WithDerivedEnergy();
    }

    private static bool IsLiquid(JsonElement p)
    {
        if (Str(p, "product_quantity_unit") is { } unit) return unit.Equals("ml", StringComparison.OrdinalIgnoreCase);
        if (Str(p, "serving_quantity_unit") is { } sunit) return sunit.Equals("ml", StringComparison.OrdinalIgnoreCase);
        if (Str(p, "nutrition_data_per") is "100ml") return true;
        return Str(p, "quantity") is { } q && LiquidQuantity().IsMatch(q);
    }

    private static string? FirstBrand(JsonElement p)
    {
        if (!p.TryGetProperty("brands", out var b)) return null;
        var raw = b.ValueKind switch
        {
            JsonValueKind.String => b.GetString()?.Split(',').FirstOrDefault(),
            JsonValueKind.Array => b.EnumerateArray().Select(x => x.GetString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static string? CleanServingLabel(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > 100 ? s[..100] : s;
    }

    private static string FormatQty(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static string[] StrArray(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
            : [];

    private static double? Num(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        double? value = v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
        return value is { } x && double.IsFinite(x) && x >= 0 ? x : null;
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"\d\s*(ml|cl|l)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LiquidQuantity();
}
