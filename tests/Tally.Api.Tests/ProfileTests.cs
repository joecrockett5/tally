using System.Net;
using System.Net.Http.Json;
using Tally.Api.Features.Profiles;
using Tally.Api.Infrastructure;
using static Tally.Api.Tests.TestHelpers;

namespace Tally.Api.Tests;

public sealed class ProfileTests(TallyApiFactory factory)
{
    [Fact]
    public async Task Create_assigns_avatar_and_colour_and_appears_in_list()
    {
        var (client, profile) = await factory.CreateProfileClientAsync("Alex");

        Assert.Equal("Alex", profile.Name);
        Assert.False(string.IsNullOrEmpty(profile.Avatar));
        Assert.Matches("^#[0-9a-f]{6}$", profile.Colour);

        var all = await (await client.GetAsync("/api/profiles", Ct)).ReadAsync<List<ProfileDto>>();
        Assert.Contains(all, p => p.Id == profile.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_blank_names(string name)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/profiles", new CreateProfileRequest(name, null, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rename_updates_name()
    {
        var (client, profile) = await factory.CreateProfileClientAsync("Before");
        var updated = await (await client.PutAsJsonAsync($"/api/profiles/{profile.Id}", new UpdateProfileRequest("After", "🍓", "#2f5d9b"), Ct)).ReadAsync<ProfileDto>();
        Assert.Equal("After", updated.Name);
        Assert.Equal("🍓", updated.Avatar);
        Assert.Equal("#2f5d9b", updated.Colour);
    }

    [Fact]
    public async Task Delete_requires_exact_name_confirmation()
    {
        var (client, profile) = await factory.CreateProfileClientAsync("Delete Me");

        var wrong = await client.DeleteAsync($"/api/profiles/{profile.Id}?confirmName=delete%20me", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var right = await client.DeleteAsync($"/api/profiles/{profile.Id}?confirmName=Delete%20Me", Ct);
        Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);

        // The deleted profile can no longer act.
        var me = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.NotFound, me.StatusCode);
    }

    [Fact]
    public async Task Scoped_endpoints_require_a_known_profile_header()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/me", Ct)).StatusCode);

        client.DefaultRequestHeaders.Add(ProfileContext.Header, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_acting_profile()
    {
        var (client, profile) = await factory.CreateProfileClientAsync();
        var me = await (await client.GetAsync("/api/me", Ct)).ReadAsync<ProfileDto>();
        Assert.Equal(profile.Id, me.Id);
    }
}
