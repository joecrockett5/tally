using Tally.Api.Data;

namespace Tally.Api.Features.Foods;

public sealed record ServingDto(Guid Id, string Name, double Amount);

/// <summary>Source labels shown in the UI.</summary>
public static class FoodLabels
{
    public const string Mine = "mine";
    public const string Shared = "shared";
    public const string Restaurant = "restaurant";
    public const string Cofid = "cofid";
    public const string OpenFoodFacts = "off";

    public static string For(Food f, Guid profileId) => f.Source switch
    {
        FoodSource.Cofid => Cofid,
        FoodSource.OpenFoodFacts => OpenFoodFacts,
        _ when f.RestaurantId is not null => Restaurant,
        _ when f.OwnerProfileId == profileId => Mine,
        _ => Shared,
    };
}

public sealed record FoodDto(
    Guid Id,
    string Name,
    string? Brand,
    string Source,
    Guid? RestaurantId,
    string? Restaurant,
    string? Barcode,
    NutritionBasis Basis,
    string? PortionName,
    Nutrients Nutrients,
    IReadOnlyList<ServingDto> Servings,
    bool IsUk,
    bool CarbsMayIncludeFibre,
    string? ImageUrl,
    bool IsShared,
    bool CanEdit,
    string? CreatedByName,
    DateTimeOffset? FetchedAt)
{
    public static FoodDto From(Food f, Guid profileId) => new(
        f.Id,
        f.Name,
        f.Brand,
        FoodLabels.For(f, profileId),
        f.RestaurantId,
        f.Restaurant?.Name,
        f.Barcode,
        f.Basis,
        f.PortionName,
        f.Nutrients,
        f.Servings.OrderBy(s => s.SortOrder).Select(s => new ServingDto(s.Id, s.Name, s.Amount)).ToList(),
        f.IsUk,
        f.CarbsMayIncludeFibre,
        f.ImageUrl,
        f.IsShared,
        f.Source == FoodSource.Custom && f.OwnerProfileId == profileId,
        f.CreatedByName,
        f.FetchedAt);
}

public static class FoodQueries
{
    /// <summary>Foods a profile may see: generic + OFF data, its own custom foods, and shared ones.</summary>
    public static IQueryable<Food> VisibleTo(this IQueryable<Food> foods, Guid profileId) =>
        foods.Where(f => f.DeletedAt == null &&
                         (f.Source != FoodSource.Custom || f.OwnerProfileId == profileId || f.IsShared));
}
