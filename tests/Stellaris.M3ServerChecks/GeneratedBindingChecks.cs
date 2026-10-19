using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Stellaris.Core.Assets;
using Stellaris.Core.Content;
using Stellaris.Core.Galaxy;
using Stellaris.Server;
using Stellaris.Session.M3;

internal static class GeneratedBindingChecks
{
    internal static async Task<int> RunAsync(string root, string worker, string sha)
    {
        var source = new ContentSession(root);
        var content = new NeutralEconomyImporter(source).Import(100);
        var traits = new[] { "trait_agrarian", "trait_industrious", "trait_ingenious" }.ToDictionary(id => id,
            id => new TraitWorkforceImporter(source).Import([id], content.Jobs));
        source.VerifyStable();
        var baseline = await new M3AssetManifestBuilder().BuildAsync(root);
        var startup = await M3StartupCatalog.ImportAsync(root);
        var galaxy = new M3GalaxyRuntime(GalaxyLobbyCatalog.Import(root, new([], [])),
            new NativeTriangleBackend(Path.GetFullPath(worker), sha, Path.Combine(Path.GetTempPath(), "m3-binding-checks-" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(30)));
        var validate = typeof(M3Server).GetMethod("ValidateSystemLighting", BindingFlags.Static | BindingFlags.NonPublic)!;
        void Validate(IReadOnlyList<string> classes, M3AssetManifest manifest)
        {
            try { validate.Invoke(null, [root, classes, manifest, CancellationToken.None]); }
            catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        }
        var failLighting = false; var failManifest = false; var checkedSystemCount = 0;
        var session = new M3GameSession(content, traits, baseline, startup: startup, galaxy: galaxy,
            galaxyManifest: (classes, objectIds) => failManifest ? baseline : new M3AssetManifestBuilder()
                .BuildAsync(root, new GeneratedWorldVisualRequirements(classes, objectIds.Count == 0 ? null : objectIds)).GetAwaiter().GetResult(),
            galaxyValidate: (candidate, manifest) => {
                if (manifest.GeneratedRequirements is not { } requirements || !requirements.ClassIds.SequenceEqual(candidate.ClassIds) ||
                    !(candidate.ObjectIds.Count == 0 ? requirements.ObjectIds is null : requirements.ObjectIds?.SequenceEqual(candidate.ObjectIds) == true))
                    throw new GeneratedWorldVisualException("requirements-mismatch", null, "test binding mismatch");
                var classes = candidate.Generated.Systems.Select(s => s.StarClassId).ToArray();
                checkedSystemCount = classes.Length;
                Validate(failLighting ? classes.Append("sc_unsupported_binding_check").ToArray() : classes, manifest);
            });
        var host = session.Join("Хост", "player", null, TimeSpan.Zero);
        var peer = session.Join("Игрок", "player", null, TimeSpan.Zero);
        var serial = 0; var checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS production binding: " + message); }
        M3CommandResult Send(M3JoinGrant who, M3ClientCommand command) => session.Execute(who.ParticipantId, who.ConnectionId,
            command with { CommandId = "binding-" + ++serial }, TimeSpan.Zero);
        void Accept(M3JoinGrant who, M3ClientCommand command) { var result = Send(who, command); if (!result.Accepted) throw new Exception(command.Type + ": " + result.Code); }
        M3ClientCommand Bound(string type, bool? ready = null) {
            var view = session.View(host.ParticipantId);
            return new("", type, Ready: ready, ProfileId: view.AssetProfile.ProfileId, ProfileDigest: view.AssetProfile.ProfileDigest,
                GalaxyWorldFingerprint: view.Galaxy!.WorldFingerprint, ExpectedLobbyRevision: type == "start" ? view.Galaxy!.LobbyRevision : null);
        }
        void Consent() { foreach (var player in new[] { host, peer }) Accept(player, new("", "ready", Ready: true, ExpectedLobbyRevision: session.View(host.ParticipantId).Galaxy!.LobbyRevision)); }
        M3ClientCommand InitialStart() => new("", "start", ExpectedLobbyRevision: session.View(host.ParticipantId).Galaxy!.LobbyRevision);
        var settings = new M3ClientCommand("", "setGalaxySettings", GalaxySeed: "production-binding", GalaxyScenarioId: "tiny", GalaxyShapeId: "elliptical", GalaxyDensity: 1);
        Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
        Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
        Accept(host, settings); Consent(); Accept(host, InitialStart());
        var generated = session.CurrentManifest;
        Check(generated.GeneratedRequirements is not null && generated.ProfileId != baseline.ProfileId && checkedSystemCount == 200,
            "actual generated requirements + все200system.sc прошли source lighting");
        Check(M3WireCodec.Serialize(generated).Length < M3WireCodec.MAX_OUTGOING_BYTES, "current dynamic manifest помещается в wire budget");

        var generatedRevision = session.View(host.ParticipantId).Galaxy!.LobbyRevision;
        var removedPeerId = peer.ParticipantId;
        Accept(host, new("binding-kick", "kickParticipant", TargetParticipantId: peer.ParticipantId));
        var afterKick = session.View(host.ParticipantId);
        var afterKickGalaxy = afterKick.Galaxy!;
        Check(afterKick.Phase == "lobby" && !afterKickGalaxy.StartPending && afterKickGalaxy.LobbyRevision > generatedRevision &&
            afterKickGalaxy.WorldFingerprint is null && afterKick.Participants.Single(item => item.Id == host.ParticipantId).Ready == false &&
            afterKick.Participants.All(item => item.Id != peer.ParticipantId),
            "generated candidate и Ready сброшены после удаления игрока на стадии ожидания ассетов");
        Accept(host, new("binding-host-ready-after-kick", "ready", Ready: true,
            ExpectedLobbyRevision: afterKickGalaxy.LobbyRevision));
        Accept(host, InitialStart());
        var afterRestartGalaxy = session.View(host.ParticipantId).Galaxy!;
        Check(afterRestartGalaxy.StartPending && afterRestartGalaxy.WorldFingerprint is not null,
            "оставшийся игрок может повторно подготовить generated start после изменения состава");

        peer = session.Join("Новый игрок", "player", null, TimeSpan.Zero);
        Check(peer.ParticipantId > removedPeerId, "production generated lobby assigns a fresh monotonic ID after kick");
        Accept(peer, new("binding-new-peer-template", "selectEmpireTemplate", TemplateId: "scientific"));

        Accept(host, settings); Consent();
        var before = JsonSerializer.Serialize(session.View(host.ParticipantId));
        var currentBeforeRejectedPrepare = session.CurrentManifest;
        failLighting = true;
        var rejected = Send(host, InitialStart()); failLighting = false;
        Check(!rejected.Accepted && rejected.Code == "DEPENDENCY_UNRESOLVED" && JsonSerializer.Serialize(session.View(host.ParticipantId)) == before
            && ReferenceEquals(currentBeforeRejectedPrepare, session.CurrentManifest), "unknown actual sc отказ атомарный; consent/revision/manifest сохранены");
        failManifest = true;
        rejected = Send(host, InitialStart()); failManifest = false;
        Check(!rejected.Accepted && rejected.Code == "DEPENDENCY_UNRESOLVED" && JsonSerializer.Serialize(session.View(host.ParticipantId)) == before,
            "baseline manifest не принимается вместо generated requirements");
        Validate(["sc_g"], generated);
        try { Validate(["sc_black_hole"], generated); throw new Exception("Unsupported black hole lighting accepted."); }
        catch (GeneratedWorldVisualException error) when (error.Code == "system-lighting-unavailable") { Check(true, "неподдержанный blackhole lighting безGfallback"); }
        var altered = generated.Files.Select(file => file.RelativePath == GeneratedWorldSystemLighting.Paths.First()
            ? new AssetManifestFile(file.RelativePath, file.ByteLength, new string('0', 64), file.Kind) : file).ToArray();
        var wrongHash = (M3AssetManifest)typeof(M3AssetManifest).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
            .Invoke([altered, generated.GeneratedRequirements]);
        try { Validate(["sc_g"], wrongHash); throw new Exception("Changed lighting metadata accepted."); }
        catch (GeneratedWorldVisualException error) when (error.Code == "lighting-source-hash") { Check(true, "lighting source SHA обязан совпадать с current manifest"); }
        Accept(host, InitialStart());
        foreach (var player in new[] { host, peer }) Accept(player, Bound("assetReady", true));
        Accept(host, Bound("start"));
        Check(session.View(host.ParticipantId).Spatial.Bodies.Where(b => b.WorldClassId is not null).All(b => b.VisualId == "world." + b.WorldClassId),
            "start сохраняет exact generated visual/class binding");
        return checks;
    }
}
