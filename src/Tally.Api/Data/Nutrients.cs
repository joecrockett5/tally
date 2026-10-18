namespace Tally.Api.Data;

/// <summary>
/// The UK back-of-pack nutrient set. Every value is nullable: null means
/// "unknown", which is different from zero.
/// </summary>
public sealed record Nutrients
{
    public const double KjPerKcal = 4.184;
    public const double SaltPerSodium = 2.5;

    public double? EnergyKcal { get; init; }
    public double? EnergyKj { get; init; }
    public double? Fat { get; init; }
    public double? Saturates { get; init; }
    public double? Carbohydrate { get; init; }
    public double? Sugars { get; init; }
    public double? Fibre { get; init; }
    public double? Protein { get; init; }
    public double? Salt { get; init; }

    public static readonly Nutrients Empty = new();

    /// <summary>Fills in kcal ↔ kJ when only one is known.</summary>
    public Nutrients WithDerivedEnergy() => this switch
    {
        { EnergyKcal: { } kcal, EnergyKj: null } => this with { EnergyKj = kcal * KjPerKcal },
        { EnergyKj: { } kj, EnergyKcal: null } => this with { EnergyKcal = kj / KjPerKcal },
        _ => this,
    };

    public static double? SaltFromSodium(double? sodium) => sodium * SaltPerSodium;

    public Nutrients Scale(double factor) => new()
    {
        EnergyKcal = EnergyKcal * factor,
        EnergyKj = EnergyKj * factor,
        Fat = Fat * factor,
        Saturates = Saturates * factor,
        Carbohydrate = Carbohydrate * factor,
        Sugars = Sugars * factor,
        Fibre = Fibre * factor,
        Protein = Protein * factor,
        Salt = Salt * factor,
    };

    public IEnumerable<(string Key, double? Value)> All()
    {
        yield return (nameof(EnergyKcal), EnergyKcal);
        yield return (nameof(EnergyKj), EnergyKj);
        yield return (nameof(Fat), Fat);
        yield return (nameof(Saturates), Saturates);
        yield return (nameof(Carbohydrate), Carbohydrate);
        yield return (nameof(Sugars), Sugars);
        yield return (nameof(Fibre), Fibre);
        yield return (nameof(Protein), Protein);
        yield return (nameof(Salt), Salt);
    }

    public bool HasNegative() => All().Any(x => x.Value < 0);
}
