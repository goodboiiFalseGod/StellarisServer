using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Stellaris.Server;
using Stellaris.Core.Assets;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException(name);
    }
    checks++;
    Console.WriteLine($"PASS {name}");
}
if (args.Contains("--nebula-wire-only"))
{
    var legacy = "\"type\":\"setGalaxySettings\",\"commandId\":\"nebula\",\"galaxySeed\":\"a\",\"galaxyScenarioId\":\"tiny\",\"galaxyShapeId\":\"elliptical\",\"galaxyDensity\":1";
    Check(M3WireCodec.ParseMessage(Encoding.UTF8.GetBytes("{" + legacy + "}")).Command!.GalaxyNebulae is null, "Старый wire caller без count");
    Check(M3WireCodec.ParseMessage(Encoding.UTF8.GetBytes("{" + legacy + ",\"galaxyNebulae\":0}")).Command!.GalaxyNebulae == 0, "Wire integer count0");
    foreach (var value in new[] { "0.5", "\"2\"", "true", "2147483648" })
    {
        var rejected = false;
        try { M3WireCodec.ParseMessage(Encoding.UTF8.GetBytes("{" + legacy + ",\"galaxyNebulae\":" + value + "}")); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Wire отвергает не-int count: " + value);
    }
    return;
}
if (args.Contains("--players-only"))
{
    var playerRootIndex = Array.IndexOf(args, "--game-root");
    var countIndex = Array.IndexOf(args, "--players-only");
    if (playerRootIndex < 0 || playerRootIndex + 1 >= args.Length || countIndex + 1 >= args.Length) throw new ArgumentException("--game-root PATH --players-only COUNT");
    var templateIndex = Array.IndexOf(args, "--player-template");
    if (templateIndex >= 0 && templateIndex + 1 >= args.Length) throw new ArgumentException("--player-template требует ID пресета.");
    await PlayerCountTransportChecks.RunAsync(args[playerRootIndex + 1], int.Parse(args[countIndex + 1]), Check, templateIndex >= 0 ? args[templateIndex + 1] : "economic");
    Console.WriteLine($"PASS player transport: {checks}");
    return;
}
if (args.Contains("--planet-menu-source-only"))
{
    var gameRoot = Array.IndexOf(args, "--game-root");
    if (gameRoot < 0 || gameRoot + 1 >= args.Length) throw new ArgumentException("--planet-menu-source-only требует --game-root.");
    if (args.Contains("--full-catalog-source-only"))
    {
        await FullCatalogSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS full catalog source: {checks}"); return;
    }
    if (args.Contains("--catalog-system-graph-only"))
    {
        await CatalogSystemGraphSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog system graph source: {checks}"); return;
    }
    if (args.Contains("--catalog-district-presence-only"))
    {
        await CatalogDistrictPresenceSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog district presence source: {checks}"); return;
    }
    if (args.Contains("--hum-species-source-only"))
    {
        await HumSpeciesSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS HUM species source: {checks}"); return;
    }
    if (args.Contains("--catalog-condition-sources-only"))
    {
        await CatalogConditionSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog condition sources: {checks}"); return;
    }
    if (args.Contains("--catalog-inline-sources-only"))
    {
        await CatalogInlineSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog inline sources: {checks}"); return;
    }
    if (args.Contains("--catalog-inline-trigger-only"))
    {
        await CatalogInlineTriggerSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS inline trigger source checks: {checks}"); return;
    }
    if (args.Contains("--catalog-zone-conditions-only"))
    {
        await CatalogZoneConditionSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog zone conditions: {checks}"); return;
    }
    if (args.Contains("--catalog-civic-conditions-only"))
    {
        await CatalogCivicConditionSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog civic conditions: {checks}"); return;
    }
    if (args.Contains("--catalog-authority-conditions-only"))
    {
        await CatalogAuthorityConditionSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog authority conditions: {checks}"); return;
    }
    if (args.Contains("--catalog-tradition-helpers-only"))
    {
        await CatalogTraditionHelperSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog tradition helpers: {checks}"); return;
    }
    if (args.Contains("--structural-production-source-only"))
    {
        await StructuralProductionSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS structural production source: {checks}"); return;
    }
    if (args.Contains("--resource-storage-source-only"))
    {
        await ResourceStorageSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS resource storage source: {checks}"); return;
    }
    if (args.Contains("--catalog-source-coverage-only"))
    {
        await CatalogCoverageSourceChecks.RunAsync(args[gameRoot + 1], Check);
        Console.WriteLine($"PASS catalog source coverage: {checks}"); return;
    }
    if (args.Contains("--resource-buildings-only") || args.Contains("--science-buildings-only") || args.Contains("--science-upgrades-only") || args.Contains("--science-tier3-only"))
    {
        await ResourceBuildingSourceChecks.RunAsync(args[gameRoot + 1], Check, args.Contains("--science-buildings-only"), args.Contains("--science-upgrades-only"), args.Contains("--science-tier3-only"));
        Console.WriteLine($"PASS resource building sources: {checks}"); return;
    }
    var basicBuildingOnly = args.Contains("--bureaucratic-building-only") ? "building_bureaucratic_1" : args.Contains("--holo-building-only") ? "building_holo_theatres" : null;
    var siloBuildingOnly = args.Contains("--silo-building-only");
    var specialZoneOnly = args.Contains("--urban-building-only") ? "zone_urban" : args.Contains("--fortress-zone-only") ? "zone_fortress" : null;
    await SourcePlanetMenuChecks.RunAsync(args[gameRoot + 1], Check, args.Contains("--reconnect-only"), args.Contains("--zone-create-only") || siloBuildingOnly, args.Contains("--city-zones-only"), args.Contains("--city-catalog-only"), specialZoneOnly, args.Contains("--job-flows-only"), args.Contains("--catalog-gates-only"), basicBuildingOnly, siloBuildingOnly);
    Console.WriteLine($"PASS source planet menu: {checks}");
    return;
}
if (args.Contains("--auth-only"))
{
    await CheckRejectedJoinTransportAsync();
    var gameRoot = Array.IndexOf(args, "--game-root");
    if (gameRoot < 0 || gameRoot + 1 >= args.Length)
        throw new ArgumentException("--auth-only требует --game-root.");
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var url = $"http://127.0.0.1:{port}";
    using var stop = new CancellationTokenSource();
    var server = M3Server.RunAsync(args[gameRoot + 1], url, "M3-test-code", stop.Token);
    using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(15) };
    try
    {
        var started = false;
        for (var attempt = 0; attempt < 300 && !server.IsCompleted; attempt++)
        {
            try
            {
                using var health = JsonDocument.Parse(await http.GetStringAsync("/health"));
                started = health.RootElement.GetProperty("protocol").GetInt32() == 2;
                break;
            }
            catch (HttpRequestException) { await Task.Delay(100); }
            catch (TaskCanceledException) when (!stop.IsCancellationRequested) { await Task.Delay(100); }
        }
        if (server.IsCompleted) await server;
        Check(started, $"auth-only source HTTP protocol2, isolated port {port}");
        for (var index = 0; index < 20; index++)
        {
            foreach (var wrongProtocol in new[] { false, true })
            {
                using var socket = new ClientWebSocket();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await socket.ConnectAsync(new Uri(url.Replace("http:", "ws:") + "/ws"), deadline.Token);
                await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { type = "join", protocol = wrongProtocol ? 1 : 2, name = "Rejected", role = "player", joinCode = wrongProtocol ? "M3-test-code" : "wrong-code" }).AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
                var buffer = new byte[M3WireCodec.MAX_OUTGOING_BYTES];
                var length = 0;
                ValueWebSocketReceiveResult received;
                do
                {
                    received = await socket.ReceiveAsync(buffer.AsMemory(length), deadline.Token);
                    Check(received.MessageType == WebSocketMessageType.Text, "refusal starts with text, never TCP reset or close");
                    length += received.Count;
                } while (!received.EndOfMessage);
                using var error = JsonDocument.Parse(buffer.AsMemory(0, length));
                Check(error.RootElement.GetProperty("type").GetString() == "error" && error.RootElement.GetProperty("message").GetString() == "Вход отклонён: проверьте protocol=2, код доступа и данные участника." && error.RootElement.EnumerateObject().Count() == 2,
                    $"auth refusal generic error, {(wrongProtocol ? "protocol" : "code")}, iteration {index + 1}");
                received = await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token);
                Check(received.MessageType == WebSocketMessageType.Close && socket.CloseStatus == WebSocketCloseStatus.PolicyViolation, "refusal closes after error with policy violation");
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", deadline.Token);
                Check(socket.State == WebSocketState.Closed, "refusal close handshake acknowledged");
            }
        }
    }
    finally
    {
        stop.Cancel();
        Check(await server.WaitAsync(TimeSpan.FromSeconds(10)) == 0, "auth-only source server clean shutdown");
    }
    Console.WriteLine($"PASS auth-only: {checks} checks, 20 wrong codes + 20 wrong protocols.");
    return;
}

async Task CheckRejectedJoinTransportAsync()
{
    using (var socket = new RefusalSocket())
    {
        var refusal = InvokeTransport("RejectJoinAsync", socket, CancellationToken.None);
        await socket.Closing.Task.WaitAsync(TimeSpan.FromSeconds(1));
        using var error = JsonDocument.Parse(socket.Message!);
        Check(error.RootElement.GetProperty("type").GetString() == "error" && socket.Sends == 1, "refusal sends one error before close");
        Check(!refusal.IsCompleted && socket.Aborts == 0, "buffered refusal awaits close acknowledgement without Abort");
        socket.Acknowledge.TrySetResult();
        await refusal.WaitAsync(TimeSpan.FromSeconds(1));
        Check(socket.State == WebSocketState.Closed && socket.Aborts == 0, "acknowledged refusal never aborts");
    }
    using (var socket = new RefusalSocket())
    {
        var clock = Stopwatch.StartNew();
        try
        {
            await InvokeTransport("RejectJoinAsync", socket, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(6));
            throw new InvalidOperationException("Close без подтверждения не отменён.");
        }
        catch (OperationCanceledException)
        {
            Check(clock.Elapsed >= TimeSpan.FromSeconds(2.5) && clock.Elapsed < TimeSpan.FromSeconds(6) && socket.Aborts == 1, "refusal close deadline 3s aborts unresponsive peer");
        }
    }
    using (var socket = new RefusalSocket { FailSend = true })
    {
        try { await InvokeTransport("RejectJoinAsync", socket, CancellationToken.None); throw new InvalidOperationException("Send failure lost."); }
        catch (WebSocketException) { Check(!socket.Closing.Task.IsCompleted && socket.Aborts == 1, "failed refusal send aborts without close"); }
    }
    using (var socket = new RefusalSocket())
    using (var cancellation = new CancellationTokenSource())
    {
        var refusal = InvokeTransport("RejectJoinAsync", socket, cancellation.Token);
        await socket.Closing.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        try { await refusal.WaitAsync(TimeSpan.FromSeconds(1)); throw new InvalidOperationException("Close cancellation lost."); }
        catch (OperationCanceledException) { Check(socket.Aborts == 1, "external cancellation aborts refusal close promptly"); }
    }
}
void Reject(Action action, string name)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
    {
        Check(true, name);
        return;
    }
    throw new InvalidOperationException($"Не отклонено: {name}");
}
byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
Check(M3WireCodec.ParseJoin(Utf8("{\"type\":\"join\",\"protocol\":2,\"name\":\"А\",\"role\":\"player\",\"joinCode\":\"12345678\"}")).Name == "А", "join v2");
foreach (var json in new[]
{
    "{}", "[]", "{\"type\":\"join\",\"protocol\":1}", "{\"type\":\"join\",\"protocol\":\"2\"}",
    "{\"type\":\"ping\",\"type\":\"ping\"}", "{\"type\":\"ping\",\"x\":null}",
    "{\"type\":\"stepDay\",\"commandId\":\"x\",\"fleetId\":null}",
    "{\"type\":\"ready\",\"commandId\":\"x\",\"ready\":\"true\"}",
    "{\"type\":\"moveFleetToPoint\",\"commandId\":\"x\",\"x\":1e999}",
    "{\"type\":\"configure\",\"commandId\":\"x\",\"traits\":[1]}",
    "{\"type\":\"speed\",\"commandId\":\"x\",\"speed\":1.5}",
    "{\"type\":\"stepDay\",\"commandId\":\"\"}"
})
{
    Reject(() => { if (json.Contains("protocol")) { M3WireCodec.ParseJoin(Utf8(json)); } else { M3WireCodec.ParseMessage(Utf8(json)); } }, $"strict JSON {checks}");
}
Reject(() => M3WireCodec.ParseMessage(new byte[] { 0xff }), "strict UTF-8");
Reject(() => M3WireCodec.ParseMessage(new byte[16 * 1024 + 1]), "incoming 16 KiB");
Reject(() => M3WireCodec.ValidateOutgoing(new byte[1024 * 1024 + 1]), "outgoing 1 MiB");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"ping\"}")).IsPing, "ping");
foreach (var confirmed in new[] { true, false })
    Check(M3WireCodec.ParseMessage(Utf8(JsonSerializer.Serialize(new { type = "demolishBuilding", commandId = "demolition-wire", colonyId = "colony", expectedColonyRevision = 0,
        definitionId = "building", slotId = "slot", confirmDemolition = confirmed }))).Command!.ConfirmDemolition == confirmed, "Demolition wire сохраняет bool");
