using Tally.Api.Data;
using Tally.Api.Features.Diary;
using Tally.Api.Features.Foods;

namespace Tally.Api.Features.Recipes;

public sealed record IngredientInput(Guid FoodId, QuantityUnit? Unit, double Quantity, Guid? ServingId);

public sealed record RecipeInput(string Name, double Servings, double? CookedWeightGrams, IReadOnlyList<IngredientInput> Ingredients);

public sealed record IngredientDto(Guid Id, FoodDto Food, QuantityUnit Unit, double Quantity, string? ServingName, double? ServingAmount, double Amount, Nutrients Nutrients);

public sealed record RecipeDto(
    Guid Id,
    string Name,
    double Servings,
    double? CookedWeightGrams,
    IReadOnlyList<IngredientDto> Ingredients,
    NutrientTotals Totals,
    Nutrients PerServing,
    Nutrients? Per100g);

public sealed record RecipeSummaryDto(Guid Id, string Name, double Servings, double? CookedWeightGrams, int IngredientCount, Nutrients PerServing);

public sealed record SavedMealItemInput(Guid? FoodId, Guid? RecipeId, QuantityUnit? Unit, double Quantity, Guid? ServingId);

public sealed record SavedMealInput(string Name, IReadOnlyList<SavedMealItemInput> Items);

public sealed record SavedMealItemDto(Guid Id, Guid? FoodId, Guid? RecipeId, string Name, string Source, QuantityUnit Unit, double Quantity, string? ServingName, double? ServingAmount, string? PortionName, NutritionBasis Basis, Nutrients Nutrients);

public sealed record SavedMealDto(Guid Id, string Name, IReadOnlyList<SavedMealItemDto> Items, NutrientTotals Totals);

public sealed record SaveFromDiaryRequest(string Name, string Date, Meal Meal);

public sealed record LogSavedMealRequest(string Date, Meal Meal);
