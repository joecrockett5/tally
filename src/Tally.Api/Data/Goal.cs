namespace Tally.Api.Data;

/// <summary>A profile's daily targets. Null means "no target" for that nutrient.</summary>
public sealed class Goal
{
    public Guid ProfileId { get; set; }
    public double? EnergyKcal { get; set; }
    public double? Fat { get; set; }
    public double? Saturates { get; set; }
    public double? Carbohydrate { get; set; }
    public double? Sugars { get; set; }
    public double? Fibre { get; set; }
    public double? Protein { get; set; }
    public double? Salt { get; set; }

    /// <summary>UK Reference Intakes for an adult, plus the 30 g fibre recommendation.</summary>
    public static Goal UkDefaults(Guid profileId) => new()
    {
        ProfileId = profileId,
        EnergyKcal = 2000,
        Fat = 70,
        Saturates = 20,
        Carbohydrate = 260,
        Sugars = 90,
        Fibre = 30,
        Protein = 50,
        Salt = 6,
    };
}
