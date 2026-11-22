using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Atomiq.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthReturnsOkStatusAndUtcTimestamp()
    {
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();

        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Status);
        Assert.NotEqual(default, payload.TimestampUtc);
    }

    private sealed record HealthPayload(string Status, DateTimeOffset TimestampUtc);
}
