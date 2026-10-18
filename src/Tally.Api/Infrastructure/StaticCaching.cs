using Microsoft.AspNetCore.StaticFiles;

namespace Tally.Api.Infrastructure;

/// <summary>
/// Hashed build assets are immutable; the HTML shell, service worker and manifest
/// must always be revalidated so app updates reach installed PWAs promptly.
/// </summary>
public static class StaticCaching
{
    public static StaticFileOptions Options()
    {
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".webmanifest"] = "application/manifest+json";
        return new StaticFileOptions
        {
            ContentTypeProvider = types,
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value ?? "";
                ctx.Context.Response.Headers.CacheControl = path.StartsWith("/assets/", StringComparison.Ordinal)
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
            },
        };
    }
}
