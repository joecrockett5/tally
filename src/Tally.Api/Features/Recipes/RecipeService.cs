using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;

namespace Tally.Api.Features.Recipes;

public sealed class RecipeService(TallyDbContext db)
{
    public const string RecipeLabel = "recipe";

    public IQueryable<Recipe> Recipes(Guid profileId) =>
        db.Recipes.Where(r => r.ProfileId == profileId)
            .Include(r => r.Ingredients).ThenInclude(i => i.Food).ThenInclude(f => f.Servings)
            .Include(r => r.Ingredients).ThenInclude(i => i.Food).ThenInclude(f => f.Restaurant);

    // ---- nutrition -------------------------------------------------------

    public static Nutrients IngredientNutrients(Food food, double amount) =>
        food.IsPortionBased ? food.Nutrients.Scale(amount) : food.Nutrients.Scale(amount / 100);

    public static NutrientTotals Totals(Recipe r) =>
        NutrientTotals.Sum(r.Ingredients.Select(i => IngredientNutrients(i.Food, i.Amount)).ToList());

    public static Nutrients PerServing(Recipe r) => Totals(r).Values.Scale(1 / Math.Max(r.Servings, 0.0001));

    public static Nutrients? Per100g(Recipe r) =>
        r.CookedWeightGrams is > 0 ? Totals(r).Values.Scale(100 / r.CookedWeightGrams.Value) : null;

    public static RecipeDto ToDto(Recipe r, Guid profileId)
    {
        var totals = Totals(r);
        return new RecipeDto(
            r.Id, r.Name, r.Servings, r.CookedWeightGrams,
            r.Ingredients.OrderBy(i => i.SortOrder).Select(i => new IngredientDto(
                i.Id, FoodDto.From(i.Food, profileId), i.Unit, i.Quantity, i.ServingName, i.ServingAmount, i.Amount,
                IngredientNutrients(i.Food, i.Amount))).ToList(),
            totals,
            totals.Values.Scale(1 / Math.Max(r.Servings, 0.0001)),
            r.CookedWeightGrams is > 0 ? totals.Values.Scale(100 / r.CookedWeightGrams.Value) : null);
    }

    public static RecipeSummaryDto ToSummary(Recipe r) =>
        new(r.Id, r.Name, r.Servings, r.CookedWeightGrams, r.Ingredients.Count, PerServing(r));

    // ---- logging ---------------------------------------------------------

    /// <summary>
    /// Snapshots a recipe for the diary: per serving (logged in portions), or per
    /// 100 g of cooked weight when logged by grams.
    /// </summary>
    public static DiaryEntry? Snapshot(Recipe r, Guid profileId, DateOnly date, Meal meal, QuantityUnit? unit, double quantity, out string? error)
    {
        var byGrams = unit == QuantityUnit.Gram;
        if (byGrams && r.CookedWeightGrams is not > 0)
        {
            error = "Set the recipe's cooked weight to log it by grams.";
            return null;
        }
        var basis = byGrams ? NutritionBasis.Per100g : NutritionBasis.PerPortion;
        var q = Quantities.Resolve(basis, byGrams ? QuantityUnit.Gram : QuantityUnit.Portion, quantity, null, null, out error);
        if (q is null) return null;

        return new DiaryEntry
        {
            ProfileId = profileId,
            Date = date,
            Meal = meal,
            RecipeId = r.Id,
            Name = r.Name,
            SourceLabel = RecipeLabel,
            Basis = basis,
            PortionName = byGrams ? null : "serving",
            PerBasis = byGrams ? Per100g(r)! : PerServing(r),
            Unit = q.Unit,
            Quantity = q.Quantity,
            Amount = q.Amount,
        };
    }

    // ---- editing ---------------------------------------------------------

    public async Task<(List<RecipeIngredient>? Ingredients, string? Error)> BuildIngredientsAsync(
        Guid profileId, RecipeInput input, IReadOnlySet<Guid> alreadyUsed, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 200) return (null, "Give the recipe a name (up to 200 characters).");
        if (!double.IsFinite(input.Servings) || input.Servings <= 0 || input.Servings > 1000) return (null, "Servings must be above zero.");
        if (input.CookedWeightGrams is { } w && (!double.IsFinite(w) || w <= 0 || w > 100_000)) return (null, "Cooked weight must be above zero.");
        if (input.Ingredients is not { Count: > 0 and <= 100 }) return (null, "Add at least one ingredient.");

        var ids = input.Ingredients.Select(i => i.FoodId).Distinct().ToList();
        // Ingredients may be any visible food, or ones already in the recipe (even if since deleted/unshared).
        var foods = await db.Foods.Include(f => f.Servings)
            .Where(f => ids.Contains(f.Id) &&
                        ((f.DeletedAt == null && (f.Source != FoodSource.Custom || f.OwnerProfileId == profileId || f.IsShared))
                         || alreadyUsed.Contains(f.Id)))
            .ToDictionaryAsync(f => f.Id, ct);

        var result = new List<RecipeIngredient>();
        var order = 0;
        foreach (var i in input.Ingredients)
        {
            if (!foods.TryGetValue(i.FoodId, out var food)) return (null, "One of the ingredients is no longer available.");
            var q = DiaryService.ResolveForFood(food, i.Unit, i.Quantity, i.ServingId, out var error);
            if (q is null) return (null, $"{food.Name}: {error}");
            result.Add(new RecipeIngredient
            {
                FoodId = food.Id,
                Food = food,
                Unit = q.Unit,
                Quantity = q.Quantity,
                ServingName = q.ServingName,
                ServingAmount = q.ServingAmount,
                Amount = q.Amount,
                SortOrder = order++,
            });
        }
        return (result, null);
    }
}