foreach (var payload in new[] { "null", "1", "\"true\"" })
    Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"demolishBuilding\",\"commandId\":\"bad\",\"colonyId\":\"colony\",\"expectedColonyRevision\":0,\"definitionId\":\"building\",\"slotId\":\"slot\",\"confirmDemolition\":" + payload + "}")), "Demolition strict bool");
Reject(() => M3WireCodec.ParseMessage(Utf8("""{"type":"demolishBuilding","commandId":"missing","colonyId":"colony","expectedColonyRevision":0,"definitionId":"building","slotId":"slot"}""")), "Demolition required confirmation");
Reject(() => M3WireCodec.ParseMessage(Utf8("""{"type":"demolishBuilding","commandId":"extra","colonyId":"colony","expectedColonyRevision":0,"definitionId":"building","slotId":"slot","confirmDemolition":true,"orderId":"extra"}""")), "Demolition не принимает orderId");
var reorderWire = new { type = "reorderConstruction", commandId = "reorder-wire", colonyId = "colony", expectedColonyRevision = 0, orderIds = new[] { "a", "c", "b" } };
foreach (var enabled in new[] { true, false })
    Check(M3WireCodec.ParseMessage(Utf8(JsonSerializer.Serialize(new { type = "setBuildingEnabled", commandId = "toggle-wire", colonyId = "colony", expectedColonyRevision = 0,
        definitionId = "building", slotId = "slot", enabled }))).Command!.Enabled == enabled, "Enabled wire сохраняет bool без OrderId");
foreach (var payload in new[] { "null", "1", "\"false\"" })
    Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"setBuildingEnabled\",\"commandId\":\"toggle-bad\",\"colonyId\":\"colony\",\"expectedColonyRevision\":0,\"definitionId\":\"building\",\"slotId\":\"slot\",\"enabled\":" + payload + "}")), "Enabled strict wire type/required");
Reject(() => M3WireCodec.ParseMessage(Utf8("""{"type":"setBuildingEnabled","commandId":"toggle-missing","colonyId":"colony","expectedColonyRevision":0,"definitionId":"building","slotId":"slot"}""")), "Enabled required field");
Reject(() => M3WireCodec.ParseMessage(Utf8("""{"type":"setBuildingEnabled","commandId":"toggle-extra","colonyId":"colony","expectedColonyRevision":0,"definitionId":"building","slotId":"slot","enabled":false,"orderId":"extra"}""")), "Toggle не принимает orderId");
Check(M3WireCodec.ParseMessage(Utf8(JsonSerializer.Serialize(reorderWire))).Command!.OrderIds!.SequenceEqual(["a", "c", "b"]), "Перестановка: полный упорядоченный список wire");
foreach (var invalid in new[] { "null", "[]", "[null]", "[1]", "[\"\"]", "[\"a\",\"a\"]", "[\"x\\u0001\"]", "[\"" + new string('x', 161) + "\"]" })
    Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"reorderConstruction\",\"commandId\":\"bad\",\"colonyId\":\"colony\",\"expectedColonyRevision\":0,\"orderIds\":" + invalid + "}")), "Перестановка: неверные IDs wire отклонены");
