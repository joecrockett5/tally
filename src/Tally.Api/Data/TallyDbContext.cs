using Microsoft.EntityFrameworkCore;

namespace Tally.Api.Data;

public sealed class TallyDbContext(DbContextOptions<TallyDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");
    }
}
