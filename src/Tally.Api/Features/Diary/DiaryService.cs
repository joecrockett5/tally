using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Foods;

namespace Tally.Api.Features.Diary;

public sealed class DiaryService(TallyDbContext db)
{
    public async Task<DayDto> GetDayAsync(Guid profileId, DateOnly date, CancellationToken ct)
    {
        var entries = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.ProfileId == profileId && e.Date == date)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .ToListAsync(ct);

        var meals = Enum.GetValues<Meal>().Select(meal =>
        {
            var items = entries.Where(e => e.Meal == meal).Select(EntryDto.From).ToList();
            return new MealDto(meal, items, NutrientTotals.Sum(items.Select(i => i.Nutrients).ToList()));
        }).ToList();

        var goals = await GoalsFor(profileId, ct);
        return new DayDto(date, meals, NutrientTotals.Sum(entries.Select(e => e.Totals()).ToList()), goals);
    }

    private async Task<GoalsDto?> GoalsFor(Guid profileId, CancellationToken ct) =>
        Goals.GoalEndpoints.ToDto(await Goals.GoalEndpoints.GetOrCreateAsync(db, profileId, ct));

    /// <summary>Snapshots <paramref name="food"/> into a new entry (not yet added to the context).</summary>
    public static DiaryEntry Snapshot(Food food, Guid profileId, DateOnly date, Meal meal, ResolvedQuantity q) => new()
    {
        ProfileId = profileId,
        Date = date,
        Meal = meal,
        FoodId = food.Id,
        Name = food.Name,
        Brand = food.Restaurant?.Name ?? food.Brand,
        SourceLabel = FoodLabels.For(food, profileId),
        Basis = food.Basis,
        PortionName = food.PortionName,
        PerBasis = food.Nutrients,
        Unit = q.Unit,
        Quantity = q.Quantity,
        ServingName = q.ServingName,
        ServingAmount = q.ServingAmount,
        Amount = q.Amount,
    };

    public static ResolvedQuantity? ResolveForFood(Food food, QuantityUnit? unit, double quantity, Guid? servingId, out string? error)
    {
        var serving = servingId is null ? null : food.Servings.FirstOrDefault(s => s.Id == servingId);
        if (servingId is not null && serving is null)
        {
            error = "That serving no longer exists.";
            return null;
        }
        var u = unit ?? (serving is not null ? QuantityUnit.Serving : Quantities.DefaultUnit(food.Basis));
        return Quantities.Resolve(food.Basis, u, quantity, serving?.Name, serving?.Amount, out error);
    }

    public Task<Food?> FindVisibleFoodAsync(Guid profileId, Guid foodId, CancellationToken ct) =>
        db.Foods.AsNoTracking().VisibleTo(profileId)
            .Include(f => f.Servings).Include(f => f.Restaurant)
            .FirstOrDefaultAsync(f => f.Id == foodId, ct);

    /// <summary>Copies entries (with their snapshots) from one day/meal to another.</summary>
    public async Task<int> CopyAsync(Guid profileId, DateOnly from, DateOnly to, Meal? fromMeal, Meal? toMeal, CancellationToken ct)
    {
        var source = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.ProfileId == profileId && e.Date == from && (fromMeal == null || e.Meal == fromMeal))
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var i = 0;
        foreach (var e in source)
        {
            db.DiaryEntries.Add(new DiaryEntry
            {
                ProfileId = profileId,
                Date = to,
                Meal = toMeal ?? e.Meal,
                FoodId = e.FoodId,
                RecipeId = e.RecipeId,
                Name = e.Name,
                Brand = e.Brand,
                SourceLabel = e.SourceLabel,
                Basis = e.Basis,
                PortionName = e.PortionName,
                PerBasis = e.PerBasis,
                Unit = e.Unit,
                Quantity = e.Quantity,
                ServingName = e.ServingName,
                ServingAmount = e.ServingAmount,
                Amount = e.Amount,
                CreatedAt = now.AddMicroseconds(i++), // keep source order (Postgres stores µs)
            });
        }
        await db.SaveChangesAsync(ct);
        return source.Count;
    }

    public async Task<List<QuickPickDto>> QuickPicksAsync(Guid profileId, bool frequent, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-90);
        var grouped = db.DiaryEntries.AsNoTracking()
            .Where(e => e.ProfileId == profileId && e.FoodId != null && (!frequent || e.CreatedAt >= since))
            .GroupBy(e => e.FoodId!.Value)
            .Select(g => new { FoodId = g.Key, Last = g.Max(e => e.CreatedAt), Count = g.Count() });
        var top = await (frequent
                ? grouped.OrderByDescending(x => x.Count).ThenByDescending(x => x.Last)
                : grouped.OrderByDescending(x => x.Last))
            .Take(20)
            .ToListAsync(ct);

        var ids = top.Select(t => t.FoodId).ToList();
        var lastEntries = await db.DiaryEntries.AsNoTracking()
            .Where(e => e.ProfileId == profileId && e.FoodId != null && ids.Contains(e.FoodId.Value))
            .GroupBy(e => e.FoodId!.Value)
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
            .ToDictionaryAsync(e => e.FoodId!.Value, ct);
        var foods = await db.Foods.AsNoTracking().VisibleTo(profileId)
            .Include(f => f.Servings).Include(f => f.Restaurant)
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);

        var picks = new List<QuickPickDto>();
        foreach (var t in top)
        {
            if (!foods.TryGetValue(t.FoodId, out var food) || !lastEntries.TryGetValue(t.FoodId, out var last)) continue;
            var serving = last.Unit == QuantityUnit.Serving ? food.Servings.FirstOrDefault(s => s.Name == last.ServingName) : null;
            var (unit, qty) = last.Unit switch
            {
                QuantityUnit.Serving when serving is null => (Quantities.DefaultUnit(food.Basis), last.Amount),
                QuantityUnit.Gram or QuantityUnit.Millilitre when food.IsPortionBased => (QuantityUnit.Portion, 1.0),
                QuantityUnit.Portion when !food.IsPortionBased => (Quantities.DefaultUnit(food.Basis), 100.0),
                _ => (last.Unit, last.Quantity),
            };
            picks.Add(new QuickPickDto(FoodDto.From(food, profileId), unit, qty, serving?.Id, t.Count));
        }
        return picks;
    }
}
