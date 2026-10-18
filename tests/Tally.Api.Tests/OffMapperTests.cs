using System.Text.Json;
using Tally.Api.Data;
using Tally.Api.OpenFoodFacts;

namespace Tally.Api.Tests;

public sealed class OffMapperTests
{
    private static Food Map(object product)
    {
        var food = new Food { Name = "" };
        OffProductMapper.Apply(JsonSerializer.SerializeToElement(product), food, DateTimeOffset.UtcNow);
        return food;
    }

    [Fact]
    public void Derives_kj_and_salt_when_only_kcal_and_sodium_known()
    {
        var food = Map(FakeOffHandler.Product("1", "Oat bar", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 400, ["sodium_100g"] = 0.4 }));

        Assert.Equal(400, food.Nutrients.EnergyKcal);
        Assert.Equal(400 * 4.184, food.Nutrients.EnergyKj!.Value, 6);
        Assert.Equal(1.0, food.Nutrients.Salt!.Value, 6);
        Assert.Null(food.Nutrients.Fat); // unknown stays unknown, not zero
        Assert.True(food.IsUk);
        Assert.Equal(NutritionBasis.Per100g, food.Basis);
    }

    [Fact]
    public void Drinks_are_per_100ml_with_can_serving()
    {
        var food = Map(FakeOffHandler.Product("2", "Cola", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_100g"] = 42, ["energy-kj_100g"] = 180 },
            quantity: "330 ml", servingQuantity: 330, servingSize: "1 can (330 ml)"));

        Assert.Equal(NutritionBasis.Per100ml, food.Basis);
        var serving = Assert.Single(food.Servings);
        Assert.Equal("1 can (330 ml)", serving.Name);
        Assert.Equal(330, serving.Amount);
    }

    [Fact]
    public void Per_serving_only_products_become_portion_foods()
    {
        var food = Map(FakeOffHandler.Product("3", "Wrap", ["en:united-kingdom"],
            new Dictionary<string, double> { ["energy-kcal_serving"] = 450, ["protein_serving"] = 20 },
            servingSize: "1 wrap"));

        Assert.Equal(NutritionBasis.PerPortion, food.Basis);
        Assert.Equal("1 wrap", food.PortionName);
        Assert.Equal(450, food.Nutrients.EnergyKcal);
        Assert.Empty(food.Servings);
    }

    [Fact]
    public void Us_only_products_are_flagged_for_total_carbohydrate()
    {
        var food = Map(FakeOffHandler.Product("4", "Cereal", ["en:united-states"],
            new Dictionary<string, double> { ["carbohydrates_100g"] = 80 }));
        Assert.True(food.CarbsMayIncludeFibre);
        Assert.False(food.IsUk);
    }

    [Theory]
    [InlineData("Nando's", "nando-s")]
    [InlineData("Marks & Spencer", "marks-spencer")]
    [InlineData("Greggs", "greggs")]
    public void Brand_slugs_match_off_tags(string brand, string slug) =>
        Assert.Equal(slug, OffFoodService.BrandSlug(brand));
}
