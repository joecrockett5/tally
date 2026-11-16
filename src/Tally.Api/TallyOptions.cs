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

    public HttpsOptions Https { get; set; } = new();
}

/// <summary>
/// The certificate for Tally's own HTTPS port. HTTPS is served on the ports in ASPNETCORE_HTTPS_PORTS;
/// with none set there is no HTTPS and these settings are unused.
/// </summary>
public sealed class HttpsOptions
{
    /// <summary>A PEM certificate (with any intermediates) to use instead of a self-signed one.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>The PEM private key for <see cref="CertificatePath"/>, unless it is in the same file.</summary>
    public string? KeyPath { get; set; }

    /// <summary>Where the self-signed certificate is kept. Unset, a new one is made on every start.</summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Addresses people browse to (comma-separated IPs or names), added to the self-signed certificate.
    /// Only needed to install it as trusted on a device; accepting the browser warning works without.
    /// </summary>
    public string? Hosts { get; set; }
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
