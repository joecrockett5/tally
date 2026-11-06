namespace Tally.Api;

public static class AppInfo
{
    public const string Name = "Tally";

    private const string DevVersion = "dev";

    /// <summary>The release version, stamped into the image as APP_VERSION at build time.</summary>
    public static string Version { get; } = ResolveVersion(Environment.GetEnvironmentVariable("APP_VERSION"));

    public static string ResolveVersion(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DevVersion : value.Trim();
}
