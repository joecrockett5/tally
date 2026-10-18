namespace Tally.Api;

public sealed class TallyOptions
{
    public const string Section = "Tally";

    /// <summary>IANA timezone used to decide which calendar day "today" is.</summary>
    public string TimeZone { get; set; } = "Europe/London";

    /// <summary>Seed CoFID on startup when the generic food table is empty.</summary>
    public bool SeedCofid { get; set; } = true;

    /// <summary>Path to the converted CoFID CSV (relative to the content root or absolute).</summary>
    public string CofidPath { get; set; } = "data/cofid/cofid-2021.csv";

    public OpenFoodFactsOptions OpenFoodFacts { get; set; } = new();
}

public sealed class OpenFoodFactsOptions
{
    public bool Enabled { get; set; } = true;
    public string ProductBaseUrl { get; set; } = "https://world.openfoodfacts.org";
    public string SearchBaseUrl { get; set; } = "https://search.openfoodfacts.org";
    public string ContactEmail { get; set; } = "";
    public int ProductCacheDays { get; set; } = 30;
    public int NotFoundCacheHours { get; set; } = 24;
    public int SearchCacheHours { get; set; } = 24;
    public int ProductRequestsPerMinute { get; set; } = 15;
    public int SearchRequestsPerMinute { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 8;
}
