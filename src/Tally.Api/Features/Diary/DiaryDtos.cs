using Tally.Api.Data;
using Tally.Api.Features.Foods;

namespace Tally.Api.Features.Diary;

public sealed record EntryDto(
    Guid Id,
    DateOnly Date,
    Meal Meal,
    Guid? FoodId,
    Guid? RecipeId,
    string Name,
    string? Brand,
    string Source,
    NutritionBasis Basis,
    string? PortionName,
    QuantityUnit Unit,
    double Quantity,
    string? ServingName,
    double? ServingAmount,
    double Amount,
    Nutrients PerBasis,
    Nutrients Nutrients)
{
    public static EntryDto From(DiaryEntry e) => new(
        e.Id, e.Date, e.Meal, e.FoodId, e.RecipeId, e.Name, e.Brand, e.SourceLabel, e.Basis, e.PortionName,
        e.Unit, e.Quantity, e.ServingName, e.ServingAmount, e.Amount, e.PerBasis, e.Totals());
}

public sealed record MealDto(Meal Meal, IReadOnlyList<EntryDto> Entries, NutrientTotals Totals);

public sealed record DayDto(DateOnly Date, IReadOnlyList<MealDto> Meals, NutrientTotals Totals, GoalsDto? Goals);

/// <summary>Daily targets; null means "no target". Filled in by the goals feature.</summary>
public sealed record GoalsDto(
    double? EnergyKcal, double? Fat, double? Saturates, double? Carbohydrate,
    double? Sugars, double? Fibre, double? Protein, double? Salt);

public sealed record LogEntryRequest(
    string Date, Meal Meal, Guid? FoodId, Guid? RecipeId, QuantityUnit? Unit, double Quantity, Guid? ServingId);

public sealed record UpdateEntryRequest(Meal? Meal, string? Date, QuantityUnit? Unit, double? Quantity);

public sealed record CopyRequest(string FromDate, string ToDate, Meal? FromMeal, Meal? ToMeal);

public sealed record QuickPickDto(FoodDto Food, QuantityUnit Unit, double Quantity, Guid? ServingId, int TimesLogged);
