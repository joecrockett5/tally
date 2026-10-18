using System.Net;
using Tally.Api.Features.Foods;
using Tally.Api.OpenFoodFacts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class FoodSearchTests(TallyApiFactory factory)
{
    [Fact]
    public async Task Chicken_breast_returns_cofid_generic_results()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var results = await (await client.GetAsync("/api/foods/search?q=chicken%20breast", Ct)).ReadAsync<List<FoodDto>>();

        Assert.NotEmpty(results);
        Assert.Equal(FoodLabels.Cofid, results[0].Source);
        Assert.Contains("chicken", results[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("breast", results[0].Name, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(results[0].Nutrients.EnergyKcal);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("basmati rice")]
    [InlineData("chick")] // prefix match while typing
    public async Task Common_generic_foods_are_found(string q)
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var results = await (await client.GetAsync($"/api/foods/search?q={Uri.EscapeDataString(q)}", Ct)).ReadAsync<List<FoodDto>>();
        Assert.Contains(results, r => r.Source == FoodLabels.Cofid);
    }

    [Fact]
    public async Task Blank_query_returns_nothing()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        Assert.Empty(await (await client.GetAsync("/api/foods/search?q=%20", Ct)).ReadAsync<List<FoodDto>>());
    }

    [Fact]
    public async Task Barcode_lookup_fetches_from_off_once_then_serves_cache()
    {
        const string code = "5000000000011";
        factory.Off.Products[code] = FakeOffHandler.Product(code, "Test Crisps", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 520, ["fat_100g"] = 30, ["salt_100g"] = 1.2 },
            brands: "Walkers", servingQuantity: 25, servingSize: "1 bag (25 g)");
        var (client, _) = await factory.CreateProfileClientAsync();

        var first = await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>();
        var second = await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>();

        Assert.Equal("found", first.Status);
        Assert.Equal("Test Crisps", first.Food!.Name);
        Assert.Equal(FoodLabels.OpenFoodFacts, first.Food.Source);
        Assert.Equal("found", second.Status);
        Assert.Equal(first.Food.Id, second.Food!.Id);
        Assert.Equal(1, factory.Off.RequestCount(code));

        var request = factory.Off.Requests.First(r => r.RequestUri!.ToString().Contains(code));
        Assert.Equal($"Tally/{AppInfo.Version} (tests@example.com)", request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Unknown_barcode_is_negatively_cached()
    {
        const string code = "5000000000028";
        var (client, _) = await factory.CreateProfileClientAsync();

        var first = await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>();
        var second = await (await client.GetAsync($"/api/foods/barcode/{code}", Ct)).ReadAsync<BarcodeLookupResponse>();

        Assert.Equal("not_found", first.Status);
        Assert.Equal(code, first.Barcode);
        Assert.Equal("not_found", second.Status);
        Assert.Equal(1, factory.Off.RequestCount(code));
    }

    [Fact]
    public async Task Invalid_barcode_is_rejected()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/foods/barcode/123", Ct)).StatusCode);
    }

    [Fact]
    public async Task Off_outage_reports_unavailable_but_cached_products_still_work()
    {
        const string cachedCode = "5000000000035";
        factory.Off.Products[cachedCode] = FakeOffHandler.Product(cachedCode, "Cached Beans", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 80 });
        var (client, _) = await factory.CreateProfileClientAsync();
        Assert.Equal("found", (await (await client.GetAsync($"/api/foods/barcode/{cachedCode}", Ct)).ReadAsync<BarcodeLookupResponse>()).Status);

        // A separate host (own rate gate) whose OFF is down.
        var down = new FakeOffHandler { ForceStatus = HttpStatusCode.ServiceUnavailable };
        using var outage = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddHttpClient<OffClient>().ConfigurePrimaryHttpMessageHandler(() => down)));
        var outageClient = outage.CreateClient();
        outageClient.DefaultRequestHeaders.Add("X-Profile-Id", client.DefaultRequestHeaders.GetValues("X-Profile-Id").First());

        var fresh = await (await outageClient.GetAsync("/api/foods/barcode/5000000000042", Ct)).ReadAsync<BarcodeLookupResponse>();
        Assert.Equal("unavailable", fresh.Status);

        var cached = await (await outageClient.GetAsync($"/api/foods/barcode/{cachedCode}", Ct)).ReadAsync<BarcodeLookupResponse>();
        Assert.Equal("found", cached.Status);

        var online = await (await outageClient.GetAsync("/api/foods/search/online?q=zzqx%20outage", Ct)).ReadAsync<OnlineSearchResponse>();
        Assert.False(online.Available);
        Assert.NotNull(online.Message);
    }

    [Fact]
    public async Task Online_search_ranks_uk_products_first_and_caches()
    {
        factory.Off.SearchHits["houmous"] =
        [
            FakeOffHandler.Product("3000000000017", "Houmous Nature", ["en:france"], new Dictionary<string, double> { ["energy-kcal_100g"] = 300 }),
            FakeOffHandler.Product("5000000000059", "Classic Houmous", ["en:united-kingdom"], new Dictionary<string, double> { ["energy-kcal_100g"] = 320 }),
        ];
        var (client, _) = await factory.CreateProfileClientAsync();

        var result = await (await client.GetAsync("/api/foods/search/online?q=houmous", Ct)).ReadAsync<OnlineSearchResponse>();
        var before = factory.Off.RequestCount("/search");
        var again = await (await client.GetAsync("/api/foods/search/online?q=Houmous", Ct)).ReadAsync<OnlineSearchResponse>();

        Assert.True(result.Available);
        Assert.Equal(["Classic Houmous", "Houmous Nature"], result.Results.Select(r => r.Name));
        Assert.True(result.Results[0].IsUk);
        Assert.Equal(before, factory.Off.RequestCount("/search")); // second call served from cache
        Assert.Equal(2, again.Results.Count);

        // Products found online are now in the local index too.
        var local = await (await client.GetAsync("/api/foods/search?q=classic%20houmous", Ct)).ReadAsync<List<FoodDto>>();
        Assert.Contains(local, f => f.Name == "Classic Houmous" && f.Source == FoodLabels.OpenFoodFacts);
    }
}
