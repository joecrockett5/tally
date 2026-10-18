using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;
using Tally.Api.Infrastructure;

namespace Tally.Api.Features.Profiles;

public sealed record ProfileDto(Guid Id, string Name, string Avatar, string Colour);

public sealed record CreateProfileRequest(string Name, string? Avatar, string? Colour);

public sealed record UpdateProfileRequest(string Name, string? Avatar, string? Colour);

public static class ProfileEndpoints
{
    public static readonly string[] Avatars = ["🍎", "🥑", "🥕", "🍋", "🍓", "🥦", "🍌", "🫐", "🍊", "🥝", "🍇", "🌽"];
    public static readonly string[] Colours = ["#1f6f5c", "#2f5d9b", "#9b3f6b", "#b5651d", "#6b4fa0", "#3c7d2b", "#a33a3a", "#2a7f8f"];

    public static void MapProfileEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/profiles");

        g.MapGet("/", async (TallyDbContext db, CancellationToken ct) =>
            await db.Profiles.OrderBy(p => p.CreatedAt)
                .Select(p => new ProfileDto(p.Id, p.Name, p.Avatar, p.Colour))
                .ToListAsync(ct));

        g.MapGet("/{id:guid}", async (Guid id, TallyDbContext db, CancellationToken ct) =>
            await db.Profiles.Where(p => p.Id == id)
                .Select(p => new ProfileDto(p.Id, p.Name, p.Avatar, p.Colour))
                .FirstOrDefaultAsync(ct) is { } dto
                ? Results.Ok(dto)
                : Results.NotFound());

        g.MapPost("/", async (CreateProfileRequest req, TallyDbContext db, CancellationToken ct) =>
        {
            var name = req.Name?.Trim() ?? "";
            if (Validate(name) is { } problem) return problem;

            var count = await db.Profiles.CountAsync(ct);
            var profile = new Profile
            {
                Name = name,
                Avatar = string.IsNullOrWhiteSpace(req.Avatar) ? Avatars[Random.Shared.Next(Avatars.Length)] : req.Avatar.Trim(),
                Colour = ValidColour(req.Colour) ?? Colours[count % Colours.Length],
            };
            db.Profiles.Add(profile);
            await ProfileLifecycle.OnCreatedAsync(db, profile, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/profiles/{profile.Id}", ToDto(profile));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateProfileRequest req, TallyDbContext db, CancellationToken ct) =>
        {
            var profile = await db.Profiles.FindAsync([id], ct);
            if (profile is null) return Results.NotFound();
            var name = req.Name?.Trim() ?? "";
            if (Validate(name) is { } problem) return problem;

            profile.Name = name;
            if (!string.IsNullOrWhiteSpace(req.Avatar)) profile.Avatar = req.Avatar.Trim();
            if (ValidColour(req.Colour) is { } colour) profile.Colour = colour;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(profile));
        });

        // Deleting needs the profile's exact name as confirmation.
        g.MapDelete("/{id:guid}", async (Guid id, string? confirmName, TallyDbContext db, CancellationToken ct) =>
        {
            var profile = await db.Profiles.FindAsync([id], ct);
            if (profile is null) return Results.NotFound();
            if (!string.Equals(confirmName?.Trim(), profile.Name, StringComparison.Ordinal))
                return Results.Problem("Type the profile name exactly to confirm deletion.", statusCode: 400, title: "confirmation_mismatch");

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await ProfileLifecycle.OnDeletingAsync(db, profile.Id, ct);
            db.Profiles.Remove(profile);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });
    }

    private static IResult? Validate(string name) =>
        name.Length switch
        {
            0 => Results.Problem("Give the profile a name.", statusCode: 400, title: "name_required"),
            > 40 => Results.Problem("Names can be at most 40 characters.", statusCode: 400, title: "name_too_long"),
            _ => null,
        };

    private static string? ValidColour(string? colour) =>
        colour is not null && System.Text.RegularExpressions.Regex.IsMatch(colour, "^#[0-9a-fA-F]{6}$") ? colour : null;

    private static ProfileDto ToDto(Profile p) => new(p.Id, p.Name, p.Avatar, p.Colour);
}
