using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Stellaris.Core.Assets;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Galaxy;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

namespace Stellaris.Server;

public static class M3Server
{
    private static M3GalaxyRuntime CreateGalaxyRuntime(string gameRoot)
    {
        var worker = Environment.GetEnvironmentVariable("STELLARIS_TRIANGLE_WORKER");
        var sha = Environment.GetEnvironmentVariable("STELLARIS_TRIANGLE_WORKER_SHA256");
        if (string.IsNullOrWhiteSpace(worker) || string.IsNullOrWhiteSpace(sha))
            throw new InvalidOperationException("Для generated M3 нужны STELLARIS_TRIANGLE_WORKER и STELLARIS_TRIANGLE_WORKER_SHA256.");
        return new M3GalaxyRuntime(GalaxyLobbyCatalog.Import(gameRoot, new([], [])),
            new NativeTriangleBackend(worker, sha, Path.Combine(Path.GetTempPath(), "stellaris-m3-triangle-" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(30)));
    }

    public static async Task<int> RunAsync(string gameRoot, string url, string joinCode, CancellationToken cancellationToken = default,
        M3GalaxyRuntime? galaxy = null, Func<IReadOnlyList<string>, IReadOnlyList<string>, M3AssetManifest>? galaxyManifest = null)
    {
        ValidateLoopbackUrl(url);
        if (joinCode.Length is < 8 or > 64)
        {
            throw new ArgumentException("Код доступа должен содержать 8..64 символа.");
        }
        var contentSession = new ContentSession(gameRoot);
        var content = new NeutralEconomyImporter(contentSession).Import(100);
        var traits = new[] { "trait_ingenious", "trait_industrious", "trait_agrarian" }.ToDictionary(id => id, id => new TraitWorkforceImporter(contentSession).Import([id], content.Jobs), StringComparer.Ordinal);
        contentSession.VerifyStable();
        var manifest = await new M3AssetManifestBuilder().BuildAsync(gameRoot, cancellationToken);
        M3WireCodec.Serialize(manifest);
        galaxy ??= CreateGalaxyRuntime(gameRoot);
        galaxyManifest ??= (classes, objectIds) => new M3AssetManifestBuilder().BuildAsync(gameRoot,
            new GeneratedWorldVisualRequirements(classes, objectIds.Count == 0 ? null : objectIds), cancellationToken).GetAwaiter().GetResult();
        ConstructionProjection? construction = null;
        try
        {
            construction = await new ConstructionDefinitionImporter().ImportAsync(gameRoot, cancellationToken);
        }
        catch (ConstructionImportException)
        {
            Console.Error.WriteLine("Warning: construction source profile is unavailable; existing M3 gameplay remains enabled.");
        }
        var startup = await M3StartupCatalog.ImportAsync(gameRoot, cancellationToken);
        var sourceEconomyCatalog = await new PlanetEconomyImporter().ImportAsync(gameRoot, cancellationToken);
        var constructionContent = await M3ConstructionContent.ImportAsync(gameRoot, sourceEconomyCatalog, cancellationToken);
        var researchCatalog = new Stellaris.Core.Research.TechnologyImporter().Import(gameRoot);
        var technologyKeys = M3TechnologyKeys.Import(gameRoot);
        var startPopulation = new StartupPopulationResourceImporter().Import(gameRoot);
        var sourceTraitBonuses = new StartupTraitWorkforceImporter().Import(gameRoot);
        var sourceEconomy = new M3BaselineEconomyProfile(sourceEconomyCatalog, startPopulation, sourceTraitBonuses.BonusesByTemplate, researchCatalog.CategoryParents,
            founderSpecies: new StartupFounderSpeciesImporter().Import(gameRoot));
        var rareDepositPolicy = M3RareDepositResearchPolicy.Import(gameRoot);
        Console.WriteLine("Политика весов исследований: " + rareDepositPolicy.Manifest.Profile + "; " + string.Join(" ", rareDepositPolicy.Manifest.Limitations));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory, WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot") });
        builder.WebHost.UseUrls(url);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        await using var app = builder.Build();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, app.Lifetime.ApplicationStopping);
        var session = new M3GameSession(content, traits, manifest, construction, constructionImportAttempted: true, startup, sourceEconomy, constructionContent, researchCatalog, technologyKeys: technologyKeys,
            galaxy: galaxy, galaxyManifest: (classes, objectIds) => {
                var generated = galaxyManifest(classes, objectIds);
                M3WireCodec.Serialize(generated);
                return generated;
            }, galaxyValidate: (candidate, generatedManifest) => {
                if (generatedManifest.GeneratedRequirements is not { } requirements || !requirements.ClassIds.SequenceEqual(candidate.ClassIds) ||
                    !(requirements.ObjectIds ?? Array.Empty<string>()).SequenceEqual(candidate.ObjectIds))
                    throw new GeneratedWorldVisualException("requirements-mismatch", null, "Профиль не соответствует полному candidate.");
                ValidateSystemLighting(gameRoot, candidate.Generated.Systems.Select(system => system.StarClassId).ToArray(), generatedManifest, cancellationToken);
            }, rareDepositResearchPolicy: rareDepositPolicy);
        var runtime = new Runtime(session, joinCode, shutdown.Token, app.Logger);
        const string importMap = "{\"imports\":{\"three\":\"/m3-vendor/three.module.js\",\"three/addons/\":\"/m3-vendor/addons/\"}}";
        var importHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(importMap)));
        app.Use(async (context, next) =>
        {
            if (context.Request.Host.Host.ToLowerInvariant() is not ("localhost" or "127.0.0.1" or "[::1]" or "::1"))
            {
                context.Response.StatusCode = 403;
                return;
            }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self' 'sha256-{importHash}'; style-src 'self'; worker-src 'self'; img-src 'self' blob: data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });
        app.UseStaticFiles();
        app.MapGet("/", () => Results.Redirect("/m3.html"));
        app.MapGet("/health", () => Results.Json(new { status = "ok", protocol = 2, mode = "m3" }));
        app.MapGet("/m3/asset-manifest", () => Results.Bytes(M3WireCodec.Serialize(session.CurrentManifest), "application/json; charset=utf-8"));
        app.Map("/ws", runtime.HandleAsync);
        Console.WriteLine($"M3: {url}");
        Console.WriteLine($"Код доступа: {joinCode}");
        Console.WriteLine("Только текущий ПК. Перезапуск завершает сессию; ассеты читаются браузером локально.");
        var ticker = runtime.TickAsync(app.Lifetime.StopApplication);
        try
        {
            await app.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            app.Logger.LogInformation("M3 остановлен запросом владельца.");
        }
        finally
        {
            shutdown.Cancel();
            await ticker;
            await runtime.DrainAsync();
        }
        return 0;
    }

    private static void ValidateSystemLighting(string gameRoot, IReadOnlyList<string> classes, M3AssetManifest manifest, CancellationToken cancellationToken)
    {
        async Task<byte[]> Read(string relative, CancellationToken token)
        {
            AssetManifestCanonical.ValidateRelativePath(relative);
            var file = manifest.Files.SingleOrDefault(item => item.RelativePath == relative)
                ?? throw new GeneratedWorldVisualException("lighting-source-missing", null, "Lighting source отсутствует в manifest.");
            var path = Path.GetFullPath(Path.Combine(gameRoot, relative));
            for (FileSystemInfo? part = new FileInfo(path); part is not null; part = part is FileInfo info ? info.Directory : ((DirectoryInfo)part).Parent)
                if ((part.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new GeneratedWorldVisualException("lighting-source-path", null, "Reparse lighting source запрещён.");
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (stream.Length != file.ByteLength || stream.Length > M3AssetManifestBuilder.MAX_FILE_BYTES)
                throw new GeneratedWorldVisualException("lighting-source-length", null, "Lighting source изменился после manifest.");
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, token);
            var bytes = output.ToArray();
            if (bytes.LongLength != file.ByteLength || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != file.Sha256)
                throw new GeneratedWorldVisualException("lighting-source-hash", null, "Lighting source изменился после manifest.");
            return bytes;
        }
        var lighting = GeneratedWorldSystemLighting.ResolveAsync(Read, cancellationToken).GetAwaiter().GetResult();
        foreach (var starClass in classes)
            if (!lighting.ContainsKey(starClass))
                throw new GeneratedWorldVisualException("system-lighting-unavailable", starClass, "Actual star class не имеет поддержанного source lighting.");
    }

    public static Uri ValidateLoopbackUrl(string url)
    {
        if (!Regex.IsMatch(url, @"\Ahttp://(?:localhost|127\.0\.0\.1|\[::1\])(?::[0-9]{1,5})?/?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Port is < 1 or > 65535)
        {
            throw new ArgumentException("M3 требует один HTTP loopback URL без дополнительных компонентов.");
        }
        return uri;
    }

    internal sealed class Runtime
    {
        private readonly M3GameSession _session;
        private readonly byte[] _joinCode;
        private readonly CancellationToken _shutdown;
        private readonly ILogger _logger;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly object _gate = new();
        private readonly M3SimulationRate _simulationRate = new();
        private readonly Func<TimeSpan> _diagnosticNow;
        private readonly ConcurrentDictionary<long, TaskCompletionSource> _requests = new();
        private readonly Dictionary<int, ActiveConnection> _connections = new();
        private long _requestId;

        public Runtime(M3GameSession session, string joinCode, CancellationToken shutdown, ILogger logger, Func<TimeSpan>? diagnosticNow = null)
        {
            _session = session;
            _joinCode = Encoding.UTF8.GetBytes(joinCode);
            _shutdown = shutdown;
            _logger = logger;
            _diagnosticNow = diagnosticNow ?? (() => _clock.Elapsed);
        }

        internal void TickOnce(TimeSpan now)
        {
            lock (_gate)
            {
                var before = _session.ObserveTiming();
                _session.Tick(now);
                var after = _session.ObserveTiming();
                _simulationRate.Observe(DiagnosticNow(), after.Phase, after.Paused, completed: after.ElapsedDay - before.ElapsedDay == 1);
            }
        }

        internal M3CommandResult ExecuteOnce(int id, string connectionId, M3ClientCommand command, TimeSpan now)
        {
            lock (_gate)
            {
                var before = _session.ObserveTiming();
                var result = _session.Execute(id, connectionId, command, now);
                if (result.Accepted && command.Type == "kickParticipant" && command.TargetParticipantId is { } targetId &&
                    _connections.TryGetValue(targetId, out var target) && target.ConnectionId != connectionId)
                {
                    target.Outbox.CompleteWithFinalReply(M3WireCodec.Serialize(new
                    {
                        type = "error",
                        code = "LOBBY_REMOVED",
                        message = "Хост исключил вас из лобби. Чтобы присоединиться, войдите заново."
                    }));
                    _snapshotCache.Remove(targetId);
                    target.Removed.TrySetResult();
                }
                var after = _session.ObserveTiming();
                var reset = result.Accepted && after.CommandSequence > before.CommandSequence && command.Type is "start" or "pause" or "speed";
                _simulationRate.Observe(DiagnosticNow(), after.Phase, after.Paused, reset);
                return result;
            }
        }

        private readonly Dictionary<int, M3SessionView> _snapshotCache = new();

        internal M3SessionView Snapshot(int id)
        {
            lock (_gate)
            {
                // На паузе переиспользуем source-проекции; изменения прав и ракурса меняют sequence.
                if (!_snapshotCache.TryGetValue(id, out var view) || view.Sequence != _session.SnapshotSequence)
                {
                    view = _session.View(id);
                    _snapshotCache[id] = view;
                }
                return view with { SimulationRate = _simulationRate.Observe(DiagnosticNow(), view.Phase, view.Paused) };
            }
        }

        private M3SessionView? Snapshot(int id, string connectionId)
        {
            lock (_gate)
            {
                if (!_session.IsCurrentConnection(id, connectionId)) return null;
                if (!_snapshotCache.TryGetValue(id, out var view) || view.Sequence != _session.SnapshotSequence)
                {
                    view = _session.View(id);
                    _snapshotCache[id] = view;
                }
                return view with { SimulationRate = _simulationRate.Observe(DiagnosticNow(), view.Phase, view.Paused) };
            }
        }

        private TimeSpan DiagnosticNow()
        {
            try { return _diagnosticNow(); }
            catch (Exception exception)
            {
                _logger.LogWarning("M3 diagnostic clock unavailable: {Type}.", exception.GetType().Name);
                return TimeSpan.MinValue;
            }
        }

        public async Task TickAsync(Action stop)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            try
            {
                while (await timer.WaitForNextTickAsync(_shutdown))
                {
                    lock (_gate) { TickOnce(_clock.Elapsed); }
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                _logger.LogInformation("M3: таймер остановлен.");
            }
            catch (Exception exception)
            {
                _logger.LogError("M3: таймер завершился ошибкой {Type}.", exception.GetType().Name);
                stop();
            }
        }

        public async Task DrainAsync()
        {
            await Task.WhenAll(_requests.Values.Select(source => source.Task));
        }

        public async Task HandleAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var address) || address.Scheme != context.Request.Scheme || !string.Equals(address.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase) || address.AbsolutePath != "/" || address.Query.Length > 0 || address.Fragment.Length > 0 || address.UserInfo.Length > 0))
            {
                context.Response.StatusCode = 403;
                return;
            }
            if (_shutdown.IsCancellationRequested)
            {
                context.Response.StatusCode = 429;
                return;
            }
            var requestId = Interlocked.Increment(ref _requestId);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests[requestId] = completion;
            M3JoinGrant? grant = null;
            ActiveConnection? activeConnection = null;
            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, _shutdown);
                using var outbox = new M3Outbox();
                Task sender = Task.CompletedTask;
                Task publisher = Task.CompletedTask;
                try
                {
                    var rate = new M3RateLimiter();
                    var join = M3WireCodec.ParseJoin(await ReceiveAsync(socket, TimeSpan.FromSeconds(10), lifetime.Token));
                    rate.Accept(_clock.Elapsed);
                    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(join.JoinCode), _joinCode))
                    {
                        throw new InvalidDataException("Неверный код доступа.");
                    }
                    lock (_gate)
                    {
                        grant = _session.Join(join.Name, join.Role, join.Token, _clock.Elapsed);
                        activeConnection = new ActiveConnection(grant.ConnectionId, outbox);
                        _connections[grant.ParticipantId] = activeConnection;
                    }
                    await SendAsync(socket, M3WireCodec.Serialize(new { type = "welcome", protocol = 2, participantId = grant.ParticipantId, token = grant.Token, sessionId = grant.SessionId, profileId = grant.ProfileId, profileDigest = grant.ProfileDigest }), lifetime.Token);
                    sender = GuardAsync(() => SendLoopAsync(socket, outbox, lifetime.Token), lifetime, socket);
                    publisher = GuardAsync(() => PublishAsync(grant.ParticipantId, grant.ConnectionId, outbox, lifetime.Token), lifetime, socket);
                    while (!lifetime.IsCancellationRequested)
                    {
                        using var receiveLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        var receive = ReceiveAsync(socket, TimeSpan.FromSeconds(30), receiveLifetime.Token);
                        if (activeConnection is not null && await Task.WhenAny(receive, activeConnection.Removed.Task) != receive)
                        {
                            await sender;
                            try
                            {
                                using var closeDeadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                                closeDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                                await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "Исключён из лобби.", closeDeadline.Token);
                            }
                            catch (Exception closeError) when (closeError is WebSocketException or OperationCanceledException or InvalidOperationException)
                            {
                                socket.Abort();
                            }
                            try { await receive.WaitAsync(TimeSpan.FromSeconds(3)); }
                            catch (TimeoutException)
                            {
                                receiveLifetime.Cancel();
                                try { await receive; }
                                catch (Exception receiveError) when (receiveError is OperationCanceledException or WebSocketException or InvalidDataException) { }
                            }
                            catch (Exception receiveError) when (receiveError is OperationCanceledException or WebSocketException or InvalidDataException) { }
                            lifetime.Cancel();
                            break;
                        }
                        var bytes = await receive;
                        if (!rate.Accept(_clock.Elapsed))
                        {
                            throw new InvalidDataException("Превышен темп сообщений.");
                        }
                        var message = M3WireCodec.ParseMessage(bytes);
                        if (message.IsPing)
                        {
                            outbox.EnqueueReply(M3WireCodec.Serialize(new { type = "pong" }));
                            continue;
                        }
                        M3CommandResult result;
                        lock (_gate)
                        {
                            result = ExecuteOnce(grant.ParticipantId, grant.ConnectionId, message.Command!, _clock.Elapsed);
                        }
                        outbox.EnqueueReply(M3WireCodec.Serialize(new { type = "result", result }));
                    }
                }
                catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException or WebSocketException or OperationCanceledException)
                {
                    _logger.LogWarning("M3: соединение завершено ({Type}).", exception.GetType().Name);
                    if (grant == null && socket.State == WebSocketState.Open && !lifetime.IsCancellationRequested)
                    {
                        try
                        {
                            await RejectJoinAsync(socket, lifetime.Token);
                        }
                        catch (Exception sendError) when (sendError is WebSocketException or OperationCanceledException)
                        {
                            _logger.LogWarning("M3: ответ отказа не доставлен ({Type}).", sendError.GetType().Name);
                        }
                    }
                }
                finally
                {
                    lifetime.Cancel();
                    outbox.Complete();
                    if (socket.State is not (WebSocketState.Closed or WebSocketState.Aborted))
                        socket.Abort();
                    await Task.WhenAll(sender, publisher);
                }
            }
            finally
            {
                if (grant != null)
                {
                    lock (_gate)
                    {
                        if (_connections.TryGetValue(grant.ParticipantId, out var connection) && connection.ConnectionId == grant.ConnectionId)
                        {
                            _connections.Remove(grant.ParticipantId);
                            _snapshotCache.Remove(grant.ParticipantId);
                        }
                        _session.Disconnect(grant.ParticipantId, grant.ConnectionId, _clock.Elapsed);
                    }
                }
                completion.SetResult();
                _requests.TryRemove(requestId, out _);
            }
        }

        private async Task PublishAsync(int id, string connectionId, M3Outbox outbox, CancellationToken cancellation)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            do
            {
                var view = Snapshot(id, connectionId);
                if (view is null) return;
                outbox.ReplaceSnapshot(M3WireCodec.Serialize(new { type = "snapshot", state = view }));
            }
            while (await timer.WaitForNextTickAsync(cancellation));
        }

        private sealed class ActiveConnection(string connectionId, M3Outbox outbox)
        {
            public string ConnectionId { get; } = connectionId;
            public M3Outbox Outbox { get; } = outbox;
            public TaskCompletionSource Removed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private async Task GuardAsync(Func<Task> operation, CancellationTokenSource lifetime, WebSocket socket)
        {
            try
            {
                await operation();
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                _logger.LogInformation("M3: фоновая операция отменена.");
            }
            catch (Exception exception)
            {
                _logger.LogWarning("M3: фоновая операция завершена ({Type}).", exception.GetType().Name);
                lifetime.Cancel();
                socket.Abort();
            }
        }
    }

    private static async Task SendLoopAsync(WebSocket socket, M3Outbox outbox, CancellationToken cancellation)
    {
        while (await outbox.TakeAsync(cancellation) is { } bytes)
        {
            await SendAsync(socket, bytes, cancellation);
        }
    }

    private static async Task RejectJoinAsync(WebSocket socket, CancellationToken cancellation)
    {
        try
        {
            await SendAsync(socket, M3WireCodec.Serialize(new { type = "error", message = "Вход отклонён: проверьте protocol=2, код доступа и данные участника." }), cancellation);
            // Дождаться подтверждения close: немедленный Abort может сбросить уже отправленный error.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Вход отклонён.", deadline.Token);
        }
        catch
        {
            socket.Abort();
            throw;
        }
    }

    private static async Task SendAsync(WebSocket socket, byte[] bytes, CancellationToken cancellation)
    {
        M3WireCodec.ValidateOutgoing(bytes);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
    }

    private static async Task<byte[]> ReceiveAsync(WebSocket socket, TimeSpan timeout, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token);
            if (result.MessageType != WebSocketMessageType.Text || stream.Length + result.Count > M3WireCodec.MAX_INCOMING_BYTES)
            {
                throw new InvalidDataException("Нужен текст UTF-8 размером до 16 KiB.");
            }
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return stream.ToArray();
    }
}
