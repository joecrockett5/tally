using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.Features.Meals;

/// <summary>Every meal layout a profile has had, so any day can be shown as it was.</summary>
public sealed class MealHistory(IReadOnlyList<MealPlan> plans)
{
    public const string UnknownName = "Removed meal";

    // Oldest first.
    private readonly List<MealPlan> _plans = plans.OrderBy(p => p.EffectiveFrom).ToList();

    public MealPlan? PlanFor(DateOnly date) => _plans.LastOrDefault(p => p.EffectiveFrom <= date);

    /// <summary>The meals shown on <paramref name="date"/>, in order.</summary>
    public IReadOnlyList<MealSlot> LayoutFor(DateOnly date) => PlanFor(date)?.Slots() ?? DefaultMeals.All;

    public bool Has(DateOnly date, Guid mealId) => LayoutFor(date).Any(m => m.Id == mealId);

    /// <summary>
    /// The name a meal had on <paramref name="date"/>. For a meal that isn't in that day's layout
    /// (removed while the day still had entries in it) this is its most recent earlier name.
    /// </summary>
    public string NameFor(Guid mealId, DateOnly date)
    {
        var layouts = _plans.Where(p => p.EffectiveFrom <= date).Reverse().Select(p => p.Slots())
            .Append(DefaultMeals.All)
            .Concat(_plans.Where(p => p.EffectiveFrom > date).Select(p => p.Slots()));
        foreach (var layout in layouts)
            if (layout.FirstOrDefault(m => m.Id == mealId) is { } slot) return slot.Name;
        return UnknownName;
    }

    /// <summary>
    /// Where an entry copied or moved from another day belongs on <paramref name="date"/>: the same meal
    /// if that day has it, otherwise one with the same name, otherwise unchanged.
    /// </summary>
    public Guid Remap(Guid mealId, DateOnly from, DateOnly date)
    {
        var layout = LayoutFor(date);
        if (layout.Any(m => m.Id == mealId)) return mealId;
        var name = NameFor(mealId, from);
        return layout.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))?.Id ?? mealId;
    }
}

public sealed class MealPlans(TallyDbContext db)
{
    public async Task<MealHistory> HistoryAsync(Guid profileId, CancellationToken ct) =>
        new(await db.MealPlans.AsNoTracking().Where(p => p.ProfileId == profileId).ToListAsync(ct));
}
