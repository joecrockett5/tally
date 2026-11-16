using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Meals;

/// <param name="EffectiveFrom">The day this layout started; null for the built-in defaults.</param>
public sealed record MealLayoutDto(DateOnly Date, DateOnly? EffectiveFrom, IReadOnlyList<MealSlot> Meals);

/// <param name="Id">An existing meal's id to keep (and maybe rename) it; null adds a new meal.</param>
public sealed record MealInput(Guid? Id, string? Name);

public sealed record UpdateMealsRequest(IReadOnlyList<MealInput>? Meals);

/// <summary>Each profile's own list of meals. Changes apply from today; earlier days are untouched.</summary>
public static class MealEndpoints
{
    public const int MaxMeals = 12;
    public const int MaxNameLength = 40;

    public static void MapMealEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/meals");

        // The layout for a day (today by default).
        g.MapGet("/", async (string? date, MealPlans plans, Clock clock, ProfileContext profile, CancellationToken ct) =>
        {
            var day = clock.Today;
            if (date is not null && !Clock.TryParseDate(date, out day))
                return Results.Problem("Dates must be in YYYY-MM-DD format.", statusCode: 400, title: "invalid_date");
            return Results.Ok(ToDto(await plans.HistoryAsync(profile.Id, ct), day));
        });

        g.MapPut("/", async (UpdateMealsRequest req, MealPlans plans, TallyDbContext db, Clock clock, ProfileContext profile, CancellationToken ct) =>
        {
            var today = clock.Today;
            var history = await plans.HistoryAsync(profile.Id, ct);
            var current = history.LayoutFor(today);

            var inputs = req.Meals ?? [];
            if (inputs.Count is < 1 or > MaxMeals) return Invalid($"Keep between 1 and {MaxMeals} meals.");

            var meals = new List<MealSlot>();
            foreach (var input in inputs)
            {
                var name = string.Join(' ', (input.Name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (name.Length is 0 or > MaxNameLength) return Invalid($"Give every meal a name (up to {MaxNameLength} characters).");
                if (meals.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Invalid($"Two meals can't both be called “{name}”.");
                // Only ids from the current layout carry over; anything else is a new meal.
                var id = input.Id is { } given && current.Any(m => m.Id == given) && meals.All(m => m.Id != given) ? given : Guid.CreateVersion7();
                meals.Add(new MealSlot(id, name));
            }

            if (!meals.SequenceEqual(current))
            {
                // Replace a plan already made today; one starting later (the clock or timezone moved) would shadow this one.
                await db.MealPlans.Where(p => p.ProfileId == profile.Id && p.EffectiveFrom >= today).ExecuteDeleteAsync(ct);
                db.MealPlans.Add(new MealPlan
                {
                    ProfileId = profile.Id,
                    EffectiveFrom = today,
                    MealIds = meals.Select(m => m.Id).ToArray(),
                    MealNames = meals.Select(m => m.Name).ToArray(),
                });
                await db.SaveChangesAsync(ct);
                history = await plans.HistoryAsync(profile.Id, ct);
            }
            return Results.Ok(ToDto(history, today));
        });
    }

    private static MealLayoutDto ToDto(MealHistory history, DateOnly date) =>
        new(date, history.PlanFor(date)?.EffectiveFrom, history.LayoutFor(date));

    private static IResult Invalid(string error) => Results.Problem(error, statusCode: 400, title: "invalid_meals");

    public static IResult UnknownMeal() =>
        Results.Problem("That meal isn't part of this day. Choose another.", statusCode: 400, title: "unknown_meal");
}
