using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tally.Api.Data;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Foods;

/// <summary>
/// Loads the converted CoFID 2021 CSV (McCance and Widdowson's, Open Government
/// Licence v3.0) into the Food table. Idempotent: upserts by CoFID food code.
/// </summary>
public sealed class CofidImporter(TallyDbContext db, IOptions<TallyOptions> options, ILogger<CofidImporter> log)
{
    public async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        if (!options.Value.SeedCofid) return;
        if (await db.Foods.AnyAsync(f => f.Source == FoodSource.Cofid, ct)) return;
        await ImportAsync(ct);
    }

    public async Task<int> ImportAsync(CancellationToken ct)
    {
        var path = ResolvePath(options.Value.CofidPath);
        if (!File.Exists(path))
        {
            log.LogWarning("CoFID file not found at {Path}; generic foods will be unavailable", path);
            return 0;
        }

        var rows = Csv.Parse(await File.ReadAllTextAsync(path, ct));
        var header = rows[0].Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        var existing = await db.Foods.Where(f => f.Source == FoodSource.Cofid)
            .ToDictionaryAsync(f => f.ExternalId!, ct);

        var now = DateTimeOffset.UtcNow;
        var count = 0;
        foreach (var row in rows.Skip(1))
        {
            string Col(string name) => row[header[name]].Trim();
            double? Num(string name) =>
                double.TryParse(Col(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

            var code = Col("code");
            if (code.Length == 0) continue;

            if (!existing.TryGetValue(code, out var food))
            {
                food = new Food { Source = FoodSource.Cofid, ExternalId = code, Name = Col("name") };
                db.Foods.Add(food);
            }

            food.Name = Col("name");
            food.Basis = NutritionBasis.Per100g;
            food.IsUk = true;
            food.Countries = ["en:united-kingdom"];
            food.UpdatedAt = now;
            food.Nutrients = new Nutrients
            {
                EnergyKcal = Num("energy_kcal"),
                EnergyKj = Num("energy_kj"),
                Fat = Num("fat"),
                Saturates = Num("saturates"),
                Carbohydrate = Num("carbohydrate"),
                Sugars = Num("sugars"),
                Fibre = Num("fibre"),
                Protein = Num("protein"),
                Salt = Num("salt"),
            }.WithDerivedEnergy();
            count++;
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Imported {Count} CoFID foods from {Path}", count, path);
        return count;
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
