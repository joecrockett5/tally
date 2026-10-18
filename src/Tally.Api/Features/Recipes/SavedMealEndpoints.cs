using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Recipes;

/// <summary>Saved meals: named groups of foods/recipes logged together. Private to their profile.</summary>
public static class SavedMealEndpoints
{
    public static void MapSavedMealEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/saved-meals");

        g.MapGet("/", async (TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            (await Load(db, profile.Id).AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct)).Select(m => ToDto(m, profile.Id)));

        g.MapGet("/{id:guid}", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            await Load(db, profile.Id).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct) is { } meal
                ? Results.Ok(ToDto(meal, profile.Id))
                : Results.NotFound());

        g.MapPost("/", async (SavedMealInput input, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var (items, error) = await BuildItemsAsync(db, profile.Id, input.Items, ct);
            if (items is null || NameError(input.Name) is not null) return Invalid(error ?? NameError(input.Name));
            var meal = new SavedMeal { ProfileId = profile.Id, Name = input.Name.Trim(), Items = items };
            db.SavedMeals.Add(meal);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/saved-meals/{meal.Id}", await ReloadAsync(db, profile.Id, meal.Id, ct));
        });

        g.MapPut("/{id:guid}", async (Guid id, SavedMealInput input, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var meal = await Load(db, profile.Id).FirstOrDefaultAsync(m => m.Id == id, ct);
            if (meal is null) return Results.NotFound();
            var (items, error) = await BuildItemsAsync(db, profile.Id, input.Items, ct);
            if (items is null || NameError(input.Name) is not null) return Invalid(error ?? NameError(input.Name));
            meal.Name = input.Name.Trim();
            db.RemoveRange(meal.Items);
            meal.Items = items;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ReloadAsync(db, profile.Id, meal.Id, ct));
        });

        g.MapDelete("/{id:guid}", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            await db.SavedMeals.Where(m => m.Id == id && m.ProfileId == profile.Id).ExecuteDeleteAsync(ct) > 0
                ? Results.NoContent()
                : Results.NotFound());

        // "Save this meal": turns a diary meal into a saved meal.
        g.MapPost("/from-diary", async (SaveFromDiaryRequest req, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            if (!Clock.TryParseDate(req.Date, out var date)) return Results.Problem("Dates must be in YYYY-MM-DD format.", statusCode: 400, title: "invalid_date");
            if (NameError(req.Name) is { } nameError) return Invalid(nameError);

            var entries = await db.DiaryEntries.AsNoTracking()
                .Where(e => e.ProfileId == profile.Id && e.Date == date && e.Meal == req.Meal)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(ct);
            var foodIds = entries.Where(e => e.FoodId != null).Select(e => e.FoodId!.Value).ToList();
            var recipeIds = entries.Where(e => e.RecipeId != null).Select(e => e.RecipeId!.Value).ToList();
            var visibleFoods = await db.Foods.VisibleTo(profile.Id).Where(f => foodIds.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
            var ownRecipes = await db.Recipes.Where(r => r.ProfileId == profile.Id && recipeIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);

            var order = 0;
            var items = entries
                .Where(e => (e.FoodId is { } f && visibleFoods.Contains(f)) || (e.RecipeId is { } r && ownRecipes.Contains(r)))
                .Select(e => new SavedMealItem
                {
                    FoodId = e.FoodId,
                    RecipeId = e.FoodId is null ? e.RecipeId : null,
                    Unit = e.Unit,
                    Quantity = e.Quantity,
                    ServingName = e.ServingName,
                    ServingAmount = e.ServingAmount,
                    Amount = e.Amount,
                    SortOrder = order++,
                })
                .ToList();
            if (items.Count == 0) return Invalid("There's nothing in that meal that can be saved.");

            var meal = new SavedMeal { ProfileId = profile.Id, Name = req.Name.Trim(), Items = items };
            db.SavedMeals.Add(meal);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/saved-meals/{meal.Id}", await ReloadAsync(db, profile.Id, meal.Id, ct));
        });

        // Logs every item with fresh snapshots of current values.
        g.MapPost("/{id:guid}/log", async (Guid id, LogSavedMealRequest req, TallyDbContext db, RecipeService recipes, ProfileContext profile, CancellationToken ct) =>
        {
            if (!Clock.TryParseDate(req.Date, out var date)) return Results.Problem("Dates must be in YYYY-MM-DD format.", statusCode: 400, title: "invalid_date");
            var meal = await Load(db, profile.Id).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
            if (meal is null) return Results.NotFound();

            var recipeIds = meal.Items.Where(i => i.RecipeId != null).Select(i => i.RecipeId!.Value).ToList();
            var recipeMap = await recipes.Recipes(profile.Id).AsNoTracking().AsSplitQuery()
                .Where(r => recipeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

            var now = DateTimeOffset.UtcNow;
            var logged = new List<DiaryEntry>();
            var skipped = 0;
            foreach (var item in meal.Items.OrderBy(i => i.SortOrder))
            {
                DiaryEntry? entry = null;
                if (item.Food is { DeletedAt: null } food)
                {
                    var q = Quantities.Resolve(food.Basis, item.Unit, item.Quantity, item.ServingName, item.ServingAmount, out _);
                    if (q is not null) entry = DiaryService.Snapshot(food, profile.Id, date, req.Meal, q);
                }
                else if (item.RecipeId is { } rid && recipeMap.TryGetValue(rid, out var recipe))
                {
                    entry = RecipeService.Snapshot(recipe, profile.Id, date, req.Meal, item.Unit, item.Quantity, out _);
                }

                if (entry is null) { skipped++; continue; }
                entry.CreatedAt = now.AddTicks(logged.Count);
                logged.Add(entry);
            }
            db.DiaryEntries.AddRange(logged);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { logged = logged.Count, skipped });
        });
    }

    private static async Task<SavedMealDto> ReloadAsync(TallyDbContext db, Guid profileId, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return ToDto(await Load(db, profileId).AsNoTracking().FirstAsync(m => m.Id == id, ct), profileId);
    }

    private static IQueryable<SavedMeal> Load(TallyDbContext db, Guid profileId) =>
        db.SavedMeals.Where(m => m.ProfileId == profileId)
            .Include(m => m.Items).ThenInclude(i => i.Food).ThenInclude(f => f!.Restaurant)
            .Include(m => m.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(i => i.Food)
            .AsSplitQuery();

    private static async Task<(List<SavedMealItem>? Items, string? Error)> BuildItemsAsync(
        TallyDbContext db, Guid profileId, IReadOnlyList<SavedMealItemInput>? inputs, CancellationToken ct)
    {
        if (inputs is not { Count: > 0 and <= 50 }) return (null, "Add at least one item.");
        var foodIds = inputs.Where(i => i.FoodId != null).Select(i => i.FoodId!.Value).ToList();
        var recipeIds = inputs.Where(i => i.RecipeId != null).Select(i => i.RecipeId!.Value).ToList();
        var foods = await db.Foods.Include(f => f.Servings).VisibleTo(profileId).Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        var recipeSet = (await db.Recipes.Where(r => r.ProfileId == profileId && recipeIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct)).ToHashSet();

        var items = new List<SavedMealItem>();
        var order = 0;
        foreach (var i in inputs)
        {
            if (i.FoodId is { } fid)
            {
                if (!foods.TryGetValue(fid, out var food)) return (null, "One of the foods is no longer available.");
                var q = DiaryService.ResolveForFood(food, i.Unit, i.Quantity, i.ServingId, out var error);
                if (q is null) return (null, $"{food.Name}: {error}");
                items.Add(new SavedMealItem { FoodId = fid, Unit = q.Unit, Quantity = q.Quantity, ServingName = q.ServingName, ServingAmount = q.ServingAmount, Amount = q.Amount, SortOrder = order++ });
            }
            else if (i.RecipeId is { } rid && recipeSet.Contains(rid))
            {
                var unit = i.Unit == QuantityUnit.Gram ? QuantityUnit.Gram : QuantityUnit.Portion;
                if (!double.IsFinite(i.Quantity) || i.Quantity <= 0) return (null, "Quantity must be more than zero.");
                items.Add(new SavedMealItem { RecipeId = rid, Unit = unit, Quantity = i.Quantity, Amount = i.Quantity, SortOrder = order++ });
            }
            else return (null, "Each item needs a food or one of your recipes.");
        }
        return (items, null);
    }

    private static SavedMealDto ToDto(SavedMeal m, Guid profileId)
    {
        var items = m.Items.OrderBy(i => i.SortOrder).Select(i =>
        {
            if (i.Food is { } f)
            {
                var n = f.IsPortionBased ? f.Nutrients.Scale(i.Amount) : f.Nutrients.Scale(i.Amount / 100);
                return new SavedMealItemDto(i.Id, f.Id, null, f.Name, FoodLabels.For(f, profileId), i.Unit, i.Quantity, i.ServingName, i.ServingAmount, f.PortionName, f.Basis, n);
            }
            var r = i.Recipe!;
            var gram = i.Unit == QuantityUnit.Gram;
            var nutrients = gram && RecipeService.Per100g(r) is { } per100 ? per100.Scale(i.Quantity / 100) : RecipeService.PerServing(r).Scale(i.Quantity);
            return new SavedMealItemDto(i.Id, null, r.Id, r.Name, RecipeService.RecipeLabel, i.Unit, i.Quantity, null, null, "serving",
                gram ? NutritionBasis.Per100g : NutritionBasis.PerPortion, nutrients);
        }).ToList();
        return new SavedMealDto(m.Id, m.Name, items, NutrientTotals.Sum(items.Select(i => i.Nutrients).ToList()));
    }

    private static string? NameError(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 ? "Give the meal a name (up to 200 characters)." : null;

    private static IResult Invalid(string? error) => Results.Problem(error, statusCode: 400, title: "invalid_saved_meal");
}
