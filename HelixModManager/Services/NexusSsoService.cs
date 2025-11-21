using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HelixModManager.Models;

namespace HelixModManager.Services
{
    public sealed class NexusSsoService
    {
        private const string SocketUrl = "wss://sso.nexusmods.com";
        private const string AuthorizeUrl = "https://www.nexusmods.com/sso";
        private const string ApplicationSlug = "nnugget-helix";
        private static readonly Uri SocketUri = new(SocketUrl);
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        private readonly Func<INexusSsoTransport> _transportFactory;
        private readonly Action<Settings> _saveSettings;
        private readonly Action<string> _browserLauncher;
        private readonly TimeSpan _timeout;

        public NexusSsoService()
            : this(() => new WebSocketNexusSsoTransport(), SettingsService.SaveSettings, OpenBrowser, DefaultTimeout)
        {
        }

        internal NexusSsoService(
            Func<INexusSsoTransport> transportFactory,
            Action<Settings> saveSettings,
            Action<string>? browserLauncher = null,
            TimeSpan? timeout = null)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
            _browserLauncher = browserLauncher ?? OpenBrowser;
            _timeout = timeout ?? DefaultTimeout;
        }

        public async Task<NexusSsoResult> LinkAccountAsync(
            Settings settings,
            IProgress<string>? status = null,
            CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_timeout);
            var ct = linkedCts.Token;

            var requestId = EnsureRequestId(settings);
            var payload = JsonSerializer.Serialize(new
            {
                id = requestId,
                token = string.IsNullOrWhiteSpace(settings.NexusSsoConnectionToken)
                    ? null
                    : settings.NexusSsoConnectionToken,
                protocol = 2
            });

            try
            {
                await using var transport = _transportFactory();
                status?.Report("Connecting to Nexus Mods SSO...");
                await transport.ConnectAsync(SocketUri, ct);

                status?.Report("Registering SSO request...");
                await transport.SendAsync(payload, ct);

                status?.Report("Waiting for authorisation (browser window opened)...");
                OpenAuthorizePage(requestId);

                while (!ct.IsCancellationRequested)
                {
                    var message = await transport.ReceiveAsync(ct);
                    if (message.IsClosed)
                    {
                        await TryCloseAsync(transport);
                        return NexusSsoResult.CreateFailure("SSO connection closed before the request completed.");
                    }

                    if (string.IsNullOrWhiteSpace(message.Payload))
                        continue;

                    var handled = HandleMessage(message.Payload, settings, status);
                    if (handled != null)
                    {
                        await TryCloseAsync(transport);
                        return handled;
                    }
                }

                await TryCloseAsync(transport);
                return NexusSsoResult.CreateCancelled("SSO linking cancelled.");
            }
            catch (OperationCanceledException)
            {
                return NexusSsoResult.CreateCancelled("SSO linking timed out or was cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SSO] Unexpected error: {ex}");
                return NexusSsoResult.CreateFailure($"Unexpected SSO error: {ex.Message}");
            }
        }

        private static async Task TryCloseAsync(INexusSsoTransport transport)
        {
            if (transport.State == WebSocketState.Open || transport.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await transport.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SSO] Failed to close websocket gracefully: {ex.Message}");
                }
            }
        }

        private NexusSsoResult? HandleMessage(string payload, Settings settings, IProgress<string>? status)
        {
            using var doc = JsonDocument.Parse(payload);

            var root = doc.RootElement;
            if (!root.TryGetProperty("success", out var successElement) || !successElement.GetBoolean())
            {
                var error = root.TryGetProperty("error", out var errorElement)
                    ? errorElement.GetString()
                    : "Nexus Mods returned an unknown error.";
                return NexusSsoResult.CreateFailure(error ?? "SSO error returned.");
            }

            if (!root.TryGetProperty("data", out var dataElement))
            {
                return null;
            }

            if (dataElement.TryGetProperty("connection_token", out var tokenElement))
            {
                var token = tokenElement.GetString();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    settings.NexusSsoConnectionToken = token;
                    _saveSettings(settings);
                    status?.Report("Connection confirmed. Please approve the request in your browser.");
                }

                return null;
            }

            if (dataElement.TryGetProperty("api_key", out var apiKeyElement))
            {
                var apiKey = apiKeyElement.GetString();
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    settings.NexusApiKey = apiKey;
                    settings.NexusSsoLinkedAt = DateTimeOffset.UtcNow;
                    settings.NexusSsoConnectionToken = null;
                    settings.NexusSsoRequestId = null;
                    _saveSettings(settings);
                    status?.Report("API key received.");
                    return NexusSsoResult.CreateSuccess(apiKey);
                }

                return NexusSsoResult.CreateFailure("SSO completed without providing an API key.");
            }

            return null;
        }

        private string EnsureRequestId(Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.NexusSsoRequestId))
            {
                return settings.NexusSsoRequestId;
            }

            var requestId = Guid.NewGuid().ToString();
            settings.NexusSsoRequestId = requestId;
            _saveSettings(settings);
            return requestId;
        }

        private void OpenAuthorizePage(string requestId)
        {
            var url = $"{AuthorizeUrl}?id={Uri.EscapeDataString(requestId)}&application={ApplicationSlug}";
            _browserLauncher(url);
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SSO] Failed to launch browser: {ex}");
            }
        }
    }

    internal interface INexusSsoTransport : IAsyncDisposable
    {
        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
        Task SendAsync(string payload, CancellationToken cancellationToken);
        Task<SsoTransportMessage> ReceiveAsync(CancellationToken cancellationToken);
        WebSocketState State { get; }
        Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken);
    }

    internal readonly struct SsoTransportMessage
    {
        private SsoTransportMessage(bool isClosed, string? payload)
        {
            IsClosed = isClosed;
            Payload = payload;
        }

        public bool IsClosed { get; }
        public string? Payload { get; }

        public static SsoTransportMessage Closed => new(true, null);

        public static SsoTransportMessage FromPayload(string payload) => new(false, payload);
    }

    internal sealed class WebSocketNexusSsoTransport : INexusSsoTransport
    {
        private readonly ClientWebSocket _socket = new();
        private readonly byte[] _buffer = new byte[4096];

        public WebSocketState State => _socket.State;

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => _socket.ConnectAsync(uri, cancellationToken);

        public Task SendAsync(string payload, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            return _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }

        public async Task<SsoTransportMessage> ReceiveAsync(CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            while (true)
            {
                var segment = new ArraySegment<byte>(_buffer);
                var result = await _socket.ReceiveAsync(segment, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return SsoTransportMessage.Closed;
                }

                builder.Append(Encoding.UTF8.GetString(_buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    return SsoTransportMessage.FromPayload(builder.ToString());
                }
            }
        }

        public Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken) =>
            _socket.CloseAsync(status, description, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
                }
                catch
                {
                }
            }

            _socket.Dispose();
        }
    }

    public sealed class NexusSsoResult
    {
        private NexusSsoResult(bool success, bool cancelled, string? apiKey, string? errorMessage)
        {
            Success = success;
            IsCancelled = cancelled;
            ApiKey = apiKey;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public bool IsCancelled { get; }
        public string? ApiKey { get; }
        public string? ErrorMessage { get; }

        public static NexusSsoResult CreateSuccess(string apiKey) => new(true, false, apiKey, null);

        public static NexusSsoResult CreateFailure(string? errorMessage) => new(false, false, null, errorMessage);

        public static NexusSsoResult CreateCancelled(string? errorMessage) => new(false, true, null, errorMessage);
    }
}
