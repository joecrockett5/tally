using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Goals;

public static class GoalEndpoints
{
    public static GoalsDto ToDto(Goal g) => new(g.EnergyKcal, g.Fat, g.Saturates, g.Carbohydrate, g.Sugars, g.Fibre, g.Protein, g.Salt);

    public static void MapGoalEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/goals");

        g.MapGet("/", async (TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            ToDto(await GetOrCreateAsync(db, profile.Id, ct)));

        g.MapGet("/defaults", () => ToDto(Goal.UkDefaults(Guid.Empty)));

        g.MapPut("/", async (GoalsDto input, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            double?[] values = [input.EnergyKcal, input.Fat, input.Saturates, input.Carbohydrate, input.Sugars, input.Fibre, input.Protein, input.Salt];
            if (values.Any(v => v is { } x && (!double.IsFinite(x) || x <= 0 || x > 100_000)))
                return Results.Problem("Targets must be above zero, or left blank for no target.", statusCode: 400, title: "invalid_goal");

            var goal = await GetOrCreateAsync(db, profile.Id, ct);
            goal.EnergyKcal = input.EnergyKcal;
            goal.Fat = input.Fat;
            goal.Saturates = input.Saturates;
            goal.Carbohydrate = input.Carbohydrate;
            goal.Sugars = input.Sugars;
            goal.Fibre = input.Fibre;
            goal.Protein = input.Protein;
            goal.Salt = input.Salt;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(goal));
        });
    }

    /// <summary>Profiles get UK defaults on creation; this also covers any created before goals existed.</summary>
    public static async Task<Goal> GetOrCreateAsync(TallyDbContext db, Guid profileId, CancellationToken ct)
    {
        var goal = await db.Goals.FindAsync([profileId], ct);
        if (goal is not null) return goal;
        goal = Goal.UkDefaults(profileId);
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return goal;
    }
}
