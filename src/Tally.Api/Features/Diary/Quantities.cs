using Tally.Api.Data;

namespace Tally.Api.Features.Diary;

/// <summary>A quantity resolved against a food's basis.</summary>
public sealed record ResolvedQuantity(QuantityUnit Unit, double Quantity, string? ServingName, double? ServingAmount, double Amount);

public static class Quantities
{
    public const double Max = 100_000;

    /// <summary>
    /// Turns "unit × quantity" into an amount in the food's basis units.
    /// Weight/volume units need a per-100 food; portions need a portion food.
    /// </summary>
    public static ResolvedQuantity? Resolve(
        NutritionBasis basis, QuantityUnit unit, double quantity, string? servingName, double? servingAmount, out string? error)
    {
        error = null;
        if (!double.IsFinite(quantity) || quantity <= 0 || quantity > Max)
        {
            error = "Quantity must be more than zero.";
            return null;
        }

        switch (unit)
        {
            case QuantityUnit.Gram or QuantityUnit.Millilitre when basis == NutritionBasis.PerPortion:
                error = "This food is only known per portion, so log it in portions.";
                return null;
            case QuantityUnit.Gram or QuantityUnit.Millilitre:
                return new(unit, quantity, null, null, quantity);
            case QuantityUnit.Portion when basis != NutritionBasis.PerPortion:
                error = "Log this food by weight or by serving.";
                return null;
            case QuantityUnit.Portion:
                return new(unit, quantity, null, null, quantity);
            case QuantityUnit.Serving when servingAmount is > 0 && !string.IsNullOrWhiteSpace(servingName):
                return new(unit, quantity, servingName, servingAmount, servingAmount.Value * quantity);
            default:
                error = "Choose a serving.";
                return null;
        }
    }

    public static QuantityUnit DefaultUnit(NutritionBasis basis) => basis switch
    {
        NutritionBasis.PerPortion => QuantityUnit.Portion,
        NutritionBasis.Per100ml => QuantityUnit.Millilitre,
        _ => QuantityUnit.Gram,
    };
}
