using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.Infrastructure;

/// <summary>The profile the current request acts as, resolved from the X-Profile-Id header.</summary>
public sealed class ProfileContext
{
    public const string Header = "X-Profile-Id";

    private Guid? _id;

    public Guid Id => _id ?? throw new InvalidOperationException("No profile resolved for this request");

    public void Set(Guid id) => _id = id;
}

public static class ProfileScopeExtensions
{
    /// <summary>
    /// Requires a valid X-Profile-Id on every endpoint in the group. There is no
    /// authentication by design; this only makes sure the profile exists so
    /// that every query can be scoped to it.
    /// </summary>
    public static RouteGroupBuilder RequireProfile(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var raw = http.Request.Headers[ProfileContext.Header].ToString();
            if (!Guid.TryParse(raw, out var id))
                return Results.Problem($"The {ProfileContext.Header} header is required.", statusCode: 400, title: "profile_required");

            var db = http.RequestServices.GetRequiredService<TallyDbContext>();
            if (!await db.Profiles.AnyAsync(p => p.Id == id, http.RequestAborted))
                return Results.Problem("That profile no longer exists.", statusCode: 404, title: "profile_not_found");

            http.RequestServices.GetRequiredService<ProfileContext>().Set(id);
            return await next(ctx);
        });
        return group;
    }
}
