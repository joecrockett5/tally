using System.Net;
using System.Net.Http.Json;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using Tally.Api.Features.Recipes;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class RecipeTests(TallyApiFactory factory)
{
    private static async Task<FoodDto> Food(HttpClient c, string name, double kcal, double protein) =>
        await (await c.PostAsJsonAsync("/api/foods", new FoodInput(name, null, null, null, NutritionBasis.Per100g, null,
            new Nutrients { EnergyKcal = kcal, Protein = protein }, [new ServingInput("1 tin", 400)], false), Ct)).ReadAsync<FoodDto>();

    private static async Task<(RecipeDto Recipe, FoodDto Lentils)> Chilli(HttpClient c)
    {
        var lentils = await Food(c, "Lentils", 100, 8);   // 400 g → 400 kcal, 32 g protein
        var tomatoes = await Food(c, "Tomatoes", 20, 1);  // 2 tins (800 g) → 160 kcal, 8 g protein
        var recipe = await (await c.PostAsJsonAsync("/api/recipes", new RecipeInput("Lentil chilli", 4, 1200,
        [
            new IngredientInput(lentils.Id, QuantityUnit.Gram, 400, null),
            new IngredientInput(tomatoes.Id, null, 2, tomatoes.Servings[0].Id),
        ]), Ct)).ReadAsync<RecipeDto>();
        return (recipe, lentils);
    }

    [Fact]
    public async Task Recipe_nutrition_is_computed_per_serving_and_per_100g()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var (recipe, _) = await Chilli(client);

        Assert.Equal(560, recipe.Totals.Values.EnergyKcal!.Value, 6);
        Assert.Equal(140, recipe.PerServing.EnergyKcal!.Value, 6);
        Assert.Equal(10, recipe.PerServing.Protein!.Value, 6);
        Assert.Equal(560 / 12.0, recipe.Per100g!.EnergyKcal!.Value, 6);
        Assert.Contains("fat", recipe.Totals.Incomplete);
        Assert.Equal(800, recipe.Ingredients[1].Amount);
    }

    [Fact]
    public async Task Recipes_log_by_serving_or_grams_and_are_snapshotted()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var (recipe, lentils) = await Chilli(client);

        var bowl = await (await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-07-01", Meal.Dinner, null, recipe.Id, null, 1.5, null), Ct)).ReadAsync<EntryDto>();
        var grams = await (await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-07-01", Meal.Lunch, null, recipe.Id, QuantityUnit.Gram, 300, null), Ct)).ReadAsync<EntryDto>();
        Assert.Equal(210, bowl.Nutrients.EnergyKcal!.Value, 6);
        Assert.Equal("recipe", bowl.Source);
        Assert.Equal(140, grams.Nutrients.EnergyKcal!.Value, 6);

        // Changing an ingredient later changes the recipe but not the diary.
        await client.PutAsJsonAsync($"/api/foods/{lentils.Id}", new FoodInput("Lentils", null, null, null, NutritionBasis.Per100g, null,
            new Nutrients { EnergyKcal = 300 }, [], false), Ct);
        var updated = await (await client.GetAsync($"/api/recipes/{recipe.Id}", Ct)).ReadAsync<RecipeDto>();
        Assert.Equal(1360, updated.Totals.Values.EnergyKcal!.Value, 6);
        var day = await (await client.GetAsync("/api/diary/2026-07-01", Ct)).ReadAsync<DayDto>();
        Assert.Equal(350, day.Totals.Values.EnergyKcal!.Value, 6);

        // Deleting the recipe keeps the entries.
        await client.DeleteAsync($"/api/recipes/{recipe.Id}", Ct);
        day = await (await client.GetAsync("/api/diary/2026-07-01", Ct)).ReadAsync<DayDto>();
        Assert.Equal(2, day.Meals.Sum(m => m.Entries.Count));
    }

    [Fact]
    public async Task Logging_by_grams_needs_a_cooked_weight()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var food = await Food(client, "Rice", 130, 3);
        var recipe = await (await client.PostAsJsonAsync("/api/recipes", new RecipeInput("Plain rice", 2, null,
            [new IngredientInput(food.Id, QuantityUnit.Gram, 300, null)]), Ct)).ReadAsync<RecipeDto>();
        var response = await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-07-02", Meal.Dinner, null, recipe.Id, QuantityUnit.Gram, 100, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recipes_are_private()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        var (recipe, _) = await Chilli(alice);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/recipes/{recipe.Id}", Ct)).StatusCode);
        Assert.Empty(await (await bob.GetAsync("/api/recipes", Ct)).ReadAsync<List<RecipeSummaryDto>>());
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-07-01", Meal.Dinner, null, recipe.Id, null, 1, null), Ct)).StatusCode);
    }

    [Fact]
    public async Task Recipe_cannot_use_someone_elses_private_food()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        var secret = await Food(alice, "Secret sauce", 300, 1);
        var response = await bob.PostAsJsonAsync("/api/recipes", new RecipeInput("Nicked", 1, null, [new IngredientInput(secret.Id, QuantityUnit.Gram, 10, null)]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class SavedMealTests(TallyApiFactory factory)
{
    [Fact]
    public async Task Save_a_diary_meal_and_log_it_again_in_one_go()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");
        var oats = await client.FirstFoodAsync("porridge oats");
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-08-01", Meal.Breakfast, banana.Id, null, null, 120, null), Ct);
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-08-01", Meal.Breakfast, oats.Id, null, null, 50, null), Ct);

        var saved = await (await client.PostAsJsonAsync("/api/saved-meals/from-diary", new SaveFromDiaryRequest("Usual breakfast", "2026-08-01", Meal.Breakfast), Ct)).ReadAsync<SavedMealDto>();
        Assert.Equal(2, saved.Items.Count);

        var result = await (await client.PostAsJsonAsync($"/api/saved-meals/{saved.Id}/log", new LogSavedMealRequest("2026-08-05", Meal.Breakfast), Ct)).ReadAsync<Dictionary<string, int>>();
        Assert.Equal(2, result["logged"]);

        var day = await (await client.GetAsync("/api/diary/2026-08-05", Ct)).ReadAsync<DayDto>();
        var breakfast = day.Meals.Single(m => m.Meal == Meal.Breakfast).Entries;
        Assert.Equal([banana.Name, oats.Name], breakfast.Select(e => e.Name));
        Assert.Equal(120, breakfast[0].Quantity);
        Assert.Equal(saved.Totals.Values.EnergyKcal!.Value, day.Totals.Values.EnergyKcal!.Value, 6);
    }

    [Fact]
    public async Task Saved_meals_can_mix_foods_and_recipes_and_are_private()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var (other, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");
        var recipe = await (await client.PostAsJsonAsync("/api/recipes", new RecipeInput("Smoothie", 2, null,
            [new IngredientInput(banana.Id, QuantityUnit.Gram, 200, null)]), Ct)).ReadAsync<RecipeDto>();

        var meal = await (await client.PostAsJsonAsync("/api/saved-meals", new SavedMealInput("Snack combo",
        [
            new SavedMealItemInput(banana.Id, null, null, 100, null),
            new SavedMealItemInput(null, recipe.Id, null, 1, null),
        ]), Ct)).ReadAsync<SavedMealDto>();
        Assert.Equal(banana.Nutrients.EnergyKcal!.Value * 2, meal.Totals.Values.EnergyKcal!.Value, 6);

        Assert.Empty(await (await other.GetAsync("/api/saved-meals", Ct)).ReadAsync<List<SavedMealDto>>());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/saved-meals/{meal.Id}/log", new LogSavedMealRequest("2026-08-06", Meal.Snacks), Ct)).StatusCode);
    }
}
