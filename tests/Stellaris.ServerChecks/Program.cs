using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

try
{
    var url = args.Length is 2 or 3 ? args[0].TrimEnd('/') : throw new ArgumentException("ServerChecks URL JOIN_CODE [PLAYER_COUNT >= 2] (только отдельная тестовая сессия).");
    var code = args[1];
    var playerCount = args.Length == 3 ? int.Parse(args[2]) : 20;
    Check(playerCount >= 2, "Этот интеграционный сценарий требует минимум двух игроков");
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    var index = await http.GetAsync(url);
    var indexSource = await index.Content.ReadAsStringAsync();
    Check(index.IsSuccessStatusCode && indexSource.Contains("data-testid"), "Клиент раздаётся по HTTP");
    var script = await http.GetAsync(url + "/app.js");
    var scriptSource = await script.Content.ReadAsStringAsync();
    Check(script.IsSuccessStatusCode, "JS доступен");
    Check(indexSource.Contains("Содержание профессий") && indexSource.Contains("Содержание зданий") && indexSource.Contains("Общий расход"), "Таблица показывает раздельные источники расходов");
    Check(scriptSource.Contains("[\"produced\", \"jobUpkeep\", \"buildingUpkeep\", \"upkeep\", \"net\"]"), "Клиент выводит все серверные поля расходов без локального суммирования");
    await RejectHandshake(url, JsonSerializer.Serialize(new { type = "join", protocol = 1, name = "Bad code", role = "player", joinCode = "wrong-code" }));
    await RejectHandshake(url, new string('x', 17000));
    Console.WriteLine("PASS: неверный код и сообщение больше 16 KiB отклонены");
    using (var wrongOrigin = new ClientWebSocket())
    {
        wrongOrigin.Options.SetRequestHeader("Origin", "http://untrusted.invalid");
        try
        {
            await wrongOrigin.ConnectAsync(new Uri(url.Replace("http:", "ws:") + "/ws"), CancellationToken.None);
            throw new InvalidOperationException("Чужой Origin принят.");
        }
        catch (WebSocketException)
        {
            Console.WriteLine("PASS: чужой Origin отклонён");
        }
    }
    using var first = await Peer.Join(url, code, "Первый", "player");
    using var second = await Peer.Join(url, code, "Второй", "player");
    using var additional = new PeerGroup();
    for (var i = 2; i < playerCount; i++)
    {
        additional.Items.Add(await Peer.Join(url, code, $"Игрок {i + 1}", "player"));
    }
    using var observer = await Peer.Join(url, code, "Наблюдатель", "observer");
    var pending = await observer.State(state => true);
    Check(pending.GetProperty("planets").GetArrayLength() == 0, "Неутверждённому наблюдателю экономика не передаётся");
    await first.Command(new { commandId = "approve", type = "approveObserver", targetParticipantId = observer.Id }, true);
    await first.Command(new { commandId = "config", type = "configure", traits = new[] { "trait_ingenious" }, buildings = new[] { "building_energy_grid" } }, true);
    await first.Command(new { commandId = "r1", type = "ready", ready = true }, true);
    await second.Command(new { commandId = "r2", type = "ready", ready = true }, true);
    if (additional.Items.Count > 0)
    {
        await first.Command(new { commandId = "not-all-ready", type = "start" }, false);
    }
    foreach (var peer in additional.Items)
    {
        await peer.Command(new { commandId = "ready", type = "ready", ready = true }, true);
    }
    await first.Command(new { commandId = "start", type = "start" }, true);
    var own = await first.State(state => state.GetProperty("phase").GetString() == "running");
    var other = await second.State(state => state.GetProperty("phase").GetString() == "running");
    var all = await observer.State(state => state.GetProperty("phase").GetString() == "running");
    Check(own.GetProperty("planets").GetArrayLength() == 1 && other.GetProperty("planets").GetArrayLength() == 1 && all.GetProperty("planets").GetArrayLength() == playerCount, "Снапшоты фильтруются по роли");
    foreach (var peer in additional.Items)
    {
        var view = await peer.State(state => state.GetProperty("phase").GetString() == "running");
        Check(view.GetProperty("planets").GetArrayLength() == 1 && view.GetProperty("planets")[0].GetProperty("ownerId").GetInt32() == peer.Id, "Дополнительный игрок видит только свою планету");
    }
    var ownPlanet = own.GetProperty("planets")[0].GetProperty("id").GetInt32();
    var otherPlanet = other.GetProperty("planets")[0].GetProperty("id").GetInt32();
    Check(!other.GetRawText().Contains("trait_ingenious" + "\"]"), "Чужие настройки не находятся в снапшоте");
    await observer.Command(new { commandId = "observe-write", type = "step" }, false);
    var foreign = await first.Command(new { commandId = "foreign", type = "setJobs", planetId = otherPlanet, job = "technician", count = 0 }, false);
    var missing = await first.Command(new { commandId = "missing", type = "setJobs", planetId = -123, job = "technician", count = 0 }, false);
    Check(foreign.GetProperty("message").GetString() == missing.GetProperty("message").GetString(), "Ошибка не раскрывает существование чужой планеты");
    await second.Command(new { commandId = "not-host", type = "pause", paused = false }, false);
    await first.Command(new { commandId = "step1", type = "step" }, true);
    var month1 = await first.State(state => state.GetProperty("month").GetInt32() == 1);
    var report = month1.GetProperty("planets")[0].GetProperty("report");
    var allMonth1 = await observer.State(state => state.GetProperty("month").GetInt32() == 1);
    Check(allMonth1.GetProperty("planets").EnumerateArray().All(planet => planet.GetProperty("reportMonth").GetInt32() == 1 && planet.GetProperty("balances").GetProperty("energy").GetDecimal() > 1000), "Экономика всех игроков продвинулась");
    var energy = report.GetProperty("produced").GetProperty("energy").GetDecimal();
    Check(energy == 14.95m, "Реальный trait/building влияют на серверный выпуск: 14.95");
    Check(report.GetProperty("jobUpkeep").GetProperty("energy").GetDecimal() == 0, "Нейтральный technician не создаёт JobUpkeep");
    Check(report.GetProperty("buildingUpkeep").GetProperty("energy").GetDecimal() == 2, "Сервер передаёт отдельное содержание здания");
    Check(report.GetProperty("upkeep").GetProperty("energy").GetDecimal() == 2, "Суммарный расход сохранён без двойного учёта");
    await first.Command(new { commandId = "jobs", type = "setJobs", planetId = ownPlanet, job = "technician", count = 1 }, true);
    await first.Command(new { commandId = "step2", type = "step" }, true);
    await first.Command(new { commandId = "step2", type = "step" }, true);
    await first.Command(new { commandId = "step2", type = "pause", paused = false }, false);
    var month2 = await first.State(state => state.GetProperty("month").GetInt32() == 2);
    Check(month2.GetProperty("planets")[0].GetProperty("report").GetProperty("produced").GetProperty("energy").GetDecimal() == 7.475m, "Настройка вакансий применяется в следующем месяце");
    second.Dispose();
    await first.State(state => state.GetProperty("participants").EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == second.Id && !p.GetProperty("connected").GetBoolean()));
    await first.Command(new { commandId = "step3", type = "step" }, true);
    using var returned = await Peer.Join(url, code, "Игнорируемое имя", "player", second.Token);
    Check(returned.Id == second.Id, "Токен возвращает прежнего участника");
    var resumed = await returned.State(state => state.GetProperty("month").GetInt32() == 3);
    Check(resumed.GetProperty("planets")[0].GetProperty("id").GetInt32() == otherPlanet, "Возвращена прежняя планета и актуальный месяц");
    await first.Command(new { commandId = "transfer", type = "transferHost", targetParticipantId = returned.Id }, true);
    await first.Command(new { commandId = "old-host", type = "step" }, false);
    await returned.Command(new { commandId = "new-host", type = "step" }, true);
    await first.State(state => state.GetProperty("month").GetInt32() == 4);
    if (additional.Items.Count > 0)
    {
        var last = additional.Items[^1];
        await returned.Command(new { commandId = "transfer-last", type = "transferHost", targetParticipantId = last.Id }, true);
        await returned.Command(new { commandId = "former-host", type = "step" }, false);
        await last.Command(new { commandId = "last-host", type = "step" }, true);
        await first.State(state => state.GetProperty("month").GetInt32() == 5);
    }
    Console.WriteLine($"PASS: HTTP + реальные WebSocket: {playerCount} игроков/наблюдатель, экономика всех, приватность, права, повторы, возврат и передача хоста.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception}");
    return 1;
}

