using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Tally.Api.OpenFoodFacts;

public enum OffStatus
{
    Ok,
    NotFound,
    Unavailable,
}

public sealed record OffResult<T>(OffStatus Status, T? Value = default, string? Message = null)
{
    public static OffResult<T> Unavailable(string message) => new(OffStatus.Unavailable, default, message);
}

/// <summary>
/// Thin HTTP client for Open Food Facts. Product lookups use API v3; text
/// search uses Search-a-licious (search.openfoodfacts.org), the recommended
/// full-text search service. Every request carries the required User-Agent.
/// </summary>
public sealed class OffClient(HttpClient http, IOptions<TallyOptions> options, OffRateGate gate, ILogger<OffClient> log)
{
    public const string Fields =
        "code,lang,product_name,product_name_en,generic_name,generic_name_en,brands,countries_tags,nutriments," +
        "nutrition_data_per,serving_size,serving_quantity,serving_quantity_unit,product_quantity," +
        "product_quantity_unit,quantity,image_front_small_url";

    private OpenFoodFactsOptions Options => options.Value.OpenFoodFacts;

    public static string UserAgent(string contactEmail) =>
        $"{AppInfo.Name}/{AppInfo.Version} ({(string.IsNullOrWhiteSpace(contactEmail) ? "self-hosted; contact not configured" : contactEmail.Trim())})";

    public async Task<OffResult<JsonElement>> GetProductAsync(string code, bool background, CancellationToken ct)
    {
        if (!Options.Enabled) return OffResult<JsonElement>.Unavailable("Online lookups are turned off on this server.");
        var acquired = background ? gate.TryAcquireProductNow() : await gate.TryAcquireProductAsync(ct);
        if (!acquired) return OffResult<JsonElement>.Unavailable("Open Food Facts is busy; try again shortly.");

        var url = $"{Options.ProductBaseUrl.TrimEnd('/')}/api/v3/product/{Uri.EscapeDataString(code)}?fields={Fields}";
        var (status, doc) = await SendAsync(url, ct);
        if (status == HttpStatusCode.NotFound) return new(OffStatus.NotFound);
        if (doc is null) return OffResult<JsonElement>.Unavailable("Open Food Facts is unavailable right now.");

        var root = doc.RootElement;
        if (root.TryGetProperty("product", out var product) && product.ValueKind == JsonValueKind.Object)
            return new(OffStatus.Ok, product.Clone());
        return new(OffStatus.NotFound);
    }

    public async Task<OffResult<List<JsonElement>>> SearchAsync(string query, int pageSize, CancellationToken ct)
    {
        if (!Options.Enabled) return OffResult<List<JsonElement>>.Unavailable("Online search is turned off on this server.");
        if (!gate.TryAcquireSearch()) return OffResult<List<JsonElement>>.Unavailable("Online search is rate limited; try again in a minute.");

        var url = $"{Options.SearchBaseUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(query)}" +
                  $"&page_size={pageSize}&langs=en&fields={Fields}";
        var (_, doc) = await SendAsync(url, ct);
        if (doc is null) return OffResult<List<JsonElement>>.Unavailable("Open Food Facts search is unavailable right now.");

        var hits = doc.RootElement.TryGetProperty("hits", out var h) && h.ValueKind == JsonValueKind.Array
            ? h.EnumerateArray().Select(x => x.Clone()).ToList()
            : [];
        return new(OffStatus.Ok, hits);
    }

    private async Task<(HttpStatusCode? Status, JsonDocument? Doc)> SendAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent(Options.ContactEmail));
        request.Headers.Accept.ParseAdd("application/json");
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                gate.ReportSuccess();
                return (response.StatusCode, null);
            }
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable || (int)response.StatusCode >= 500)
            {
                log.LogWarning("Open Food Facts returned {Status} for {Url}", (int)response.StatusCode, url);
                gate.ReportFailure(response.Headers.RetryAfter?.Delta);
                return (response.StatusCode, null);
            }
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Open Food Facts returned {Status} for {Url}", (int)response.StatusCode, url);
                return (response.StatusCode, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            gate.ReportSuccess();
            return (response.StatusCode, doc);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            log.LogWarning("Open Food Facts request failed: {Message}", ex.Message);
            gate.ReportFailure();
            return (null, null);
        }
    }
}
