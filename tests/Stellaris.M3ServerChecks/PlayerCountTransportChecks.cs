using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Stellaris.Server;

internal static class PlayerCountTransportChecks
{
    public static async Task RunAsync(string root, int count, Action<bool, string> check, string templateId = "economic")
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var url = $"http://127.0.0.1:{port}";
        using var stop = new CancellationTokenSource();
        var server = M3Server.RunAsync(root, url, "M3-test-code", stop.Token);
        var connected = new ConcurrentBag<Peer>();
        var heartbeat = Task.CompletedTask;
        Exception? primaryError = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            for (var attempt = 0; ; attempt++)
            {
                if (server.IsCompleted) await server;
                try { using var health = await http.GetAsync(url + "/health"); if (health.IsSuccessStatusCode) break; }
                catch (HttpRequestException) when (attempt < 300) { }
                catch (TaskCanceledException) when (attempt < 300) { }
                if (attempt >= 300) throw new TimeoutException("Сервер не запустился.");
                await Task.Delay(100);
            }
            heartbeat = KeepAlive();
            var ws = new Uri(url.Replace("http:", "ws:") + "/ws");
            // Наблюдатель создаёт пропуск в player IDs и не должен влиять на голоса.
            var observer = await Join("Наблюдатель", "observer");
            var players = new List<Peer>();
            for (var index = 0; index < count; index++) players.Add(await Join($"Игрок {index + 1}", "player"));
            var host = players[0];
            await Command(host, new { type = "approveObserver", targetParticipantId = observer.Id });
            await Command(host, new { type = "setGalaxySettings", galaxySeed = "m3-real-start", galaxyScenarioId = "tiny", galaxyShapeId = "elliptical", galaxyDensity = 1 });
            foreach (var player in players) await Command(player, new { type = "selectEmpireTemplate", templateId });
            var lobby = await Wait(host, state => state.GetProperty("participants").GetArrayLength() == count + 1);
            var revision = lobby.GetProperty("galaxy").GetProperty("lobbyRevision").GetInt64();
            foreach (var player in players.Take(count - 1)) await Command(player, new { type = "ready", ready = true, expectedLobbyRevision = revision });
            await Command(host, new { type = "start", expectedLobbyRevision = revision }, false);
            await Command(players[^1], new { type = "ready", ready = true, expectedLobbyRevision = revision });
            await Command(host, new { type = "start", expectedLobbyRevision = revision });
            var candidate = await Wait(host, state => state.GetProperty("galaxy").GetProperty("stage").GetString() == "awaitingAssets");
            var profile = candidate.GetProperty("assetProfile");
            var fingerprint = candidate.GetProperty("galaxy").GetProperty("worldFingerprint").GetString();
            foreach (var player in players)
            {
                await Command(player, new { type = "assetReady", ready = true, profileId = profile.GetProperty("profileId").GetString(), profileDigest = profile.GetProperty("profileDigest").GetString(), galaxyWorldFingerprint = fingerprint });
                if (player != players[^1]) check(host.LatestState.GetProperty("phase").GetString() == "lobby", "Частичный импорт не запускает партию");
            }
            await Command(host, new { type = "start", expectedLobbyRevision = revision, profileId = profile.GetProperty("profileId").GetString(), profileDigest = profile.GetProperty("profileDigest").GetString(), galaxyWorldFingerprint = fingerprint });
            foreach (var player in players)
            {
                var running = await Wait(player, state => state.GetProperty("phase").GetString() == "running");
                var colonies = running.GetProperty("development").GetProperty("colonies");
                check(colonies.GetArrayLength() == 1 && colonies[0].GetProperty("ownerParticipantId").GetInt32() == player.Id, "Приватная колония каждого игрока по реальному owner ID");
            }
            await Command(observer, new { type = "assetReady", ready = true, profileId = profile.GetProperty("profileId").GetString(), profileDigest = profile.GetProperty("profileDigest").GetString(), galaxyWorldFingerprint = fingerprint });
            var general = await Wait(observer, state => state.GetProperty("phase").GetString() == "running");
            check(general.GetProperty("planets").GetArrayLength() == count && general.GetProperty("spatial").GetProperty("colonies").GetArrayLength() == count, "Общий снапшот наблюдателя содержит всех игроков и проходит wire budget");
            await Command(host, new { type = "stepDay" });
            var stepped = await Wait(host, state => state.GetProperty("elapsedDay").GetInt32() == 1);
            check(stepped.GetProperty("participants").GetArrayLength() == count + 1, $"Реальный HTTP/WS: {count} игроков и наблюдатель прошли Ready→generation→ACK→running→day1");

            async Task<Peer> Join(string name, string role) { var peer = await Peer.JoinAsync(ws, name, role: role); connected.Add(peer); return peer; }
            async Task Command(Peer peer, object payload, bool accepted = true)
            {
                var fields = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(payload))!;
                fields["commandId"] = Guid.NewGuid().ToString("N");
                var result = await peer.CommandAsync(fields, timeoutSeconds: 30);
                check(result.GetProperty("accepted").GetBoolean() == accepted, payload + " → " + result);
                if (accepted) await Wait(peer, state => state.GetProperty("sequence").GetInt64() >= result.GetProperty("stateSequence").GetInt64());
            }
            async Task KeepAlive()
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
                try { while (await timer.WaitForNextTickAsync(stop.Token)) await Task.WhenAll(connected.Select(peer => peer.PingAsync(timeoutSeconds: 30))); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            }
        }
        catch (Exception error) { primaryError = error; throw; }
        finally
        {
            var earlierHeartbeatFailure = heartbeat.IsFaulted ? heartbeat.Exception : null;
            stop.Cancel();
            try { await heartbeat; }
            catch (Exception) when (stop.IsCancellationRequested) { /* Завершение сервера может закрыть текущий heartbeat. */ }
            foreach (var peer in connected) await peer.DisposeAsync();
            await server;
            if (primaryError is null && earlierHeartbeatFailure is not null) throw earlierHeartbeatFailure;
        }
    }

    private static async Task<JsonElement> Wait(Peer peer, Func<JsonElement, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var state = peer.LatestState;
            if (state.ValueKind == JsonValueKind.Object && predicate(state)) return state;
            await Task.Delay(25, timeout.Token);
        }
    }
}