static void Check(bool success, string message)
{
    if (!success)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task RejectHandshake(string url, string payload)
{
    using var socket = new ClientWebSocket();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await socket.ConnectAsync(new Uri(url.Replace("http:", "ws:") + "/ws"), timeout.Token);
    await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, timeout.Token);
    var buffer = new byte[32768];
    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
    if (result.MessageType == WebSocketMessageType.Text)
    {
        using var document = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
        Check(document.RootElement.GetProperty("type").GetString() == "error", "Некорректный вход не должен получать welcome.");
    }
    socket.Abort();
}

internal sealed class PeerGroup : IDisposable
{
    public List<Peer> Items { get; } = [];
    public void Dispose()
    {
        foreach (var peer in Items) peer.Dispose();
    }
}

internal sealed class Peer : IDisposable
{
    public readonly int Id;
    public readonly string Token;
    private readonly ClientWebSocket _socket;
    private bool _disposed;

    private Peer(ClientWebSocket socket, int id, string token)
    {
        _socket = socket;
        Id = id;
        Token = token;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _socket.Abort();
            _socket.Dispose();
        }
    }

    public static async Task<Peer> Join(string url, string code, string name, string role, string? token = null)
    {
        var socket = new ClientWebSocket();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await socket.ConnectAsync(new Uri(url.Replace("http:", "ws:") + "/ws"), deadline.Token);
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = "join", protocol = 1, name, role, token, joinCode = code }), WebSocketMessageType.Text, true, deadline.Token);
            var welcome = await Read(socket);
            if (welcome.GetProperty("type").GetString() != "welcome")
            {
                throw new InvalidOperationException("Вход не подтверждён: " + welcome);
            }
            return new Peer(socket, welcome.GetProperty("participantId").GetInt32(), welcome.GetProperty("token").GetString()!);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async Task<JsonElement> Command(object command, bool accepted)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(command), WebSocketMessageType.Text, true, deadline.Token);
        while (true)
        {
            var message = await Read(_socket);
            if (message.GetProperty("type").GetString() != "result") { continue; }
            var result = message.GetProperty("result");
            if (result.GetProperty("accepted").GetBoolean() != accepted)
            {
                throw new InvalidOperationException("Неожиданный результат команды: " + result);
            }
            return result;
        }
    }

    public async Task<JsonElement> State(Func<JsonElement, bool> predicate)
    {
        while (true)
        {
            var message = await Read(_socket);
            if (message.GetProperty("type").GetString() == "snapshot" && predicate(message.GetProperty("state")))
            {
                return message.GetProperty("state");
            }
        }
    }

    private static async Task<JsonElement> Read(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var data = new MemoryStream();
        var buffer = new byte[32768];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            if (result.MessageType != WebSocketMessageType.Text) { throw new InvalidOperationException("Соединение закрыто до ответа."); }
            data.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        using var document = JsonDocument.Parse(data.ToArray());
        return document.RootElement.Clone();
    }
}
