using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tally.Api.Data;

namespace Tally.Api.Infrastructure;

public sealed class DatabaseHealthCheck(TallyDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot reach the database");
}
