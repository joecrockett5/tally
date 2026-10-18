using System.Text.Json;
using Tally.Api.Data;

namespace Tally.Api.Features.Diary;

/// <summary>
/// Sum of several items' nutrients. A nutrient is listed in <see cref="Incomplete"/>
/// when at least one item didn't know it, so the UI can say the total is a minimum.
/// </summary>
public sealed record NutrientTotals(Nutrients Values, IReadOnlyList<string> Incomplete)
{
    private static readonly JsonNamingPolicy Naming = JsonNamingPolicy.CamelCase;

    public static NutrientTotals Sum(IReadOnlyCollection<Nutrients> items)
    {
        if (items.Count == 0)
        {
            return new(new Nutrients
            {
                EnergyKcal = 0, EnergyKj = 0, Fat = 0, Saturates = 0, Carbohydrate = 0,
                Sugars = 0, Fibre = 0, Protein = 0, Salt = 0,
            }, []);
        }

        double? Total(Func<Nutrients, double?> pick) =>
            items.Any(i => pick(i) is not null) ? items.Sum(i => pick(i) ?? 0) : null;

        var values = new Nutrients
        {
            EnergyKcal = Total(n => n.EnergyKcal),
            EnergyKj = Total(n => n.EnergyKj),
            Fat = Total(n => n.Fat),
            Saturates = Total(n => n.Saturates),
            Carbohydrate = Total(n => n.Carbohydrate),
            Sugars = Total(n => n.Sugars),
            Fibre = Total(n => n.Fibre),
            Protein = Total(n => n.Protein),
            Salt = Total(n => n.Salt),
        };

        var incomplete = Nutrients.Empty.All().Select(x => x.Key)
            .Where(key => items.Any(i => i.All().First(x => x.Key == key).Value is null))
            .Select(Naming.ConvertName)
            .ToList();
        return new(values, incomplete);
    }
}
