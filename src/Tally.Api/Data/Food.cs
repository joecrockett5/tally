using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace Tally.Api.Data;

[JsonConverter(typeof(JsonStringEnumConverter<FoodSource>))]
public enum FoodSource
{
    Custom,
    Cofid,
    OpenFoodFacts,
}

/// <summary>What the stored nutrient values refer to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NutritionBasis>))]
public enum NutritionBasis
{
    Per100g,
    Per100ml,
    /// <summary>Known only per portion (typical for restaurant items); no weight is invented.</summary>
    PerPortion,
}

public sealed class Food
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public FoodSource Source { get; set; }

    /// <summary>Creator of a custom food; null for CoFID/OFF, or once the creator's profile is deleted.</summary>
    public Guid? OwnerProfileId { get; set; }
    public string? CreatedByName { get; set; }
    public bool IsShared { get; set; }

    public required string Name { get; set; }
    public string? Brand { get; set; }
    public Guid? RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
    public string? Barcode { get; set; }

    /// <summary>CoFID food code or OFF product code.</summary>
    public string? ExternalId { get; set; }

    public NutritionBasis Basis { get; set; }
    /// <summary>Label of one portion for <see cref="NutritionBasis.PerPortion"/> foods, e.g. "1 burger".</summary>
    public string? PortionName { get; set; }
    public Nutrients Nutrients { get; set; } = Nutrients.Empty;

    /// <summary>Set when the source appears to use US-style total carbohydrate (includes fibre).</summary>
    public bool CarbsMayIncludeFibre { get; set; }
    public string[] Countries { get; set; } = [];
    public bool IsUk { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>When an OFF product was last fetched.</summary>
    public DateTimeOffset? FetchedAt { get; set; }
    public Guid? CopiedFromFoodId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }

    public List<FoodServing> Servings { get; set; } = [];

    public NpgsqlTsVector SearchVector { get; set; } = null!;

    public bool IsPortionBased => Basis == NutritionBasis.PerPortion;
}

public sealed class FoodServing
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FoodId { get; set; }

    /// <summary>e.g. "1 slice", "1 can".</summary>
    public required string Name { get; set; }

    /// <summary>Size in the food's basis units: grams, millilitres or portions.</summary>
    public double Amount { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Restaurant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public static string Normalize(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Replace('’', '\'').Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>Negative cache for barcodes Open Food Facts doesn't know.</summary>
public sealed class OffBarcodeMiss
{
    public required string Barcode { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}

/// <summary>Cache of OFF text-search results: query → product codes in result order.</summary>
public sealed class OffSearchCache
{
    public required string QueryKey { get; set; }
    public string[] Codes { get; set; } = [];
    public DateTimeOffset FetchedAt { get; set; }
}
