using System.Text.Json.Serialization;

namespace Tally.Api.Data;

[JsonConverter(typeof(JsonStringEnumConverter<Meal>))]
public enum Meal
{
    Breakfast,
    Lunch,
    Dinner,
    Snacks,
}

/// <summary>How the quantity was entered.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuantityUnit>))]
public enum QuantityUnit
{
    Gram,
    Millilitre,
    Portion,
    Serving,
}

/// <summary>
/// One logged item. Nutrition is a snapshot taken at logging time, so later edits
/// to the food (or refreshed OFF data) never rewrite history.
/// </summary>
public sealed class DiaryEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProfileId { get; set; }

    /// <summary>Local calendar day in the configured timezone.</summary>
    public DateOnly Date { get; set; }
    public Meal Meal { get; set; }

    public Guid? FoodId { get; set; }
    public Guid? RecipeId { get; set; }

    // ---- snapshot ----
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public required string SourceLabel { get; set; }
    public NutritionBasis Basis { get; set; }
    public string? PortionName { get; set; }
    /// <summary>Per 100 g/ml, or per portion, as the food was when logged.</summary>
    public Nutrients PerBasis { get; set; } = Nutrients.Empty;

    // ---- quantity ----
    public QuantityUnit Unit { get; set; }
    public double Quantity { get; set; }
    public string? ServingName { get; set; }
    /// <summary>Size of one serving in basis units, when Unit is Serving.</summary>
    public double? ServingAmount { get; set; }
    /// <summary>Total amount in basis units (g, ml or portions).</summary>
    public double Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Nutrients Totals() => Basis == NutritionBasis.PerPortion ? PerBasis.Scale(Amount) : PerBasis.Scale(Amount / 100);
}
