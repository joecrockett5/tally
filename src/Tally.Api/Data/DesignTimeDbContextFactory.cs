using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tally.Api.Data;

/// <summary>Lets `dotnet ef` build the model without starting the web host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TallyDbContext>
{
    public TallyDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TallyDbContext>()
            .UseNpgsql("Host=localhost;Database=tally;Username=tally;Password=tally")
            .Options);
}
