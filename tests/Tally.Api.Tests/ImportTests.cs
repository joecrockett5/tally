using System.Net.Http.Json;
using Tally.Api.Features.Foods;
using Tally.Api.Features.Import;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class ImportTests(TallyApiFactory factory)
{
    private static string Csv(string unique) => $"""
        name,restaurant,basis,portion_name,energy_kcal,fat,salt,sodium,servings
        Burger {unique},Testaurant {unique},per_portion,1 burger,650,30,,1.2,
        Fries {unique},Testaurant {unique},per_portion,regular,320,15,0.9,,large=1.5
        Fries {unique},Testaurant {unique},per_portion,regular,320,15,0.9,,
        Broken {unique},,per_100g,,lots,,,,
        Granola {unique},,per_100g,,450,18,0.05,,1 bowl=50
        """;

    [Fact]
    public async Task Preview_validates_rows_and_flags_duplicates()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await factory.CreateProfileClientAsync();

        var preview = await (await client.PostAsJsonAsync("/api/import/preview", new ImportRequest("csv", Csv(unique), false), Ct)).ReadAsync<ImportPreview>();

        Assert.Equal(5, preview.Rows.Count);
        Assert.Equal(3, preview.Valid);
        Assert.Equal(1, preview.Invalid);
        Assert.Equal(1, preview.Duplicates);
        Assert.Equal(2, preview.Rows[0].Row); // line numbers count the header
        Assert.Equal(3.0, preview.Rows[0].Food!.Nutrients!.Salt!.Value, 6); // salt from sodium
        Assert.Equal("Repeated in this file", preview.Rows[2].Duplicate);
        Assert.Contains(preview.Rows[3].Errors, e => e.Contains("energy_kcal"));
        Assert.Single(preview.Rows[1].Food!.Servings!);
    }

    [Fact]
    public async Task Commit_imports_valid_rows_and_detects_existing_duplicates_next_time()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await factory.CreateProfileClientAsync();
        var (other, _) = await factory.CreateProfileClientAsync();

        var result = await (await client.PostAsJsonAsync("/api/import/commit", new ImportRequest("csv", Csv(unique), Shared: true), Ct)).ReadAsync<ImportResult>();
        Assert.Equal(3, result.Created);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Equal(1, result.SkippedInvalid);

        // Shared at import time, so other profiles see the restaurant items.
        var found = await (await other.GetAsync($"/api/foods/search?q=burger%20{unique}", Ct)).ReadAsync<List<FoodDto>>();
        var burger = found[0];
        Assert.Equal($"Burger {unique}", burger.Name);
        Assert.Equal(FoodLabels.Restaurant, burger.Source);
        Assert.Equal($"Testaurant {unique}", burger.Restaurant);

        var again = await (await other.PostAsJsonAsync("/api/import/preview", new ImportRequest("csv", Csv(unique), false), Ct)).ReadAsync<ImportPreview>();
        Assert.Equal("Already exists", again.Rows[0].Duplicate);
    }

    [Fact]
    public async Task Json_import_supports_serving_arrays()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var json = $$"""
            [{ "name": "Oats {{unique}}", "basis": "per_100g", "energy_kj": 1550, "servings": [{ "name": "1 bowl", "amount": 40 }] },
             { "name": "Bad {{unique}}", "colour": "blue", "energy_kcal": 1 }]
            """;
        var (client, _) = await factory.CreateProfileClientAsync();
        var preview = await (await client.PostAsJsonAsync("/api/import/preview", new ImportRequest("json", json, false), Ct)).ReadAsync<ImportPreview>();

        Assert.Equal(1, preview.Valid);
        Assert.Equal(1550 / 4.184, preview.Rows[0].Food!.Nutrients!.EnergyKcal!.Value, 6);
        Assert.Equal(40, preview.Rows[0].Food!.Servings![0].Amount);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("colour"));
    }

    [Fact]
    public async Task Example_files_in_the_repo_are_valid()
    {
        var root = FindRepoRoot();
        var (client, _) = await factory.CreateProfileClientAsync();
        foreach (var (file, format) in new[] { ("restaurant-menu.csv", "csv"), ("custom-foods.json", "json") })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(root, "examples", file), Ct);
            var preview = await (await client.PostAsJsonAsync("/api/import/preview", new ImportRequest(format, content, false), Ct)).ReadAsync<ImportPreview>();
            Assert.Equal(0, preview.Invalid);
            Assert.True(preview.Rows.Count > 0);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tally.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
