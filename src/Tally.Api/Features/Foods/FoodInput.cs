using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.Features.Foods;

public sealed record ServingInput(string Name, double Amount);

/// <summary>A custom food or restaurant item as entered by hand or imported.</summary>
public sealed record FoodInput(
    string Name,
    string? Brand,
    string? Restaurant,
    string? Barcode,
    NutritionBasis Basis,
    string? PortionName,
    Nutrients? Nutrients,
    IReadOnlyList<ServingInput>? Servings,
    bool IsShared);

public static class FoodInputs
{
    public static List<string> Validate(FoodInput input)
    {
        var errors = new List<string>();
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0) errors.Add("Name is required.");
        if (name.Length > 300) errors.Add("Name must be 300 characters or fewer.");
        if (input.Brand?.Trim().Length > 200) errors.Add("Brand must be 200 characters or fewer.");
        if (input.Restaurant?.Trim().Length > 100) errors.Add("Restaurant must be 100 characters or fewer.");
        if (input.PortionName?.Trim().Length > 100) errors.Add("Portion name must be 100 characters or fewer.");
        if (!string.IsNullOrWhiteSpace(input.Barcode) && Barcodes.Normalize(input.Barcode) is null)
            errors.Add("Barcode must be an EAN-13, EAN-8 or UPC-A number.");
        if (input.Nutrients?.HasNegative() == true) errors.Add("Nutrient values can't be negative.");
        if (input.Nutrients?.All().Any(x => x.Value is { } v && (!double.IsFinite(v) || v > 100_000)) == true)
            errors.Add("Nutrient values look too large.");
        foreach (var s in input.Servings ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Name) || s.Name.Trim().Length > 100) errors.Add("Each serving needs a name (up to 100 characters).");
            if (!double.IsFinite(s.Amount) || s.Amount <= 0 || s.Amount > 100_000) errors.Add($"Serving \"{s.Name}\" needs a size above zero.");
        }
        return errors;
    }

    /// <summary>Copies validated input onto a custom food, resolving (or creating) its restaurant.</summary>
    public static async Task ApplyAsync(TallyDbContext db, Food food, FoodInput input, CancellationToken ct)
    {
        food.Name = input.Name.Trim();
        food.Brand = Blank(input.Brand);
        food.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : Barcodes.Normalize(input.Barcode);
        food.Basis = input.Basis;
        food.PortionName = input.Basis == NutritionBasis.PerPortion ? Blank(input.PortionName) ?? "1 portion" : null;
        food.Nutrients = (input.Nutrients ?? Nutrients.Empty).WithDerivedEnergy();
        food.IsShared = input.IsShared;
        food.IsUk = true;
        food.UpdatedAt = DateTimeOffset.UtcNow;

        var restaurant = await ResolveRestaurantAsync(db, input.Restaurant, ct);
        food.RestaurantId = restaurant?.Id;
        food.RestaurantName = restaurant?.Name;
        food.Restaurant = restaurant;

        food.Servings.Clear();
        var order = 0;
        foreach (var s in input.Servings ?? [])
            food.Servings.Add(new FoodServing { Name = s.Name.Trim(), Amount = s.Amount, SortOrder = order++ });
    }

    public static async Task<Restaurant?> ResolveRestaurantAsync(TallyDbContext db, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = Restaurant.Normalize(name);
        var existing = db.Restaurants.Local.FirstOrDefault(r => r.NormalizedName == normalized)
                       ?? await db.Restaurants.FirstOrDefaultAsync(r => r.NormalizedName == normalized, ct);
        if (existing is not null) return existing;
        var created = new Restaurant { Name = name.Trim(), NormalizedName = normalized };
        db.Restaurants.Add(created);
        return created;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
