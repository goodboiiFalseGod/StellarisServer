using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Stellaris.Core.Content;
using Stellaris.Core.Scripting;
using Stellaris.Server;

// Только настоящая source-сессия и публичный протокол: без подмены состояния и WebGL.
internal static class SourcePlanetMenuChecks
{
    private static void CheckNativeSiloMiningCompatibility(string root, Action<bool, string> check)
    {
        const string zonePath = "common/zones/00_zones.txt";
        const string zoneInlinePath = "common/inline_scripts/zones/shared_minerals_zone.txt";
        const string buildingPath = "common/buildings/03_resource_buildings.txt";
        var source = new ContentSession(root);
        var zoneSource = source.LoadFile(zonePath);
        var zoneInlineSource = source.LoadFile(zoneInlinePath);
        var buildingSource = source.LoadFile(buildingPath);
        var zone = zoneSource.Root.Named("zone_minerals").Single();
        var silo = buildingSource.Root.Named("building_resource_silo").Single();
        var inlinePath = zone.AsBlock().Named("inline_script").Select(entry => entry.AsBlock().Required("script").AsScalar()).Single();
        static string[] Values(ScriptEntry entry) => entry.AsBlock().Entries
            .Where(entry => entry.Operator.Length == 0 && entry.Block is null)
            .Select(entry => entry.Key).ToArray();
        var included = Values(zoneInlineSource.Root.Required("included_building_sets"));
        var siloSets = Values(silo.AsBlock().Required("building_sets"));
        var excludedEntry = zoneInlineSource.Root.Optional("excluded_building_sets");
        var excluded = excludedEntry is null ? Array.Empty<string>() : Values(excludedEntry);
        check(zone.Source == zonePath && silo.Source == buildingPath && inlinePath == "zones/shared_minerals_zone"
            && zoneInlineSource.Path == zoneInlinePath && included.Contains("mining") && siloSets.Contains("mining")
            && !siloSets.Intersect(excluded, StringComparer.Ordinal).Any(),
            "source-only: native zone_minerals includes mining; silo mining set is not excluded");
        source.VerifyStable();
    }

