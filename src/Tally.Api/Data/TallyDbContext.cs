using Microsoft.EntityFrameworkCore;

namespace Tally.Api.Data;

public sealed class TallyDbContext(DbContextOptions<TallyDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<FoodServing> FoodServings => Set<FoodServing>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<OffBarcodeMiss> OffBarcodeMisses => Set<OffBarcodeMiss>();
    public DbSet<OffSearchCache> OffSearchCache => Set<OffSearchCache>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<SavedMeal> SavedMeals => Set<SavedMeal>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<Enum>().HaveConversion<string>().HaveMaxLength(24);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");

        b.Entity<Profile>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(40);
            e.Property(p => p.Avatar).HasMaxLength(16);
            e.Property(p => p.Colour).HasMaxLength(16);
        });

        b.Entity<Food>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(300);
            e.Property(f => f.Brand).HasMaxLength(200);
            e.Property(f => f.RestaurantName).HasMaxLength(100);
            e.Property(f => f.Barcode).HasMaxLength(32);
            e.Property(f => f.ExternalId).HasMaxLength(32);
            e.Property(f => f.PortionName).HasMaxLength(100);
            e.Property(f => f.CreatedByName).HasMaxLength(40);
            e.Property(f => f.ImageUrl).HasMaxLength(500);
            e.ComplexProperty(f => f.Nutrients);

            e.HasOne<Profile>().WithMany().HasForeignKey(f => f.OwnerProfileId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.Restaurant).WithMany().HasForeignKey(f => f.RestaurantId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(f => f.Servings).WithOne().HasForeignKey(s => s.FoodId).OnDelete(DeleteBehavior.Cascade);

            e.HasGeneratedTsVectorColumn(f => f.SearchVector, "english", f => new { f.Name, f.Brand, f.RestaurantName })
                .HasIndex(f => f.SearchVector).HasMethod("GIN");
            e.HasIndex(f => f.Name).HasMethod("GIN").HasOperators("gin_trgm_ops");
            e.HasIndex(f => new { f.Source, f.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
            e.HasIndex(f => f.Barcode);
            e.HasIndex(f => f.OwnerProfileId);
        });

        b.Entity<FoodServing>(e => e.Property(s => s.Name).HasMaxLength(100));

        b.Entity<Restaurant>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.NormalizedName).HasMaxLength(100);
            e.HasIndex(r => r.NormalizedName).IsUnique();
        });

        b.Entity<OffBarcodeMiss>(e =>
        {
            e.HasKey(m => m.Barcode);
            e.Property(m => m.Barcode).HasMaxLength(32);
        });

        b.Entity<DiaryEntry>(e =>
        {
            e.Property(d => d.Name).HasMaxLength(300);
            e.Property(d => d.Brand).HasMaxLength(200);
            e.Property(d => d.SourceLabel).HasMaxLength(24);
            e.Property(d => d.PortionName).HasMaxLength(100);
            e.Property(d => d.ServingName).HasMaxLength(100);
            e.ComplexProperty(d => d.PerBasis);
            e.HasOne<Profile>().WithMany().HasForeignKey(d => d.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Food>().WithMany().HasForeignKey(d => d.FoodId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Recipe>().WithMany().HasForeignKey(d => d.RecipeId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(d => new { d.ProfileId, d.Date });
            e.HasIndex(d => new { d.ProfileId, d.CreatedAt });
        });

        b.Entity<Recipe>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(200);
            e.HasOne<Profile>().WithMany().HasForeignKey(r => r.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Ingredients).WithOne().HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.ProfileId);
        });

        b.Entity<RecipeIngredient>(e =>
        {
            e.Property(i => i.ServingName).HasMaxLength(100);
            e.HasOne(i => i.Food).WithMany().HasForeignKey(i => i.FoodId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SavedMeal>(e =>
        {
            e.Property(m => m.Name).HasMaxLength(200);
            e.HasOne<Profile>().WithMany().HasForeignKey(m => m.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(m => m.Items).WithOne().HasForeignKey(i => i.SavedMealId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.ProfileId);
        });

        b.Entity<SavedMealItem>(e =>
        {
            e.Property(i => i.ServingName).HasMaxLength(100);
            e.HasOne(i => i.Food).WithMany().HasForeignKey(i => i.FoodId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Recipe).WithMany().HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OffSearchCache>(e =>
        {
            e.HasKey(c => c.QueryKey);
            e.Property(c => c.QueryKey).HasMaxLength(200);
        });
    }
}
