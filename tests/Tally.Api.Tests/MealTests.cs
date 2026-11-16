using System.Net;
using System.Net.Http.Json;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Meals;
using Tally.Api.Infrastructure;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class MealTests(TallyApiFactory factory)
{
    private static Task<HttpResponseMessage> Put(HttpClient c, params MealInput[] meals) =>
        c.PutAsJsonAsync("/api/meals", new UpdateMealsRequest(meals), Ct);

    private static Task<HttpResponseMessage> Log(HttpClient c, Guid foodId, DateOnly date, Guid mealId) =>
        c.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest(date.ToString("yyyy-MM-dd"), mealId, foodId, null, null, 100, null), Ct);

    private static async Task<DayDto> Day(HttpClient c, DateOnly date) =>
        await (await c.GetAsync($"/api/diary/{date:yyyy-MM-dd}", Ct)).ReadAsync<DayDto>();

    private async Task<DateOnly> Today(HttpClient c) =>
        (await (await c.GetAsync("/api/meals", Ct)).ReadAsync<MealLayoutDto>()).Date;

    [Fact]
    public async Task New_profiles_start_with_the_four_default_meals()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var layout = await (await client.GetAsync("/api/meals", Ct)).ReadAsync<MealLayoutDto>();
        Assert.Null(layout.EffectiveFrom);
        Assert.Equal(["Breakfast", "Lunch", "Dinner", "Snacks"], layout.Meals.Select(m => m.Name));
        Assert.Equal(DefaultMeals.All, layout.Meals);
    }

    [Fact]
    public async Task Changes_apply_from_today_and_leave_earlier_days_as_they_were()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");
        var today = await Today(client);
        var yesterday = today.AddDays(-1);
        (await Log(client, banana.Id, yesterday, DefaultMeals.Dinner)).EnsureSuccessStatusCode();
        (await Log(client, banana.Id, yesterday, DefaultMeals.Snacks)).EnsureSuccessStatusCode();

        // Rename Dinner, drop Snacks, add a meal, and put it first.
        var updated = await (await Put(client,
            new MealInput(null, "  Pre-workout "),
            new MealInput(DefaultMeals.Breakfast, "Breakfast"),
            new MealInput(DefaultMeals.Lunch, "Lunch"),
            new MealInput(DefaultMeals.Dinner, "Tea"))).ReadAsync<MealLayoutDto>();
        Assert.Equal(today, updated.EffectiveFrom);
        Assert.Equal(["Pre-workout", "Breakfast", "Lunch", "Tea"], updated.Meals.Select(m => m.Name));
        Assert.Equal(DefaultMeals.Dinner, updated.Meals[3].Id);
        var preWorkout = updated.Meals[0].Id;

        var now = await Day(client, today);
        Assert.Equal(["Pre-workout", "Breakfast", "Lunch", "Tea"], now.Meals.Select(m => m.Name));
        Assert.Equal(["Pre-workout", "Breakfast", "Lunch", "Tea"], (await Day(client, today.AddDays(7))).Meals.Select(m => m.Name));

        var before = await Day(client, yesterday);
        Assert.Equal(["Breakfast", "Lunch", "Dinner", "Snacks"], before.Meals.Select(m => m.Name));
        Assert.All(before.Meals, m => Assert.True(m.Active));
        Assert.Single(before.Meals.Single(m => m.Name == "Dinner").Entries);
        Assert.Single(before.Meals.Single(m => m.Name == "Snacks").Entries);

        // Each day only accepts its own meals.
        Assert.Equal(HttpStatusCode.Created, (await Log(client, banana.Id, today, preWorkout)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Log(client, banana.Id, today, DefaultMeals.Snacks)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Log(client, banana.Id, yesterday, DefaultMeals.Snacks)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Log(client, banana.Id, yesterday, preWorkout)).StatusCode);

        // Copying yesterday's dinner lands in today's renamed meal.
        await client.PostAsJsonAsync("/api/diary/copy", new CopyRequest($"{yesterday:yyyy-MM-dd}", $"{today:yyyy-MM-dd}", DefaultMeals.Dinner, DefaultMeals.Dinner), Ct);
        Assert.Single((await Day(client, today)).Meals.Single(m => m.Name == "Tea").Entries);
    }

    [Fact]
    public async Task A_removed_meal_still_shows_the_entries_it_holds()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var banana = await client.FirstFoodAsync("banana");
        var today = await Today(client);
        var entry = await (await Log(client, banana.Id, today, DefaultMeals.Snacks)).ReadAsync<EntryDto>();

        await Put(client, new MealInput(DefaultMeals.Breakfast, "Breakfast"), new MealInput(DefaultMeals.Dinner, "Dinner"));

        var day = await Day(client, today);
        Assert.Equal(["Breakfast", "Dinner", "Snacks"], day.Meals.Select(m => m.Name));
        var snacks = day.Meals[2];
        Assert.False(snacks.Active);
        Assert.Equal(entry.Id, Assert.Single(snacks.Entries).Id);
        Assert.Equal(banana.Nutrients.EnergyKcal!.Value, day.Totals.Values.EnergyKcal!.Value, 6);

        // It can be edited where it is, and moved into a meal the day has.
        var edited = await (await client.PutAsJsonAsync($"/api/diary/entries/{entry.Id}", new UpdateEntryRequest(DefaultMeals.Snacks, null, null, 50), Ct)).ReadAsync<EntryDto>();
        Assert.Equal(DefaultMeals.Snacks, edited.MealId);
        var moved = await (await client.PutAsJsonAsync($"/api/diary/entries/{entry.Id}", new UpdateEntryRequest(DefaultMeals.Dinner, null, null, null), Ct)).ReadAsync<EntryDto>();
        Assert.Equal(DefaultMeals.Dinner, moved.MealId);
        Assert.Equal(["Breakfast", "Dinner"], (await Day(client, today)).Meals.Select(m => m.Name));
    }

    [Fact]
    public async Task Each_layout_covers_the_days_until_the_next_one()
    {
        var (client, profile) = await factory.CreateProfileClientAsync();
        var today = await Today(client);
        var brunch = Guid.CreateVersion7();
        await factory.WithDbAsync(async db =>
        {
            db.MealPlans.Add(new MealPlan { ProfileId = profile.Id, EffectiveFrom = today.AddDays(-30), MealIds = [brunch, DefaultMeals.Dinner], MealNames = ["Brunch", "Dinner"] });
            await db.SaveChangesAsync(Ct);
        });
        await Put(client, new MealInput(brunch, "Late breakfast"), new MealInput(DefaultMeals.Dinner, "Dinner"));

        Assert.Equal(["Breakfast", "Lunch", "Dinner", "Snacks"], (await Day(client, today.AddDays(-31))).Meals.Select(m => m.Name));
        Assert.Equal(["Brunch", "Dinner"], (await Day(client, today.AddDays(-30))).Meals.Select(m => m.Name));
        Assert.Equal(["Brunch", "Dinner"], (await Day(client, today.AddDays(-1))).Meals.Select(m => m.Name));
        Assert.Equal(["Late breakfast", "Dinner"], (await Day(client, today)).Meals.Select(m => m.Name));

        // The export uses each day's own names and meal order.
        var banana = await client.FirstFoodAsync("banana");
        (await Log(client, banana.Id, today.AddDays(-1), DefaultMeals.Dinner)).EnsureSuccessStatusCode();
        (await Log(client, banana.Id, today.AddDays(-1), brunch)).EnsureSuccessStatusCode();
        (await Log(client, banana.Id, today, brunch)).EnsureSuccessStatusCode();
        var csv = await client.GetStringAsync($"/api/export/diary.csv?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}", Ct);
        Assert.Equal(["Brunch", "Dinner", "Late breakfast"], Csv.Parse(csv.TrimStart('\uFEFF')).Skip(1).Select(r => r[1]));

        var past = await (await client.GetAsync($"/api/meals?date={today.AddDays(-5):yyyy-MM-dd}", Ct)).ReadAsync<MealLayoutDto>();
        Assert.Equal(today.AddDays(-30), past.EffectiveFrom);
        Assert.Equal("Brunch", past.Meals[0].Name);
    }

    [Fact]
    public async Task Meals_belong_to_one_profile()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        await Put(alice, new MealInput(null, "Only meal"));

        Assert.Equal(["Only meal"], (await (await alice.GetAsync("/api/meals", Ct)).ReadAsync<MealLayoutDto>()).Meals.Select(m => m.Name));
        Assert.Equal(DefaultMeals.All, (await (await bob.GetAsync("/api/meals", Ct)).ReadAsync<MealLayoutDto>()).Meals);
    }

    [Fact]
    public async Task Invalid_layouts_are_rejected_and_unchanged_ones_are_not_stored()
    {
        var (client, profile) = await factory.CreateProfileClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, new MealInput(null, "  "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, new MealInput(null, "Lunch"), new MealInput(null, "lunch"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, new MealInput(null, new string('x', MealEndpoints.MaxNameLength + 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Enumerable.Range(0, MealEndpoints.MaxMeals + 1).Select(i => new MealInput(null, $"Meal {i}")).ToArray())).StatusCode);

        var same = await (await Put(client, DefaultMeals.All.Select(m => new MealInput(m.Id, m.Name)).ToArray())).ReadAsync<MealLayoutDto>();
        Assert.Null(same.EffectiveFrom);
        await factory.WithDbAsync(async db => Assert.Empty(db.MealPlans.Where(p => p.ProfileId == profile.Id)));
    }
}
