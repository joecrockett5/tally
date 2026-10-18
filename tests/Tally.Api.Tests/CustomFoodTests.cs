using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using Tally.Api.Features.Restaurants;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class CustomFoodTests(TallyApiFactory factory)
{
    private static FoodInput Item(string name, string? restaurant = null, bool shared = false, string? barcode = null) => new(
        name, null, restaurant, barcode, NutritionBasis.PerPortion, "1 portion",
        new Nutrients { EnergyKcal = 500, Protein = 30, Salt = 1.5 }, [], shared);

    private static async Task<FoodDto> Create(HttpClient c, FoodInput input) =>
        await (await c.PostAsJsonAsync("/api/foods", input, Ct)).ReadAsync<FoodDto>();

    [Fact]
    public async Task Shared_restaurant_item_is_visible_to_others_who_can_copy_but_not_edit()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        var item = await Create(alice, Item($"Peri wrap {unique}", "Nando's", shared: true));

        Assert.Equal(FoodLabels.Restaurant, item.Source);
        Assert.True(item.CanEdit);
        Assert.Equal(0, item.Nutrients.EnergyKj is null ? 1 : 0); // kJ derived
        Assert.Equal(500 * 4.184, item.Nutrients.EnergyKj!.Value, 6);

        // Bob finds it by search and in the restaurant listing…
        var results = await (await bob.GetAsync($"/api/foods/search?q=peri%20wrap%20{unique}", Ct)).ReadAsync<List<FoodDto>>();
        var seen = Assert.Single(results, f => f.Id == item.Id);
        Assert.False(seen.CanEdit);
        var byRestaurant = await (await bob.GetAsync($"/api/foods/search?q=nando%27s%20{unique}", Ct)).ReadAsync<List<FoodDto>>();
        Assert.Equal(item.Id, byRestaurant[0].Id);
        var restaurants = await (await bob.GetAsync("/api/restaurants", Ct)).ReadAsync<List<RestaurantDto>>();
        Assert.Contains(restaurants, r => r.Name == "Nando's");

        // …can't edit or delete it…
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/foods/{item.Id}", Item("Hacked", "Nando's", true), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"/api/foods/{item.Id}", Ct)).StatusCode);

        // …but can copy it into an editable private food.
        var copy = await (await bob.PostAsync($"/api/foods/{item.Id}/copy", null, Ct)).ReadAsync<FoodDto>();
        Assert.True(copy.CanEdit);
        Assert.False(copy.IsShared);
        Assert.Equal("Nando's", copy.Restaurant);
        var renamed = await (await bob.PutAsJsonAsync($"/api/foods/{copy.Id}", Item("Bob's wrap", "Nando's"), Ct)).ReadAsync<FoodDto>();
        Assert.Equal("Bob's wrap", renamed.Name);

        // Alice's original is untouched; Alice can't see Bob's private copy.
        Assert.Equal($"Peri wrap {unique}", (await (await alice.GetAsync($"/api/foods/{item.Id}", Ct)).ReadAsync<FoodDto>()).Name);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/foods/{copy.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Private_foods_are_invisible_to_others_until_shared()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        var food = await Create(alice, Item($"Secret soup {unique}"));
        Assert.Equal(FoodLabels.Mine, food.Source);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/foods/{food.Id}", Ct)).StatusCode);
        Assert.Empty(await (await bob.GetAsync($"/api/foods/search?q=secret%20soup%20{unique}", Ct)).ReadAsync<List<FoodDto>>());

        await alice.PutAsJsonAsync($"/api/foods/{food.Id}", Item($"Secret soup {unique}", shared: true), Ct);
        var seen = await (await bob.GetAsync($"/api/foods/{food.Id}", Ct)).ReadAsync<FoodDto>();
        Assert.Equal(FoodLabels.Shared, seen.Source);

        // Unsharing hides it again.
        await alice.PutAsJsonAsync($"/api/foods/{food.Id}", Item($"Secret soup {unique}", shared: false), Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/foods/{food.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Editing_a_logged_custom_food_keeps_the_diary_snapshot()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var food = await Create(alice, Item("Pasta bake"));
        await alice.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-06-01", Meal.Dinner, food.Id, null, null, 1, null), Ct);

        await alice.PutAsJsonAsync($"/api/foods/{food.Id}", Item("Pasta bake") with { Nutrients = new Nutrients { EnergyKcal = 900 } }, Ct);
        await alice.DeleteAsync($"/api/foods/{food.Id}", Ct);

        var day = await (await alice.GetAsync("/api/diary/2026-06-01", Ct)).ReadAsync<DayDto>();
        Assert.Equal(500, day.Totals.Values.EnergyKcal!.Value, 6);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/foods/{food.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_profile_removes_private_foods_but_keeps_shared_ones()
    {
        var (alice, profile) = await factory.CreateProfileClientAsync("Leaver");
        var (bob, _) = await factory.CreateProfileClientAsync();
        var privateFood = await Create(alice, Item("Leaver private"));
        var sharedFood = await Create(alice, Item("Leaver shared", shared: true));

        await alice.DeleteAsync($"/api/profiles/{profile.Id}?confirmName=Leaver", Ct);

        await factory.WithDbAsync(async db => Assert.False(await db.Foods.AnyAsync(f => f.Id == privateFood.Id, Ct)));
        var stillThere = await (await bob.GetAsync($"/api/foods/{sharedFood.Id}", Ct)).ReadAsync<FoodDto>();
        Assert.Equal("Leaver", stillThere.CreatedByName);
        Assert.False(stillThere.CanEdit);
    }

    [Fact]
    public async Task Invalid_food_is_rejected()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var bad = Item("") with { Nutrients = new Nutrients { Fat = -1 }, Barcode = "123" };
        var response = await alice.PostAsJsonAsync("/api/foods", bad, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Name is required", body);
        Assert.Contains("negative", body);
        Assert.Contains("Barcode", body);
    }

    [Fact]
    public async Task Custom_food_with_barcode_wins_barcode_lookup()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var food = await Create(alice, Item("My own label", barcode: "5000000000103"));
        var lookup = await (await alice.GetAsync("/api/foods/barcode/5000000000103", Ct)).ReadAsync<BarcodeLookupResponse>();
        Assert.Equal(food.Id, lookup.Food!.Id);
        Assert.Equal(0, factory.Off.RequestCount("5000000000103"));
    }
}
