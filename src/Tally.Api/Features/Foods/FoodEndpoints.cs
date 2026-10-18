using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Infrastructure;
using Tally.Api.OpenFoodFacts;

namespace Tally.Api.Features.Foods;

public sealed record OnlineSearchResponse(bool Available, string? Message, IReadOnlyList<FoodDto> Results);

public sealed record BarcodeLookupResponse(string Status, string Barcode, FoodDto? Food, string? Message);

public static class FoodEndpoints
{
    public static void MapFoodEndpoints(this RouteGroupBuilder scoped)
    {
        var g = scoped.MapGroup("/foods");

        g.MapGet("/search", async (string? q, int? limit, FoodSearch search, ProfileContext profile, CancellationToken ct) =>
            await search.SearchAsync(profile.Id, q, Math.Clamp(limit ?? 30, 1, 60), ct));

        g.MapGet("/search/online", async (string? q, OffFoodService off, ProfileContext profile, CancellationToken ct) =>
        {
            var result = await off.SearchAsync(q ?? "", ct);
            return new OnlineSearchResponse(result.Available, result.Message,
                result.Foods.Select(f => FoodDto.From(f, profile.Id)).ToList());
        });

        g.MapGet("/barcode/{code}", async (string code, string? format, TallyDbContext db, OffFoodService off,
            ProfileContext profile, CancellationToken ct) =>
        {
            var barcode = Barcodes.Normalize(code, format);
            if (barcode is null)
                return Results.Problem("That doesn't look like an EAN-13, EAN-8, UPC-A or UPC-E barcode.", statusCode: 400, title: "invalid_barcode");

            // The profile's own and shared foods win over Open Food Facts.
            var local = await db.Foods.AsNoTracking().VisibleTo(profile.Id)
                .Include(f => f.Servings).Include(f => f.Restaurant)
                .Where(f => f.Source == FoodSource.Custom && f.Barcode == barcode)
                .OrderByDescending(f => f.OwnerProfileId == profile.Id)
                .FirstOrDefaultAsync(ct);
            if (local is not null)
                return Results.Ok(new BarcodeLookupResponse("found", barcode, FoodDto.From(local, profile.Id), null));

            var result = await off.LookupBarcodeAsync(barcode, ct);
            return Results.Ok(result.Status switch
            {
                OffStatus.Ok => new BarcodeLookupResponse("found", barcode, FoodDto.From(result.Food!, profile.Id), null),
                OffStatus.NotFound => new BarcodeLookupResponse("not_found", barcode, null, "This barcode isn't in Open Food Facts yet."),
                _ => new BarcodeLookupResponse("unavailable", barcode, null, result.Message),
            });
        });

        g.MapGet("/{id:guid}", async (Guid id, TallyDbContext db, ProfileContext profile, CancellationToken ct) =>
            await db.Foods.AsNoTracking().VisibleTo(profile.Id)
                .Include(f => f.Servings).Include(f => f.Restaurant)
                .FirstOrDefaultAsync(f => f.Id == id, ct) is { } food
                ? Results.Ok(FoodDto.From(food, profile.Id))
                : Results.NotFound());
    }
}
