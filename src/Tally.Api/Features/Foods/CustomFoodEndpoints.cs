using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Foods;

/// <summary>
/// Custom foods and restaurant items. Private by default; shared items are visible
/// to every profile, but only their creator can edit, unshare or delete them.
/// Anyone can copy a visible food into an editable private copy.
/// </summary>
public static class CustomFoodEndpoints
{
    public static void MapCustomFoodEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/foods");

        // filter: mine (created by me), shared (shared by anyone), all (both)
        g.MapGet("/", async (string? filter, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var me = profile.Id;
            var query = db.Foods.AsNoTracking().VisibleTo(me).Where(f => f.Source == FoodSource.Custom);
            query = filter switch
            {
                "mine" => query.Where(f => f.OwnerProfileId == me),
                "shared" => query.Where(f => f.IsShared),
                _ => query,
            };
            var foods = await query.Include(f => f.Servings).Include(f => f.Restaurant)
                .OrderBy(f => f.Restaurant!.Name).ThenBy(f => f.Name)
                .Take(500)
                .ToListAsync(ct);
            return foods.Select(f => FoodDto.From(f, me));
        });

        g.MapPost("/", async (FoodInput input, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            if (Invalid(input) is { } problem) return problem;
            var owner = await db.Profiles.FindAsync([profile.Id], ct);
            var food = new Food { Name = "", Source = FoodSource.Custom, OwnerProfileId = profile.Id, CreatedByName = owner!.Name };
            await FoodInputs.ApplyAsync(db, food, input, ct);
            db.Foods.Add(food);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/foods/{food.Id}", FoodDto.From(food, profile.Id));
        });

        g.MapPut("/{id:guid}", async (Guid id, FoodInput input, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var food = await db.Foods.Include(f => f.Servings).Include(f => f.Restaurant)
                .VisibleTo(profile.Id).FirstOrDefaultAsync(f => f.Id == id, ct);
            if (food is null) return Results.NotFound();
            if (!CanEdit(food, profile.Id)) return NotYours();
            if (Invalid(input) is { } problem) return problem;

            await FoodInputs.ApplyAsync(db, food, input, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(FoodDto.From(food, profile.Id));
        });

        // Soft delete: the owner's recipes keep resolving; diary entries are snapshots anyway.
        g.MapDelete("/{id:guid}", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var food = await db.Foods.VisibleTo(profile.Id).FirstOrDefaultAsync(f => f.Id == id, ct);
            if (food is null) return Results.NotFound();
            if (!CanEdit(food, profile.Id)) return NotYours();
            food.DeletedAt = DateTimeOffset.UtcNow;
            food.IsShared = false;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/copy", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var source = await db.Foods.AsNoTracking().Include(f => f.Servings).Include(f => f.Restaurant)
                .VisibleTo(profile.Id).FirstOrDefaultAsync(f => f.Id == id, ct);
            if (source is null) return Results.NotFound();
            var owner = await db.Profiles.FindAsync([profile.Id], ct);

            var copy = new Food
            {
                Source = FoodSource.Custom,
                OwnerProfileId = profile.Id,
                CreatedByName = owner!.Name,
                IsShared = false,
                Name = source.Name,
                Brand = source.Brand,
                RestaurantId = source.RestaurantId,
                RestaurantName = source.RestaurantName,
                Barcode = source.Barcode,
                Basis = source.Basis,
                PortionName = source.PortionName,
                Nutrients = source.Nutrients,
                CarbsMayIncludeFibre = source.CarbsMayIncludeFibre,
                Countries = source.Countries,
                IsUk = source.IsUk,
                ImageUrl = source.ImageUrl,
                CopiedFromFoodId = source.Id,
                Servings = source.Servings.OrderBy(s => s.SortOrder)
                    .Select(s => new FoodServing { Name = s.Name, Amount = s.Amount, SortOrder = s.SortOrder }).ToList(),
            };
            db.Foods.Add(copy);
            await db.SaveChangesAsync(ct);
            copy.Restaurant = source.Restaurant;
            return Results.Created($"/api/foods/{copy.Id}", FoodDto.From(copy, profile.Id));
        });
    }

    public static bool CanEdit(Food food, Guid profileId) =>
        food.Source == FoodSource.Custom && food.OwnerProfileId == profileId;

    private static IResult NotYours() =>
        Results.Problem("Only the profile that created this food can change it. Copy it to your foods to make your own version.",
            statusCode: 403, title: "not_owner");

    private static IResult? Invalid(FoodInput input) =>
        FoodInputs.Validate(input) is { Count: > 0 } errors
            ? Results.Problem(string.Join(" ", errors), statusCode: 400, title: "invalid_food")
            : null;
}
