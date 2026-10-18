using System.Net;
using System.Net.Http.Json;
using Tally.Api.Features.Diary;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class GoalTests(TallyApiFactory factory)
{
    [Fact]
    public async Task New_profiles_get_uk_reference_intakes()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var goals = await (await client.GetAsync("/api/goals", Ct)).ReadAsync<GoalsDto>();
        Assert.Equal(new GoalsDto(2000, 70, 20, 260, 90, 30, 50, 6), goals);
    }

    [Fact]
    public async Task Goals_can_be_edited_and_cleared_and_show_on_the_day()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var updated = await (await client.PutAsJsonAsync("/api/goals", new GoalsDto(1800, null, 20, 220, 90, 30, 120, 6), Ct)).ReadAsync<GoalsDto>();
        Assert.Null(updated.Fat);
        Assert.Equal(120, updated.Protein);

        var day = await (await client.GetAsync("/api/diary/2026-09-01", Ct)).ReadAsync<DayDto>();
        Assert.Equal(updated, day.Goals);
    }

    [Fact]
    public async Task Goals_must_be_positive()
    {
        var (client, _) = await factory.CreateProfileClientAsync();
        var response = await client.PutAsJsonAsync("/api/goals", new GoalsDto(-5, null, null, null, null, null, null, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Goals_are_per_profile()
    {
        var (alice, _) = await factory.CreateProfileClientAsync();
        var (bob, _) = await factory.CreateProfileClientAsync();
        await alice.PutAsJsonAsync("/api/goals", new GoalsDto(1500, null, null, null, null, null, null, null), Ct);
        var bobs = await (await bob.GetAsync("/api/goals", Ct)).ReadAsync<GoalsDto>();
        Assert.Equal(2000, bobs.EnergyKcal);
    }
}
