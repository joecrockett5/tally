using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Tally.Api;
using Tally.Api.Data;
using System.Text.Json.Serialization;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using Tally.Api.Features.Import;
using Tally.Api.Features.Restaurants;
using Tally.Api.Features.Profiles;
using Tally.Api.OpenFoodFacts;
using Tally.Api.Infrastructure;

if (args.FirstOrDefault() == "healthcheck")
    return await HealthProbe.RunAsync();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TallyOptions>(builder.Configuration.GetSection(TallyOptions.Section));
builder.Services.AddDbContext<TallyDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Tally")
                ?? throw new InvalidOperationException("ConnectionStrings:Tally is not configured")));
builder.Services.AddScoped<ProfileContext>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<CofidImporter>();
builder.Services.AddScoped<FoodSearch>();
builder.Services.AddScoped<DiaryService>();
builder.Services.AddScoped<FoodImport>();
builder.Services.AddSingleton<Clock>();
builder.Services.AddScoped<OffFoodService>();
builder.Services.AddSingleton<OffRateGate>();
builder.Services.AddSingleton<OffRefreshQueue>();
builder.Services.AddHostedService<OffRefreshWorker>();
builder.Services.AddHttpClient<OffClient>((sp, http) =>
    http.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TallyOptions>>().Value.OpenFoodFacts.TimeoutSeconds));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();
// Imports can be a few hundred KB of CSV/JSON.
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 5 * 1024 * 1024);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // TLS terminates at Nginx Proxy Manager on a private network; trust it.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

await DatabaseStartup.MigrateAsync(app.Services);
await using (var scope = app.Services.CreateAsyncScope())
{
    var cofid = scope.ServiceProvider.GetRequiredService<CofidImporter>();
    if (args.FirstOrDefault() == "import-cofid")
    {
        await cofid.ImportAsync(CancellationToken.None);
        return 0;
    }
    await cofid.SeedIfEmptyAsync(CancellationToken.None);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/healthz");

var api = app.MapGroup("/api");
api.MapGet("/config", (Microsoft.Extensions.Options.IOptions<TallyOptions> o, Clock clock) =>
    new { version = AppInfo.Version, timeZone = o.Value.TimeZone, today = clock.Today, onlineEnabled = o.Value.OpenFoodFacts.Enabled });
api.MapProfileEndpoints();

// Everything below acts on behalf of a profile.
var scoped = api.MapGroup("").RequireProfile();
scoped.MapGet("/me", async (ProfileContext profile, TallyDbContext db) =>
    await db.Profiles.Where(p => p.Id == profile.Id).Select(p => new ProfileDto(p.Id, p.Name, p.Avatar, p.Colour)).FirstAsync());
scoped.MapDiaryEndpoints();
scoped.MapFoodEndpoints();
scoped.MapCustomFoodEndpoints();
scoped.MapRestaurantEndpoints();
scoped.MapImportEndpoints();
// Unknown /api routes must 404 rather than fall through to the SPA.
api.MapFallback(() => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();
return 0;

public partial class Program;
