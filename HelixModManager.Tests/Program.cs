using System.Net.WebSockets;
using HelixModManager.Models;
using HelixModManager.Services;

var runner = new TestRunner();
await runner.RunAsync();
return runner.Failures > 0 ? 1 : 0;

public sealed class TestRunner
{
    private readonly List<Func<Task>> _tests;

    public TestRunner()
    {
        _tests = new List<Func<Task>>
        {
            HappyPathAsync,
            ReconnectUsesStoredTokenAsync,
            TimeoutCancelsRequestAsync
        };
    }

    public int Failures { get; private set; }

    public async Task RunAsync()
    {
        foreach (var test in _tests)
        {
            var name = test.Method.Name;
            try
            {
                await test();
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception ex)
            {
                Failures++;
                Console.WriteLine($"[FAIL] {name}: {ex.Message}");
            }
        }
    }

    private static NexusSsoService CreateService(ScriptedTransport transport, TimeSpan? timeout = null)
    {
        return new NexusSsoService(
            () => transport,
            _ => { },
            _ => { },
            timeout);
    }

    private static string BuildResponse(string? connectionToken = null, string? apiKey = null, bool success = true, string? error = null)
    {
        var data = connectionToken != null
            ? $"\"connection_token\":\"{connectionToken}\""
            : apiKey != null
                ? $"\"api_key\":\"{apiKey}\""
                : string.Empty;
        return $"{{\"success\":{success.ToString().ToLowerInvariant()},\"data\":{{{data}}},\"error\":{(error == null ? "null" : $"\"{error}\"")}}}";
    }

    private async Task HappyPathAsync()
    {
        var settings = new Settings();
        var messages = new[]
        {
            SsoTransportMessage.FromPayload(BuildResponse(connectionToken: "token-123")),
            SsoTransportMessage.FromPayload(BuildResponse(apiKey: "abc-123"))
        };

        var transport = new ScriptedTransport(messages);
        var service = CreateService(transport, TimeSpan.FromSeconds(1));
        var result = await service.LinkAccountAsync(settings, null, CancellationToken.None);

        Assert(result.Success, "Expected success result.");
        Assert(result.ApiKey == "abc-123", "API key mismatch.");
        Assert(string.IsNullOrWhiteSpace(settings.NexusSsoConnectionToken), "Connection token should be cleared.");
    }

    private async Task ReconnectUsesStoredTokenAsync()
    {
        var settings = new Settings
        {
            NexusSsoRequestId = "existing-request",
            NexusSsoConnectionToken = "persisted-token"
        };

        var transport = new ScriptedTransport(new[]
        {
            SsoTransportMessage.FromPayload(BuildResponse(apiKey: "fresh-key"))
        });

        var service = CreateService(transport, TimeSpan.FromSeconds(1));
        var result = await service.LinkAccountAsync(settings, null, CancellationToken.None);

        Assert(result.Success && result.ApiKey == "fresh-key", "Expected API key from reconnect.");
        Assert(transport.LastPayload?.Contains("persisted-token") == true, "Existing token was not reused.");
    }

    private async Task TimeoutCancelsRequestAsync()
    {
        var settings = new Settings();
        var transport = new ScriptedTransport(Array.Empty<SsoTransportMessage>(), simulateHang: true);
        var service = CreateService(transport, TimeSpan.FromMilliseconds(50));

        var result = await service.LinkAccountAsync(settings, null, CancellationToken.None);
        Assert(result.IsCancelled, "Expected cancellation result.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

internal sealed class ScriptedTransport : INexusSsoTransport
{
    private readonly Queue<Func<CancellationToken, Task<SsoTransportMessage>>> _messages;
    private readonly bool _simulateHang;

    public ScriptedTransport(IEnumerable<SsoTransportMessage> messages, bool simulateHang = false)
    {
        _messages = new Queue<Func<CancellationToken, Task<SsoTransportMessage>>>(
            messages.Select(msg => new Func<CancellationToken, Task<SsoTransportMessage>>(_ => Task.FromResult(msg))));
        _simulateHang = simulateHang;
        State = WebSocketState.Open;
    }

    public string? LastPayload { get; private set; }
    public bool Closed { get; private set; }
    public WebSocketState State { get; private set; }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        State = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        LastPayload = payload;
        return Task.CompletedTask;
    }

    public async Task<SsoTransportMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_simulateHang)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        if (_messages.Count == 0)
        {
            return SsoTransportMessage.Closed;
        }

        var producer = _messages.Dequeue();
        return await producer(cancellationToken);
    }

    public Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken)
    {
        Closed = true;
        State = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        State = WebSocketState.Closed;
        return ValueTask.CompletedTask;
    }
}
