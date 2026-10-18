using Tally.Api.Data;

namespace Tally.Api.Features.Profiles;

/// <summary>Side effects of creating and deleting a profile.</summary>
public static class ProfileLifecycle
{
    public static Task OnCreatedAsync(TallyDbContext db, Profile profile, CancellationToken ct) => Task.CompletedTask;

    public static Task OnDeletingAsync(TallyDbContext db, Guid profileId, CancellationToken ct) => Task.CompletedTask;
}
