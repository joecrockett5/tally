using Microsoft.EntityFrameworkCore;

namespace Tally.Api.Data;

public sealed class TallyDbContext(DbContextOptions<TallyDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");

        b.Entity<Profile>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(40);
            e.Property(p => p.Avatar).HasMaxLength(16);
            e.Property(p => p.Colour).HasMaxLength(16);
        });
    }
}
