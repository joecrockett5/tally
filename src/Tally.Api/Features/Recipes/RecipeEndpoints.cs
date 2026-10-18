using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Recipes;

/// <summary>Recipes are private to their profile.</summary>
public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/recipes");

        g.MapGet("/", async (RecipeService recipes, ProfileContext profile, CancellationToken ct) =>
            (await recipes.Recipes(profile.Id).AsNoTracking().AsSplitQuery().OrderBy(r => r.Name).ToListAsync(ct))
                .Select(RecipeService.ToSummary));

        g.MapGet("/{id:guid}", async (Guid id, RecipeService recipes, ProfileContext profile, CancellationToken ct) =>
            await recipes.Recipes(profile.Id).AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(r => r.Id == id, ct) is { } recipe
                ? Results.Ok(RecipeService.ToDto(recipe, profile.Id))
                : Results.NotFound());

        g.MapPost("/", async (RecipeInput input, RecipeService recipes, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var (ingredients, error) = await recipes.BuildIngredientsAsync(profile.Id, input, new HashSet<Guid>(), ct);
            if (ingredients is null) return Invalid(error);
            var recipe = new Recipe
            {
                ProfileId = profile.Id,
                Name = input.Name.Trim(),
                Servings = input.Servings,
                CookedWeightGrams = input.CookedWeightGrams,
                Ingredients = ingredients,
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/recipes/{recipe.Id}", RecipeService.ToDto(recipe, profile.Id));
        });

        g.MapPut("/{id:guid}", async (Guid id, RecipeInput input, RecipeService recipes, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            var recipe = await recipes.Recipes(profile.Id).AsSplitQuery().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (recipe is null) return Results.NotFound();
            var (ingredients, error) = await recipes.BuildIngredientsAsync(
                profile.Id, input, recipe.Ingredients.Select(i => i.FoodId).ToHashSet(), ct);
            if (ingredients is null) return Invalid(error);

            recipe.Name = input.Name.Trim();
            recipe.Servings = input.Servings;
            recipe.CookedWeightGrams = input.CookedWeightGrams;
            recipe.UpdatedAt = DateTimeOffset.UtcNow;
            db.RemoveRange(recipe.Ingredients);
            recipe.Ingredients = ingredients;
            await db.SaveChangesAsync(ct);
            return Results.Ok(RecipeService.ToDto(recipe, profile.Id));
        });

        g.MapDelete("/{id:guid}", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            await db.Recipes.Where(r => r.Id == id && r.ProfileId == profile.Id).ExecuteDeleteAsync(ct) > 0
                ? Results.NoContent()
                : Results.NotFound());
    }

    private static IResult Invalid(string? error) => Results.Problem(error, statusCode: 400, title: "invalid_recipe");
}