Reject(() => M3WireCodec.ParseMessage(Utf8("""{"type":"reorderConstruction","commandId":"missing","colonyId":"colony","expectedColonyRevision":0}""")), "Перестановка: список обязателен");
var galaxySettings = M3WireCodec.ParseMessage(Utf8("""{"type":"setGalaxySettings","commandId":"galaxy-settings","galaxySeed":"","galaxyScenarioId":"tiny","galaxyShapeId":"elliptical","galaxyDensity":3}""")).Command!;
Check(galaxySettings.GalaxySeed == "" && galaxySettings.GalaxyDensity == 3m, "Galaxy settings: seed и decimal density");
foreach (var json in new[] {
    """{"type":"setGalaxySettings","commandId":"bad-density","galaxyDensity":"3"}""",
    """{"type":"prepareGalaxy","commandId":"bad-prepare","galaxySeed":"seed"}""",
    """{"type":"inspectGalaxySystem","commandId":"bad-inspect","galaxyWorldFingerprint":42}"""
}) Reject(() => M3WireCodec.ParseMessage(Utf8(json)), "Galaxy strict wire fields/types");
foreach (var type in new[] { "assetReady", "start" }) {
    var command = M3WireCodec.ParseMessage(Utf8(JsonSerializer.Serialize(new { type, commandId = "bound-" + type,
        profileId = "current", profileDigest = "digest", galaxyWorldFingerprint = "world" }))).Command!;
    Check(command.GalaxyWorldFingerprint == "world" && command.ProfileId == "current", "Galaxy bound " + type);
}
Check(M3WireCodec.ParseMessage(Utf8("""{"type":"ready","commandId":"consent","ready":true,"expectedLobbyRevision":9223372036854775807}""")).Command!.ExpectedLobbyRevision == long.MaxValue, "Lobby Ready Int64 revision без assets");
foreach (var json in new[] {
    """{"type":"ready","commandId":"old-consent","ready":true,"profileId":"old"}""",
    """{"type":"ready","commandId":"fractional-consent","ready":true,"expectedLobbyRevision":1.5}""",
    """{"type":"start","commandId":"text-revision","expectedLobbyRevision":"1"}"""
}) Reject(() => M3WireCodec.ParseMessage(Utf8(json)), "Lobby strict consent fields/types");
Check(M3WireCodec.ParseMessage(Utf8("""{"type":"inspectGalaxySystem","commandId":"clear-details","systemId":"","galaxyWorldFingerprint":"world"}""")).Command!.SystemId == "", "Galaxy inspect clear");
var researchPrefix = "{\"type\":\"selectResearch\",\"commandId\":\"research-wire\",\"area\":\"physics\",\"technologyId\":\"tech_physics_1\"";
foreach (var revision in new[] { long.MinValue, -1L, 0L, (long)int.MaxValue + 1, long.MaxValue })
{
    var command = M3WireCodec.ParseMessage(Utf8(researchPrefix + ",\"expectedResearchRevision\":" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}")).Command!;
    Check(command.Area == "physics" && command.TechnologyId == "tech_physics_1" && command.ExpectedResearchRevision == revision, "Исследование: строгая форма и Int64 revision");
}
Check(M3WireCodec.ParseMessage(Utf8(researchPrefix + "}")).Command!.ExpectedResearchRevision is null, "Исследование: отсутствующая revision передаётся семантической проверке");
Check(M3WireCodec.ParseMessage(Utf8(researchPrefix + ",\"expectedResearchRevision\":null}")).Command!.ExpectedResearchRevision is null, "Исследование: null revision передаётся семантической проверке");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"selectResearch\",\"commandId\":\"null-research\",\"area\":null,\"technologyId\":null,\"expectedResearchRevision\":0}")).Command!.Area is null, "Исследование: null ID передаётся семантической проверке");
foreach (var invalid in new[] { "\"0\"", "0.5", "true", "[]", "{}", "9223372036854775808", "-9223372036854775809" })
{
    Reject(() => M3WireCodec.ParseMessage(Utf8(researchPrefix + ",\"expectedResearchRevision\":" + invalid + "}")), "Исследование: неверные тип/диапазон revision отклонены");
}
Reject(() => M3WireCodec.ParseMessage(Utf8(researchPrefix + ",\"expectedResearchRevision\":0,\"count\":1}")), "Исследование: чужое численное поле отклонено");
Reject(() => M3WireCodec.ParseMessage(Utf8(researchPrefix + ",\"expectedResearchRevision\":0,\"expectedResearchRevision\":1}")), "Исследование: повторное поле отклонено");
Reject(() => M3WireCodec.ParseMessage(Utf8(researchPrefix.Replace("\"area\":\"physics\"", "\"area\":7", StringComparison.Ordinal) + ",\"expectedResearchRevision\":0}")), "Исследование: неверный тип направления отклонён");
var upgradeWire = new Dictionary<string, object?>
{
    ["type"] = "enqueueUpgrade", ["commandId"] = "upgrade-wire", ["colonyId"] = "colony", ["expectedColonyRevision"] = 7L,
    ["definitionId"] = "building_major_capital", ["sourceDefinitionId"] = "building_capital", ["slotId"] = "slot", ["zoneInstanceId"] = "zone", ["orderId"] = "order"
};
var parsedUpgrade = M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(upgradeWire)).Command!;
Check(parsedUpgrade.SourceDefinitionId == "building_capital" && parsedUpgrade.ZoneInstanceId == "zone" && parsedUpgrade.ExpectedColonyRevision == 7, "upgrade exact source/slot-zone/revision wire shape");
foreach (var field in upgradeWire.Keys.Where(key => key is not ("type" or "commandId")))
{
    var missing = new Dictionary<string, object?>(upgradeWire); missing.Remove(field);
    Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(missing)), "upgrade missing required " + field);
    missing[field] = null;
    Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(missing)), "upgrade null required " + field);
}
var extraUpgrade = new Dictionary<string, object?>(upgradeWire) { ["costs"] = new { minerals = 0 } };
Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(extraUpgrade)), "client-authored upgrade quote refused");
extraUpgrade = new(upgradeWire) { ["type"] = "enqueueConstruction" };
Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(extraUpgrade)), "upgrade source fields cannot bypass from-scratch shape");
var zoneReplacementWire = new Dictionary<string, object?>
{
    ["type"] = "enqueueZoneReplacement", ["commandId"] = "zone-replacement-wire", ["colonyId"] = "colony",
    ["expectedColonyRevision"] = 7L, ["sourceDefinitionId"] = "zone_industrial", ["definitionId"] = "zone_foundry",
    ["slotId"] = "zone-slot", ["zoneInstanceId"] = "zone-occurrence", ["orderId"] = "zone-order",
    ["zoneReplacementPreviewId"] = "preview", ["confirmZoneRemoval"] = true
};
foreach (var confirmed in new[] { true, false })
{
    var payload = new Dictionary<string, object?>(zoneReplacementWire) { ["confirmZoneRemoval"] = confirmed };
    var command = M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(payload)).Command!;
    Check(command.Type == "enqueueZoneReplacement" && command.ColonyId == "colony" && command.ExpectedColonyRevision == 7L
        && command.SourceDefinitionId == "zone_industrial" && command.DefinitionId == "zone_foundry" && command.SlotId == "zone-slot"
        && command.ZoneInstanceId == "zone-occurrence" && command.OrderId == "zone-order"
        && command.ZoneReplacementPreviewId == "preview" && command.ConfirmZoneRemoval == confirmed,
        "Zone replacement strict wire fields preserve explicit confirmation bool");
}
foreach (var field in zoneReplacementWire.Keys.Where(key => key is not ("type" or "commandId")))
{
    var missing = new Dictionary<string, object?>(zoneReplacementWire); missing.Remove(field);
    Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(missing)), "zone replacement missing required " + field);
    missing[field] = null;
    Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(missing)), "zone replacement null required " + field);
}
foreach (var invalid in new object?[] { null, 1, "true", new[] { true }, new { value = true } })
{
    var malformed = new Dictionary<string, object?>(zoneReplacementWire) { ["confirmZoneRemoval"] = invalid };
    Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(malformed)), "zone replacement wrong confirmation bool type refused");
}
foreach (var otherType in new[] { "enqueueConstruction", "enqueueUpgrade", "setBuildingEnabled" })
{
    var fields = otherType switch
    {
        "enqueueConstruction" => new Dictionary<string, object?>
        {
            ["type"] = otherType, ["commandId"] = "other-command", ["colonyId"] = "colony", ["expectedColonyRevision"] = 0L,
            ["definitionId"] = "building", ["slotId"] = "slot", ["orderId"] = "order"
        },
        "enqueueUpgrade" => new Dictionary<string, object?>
        {
            ["type"] = otherType, ["commandId"] = "other-command", ["colonyId"] = "colony", ["expectedColonyRevision"] = 0L,
            ["sourceDefinitionId"] = "building_old", ["definitionId"] = "building_new", ["slotId"] = "slot",
            ["zoneInstanceId"] = "zone", ["orderId"] = "order"
        },
        _ => new Dictionary<string, object?>
        {
            ["type"] = otherType, ["commandId"] = "other-command", ["colonyId"] = "colony", ["expectedColonyRevision"] = 0L,
            ["definitionId"] = "building", ["slotId"] = "slot", ["enabled"] = true
        }
    };
    foreach (var field in new[] { "zoneReplacementPreviewId", "confirmZoneRemoval" })
    {
        var unexpected = new Dictionary<string, object?>(fields)
        {
            [field] = field == "confirmZoneRemoval" ? true : "preview"
        };
        Reject(() => M3WireCodec.ParseMessage(JsonSerializer.SerializeToUtf8Bytes(unexpected)),
            otherType + " refuses zone replacement field " + field);
    }
}
foreach (var type in new[] { "enqueueConstruction", "clearBlocker", "cancelConstruction" })
{
    var fields = type == "cancelConstruction" ? "" : ",\"definitionId\":\"building_hydroponics_farm\",\"slotId\":\"slot\"";
    var prefix = "{\"type\":\"" + type + "\",\"commandId\":\"construction\",\"colonyId\":\"colony\",\"orderId\":\"order\"" + fields;
    foreach (var revision in new[] { long.MinValue, -1L, 0L, (long)int.MaxValue + 1, long.MaxValue })
    {
        var command = M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}")).Command!;
        Check(command.Type == type && command.ColonyId == "colony" && command.OrderId == "order" && command.ExpectedColonyRevision == revision, "construction Int64 wire shape");
    }
    Check(M3WireCodec.ParseMessage(Utf8(prefix + "}")).Command!.ExpectedColonyRevision is null, "missing construction revision delegated");
    Check(M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":null}")).Command!.ExpectedColonyRevision is null, "null construction revision delegated");
    foreach (var invalid in new[] { "\"0\"", "0.5", "true", "[]", "{}", "9223372036854775808", "-9223372036854775809" })
    {
        Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":" + invalid + "}")), "invalid construction revision wire type/range");
    }
    Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":0,\"count\":1}")), "construction numerical payload refused");
    Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":0,\"expectedColonyRevision\":1}")), "duplicate construction revision refused");
}
foreach (var item in new[]
{
    (Type: "adjustWorkforceLimit", Fields: ",\"count\":1", HasCount: true),
    (Type: "setWorkforceLimitMin", Fields: "", HasCount: false),
    (Type: "setWorkforceLimitMax", Fields: "", HasCount: false),
    (Type: "setWorkforceLimit", Fields: ",\"count\":12.5", HasCount: true)
})
{
    var prefix = "{\"type\":\"" + item.Type + "\",\"commandId\":\"wire-" + item.Type + "\",\"colonyId\":\"colony\",\"job\":\"miner\"";
    var expectedMax = M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":9223372036854775807" + item.Fields + "}")).Command!;
    Check(expectedMax.Type == item.Type && expectedMax.ColonyId == "colony" && expectedMax.Job == "miner"
        && expectedMax.ExpectedColonyRevision == long.MaxValue && (item.HasCount ? expectedMax.Count is not null : expectedMax.Count is null), "workforce Int64 wire shape");
    Check(M3WireCodec.ParseMessage(Utf8(prefix + item.Fields + "}")).Command!.ExpectedColonyRevision is null, "missing workforce revision delegated to semantic validation");
    Check(M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":null" + item.Fields + "}")).Command!.ExpectedColonyRevision is null, "null workforce revision delegated to semantic validation");
    Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"" + item.Type + "\",\"commandId\":\"missing-job\",\"colonyId\":\"colony\",\"expectedColonyRevision\":0" + item.Fields + "}")).Command!.Job is null, "missing workforce job delegated to semantic validation");
    Check(M3WireCodec.ParseMessage(Utf8(prefix.Replace("\"job\":\"miner\"", "\"job\":null", StringComparison.Ordinal) + ",\"expectedColonyRevision\":0" + item.Fields + "}")).Command!.Job is null, "null workforce job delegated to semantic validation");
    Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":0" + item.Fields + ",\"targetParticipantId\":1}")), "workforce extra field refused");
    Reject(() => M3WireCodec.ParseMessage(Utf8(prefix.Replace("\"colonyId\":\"colony\"", "\"colonyId\":7", StringComparison.Ordinal) + ",\"expectedColonyRevision\":0" + item.Fields + "}")), "workforce wrong colony type refused");
    Reject(() => M3WireCodec.ParseMessage(Utf8(prefix.Replace("\"job\":\"miner\"", "\"job\":7", StringComparison.Ordinal) + ",\"expectedColonyRevision\":0" + item.Fields + "}")), "workforce wrong job type refused");
    foreach (var invalidRevision in new[] { "\"0\"", "0.5", "true", "9223372036854775808" })
    {
        Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":" + invalidRevision + item.Fields + "}")), "workforce wrong revision type/range refused");
    }
    if (item.HasCount)
    {
        foreach (var invalidCount in new[] { "\"1\"", "true", "[]", "1e100" })
        {
            Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":0,\"count\":" + invalidCount + "}")), "workforce wrong count type/range refused");
        }
        Check(M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":0,\"count\":null}")).Command!.Count is null, "null workforce count delegated to semantic validation");
    }
    else
    {
        Reject(() => M3WireCodec.ParseMessage(Utf8(prefix + ",\"expectedColonyRevision\":0,\"count\":1}")), "workforce unexpected count refused");
    }
}
Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"cancelConstruction\",\"commandId\":\"cancel\",\"definitionId\":\"building_hydroponics_farm\"}")), "cancel definition field refused");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"cancelConstruction\",\"commandId\":\"refund\",\"colonyId\":\"C1\",\"expectedColonyRevision\":1,\"orderId\":\"O1\",\"refundPreviewId\":\"preview\",\"confirmRefundLoss\":true}")).Command is { RefundPreviewId: "preview", ConfirmRefundLoss: true }, "refund preview/consent wire fields");
foreach (var invalid in new[] { "\"confirmRefundLoss\":\"true\"", "\"confirmRefundLoss\":1", "\"refundPreviewId\":12", "\"losses\":{\"minerals\":0}" })
{
    Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"cancelConstruction\",\"commandId\":\"invalid-refund\"," + invalid + "}")), "wrong refund type/client amounts refused");
}
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"selectEmpireTemplate\",\"commandId\":\"select\",\"templateId\":\"economic\"}")).Command!.TemplateId == "economic", "template selection wire field");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"selectEmpireTemplate\",\"commandId\":\"missing-template\"}")).Command!.TemplateId is null, "missing template delegated to INVALID_COMMAND");
foreach (var fields in new[] { "\"templateId\":5", "\"templateId\":\"economic\",\"targetParticipantId\":2", "\"templateId\":\"economic\",\"count\":1", "\"templateId\":\"economic\",\"templateId\":\"military\"" })
{
    Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"selectEmpireTemplate\",\"commandId\":\"invalid-template\"," + fields + "}")), "strict template selection JSON");
}
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"observerView\",\"commandId\":\"view\",\"targetParticipantId\":0}")).Command!.TargetParticipantId == 0, "observerView general target");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"observerView\",\"commandId\":\"missing-view\"}")).Command!.TargetParticipantId == null, "missing POV target delegated to INVALID_COMMAND");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"observerView\",\"commandId\":\"null-view\",\"targetParticipantId\":null}")).Command!.TargetParticipantId == null, "null POV target delegated to INVALID_COMMAND");
foreach (var fields in new[] { "\"targetParticipantId\":1.5", "\"targetParticipantId\":\"1\"", "\"targetParticipantId\":0,\"ready\":null", "\"targetParticipantId\":0,\"targetParticipantId\":1" })
{
    Reject(() => M3WireCodec.ParseMessage(Utf8("{\"type\":\"observerView\",\"commandId\":\"invalid-view\"," + fields + "}")), "strict observerView JSON");
}
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"kickParticipant\",\"commandId\":\"kick\",\"targetParticipantId\":7}")).Command is { Type: "kickParticipant", TargetParticipantId: 7 }, "strict kick target participant wire field");
foreach (var malformedKick in new[]
{
    "{\"type\":\"kickParticipant\",\"commandId\":\"missing-target\"}",
    "{\"type\":\"kickParticipant\",\"commandId\":\"null-target\",\"targetParticipantId\":null}",
    "{\"type\":\"kickParticipant\",\"commandId\":\"bad-target\",\"targetParticipantId\":1.5}",
    "{\"type\":\"kickParticipant\",\"commandId\":\"extra-field\",\"targetParticipantId\":2,\"ready\":true}"
}) Reject(() => M3WireCodec.ParseMessage(Utf8(malformedKick)), "strict kick JSON rejects missing/null/wrong/extra target fields");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"speed\",\"commandId\":\"missing\"}")).Command!.Speed == null, "missing argument delegated to INVALID_COMMAND");
Check(M3WireCodec.ParseMessage(Utf8("{\"type\":\"unknown\",\"commandId\":\"unknown\"}")).Command!.Type == "unknown", "unknown delegated to INVALID_COMMAND");
foreach (var url in new[] { "http://127.0.0.1:5080", "http://localhost/", "http://[::1]:5080/" })
{
    Check(M3Server.ValidateLoopbackUrl(url).Scheme == "http", "loopback accepted");
}
foreach (var url in new[] { "http://0.0.0.0:5080", "http://127.1", "https://localhost", "http://localhost/?x", "http://x@localhost", "http://localhost/#x", "http://localhost/../", "http://localhost:0", "http://localhost:65536", "http://localhost;http://localhost", " http://localhost" })
{
    Reject(() => M3Server.ValidateLoopbackUrl(url), "unsafe URL");
}
using (var queue = new M3Outbox())
{
    for (var index = 0; index < 100; index++)
    {
        queue.ReplaceSnapshot([checked((byte)index)]);
    }
    var source = new byte[] { 42 };
    queue.EnqueueReply(source);
    source[0] = 0;
    for (var index = 1; index < 64; index++)
    {
        queue.EnqueueReply([checked((byte)index)]);
    }
    Reject(() => queue.EnqueueReply([0]), "reply overflow 65");
    Check((await queue.TakeAsync(default))![0] == 42, "reply immutable and before snapshot");
    for (var index = 1; index < 64; index++)
    {
        Check((await queue.TakeAsync(default))![0] == index, "FIFO ACK");
    }
    Check((await queue.TakeAsync(default))![0] == 99, "only latest snapshot retained");
    using var deadline = new CancellationTokenSource(25);
    try { await queue.TakeAsync(deadline.Token); throw new InvalidOperationException("Нет отмены очереди"); }
    catch (OperationCanceledException) { Check(true, "queue cancellation"); }
    queue.Complete();
    Check(await queue.TakeAsync(default) == null, "complete drains");
}
var limiter = new M3RateLimiter();
for (var index = 0; index < 20; index++)
{
    Check(limiter.Accept(TimeSpan.Zero), "rate permits first 20");
}
Check(!limiter.Accept(TimeSpan.FromMilliseconds(999)), "rate rejects 21st");
Check(limiter.Accept(TimeSpan.FromSeconds(1)), "sliding rate expires exactly at 1s");
Reject(() => limiter.Accept(TimeSpan.Zero), "rate monotonic");

