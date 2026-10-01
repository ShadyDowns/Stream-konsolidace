using System.IO;
using System.Net.WebSockets;
using System.Text;
using StreamKartoteka.Core;

namespace StreamKartoteka.Services;

public sealed class TwitchIrcClient : IAsyncDisposable
{
    private static readonly Uri TwitchIrcUri = new("wss://irc-ws.chat.twitch.tv:443");
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connectionCancellation;
    private Task? _receiveTask;
    private string _lineRemainder = string.Empty;

    public event EventHandler<ChatEntry>? MessageReceived;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ConnectionFaulted;

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string channel, string login, string accessToken)
    {
        await DisconnectAsync();

        channel = NormalizeName(channel);
        login = NormalizeName(login);
        accessToken = accessToken.Trim();
        if (accessToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = accessToken[6..];
        }

        ValidateCredentials(channel, login, accessToken);

        var cancellation = new CancellationTokenSource();
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        _connectionCancellation = cancellation;
        _socket = socket;
        _lineRemainder = string.Empty;

        StatusChanged?.Invoke(this, $"Připojuji se ke #{channel}…");

        try
        {
            await socket.ConnectAsync(TwitchIrcUri, cancellation.Token);
            await SendLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands", cancellation.Token);
            await SendLineAsync($"PASS oauth:{accessToken}", cancellation.Token);
            await SendLineAsync($"NICK {login}", cancellation.Token);
            await SendLineAsync($"JOIN #{channel}", cancellation.Token);

            _receiveTask = ReceiveLoopAsync(channel, socket, cancellation.Token);
        }
        catch
        {
            await DisconnectAsync();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        var cancellation = _connectionCancellation;
        var socket = _socket;
        var receiveTask = _receiveTask;

        _connectionCancellation = null;
        _socket = null;
        _receiveTask = null;

        if (cancellation is null && socket is null)
        {
            return;
        }

        try
        {
            cancellation?.Cancel();
            if (socket?.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Odpojení", timeout.Token);
            }
        }
        catch (Exception) when (cancellation?.IsCancellationRequested == true || socket is not null)
        {
            // Closing is best-effort; sockets are disposed below.
        }

        if (receiveTask is not null && Task.CurrentId != receiveTask.Id)
        {
            try
            {
                await receiveTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
                // Cancellation and remote close are both expected here.
            }
        }

        socket?.Dispose();
        cancellation?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _sendLock.Dispose();
    }

    private async Task ReceiveLoopAsync(string channel, ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var frame = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        throw new WebSocketException("Twitch ukončil spojení.");
                    }

                    frame.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var payload = _lineRemainder + Encoding.UTF8.GetString(frame.ToArray());
                var lines = payload.Split("\r\n", StringSplitOptions.None);
                _lineRemainder = lines[^1];

                foreach (var line in lines[..^1])
                {
                    await HandleLineAsync(channel, line, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Deliberate disconnect.
        }
        catch (Exception exception)
        {
            ConnectionFaulted?.Invoke(this, exception.Message);
        }
    }

    private async Task HandleLineAsync(string channel, string line, CancellationToken cancellationToken)
    {
        if (line.StartsWith("PING ", StringComparison.Ordinal))
        {
            await SendLineAsync("PONG " + line[5..], cancellationToken);
            return;
        }

        if (line.Contains(" 001 ", StringComparison.Ordinal))
        {
            StatusChanged?.Invoke(this, $"Připojeno k #{channel}");
            return;
        }

        if (line.Contains("Login authentication failed", StringComparison.OrdinalIgnoreCase))
        {
            ConnectionFaulted?.Invoke(this, "Twitch odmítl přihlášení. Zkontrolujte účet a OAuth token.");
            return;
        }

        if (line.Contains(" RECONNECT", StringComparison.Ordinal))
        {
            ConnectionFaulted?.Invoke(this, "Twitch požádal o nové připojení. Připojte aplikaci znovu.");
            return;
        }

        if (IrcMessageParser.TryParsePrivMsg(line, out var message) && message is not null)
        {
            MessageReceived?.Invoke(this, message);
        }
    }

    private async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new InvalidOperationException("Spojení není otevřené.");
        var bytes = Encoding.UTF8.GetBytes(line + "\r\n");

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static string NormalizeName(string value) =>
        value.Trim().TrimStart('#').ToLowerInvariant();

    private static void ValidateCredentials(string channel, string login, string accessToken)
    {
        static bool IsValidName(string value) =>
            value.Length is >= 1 and <= 25 && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

        if (!IsValidName(channel))
        {
            throw new ArgumentException("Název kanálu může obsahovat jen písmena, číslice a podtržítko.");
        }

        if (!IsValidName(login))
        {
            throw new ArgumentException("Přihlašovací jméno Twitch není platné.");
        }

        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("OAuth token chybí nebo obsahuje nepovolenou mezeru.");
        }
    }
}
