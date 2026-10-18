using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Tally.Api.Tests;

/// <summary>Stands in for Open Food Facts. Tests register products and search hits by key.</summary>
public sealed class FakeOffHandler : HttpMessageHandler
{
    public ConcurrentDictionary<string, object> Products { get; } = new();
    /// <summary>Search hits keyed by a substring of the q parameter.</summary>
    public ConcurrentDictionary<string, object[]> SearchHits { get; } = new();
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();
    public HttpStatusCode? ForceStatus { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Enqueue(request);
        if (ForceStatus is { } forced) return Task.FromResult(new HttpResponseMessage(forced));

        var uri = request.RequestUri!;
        if (uri.AbsolutePath.StartsWith("/api/v3/product/"))
        {
            var code = uri.AbsolutePath["/api/v3/product/".Length..];
            return Task.FromResult(Products.TryGetValue(code, out var product)
                ? Json(new { code, status = "success", product })
                : Json(new { code, status = "failure", result = new { id = "product_not_found" } }, HttpStatusCode.NotFound));
        }

        if (uri.AbsolutePath == "/search")
        {
            var q = Uri.UnescapeDataString(uri.Query.Split('&').First(p => p.TrimStart('?').StartsWith("q=")).Split('=')[1]);
            var ukOnly = q.Contains("countries_tags:\"en:united-kingdom\"");
            var hits = SearchHits.Where(kv => q.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                .SelectMany(kv => kv.Value)
                .Where(h => !ukOnly || JsonSerializer.Serialize(h).Contains("en:united-kingdom"))
                .ToArray();
            return Task.FromResult(Json(new { hits, count = hits.Length, page = 1 }));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public int RequestCount(string pathContains) => Requests.Count(r => r.RequestUri!.ToString().Contains(pathContains));

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    // The handler is shared across HttpClient handler rotations; never dispose it.
    protected override void Dispose(bool disposing) { }

    public static object Product(string code, string name, string[] countries, object nutriments, string? brands = null,
        string? quantity = null, double? servingQuantity = null, string? servingSize = null) => new Dictionary<string, object?>
    {
        ["code"] = code,
        ["product_name"] = name,
        ["brands"] = brands,
        ["countries_tags"] = countries,
        ["nutriments"] = nutriments,
        ["quantity"] = quantity,
        ["serving_quantity"] = servingQuantity,
        ["serving_size"] = servingSize,
    };
}
