using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Goals;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.History;

public sealed record HistoryDay(DateOnly Date, int EntryCount, Nutrients Totals, IReadOnlyList<string> Incomplete, bool? OnTarget);

/// <summary>Energy from each macro, using UK label factors (protein 4, carbohydrate 4, fat 9 kcal/g).</summary>
public sealed record MacroSplit(double ProteinKcal, double CarbohydrateKcal, double FatKcal, double ProteinPct, double CarbohydratePct, double FatPct);

public sealed record HistoryDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<HistoryDay> Days,
    int LoggedDays,
    Nutrients Averages,
    MacroSplit? Macros,
    int? DaysOnTarget,
    GoalsDto Goals);

public static class HistoryEndpoints
{
    private const int MaxDays = 366;

    public static void MapHistoryEndpoints(this RouteGroupBuilder scoped)
    {
        scoped.MapGet("/history", async (string? from, string? to, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            if (ParseRange(from, to) is not { } range) return BadRange();
            var goals = GoalEndpoints.ToDto(await GoalEndpoints.GetOrCreateAsync(db, profile.Id, ct));
            var entries = await LoadAsync(db, profile.Id, range.From, range.To, ct);
            return Results.Ok(Build(range.From, range.To, entries, goals));
        });

        scoped.MapGet("/export/diary.csv", async (string? from, string? to, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
        {
            if (ParseRange(from, to) is not { } range) return BadRange();
            var entries = await LoadAsync(db, profile.Id, range.From, range.To, ct);
            var csv = ToCsv(entries);
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(),
                "text/csv; charset=utf-8", $"tally-diary-{range.From:yyyy-MM-dd}-to-{range.To:yyyy-MM-dd}.csv");
        });
    }

    private static Task<List<DiaryEntry>> LoadAsync(TallyDbContext db, Guid profileId, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.DiaryEntries.AsNoTracking()
            .Where(e => e.ProfileId == profileId && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date).ThenBy(e => e.Meal).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .ToListAsync(ct);

    public static HistoryDto Build(DateOnly from, DateOnly to, IReadOnlyList<DiaryEntry> entries, GoalsDto goals)
    {
        var byDay = entries.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.ToList());
        var days = new List<HistoryDay>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!byDay.TryGetValue(d, out var list))
            {
                days.Add(new HistoryDay(d, 0, Nutrients.Empty, [], null));
                continue;
            }
            var totals = NutrientTotals.Sum(list.Select(e => e.Totals()).ToList());
            days.Add(new HistoryDay(d, list.Count, totals.Values, totals.Incomplete, OnTarget(totals.Values, goals)));
        }

        // Only days with at least one entry count towards averages.
        var logged = days.Where(d => d.EntryCount > 0).ToList();
        double? Avg(Func<Nutrients, double?> pick) =>
            logged.Count == 0 || logged.All(d => pick(d.Totals) is null) ? null : logged.Average(d => pick(d.Totals) ?? 0);
        var averages = new Nutrients
        {
            EnergyKcal = Avg(n => n.EnergyKcal),
            EnergyKj = Avg(n => n.EnergyKj),
            Fat = Avg(n => n.Fat),
            Saturates = Avg(n => n.Saturates),
            Carbohydrate = Avg(n => n.Carbohydrate),
            Sugars = Avg(n => n.Sugars),
            Fibre = Avg(n => n.Fibre),
            Protein = Avg(n => n.Protein),
            Salt = Avg(n => n.Salt),
        };

        return new HistoryDto(from, to, days, logged.Count, averages, Macros(logged),
            goals.EnergyKcal is null ? null : logged.Count(d => d.OnTarget == true), goals);
    }

    /// <summary>Energy within ±10% of target and no upper limit (sugars, saturates, salt) exceeded.</summary>
    public static bool? OnTarget(Nutrients totals, GoalsDto goals)
    {
        if (goals.EnergyKcal is not { } target || totals.EnergyKcal is not { } kcal) return null;
        var energyOk = Math.Abs(kcal - target) <= target * 0.1;
        var limitsOk = !(Exceeds(totals.Sugars, goals.Sugars) || Exceeds(totals.Saturates, goals.Saturates) || Exceeds(totals.Salt, goals.Salt));
        return energyOk && limitsOk;
    }

    private static bool Exceeds(double? value, double? limit) => value is { } v && limit is { } l && v > l;

    private static MacroSplit? Macros(IReadOnlyList<HistoryDay> logged)
    {
        var protein = logged.Sum(d => d.Totals.Protein ?? 0) * 4;
        var carbs = logged.Sum(d => d.Totals.Carbohydrate ?? 0) * 4;
        var fat = logged.Sum(d => d.Totals.Fat ?? 0) * 9;
        var total = protein + carbs + fat;
        if (total <= 0) return null;
        var n = logged.Count;
        return new MacroSplit(protein / n, carbs / n, fat / n, protein / total * 100, carbs / total * 100, fat / total * 100);
    }

    public static string ToCsv(IEnumerable<DiaryEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Csv.Line([
            "date", "meal", "food", "brand", "source", "quantity", "unit", "serving", "amount", "amount_unit",
            "energy_kcal", "energy_kj", "fat_g", "saturates_g", "carbohydrate_g", "sugars_g", "fibre_g", "protein_g", "salt_g",
        ]));
        foreach (var e in entries)
        {
            var n = e.Totals();
            sb.AppendLine(Csv.Line([
                e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.Meal.ToString(),
                e.Name,
                e.Brand,
                SourceName(e.SourceLabel),
                Num(e.Quantity),
                e.Unit switch { QuantityUnit.Gram => "g", QuantityUnit.Millilitre => "ml", QuantityUnit.Portion => "portion", _ => "serving" },
                e.ServingName,
                Num(e.Amount),
                e.Basis switch { NutritionBasis.Per100g => "g", NutritionBasis.Per100ml => "ml", _ => e.PortionName ?? "portion" },
                Num(n.EnergyKcal), Num(n.EnergyKj), Num(n.Fat), Num(n.Saturates), Num(n.Carbohydrate),
                Num(n.Sugars), Num(n.Fibre), Num(n.Protein), Num(n.Salt),
            ]));
        }
        return sb.ToString();
    }

    private static string SourceName(string label) => label switch
    {
        "mine" => "Mine",
        "shared" => "Shared",
        "restaurant" => "Restaurant",
        "cofid" => "Generic (CoFID)",
        "off" => "Open Food Facts",
        "recipe" => "Recipe",
        _ => label,
    };

    /// <summary>Unknown stays blank (not 0); up to 3 decimal places.</summary>
    private static string Num(double? v) => v is { } x ? x.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private static (DateOnly From, DateOnly To)? ParseRange(string? from, string? to) =>
        Clock.TryParseDate(from, out var f) && Clock.TryParseDate(to, out var t) && f <= t && t.DayNumber - f.DayNumber < MaxDays
            ? (f, t)
            : null;

    private static IResult BadRange() =>
        Results.Problem($"Give from and to as YYYY-MM-DD, at most {MaxDays} days apart.", statusCode: 400, title: "invalid_range");
}
