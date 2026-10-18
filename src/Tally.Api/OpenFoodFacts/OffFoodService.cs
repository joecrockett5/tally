using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Tally.Api.Data;

namespace Tally.Api.OpenFoodFacts;

public sealed record OffLookup(OffStatus Status, Food? Food, string? Message = null);

public sealed record OffSearch(bool Available, string? Message, IReadOnlyList<Food> Foods);

/// <summary>Open Food Facts data, cache-first: Postgres holds every product we've fetched.</summary>
public sealed class OffFoodService(
    TallyDbContext db,
    OffClient client,
    OffRefreshQueue refreshQueue,
    IOptions<TallyOptions> options,
    TimeProvider time)
{
    private const int PageSize = 24;
    private const int TopUpThreshold = 8;

    private OpenFoodFactsOptions O => options.Value.OpenFoodFacts;

    public async Task<OffLookup> LookupBarcodeAsync(string code, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var cached = await db.Foods.Include(f => f.Servings)
            .FirstOrDefaultAsync(f => f.Source == FoodSource.OpenFoodFacts && f.ExternalId == code, ct);
        if (cached is not null)
        {
            if (IsStale(cached, now)) refreshQueue.Enqueue(code);
            return new(OffStatus.Ok, cached);
        }

        var miss = await db.OffBarcodeMisses.FindAsync([code], ct);
        if (miss is not null && miss.CheckedAt > now.AddHours(-O.NotFoundCacheHours))
            return new(OffStatus.NotFound, null);

        var result = await client.GetProductAsync(code, background: false, ct);
        switch (result.Status)
        {
            case OffStatus.Ok:
                var food = (await UpsertAsync([result.Value], ct)).Single();
                if (miss is not null) db.OffBarcodeMisses.Remove(miss);
                await db.SaveChangesAsync(ct);
                return new(OffStatus.Ok, food);
            case OffStatus.NotFound:
                if (miss is null) db.OffBarcodeMisses.Add(new OffBarcodeMiss { Barcode = code, CheckedAt = now });
                else miss.CheckedAt = now;
                await db.SaveChangesAsync(ct);
                return new(OffStatus.NotFound, null);
            default:
                return new(OffStatus.Unavailable, null, result.Message);
        }
    }

    /// <summary>Re-fetches a cached product. Used by the background worker.</summary>
    public async Task<OffStatus> RefreshAsync(string code, CancellationToken ct)
    {
        var cached = await db.Foods.FirstOrDefaultAsync(f => f.Source == FoodSource.OpenFoodFacts && f.ExternalId == code, ct);
        if (cached is null || !IsStale(cached, time.GetUtcNow())) return OffStatus.Ok;

        var result = await client.GetProductAsync(code, background: true, ct);
        if (result.Status == OffStatus.Ok) await UpsertAsync([result.Value], ct);
        else if (result.Status == OffStatus.NotFound)
        {
            // Product removed upstream: keep serving what we have, but don't retry until the next TTL.
            cached.FetchedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        return result.Status;
    }

    public async Task<OffSearch> SearchAsync(string rawQuery, CancellationToken ct)
    {
        var text = SanitizeQuery(rawQuery);
        if (text.Length < 3) return new(true, null, []);

        var key = "q:" + text.ToLowerInvariant();
        if (key.Length > 200) key = key[..200];
        var now = time.GetUtcNow();
        var cache = await db.OffSearchCache.FindAsync([key], ct);
        if (cache is not null && cache.FetchedAt > now.AddHours(-O.SearchCacheHours))
            return new(true, null, await LoadByCodesAsync(cache.Codes, ct));

        var brandSlug = await MatchRestaurantSlugAsync(text, ct);
        var baseQuery = brandSlug is null ? text : $"brands_tags:\"{brandSlug.Value.Slug}\" {brandSlug.Value.Remainder}".Trim();

        var uk = await client.SearchAsync($"{baseQuery} countries_tags:\"{OffProductMapper.UkTag}\"", PageSize, ct);
        if (uk.Status != OffStatus.Ok)
        {
            return cache is not null
                ? new(true, "Showing saved online results.", await LoadByCodesAsync(cache.Codes, ct))
                : new(false, uk.Message, []);
        }

        var hits = uk.Value!;
        if (hits.Count < TopUpThreshold)
        {
            var all = await client.SearchAsync(baseQuery, PageSize, ct);
            if (all.Status == OffStatus.Ok) hits = [.. hits, .. all.Value!];
        }

        var products = hits.Where(h => OffProductMapper.Code(h) is { Length: > 0 })
            .DistinctBy(h => OffProductMapper.Code(h))
            .ToList();
        var foods = (await UpsertAsync(products, ct))
            .OrderByDescending(f => f.IsUk) // stable: keeps OFF relevance order within each group
            .ToList();

        if (cache is null) db.OffSearchCache.Add(cache = new OffSearchCache { QueryKey = key });
        cache.Codes = foods.Select(f => f.ExternalId!).ToArray();
        cache.FetchedAt = now;
        await SaveIgnoringRacesAsync(ct);
        return new(true, null, foods);
    }

    /// <summary>Inserts or updates cached OFF products, returning them in input order.</summary>
    public async Task<List<Food>> UpsertAsync(IReadOnlyList<JsonElement> products, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var codes = products.Select(p => OffProductMapper.Code(p)!).ToList();
        var existing = await db.Foods.Include(f => f.Servings)
            .Where(f => f.Source == FoodSource.OpenFoodFacts && codes.Contains(f.ExternalId!))
            .ToDictionaryAsync(f => f.ExternalId!, ct);

        var result = new List<Food>(products.Count);
        foreach (var product in products)
        {
            var code = OffProductMapper.Code(product)!;
            if (!existing.TryGetValue(code, out var food))
            {
                food = new Food { Name = "", Source = FoodSource.OpenFoodFacts };
                db.Foods.Add(food);
                existing[code] = food;
            }
            OffProductMapper.Apply(product, food, now);
            result.Add(food);
        }

        await SaveIgnoringRacesAsync(ct);
        return result;
    }

    private async Task<List<Food>> LoadByCodesAsync(string[] codes, CancellationToken ct)
    {
        var foods = await db.Foods.Include(f => f.Servings)
            .Where(f => f.Source == FoodSource.OpenFoodFacts && codes.Contains(f.ExternalId!))
            .ToDictionaryAsync(f => f.ExternalId!, ct);
        var now = time.GetUtcNow();
        foreach (var f in foods.Values.Where(f => IsStale(f, now))) refreshQueue.Enqueue(f.ExternalId!);
        return codes.Where(foods.ContainsKey).Select(c => foods[c]).ToList();
    }

    private async Task<(string Slug, string Remainder)?> MatchRestaurantSlugAsync(string text, CancellationToken ct)
    {
        var normalized = Restaurant.Normalize(text).Replace("'", "");
        var names = await db.Restaurants.Select(r => r.Name).ToListAsync(ct);
        var match = names
            .Select(n => new { Name = n, Norm = Restaurant.Normalize(n).Replace("'", "") })
            .Where(x => normalized == x.Norm || normalized.StartsWith(x.Norm + " "))
            .OrderByDescending(x => x.Norm.Length)
            .FirstOrDefault();
        return match is null ? null : (BrandSlug(match.Name), normalized[match.Norm.Length..].Trim());
    }

    /// <summary>OFF tag slug: lower case, every run of non-alphanumerics → "-". "Nando's" → "nando-s".</summary>
    public static string BrandSlug(string brand)
    {
        var sb = new StringBuilder();
        foreach (var c in brand.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    /// <summary>Strips characters that have meaning in Search-a-licious' Lucene-style syntax.</summary>
    public static string SanitizeQuery(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return "";
        var sb = new StringBuilder(q.Length);
        foreach (var c in q.Trim())
            sb.Append(char.IsLetterOrDigit(c) || c is '\'' or '’' ? c : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private bool IsStale(Food food, DateTimeOffset now) =>
        food.FetchedAt is null || food.FetchedAt < now.AddDays(-O.ProductCacheDays);

    private async Task SaveIgnoringRacesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request cached the same product/query first; theirs is just as good.
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            await db.SaveChangesAsync(ct);
        }
    }
}
