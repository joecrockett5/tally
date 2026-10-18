using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Tally.Api.OpenFoodFacts;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Tally.Api.Tests.TallyApiFactory))]

namespace Tally.Api.Tests;

/// <summary>
/// One real Postgres container and one API host for the whole test run.
/// Tests isolate themselves by creating their own profiles.
/// </summary>
public sealed class TallyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public FakeOffHandler Off { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server; // start the host (runs migrations + seeding)
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Tally", _postgres.GetConnectionString());
        builder.UseSetting("Tally:OpenFoodFacts:ContactEmail", "tests@example.com");
        builder.UseSetting("Tally:OpenFoodFacts:ProductBaseUrl", "http://off.test");
        builder.UseSetting("Tally:OpenFoodFacts:SearchBaseUrl", "http://search.off.test");
        builder.UseSetting("Tally:OpenFoodFacts:ProductRequestsPerMinute", "6000");
        builder.UseSetting("Tally:OpenFoodFacts:SearchRequestsPerMinute", "6000");
        builder.ConfigureTestServices(services =>
            services.AddHttpClient<OffClient>().ConfigurePrimaryHttpMessageHandler(() => Off));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
