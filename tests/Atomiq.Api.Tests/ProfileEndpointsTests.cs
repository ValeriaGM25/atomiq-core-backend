using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atomiq.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Atomiq.Api.Tests;

public sealed class ProfileEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public ProfileEndpointsTests(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ProfileIsCreatedAndReturnedWithoutInternalId()
    {
        using var client = CreateClient("profile-token");

        var response = await client.GetAsync("/api/profile/profile-device");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("profile-device", payload.GetProperty("deviceId").GetString());
        Assert.Equal(0, payload.GetProperty("totalXp").GetInt32());
        Assert.False(payload.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task XpIsAppliedOnlyOnceForTheSameEvent()
    {
        using var client = CreateClient("xp-token");
        await client.GetAsync("/api/profile/xp-device");

        var request = new { deviceId = "xp-device", eventId = "event-1", xpGained = 25 };
        var first = await client.PostAsJsonAsync("/api/profile/xp", request);
        var second = await client.PostAsJsonAsync("/api/profile/xp", request);
        var payload = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(payload.GetProperty("duplicateEvent").GetBoolean());
        Assert.Equal(25, payload.GetProperty("totalXp").GetInt32());
    }

    [Fact]
    public async Task InvalidXpAndDeviceIdReturnValidationProblemDetails()
    {
        using var client = CreateClient("xp-token");

        var invalidXp = await client.PostAsJsonAsync("/api/profile/xp", new
        {
            deviceId = "device-1",
            eventId = "event-invalid",
            xpGained = 1001
        });
        var invalidDevice = await client.GetAsync("/api/profile/invalid device");

        Assert.Equal(HttpStatusCode.BadRequest, invalidXp.StatusCode);
        Assert.Equal("application/problem+json", invalidXp.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDevice.StatusCode);
    }

    [Fact]
    public async Task AuthenticationAndDeviceIsolationAreEnforced()
    {
        using var unauthenticated = factory.CreateClient();
        using var deviceOne = CreateClient("test-token-1");

        var unauthorized = await unauthenticated.GetAsync("/api/profile/device-1");
        var forbidden = await deviceOne.GetAsync("/api/profile/device-2");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task HistoryIsPagedAndOrderedForAuthenticatedDevice()
    {
        factory.Reactions.Add(CreateReaction("event-1", DateTimeOffset.UtcNow.AddMinutes(-2), "history-device"));
        factory.Reactions.Add(CreateReaction("event-2", DateTimeOffset.UtcNow.AddMinutes(-1), "history-device"));

        using var client = CreateClient("history-token");
        var response = await client.GetAsync("/api/reactions/history?page=1&pageSize=1");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("event-2", items[0].GetProperty("eventId").GetString());
        Assert.True(payload.GetProperty("hasNextPage").GetBoolean());
    }

    private HttpClient CreateClient(string token)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Atomiq-Device-Token", token);
        return client;
    }

    private static ReactionLog CreateReaction(string eventId, DateTimeOffset timestamp, string deviceId) => ReactionLog.Create(
        eventId,
        deviceId,
        "profile-1",
        "H2",
        "O2",
        "H2O",
        "2H2+O2->2H2O",
        -285.8,
        0,
        true,
        ReactionOutcomeType.Valid,
        "Exothermic",
        null,
        null,
        timestamp);
}

public sealed class RateLimitTests : IClassFixture<RateLimitWebApplicationFactory>
{
    private readonly RateLimitWebApplicationFactory factory;

    public RateLimitTests(RateLimitWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RateLimitReturnsTooManyRequests()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Atomiq-Device-Token", "rate-token");

        HttpResponseMessage? lastResponse = null;
        for (var requestNumber = 0; requestNumber <= 100; requestNumber++)
        {
            lastResponse = await client.GetAsync("/api/profile/rate-device");
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }
}
