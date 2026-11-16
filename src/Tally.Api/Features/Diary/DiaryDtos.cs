using Tally.Api.Data;
using Tally.Api.Features.Foods;

namespace Tally.Api.Features.Diary;

public sealed record EntryDto(
    Guid Id,
    DateOnly Date,
    Guid MealId,
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
        e.Id, e.Date, e.MealId, e.FoodId, e.RecipeId, e.Name, e.Brand, e.SourceLabel, e.Basis, e.PortionName,
        e.Unit, e.Quantity, e.ServingName, e.ServingAmount, e.Amount, e.PerBasis, e.Totals());
}

/// <param name="Active">False for a meal that has since been removed but still holds entries on this day.</param>
public sealed record MealDto(Guid Id, string Name, bool Active, IReadOnlyList<EntryDto> Entries, NutrientTotals Totals);

public sealed record DayDto(DateOnly Date, IReadOnlyList<MealDto> Meals, NutrientTotals Totals, GoalsDto? Goals);

/// <summary>Daily targets; null means "no target". Filled in by the goals feature.</summary>
public sealed record GoalsDto(
    double? EnergyKcal, double? Fat, double? Saturates, double? Carbohydrate,
    double? Sugars, double? Fibre, double? Protein, double? Salt);

public sealed record LogEntryRequest(
    string Date, Guid MealId, Guid? FoodId, Guid? RecipeId, QuantityUnit? Unit, double Quantity, Guid? ServingId);

public sealed record UpdateEntryRequest(Guid? MealId, string? Date, QuantityUnit? Unit, double? Quantity);

public sealed record CopyRequest(string FromDate, string ToDate, Guid? FromMealId, Guid? ToMealId);

public sealed record QuickPickDto(FoodDto Food, QuantityUnit Unit, double Quantity, Guid? ServingId, int TimesLogged);
