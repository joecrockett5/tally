using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tally.Api.Features.Profiles;
using Tally.Api.Infrastructure;

namespace Tally.Api.Tests;

public static class TestHelpers
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates a fresh profile and returns a client that acts as it.</summary>
    public static async Task<(HttpClient Client, ProfileDto Profile)> CreateProfileClientAsync(this TallyApiFactory factory, string? name = null)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/profiles", new CreateProfileRequest(name ?? $"Test {Guid.NewGuid():N}"[..20], null, null), Ct);
        response.EnsureSuccessStatusCode();
        var profile = (await response.Content.ReadFromJsonAsync<ProfileDto>(Ct))!;
        client.DefaultRequestHeaders.Add(ProfileContext.Header, profile.Id.ToString());
        return (client, profile);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}

public static class FactoryExtensions
{
    /// <summary>Runs <paramref name="work"/> against the test database directly.</summary>
    public static async Task WithDbAsync(this TallyApiFactory factory, Func<Tally.Api.Data.TallyDbContext, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<Tally.Api.Data.TallyDbContext>());
    }

    public static async Task<Tally.Api.Features.Foods.FoodDto> FirstFoodAsync(this HttpClient client, string q) =>
        (await (await client.GetAsync($"/api/foods/search?q={Uri.EscapeDataString(q)}", TestHelpers.Ct))
            .ReadAsync<List<Tally.Api.Features.Foods.FoodDto>>())[0];
}