    public static async Task RunAsync(string root, Action<bool, string> check, bool reconnectOnly = false, bool zoneCreateOnly = false, bool cityZonesOnly = false, bool cityCatalogOnly = false, string? specialZoneOnly = null, bool jobFlowsOnly = false, bool catalogGatesOnly = false, string? basicBuildingOnly = null, bool siloBuildingOnly = false)
    {
        if (siloBuildingOnly) CheckNativeSiloMiningCompatibility(root, check);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var url = $"http://127.0.0.1:{port}";
        using var stop = new CancellationTokenSource();
        var server = M3Server.RunAsync(root, url, "M3-test-code", stop.Token);
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) };
        try
        {
            var healthy = false;
            for (var attempt = 0; attempt < 300 && !server.IsCompleted; attempt++)
            {
                try { using var health = JsonDocument.Parse(await http.GetStringAsync("/health")); healthy = health.RootElement.GetProperty("protocol").GetInt32() == 2; break; }
                catch (HttpRequestException) { await Task.Delay(100); }
            }
            if (server.IsCompleted) await server;
            check(healthy, "source-only: настоящий HTTP protocol2 на отдельном порту");
            var ws = new Uri(url.Replace("http:", "ws:") + "/ws");
            await using var first = await Peer.JoinAsync(ws, "Source menu owner");
            await using var second = await Peer.JoinAsync(ws, "Source menu peer");
            var serial = 0;
            async Task<JsonElement> Accepted(Peer peer, string type, Dictionary<string, object?>? fields = null)
            {
                var command = fields ?? new();
                command["type"] = type;
                command["commandId"] = "source-menu-" + ++serial;
                var result = await peer.CommandAsync(command);
                if (!result.GetProperty("accepted").GetBoolean()) throw new InvalidOperationException(type + ": " + result.GetRawText());
                await Task.Delay(60); // Соблюдаем обычный лимит входящих команд сервера.
                return result;
            }
            await Accepted(first, "selectEmpireTemplate", new() { ["templateId"] = "economic" });
            await Accepted(second, "selectEmpireTemplate", new() { ["templateId"] = "scientific" });
            var lobby = await first.StateAsync();
            var lobbyRevision = lobby.GetProperty("galaxy").GetProperty("lobbyRevision").GetInt64();
            foreach (var peer in new[] { first, second }) await Accepted(peer, "ready", new() { ["ready"] = true, ["expectedLobbyRevision"] = lobbyRevision });
            var prepare = await Accepted(first, "start", new() { ["expectedLobbyRevision"] = lobbyRevision });
            var prepared = await first.StateAsync(prepare.GetProperty("stateSequence").GetInt64());
            var fingerprint = prepared.GetProperty("galaxy").GetProperty("worldFingerprint").GetString();
            using var manifestDocument = JsonDocument.Parse(await http.GetStringAsync("/m3/asset-manifest"));
            var manifest = manifestDocument.RootElement;
            var gameRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var verified = 0;
            foreach (var file in manifest.GetProperty("files").EnumerateArray())
            {
                var path = Path.GetFullPath(Path.Combine(gameRoot, file.GetProperty("relativePath").GetString()!));
                if (!path.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Manifest path покидает установку.");
                await using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
                if (stream.Length != file.GetProperty("byteLength").GetInt64() || hash != file.GetProperty("sha256").GetString()) throw new InvalidDataException("Локальный файл не совпал с manifest.");
                verified++;
            }
            check(verified > 0, "source-only: текущие локальные assets проверены потоковым SHA256, без загрузки на сервер");
            Dictionary<string, object?> Assets() => new() { ["profileId"] = manifest.GetProperty("profileId").GetString(), ["profileDigest"] = manifest.GetProperty("profileDigest").GetString(), ["galaxyWorldFingerprint"] = fingerprint, ["ready"] = true };
            foreach (var peer in new[] { first, second }) await Accepted(peer, "assetReady", Assets());
            var startFields = Assets(); startFields.Remove("ready"); startFields["expectedLobbyRevision"] = lobbyRevision;
            await Accepted(first, "start", startFields);
            await Accepted(first, "pause", new() { ["paused"] = true });
            JsonElement Colony(JsonElement state) => state.GetProperty("development").GetProperty("colonies").EnumerateArray().Single();
            if (catalogGatesOnly)
            {
                var colony = Colony(await first.StateAsync());
                var catalog = colony.GetProperty("catalog").EnumerateArray().ToArray();
                foreach (var item in catalog)
                    Console.WriteLine($"Catalog: {item.GetProperty("definitionId").GetString()} quote={item.GetProperty("quote").ValueKind} gates={item.GetProperty("gates").GetRawText()}");
                foreach (var installed in colony.GetProperty("installed").EnumerateArray())
                    foreach (var replacement in installed.GetProperty("zoneReplacements").EnumerateArray())
                        Console.WriteLine($"Zone replacement: {installed.GetProperty("definitionId").GetString()} -> {replacement.GetProperty("definitionId").GetString()} quote={replacement.GetProperty("quote").ValueKind} gates={replacement.GetProperty("gates").GetRawText()}");
                check(catalog.Length > 0, "source-only: actual building catalog is nonempty");
                return;
            }
            if (specialZoneOnly == "zone_urban")
            {
                var initial = Colony(await first.StateAsync());
                var urban = initial.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "zone_industrial")
                    .GetProperty("zoneReplacements").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "zone_urban");
                var stronghold = initial.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "building_stronghold");
                check(urban.GetProperty("quote").ValueKind == JsonValueKind.Object && stronghold.GetProperty("quote").ValueKind == JsonValueKind.Object,
                    "source-only: urban/stronghold условия разрешены до накопления; " + urban.GetProperty("gates").GetRawText() + stronghold.GetProperty("gates").GetRawText());
            }
            if (cityCatalogOnly)
            {
                var colony = Colony(await first.StateAsync());
                var industrial = colony.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "zone_industrial");
                var choices = industrial.GetProperty("zoneReplacements").EnumerateArray().ToArray();
                var expected = new[] { "zone_factory", "zone_foundry", "zone_research_unity", "zone_research", "zone_research_physics", "zone_research_society", "zone_research_engineering", "zone_unity", "zone_unity_spiritualist", "zone_fortress", "zone_trade", "zone_urban" };
                check(choices.Length == expected.Length && choices.Select(choice => choice.GetProperty("definitionId").GetString()).ToHashSet().SetEquals(expected),
                    "source-only: все 13 ordinary городских форм представлены без дубликатов, включая zone_urban");
                foreach (var choice in choices)
                {
                    var id = choice.GetProperty("definitionId").GetString();
                    check(choice.GetProperty("sourceDefinitionId").GetString() == "zone_industrial" && choice.GetProperty("slotId").GetString() == industrial.GetProperty("slotId").GetString(),
                        "source-only: привязка городского preview " + id);
                    Console.WriteLine("City catalog: " + id + " quote=" + choice.GetProperty("quote").ValueKind + " gates=" + choice.GetProperty("gates").GetRawText());
                    if (id is "zone_research" or "zone_research_physics" or "zone_research_society" or "zone_research_engineering" or "zone_unity")
                        check(choice.GetProperty("quote").ValueKind == JsonValueKind.Object && choice.GetProperty("quote").GetProperty("costs").GetProperty("minerals").GetDecimal() == 850,
                            "source-only: исходные условия и quote исследовательской цели " + id);
                    if (id == "zone_unity_spiritualist")
                        check(choice.GetProperty("quote").ValueKind == JsonValueKind.Null && choice.GetProperty("gates").EnumerateArray().Any(gate => gate.GetProperty("dependencyId").GetString() == "not-eligible"),
                            "source-only: духовная зона запрещена известными условиями материалистической страны");
                }
                return;
            }
            decimal Minerals(JsonElement colony) => colony.GetProperty("economy").GetProperty("resources").EnumerateArray().Single(resource => resource.GetProperty("resourceId").GetString() == "minerals").GetProperty("stock").GetDecimal();
            async Task<Peer> Reconnect()
            {
                var token = first.Token;
                first.Abort();
                // Сначала сервер должен наблюдать закрытие старого соединения.
                using (var disconnected = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    while (second.LatestState.GetProperty("participants").EnumerateArray()
                        .Single(participant => participant.GetProperty("id").GetInt32() == first.Id).GetProperty("connected").GetBoolean())
                        await Task.Delay(100, disconnected.Token);
                }
                var peer = await Peer.JoinAsync(ws, "Source menu owner", token: token);
                try { await Accepted(peer, "assetReady", Assets()); return peer; }
                catch { await peer.DisposeAsync(); throw; }
            }
            if (reconnectOnly)
            {
                var stock = Minerals(Colony(await first.StateAsync()));
                await using var peer = await Reconnect();
                check(peer.Id == first.Id && Minerals(Colony(await peer.StateAsync())) == stock, "source-only: reconnect сохранил actor и банк");
                return;
            }
            async Task SetLimit(Peer peer, string job, decimal count)
            {
                var colony = Colony(await peer.StateAsync());
                await Accepted(peer, "setWorkforceLimit", new() { ["colonyId"] = colony.GetProperty("colonyId").GetString(), ["expectedColonyRevision"] = colony.GetProperty("revision").GetInt64(), ["job"] = job, ["count"] = count });
            }
            // Обычная экономическая настройка: достаточно потребительских товаров на накопление.
            await SetLimit(first, "foundry", 0);
            await SetLimit(first, "artisan", 200);
            foreach (var job in new[] { "physicist", "biologist", "engineer" }) await SetLimit(first, job, 0);
            async Task Days(int count)
            {
                for (var day = 0; day < count; day++)
                {
                    await Accepted(first, "stepDay");
                    if (day % 15 == 0) await second.PingAsync();
                }
            }
            async Task<JsonElement> Funds(decimal minimum)
            {
                for (var month = 0; month < 180; month++)
                {
                    var colony = Colony(await first.StateAsync());
                    if (Minerals(colony) >= minimum) return colony;
                    await Days(30);
                    if (month % 12 == 0) Console.WriteLine($"Source menu: накопление, месяц {month + 1}, минералы {Minerals(Colony(await first.StateAsync()))}.");
                }
                throw new InvalidOperationException("Штатная экономика не накопила цену за отведённые месяцы.");
            }
            void CheckJobFlows(JsonElement colony)
            {
                var reportEconomy = colony.GetProperty("economy");
                var reportJobs = reportEconomy.GetProperty("jobs").EnumerateArray().ToArray();
                check(reportEconomy.GetProperty("reportMonth").GetInt32() > 0, "source-only: закрытый месяц доступен");
                foreach (var resource in reportEconomy.GetProperty("resources").EnumerateArray())
                {
                    var resourceId = resource.GetProperty("resourceId").GetString()!;
                    foreach (var (jobField, resourceField) in new[] { ("produced", "produced"), ("upkeep", "jobUpkeep") })
                    {
                        var sum = reportJobs.Sum(job => job.GetProperty(jobField).GetProperty(resourceId).GetDecimal());
                        check(sum == resource.GetProperty(resourceField).GetDecimal(), "source-only: job flow sum " + jobField + "/" + resourceId);
                    }
                }
                check(reportJobs.Any(job => job.GetProperty("produced").EnumerateObject().Any(flow => flow.Value.GetDecimal() > 0))
                    && reportJobs.Any(job => job.GetProperty("upkeep").EnumerateObject().Any(flow => flow.Value.GetDecimal() > 0)), "source-only: ненулевые выпуск и содержание профессий");
            }
            if (jobFlowsOnly)
            {
                var initialJobs = Colony(await first.StateAsync()).GetProperty("economy").GetProperty("jobs");
                check(initialJobs.EnumerateArray().All(job => job.GetProperty("produced").ValueKind == JsonValueKind.Null && job.GetProperty("upkeep").ValueKind == JsonValueKind.Null), "source-only: до закрытия месяца потоки неизвестны");
                await Days(30); CheckJobFlows(Colony(await first.StateAsync())); return;
            }
            Dictionary<string, object?> Target(JsonElement colony) => new() { ["colonyId"] = colony.GetProperty("colonyId").GetString(), ["expectedColonyRevision"] = colony.GetProperty("revision").GetInt64() };
            async Task RunSiloLifecycle(JsonElement startup, string miningZoneInstance)
            {
                JsonElement Resource(JsonElement colony, string id) => colony.GetProperty("economy").GetProperty("resources").EnumerateArray()
                    .Single(resource => resource.GetProperty("resourceId").GetString() == id);
                decimal Amount(JsonElement resource, string field) => resource.GetProperty(field).GetDecimal();
                var sourceMinerals = Resource(startup, "minerals");
                var sourceInfluence = Resource(startup, "influence");
                var mineralsMaximum = Amount(sourceMinerals, "sourceMaximum");
                var mineralsEffective = Amount(sourceMinerals, "effectiveMaximum");
                check(Amount(sourceInfluence, "sourceMaximum") == 1000 && Amount(sourceInfluence, "effectiveMaximum") == 1000,
                    "source-only: native fixed influence cap остаётся 1000 до строительства");
                var peerStart = Resource(Colony(await second.StateAsync()), "minerals");
                var peerMaximum = Amount(peerStart, "sourceMaximum");
                var peerEffective = Amount(peerStart, "effectiveMaximum");
                var siloEntry = startup.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "building_resource_silo");
                var siloQuote = siloEntry.GetProperty("quote");
                check(siloQuote.ValueKind == JsonValueKind.Object, "source-only: native silo quote доступен в mining zone; gates=" + siloEntry.GetProperty("gates").GetRawText());
                var price = siloQuote.GetProperty("costs").GetProperty("minerals").GetDecimal();
                var duration = siloQuote.GetProperty("durationDays").GetDecimal();
                check(price > 0 && duration > 0, "source-only: silo имеет положительную native цену и срок");
                var funded = await Funds(price * 2);
                var fundedEntry = funded.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "building_resource_silo");
                var used = funded.GetProperty("installed").EnumerateArray().Select(item => item.GetProperty("slotId").GetString()).ToHashSet();
                var miningSlots = funded.GetProperty("slots").EnumerateArray().Where(slot => slot.GetProperty("kind").GetString() == "building"
                    && slot.GetProperty("zoneInstanceId").GetString() == miningZoneInstance && !used.Contains(slot.GetProperty("slotId").GetString())).ToArray();
                var siloSlots = fundedEntry.GetProperty("slotIds").EnumerateArray().Select(slot => slot.GetString())
                    .Where(candidate => candidate is not null && miningSlots.Any(slot => slot.GetProperty("slotId").GetString() == candidate)).Take(2).ToArray();
                check(siloSlots.Length == 2, "source-only: действительные свободные mining slots допускают два native silo");
                for (var index = 0; index < siloSlots.Length; index++)
                {
                    var orderState = Colony(await first.StateAsync());
                    var fields = Target(orderState); fields["definitionId"] = "building_resource_silo"; fields["slotId"] = siloSlots[index]; fields["orderId"] = "source-silo-" + index;
                    var stock = Minerals(orderState);
                    await Accepted(first, "enqueueConstruction", fields);
                    var paid = Colony(await first.StateAsync());
                    check(Minerals(paid) == stock - price, "source-only: silo оплачено из actual stock ровно один раз " + (index + 1));
                    var next = paid.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "building_resource_silo");
                    check(next.GetProperty("quote").ValueKind == JsonValueKind.Object
                        && (index == 1 || next.GetProperty("slotIds").EnumerateArray().Any(slot => slot.GetString() == siloSlots[1])),
                        "source-only: следующий silo остаётся доступен через actual catalog " + (index + 1));
                }
                await Days(checked((int)decimal.Ceiling(duration) * 2 + 2));
                var completed = Colony(await first.StateAsync());
                var installed = completed.GetProperty("installed").EnumerateArray().Where(item => item.GetProperty("definitionId").GetString() == "building_resource_silo").ToArray();
                check(installed.Length == 2 && installed.All(item => item.GetProperty("enabled").GetBoolean()), "source-only: оба оплаченных silo завершились включёнными на actual mining colony");
                await Days(30);
                completed = Colony(await first.StateAsync());
                var minerals = Resource(completed, "minerals");
                var influence = Resource(completed, "influence");
                check(Amount(minerals, "sourceMaximum") == mineralsMaximum && Amount(minerals, "effectiveMaximum") == mineralsEffective + 10_000
                    && Amount(influence, "sourceMaximum") == 1000 && Amount(influence, "effectiveMaximum") == 1000,
                    "source-only: два включённых silo дают владельцу +10000 generic cap; source и fixed influence неизменны");
                var peerBuilt = Resource(Colony(await second.StateAsync()), "minerals");
                check(Amount(peerBuilt, "sourceMaximum") == peerMaximum && Amount(peerBuilt, "effectiveMaximum") == peerEffective, "source-only: silo не меняет cap второй страны");
                var energyBefore = Amount(Resource(completed, "energy"), "buildingUpkeep");
                var siloSlot = installed[0].GetProperty("slotId").GetString();
                var off = Target(completed); off["definitionId"] = "building_resource_silo"; off["slotId"] = siloSlot; off["enabled"] = false;
                await Accepted(first, "setBuildingEnabled", off);
                var disabled = Colony(await first.StateAsync());
                var disabledMinerals = Resource(disabled, "minerals");
                check(Amount(disabledMinerals, "sourceMaximum") == mineralsMaximum && Amount(disabledMinerals, "effectiveMaximum") == mineralsEffective + 5_000,
                    "source-only: отключение одного silo снимает только его +5000 bonus");
                await Days(30);
                var upkeepOff = Amount(Resource(Colony(await first.StateAsync()), "energy"), "buildingUpkeep");
                check(energyBefore - upkeepOff == 1, "source-only: отключение silo снимает ровно его energy upkeep 1");
                var current = Colony(await first.StateAsync());
                var on = Target(current); on["definitionId"] = "building_resource_silo"; on["slotId"] = siloSlot; on["enabled"] = true;
                await Accepted(first, "setBuildingEnabled", on);
                var enabled = Colony(await first.StateAsync());
                var enabledMinerals = Resource(enabled, "minerals");
                check(Amount(enabledMinerals, "sourceMaximum") == mineralsMaximum && Amount(enabledMinerals, "effectiveMaximum") == mineralsEffective + 10_000,
                    "source-only: повторное включение silo восстанавливает его +5000 bonus");
                await Days(30);
                var upkeepOn = Amount(Resource(Colony(await first.StateAsync()), "energy"), "buildingUpkeep");
                check(upkeepOn == energyBefore, "source-only: повторное включение silo восстанавливает energy upkeep");
            }
            if (basicBuildingOnly is not null)
            {
                var buildingId = basicBuildingOnly;
                var jobId = buildingId == "building_holo_theatres" ? "entertainer" : "bureaucrat";
                var buildingName = buildingId == "building_holo_theatres" ? "holo theatres" : "administrative building";
                var orderId = "source-" + buildingId;
                await SetLimit(first, "artisan", 0);
                var initial = Colony(await first.StateAsync());
                var entry = initial.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == buildingId);
                Console.WriteLine($"Building source quote diagnostic ({buildingId}): " + entry.GetProperty("quote").GetRawText());
                var buildingQuote = entry.GetProperty("quote");
                check(buildingQuote.ValueKind == JsonValueKind.Object,
                    $"source-only: {buildingName} quote доступен; gates={entry.GetProperty("gates").GetRawText()}");
                var buildingCost = buildingQuote.GetProperty("costs").GetProperty("minerals").GetDecimal();
                var duration = buildingQuote.GetProperty("durationDays").GetDecimal();
                check(buildingCost == 340 && Math.Abs(duration - 360m / 1.15m) < .0000001m
                    && buildingQuote.GetProperty("infrastructureUpkeep").GetProperty("energy").GetDecimal() == 2
                    && buildingQuote.GetProperty("jobCapacity").GetProperty(jobId).GetDecimal() == 200,
                    $"source-only: {buildingName} quote — 340 minerals, 360/1,15 days, energy upkeep 2, capacity 200 {jobId}");
                Console.WriteLine($"Building source: quote confirmed; funding {buildingCost} minerals.");
                var ready = await Funds(buildingCost);
                entry = ready.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == buildingId);
                var freeSlots = ready.GetProperty("slots").EnumerateArray()
                    .Where(slot => slot.GetProperty("kind").GetString() == "building")
                    .Select(slot => slot.GetProperty("slotId").GetString()).ToHashSet();
                var occupied = ready.GetProperty("installed").EnumerateArray().Select(item => item.GetProperty("slotId").GetString()).ToHashSet();
                var slotId = entry.GetProperty("slotIds").EnumerateArray().Select(slot => slot.GetString())
                    .First(candidate => candidate is not null && freeSlots.Contains(candidate) && !occupied.Contains(candidate));
                var stock = Minerals(ready);
                var fields = Target(ready); fields["definitionId"] = buildingId; fields["slotId"] = slotId; fields["orderId"] = orderId;
                await Accepted(first, "enqueueConstruction", fields);
                var funded = Colony(await first.StateAsync());
                var fundedOrder = funded.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == orderId);
                var reservedSlot = funded.GetProperty("slots").EnumerateArray().Single(slot => slot.GetProperty("slotId").GetString() == slotId);
                check(Minerals(funded) == stock - buildingCost
                    && fundedOrder.GetProperty("slotId").GetString() == slotId
                    && fundedOrder.GetProperty("paidCosts").GetProperty("minerals").GetDecimal() == buildingCost
                    && reservedSlot.GetProperty("reservedByOrderId").GetString() == orderId
                    && !funded.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("slotId").GetString() == slotId),
                    $"source-only: {buildingName} оплачено один раз; заказ зарезервировал выбранный slot без ранней установки");
                await SetLimit(first, "artisan", 200);
                var afterPayment = Colony(await first.StateAsync());
                check(afterPayment.GetProperty("economy").GetProperty("jobs").EnumerateArray()
                    .Single(job => job.GetProperty("jobId").GetString() == "artisan").GetProperty("limit").GetDecimal() == 200,
                    "source-only: лимит artisan восстановлен до 200 после оплаты строительства");
                Console.WriteLine($"Building source: order funded and enqueued in {slotId}; advancing to completion.");
                var completionDays = (int)decimal.Ceiling(duration);
                await Days(completionDays - 1);
                var oneShort = Colony(await first.StateAsync());
                check(!oneShort.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("slotId").GetString() == slotId)
                    && oneShort.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == orderId).GetProperty("status").GetString() != "completed",
                    $"source-only: {buildingName} не установлен до полного срока строительства");
                await Days(1);
                var buildingCompleted = Colony(await first.StateAsync());
                var installed = buildingCompleted.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("slotId").GetString() == slotId);
                var capacityBefore = ready.GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == jobId).GetProperty("capacity").GetDecimal();
                var capacityAfter = buildingCompleted.GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == jobId).GetProperty("capacity").GetDecimal();
                check(installed.GetProperty("definitionId").GetString() == buildingId && capacityAfter - capacityBefore == 200
                    && buildingCompleted.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == orderId).GetProperty("status").GetString() == "completed",
                    $"source-only: завершение установило {buildingName} и добавило 200 {jobId} capacity");
                Console.WriteLine($"Building source: installed after {completionDays} days with +200 {jobId} capacity.");
                await Days(30);
                var monthly = Colony(await first.StateAsync()).GetProperty("economy").GetProperty("resources").EnumerateArray()
                    .Single(resource => resource.GetProperty("resourceId").GetString() == "energy").GetProperty("buildingUpkeep").GetDecimal();
                var baseline = ready.GetProperty("economy").GetProperty("resources").EnumerateArray()
                    .Single(resource => resource.GetProperty("resourceId").GetString() == "energy").GetProperty("buildingUpkeep").GetDecimal();
                check(monthly - baseline == 2, "source-only: закрытый месячный отчёт добавил energy building upkeep 2");
                return;
            }
            // Два настоящих района ускоряют накопление без изменения банков и технологий.
            for (var level = 0; level < 2; level++)
            {
                var colony = Colony(await first.StateAsync());
                var district = colony.GetProperty("catalog").EnumerateArray().Single(entry => entry.GetProperty("definitionId").GetString() == "district_mining");
                var price = district.GetProperty("quote").GetProperty("costs").GetProperty("minerals").GetDecimal();
                colony = await Funds(price);
                district = colony.GetProperty("catalog").EnumerateArray().Single(entry => entry.GetProperty("definitionId").GetString() == "district_mining");
                var fields = Target(colony); fields["definitionId"] = "district_mining"; fields["slotId"] = district.GetProperty("slotIds")[0].GetString(); fields["orderId"] = "source-menu-mine-" + level;
                await Accepted(first, "enqueueConstruction", fields);
                await Days(240);
            }
            if (zoneCreateOnly)
            {
                JsonElement Entry(JsonElement colony) => colony.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "zone_minerals");
                var initial = Colony(await first.StateAsync());
                var zoneQuote = Entry(initial).GetProperty("quote");
                var zoneCost = zoneQuote.GetProperty("costs").GetProperty("minerals").GetDecimal();
                check(zoneCost == 850 && Math.Abs(zoneQuote.GetProperty("durationDays").GetDecimal() - 360m / 1.15m) < .0000001m,
                    "source-only: civic открыл создание зоны с building cost/speed");
                initial = await Funds(zoneCost);
                var entry = Entry(initial);
                var extraJobs = entry.GetProperty("quote").GetProperty("jobCapacity").GetProperty("miner").GetDecimal();
                var baseJobs = initial.GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == "miner").GetProperty("capacity").GetDecimal();
                var slotCount = initial.GetProperty("slots").GetArrayLength();
                var zoneStock = Minerals(initial);
                var slotId = entry.GetProperty("slotIds")[0].GetString();
                var fields = Target(initial); fields["definitionId"] = "zone_minerals"; fields["slotId"] = slotId; fields["orderId"] = "source-menu-create-zone";
                await Accepted(first, "enqueueConstruction", fields);
                check(Minerals(Colony(await first.StateAsync())) == zoneStock - zoneCost, "source-only: создание зоны оплачено один раз");
                await Days(1);
                var activeZone = Colony(await first.StateAsync());
                check(activeZone.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == "source-menu-create-zone").GetProperty("completedWork").GetDecimal() == 1.15m
                    && activeZone.GetProperty("slots").GetArrayLength() == slotCount
                    && !activeZone.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() == "zone_minerals"),
                    "source-only: день дал work1,15 без ранней установки зоны и слотов");
                await Days(313);
                var finished = Colony(await first.StateAsync());
                var installedZone = finished.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "zone_minerals");
                check(installedZone.GetProperty("slotId").GetString() == slotId, "source-only: зона установлена в оплаченный slot");
                var instance = finished.GetProperty("startupZones").EnumerateArray().Single(zone => zone.GetProperty("zoneId").GetString() == "zone_minerals").GetProperty("instanceId").GetString()
                    ?? throw new InvalidDataException("У завершённой zone_minerals отсутствует instanceId.");
                check(finished.GetProperty("slots").GetArrayLength() == slotCount + 3
                    && finished.GetProperty("slots").EnumerateArray().Count(slot => slot.GetProperty("kind").GetString() == "building" && slot.GetProperty("zoneInstanceId").GetString() == instance) == 3,
                    "source-only: completion добавил ровно три связанных building slots");
                check(extraJobs > 0 && installedZone.GetProperty("jobCapacity").GetProperty("miner").GetDecimal() == extraJobs
                    && finished.GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == "miner").GetProperty("capacity").GetDecimal() == baseJobs + extraJobs,
                    "source-only: miner capacity выросла ровно на source quote contribution");
                check(finished.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == "source-menu-create-zone").GetProperty("status").GetString() == "completed",
                    "source-only: заказ создания зоны завершён");
                if (siloBuildingOnly) await RunSiloLifecycle(finished, instance);
                return;
            }
            JsonElement Preview(JsonElement colony, string source = "zone_industrial") => colony.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == source)
                .GetProperty("zoneReplacements").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == (source == "zone_foundry" ? "zone_industrial" : "zone_foundry"));
            var before = Colony(await first.StateAsync());
            var preview = Preview(before);
            var quote = preview.GetProperty("quote");
            var cost = quote.GetProperty("costs").GetProperty("minerals").GetDecimal();
            check(cost == 850 && Math.Abs(quote.GetProperty("durationDays").GetDecimal() - 360m / 1.15m) < .0000001m, "source-only: зона получила стоимость −15% и скорость +15% зданий");
            before = await Funds(cost * (specialZoneOnly is null ? 2 : 1));
            Dictionary<string, object?> Replace(JsonElement colony, string order, string source = "zone_industrial")
            {
                var current = Preview(colony, source); var fields = Target(colony);
                foreach (var key in new[] { "definitionId", "sourceDefinitionId", "slotId", "zoneInstanceId" }) fields[key] = current.GetProperty(key).GetString();
                fields["zoneReplacementPreviewId"] = current.GetProperty("previewId").GetString(); fields["confirmZoneRemoval"] = true; fields["orderId"] = order;
                return fields;
            }
            if (cityZonesOnly || specialZoneOnly is not null)
            {
                var citySlot = before.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "zone_industrial").GetProperty("slotId").GetString();
                JsonElement CityZone(JsonElement colony) => colony.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("slotId").GetString() == citySlot);
                var slotIds = before.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("slotId").GetString()).ToHashSet();
                var instance = before.GetProperty("startupZones").EnumerateArray().Single(zone => zone.GetProperty("slotInstanceId").GetString() == citySlot).GetProperty("instanceId").GetString();
                foreach (var target in specialZoneOnly is null ? new[] { "zone_factory", "zone_research_unity" } : new[] { specialZoneOnly })
                {
                    var colony = Colony(await first.StateAsync());
                    var sourceZone = CityZone(colony);
                    var choices = sourceZone.GetProperty("zoneReplacements").EnumerateArray().ToArray();
                    check(choices.Length >= 3 && choices.Select(choice => choice.GetProperty("definitionId").GetString()).Distinct().Count() == choices.Length
                        && new[] { "zone_industrial", "zone_foundry", "zone_factory", "zone_research_unity" }.Where(id => id != sourceZone.GetProperty("definitionId").GetString()).All(id => choices.Any(choice => choice.GetProperty("definitionId").GetString() == id)),
                        "source-only: разные городские цели включают исходную четвёрку для " + sourceZone.GetProperty("definitionId").GetString());
                    var choice = choices.Single(candidate => candidate.GetProperty("definitionId").GetString() == target);
                    check(choice.GetProperty("canEnqueue").GetBoolean() && choice.GetProperty("quote").GetProperty("costs").GetProperty("minerals").GetDecimal() == cost,
                        "source-only: актуальное разрешение и цена " + target);
                    var expectedJobs = colony.GetProperty("economy").GetProperty("jobs").EnumerateArray().ToDictionary(job => job.GetProperty("jobId").GetString()!, job => job.GetProperty("capacity").GetDecimal());
                    void AddJobs(JsonElement values, int sign) { foreach (var job in values.EnumerateObject()) expectedJobs[job.Name] = expectedJobs.GetValueOrDefault(job.Name) + sign * job.Value.GetDecimal(); }
                    AddJobs(sourceZone.GetProperty("jobCapacity"), -1);
                    foreach (var removed in choice.GetProperty("removedBuildings").EnumerateArray())
                        AddJobs(colony.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("slotId").GetString() == removed.GetProperty("slotId").GetString()).GetProperty("jobCapacity"), -1);
                    AddJobs(choice.GetProperty("quote").GetProperty("jobCapacity"), 1);
                    var fields = Target(colony);
                    foreach (var key in new[] { "definitionId", "sourceDefinitionId", "slotId", "zoneInstanceId" }) fields[key] = choice.GetProperty(key).GetString();
                    fields["zoneReplacementPreviewId"] = choice.GetProperty("previewId").GetString(); fields["confirmZoneRemoval"] = true; fields["orderId"] = "source-city-" + target;
                    var stock = Minerals(colony);
                    await Accepted(first, "enqueueZoneReplacement", fields);
                    check(Minerals(Colony(await first.StateAsync())) == stock - cost, "source-only: однократная оплата " + target);
                    await Days(314);
                    var completedCity = Colony(await first.StateAsync());
                    check(CityZone(completedCity).GetProperty("definitionId").GetString() == target
                        && completedCity.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("slotId").GetString()).ToHashSet().SetEquals(slotIds)
                        && completedCity.GetProperty("startupZones").EnumerateArray().Single(zone => zone.GetProperty("slotInstanceId").GetString() == citySlot).GetProperty("instanceId").GetString() == instance,
                        "source-only: completion сохранил slots/instance " + target);
                    check(completedCity.GetProperty("economy").GetProperty("jobs").EnumerateArray().All(job => expectedJobs.GetValueOrDefault(job.GetProperty("jobId").GetString()!) == job.GetProperty("capacity").GetDecimal()),
                        "source-only: jobs пересчитаны с учётом удалённых зданий " + target);
                    check(completedCity.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() == "building_factory_1") == (target is "zone_factory" or "zone_urban"),
                        "source-only: совместимость установленной фабрики " + target);
                    if (target == "zone_urban")
                    {
                        var urbanInstance = completedCity.GetProperty("startupZones").EnumerateArray().Single(zone => zone.GetProperty("slotInstanceId").GetString() == citySlot).GetProperty("instanceId").GetString();
                        check(CityZone(completedCity).GetProperty("definitionId").GetString() == "zone_urban" && urbanInstance == instance,
                            "source-only: zone_urban сохранил исходную city occurrence");
                        var stronghold = completedCity.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "building_stronghold");
                        var buildQuote = stronghold.GetProperty("quote");
                        var buildPrice = buildQuote.GetProperty("costs").GetProperty("minerals").GetDecimal();
                        check(buildPrice == 340 && Math.Abs(buildQuote.GetProperty("durationDays").GetDecimal() - 240m / 1.15m) < .0000001m
                            && buildQuote.GetProperty("infrastructureUpkeep").GetProperty("energy").GetDecimal() == 1
                            && buildQuote.GetProperty("jobCapacity").GetProperty("soldier").GetDecimal() == 200,
                            "source-only: stronghold quote — 340 minerals, 240/1,15 days, energy upkeep 1, soldier 200");
                        var readyToBuild = await Funds(buildPrice);
                        var readySlots = readyToBuild.GetProperty("slots").EnumerateArray()
                            .Where(slot => slot.GetProperty("kind").GetString() == "building" && slot.GetProperty("zoneInstanceId").GetString() == urbanInstance)
                            .ToArray();
                        var readyStronghold = readyToBuild.GetProperty("catalog").EnumerateArray().Single(item => item.GetProperty("definitionId").GetString() == "building_stronghold");
                        var usedSlots = readyToBuild.GetProperty("installed").EnumerateArray().Select(item => item.GetProperty("slotId").GetString()).ToHashSet();
                        var slotId = readyStronghold.GetProperty("slotIds").EnumerateArray().Select(slot => slot.GetString())
                            .First(candidate => candidate is not null && readySlots.Any(slot => slot.GetProperty("slotId").GetString() == candidate)
                                && !usedSlots.Contains(candidate));
                        var buildStock = Minerals(readyToBuild);
                        var buildFields = Target(readyToBuild); buildFields["definitionId"] = "building_stronghold"; buildFields["slotId"] = slotId; buildFields["orderId"] = "source-urban-stronghold";
                        await Accepted(first, "enqueueConstruction", buildFields);
                        check(Minerals(Colony(await first.StateAsync())) == buildStock - 340,
                            "source-only: urban stronghold удержал ровно одну цену 340");
                        await Days(1);
                        var activeBuild = Colony(await first.StateAsync());
                        check(activeBuild.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == "source-urban-stronghold").GetProperty("completedWork").GetDecimal() == 1.15m
                            && !activeBuild.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("slotId").GetString() == slotId),
                            "source-only: stronghold получил 1,15 work за день и не установился досрочно");
                        await Days(208);
                        var completedBuild = Colony(await first.StateAsync());
                        var installedStronghold = completedBuild.GetProperty("installed").EnumerateArray().Single(item => item.GetProperty("slotId").GetString() == slotId);
                        var completedChildSlot = completedBuild.GetProperty("slots").EnumerateArray().Single(slot => slot.GetProperty("slotId").GetString() == slotId);
                        var soldierBefore = readyToBuild.GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == "soldier").GetProperty("capacity").GetDecimal();
                        var soldierAfter = completedBuild.GetProperty("economy").GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == "soldier").GetProperty("capacity").GetDecimal();
                        check(installedStronghold.GetProperty("definitionId").GetString() == "building_stronghold"
                            && completedChildSlot.GetProperty("zoneInstanceId").GetString() == urbanInstance
                            && completedBuild.GetProperty("queue").EnumerateArray().Single(order => order.GetProperty("orderId").GetString() == "source-urban-stronghold").GetProperty("status").GetString() == "completed",
                            "source-only: на день 209 stronghold установлен в том же child slot zone_urban");
                        check(soldierAfter - soldierBefore == 200 && installedStronghold.GetProperty("infrastructureUpkeep").GetProperty("energy").GetDecimal() == 1,
                            "source-only: completion добавила 200 soldier capacity и source upkeep1");
                        // Ресурсный отчёт содержит завершённый месяц; проверяем новый поток после следующего расчёта.
                        var energyBefore = readyToBuild.GetProperty("economy").GetProperty("resources").EnumerateArray().Single(resource => resource.GetProperty("resourceId").GetString() == "energy").GetProperty("buildingUpkeep").GetDecimal();
                        await Days(30);
                        var energyAfter = Colony(await first.StateAsync()).GetProperty("economy").GetProperty("resources").EnumerateArray().Single(resource => resource.GetProperty("resourceId").GetString() == "energy").GetProperty("buildingUpkeep").GetDecimal();
                        check(energyAfter - energyBefore == 1, "source-only: месячный отчёт учёл energy building upkeep+1");
                    }
                }
                var final = Colony(await first.StateAsync());
                if (specialZoneOnly is not null) CheckJobFlows(final);
                if (specialZoneOnly == "zone_fortress")
                {
                    foreach (var job in final.GetProperty("economy").GetProperty("jobs").EnumerateArray().Where(job => job.GetProperty("capacity").GetDecimal() > 0)) await SetLimit(first, job.GetProperty("jobId").GetString()!, 0);
                    await SetLimit(first, "soldier", 300);
                    await Days(30);
                    var economy = Colony(await first.StateAsync()).GetProperty("economy");
                    var soldiers = economy.GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("jobId").GetString() == "soldier");
                    check(soldiers.GetProperty("capacity").GetDecimal() == 300 && soldiers.GetProperty("effectiveWorkforce").GetDecimal() > 0
                        && economy.GetProperty("resources").EnumerateArray().Single(resource => resource.GetProperty("resourceId").GetString() == "alloys").GetProperty("jobUpkeep").GetDecimal() == soldiers.GetProperty("effectiveWorkforce").GetDecimal() / 100,
                        "source-only: крепость дала300 soldier WF, занятые солдаты расходуют сплавы по исходному рецепту");
                }
                if (specialZoneOnly is null)
                    check(final.GetProperty("startupZones").EnumerateArray().Count(zone => zone.GetProperty("zoneId").GetString() == "zone_research_unity") == 2,
                        "source-only: две research occurrence не слились по definition ID");
                return;
            }
            var initialStock = Minerals(before);
            await Accepted(first, "enqueueZoneReplacement", Replace(before, "source-menu-cancel"));
            var paid = Colony(await first.StateAsync());
            check(Minerals(paid) == initialStock - cost, "source-only: один платёж замены зоны");
            await Days(1);
            var active = Colony(await first.StateAsync());
            var order = active.GetProperty("queue").EnumerateArray().Single(item => item.GetProperty("orderId").GetString() == "source-menu-cancel");
            check(order.GetProperty("completedWork").GetDecimal() == 1.15m && active.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() == "building_factory_1"), "source-only: work1,15 и старая фабрика работает до completion");
            var cancel = Target(active); cancel["type"] = "cancelConstruction"; cancel["commandId"] = "source-menu-cancel-command"; cancel["orderId"] = "source-menu-cancel";
            var cancelResult = await first.CommandAsync(cancel);
            check(cancelResult.GetProperty("accepted").GetBoolean(), "source-only: ручная отмена принята");
            var refunded = Colony(await first.StateAsync());
            check(Minerals(refunded) == initialStock && Preview(refunded).GetProperty("canEnqueue").GetBoolean(), "source-only: точный refund и снятие reservation");
            await Task.Delay(60);
            check((await first.CommandAsync(cancel)).GetProperty("accepted").GetBoolean() && Minerals(Colony(await first.StateAsync())) == initialStock, "source-only: replay отмены не возвращает цену дважды");
            await using var returning = await Reconnect();
            var resumed = Colony(await returning.StateAsync());
            check(returning.Id == first.Id && Minerals(resumed) == initialStock, "source-only: reconnect сохранил actor и банк");
            // Далее команды идут через вернувшееся соединение; старый сокет не используется.
            await Accepted(returning, "enqueueZoneReplacement", Replace(resumed, "source-menu-complete"));
            for (var day = 0; day < 314; day++)
            {
                await Accepted(returning, "stepDay");
                if (day % 15 == 0) await second.PingAsync();
            }
            var completed = Colony(await returning.StateAsync());
            check(completed.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() == "zone_foundry")
                && !completed.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() == "building_factory_1"), "source-only: completion установил foundry и удалил несовместимую фабрику");
            check(completed.GetProperty("queue").EnumerateArray().Single(item => item.GetProperty("orderId").GetString() == "source-menu-complete").GetProperty("status").GetString() == "completed", "source-only: заказ завершён без pending reservation");
            var reverse = Preview(completed, "zone_foundry");
            check(reverse.GetProperty("canEnqueue").GetBoolean() && reverse.GetProperty("quote").GetProperty("costs").GetProperty("minerals").GetDecimal() == cost,
                "source-only: runtime CG expenses открыли обратный industrial preview");
            var reverseStock = Minerals(completed);
            await Accepted(returning, "enqueueZoneReplacement", Replace(completed, "source-menu-reverse", "zone_foundry"));
            check(Minerals(Colony(await returning.StateAsync())) == reverseStock - cost, "source-only: обратный переход оплачен один раз");
            for (var day = 0; day < 314; day++)
            {
                await Accepted(returning, "stepDay");
                if (day % 15 == 0) await second.PingAsync();
            }
            var restored = Colony(await returning.StateAsync());
            check(restored.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() == "zone_industrial")
                && !restored.GetProperty("installed").EnumerateArray().Any(item => item.GetProperty("definitionId").GetString() is "zone_foundry" or "building_factory_1"),
                "source-only: reverse completion вернул industrial, не воскресил удалённую фабрику");
            check(restored.GetProperty("queue").EnumerateArray().Single(item => item.GetProperty("orderId").GetString() == "source-menu-reverse").GetProperty("status").GetString() == "completed",
                "source-only: обратный заказ завершён");
        }
        finally
        {
            stop.Cancel();
            check(await server.WaitAsync(TimeSpan.FromSeconds(10)) == 0, "source-only: свой сервер корректно остановлен");
        }
    }
}
