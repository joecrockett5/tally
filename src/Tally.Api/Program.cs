using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Api;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;
using Tally.Api.Features.Goals;
using Tally.Api.Features.History;
using Tally.Api.Features.Import;
using Tally.Api.Features.Meals;
using Tally.Api.Features.Profiles;
using Tally.Api.Features.Recipes;
using Tally.Api.Features.Restaurants;
using Tally.Api.Infrastructure;
using Tally.Api.OpenFoodFacts;

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
builder.Services.AddScoped<MealPlans>();
builder.Services.AddScoped<DiaryService>();
builder.Services.AddScoped<FoodImport>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddSingleton<Clock>();
builder.Services.AddScoped<OffFoodService>();
builder.Services.AddSingleton<OffRateGate>();
builder.Services.AddSingleton<OffRefreshQueue>();
builder.Services.AddHostedService<OffRefreshWorker>();
builder.Services.AddHttpClient<OffClient>((sp, http) =>
    http.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<TallyOptions>>().Value.OpenFoodFacts.TimeoutSeconds));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();
// Imports can be a few hundred KB of CSV/JSON.
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
    // Runs once per HTTPS endpoint as it is bound (ASPNETCORE_HTTPS_PORTS), and not at all without one.
    k.ConfigureHttpsDefaults(https =>
        (https.ServerCertificate, https.ServerCertificateChain) = k.ApplicationServices.GetRequiredService<TlsCertificates>().Get());
});
builder.Services.AddSingleton<TlsCertificates>();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Optional: a reverse proxy on a private network may terminate TLS instead; trust what it forwards.
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

var staticFiles = StaticCaching.Options();
app.UseDefaultFiles();
app.UseStaticFiles(staticFiles);

app.MapHealthChecks("/healthz", new HealthCheckOptions { ResponseWriter = HealthResponse.WriteAsync });

var api = app.MapGroup("/api");
api.MapGet("/config", (IOptions<TallyOptions> o, Clock clock) =>
    new
    {
        version = AppInfo.Version,
        timeZone = o.Value.TimeZone,
        today = clock.Today,
        onlineEnabled = o.Value.OpenFoodFacts.Enabled,
        // Lets a page loaded over plain HTTP point at the HTTPS port, which the camera needs.
        httpsPort = HttpsPort(app.Configuration),
    });
api.MapProfileEndpoints();

// Everything below acts on behalf of a profile.
var scoped = api.MapGroup("").RequireProfile();
scoped.MapGet("/me", async (ProfileContext profile, TallyDbContext db) =>
    await db.Profiles.Where(p => p.Id == profile.Id).Select(p => new ProfileDto(p.Id, p.Name, p.Avatar, p.Colour)).FirstAsync());
scoped.MapMealEndpoints();
scoped.MapDiaryEndpoints();
scoped.MapFoodEndpoints();
scoped.MapCustomFoodEndpoints();
scoped.MapRestaurantEndpoints();
scoped.MapImportEndpoints();
scoped.MapRecipeEndpoints();
scoped.MapSavedMealEndpoints();
scoped.MapGoalEndpoints();
scoped.MapHistoryEndpoints();
// Unknown /api routes must 404 rather than fall through to the SPA.
api.MapFallback(() => Results.NotFound());

app.MapFallbackToFile("index.html", staticFiles);

app.Run();
return 0;

static int? HttpsPort(IConfiguration config) =>
    int.TryParse((config["HTTPS_PORTS"] ?? "").Split(';', ',')[0], out var port) ? port : null;

public partial class Program;
