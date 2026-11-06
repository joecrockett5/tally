using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tally.Api.Tests;

public sealed class HealthTests(TallyApiFactory factory)
{
    [Fact]
    public async Task Healthz_reports_healthy_when_database_is_reachable()
    {
        var response = await factory.CreateClient().GetAsync("/healthz", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(health.GetProperty("ok").GetBoolean());
        Assert.Equal(AppInfo.Version, health.GetProperty("version").GetString());
    }

    [Theory]
    [InlineData(null, "dev")]
    [InlineData("", "dev")]
    [InlineData("  ", "dev")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-rc.1", "1.2.3-rc.1")]
    public void Version_comes_from_APP_VERSION_and_defaults_to_dev(string? value, string expected)
    {
        Assert.Equal(expected, AppInfo.ResolveVersion(value));
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_spa_fallback()
    {
        var response = await factory.CreateClient().GetAsync("/api/nope", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
