namespace Tally.Api.Data;

/// <summary>A profile's recipe. Nutrition is computed from the ingredients' current values.</summary>
public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProfileId { get; set; }
    public required string Name { get; set; }
    public double Servings { get; set; } = 1;
    /// <summary>Total cooked weight, so the recipe can also be logged by grams.</summary>
    public double? CookedWeightGrams { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeId { get; set; }
    public Guid FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public QuantityUnit Unit { get; set; }
    public double Quantity { get; set; }
    public string? ServingName { get; set; }
    public double? ServingAmount { get; set; }
    /// <summary>Amount in the food's basis units.</summary>
    public double Amount { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A named group of foods/recipes logged together, e.g. "usual breakfast".</summary>
public sealed class SavedMeal
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProfileId { get; set; }
    public required string Name { get; set; }
    public List<SavedMealItem> Items { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SavedMealItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SavedMealId { get; set; }
    public Guid? FoodId { get; set; }
    public Food? Food { get; set; }
    public Guid? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public QuantityUnit Unit { get; set; }
    public double Quantity { get; set; }
    public string? ServingName { get; set; }
    public double? ServingAmount { get; set; }
    public double Amount { get; set; }
    public int SortOrder { get; set; }
}
