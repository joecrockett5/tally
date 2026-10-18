namespace Tally.Api.Infrastructure;

/// <summary>
/// `dotnet Tally.Api.dll healthcheck` — used by the Docker HEALTHCHECK so the
/// runtime image needs no curl/wget.
/// </summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0];
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var response = await http.GetAsync($"http://127.0.0.1:{port}/healthz");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch
        {
            return 1;
        }
    }
}
