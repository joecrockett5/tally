using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class DiaryTests(TallyApiFactory factory)
{
    private const string Day = "2026-03-29"; // BST starts that day: dates must not shift

    private static Task<HttpResponseMessage> Log(HttpClient c, Guid foodId, double qty, QuantityUnit? unit = null, Guid? servingId = null, Meal meal = Meal.Lunch, string date = Day) =>
        c.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest(date, meal, foodId, null, unit, qty, servingId), Ct);

    [Fact]
    public async Task Logging_by_grams_scales_per_100g_values()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");

        var entry = await (await Log(client, banana.Id, 150)).ReadAsync<EntryDto>();

        Assert.Equal(QuantityUnit.Gram, entry.Unit);
        Assert.Equal(banana.Nutrients.EnergyKcal!.Value * 1.5, entry.Nutrients.EnergyKcal!.Value, 6);
        Assert.Equal(DateOnly.Parse(Day), entry.Date);

        var day = await (await client.GetAsync($"/api/diary/{Day}", Ct)).ReadAsync<DayDto>();
        var lunch = day.Meals.Single(m => m.Meal == Meal.Lunch);
        Assert.Single(lunch.Entries);
        Assert.Equal(entry.Nutrients.EnergyKcal!.Value, day.Totals.Values.EnergyKcal!.Value, 6);
        Assert.Empty(day.Meals.Single(m => m.Meal == Meal.Dinner).Entries);
    }

    [Fact]
    public async Task Logging_by_serving_uses_serving_size()
    {
        const string code = "5000000000066";
        factory.Off.Products[code] = FakeOffHandler.Product(code, "Bread", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 250 }, servingQuantity: 36, servingSize: "1 slice (36 g)");
        var (client, _) = await factory.CreateProfileClientAsync();
        var bread = (await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>()).Food!;
        var slice = bread.Servings.First(s => s.Name.StartsWith("1 slice"));

        var entry = await (await Log(client, bread.Id, 2, servingId: slice.Id)).ReadAsync<EntryDto>();

        Assert.Equal(QuantityUnit.Serving, entry.Unit);
        Assert.Equal(72, entry.Amount);
        Assert.Equal(180, entry.Nutrients.EnergyKcal!.Value, 6);
    }

    [Fact]
    public async Task Portion_only_foods_log_by_portion_and_reject_grams()
    {
        const string code = "5000000000073";
        factory.Off.Products[code] = FakeOffHandler.Product(code, "Meal deal wrap", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_serving"] = 450 }, servingSize: "1 wrap");
        var (client, _) = await factory.CreateProfileClientAsync();
        var wrap = (await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>()).Food!;
        Assert.Equal(NutritionBasis.PerPortion, wrap.Basis);

        Assert.Equal(HttpStatusCode.BadRequest, (await Log(client, wrap.Id, 200, QuantityUnit.Gram)).StatusCode);
        var entry = await (await Log(client, wrap.Id, 1.5)).ReadAsync<EntryDto>();
        Assert.Equal(QuantityUnit.Portion, entry.Unit);
        Assert.Equal(675, entry.Nutrients.EnergyKcal!.Value, 6);
    }

    [Fact]
    public async Task Editing_a_food_after_logging_does_not_change_history()
    {
        const string code = "5000000000080";
        factory.Off.Products[code] = FakeOffHandler.Product(code, "Yoghurt", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 100, ["protein_100g"] = 5 });
        var (client, _) = await factory.CreateProfileClientAsync();
        var food = (await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>()).Food!;
        var entry = await (await Log(client, food.Id, 100)).ReadAsync<EntryDto>();

        // The underlying food changes (e.g. refreshed OFF data or an owner's edit).
        await factory.WithDbAsync(async db =>
        {
            var f = await db.Foods.SingleAsync(x => x.Id == food.Id, Ct);
            f.Nutrients = f.Nutrients with { EnergyKcal = 999 };
            await db.SaveChangesAsync(Ct);
        });

        var day = await (await client.GetAsync($"/api/diary/{Day}", Ct)).ReadAsync<DayDto>();
        Assert.Equal(100, day.Meals.SelectMany(m => m.Entries).Single(e => e.Id == entry.Id).Nutrients.EnergyKcal!.Value, 6);

        // Changing the quantity re-uses the snapshot, too.
        var edited = await (await client.PutAsJsonAsync($"/api/diary/entries/{entry.Id}", new UpdateEntryRequest(Meal.Dinner, null, null, 200), Ct)).ReadAsync<EntryDto>();
        Assert.Equal(200, edited.Nutrients.EnergyKcal!.Value, 6);
        Assert.Equal(Meal.Dinner, edited.Meal);
    }

    [Fact]
    public async Task Totals_flag_nutrients_that_include_unknowns()
    {
        const string code = "5000000000097";
        factory.Off.Products[code] = FakeOffHandler.Product(code, "Mystery", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 200 });
        var (client, _) = await factory.CreateProfileClientAsync();
        var mystery = (await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>()).Food!;
        var banana = await client.FirstFoodAsync("banana");
        await Log(client, mystery.Id, 100);
        await Log(client, banana.Id, 100);

        var day = await (await client.GetAsync($"/api/diary/{Day}", Ct)).ReadAsync<DayDto>();
        Assert.Contains("fat", day.Totals.Incomplete);
        Assert.DoesNotContain("energyKcal", day.Totals.Incomplete);
        Assert.Equal(banana.Nutrients.Fat!.Value, day.Totals.Values.Fat!.Value, 6);
    }

    [Fact]
    public async Task Copy_meal_from_yesterday()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");
        await Log(client, banana.Id, 120, meal: Meal.Breakfast, date: "2026-05-01");
        await Log(client, banana.Id, 80, meal: Meal.Lunch, date: "2026-05-01");

        var result = await (await client.PostAsJsonAsync("/api/diary/copy", new CopyRequest("2026-05-01", "2026-05-02", Meal.Breakfast, Meal.Breakfast), Ct))
            .ReadAsync<Dictionary<string, int>>();
        Assert.Equal(1, result["copied"]);

        var day = await (await client.GetAsync("/api/diary/2026-05-02", Ct)).ReadAsync<DayDto>();
        var entry = Assert.Single(day.Meals.SelectMany(m => m.Entries));
        Assert.Equal(Meal.Breakfast, entry.Meal);
        Assert.Equal(120, entry.Quantity);
    }

    [Fact]
    public async Task Entries_are_scoped_to_their_profile()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        var banana = await alice.FirstFoodAsync("banana");
        var entry = await (await Log(alice, banana.Id, 100)).ReadAsync<EntryDto>();

        var bobsDay = await (await bob.GetAsync($"/api/diary/{Day}", Ct)).ReadAsync<DayDto>();
        Assert.Empty(bobsDay.Meals.SelectMany(m => m.Entries));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/diary/entries/{entry.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/diary/entries/{entry.Id}", new UpdateEntryRequest(null, null, null, 1), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/diary/entries/{entry.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Recent_and_frequent_reflect_logging()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");
        var rice = await client.FirstFoodAsync("basmati rice");
        await Log(client, banana.Id, 100);
        await Log(client, banana.Id, 110);
        await Log(client, rice.Id, 180);

        var recent = await (await client.GetAsync("/api/foods/recent", Ct)).ReadAsync<List<QuickPickDto>>();
        var frequent = await (await client.GetAsync("/api/foods/frequent", Ct)).ReadAsync<List<QuickPickDto>>();

        Assert.Equal(rice.Id, recent[0].Food.Id);
        Assert.Equal(180, recent[0].Quantity);
        Assert.Equal(banana.Id, frequent[0].Food.Id);
        Assert.Equal(2, frequent[0].TimesLogged);
        Assert.Equal(110, frequent[0].Quantity); // last used amount
    }

    [Fact]
    public async Task Deleting_a_profile_removes_its_diary()
    {
        var (client, profile) = await factory.CreateProfileClientAsync("Gone Soon");
        var banana = await client.FirstFoodAsync("banana");
        await Log(client, banana.Id, 100);

        await client.DeleteAsync($"/api/profiles/{profile.Id}?confirmName=Gone%20Soon", Ct);

        await factory.WithDbAsync(async db =>
            Assert.False(await db.DiaryEntries.AnyAsync(e => e.ProfileId == profile.Id, Ct)));
    }

    [Theory]
    [InlineData("29/03/2026")]
    [InlineData("2026-13-01")]
    public async Task Bad_dates_are_rejected(string date)
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/diary/{Uri.EscapeDataString(date)}", Ct)).StatusCode);
    }
}
