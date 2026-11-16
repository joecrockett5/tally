namespace Tally.Api.Data;

/// <summary>One meal in a profile's day, e.g. "Breakfast". The id survives renames.</summary>
public sealed record MealSlot(Guid Id, string Name);

/// <summary>
/// A profile's meal layout from <see cref="EffectiveFrom"/> until the next plan starts.
/// Editing meals adds a plan dated today, so earlier days keep the names and
/// order they had. A profile with no plan covering a day uses <see cref="DefaultMeals"/>.
/// </summary>
public sealed class MealPlan
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProfileId { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    // Parallel arrays, in display order.
    public Guid[] MealIds { get; set; } = [];
    public string[] MealNames { get; set; } = [];

    public IReadOnlyList<MealSlot> Slots() => MealIds.Zip(MealNames, (id, name) => new MealSlot(id, name)).ToList();
}

/// <summary>The meals every profile starts with. The ids are fixed so existing diaries map onto them.</summary>
public static class DefaultMeals
{
    public static readonly Guid Breakfast = new("0a11e000-0000-4000-8000-000000000001");
    public static readonly Guid Lunch = new("0a11e000-0000-4000-8000-000000000002");
    public static readonly Guid Dinner = new("0a11e000-0000-4000-8000-000000000003");
    public static readonly Guid Snacks = new("0a11e000-0000-4000-8000-000000000004");

    public static readonly IReadOnlyList<MealSlot> All =
    [
        new(Breakfast, "Breakfast"), new(Lunch, "Lunch"), new(Dinner, "Dinner"), new(Snacks, "Snacks"),
    ];
}
