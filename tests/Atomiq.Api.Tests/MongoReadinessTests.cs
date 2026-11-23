using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Atomiq.Api.Tests;

public sealed class MongoReadinessTests : IClassFixture<NotReadyApiWebApplicationFactory>
{
    private readonly NotReadyApiWebApplicationFactory factory;

    public MongoReadinessTests(NotReadyApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ReadinessIsSeparateFromLivenessAndBlocksRestPersistence()
    {
        using var client = factory.CreateClient();
        var health = await client.GetAsync("/health");
        var readiness = await client.GetAsync("/health/readiness");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);

        client.DefaultRequestHeaders.Add("X-Atomiq-Device-Token", "unity-rest-token");
        var response = await client.PostAsJsonAsync(
            "/api/reactions/explain",
            CreatePayload("readiness-rest-event"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task MissingIndexesBlockSignalRPersistence()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/aitutor", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Headers.Add("X-Atomiq-Device-Token", "unity-signalr-token");
            })
            .Build();

        await connection.StartAsync();
        var exception = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(
            "ExplainReaction",
            JsonSerializer.Serialize(CreatePayload("readiness-signalr-event")),
            "local test prompt"));

        Assert.NotEmpty(exception.Message);
        Assert.DoesNotContain("System.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static object CreatePayload(string eventId) => new
    {
        reactantA = "Na",
        reactantB = "Cl",
        productFormula = "NaCl",
        productDisplayName = "Cloruro de Sodio",
        reactionEquation = "Na+ + Cl- -> NaCl (s)",
        isValid = true,
        outcomeType = 1,
        deltaEnthalpyKjMol = -411.2,
        electronsTransferred = 1,
        thermodynamicClassification = "Exotermica",
        timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        eventId
    };
}
