using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tally.Api.Infrastructure;

/// <summary>The /healthz body: {"ok":true,"version":"1.0.0"}. The status code still follows the health status.</summary>
public static class HealthResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new { ok = report.Status != HealthStatus.Unhealthy, version = AppInfo.Version });
}
