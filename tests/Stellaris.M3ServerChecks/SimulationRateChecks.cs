using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Stellaris.Core.Assets;
using Stellaris.Core.Content;
using Stellaris.Core.Economy;
using Stellaris.Server;
using Stellaris.Session.M3;

internal static class SimulationRateChecks
{
    internal static async Task<int> RunAsync()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) { throw new InvalidOperationException(name); }
            checks++;
            Console.WriteLine("PASS simulation rate: " + name);
        }
        foreach (var speed in new[] { 1, 2, 4 })
        {
            var rate = new M3SimulationRate();
            Check(rate.Observe(TimeSpan.Zero, "lobby", true).Status == "not-running", "lobby unavailable");
            rate.Observe(TimeSpan.Zero, "running", false, reset: true);
            for (var day = 1; day <= speed * 10; day++) { rate.Observe(TimeSpan.FromSeconds((double)day / speed), "running", false, completed: true); }
            var measured = rate.Observe(TimeSpan.FromSeconds(10), "running", false);
            Check(measured.Status == "measured" && measured.CompletedAutomaticDays == speed * 10 && measured.WindowSeconds == 10 && measured.AutomaticDaysPerRealSecond == speed, "aligned " + speed + " days per real second");
            var boundary = rate.Observe(TimeSpan.FromSeconds(10 + 1d / speed), "running", false);
            Check(boundary.CompletedAutomaticDays == speed * 10 - 1, "left boundary excluded");
            var zero = rate.Observe(TimeSpan.FromSeconds(20), "running", false);
            Check(zero.Status == "measured" && zero.AutomaticDaysPerRealSecond == 0 && zero.CompletedAutomaticDays == 0, "running stall decays to measured zero");
            Check(rate.Observe(TimeSpan.FromSeconds(21), "running", true).Status == "paused", "paused is not zero");
            Check(rate.Observe(TimeSpan.FromSeconds(22), "running", false).Status == "warming-up", "resume warms up");
            var invalid = rate.Observe(TimeSpan.FromSeconds(21), "running", false);
            Check(invalid.Status == "unavailable" && invalid.SampledAtMilliseconds is null, "backward clock has no fabricated timestamp");
            Check(rate.Observe(TimeSpan.FromSeconds(23), "running", false).Status == "warming-up", "valid recovery starts a new epoch");
            Check(rate.Observe(TimeSpan.MaxValue, "running", false).AutomaticDaysPerRealSecond == 0, "maximum timestamp has finite zero rate");
        }
        var bounded = new M3SimulationRate();
        bounded.Observe(TimeSpan.Zero, "running", false);
        M3SimulationRateView? overflow = null;
        for (var index = 1; index <= 513; index++) { overflow = bounded.Observe(TimeSpan.FromTicks(index), "running", false, completed: true); }
        Check(overflow!.Status == "unavailable" && overflow.AutomaticDaysPerRealSecond is null, "ring overflow is unavailable, not truncation");
        Check(bounded.Observe(TimeSpan.FromSeconds(1), "running", false).Status == "warming-up", "overflow recovery starts warm-up");
        var initial = new M3SimulationRate();
        Check(initial.Observe(TimeSpan.MinValue, "running", false).SampledAtMilliseconds is null && initial.Observe(TimeSpan.Zero, "running", false).Status == "warming-up", "invalid initial sample recovers without correction");

        var manifest = await ManifestAsync();
        {
            var cachedSession = Session(manifest);
            var first = cachedSession.Join("Cache host", "player", null, TimeSpan.Zero);
            var peer = cachedSession.Join("Cache peer", "player", null, TimeSpan.Zero);
            var observer = cachedSession.Join("Cache observer", "observer", null, TimeSpan.Zero);
            var cachedRuntime = new M3Server.Runtime(cachedSession, "checks-code", CancellationToken.None, NullLogger.Instance, () => TimeSpan.Zero);
            void Apply(M3JoinGrant who, M3ClientCommand command) => Check(cachedRuntime.ExecuteOnce(who.ParticipantId, who.ConnectionId, command, TimeSpan.Zero).Accepted, "cache mutation accepted: " + command.Type);
            Apply(first, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
            Apply(observer, new("assets", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest));
            var general = cachedRuntime.Snapshot(observer.ParticipantId);
            Check(ReferenceEquals(general.Planets, cachedRuntime.Snapshot(observer.ParticipantId).Planets), "unchanged snapshot reuses immutable projection");
            Apply(observer, new("pov", "observerView", TargetParticipantId: peer.ParticipantId));
            var scoped = cachedRuntime.Snapshot(observer.ParticipantId);
            Check(scoped.Sequence > general.Sequence && scoped.Planets.Single().OwnerId == peer.ParticipantId && scoped.ViewContext.ParticipantId == peer.ParticipantId, "POV change invalidates cache and preserves privacy");
            Apply(first, new("transfer", "transferHost", TargetParticipantId: peer.ParticipantId));
            Check(cachedRuntime.Snapshot(first.ParticipantId).Participants.Single(p => p.IsHost).Id == peer.ParticipantId, "host transfer invalidates participant cache");
            foreach (var player in new[] { first, peer })
            {
                Check(cachedRuntime.ExecuteOnce(player.ParticipantId, player.ConnectionId,
                    new("asset-" + player.ParticipantId, "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), TimeSpan.Zero).Accepted,
                    "cache test player import accepted");
                Check(cachedRuntime.ExecuteOnce(player.ParticipantId, player.ConnectionId,
                    new("ready-" + player.ParticipantId, "ready", Ready: true), TimeSpan.Zero).Accepted,
                    "cache test player ready accepted");
            }
            Check(cachedRuntime.ExecuteOnce(peer.ParticipantId, peer.ConnectionId, new("cache-start", "start"), TimeSpan.Zero).Accepted,
                "cache test lobby starts before reconnect path");
            cachedSession.Disconnect(peer.ParticipantId, peer.ConnectionId, TimeSpan.Zero);
            Check(!cachedRuntime.Snapshot(first.ParticipantId).Participants.Single(p => p.Id == peer.ParticipantId).Connected, "disconnect invalidates cache");
            cachedSession.Join("return", "player", peer.Token, TimeSpan.Zero);
            Check(cachedRuntime.Snapshot(first.ParticipantId).Participants.Single(p => p.Id == peer.ParticipantId).Connected, "reconnect invalidates cache");
        }
        var session = Session(manifest);
        var host = session.Join("Host", "player", null, TimeSpan.Zero);
        var second = session.Join("Second", "player", null, TimeSpan.Zero);
        var sample = TimeSpan.Zero;
        var throwSample = false;
        Action? atSample = null;
        var runtime = new M3Server.Runtime(session, "checks-code", CancellationToken.None, NullLogger.Instance, () =>
        {
            atSample?.Invoke();
            if (throwSample) { throw new InvalidOperationException("Synthetic diagnostic failure."); }
            return sample;
        });
        M3CommandResult Execute(M3JoinGrant player, M3ClientCommand command, double entry, double completion)
        {
            sample = TimeSpan.FromSeconds(completion);
            return runtime.ExecuteOnce(player.ParticipantId, player.ConnectionId, command, TimeSpan.FromSeconds(entry));
        }
        foreach (var player in new[] { host, second })
        {
            Check(Execute(player, new("assets", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), 0, 0).Accepted, "asset readiness unchanged");
            Check(Execute(player, new("ready", "ready", Ready: true), 0, 0).Accepted, "lobby ready unchanged");
        }
        Check(Execute(host, new("start", "start"), 0, 0).Accepted, "start unchanged");
        Check(Execute(host, new("resume", "pause", Paused: false), 0, 0).Accepted, "resume unchanged");
        sample = TimeSpan.FromSeconds(.5);
        runtime.TickOnce(TimeSpan.FromSeconds(.5));
        Check(runtime.Snapshot(host.ParticipantId).SimulationRate!.CompletedAutomaticDays == 0, "no-op automatic tick excluded");
        sample = TimeSpan.FromSeconds(3);
        atSample = () => Check(session.ObserveTiming().ElapsedDay == 1, "actual runtime samples only after authoritative Tick returns");
        runtime.TickOnce(TimeSpan.FromSeconds(1));
        atSample = null;
        var view = runtime.Snapshot(host.ParticipantId);
        Check(view.SimulationRate!.CompletedAutomaticDays == 1 && view.SimulationRate.WindowSeconds == 3 && view.SimulationRate.AutomaticDaysPerRealSecond == 1d / 3, "completion time, not entry time, is sampled");
        var unchanged = session.View(host.ParticipantId);
        sample = TimeSpan.FromSeconds(3.1);
        var refreshed = runtime.Snapshot(host.ParticipantId);
        Check(refreshed.Sequence == unchanged.Sequence && session.View(host.ParticipantId).Sequence == unchanged.Sequence && refreshed.SimulationRate!.SampledAtMilliseconds == 3100, "diagnostic refresh never mutates simulation sequence");
        Check(unchanged.SimulationRate is null, "session view remains free of runtime diagnostic state");
        var reset = new M3ClientCommand("same-speed", "speed", Speed: 1);
        var accepted = Execute(host, reset, 3.2, 3.2);
        Check(accepted.Accepted && runtime.Snapshot(host.ParticipantId).SimulationRate!.Status == "warming-up", "fresh same-value speed resets epoch");
        Check(Execute(host, reset, 3.4, 3.4) == accepted, "cached speed ACK unchanged");
        Check(!Execute(host, new("bad-speed", "speed", Speed: 3), 3.5, 3.5).Accepted, "rejected speed unchanged");
        sample = TimeSpan.FromSeconds(5.2);
        Check(runtime.Snapshot(host.ParticipantId).SimulationRate!.Status == "measured", "cached and rejected resets do not restart warm-up");
        Check(Execute(host, new("pause", "pause", Paused: true), 5.3, 5.4).Accepted, "pause unchanged");
        Check(Execute(host, new("manual", "stepDay"), 5.5, 5.6).Accepted && session.ObserveTiming().ElapsedDay == 2 && runtime.Snapshot(host.ParticipantId).SimulationRate!.Status == "paused", "manual day excluded from throughput");
        Check(Execute(host, new("resume2", "pause", Paused: false), 5.7, 5.8).Accepted, "second resume unchanged");
        sample = TimeSpan.FromSeconds(7);
        runtime.TickOnce(TimeSpan.FromSeconds(6.7));
        Check(runtime.Snapshot(host.ParticipantId).SimulationRate!.CompletedAutomaticDays == 1, "automatic day after resume counts once");
        throwSample = true;
        runtime.TickOnce(TimeSpan.FromSeconds(7.8));
        Check(session.ObserveTiming().ElapsedDay == 4 && !session.ObserveTiming().Paused, "diagnostic exception cannot undo or fail committed day");
        throwSample = false;
        sample = TimeSpan.MinValue;
        Check(runtime.Snapshot(host.ParticipantId).SimulationRate!.Status == "unavailable", "invalid diagnostic clock reported explicitly");
        sample = TimeSpan.FromSeconds(10);
        Check(runtime.Snapshot(host.ParticipantId).SimulationRate!.Status == "warming-up", "runtime recovery starts valid epoch");
        sample = TimeSpan.FromSeconds(12);
        var beforeReconnect = runtime.Snapshot(host.ParticipantId).SimulationRate!;
        session.Disconnect(second.ParticipantId, second.ConnectionId, TimeSpan.FromSeconds(12));
        session.Join("Second", "player", second.Token, TimeSpan.FromSeconds(12));
        Check(runtime.Snapshot(host.ParticipantId).SimulationRate == beforeReconnect, "reconnect preserves shared diagnostic epoch");
        var wire = JsonSerializer.SerializeToElement(new { type = "snapshot", state = runtime.Snapshot(host.ParticipantId) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var metric = wire.GetProperty("state").GetProperty("simulationRate");
        Check(metric.EnumerateObject().Count() == 5 && metric.GetProperty("status").GetString() == "measured" && metric.GetProperty("automaticDaysPerRealSecond").GetDouble() == 0, "additive bounded scalar wire shape");
        var old = JsonSerializer.SerializeToElement(unchanged, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(!old.TryGetProperty("simulationRate", out _), "legacy nullable diagnostic omitted");
        throwSample = true;
        var committedPause = Execute(host, new("clock-failure-pause", "pause", Paused: true), 13, 13);
        Check(committedPause.Accepted && session.ObserveTiming().Paused, "diagnostic exception preserves committed command ACK");

        var failing = Session(manifest, failingEconomy: true);
        var failingHost = failing.Join("Host", "player", null, TimeSpan.Zero);
        var failingSecond = failing.Join("Second", "player", null, TimeSpan.Zero);
        var failedRuntime = new M3Server.Runtime(failing, "checks-code", CancellationToken.None, NullLogger.Instance, () => TimeSpan.FromSeconds(2));
        foreach (var player in new[] { failingHost, failingSecond })
        {
            failedRuntime.ExecuteOnce(player.ParticipantId, player.ConnectionId, new("assets", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest), TimeSpan.Zero);
            failedRuntime.ExecuteOnce(player.ParticipantId, player.ConnectionId, new("ready", "ready", Ready: true), TimeSpan.Zero);
        }
        failedRuntime.ExecuteOnce(failingHost.ParticipantId, failingHost.ConnectionId, new("start", "start"), TimeSpan.Zero);
        for (var day = 1; day <= 29; day++) { failedRuntime.ExecuteOnce(failingHost.ParticipantId, failingHost.ConnectionId, new("manual-" + day, "stepDay"), TimeSpan.Zero); }
        failedRuntime.ExecuteOnce(failingHost.ParticipantId, failingHost.ConnectionId, new("resume", "pause", Paused: false), TimeSpan.Zero);
        failedRuntime.TickOnce(TimeSpan.FromSeconds(1));
        var failedView = failedRuntime.Snapshot(failingHost.ParticipantId);
        Check(failedView.ElapsedDay == 29 && failedView.Paused && failedView.LastError is not null && failedView.SimulationRate!.Status == "paused" && failedView.SimulationRate.CompletedAutomaticDays == 0, "rejected automatic day remains atomic and contributes no completion");
        return checks;
    }

    private static M3GameSession Session(M3AssetManifest manifest, bool failingEconomy = false)
    {
        var contentManifest = new ContentManifest("4.5.2", "synthetic", [], []);
        var resources = new[] { "energy", "minerals", "food" }.Select(id => new ResourceDefinition(id, 1000000)).ToArray();
        var jobs = new[] { "technician", "miner", "farmer" }.Select((id, index) => new JobDefinition(id, "worker", 100, new Dictionary<string, decimal> { [resources[index].Id] = 4 }, failingEconomy ? new Dictionary<string, decimal> { ["energy"] = 1000000 } : new Dictionary<string, decimal>())).ToArray();
        var buildings = new[] { "building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility" }.Select(id => new BuildingDefinition(id, new Dictionary<string, decimal>(), [])).ToArray();
        var projection = new EconomyProjection(contentManifest, resources, jobs, [new CategoryDefinition("worker", null)], buildings, [], 100);
        var traits = new[] { "trait_agrarian", "trait_industrious", "trait_ingenious" }.ToDictionary(id => id, id => new TraitWorkforceProjection(contentManifest, [id], jobs.ToDictionary(job => job.Id, _ => 0m), []));
        return new(projection, traits, manifest);
    }

    private static async Task<M3AssetManifest> ManifestAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Stellaris-RateChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\"}");
            foreach (var relative in M3AssetManifestBuilder.ProfilePaths)
            {
                var path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, "synthetic");
            }
            return await new M3AssetManifestBuilder().BuildAsync(directory);
        }
        finally { Directory.Delete(directory, true); }
    }
}
