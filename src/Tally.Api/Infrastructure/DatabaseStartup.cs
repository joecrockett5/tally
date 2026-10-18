using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.Infrastructure;

public static class DatabaseStartup
{
    /// <summary>Applies pending migrations, retrying while Postgres finishes starting.</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("Tally.Database");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TallyDbContext>();
                await db.Database.MigrateAsync(ct);
                log.LogInformation("Database migrations applied");
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
            {
                log.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }
    }
}
