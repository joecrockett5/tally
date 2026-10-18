using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Tally.Api;
using Tally.Api.Data;
using Tally.Api.Infrastructure;

if (args.FirstOrDefault() == "healthcheck")
    return await HealthProbe.RunAsync();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TallyOptions>(builder.Configuration.GetSection(TallyOptions.Section));
builder.Services.AddDbContext<TallyDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Tally")
                ?? throw new InvalidOperationException("ConnectionStrings:Tally is not configured")));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();
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

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/healthz");

var api = app.MapGroup("/api");
api.MapGet("/config", () => new { version = AppInfo.Version });
// Unknown /api routes must 404 rather than fall through to the SPA.
api.MapFallback(() => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();
return 0;

public partial class Program;
