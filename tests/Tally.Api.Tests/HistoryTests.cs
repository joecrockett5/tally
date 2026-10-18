using System.Net;
using System.Net.Http.Json;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using Tally.Api.Features.History;
using Tally.Api.Infrastructure;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class HistoryTests(TallyApiFactory factory)
{
    private static async Task<FoodDto> Food(HttpClient c, string name, Nutrients n) =>
        await (await c.PostAsJsonAsync("/api/foods", new FoodInput(name, "Brand, \"quoted\"", null, null, NutritionBasis.Per100g, null, n, [], false), Ct)).ReadAsync<FoodDto>();

    [Fact]
    public async Task Weekly_history_averages_only_logged_days()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var food = await Food(client, "Test meal", new Nutrients { EnergyKcal = 100, Protein = 10, Carbohydrate = 10, Fat = 4, Salt = 0.1 });
        // Mon 05/10/2026: 2000 kcal (on target); Wed: 1000 kcal (under); other days empty.
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-10-05", Meal.Lunch, food.Id, null, QuantityUnit.Gram, 2000, null), Ct);
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-10-07", Meal.Lunch, food.Id, null, QuantityUnit.Gram, 1000, null), Ct);

        var h = await (await client.GetAsync("/api/history?from=2026-10-05&to=2026-10-11", Ct)).ReadAsync<HistoryDto>();

        Assert.Equal(7, h.Days.Count);
        Assert.Equal(2, h.LoggedDays);
        Assert.Equal(1500, h.Averages.EnergyKcal!.Value, 6);
        Assert.Equal(1, h.DaysOnTarget);
        Assert.True(h.Days[0].OnTarget);
        Assert.False(h.Days[2].OnTarget);
        Assert.Null(h.Days[1].OnTarget);
        Assert.Null(h.Averages.Sugars); // never known
        // 10 g protein (40 kcal), 10 g carbs (40 kcal), 4 g fat (36 kcal) per 100 g.
        Assert.Equal(40.0 / 116 * 100, h.Macros!.ProteinPct, 6);
        Assert.Equal(36.0 / 116 * 100, h.Macros.FatPct, 6);
    }

    [Fact]
    public async Task Exceeding_a_limit_means_not_on_target()
    {
        var goals = new GoalsDto(2000, 70, 20, 260, 90, 30, 50, 6);
        var onEnergyButSalty = new Nutrients { EnergyKcal = 2000, Salt = 9 };
        Assert.False(HistoryEndpoints.OnTarget(onEnergyButSalty, goals));
        Assert.True(HistoryEndpoints.OnTarget(new Nutrients { EnergyKcal = 1850 }, goals));
        Assert.Null(HistoryEndpoints.OnTarget(new Nutrients { EnergyKcal = 1850 }, goals with { EnergyKcal = null }));
    }

    [Fact]
    public async Task Csv_export_has_one_row_per_entry_with_blanks_for_unknowns()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var food = await Food(client, "Mystery, item", new Nutrients { EnergyKcal = 250 });
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-10-01", Meal.Breakfast, food.Id, null, QuantityUnit.Gram, 50, null), Ct);
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-10-03", Meal.Snacks, food.Id, null, QuantityUnit.Gram, 20, null), Ct);
        await client.PostAsJsonAsync("/api/diary/entries", new LogEntryRequest("2026-11-01", Meal.Snacks, food.Id, null, QuantityUnit.Gram, 20, null), Ct);

        var response = await client.GetAsync("/api/export/diary.csv?from=2026-10-01&to=2026-10-31", Ct);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("tally-diary-2026-10-01-to-2026-10-31.csv", response.Content.Headers.ContentDisposition!.ToString());

        var rows = Csv.Parse((await response.Content.ReadAsStringAsync(Ct)).TrimStart('﻿'));
        Assert.Equal(3, rows.Count);
        Assert.Equal(["date", "meal", "food", "brand", "source"], rows[0][..5]);
        Assert.Equal(["2026-10-01", "Breakfast", "Mystery, item", "Brand, \"quoted\"", "Mine", "50", "g"], rows[1][..7]);
        Assert.Equal("125", rows[1][10]); // energy_kcal
        Assert.Equal("", rows[1][12]);    // fat unknown → blank, not 0
    }

    [Theory]
    [InlineData("from=2026-10-10&to=2026-10-01")]
    [InlineData("from=2025-01-01&to=2026-12-31")]
    [InlineData("from=bad&to=2026-10-01")]
    public async Task Bad_ranges_are_rejected(string query)
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/history?{query}", Ct)).StatusCode);
    }
}