Task InvokeTransport(string name, params object[] arguments)
{
    var method = typeof(M3Server).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Не найден transport method {name}.");
    return (Task)(method.Invoke(null, arguments) ?? throw new InvalidOperationException("Метод не вернул Task."));
}
using (var blocked = new ControlledSocket())
{
    var clock = Stopwatch.StartNew();
    var send = InvokeTransport("SendAsync", blocked, new byte[] { 1 }, CancellationToken.None);
    await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
    try
    {
        await send.WaitAsync(TimeSpan.FromSeconds(6));
        throw new InvalidOperationException("Заблокированный send не отменён.");
    }
    catch (OperationCanceledException)
    {
        Check(clock.Elapsed >= TimeSpan.FromSeconds(2.5) && clock.Elapsed < TimeSpan.FromSeconds(6), "actual SendAsync deadline 3s, bounded 2.5..6s");
        Check(blocked.LastToken.IsCancellationRequested, "deadline token passed to actual WebSocket send");
    }
}
using (var blocked = new ControlledSocket())
using (var external = new CancellationTokenSource())
{
    var send = InvokeTransport("SendAsync", blocked, new byte[] { 1 }, external.Token);
    await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
    var clock = Stopwatch.StartNew();
    external.Cancel();
    try
    {
        await send.WaitAsync(TimeSpan.FromSeconds(2));
        throw new InvalidOperationException("Внешняя отмена send проигнорирована.");
    }
    catch (OperationCanceledException)
    {
        Check(clock.Elapsed < TimeSpan.FromSeconds(2) && blocked.LastToken.IsCancellationRequested, "external cancellation promptly reaches send");
    }
}
using (var untouched = new ControlledSocket())
{
    try
    {
        await InvokeTransport("SendAsync", untouched, new byte[M3WireCodec.MAX_OUTGOING_BYTES + 1], CancellationToken.None);
        throw new InvalidOperationException("Oversize send не отклонён.");
    }
    catch (InvalidDataException)
    {
        Check(untouched.Messages.Count == 0 && !untouched.Entered.Task.IsCompleted, "actual oversized send rejected before WebSocket call");
    }
}
using (var delayed = new ControlledSocket())
using (var queue = new M3Outbox())
{
    queue.EnqueueReply([1]);
    var sendLoop = InvokeTransport("SendLoopAsync", delayed, queue, CancellationToken.None);
    await delayed.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
    queue.EnqueueReply([2]);
    for (var index = 0; index < 100; index++)
    {
        queue.ReplaceSnapshot([checked((byte)index)]);
    }
    Check(delayed.Messages.Count == 1 && queue.PendingReplies == 1, "blocked send does not drain pending ACK or snapshot");
    queue.Complete();
    delayed.Release(3);
    await sendLoop.WaitAsync(TimeSpan.FromSeconds(2));
    Check(delayed.Messages.Select(message => message.Single()).SequenceEqual(new byte[] { 1, 2, 99 }), "actual send loop FIFO replies then latest snapshot");
    Check(delayed.MaxConcurrent == 1, "actual send loop has exactly one concurrent WebSocket send");
}

await CheckRejectedJoinTransportAsync();
checks += await SimulationRateChecks.RunAsync();
var bindingIndex = Array.IndexOf(args, "--galaxy-binding");
if (bindingIndex >= 0)
    checks += await GeneratedBindingChecks.RunAsync(args[bindingIndex + 1], args[bindingIndex + 2], args[bindingIndex + 3]);
