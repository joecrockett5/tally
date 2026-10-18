using Microsoft.EntityFrameworkCore;
using Tally.Api.Data;

namespace Tally.Api.Features.Profiles;

/// <summary>Side effects of creating and deleting a profile.</summary>
public static class ProfileLifecycle
{
    /// <summary>New profiles start with UK Reference Intake targets.</summary>
    public static Task OnCreatedAsync(TallyDbContext db, Profile profile, CancellationToken ct)
    {
        db.Goals.Add(Goal.UkDefaults(profile.Id));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Diary entries, recipes, saved meals and goals cascade with the profile. Private foods are removed; shared
    /// foods stay available to everyone (their owner is cleared by the FK).
    /// </summary>
    public static async Task OnDeletingAsync(TallyDbContext db, Guid profileId, CancellationToken ct)
    {
        await db.Foods
            .Where(f => f.Source == FoodSource.Custom && f.OwnerProfileId == profileId && !f.IsShared)
            .ExecuteDeleteAsync(ct);
    }
}
