using System.Net;
using System.Net.WebSockets;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace Atomiq.Api.Tests;

public sealed class SignalRHubTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public SignalRHubTests(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AuthorizedConnectionReceivesTutorExplanationEvent()
    {
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection("test-token-1");
        connection.On<string>("ReceiveTutorExplanation", response => received.TrySetResult(response));

        await connection.StartAsync();
        await connection.InvokeAsync(
            "ExplainReaction",
            JsonSerializer.Serialize(CreatePayload(eventId: "signalr-event-1")),
            "Ignore this client prompt");

        var responseJson = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var source = response.GetProperty("source").GetString();
        Assert.True(source is "OfflineFallback" or "MemoryCache");
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("explanation").GetString()));
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task UnauthenticatedConnectionIsRejected()
    {
        await using var connection = CreateConnection(null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task MalformedInvocationReturnsSanitizedHubError()
    {
        await using var connection = CreateConnection("test-token-1");
        await connection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("ExplainReaction", "not-json", "prompt"));

        Assert.DoesNotContain("System.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OversizedReactionMessageIsRejected()
    {
        await using var connection = CreateConnection("test-token-1");
        await connection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("ExplainReaction", new string('x', 70 * 1024), "prompt"));

        Assert.DoesNotContain("System.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownUnityOutcomeIsRejectedWithoutLeakingDetails()
    {
        await using var connection = CreateConnection("unity-signalr-token");
        await connection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "ExplainReaction",
                JsonSerializer.Serialize(CreatePayload("unknown-signalr-outcome", 99)),
                "prompt"));

        Assert.DoesNotContain("System.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public async Task AuthorizedConnectionCanReconnect()
    {
        await using var connection = CreateConnection("test-token-2");
        await connection.StartAsync();
        await connection.StopAsync();
        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task RestAndSignalRShareEventIdempotency()
    {
        using var restClient = factory.CreateClient();
        restClient.DefaultRequestHeaders.Add("X-Atomiq-Device-Token", "test-token-1");
        var payload = CreatePayload("cross-transport-event");
        var restResponse = await restClient.PostAsJsonAsync("/api/reactions/explain", payload);
        Assert.Equal(HttpStatusCode.OK, restResponse.StatusCode);

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection("test-token-1");
        connection.On<string>("ReceiveTutorExplanation", response => received.TrySetResult(response));
        await connection.StartAsync();
        await connection.InvokeAsync(
            "ExplainReaction",
            JsonSerializer.Serialize(payload),
            "client prompt");

        var signalRResponse = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var result = JsonSerializer.Deserialize<JsonElement>(signalRResponse);
        Assert.True(result.GetProperty("historyDuplicate").GetBoolean());
    }

    [Fact]
    public async Task RepeatedSignalREventIsIdempotentInHistory()
    {
        await using var connection = CreateConnection("test-token-2");
        await connection.StartAsync();
        var payload = JsonSerializer.Serialize(CreatePayload(eventId: "same-signalr-event"));
        var received = new List<JsonElement>();
        connection.On<string>("ReceiveTutorExplanation", response =>
            received.Add(JsonSerializer.Deserialize<JsonElement>(response)));

        await connection.InvokeAsync("ExplainReaction", payload, "prompt");
        await connection.InvokeAsync("ExplainReaction", payload, "prompt");

        await WaitForCountAsync(received, 2);
        Assert.False(received[0].GetProperty("historyDuplicate").GetBoolean());
        Assert.True(received[1].GetProperty("historyDuplicate").GetBoolean());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    public async Task SignalRMapsUnityOutcomeAndPreservesIsValid(
        int unityOutcomeType,
        int domainOutcomeType)
    {
        var eventId = "unity-signalr-outcome-" + unityOutcomeType + "-" + Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateConnection("unity-signalr-token");
        connection.On<string>("ReceiveTutorExplanation", response => received.TrySetResult(response));
        await connection.StartAsync();
        await connection.InvokeAsync(
            "ExplainReaction",
            JsonSerializer.Serialize(CreatePayload(eventId, unityOutcomeType)),
            "client prompt");

        var response = JsonSerializer.Deserialize<JsonElement>(
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        using var restClient = factory.CreateClient();
        restClient.DefaultRequestHeaders.Add("X-Atomiq-Device-Token", "unity-signalr-token");
        var history = await restClient.GetFromJsonAsync<JsonElement>(
            "/api/reactions/history?page=1&pageSize=100");
        var historyItem = history.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("eventId").GetString() == eventId);

        Assert.True(response.GetProperty("source").GetString() is "OfflineFallback" or "MemoryCache");
        Assert.False(response.GetProperty("historyDuplicate").GetBoolean());
        Assert.True(historyItem.GetProperty("isValid").GetBoolean());
        Assert.Equal(domainOutcomeType, historyItem.GetProperty("outcomeType").GetInt32());
        Assert.Equal(unityOutcomeType, historyItem.GetProperty("unityOutcomeType").GetInt32());
    }

    [Fact]
    public async Task WebSocketHandshakeAndInvocationUseSignalRJsonProtocol()
    {
        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request =>
            request.Headers["X-Atomiq-Device-Token"] = "test-token-1";
        using var socket = await webSocketClient.ConnectAsync(
            new Uri("ws://localhost/hubs/aitutor"),
            CancellationToken.None);

        await SendTextAsync(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
        var handshakeResponse = await ReceiveTextAsync(socket);
        Assert.EndsWith("\u001e", handshakeResponse, StringComparison.Ordinal);

        var reactionJson = JsonSerializer.Serialize(CreatePayload("websocket-event"));
        var invocation = JsonSerializer.Serialize(new
        {
            type = 1,
            invocationId = "1",
            target = "ExplainReaction",
            arguments = new object[] { reactionJson, "client prompt is data" }
        });
        await SendTextAsync(socket, invocation + "\u001e");

        var receivedEvent = false;
        var receivedCompletion = false;
        for (var attempt = 0; attempt < 5 && (!receivedEvent || !receivedCompletion); attempt++)
        {
            var message = await ReceiveTextAsync(socket);
            foreach (var record in message.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var document = JsonDocument.Parse(record);
                if (document.RootElement.TryGetProperty("type", out var messageType)
                    && messageType.GetInt32() == 3
                    && document.RootElement.TryGetProperty("invocationId", out var invocationId)
                    && invocationId.GetString() == "1")
                {
                    receivedCompletion = !document.RootElement.TryGetProperty("error", out _);
                }

                if (document.RootElement.TryGetProperty("target", out var target)
                    && target.GetString() == "ReceiveTutorExplanation")
                {
                    receivedEvent = true;
                }
            }
        }

        Assert.True(receivedEvent);
        Assert.True(receivedCompletion);
    }

    private HubConnection CreateConnection(string? token)
    {
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/aitutor", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (token is not null)
                {
                    options.Headers.Add("X-Atomiq-Device-Token", token);
                }
            })
            .Build();
    }

    private static object CreatePayload(string? eventId = null, int outcomeType = 1)
    {
        return new
        {
            reactantA = "Na",
            reactantB = "Cl",
            productFormula = "NaCl",
            productDisplayName = "Cloruro de Sodio",
            reactionEquation = eventId == "signalr-event-1"
                ? "Na+ + Cl- -> NaCl (signalr)"
                : "Na+ + Cl- -> NaCl (s)",
            isValid = true,
            outcomeType,
            deltaEnthalpyKjMol = -411.2,
            electronsTransferred = 1,
            thermodynamicClassification = "Exotérmica",
            timestampUnixMs = 1732068000000L,
            eventId
        };
    }

    private static async Task WaitForCountAsync(List<JsonElement> values, int count)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (values.Count < count && DateTime.UtcNow < timeout)
        {
            await Task.Delay(25);
        }
    }

    private static async Task SendTextAsync(WebSocket socket, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveTextAsync(WebSocket socket)
    {
        var buffer = new byte[8192];
        using var output = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("The SignalR WebSocket closed unexpectedly.");
            }

            output.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
