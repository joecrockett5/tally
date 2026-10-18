using System.Net;

namespace Tally.Api.Tests;

public sealed class HealthTests(TallyApiFactory factory)
{
    [Fact]
    public async Task Healthz_reports_healthy_when_database_is_reachable()
    {
        var response = await factory.CreateClient().GetAsync("/healthz", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_spa_fallback()
    {
        var response = await factory.CreateClient().GetAsync("/api/nope", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
