using System.Reflection;

namespace Tally.Api;

public static class AppInfo
{
    public const string Name = "Tally";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
