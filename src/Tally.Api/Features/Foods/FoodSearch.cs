using System.Text;
using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.Features.Foods;

/// <summary>
/// Local search over everything in Postgres: the profile's foods, shared and
/// restaurant foods, CoFID generics and cached OFF products. Full-text (prefix)
/// matches and trigram similarity give relevance; a small source boost keeps the
/// brief's priority order (mine → shared/restaurant → CoFID → OFF, UK first).
/// </summary>
public sealed class FoodSearch(TallyDbContext db)
{
    public async Task<List<FoodDto>> SearchAsync(Guid profileId, string? q, int limit, CancellationToken ct)
    {
        var tokens = Tokenize(q);
        if (tokens.Count == 0) return [];

        var tsQuery = string.Join(" & ", tokens.Select(t => t + ":*"));
        var text = string.Join(' ', tokens);

        var ranked = await db.Foods.AsNoTracking()
            .VisibleTo(profileId)
            .Select(f => new
            {
                f.Id,
                f.Barcode,
                Fts = f.SearchVector.Matches(EF.Functions.ToTsQuery("english", tsQuery)),
                Rank = f.SearchVector.Rank(EF.Functions.ToTsQuery("english", tsQuery)),
                Sim = EF.Functions.TrigramsWordSimilarity(text, f.Name + " " + (f.Brand ?? "")),
                Boost = f.Source == FoodSource.Custom
                    ? (f.OwnerProfileId == profileId && f.RestaurantId == null ? 0.6 : 0.5)
                    : f.Source == FoodSource.Cofid ? 0.4
                    : f.IsUk ? 0.2 : 0.0,
                NameLength = f.Name.Length,
            })
            .Where(x => x.Fts || x.Sim > 0.5)
            .OrderByDescending(x => (x.Fts ? 1.0 : 0.0) + x.Sim + x.Rank + x.Boost)
            .ThenBy(x => x.NameLength)
            .Take(limit * 2)
            .Select(x => new { x.Id, x.Barcode })
            .ToListAsync(ct);

        // Same product from several sources: keep the highest-priority one.
        var ids = ranked
            .DistinctBy(x => x.Barcode ?? x.Id.ToString())
            .Take(limit)
            .Select(x => x.Id)
            .ToList();

        var foods = await db.Foods.AsNoTracking()
            .Include(f => f.Servings).Include(f => f.Restaurant)
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);
        return ids.Select(id => FoodDto.From(foods[id], profileId)).ToList();
    }

    /// <summary>Lower-cased letter/digit tokens; apostrophes are dropped so "Nando's" ≈ "nandos".</summary>
    public static List<string> Tokenize(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return [];
        var sb = new StringBuilder();
        foreach (var c in q.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is '\'' or '’') continue;
            else sb.Append(' ');
        }
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(8).ToList();
    }
}
