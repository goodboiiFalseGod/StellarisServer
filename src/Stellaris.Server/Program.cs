using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Stellaris.Core.Content;
using Stellaris.Session;
using Stellaris.Server;

return await ServerMain.Run(args);

internal static class ServerMain
{
    private const int MAX_BYTES = 16 * 1024;
    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private static readonly HashSet<string> TRAITS = new(StringComparer.Ordinal) { "trait_ingenious", "trait_industrious", "trait_agrarian" };

    public static async Task<int> Run(string[] arguments)
    {
        try
        {
            var options = ParseOptions(arguments);
            var root = options.GetValueOrDefault("--game-root") ?? throw new ArgumentException("Нужен --game-root PATH к установке Stellaris 4.5.2.");
            var url = options.GetValueOrDefault("--urls", "http://127.0.0.1:5080");
            var mode = options.GetValueOrDefault("--mode", "m2");
            if (mode is not "m2" and not "m3")
            {
                throw new ArgumentException("Допустимые режимы --mode: m2, m3.");
            }
            if (mode == "m3")
            {
                var m3Code = options.GetValueOrDefault("--join-code", Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
                return await M3Server.RunAsync(root, url, m3Code);
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "http" || address.AbsolutePath != "/")
            {
                throw new ArgumentException("M2: --urls должен содержать один HTTP-адрес локального сервера.");
            }
            var joinCode = options.GetValueOrDefault("--join-code", Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
            if (joinCode.Length is < 8 or > 64)
            {
                throw new ArgumentException("Код доступа должен содержать 8..64 символа.");
            }
            var contentSession = new ContentSession(root);
            var content = new NeutralEconomyImporter(contentSession).Import(100);
            var traits = TRAITS.ToDictionary(id => id, id => new TraitWorkforceImporter(contentSession).Import([id], content.Jobs), StringComparer.Ordinal);
            contentSession.VerifyStable();
            var session = new GameSession(content, traits);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
            });
            builder.WebHost.UseUrls(url);
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
                context.Response.Headers.CacheControl = "no-store";
                await next(context);
            });
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapGet("/health", () => Results.Json(new { status = "ok", protocol = 1 }));
            app.Map("/ws", context => HandleSocket(context, session, joinCode, app.Logger));
            using var ticking = new CancellationTokenSource();
            var ticker = TickLoop(session, ticking.Token, app.Logger);
            Console.WriteLine($"M2: {url}");
            Console.WriteLine($"Код доступа: {joinCode}");
            Console.WriteLine("Только локальный ПК/доверенная LAN. Перезапуск завершает сессию. Не публикуйте порт в Интернет.");
            try
            {
                await app.RunAsync();
            }
            finally
            {
                ticking.Cancel();
                await ticker;
                await app.DisposeAsync();
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Ошибка запуска M2: {exception.Message}");
            return 1;
        }
    }

    private static async Task TickLoop(GameSession session, CancellationToken cancellation, ILogger logger)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                session.Tick(DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            logger.LogInformation("Месячный таймер остановлен при завершении сервера.");
        }
    }

    private static async Task HandleSocket(HttpContext context, GameSession session, string joinCode, ILogger logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) || originUri.Authority != context.Request.Host.Value || originUri.Scheme != context.Request.Scheme))
        {
            context.Response.StatusCode = 403;
            return;
        }
        var participant = 0;
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            try
            {
                var initial = await Receive(socket, TimeSpan.FromSeconds(10), lifetime.Token);
                var join = JsonSerializer.Deserialize<JoinMessage>(initial, JSON) ?? throw new InvalidDataException("Пустое сообщение входа.");
                if (join.Type != "join" || join.Protocol != 1 || join.JoinCode == null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(join.JoinCode), Encoding.UTF8.GetBytes(joinCode)))
                {
                    throw new InvalidDataException("Неверный код доступа или версия протокола.");
                }
                var grant = session.Join(join.Name ?? "", join.Role ?? "", join.Token, DateTimeOffset.UtcNow);
                participant = grant.ParticipantId;
                await Send(socket, new { type = "welcome", protocol = 1, participantId = grant.ParticipantId, token = grant.Token, sessionId = grant.SessionId }, lifetime.Token);
                var outgoing = Channel.CreateBounded<object>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
                var sender = SendLoop(socket, outgoing.Reader, lifetime);
                var publisher = PublishLoop(session, participant, outgoing.Writer, lifetime);
                try
                {
                    var window = DateTimeOffset.UtcNow;
                    var received = 0;
                    while (!lifetime.IsCancellationRequested)
                    {
                        var raw = await Receive(socket, TimeSpan.FromSeconds(30), lifetime.Token);
                        var now = DateTimeOffset.UtcNow;
                        if (now - window >= TimeSpan.FromSeconds(1))
                        {
                            window = now;
                            received = 0;
                        }
                        if (++received > 20)
                        {
                            throw new InvalidDataException("Превышен лимит 20 сообщений в секунду.");
                        }
                        using var document = JsonDocument.Parse(raw);
                        if (document.RootElement.TryGetProperty("type", out var type) && type.GetString() == "ping")
                        {
                            Enqueue(outgoing.Writer, new { type = "pong" });
                            continue;
                        }
                        var command = JsonSerializer.Deserialize<ClientCommand>(raw, JSON) ?? throw new InvalidDataException("Пустая команда.");
                        var result = session.Execute(participant, command, now);
                        Enqueue(outgoing.Writer, new { type = "result", result });
                    }
                }
                finally
                {
                    lifetime.Cancel();
                    outgoing.Writer.TryComplete();
                    socket.Abort();
                    await Task.WhenAll(sender, publisher);
                }
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
            {
                // Текст исключения ядра не отправляется: он может содержать неразрешённые данные.
                logger.LogInformation("Соединение M2 закрыто: {Reason}", exception.GetType().Name);
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await Send(socket, new { type = "error", message = "Подключение отклонено или потеряно: проверьте код, роль, токен и формат сообщения." }, context.RequestAborted);
                        await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "M2 connection ended", context.RequestAborted);
                    }
                    catch (Exception closing) when (closing is WebSocketException or OperationCanceledException)
                    {
                        logger.LogDebug("Закрытие уже недоступного соединения: {Reason}", closing.GetType().Name);
                    }
                }
            }
        }
        finally
        {
            if (participant != 0)
            {
                session.Disconnect(participant, DateTimeOffset.UtcNow);
            }
        }
    }

    private static void Enqueue(ChannelWriter<object> writer, object value)
    {
        if (!writer.TryWrite(value))
        {
            throw new InvalidDataException("Клиент не успевает получать сообщения.");
        }
    }

    private static async Task PublishLoop(GameSession session, int id, ChannelWriter<object> writer, CancellationTokenSource lifetime)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        long sequence = -1;
        try
        {
            do
            {
                var state = session.View(id);
                if (state.Sequence != sequence)
                {
                    Enqueue(writer, new { type = "snapshot", state });
                    sequence = state.Sequence;
                }
            }
            while (await timer.WaitForNextTickAsync(lifetime.Token));
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (InvalidDataException)
        {
            lifetime.Cancel();
        }
    }

    private static async Task SendLoop(WebSocket socket, ChannelReader<object> reader, CancellationTokenSource lifetime)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(lifetime.Token))
            {
                await Send(socket, message, lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            lifetime.Cancel();
        }
    }

    private static async Task Send(WebSocket socket, object message, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, JSON), WebSocketMessageType.Text, true, timeout.Token);
    }

    private static async Task<byte[]> Receive(WebSocket socket, TimeSpan timeout, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token);
            if (result.MessageType != WebSocketMessageType.Text || data.Length + result.Count > MAX_BYTES)
            {
                throw new InvalidDataException("Ожидалось текстовое сообщение до 16 KiB.");
            }
            data.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return data.ToArray();
    }

    private static Dictionary<string, string> ParseOptions(string[] arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (arguments.Length % 2 != 0)
        {
                throw new ArgumentException("Параметры: --game-root PATH [--mode m2|m3] [--urls http://127.0.0.1:5080] [--join-code CODE].");
        }
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (arguments[index] is not "--game-root" and not "--urls" and not "--join-code" and not "--mode" || !result.TryAdd(arguments[index], arguments[index + 1]))
            {
                throw new ArgumentException("Неизвестный либо повторный параметр сервера.");
            }
        }
        return result;
    }

    private sealed record JoinMessage(string? Type, int Protocol, string? Name, string? Role, string? Token, string? JoinCode);
}