var rootIndex = Array.IndexOf(args, "--game-root");
if (rootIndex >= 0)
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var url = $"http://127.0.0.1:{port}";
    using var stop = new CancellationTokenSource();
    var server = M3Server.RunAsync(args[rootIndex + 1], url, "M3-test-code", stop.Token);
    using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(15) };
    try
    {
        var started = false;
        for (var attempt = 0; attempt < 300 && !server.IsCompleted; attempt++)
        {
            try
            {
                using var health = JsonDocument.Parse(await http.GetStringAsync("/health"));
                started = health.RootElement.GetProperty("protocol").GetInt32() == 2;
                break;
            }
            catch (HttpRequestException exception)
            {
                if (attempt == 299) { Console.WriteLine($"HTTP startup: {exception.GetType().Name}, status={exception.StatusCode}"); }
                await Task.Delay(100);
            }
        }
        Check(started, "real HTTP health protocol2");
        foreach (var module in new[] { "m3-planet-assets.mjs", "m3-planet-images.mjs", "m3-navigation.mjs" })
        {
            using var served = await http.GetAsync("/m3-modules/" + module);
            var bytes = await served.Content.ReadAsByteArrayAsync();
            var packaged = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "wwwroot", "m3-modules", module));
            Check(served.StatusCode == HttpStatusCode.OK && served.Content.Headers.ContentType?.MediaType is "text/javascript" or "application/javascript" && bytes.SequenceEqual(packaged), "real reviewed module JavaScript route and bytes");
        }
        using (var uppercaseHost = new HttpRequestMessage(HttpMethod.Get, "/health"))
        {
            uppercaseHost.Headers.Host = $"LOCALHOST:{port}";
            using var hostResponse = await http.SendAsync(uppercaseHost);
            Check(hostResponse.StatusCode == HttpStatusCode.OK, "uppercase LOCALHOST accepted");
        }
        using (var foreignHost = new HttpRequestMessage(HttpMethod.Get, "/health"))
        {
            foreignHost.Headers.Host = $"example.invalid:{port}";
            using var hostResponse = await http.SendAsync(foreignHost);
            Check(hostResponse.StatusCode == HttpStatusCode.Forbidden, "foreign Host rejected HTTP403");
        }
        using var manifest = JsonDocument.Parse(await http.GetStringAsync("/m3/asset-manifest"));
        Check(manifest.RootElement.GetProperty("files").GetArrayLength() == M3AssetManifestBuilder.ProfilePaths.Count, "real manifest file count matches profile");
        var profile = manifest.RootElement.GetProperty("profileId").GetString()!;
        var digest = manifest.RootElement.GetProperty("profileDigest").GetString()!;
        Check((await http.GetAsync("/gfx/models/planets/planet_clouded.mesh")).StatusCode == HttpStatusCode.NotFound, "no original asset endpoint");
        using var response = await http.GetAsync("/health");
        Check(response.Headers.GetValues("Content-Security-Policy").Single().Contains("'sha256-") && !response.Headers.GetValues("Content-Security-Policy").Single().Contains("unsafe-inline"), "CSP importmap hash");
        var wsUrl = new Uri($"ws://127.0.0.1:{port}/ws");
        var idleSockets = new List<ClientWebSocket>();
        try
        {
            var idleClock = Stopwatch.StartNew();
            for (var index = 0; index < 8; index++)
            {
                var socket = new ClientWebSocket();
                idleSockets.Add(socket);
                using var connectDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.ConnectAsync(wsUrl, connectDeadline.Token);
            }
            Check(idleSockets.All(socket => socket.State == WebSocketState.Open), "eight simultaneous sockets accepted before join");
            var ninth = new ClientWebSocket();
            idleSockets.Add(ninth);
            using (var connectDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                await ninth.ConnectAsync(wsUrl, connectDeadline.Token);
            Check(ninth.State == WebSocketState.Open, "Девятое соединение принято без фиксированного лимита восьми");
            using var joinDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(14));
            var disconnected = false;
            try
            {
                var received = await idleSockets[0].ReceiveAsync(new byte[1024].AsMemory(), joinDeadline.Token);
                disconnected = received.MessageType == WebSocketMessageType.Close;
            }
            catch (WebSocketException)
            {
                disconnected = true;
            }
            Check(disconnected && !joinDeadline.IsCancellationRequested && idleClock.Elapsed >= TimeSpan.FromSeconds(9) && idleClock.Elapsed < TimeSpan.FromSeconds(14), "idle join disconnected at 10s, bounded below14s");
        }
        finally
        {
            foreach (var socket in idleSockets)
            {
                socket.Abort();
                socket.Dispose();
            }
        }
        Console.WriteLine("PENDING real TCP slow-reader: actual SendAsync deadline/send-loop проверены управляемым WebSocket double; TCP-buffer exhaustion отдельно не проверен.");
        await using (var wrongVersion = await Peer.JoinAsync(wsUrl, "Ошибка", expectWelcome: false, protocol: 1))
        {
            Check(wrongVersion.First.GetProperty("type").GetString() == "error", "protocol1 rejected");
        }
        await using (var wrongCode = await Peer.JoinAsync(wsUrl, "Ошибка", expectWelcome: false, code: "wrong-code"))
        {
            Check(wrongCode.First.GetProperty("type").GetString() == "error", "wrong join code rejected");
        }
        using (var foreignOrigin = new ClientWebSocket())
        {
            foreignOrigin.Options.SetRequestHeader("Origin", "http://example.invalid");
            try
            {
                await foreignOrigin.ConnectAsync(wsUrl, CancellationToken.None);
                throw new InvalidOperationException("Origin принят");
            }
            catch (WebSocketException) { Check(true, "foreign Origin rejected"); }
        }
        await using var first = await Peer.JoinAsync(wsUrl, "Первый");
        Peer second = await Peer.JoinAsync(wsUrl, "Второй");
        await using var observer = await Peer.JoinAsync(wsUrl, "Observer", role: "observer");
        async Task KeepJoinedPeersAlive(string label)
        {
            Check(await second.PingAsync(), $"second player heartbeat {label}");
            Check(await observer.PingAsync(), $"observer heartbeat {label}");
        }
        await KeepJoinedPeersAlive("after join");
        var pendingState = await observer.StateAsync();
        Check(pendingState.GetProperty("viewContext").GetProperty("mode").GetString() == "pending" && pendingState.GetProperty("viewContext").GetProperty("participantId").ValueKind == JsonValueKind.Null && pendingState.GetProperty("planets").GetArrayLength() == 0 && pendingState.GetProperty("spatial").GetProperty("systems").GetArrayLength() == 0, "real pending observer context and privacy");
        Check((await observer.CommandAsync(new { type = "assetReady", commandId = "observer-unprepared-assets", profileId = profile, profileDigest = digest, ready = true })).GetProperty("code").GetString() == "STALE_GALAXY_CANDIDATE", "baseline ACK до prepare не принимается");
        foreach (var peer in new[] { first, second })
            Check((await peer.CommandAsync(new { type = "selectEmpireTemplate", commandId = "template", templateId = peer.Id == first.Id ? "economic" : "scientific" })).GetProperty("accepted").GetBoolean(), "real template selection before asset ACK");
        var wireGalaxySettings = new { type = "setGalaxySettings", commandId = "galaxy-settings", galaxySeed = "real-websocket-production", galaxyScenarioId = "tiny", galaxyShapeId = "elliptical", galaxyDensity = 1 };
        Check((await second.CommandAsync(wireGalaxySettings)).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real peer cannot change galaxy settings");
        Check((await first.CommandAsync(wireGalaxySettings)).GetProperty("accepted").GetBoolean(), "real host source galaxy settings");
        Check((await second.CommandAsync(new { type = "prepareGalaxy", commandId = "peer-prepare" })).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real peer cannot prepare galaxy");
        Check((await first.CommandAsync(new { type = "prepareGalaxy", commandId = "host-prepare" })).GetProperty("code").GetString() == "INVALID_COMMAND", "real host cannot bypass all Ready with prepare");
        var draftAck = await first.CommandAsync(wireGalaxySettings);
        var draftState = await first.StateAsync(draftAck.GetProperty("stateSequence").GetInt64());
        var lobbyRevision = draftState.GetProperty("galaxy").GetProperty("lobbyRevision").GetInt64();
        Check((await first.CommandAsync(new { type = "start", commandId = "too-early-start", expectedLobbyRevision = lobbyRevision })).GetProperty("code").GetString() == "INVALID_PHASE", "real host Start requires everyone Ready");
        Check((await observer.CommandAsync(new { type = "ready", commandId = "observer-vote", expectedLobbyRevision = lobbyRevision, ready = true })).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real observer cannot vote");
        foreach (var peer in new[] { first, second })
            Check((await peer.CommandAsync(new { type = "ready", commandId = "lobby-ready", expectedLobbyRevision = lobbyRevision, ready = true })).GetProperty("accepted").GetBoolean(), "real individual Ready before assets/world");
        Check((await second.CommandAsync(new { type = "start", commandId = "peer-start", expectedLobbyRevision = lobbyRevision })).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real peer cannot Start");
        await KeepJoinedPeersAlive("before prepare");
        var preparedAck = await first.CommandAsync(new { type = "start", commandId = "initial-start", expectedLobbyRevision = lobbyRevision });
        Check(preparedAck.GetProperty("accepted").GetBoolean(), "real host Start applies/generates only after everyone Ready");
        var preparedState = await first.StateAsync(preparedAck.GetProperty("stateSequence").GetInt64());
        var galaxy = preparedState.GetProperty("galaxy");
        var worldFingerprint = galaxy.GetProperty("worldFingerprint").GetString()!;
        var expectedSystemCount = galaxy.GetProperty("scenarios").EnumerateArray().Single(s => s.GetProperty("id").GetString() == galaxy.GetProperty("settings").GetProperty("scenarioId").GetString()).GetProperty("stars").GetInt32();
        Check(galaxy.GetProperty("stage").GetString() == "awaitingAssets" && preparedState.GetProperty("spatial").GetProperty("systems").GetArrayLength() == 0
            && galaxy.GetProperty("startPending").GetBoolean() && galaxy.GetProperty("lobbyRevision").GetInt64() == lobbyRevision
            && preparedState.GetProperty("participants").EnumerateArray().All(p => !p.GetProperty("assetReady").GetBoolean())
            && preparedState.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("role").GetString() == "player").All(p => p.GetProperty("ready").GetBoolean()), "real prepared world retains consent and awaits only asset ACK");
        Check((await first.CommandAsync(new { type = "start", commandId = "duplicate-initial-start", expectedLobbyRevision = lobbyRevision })).GetProperty("code").GetString() == "INVALID_PHASE", "real duplicate initial Start cannot regenerate");
        var kick = await first.CommandAsync(new { type = "kickParticipant", commandId = "kick-second", targetParticipantId = second.Id });
        Check(kick.GetProperty("accepted").GetBoolean(), "real host removes a connected player from the prepared lobby");
        var removal = await second.ErrorAsync();
        Check(removal.GetProperty("type").GetString() == "error" && removal.GetProperty("code").GetString() == "LOBBY_REMOVED"
            && !string.IsNullOrWhiteSpace(removal.GetProperty("message").GetString()), "removed peer receives explicit LOBBY_REMOVED before WS termination");
        Check(await second.AcknowledgeRemovalCloseAsync(), "removed WebSocket closes with a policy violation after the error frame");
        var removedState = await first.StateAsync(kick.GetProperty("stateSequence").GetInt64());
        Check(removedState.GetProperty("galaxy").GetProperty("stage").GetString() == "unprepared"
            && !removedState.GetProperty("galaxy").GetProperty("startPending").GetBoolean()
            && removedState.GetProperty("participants").EnumerateArray().Where(p => p.GetProperty("role").GetString() == "player").All(p => !p.GetProperty("ready").GetBoolean()),
            "player removal invalidates candidate and all player votes");
        await using (var kickedToken = await Peer.JoinAsync(wsUrl, "Второй", token: second.Token, expectWelcome: false))
            Check(kickedToken.First.GetProperty("type").GetString() == "error", "kicked token cannot reclaim its participant slot");
        var removedId = second.Id;
        await second.DisposeAsync();
        second = await Peer.JoinAsync(wsUrl, "Второй заново");
        Check(second.Id > removedId, "new player entry receives a monotonic participant ID");
        Check((await second.CommandAsync(new { type = "selectEmpireTemplate", commandId = "template-after-membership", templateId = "scientific" })).GetProperty("accepted").GetBoolean(), "replacement player selects a fresh template");
        var resetState = await second.StateAsync();
        lobbyRevision = resetState.GetProperty("galaxy").GetProperty("lobbyRevision").GetInt64();
        foreach (var peer in new[] { first, second })
            Check((await peer.CommandAsync(new { type = "ready", commandId = "ready-after-membership", expectedLobbyRevision = lobbyRevision, ready = true })).GetProperty("accepted").GetBoolean(), "real membership revision requires fresh Ready");
        preparedAck = await first.CommandAsync(new { type = "start", commandId = "start-after-membership", expectedLobbyRevision = lobbyRevision });
        Check(preparedAck.GetProperty("accepted").GetBoolean(), "real host prepares a new candidate after roster change");
        preparedState = await first.StateAsync(preparedAck.GetProperty("stateSequence").GetInt64());
        galaxy = preparedState.GetProperty("galaxy");
        worldFingerprint = galaxy.GetProperty("worldFingerprint").GetString()!;
        expectedSystemCount = galaxy.GetProperty("scenarios").EnumerateArray().Single(s => s.GetProperty("id").GetString() == galaxy.GetProperty("settings").GetProperty("scenarioId").GetString()).GetProperty("stars").GetInt32();
        using (var currentManifest = JsonDocument.Parse(await http.GetStringAsync("/m3/asset-manifest")))
        {
            var current = currentManifest.RootElement;
            Check(current.TryGetProperty("generatedRequirements", out var requirements) && requirements.GetProperty("classIds").GetArrayLength() > 10,
                "HTTP current manifest contains actual generated requirements");
            profile = current.GetProperty("profileId").GetString()!;
            digest = current.GetProperty("profileDigest").GetString()!;
            Check(profile == preparedState.GetProperty("assetProfile").GetProperty("profileId").GetString() && digest == preparedState.GetProperty("assetProfile").GetProperty("profileDigest").GetString(), "HTTP current manifest matches prepared session profile/digest");
        }
        Check((await observer.CommandAsync(new { type = "assetReady", commandId = "observer-assets", profileId = profile, profileDigest = digest, galaxyWorldFingerprint = worldFingerprint, ready = true })).GetProperty("accepted").GetBoolean(), "observer current generated profile confirmation");
        Check((await observer.CommandAsync(new { type = "observerView", commandId = "pending-view", targetParticipantId = first.Id })).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real pending POV rejected");
        Check((await first.CommandAsync(new { type = "approveObserver", commandId = "approve-observer", targetParticipantId = observer.Id })).GetProperty("accepted").GetBoolean(), "approve real observer");
        await KeepJoinedPeersAlive("after observer approval");
        var result = await first.CommandAsync(new { type = "start", commandId = "before-assets", expectedLobbyRevision = lobbyRevision, profileId = profile, profileDigest = digest, galaxyWorldFingerprint = worldFingerprint });
        Check(result.GetProperty("code").GetString() == "INVALID_PHASE", "internal Start waits for both asset ACKs");
        foreach (var peer in new[] { first, second })
        {
            Check((await peer.CommandAsync(new { type = "assetReady", commandId = "stale-assets", profileId = profile, profileDigest = digest, galaxyWorldFingerprint = "stale", ready = true })).GetProperty("code").GetString() == "STALE_GALAXY_CANDIDATE", "real stale world asset ACK rejected");
            Check((await peer.CommandAsync(new { type = "assetReady", commandId = "assets", profileId = profile, profileDigest = digest, galaxyWorldFingerprint = worldFingerprint, ready = true })).GetProperty("accepted").GetBoolean(), "current profile/world confirmation");
            Check((await peer.CommandAsync(new { type = "configure", commandId = "configure", traits = Array.Empty<string>(), buildings = Array.Empty<string>() })).GetProperty("code").GetString() == "PRESET_CONFIGURATION_ONLY", "real production custom configure rejected");
        }
        await KeepJoinedPeersAlive("after readiness");
        Check((await first.CommandAsync(new { type = "start", commandId = "stale-start", expectedLobbyRevision = lobbyRevision, profileId = profile, profileDigest = digest, galaxyWorldFingerprint = "stale" })).GetProperty("code").GetString() == "STALE_GALAXY_CANDIDATE", "real stale candidate start rejected");
        var finalStart = await first.CommandAsync(new { type = "start", commandId = "start", expectedLobbyRevision = lobbyRevision, profileId = profile, profileDigest = digest, galaxyWorldFingerprint = worldFingerprint });
        Check(finalStart.GetProperty("accepted").GetBoolean(), "internal Start freezes candidate without second Ready");
        if (args.Contains("--lobby-only"))
        {
            var running = await first.StateAsync(finalStart.GetProperty("stateSequence").GetInt64());
            Check(running.GetProperty("phase").GetString() == "running" && !running.GetProperty("galaxy").GetProperty("startPending").GetBoolean()
                && running.GetProperty("galaxy").GetProperty("worldFingerprint").GetString() == worldFingerprint, "real2player+observer Ready-first lobby enters exact generated world");
            Console.WriteLine($"PASS lobby-only: {checks}");
            return;
        }
        Check((await first.CommandAsync(new { type = "pause", commandId = "pause", paused = true })).GetProperty("accepted").GetBoolean(), "pause");
        var step = await first.CommandAsync(new { type = "stepDay", commandId = "step" });
        Check(step.GetProperty("accepted").GetBoolean() && step.GetProperty("appliedTick").GetInt64() == 1, "one exact day");
        Check((await first.CommandAsync(new { type = "stepDay", commandId = "step" })).GetProperty("appliedTick").GetInt64() == 1, "duplicate ACK does not step twice");
        Check((await first.CommandAsync(new { type = "step", commandId = "old-step" })).GetProperty("code").GetString() == "INVALID_COMMAND", "old step rejected");
        Check((await first.CommandAsync(new { type = "speed", commandId = "missing-speed" })).GetProperty("code").GetString() == "INVALID_COMMAND", "missing argument result");
        await KeepJoinedPeersAlive("after start");
        var state = await first.StateAsync();
        Check(state.GetProperty("spatial").GetProperty("systems").GetArrayLength() == expectedSystemCount && expectedSystemCount > 16 && state.GetProperty("planets").GetArrayLength() == 1, "real generated spatial and private economy");
        Check(state.GetProperty("galaxy").GetProperty("worldFingerprint").GetString() == worldFingerprint && state.GetProperty("galaxy").GetProperty("stage").GetString() == "running", "real start preserved prepared world fingerprint");
        var actualPlanetId = state.GetProperty("planets").EnumerateArray().Single().GetProperty("id").GetInt32();
        Check(state.GetProperty("spatial").GetProperty("colonies").EnumerateArray().Single(c => c.GetProperty("ownerParticipantId").GetInt32() == first.Id).GetProperty("economyPlanetId").GetInt32() == actualPlanetId, "real generated colony uses actual economy planet ID");
        Check(!state.GetRawText().Contains(first.Token, StringComparison.Ordinal), "no token in snapshot");
        var correlated = await Task.WhenAll(
            first.CommandAsync(new { type = "speed", commandId = "peer-correlation-invalid" }),
            first.CommandAsync(new { type = "pause", commandId = "peer-correlation-valid", paused = true }));
        Check(correlated[0].GetProperty("commandId").GetString() == "peer-correlation-invalid"
            && correlated[0].GetProperty("code").GetString() == "INVALID_COMMAND", "continuous reader correlates rejected ACK by commandId");
        Check(correlated[1].GetProperty("commandId").GetString() == "peer-correlation-valid"
            && correlated[1].GetProperty("accepted").GetBoolean(), "continuous reader correlates concurrent accepted ACK by commandId");
        JsonElement OwnDevelopmentColony(JsonElement snapshot) => snapshot.GetProperty("development").GetProperty("colonies").EnumerateArray().Single();
        JsonElement OwnResearch(JsonElement snapshot) => snapshot.GetProperty("research").GetProperty("empires").EnumerateArray().Single();
        JsonElement ResearchArea(JsonElement snapshot, string area) => OwnResearch(snapshot).GetProperty("areas").EnumerateArray().Single(item => item.GetProperty("id").GetString() == area);
        var initialResearch = OwnResearch(state);
        Check(initialResearch.GetProperty("participantId").GetInt32() == first.Id && initialResearch.GetProperty("areas").GetArrayLength() == 3
            && initialResearch.GetProperty("areas").EnumerateArray().All(area => area.TryGetProperty("progress", out _) && area.TryGetProperty("monthlyResearch", out _) && area.TryGetProperty("reserve", out _) && area.GetProperty("choices").GetArrayLength() > 0),
            "WebSocket: три поля науки и собственная империя в снимке");
        var secondResearchBefore = OwnResearch(await second.StateAsync());
        var researchCards = initialResearch.GetProperty("areas").EnumerateArray().SelectMany(area => area.GetProperty("choices").EnumerateArray())
            .Concat(initialResearch.GetProperty("researched").EnumerateArray()).ToArray();
        Check(researchCards.All(card => card.GetProperty("name").GetString() == card.GetProperty("id").GetString())
            && researchCards.Any(card => card.TryGetProperty("nameKey", out var key) && key.ValueKind == JsonValueKind.String)
            && researchCards.All(card => !card.TryGetProperty("nameKey", out var key) || key.ValueKind == JsonValueKind.Null || key.GetString() == card.GetProperty("id").GetString()),
            "WebSocket: технологии передают проверенные native keys и IDs без оригинальных переводов");
        var researchEffects = researchCards.SelectMany(card => card.GetProperty("effects").EnumerateArray()).ToArray();
        Check(researchEffects.Where(effect => effect.TryGetProperty("amount", out var value) && value.ValueKind == JsonValueKind.Number)
            .All(effect => effect.GetProperty("format").GetString() is "number" or "percent")
            && researchEffects.Any(effect => effect.TryGetProperty("sourceKey", out var key) && key.GetString() == "unlock_building"
                && effect.GetProperty("targetDefinitionId").ValueKind == JsonValueKind.String),
            "WebSocket: численные эффекты и здания передаются структурированными значениями");
        Check(secondResearchBefore.GetProperty("participantId").GetInt32() == second.Id && secondResearchBefore.GetProperty("areas").EnumerateArray().All(area => area.GetProperty("activeTechnologyId").ValueKind == JsonValueKind.Null), "WebSocket: исследования игроков изолированы");
        var offeredPhysics = ResearchArea(state, "physics").GetProperty("choices")[0].GetProperty("id").GetString()!;
        var researchRevision = initialResearch.GetProperty("revision").GetInt64();
        var researchRequest = new { type = "selectResearch", commandId = "wire-select-research", area = "physics", technologyId = offeredPhysics, expectedResearchRevision = researchRevision };
        await Task.Delay(100);
        var researchAck = await first.CommandAsync(researchRequest);
        Check(researchAck.GetProperty("accepted").GetBoolean(), "WebSocket: исходная предложенная технология выбрана через wire");
        await Task.Delay(100);
        Check((await first.CommandAsync(researchRequest)).GetRawText() == researchAck.GetRawText(), "WebSocket: повтор выбора вернул исходный ACK");
        await Task.Delay(100);
        Check((await first.CommandAsync(new { type = "selectResearch", commandId = "wire-stale-research", area = "physics", technologyId = offeredPhysics, expectedResearchRevision = researchRevision })).GetProperty("code").GetString() == "STALE_REVISION", "WebSocket: устаревшая research revision отклонена");
        await Task.Delay(100);
        Check((await first.CommandAsync(new { type = "selectResearch", commandId = "wire-negative-research", area = "physics", technologyId = offeredPhysics, expectedResearchRevision = -1L })).GetProperty("code").GetString() == "INVALID_COMMAND", "WebSocket: отрицательная research revision отклонена семантически");
        await Task.Delay(100);
        Check((await first.CommandAsync(new { type = "selectResearch", commandId = "wire-null-research", area = "physics", technologyId = (string?)null, expectedResearchRevision = researchRevision })).GetProperty("code").GetString() == "INVALID_COMMAND", "WebSocket: null technology ID отклонён семантически");
        Check((await observer.CommandAsync(researchRequest)).GetProperty("code").GetString() == "OUT_OF_SCOPE", "WebSocket: наблюдатель не может выбирать исследование");
        state = await first.StateAsync(researchAck.GetProperty("stateSequence").GetInt64());
        Check(ResearchArea(state, "physics").GetProperty("activeTechnologyId").GetString() == offeredPhysics
            && OwnResearch(await second.StateAsync()).GetProperty("areas").EnumerateArray().All(area => area.GetProperty("activeTechnologyId").ValueKind == JsonValueKind.Null), "WebSocket: принятый выбор изменил только свою империю");
        decimal JobValue(JsonElement snapshot, string jobId, string property) => OwnDevelopmentColony(snapshot).GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(item => item.GetProperty("jobId").GetString() == jobId).GetProperty(property).GetDecimal();
        async Task<JsonElement> ApplyWorkforceLimit(Peer peer, string commandId, string type, string jobId, decimal? count = null)
        {
            var before = await peer.StateAsync();
            var colony = OwnDevelopmentColony(before);
            var request = new Dictionary<string, object?>
            {
                ["type"] = type,
                ["commandId"] = commandId,
                ["colonyId"] = colony.GetProperty("colonyId").GetString(),
                ["expectedColonyRevision"] = colony.GetProperty("revision").GetInt64(),
                ["job"] = jobId
            };
            if (count is { } absolute) { request["count"] = absolute; }
            await Task.Delay(100);
            var result = await peer.CommandAsync(request);
            Check(result.GetProperty("accepted").GetBoolean(), $"real WebSocket workforce {type}/{jobId}");
            var updated = await peer.StateAsync(result.GetProperty("stateSequence").GetInt64());
            await KeepJoinedPeersAlive("during workforce funding");
            return updated;
        }
        var mineralsBeforeFunding = OwnDevelopmentColony(state).GetProperty("economy").GetProperty("resources").EnumerateArray().Single(item => item.GetProperty("resourceId").GetString() == "minerals").GetProperty("stock").GetDecimal();
        var workforceColony = OwnDevelopmentColony(state);
        var workforceColonyId = workforceColony.GetProperty("colonyId").GetString()!;
        var initialWorkforceRevision = workforceColony.GetProperty("revision").GetInt64();
        var minerCapacity = JobValue(state, "miner", "capacity");
        Check(minerCapacity > 0, "real WebSocket workforce funding has source miner capacity");
        await Task.Delay(100);
        var minerMinimum = await first.CommandAsync(new { type = "setWorkforceLimitMin", commandId = "wire-miner-min", colonyId = workforceColonyId, expectedColonyRevision = initialWorkforceRevision, job = "miner" });
        Check(minerMinimum.GetProperty("accepted").GetBoolean(), "real WebSocket setWorkforceLimitMin accepted");
        state = await first.StateAsync(minerMinimum.GetProperty("stateSequence").GetInt64());
        Check(JobValue(state, "miner", "limit") == 0, "real WebSocket setWorkforceLimitMin applied zero");
        var minerRevision = OwnDevelopmentColony(state).GetProperty("revision").GetInt64();
        var adjustUpRequest = new { type = "adjustWorkforceLimit", commandId = "wire-miner-up", colonyId = workforceColonyId, expectedColonyRevision = minerRevision, job = "miner", count = 1 };
        await Task.Delay(100);
        var adjustUp = await first.CommandAsync(adjustUpRequest);
        Check(adjustUp.GetProperty("accepted").GetBoolean(), "real WebSocket 5% adjust accepted");
        state = await first.StateAsync(adjustUp.GetProperty("stateSequence").GetInt64());
        Check(JobValue(state, "miner", "limit") == minerCapacity * 0.05m, "real WebSocket adjust changed limit by exactly 5% capacity");
        await Task.Delay(100);
        Check((await first.CommandAsync(adjustUpRequest)).GetRawText() == adjustUp.GetRawText(), "real WebSocket workforce retry returns original ACK");
        await Task.Delay(100);
        var staleLimit = await first.CommandAsync(new { type = "adjustWorkforceLimit", commandId = "wire-miner-stale", colonyId = workforceColonyId, expectedColonyRevision = minerRevision, job = "miner", count = -1 });
        Check(staleLimit.GetProperty("code").GetString() == "STALE_REVISION", "real WebSocket workforce stale revision refused");
        Check(JobValue(await first.StateAsync(adjustUp.GetProperty("stateSequence").GetInt64()), "miner", "limit") == minerCapacity * 0.05m, "replay and stale workforce commands leave the accepted limit unchanged");
        state = await ApplyWorkforceLimit(first, "wire-miner-down", "adjustWorkforceLimit", "miner", -1);
        Check(JobValue(state, "miner", "limit") == 0, "real WebSocket negative 5% adjust accepted");
        state = await ApplyWorkforceLimit(first, "wire-miner-max", "setWorkforceLimitMax", "miner");
        Check(JobValue(state, "miner", "limit") == minerCapacity, "real WebSocket setWorkforceLimitMax applied capacity");
        state = await ApplyWorkforceLimit(first, "wire-miner-absolute", "setWorkforceLimit", "miner", minerCapacity / 2m);
        Check(JobValue(state, "miner", "limit") == minerCapacity / 2m, "real WebSocket absolute WorkforceLimit applied");
        await Task.Delay(100);
        var foreignWorkforce = await second.CommandAsync(new { type = "setWorkforceLimitMin", commandId = "wire-foreign-limit", colonyId = workforceColonyId, expectedColonyRevision = OwnDevelopmentColony(state).GetProperty("revision").GetInt64(), job = "miner" });
        Check(foreignWorkforce.GetProperty("code").GetString() == "OUT_OF_SCOPE", "real second player cannot adjust foreign workforce");

        foreach (var (peer, prefix) in new[] { (first, "wire-fund-a-"), (second, "wire-fund-b-") })
        {
            foreach (var jobId in new[] { "foundry", "artisan", "physicist", "biologist", "engineer", "miner" })
            {
                var current = await peer.StateAsync();
                var job = OwnDevelopmentColony(current).GetProperty("economy").GetProperty("jobs").EnumerateArray().SingleOrDefault(item => item.GetProperty("jobId").GetString() == jobId);
                if (job.ValueKind == JsonValueKind.Undefined || job.GetProperty("capacity").GetDecimal() <= 0) { continue; }
                var capacity = job.GetProperty("capacity").GetDecimal();
                var researchProducer = peer.Id == first.Id && jobId == "physicist";
                var commandType = jobId == "miner" ? "setWorkforceLimitMax" : jobId == "artisan" || researchProducer ? "setWorkforceLimit" : "setWorkforceLimitMin";
                state = await ApplyWorkforceLimit(peer, prefix + jobId, commandType, jobId, jobId == "artisan" ? capacity / 2m : researchProducer ? capacity / 100m : null);
            }
        }
        await KeepJoinedPeersAlive("before funding month");
        var focusedState = await first.StateAsync();
        var oldPhysicsReserve = ResearchArea(focusedState, "physics").GetProperty("reserve").GetDecimal();
        var oldPhysicsProgress = ResearchArea(focusedState, "physics").GetProperty("progress").GetDecimal();
        var elapsedBeforeFundingSettlement = focusedState.GetProperty("elapsedDay").GetInt32();
        var daysToSettlement = 30 - elapsedBeforeFundingSettlement % 30;
        long fundingSequence = focusedState.GetProperty("sequence").GetInt64();
        for (var day = 0; day < daysToSettlement; day++)
        {
            await Task.Delay(100);
            var fundingDay = await first.CommandAsync(new { type = "stepDay", commandId = "wire-funding-day-" + day.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            Check(fundingDay.GetProperty("accepted").GetBoolean(), "real WebSocket funding day accepted");
            fundingSequence = fundingDay.GetProperty("stateSequence").GetInt64();
            if ((day + 1) % 5 == 0)
            {
                Check(await second.PingAsync(), "second player funding heartbeat");
                Check(await observer.PingAsync(), "observer funding heartbeat");
            }
        }
        state = await first.StateAsync(fundingSequence);
        var settledPhysics = ResearchArea(state, "physics");
        var physicsResource = OwnDevelopmentColony(state).GetProperty("economy").GetProperty("resources").EnumerateArray().Single(item => item.GetProperty("resourceId").GetString() == "physics_research");
        var monthlyPhysics = settledPhysics.GetProperty("monthlyResearch").GetDecimal();
        var consumedReserve = Math.Min(oldPhysicsReserve, monthlyPhysics);
        // Economic preset: source materialist +.05 и no-leader -.25 складываются один раз.
        Check(monthlyPhysics > 0 && monthlyPhysics == physicsResource.GetProperty("net").GetDecimal() * 0.80m
            && settledPhysics.GetProperty("activeTechnologyId").GetString() == offeredPhysics
            && settledPhysics.GetProperty("progress").GetDecimal() == oldPhysicsProgress + monthlyPhysics + consumedReserve,
            "WebSocket: source месячная наука учитывает ethic и штраф без лидера без двойного умножения");
        Check(settledPhysics.GetProperty("reserve").GetDecimal() == oldPhysicsReserve - consumedReserve
            && physicsResource.GetProperty("stock").GetDecimal() == settledPhysics.GetProperty("reserve").GetDecimal(),
            "WebSocket: economy stock и research reserve согласованы без двойного зачисления выпуска");
        var fundedMinerals = OwnDevelopmentColony(state).GetProperty("economy").GetProperty("resources").EnumerateArray().Single(item => item.GetProperty("resourceId").GetString() == "minerals");
        var fundedConsumerGoods = OwnDevelopmentColony(state).GetProperty("economy").GetProperty("resources").EnumerateArray().Single(item => item.GetProperty("resourceId").GetString() == "consumer_goods");
        Check(fundedMinerals.GetProperty("net").GetDecimal() > 0 && fundedConsumerGoods.GetProperty("net").GetDecimal() > 0
            && fundedMinerals.GetProperty("stock").GetDecimal() > mineralsBeforeFunding, "real WebSocket workforce limits funded source minerals across monthly settlement");
        Console.WriteLine($"WebSocket funded monthly net: minerals +{fundedMinerals.GetProperty("net").GetDecimal()}; consumer_goods +{fundedConsumerGoods.GetProperty("net").GetDecimal()}.");
        var secondFunded = await second.StateAsync(fundingSequence);
        var secondMinerals = OwnDevelopmentColony(secondFunded).GetProperty("economy").GetProperty("resources").EnumerateArray().Single(item => item.GetProperty("resourceId").GetString() == "minerals");
        Check(secondMinerals.GetProperty("net").GetDecimal() >= 0, "second player real workforce funding remained solvent through shared settlement");
        var receivedExits = state.GetProperty("spatial").GetProperty("exits");
        var detailSystems = state.GetProperty("spatial").GetProperty("bodies").EnumerateArray().Where(body => body.GetProperty("kind").GetString() != "star").Select(body => body.GetProperty("systemId").GetString()!).ToHashSet(StringComparer.Ordinal);
        var expectedExits = state.GetProperty("spatial").GetProperty("hyperlanes").EnumerateArray().SelectMany(lane => new[] {
            (From: lane.GetProperty("fromSystemId").GetString()!, To: lane.GetProperty("toSystemId").GetString()!),
            (From: lane.GetProperty("toSystemId").GetString()!, To: lane.GetProperty("fromSystemId").GetString()!)
        }).Where(edge => detailSystems.Contains(edge.From)).ToHashSet();
        var actualExits = receivedExits.EnumerateArray().Select(exit => (From: exit.GetProperty("systemId").GetString()!, To: exit.GetProperty("destinationSystemId").GetString()!)).ToHashSet();
        Check(receivedExits.GetArrayLength() == expectedExits.Count && actualExits.SetEquals(expectedExits)
            && receivedExits.EnumerateArray().All(exit => exit.GetProperty("position").TryGetProperty("x", out var x) && exit.GetProperty("position").TryGetProperty("z", out var z) && double.IsFinite(x.GetDouble()) && double.IsFinite(z.GetDouble())), "real exits match every permitted detail-system lane endpoint with finite coordinates");
        Check(state.GetProperty("empireSetup").GetProperty("selectedTemplateId").GetString() == "economic", "real own explicit template projection");
        var constructionColony = state.GetProperty("development").GetProperty("colonies").EnumerateArray().Single().GetProperty("colonyId").GetString();
        var constructionRevision = state.GetProperty("development").GetProperty("colonies").EnumerateArray().Single().GetProperty("revision").GetInt64();
        var constructionRequest = new { type = "enqueueConstruction", commandId = "construction-refusal", colonyId = constructionColony, expectedColonyRevision = constructionRevision, definitionId = "building_hydroponics_farm", slotId = "unresolved-slot", orderId = "order" };
        var constructionRefusal = await first.CommandAsync(constructionRequest);
        Check(constructionRefusal.GetProperty("code").GetString() == "slot-unavailable", "real construction rejects an unresolved slot");
        Check((await first.CommandAsync(constructionRequest)).GetRawText() == constructionRefusal.GetRawText(), "real construction exact refusal retry");
        Check((await second.CommandAsync(constructionRequest)).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real foreign colony construction refused");
        Check((await observer.CommandAsync(constructionRequest)).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real observer construction refused");
        Check((await first.CommandAsync(new { type = "cancelConstruction", commandId = "cancel-refusal", colonyId = constructionColony, expectedColonyRevision = constructionRevision, orderId = "order" })).GetProperty("code").GetString() == "order-unavailable", "real cancel rejects a missing order");
        Check((await first.CommandAsync(new { type = "clearBlocker", commandId = "clear-refusal", colonyId = constructionColony, expectedColonyRevision = constructionRevision, definitionId = "d_failing_infrastructure", slotId = "unresolved-slot", orderId = "blocker-order" })).GetProperty("code").GetString() == "clearance-lifecycle-unresolved", "real blocker reports unresolved clearance lifecycle");
        Check((await first.CommandAsync(new { type = "cancelConstruction", commandId = "negative-revision", colonyId = constructionColony, expectedColonyRevision = -1L, orderId = "order" })).GetProperty("code").GetString() == "INVALID_COMMAND", "real negative revision reaches semantic refusal");
        var fleet = state.GetProperty("spatial").GetProperty("fleets").EnumerateArray().First(item => item.GetProperty("ownerParticipantId").GetInt32() == first.Id);
        Check((await first.CommandAsync(new { type = "moveFleetToPoint", commandId = "move", fleetId = fleet.GetProperty("id").GetString(), systemId = fleet.GetProperty("location").GetProperty("systemId").GetString(), x = 0, z = 0 })).GetProperty("accepted").GetBoolean(), "owned movement");
        var originSystem = fleet.GetProperty("location").GetProperty("systemId").GetString()!;
        var adjacentLane = state.GetProperty("spatial").GetProperty("hyperlanes").EnumerateArray().First(lane => lane.GetProperty("fromSystemId").GetString() == originSystem || lane.GetProperty("toSystemId").GetString() == originSystem);
        var destinationSystem = adjacentLane.GetProperty("fromSystemId").GetString() == originSystem ? adjacentLane.GetProperty("toSystemId").GetString()! : adjacentLane.GetProperty("fromSystemId").GetString()!;
        var navigationAck = await first.CommandAsync(new { type = "moveFleetToSystem", commandId = "generated-navigation", fleetId = fleet.GetProperty("id").GetString(), targetSystemId = destinationSystem });
        Check(navigationAck.GetProperty("accepted").GetBoolean(), "real navigation targets an actual generated lane endpoint");
        var navigating = await first.StateAsync(navigationAck.GetProperty("stateSequence").GetInt64());
        var route = navigating.GetProperty("spatial").GetProperty("fleets").EnumerateArray().Single(f => f.GetProperty("id").GetString() == fleet.GetProperty("id").GetString()).GetProperty("movement").GetProperty("routeSystemIds").EnumerateArray().Select(id => id.GetString()).ToArray();
        Check(route.Length >= 1 && route.Last() == destinationSystem && route.All(id => navigating.GetProperty("spatial").GetProperty("systems").EnumerateArray().Any(system => system.GetProperty("id").GetString() == id)), "real generated route references current permitted system IDs");
        Check(!(await second.CommandAsync(new { type = "stopFleet", commandId = "foreign", fleetId = fleet.GetProperty("id").GetString() })).GetProperty("accepted").GetBoolean(), "foreign fleet rejected");
        var povAck = await observer.CommandAsync(new { type = "observerView", commandId = "view-first", targetParticipantId = first.Id });
        Check(povAck.GetProperty("accepted").GetBoolean(), "real observer POV accepted");
        var povSequence = povAck.GetProperty("stateSequence").GetInt64();
        var povState = await observer.StateAsync(povSequence);
        var firstPov = await first.StateAsync(povSequence);
        Check(povState.GetProperty("you").GetInt32() == observer.Id && povState.GetProperty("viewContext").GetProperty("participantId").GetInt32() == first.Id && povState.GetProperty("participants").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == observer.Id).GetProperty("role").GetString() == "observer", "real POV preserves observer control identity");
        Check(povState.GetProperty("spatial").GetRawText() == firstPov.GetProperty("spatial").GetRawText() && povState.GetProperty("planets").GetRawText() == firstPov.GetProperty("planets").GetRawText(), "real POV matches target player spatial and economy JSON");
        Check(povState.GetProperty("empireSetup").GetProperty("selectedTemplateId").GetString() == "economic", "real first POV selected template");
        Check(povState.GetProperty("spatial").GetProperty("colonies").EnumerateArray().Where(c => c.GetProperty("ownerParticipantId").GetInt32() != first.Id).All(c => !c.TryGetProperty("economyPlanetId", out _)) && povState.GetProperty("spatial").GetProperty("fleets").EnumerateArray().Where(f => f.GetProperty("ownerParticipantId").GetInt32() != first.Id).All(f => !f.TryGetProperty("movement", out _)), "real POV omits foreign links and movement");
        Check((await observer.CommandAsync(new { type = "stopFleet", commandId = "observer-mutation", fleetId = fleet.GetProperty("id").GetString() })).GetProperty("code").GetString() == "OUT_OF_SCOPE", "real observer POV remains read-only");
        Check((await observer.CommandAsync(new { type = "observerView", commandId = "missing-observer-target" })).GetProperty("code").GetString() == "INVALID_COMMAND", "real missing POV target rejected");
        Check((await observer.CommandAsync(new { type = "observerView", commandId = "null-observer-target", targetParticipantId = (int?)null })).GetProperty("code").GetString() == "INVALID_COMMAND", "real null POV target rejected");
        var secondPovAck = await observer.CommandAsync(new { type = "observerView", commandId = "view-second", targetParticipantId = second.Id });
        Check(secondPovAck.GetProperty("accepted").GetBoolean(), "real second POV accepted");
        var secondPovSequence = secondPovAck.GetProperty("stateSequence").GetInt64();
        var secondPovState = await observer.StateAsync(secondPovSequence);
        var secondPlayerState = await second.StateAsync(secondPovSequence);
        Check(secondPovState.GetProperty("spatial").GetRawText() == secondPlayerState.GetProperty("spatial").GetRawText() && secondPovState.GetProperty("planets").GetRawText() == secondPlayerState.GetProperty("planets").GetRawText(), "real second POV matches target player JSON");
        Check(secondPovState.GetProperty("empireSetup").GetProperty("selectedTemplateId").GetString() == "scientific", "real second POV selected template");
        var generalAck = await observer.CommandAsync(new { type = "observerView", commandId = "view-general", targetParticipantId = 0 });
        Check(generalAck.GetProperty("accepted").GetBoolean(), "real general view accepted");
        var generalState = await observer.StateAsync(generalAck.GetProperty("stateSequence").GetInt64());
        Check(generalState.GetProperty("viewContext").GetProperty("mode").GetString() == "general" && generalState.GetProperty("viewContext").GetProperty("participantId").ValueKind == JsonValueKind.Null && generalState.GetProperty("planets").GetArrayLength() == 2, "real general context has explicit null and full authorized economy");
        Check(generalState.GetProperty("empireSetup").GetProperty("selectedTemplateId").ValueKind == JsonValueKind.Null, "real general observer hides individual template selection");
        var token = first.Token;
        first.Abort();
        await Task.Delay(250);
        await using var returning = await Peer.JoinAsync(wsUrl, "Первый", token: token);
        Check(returning.Id == first.Id, "reconnect same participant");
        Check((await returning.StateAsync()).GetProperty("empireSetup").GetProperty("selectedTemplateId").GetString() == "economic", "real reconnect retains explicit template");
        Check((await returning.CommandAsync(new { type = "stepDay", commandId = "reconnect-step" })).GetProperty("code").GetString() == "ASSETS_NOT_READY", "new connection resets assets");
        await using var invalid = await Peer.JoinAsync(wsUrl, "Наблюдатель", role: "observer", expectWelcome: false);
        Check(invalid.First.GetProperty("type").GetString() == "error", "observer rejected after start");
        Check(await observer.MalformedRefusedAsync(new { type = "selectResearch", commandId = "wire-extra-research", area = "physics", technologyId = offeredPhysics, expectedResearchRevision = 0L, count = 1 }), "WebSocket: лишнее поле исследования завершает malformed соединение");
        await second.DisposeAsync();
    }
    finally
    {
        stop.Cancel();
        Check(await server.WaitAsync(TimeSpan.FromSeconds(10)) == 0, "server clean shutdown");
    }
}
else
{
    Console.WriteLine("SKIP real transport: задайте --game-root PATH.");
}
Console.WriteLine($"PASS: {checks}");

sealed class ControlledSocket : WebSocket
{
    public readonly ConcurrentQueue<byte[]> Messages = new();
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken LastToken;
    private readonly SemaphoreSlim _release = new(0);
    private int _concurrent;
    private int _maxConcurrent;
    public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => WebSocketState.Open;
    public override string? SubProtocol => null;
    public override void Abort() => throw new NotSupportedException("Дубль проверяет только send.");
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => throw new NotSupportedException("Дубль проверяет только send.");
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => throw new NotSupportedException("Дубль проверяет только send.");
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException("Дубль проверяет только send.");
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => SendAsync(buffer.AsMemory(), messageType, endOfMessage, cancellationToken).AsTask();
    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken = default)
    {
        var concurrent = Interlocked.Increment(ref _concurrent);
        var previous = MaxConcurrent;
        while (concurrent > previous)
        {
            var observed = Interlocked.CompareExchange(ref _maxConcurrent, concurrent, previous);
            if (observed == previous)
            {
                break;
            }
            previous = observed;
        }
        LastToken = cancellationToken;
        Messages.Enqueue(buffer.ToArray());
        Entered.TrySetResult();
        try
        {
            await _release.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _concurrent);
        }
    }
    public void Release(int count) => _release.Release(count);
    public override void Dispose() => _release.Dispose();
}

sealed class RefusalSocket : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;
    public TaskCompletionSource Closing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Acknowledge { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public byte[]? Message { get; private set; }
    public int Sends { get; private set; }
    public int Aborts { get; private set; }
    public bool FailSend { get; init; }
    public override WebSocketState State => _state;
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;
    public override void Abort() { Aborts++; _state = WebSocketState.Aborted; }
    public override async Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        if (Message == null || closeStatus != WebSocketCloseStatus.PolicyViolation)
            throw new InvalidOperationException("Close до error или неверный статус.");
        _state = WebSocketState.CloseSent;
        Closing.TrySetResult();
        await Acknowledge.Task.WaitAsync(cancellationToken);
        _state = WebSocketState.Closed;
    }
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => SendAsync(buffer.AsMemory(), messageType, endOfMessage, cancellationToken).AsTask();
    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken = default)
    {
        Sends++;
        if (FailSend) throw new WebSocketException();
        cancellationToken.ThrowIfCancellationRequested();
        if (messageType != WebSocketMessageType.Text || !endOfMessage) throw new InvalidOperationException("Неверный error frame.");
        Message = buffer.ToArray();
        return ValueTask.CompletedTask;
    }
    public override void Dispose() { }
}

sealed class Peer : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _sendSerial = new(1, 1);
    private readonly SemaphoreSlim _pingSerial = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> _pending = new(StringComparer.Ordinal);
    private TaskCompletionSource<bool>? _pong;
    private TaskCompletionSource _snapshotChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private JsonElement _latestSnapshot;
    private long _snapshotGeneration;
    private Exception? _readerFailure;
    private JsonElement? _terminalError;
    private Task _reader = Task.CompletedTask;
    public JsonElement First;
    public int Id => First.GetProperty("participantId").GetInt32();
    public string Token => First.GetProperty("token").GetString()!;
    public JsonElement LatestState { get { lock (_gate) { RequireReader(); return _latestSnapshot.Clone(); } } }
    public static async Task<Peer> JoinAsync(Uri url, string name, string? token = null, string role = "player", bool expectWelcome = true, int protocol = 2, string code = "M3-test-code")
    {
        var peer = new Peer();
        await peer._socket.ConnectAsync(url, CancellationToken.None);
        await peer.SendAsync(new { type = "join", protocol, name, role, token, joinCode = code });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        peer.First = await peer.ReadAsync(deadline.Token);
        if (expectWelcome && peer.First.GetProperty("type").GetString() != "welcome")
        {
            await peer.DisposeAsync();
            throw new InvalidOperationException("Нет welcome");
        }
        if (peer.First.GetProperty("type").GetString() == "welcome") { peer._reader = peer.ReadLoopAsync(); }
        return peer;
    }
    public void Abort() { _lifetime.Cancel(); _socket.Abort(); }
    public async Task<bool> PingAsync(int timeoutSeconds = 5)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        await _pingSerial.WaitAsync(deadline.Token);
        var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            lock (_gate) { RequireReader(); _pong = waiter; }
            await SendAsync(new { type = "ping" });
            return await waiter.Task.WaitAsync(deadline.Token);
        }
        finally { lock (_gate) { _pong = null; } _pingSerial.Release(); }
    }
    public async Task<JsonElement> CommandAsync(object command, int timeoutSeconds = 5)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        using var document = JsonDocument.Parse(bytes);
        var id = document.RootElement.GetProperty("commandId").GetString() ?? throw new InvalidOperationException("Нет commandId.");
        var waiter = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            RequireReader();
            if (_pending.Count >= 64 || !_pending.TryAdd(id, waiter)) { throw new InvalidOperationException("Harness ACK correlation overflow/conflict."); }
        }
        try
        {
            await SendBytesAsync(bytes);
            // Prepare дополнительно выполняет source hashing и backend generation;
            // остальные ACK сохраняют прежний transport deadline.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(document.RootElement.GetProperty("type").GetString() is "prepareGalaxy" or "start" ? 30 : timeoutSeconds));
            return await waiter.Task.WaitAsync(deadline.Token);
        }
        finally { lock (_gate) { _pending.Remove(id); } }
    }
    public async Task<bool> MalformedRefusedAsync(object command)
    {
        await SendAsync(command);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _reader.WaitAsync(deadline.Token);
        lock (_gate)
        {
            return _readerFailure is WebSocketException
                || _readerFailure is InvalidOperationException error && error.Message is "Не текстовый ответ" or "Protocol error frame.";
        }
    }
    public async Task<JsonElement> ErrorAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _reader.WaitAsync(deadline.Token);
        lock (_gate) { return _terminalError?.Clone() ?? throw new InvalidOperationException("Expected an explicit protocol error frame."); }
    }
    public async Task<bool> AcknowledgeRemovalCloseAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[1024];
        var close = await _socket.ReceiveAsync(buffer.AsMemory(), deadline.Token);
        if (close.MessageType != WebSocketMessageType.Close || _socket.CloseStatus != WebSocketCloseStatus.PolicyViolation) return false;
        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", deadline.Token);
        return true;
    }
    public async Task<JsonElement> StateAsync(long minimumSequence = 0)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        long afterGeneration;
        lock (_gate) { RequireReader(); afterGeneration = _snapshotGeneration; }
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                RequireReader();
                if (_snapshotGeneration > afterGeneration && _latestSnapshot.GetProperty("sequence").GetInt64() >= minimumSequence)
                { return _latestSnapshot.Clone(); }
                changed = _snapshotChanged.Task;
            }
            await changed.WaitAsync(deadline.Token);
        }
    }
    public async ValueTask DisposeAsync()
    {
        Abort();
        await _reader;
        _socket.Dispose();
        _lifetime.Dispose();
        _sendSerial.Dispose();
        _pingSerial.Dispose();
    }
    private Task SendAsync(object value) => SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(value));
    private async Task SendBytesAsync(byte[] bytes)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sendSerial.WaitAsync(deadline.Token);
        try { await _socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, deadline.Token); }
        finally { _sendSerial.Release(); }
    }
    // Один reader постоянно дренирует каждый connected peer. В памяти один latest snapshot,
    // не более 64 ожидаемых ACK и один pong; чужой ACK не может удовлетворить команду.
    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var message = await ReadAsync(_lifetime.Token);
                lock (_gate)
                {
                    switch (message.GetProperty("type").GetString())
                    {
                        case "snapshot":
                            _latestSnapshot = message.GetProperty("state").Clone();
                            _snapshotGeneration++;
                            var previous = _snapshotChanged;
                            _snapshotChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
                            previous.TrySetResult();
                            break;
                        case "result":
                            var result = message.GetProperty("result");
                            var id = result.GetProperty("commandId").GetString()!;
                            if (!_pending.TryGetValue(id, out var waiter) || !waiter.TrySetResult(result.Clone()))
                            { throw new InvalidOperationException("Unexpected/duplicate harness ACK: " + id); }
                            break;
                        case "pong":
                            if (_pong is null || !_pong.TrySetResult(true)) { throw new InvalidOperationException("Unexpected/duplicate harness pong."); }
                            break;
                        case "error": _terminalError = message.Clone(); throw new InvalidOperationException("Protocol error frame.");
                        default: throw new InvalidOperationException("Unexpected harness message.");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (WebSocketException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (_gate)
            {
                _readerFailure = error;
                foreach (var waiter in _pending.Values) { waiter.TrySetException(error); }
                _pong?.TrySetException(error);
                _snapshotChanged.TrySetResult();
            }
        }
    }
    private void RequireReader()
    {
        if (_readerFailure is not null) { throw new InvalidOperationException("Harness reader failed.", _readerFailure); }
        _lifetime.Token.ThrowIfCancellationRequested();
    }
    private async Task<JsonElement> ReadAsync(CancellationToken cancellation)
    {
        using var stream = new MemoryStream();
        var bytes = new byte[8192];
        ValueWebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(bytes.AsMemory(), cancellation);
            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidOperationException("Не текстовый ответ");
            }
            if (stream.Length + result.Count > M3WireCodec.MAX_OUTGOING_BYTES) { throw new InvalidOperationException("Harness frame exceeds server bound."); }
            stream.Write(bytes, 0, result.Count);
        }
        while (!result.EndOfMessage);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}
