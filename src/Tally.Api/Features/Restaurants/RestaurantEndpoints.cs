using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Foods;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Restaurants;

public sealed record RestaurantDto(Guid Id, string Name, int ItemCount);

public static class RestaurantEndpoints
{
    public static void MapRestaurantEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/restaurants");

        // Restaurants with items this profile can see (plus name matches, for autocomplete).
        g.MapGet("/", async (string? q, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var visible = db.Foods.VisibleTo(profile.Id).Where(f => f.RestaurantId != null);
            var term = q?.Trim().ToLowerInvariant() ?? "";
            var rows = await db.Restaurants.AsNoTracking()
                .Where(r => term == "" || r.NormalizedName.Contains(term))
                .Select(r => new { r.Id, r.Name, Count = visible.Count(f => f.RestaurantId == r.Id) })
                .Where(r => r.Count > 0 || term != "")
                .OrderBy(r => r.Name)
                .Take(200)
                .ToListAsync(ct);
            return rows.Select(r => new RestaurantDto(r.Id, r.Name, r.Count));
        });

        g.MapGet("/{id:guid}/foods", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (restaurant is null) return Results.NotFound();
            var foods = await db.Foods.AsNoTracking().VisibleTo(profile.Id)
                .Include(f => f.Servings).Include(f => f.Restaurant)
                .Where(f => f.RestaurantId == id)
                .OrderBy(f => f.Name)
                .ToListAsync(ct);
            return Results.Ok(new { restaurant = new RestaurantDto(restaurant.Id, restaurant.Name, foods.Count), foods = foods.Select(f => FoodDto.From(f, profile.Id)) });
        });
    }
}
