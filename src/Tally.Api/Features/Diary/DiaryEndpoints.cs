using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Recipes;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Diary;

public static class DiaryEndpoints
{
    public static void MapDiaryEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/diary");

        g.MapGet("/{date}", async (string date, DiaryService diary, ProfileContext profile, CancellationToken ct) =>
            Clock.TryParseDate(date, out var day)
                ? Results.Ok(await diary.GetDayAsync(profile.Id, day, ct))
                : BadDate());

        g.MapPost("/entries", async (LogEntryRequest req, DiaryService diary, RecipeService recipes, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            if (!Clock.TryParseDate(req.Date, out var date)) return BadDate();

            if (req.RecipeId is { } recipeId)
            {
                var recipe = await recipes.Recipes(profile.Id).AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(r => r.Id == recipeId, ct);
                if (recipe is null) return Results.NotFound();
                var recipeEntry = RecipeService.Snapshot(recipe, profile.Id, date, req.Meal, req.Unit, req.Quantity, out var recipeError);
                if (recipeEntry is null) return Results.Problem(recipeError, statusCode: 400, title: "invalid_quantity");
                db.DiaryEntries.Add(recipeEntry);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/diary/entries/{recipeEntry.Id}", EntryDto.From(recipeEntry));
            }

            if (req.FoodId is not { } foodId)
                return Results.Problem("Choose a food to log.", statusCode: 400, title: "food_required");

            var food = await diary.FindVisibleFoodAsync(profile.Id, foodId, ct);
            if (food is null) return Results.NotFound();

            var q = DiaryService.ResolveForFood(food, req.Unit, req.Quantity, req.ServingId, out var error);
            if (q is null) return Results.Problem(error, statusCode: 400, title: "invalid_quantity");

            var entry = DiaryService.Snapshot(food, profile.Id, date, req.Meal, q);
            db.DiaryEntries.Add(entry);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/diary/entries/{entry.Id}", EntryDto.From(entry));
        });

        // Edits re-use the entry's own snapshot; the live food is never consulted.
        g.MapPut("/entries/{id:guid}", async (Guid id, UpdateEntryRequest req, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var entry = await db.DiaryEntries.FirstOrDefaultAsync(e => e.Id == id && e.ProfileId == profile.Id, ct);
            if (entry is null) return Results.NotFound();

            if (req.Date is not null)
            {
                if (!Clock.TryParseDate(req.Date, out var date)) return BadDate();
                entry.Date = date;
            }
            if (req.Meal is { } meal) entry.Meal = meal;
            if (req.Quantity is not null || req.Unit is not null)
            {
                var unit = req.Unit ?? entry.Unit;
                var q = Quantities.Resolve(entry.Basis, unit, req.Quantity ?? entry.Quantity, entry.ServingName, entry.ServingAmount, out var error);
                if (q is null) return Results.Problem(error, statusCode: 400, title: "invalid_quantity");
                (entry.Unit, entry.Quantity, entry.Amount) = (q.Unit, q.Quantity, q.Amount);
                if (unit != QuantityUnit.Serving) (entry.ServingName, entry.ServingAmount) = (null, null);
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(EntryDto.From(entry));
        });

        g.MapDelete("/entries/{id:guid}", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            await db.DiaryEntries.Where(e => e.Id == id && e.ProfileId == profile.Id).ExecuteDeleteAsync(ct) > 0
                ? Results.NoContent()
                : Results.NotFound());

        g.MapPost("/copy", async (CopyRequest req, DiaryService diary, ProfileContext profile, CancellationToken ct) =>
        {
            if (!Clock.TryParseDate(req.FromDate, out var from) || !Clock.TryParseDate(req.ToDate, out var to)) return BadDate();
            if (from == to && (req.FromMeal ?? req.ToMeal) == (req.ToMeal ?? req.FromMeal))
                return Results.Problem("Choose a different day or meal to copy into.", statusCode: 400, title: "same_target");
            var copied = await diary.CopyAsync(profile.Id, from, to, req.FromMeal, req.ToMeal, ct);
            return Results.Ok(new { copied });
        });

        scoped.MapGet("/foods/recent", (DiaryService diary, ProfileContext profile, CancellationToken ct) =>
            diary.QuickPicksAsync(profile.Id, frequent: false, ct));
        scoped.MapGet("/foods/frequent", (DiaryService diary, ProfileContext profile, CancellationToken ct) =>
            diary.QuickPicksAsync(profile.Id, frequent: true, ct));
    }

    private static IResult BadDate() =>
        Results.Problem("Dates must be in YYYY-MM-DD format.", statusCode: 400, title: "invalid_date");
}
