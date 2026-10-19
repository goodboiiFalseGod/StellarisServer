using System.Reflection;
using System.Text.Json;
using Stellaris.Core.Assets;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class GalaxySessionChecks
{
    public static int Run(string root, M3GalaxyRuntime galaxy, M3GalaxySettings settings, bool nebulaOnly = false, bool namesBudgetOnly = false, bool guaranteedOnly = false, bool habitableOnly = false, bool wormholeOnly = false)
    {
        var source = new ContentSession(root);
        var content = new NeutralEconomyImporter(source).Import(100);
        var traits = new[] { "trait_agrarian", "trait_industrious", "trait_ingenious" }.ToDictionary(id => id,
            id => new TraitWorkforceImporter(source).Import([id], content.Jobs));
        source.VerifyStable();
        var manifest = new M3AssetManifestBuilder().BuildAsync(root).GetAwaiter().GetResult();
        var startup = M3StartupCatalog.ImportAsync(root).GetAwaiter().GetResult();
        var catalog = new PlanetEconomyImporter().ImportAsync(root).GetAwaiter().GetResult();
        var research = new TechnologyImporter().Import(root);
        var rareDepositPolicy = M3RareDepositResearchPolicy.Import(root);
        var economy = new M3BaselineEconomyProfile(catalog, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, research.CategoryParents);
        var construction = M3ConstructionContent.ImportAsync(root, catalog).GetAwaiter().GetResult();
        var keys = M3TechnologyKeys.Import(root);
        var legacyConstruction = new ConstructionDefinitionImporter().ImportAsync(root).GetAwaiter().GetResult();
        var count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        void Test(string name, Action action) { action(); count++; Console.WriteLine("PASS Session: " + name); }
        // Только seam lifecycle: намеренно один test-only profile при разных worlds.
        var failManifest = false;
        var useGeneratedManifest = false;
        M3GameSession NewSession() => new(content, traits, manifest, construction: legacyConstruction, startup: startup, sourceEconomy: economy,
            constructionContent: construction, constructionImportAttempted: true, researchCatalog: research,
            technologyKeys: keys, galaxy: galaxy, rareDepositResearchPolicy: rareDepositPolicy, galaxyManifest: (classes, objectIds) => {
                Check(classes.Count > 10 && classes.SequenceEqual(classes.Order(StringComparer.Ordinal)), "Class requirements lost.");
                if (failManifest) throw new AssetManifestException("test-only failure");
                return useGeneratedManifest ? new M3AssetManifestBuilder().BuildAsync(root, new GeneratedWorldVisualRequirements(classes, objectIds.Count == 0 ? null : objectIds)).GetAwaiter().GetResult() : manifest;
            });
        var session = NewSession();
        var host = session.Join("Хост", "player", null, TimeSpan.Zero);
        var peer = session.Join("Игрок", "player", null, TimeSpan.Zero);
        var observer = session.Join("Наблюдатель", "observer", null, TimeSpan.Zero);
        var serial = 0;
        var now = TimeSpan.Zero;
        M3CommandResult Send(M3JoinGrant who, M3ClientCommand command) => session.Execute(who.ParticipantId, who.ConnectionId,
            command with { CommandId = "galaxy-check-" + ++serial }, now);
        M3SessionView View(M3JoinGrant who) => session.View(who.ParticipantId);
        M3ClientCommand Bound(string type, bool? ready = null, M3SessionView? view = null) {
            view ??= View(host);
            if (type == "ready") return new("", type, Ready: ready, ExpectedLobbyRevision: view.Galaxy!.LobbyRevision);
            return new("", type, Ready: ready, ProfileId: view.AssetProfile.ProfileId, ProfileDigest: view.AssetProfile.ProfileDigest,
                GalaxyWorldFingerprint: view.Galaxy!.WorldFingerprint, ExpectedLobbyRevision: type == "start" ? view.Galaxy!.LobbyRevision : null);
        }
        M3ClientCommand Settings(M3GalaxySettings value) => new("", "setGalaxySettings", GalaxySeed: value.Seed,
            GalaxyScenarioId: value.ScenarioId, GalaxyShapeId: value.ShapeId, GalaxyDensity: value.Density, GalaxyNebulae: value.Nebulae,
            GalaxyGuaranteedColonies: value.GuaranteedColonies, GalaxyGatewayFrequency: value.GatewayFrequency,
            GalaxyHabitableMultiplier: value.HabitableMultiplier, GalaxyWormholePairs: value.WormholePairs);
        void Accept(M3JoinGrant who, M3ClientCommand command) { var result = Send(who, command); Check(result.Accepted, command.Type + ": " + result.Code + ": " + result.Message); }
        void Reject(M3JoinGrant who, M3ClientCommand command, string code) { var result = Send(who, command); Check(!result.Accepted && result.Code == code, command.Type + " expected " + code + ", got " + result.Code); }
        void Consent() { foreach (var player in new[] { host, peer }) Accept(player, Bound("ready", true)); }
        M3ClientCommand InitialStart() => new("", "start", ExpectedLobbyRevision: View(host).Galaxy!.LobbyRevision);
        void Generate() { Consent(); Accept(host, InitialStart()); }
        void AckReady() { foreach (var player in new[] { host, peer }) Accept(player, Bound("assetReady", true)); }
        string State() => JsonSerializer.Serialize(View(host));
        if (habitableOnly)
        {
            Test("хост задаёт множитель 0..5, снимок подтверждает значение, peer и некорректные числа отклоняются", () => {
                Accept(host, Settings(settings with { HabitableMultiplier = 0.29m }));
                Check(View(host).Galaxy!.Settings.HabitableMultiplier == 0.29m && View(peer).Galaxy!.Settings.HabitableMultiplier == 0.29m,
                    "Подтверждённое значение не попало в host/peer snapshot.");
                Reject(peer, Settings(settings with { HabitableMultiplier = 2 }), "OUT_OF_SCOPE");
                Reject(host, Settings(settings with { HabitableMultiplier = 5.00001m }), "INVALID_GALAXY_SETTINGS");
                Reject(host, Settings(settings with { HabitableMultiplier = 1.000001m }), "INVALID_GALAXY_SETTINGS");
                Accept(host, Settings(settings with { HabitableMultiplier = 0 }));
                Check(View(peer).Galaxy!.Settings.HabitableMultiplier == 0, "Zero multiplier was not published to peer snapshot.");
            });
            return count;
        }
        if (wormholeOnly)
        {
            Test("хост подтверждает частоту червоточин, peer видит её и границы отклоняются", () => {
                Accept(host, Settings(settings with { WormholePairs = 5 }));
                Check(View(host).Galaxy!.Settings.WormholePairs == 5 && View(peer).Galaxy!.Settings.WormholePairs == 5,
                    "Подтверждённая частота не попала в host/peer snapshot.");
                Reject(peer, Settings(settings with { WormholePairs = 2 }), "OUT_OF_SCOPE");
                Reject(host, Settings(settings with { WormholePairs = -1 }), "INVALID_GALAXY_SETTINGS");
                Reject(host, Settings(settings with { WormholePairs = 6 }), "INVALID_GALAXY_SETTINGS");
                Accept(host, Settings(settings with { WormholePairs = 0 }));
                Check(View(peer).Galaxy!.Settings.WormholePairs == 0, "Нулевая частота не опубликована в peer snapshot.");
            });
            return count;
        }
        void CheckFullestBudget(string stage)
        {
            var candidate = (M3GalaxyStart)typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            var homes = candidate.Generated.Homes.Select(h => h.SystemId).ToHashSet(StringComparer.Ordinal);
            var inspected = candidate.Space.Systems.Where(s => !homes.Contains(s.Id)).OrderByDescending(s => candidate.Space.Bodies.Count(b => b.SystemId == s.Id)).First().Id;
            var fingerprint = candidate.Generated.WorldFingerprint;
            foreach (var player in new[] { host, peer }) Accept(player, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: fingerprint));
            Accept(observer, new("", "observerView", TargetParticipantId: 0));
            Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: fingerprint));
            var snapshots = new List<(string Label, M3SessionView View)> { ("host", View(host)), ("peer", View(peer)), ("general", View(observer)) };
            foreach (var player in new[] { host, peer }) {
                Accept(observer, new("", "observerView", TargetParticipantId: player.ParticipantId));
                Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: fingerprint));
                snapshots.Add(("observer-player-" + player.ParticipantId, View(observer)));
            }
            var sizes = snapshots.Select(item => (item.Label, Bytes: JsonSerializer.SerializeToUtf8Bytes(new { type = "snapshot", state = item.View }, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length)).ToArray();
            foreach (var item in sizes) Console.WriteLine($"FULL {stage} {item.Label}: {item.Bytes} bytes");
            Check(sizes.All(item => item.Bytes < 1024 * 1024), "Progressed/queued FULL production snapshot exceeds1MiB: " + stage);
            foreach (var player in new[] { host, peer }) Accept(player, new("", "inspectGalaxySystem", SystemId: "", GalaxyWorldFingerprint: fingerprint));
            Accept(observer, new("", "observerView", TargetParticipantId: 0));
        }
        void CheckHistoryBudget(string stage, M3JoinGrant? actor = null)
        {
            actor ??= host;
            var available = View(actor).Development!.Colonies.Single().Catalog.First(c => c.Kind == "building" && c.Eligibility == "available" && c.CanAfford == true && c.SlotIds.Count > 0 && c.Quote is not null);
            var beforeCount = View(actor).Development!.Colonies.Single().Queue.Count;
            for (var index = 0; index < 65; index++) {
                var colony = View(actor).Development!.Colonies.Single(); var orderId = "bounded-history-" + actor.ParticipantId + "-" + index;
                Accept(actor, new("", "enqueueConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision, DefinitionId: available.DefinitionId, SlotId: available.SlotIds.First(), OrderId: orderId));
                if (index < 64) {
                    colony = View(actor).Development!.Colonies.Single();
                    Accept(actor, new("", "cancelConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision, OrderId: orderId));
                }
            }
            var measured = View(actor).Development!.Colonies.Single();
            Check(measured.Queue.Count == beforeCount + 65 && measured.Queue.Count(order => order.Status == "pending") == 1, "History/queue measurement silently removed orders.");
            CheckFullestBudget(stage);
        }

        if (namesBudgetOnly)
        {
            Test("имена: один FULL Session huge maxdensity observer host POV budget", () => {
                useGeneratedManifest = true;
                var maximum = galaxy.Catalog.Scenarios.Single(s => s.Id == "huge").HyperlaneDensity.Maximum;
                Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
                Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
                Accept(host, Settings(settings with { ScenarioId = "huge", Density = maximum }));
                Generate(); AckReady();
                Accept(host, new("", "approveObserver", TargetParticipantId: observer.ParticipantId));
                Accept(host, Bound("start")); Accept(observer, Bound("assetReady", true));
                Accept(observer, new("", "observerView", TargetParticipantId: host.ParticipantId));
                var candidate = (M3GalaxyStart)typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
                var homes = candidate.Generated.Homes.Select(h => h.SystemId).ToHashSet(StringComparer.Ordinal);
                var inspected = candidate.Space.Systems.Where(s => !homes.Contains(s.Id)).OrderByDescending(s => candidate.Space.Bodies.Count(b => b.SystemId == s.Id)).First().Id;
                Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: candidate.Generated.WorldFingerprint));
                var view = View(observer);
                Check(view.Spatial.Systems.Count == 1000 && view.Research!.Empires.Count == 1 && view.Development!.Colonies.Count == 1, "Требуется полный observer host POV snapshot.");
                Check(session.CurrentManifest.GeneratedRequirements!.ClassIds.SequenceEqual(candidate.ClassIds), "Не production generated manifest.");
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "snapshot", state = view }, options).Length;
                Console.WriteLine($"FULL names huge density={maximum} observer-host: {bytes} bytes; spatial={JsonSerializer.SerializeToUtf8Bytes(view.Spatial, options).Length}; headroom={1024 * 1024 - bytes}; bodies={view.Spatial.Bodies.Count}");
                Check(bytes < 1024 * 1024, "FULL names huge snapshot exceeds1MiB.");
            });
            return count;
        }

        if (nebulaOnly)
        {
            Test("туманности: настройки, атомарность, privacy, prepare/start и count0", () => {
                Check(View(host).Galaxy!.Settings.Nebulae == 2, "Tiny source default lost.");
                Accept(host, Settings(settings));
                Check(View(host).Galaxy!.Settings.Nebulae == 2, "Old caller default lost.");
                var unchanged = State();
                foreach (var invalid in new[] { -1, 3 }) {
                    var result = Send(host, Settings(settings with { Nebulae = invalid }));
                    Check(!result.Accepted && State() == unchanged, "Invalid count mutated state.");
                }
                Check(!Send(peer, Settings(settings with { Nebulae = 0 })).Accepted, "Peer changed count.");
                Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
                Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
                Generate();
                var candidate = (M3GalaxyStart)typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
                Check(candidate.Generated.Nebulae.Nebulae.Count == 2, "Nebulae missing after prepare.");
                var member = candidate.Generated.Nebulae.Nebulae[0].SystemIds[0];
                var limited = M3GalaxyProjection.Project(candidate, member, host.ParticipantId, false,
                    new HashSet<string> { member }, new HashSet<string>(), new Dictionary<int, int>());
                Check(limited.Nebulae!.Count == 1 && limited.Nebulae[0].SystemIds.SequenceEqual(new[] { member }) && limited.Bodies.Count == 0, "Projection leaked hidden members/bodies.");
                var sourceNebula = candidate.Generated.Nebulae.Nebulae[0];
                Check(limited.Nebulae[0].Radius == sourceNebula.Radius / 100000d && limited.Nebulae[0].Center.X == sourceNebula.Center.X / 100000d, "Geometry units changed.");
                AckReady(); Accept(host, Bound("start"));
                Check(View(host).Phase == "running" && View(host).Spatial.Nebulae!.Count == 2, "Prepared candidate lost at start.");
                session = NewSession(); host = session.Join("Хост", "player", null, now); peer = session.Join("Игрок", "player", null, now);
                Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
                Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
                Accept(host, Settings(settings with { Nebulae = 0 }));
                Consent();
                Accept(host, Settings(settings with { Nebulae = 1 }));
                Check(View(host).Participants.Where(p => p.Role == "player").All(p => !p.Ready) && View(host).Galaxy!.Stage == "unprepared", "Count failed consent invalidation.");
                Accept(host, Settings(settings with { Nebulae = 0 })); Generate(); AckReady(); Accept(host, Bound("start"));
                Check(View(host).Spatial.Nebulae!.Count == 0, "Zero count retained stale nebulae.");
            });
            return count;
        }

        if (guaranteedOnly)
        {
            Test("guaranteed 0/1/2 settings, tiny two-player batches, provenance and ownership projection", () => {
                Check(View(host).Galaxy!.Settings.GuaranteedColonies is null or 0, "Legacy setting did not default to0.");
                Accept(host, Settings(settings));
                Check(View(host).Galaxy!.Settings.GuaranteedColonies == 0, "Host setting default did not normalize to0.");
                var unchanged = State();
                foreach (var invalid in new[] { -1, 3 })
                {
                    var result = Send(host, Settings(settings with { GuaranteedColonies = invalid }));
                    Check(!result.Accepted && State() == unchanged, "Invalid guaranteed count mutated lobby state.");
                }
                Reject(peer, Settings(settings with { GuaranteedColonies = 1 }), "OUT_OF_SCOPE");
                Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
                Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
                Accept(host, Settings(settings with { GuaranteedColonies = 2 }));
                Check(View(host).Galaxy!.Settings.GuaranteedColonies == 2 && View(peer).Galaxy!.Settings.GuaranteedColonies == 2, "Lobby projection disagrees on guaranteed count.");
                Generate();
                var candidate = (M3GalaxyStart)typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
                Check(candidate.Generated.GuaranteedAllocations.Count == 8, "Two two-system guaranteed chains per player were not allocated.");
                Check(candidate.Generated.GuaranteedAllocations.Select(a => a.ChildSystemId).Distinct(StringComparer.Ordinal).Count() == 8, "Neighbor allocations duplicate a system.");
                Check(candidate.Generated.GuaranteedAllocations.All(a => a.Hops is >= 1 and <= 3 && a.RootHomeSystemId == candidate.Generated.Homes.Single(h => h.ParticipantId == a.ParticipantId).SystemId), "Allocation provenance lost root ownership or hop bounds.");
                var guaranteedBodyAllocations = candidate.Generated.GuaranteedAllocations.Where(a => a.GuaranteedBodyId is not null).ToArray();
                var guaranteedBodies = guaranteedBodyAllocations.Select(a => candidate.Generated.Bodies.Single(b => b.Id == a.GuaranteedBodyId)).ToArray();
                Check(guaranteedBodies.Length == 4 && guaranteedBodies.All(b => b.Flags.Contains("prescripted_ideal", StringComparer.Ordinal) && b.PreventAnomaly), "Guaranteed body spatial effects were not applied.");
                Check(candidate.Space.Colonies.Count == 2 && candidate.Space.Colonies.All(c => candidate.Generated.Homes.Any(h => h.BodyId == c.BodyId && h.ParticipantId == c.OwnerParticipantId)), "A neighbor created a colony or changed home ownership.");
                var json = State();
                Check(!json.Contains("guaranteedAllocations", StringComparison.Ordinal), "Full server allocation provenance leaked through lobby wire projection.");
                var guaranteed = guaranteedBodyAllocations[0];
                var allowedSystem = new HashSet<string>(StringComparer.Ordinal) { guaranteed.ChildSystemId };
                var allowedBody = new HashSet<string>(StringComparer.Ordinal) { guaranteed.GuaranteedBodyId! };
                var projected = M3GalaxyProjection.Project(candidate, guaranteed.ChildSystemId, guaranteed.ParticipantId, false,
                    allowedSystem, allowedBody, new Dictionary<int, int>());
                Check(projected.Bodies.Any(body => body.Id == guaranteed.GuaranteedBodyId), "Permitted detailed projection omitted the owned guaranteed world.");
                Check(projected.Colonies.Count == 0, "Guaranteed world projection fabricated a colony.");
            });
            return count;
        }
        Test("lossless catalog tables сохраняют порядок/кратность/unknowns/null и private boundary", () => {
            var firstTerm = new M3ConstructionSourceTermView("JobCapacity", "fixture_job", 0.15m, null, "InactiveInOrdinaryContext", "Planet");
            var unknownTerm = new M3ConstructionSourceTermView("PlanetModifier", "fixture_unknown", null, "owner.fixture_value", "RequiresRuntimeContext", "Planet");
            var entries = new[] {
                new M3ConstructionCatalogView("fixture_a", "building", null, "unavailable", [], null, [new("dependency-unresolved", "fixture-unknown")], CompatibleSlotIds: ["slot/1", "slot/2", "slot/1"], SourceRecipe: [firstTerm, unknownTerm, firstTerm]),
                new M3ConstructionCatalogView("fixture_b", "building", null, "available", ["slot/2"], null, [], CompatibleSlotIds: ["slot/1", "slot/2", "slot/1"], SourceRecipe: [unknownTerm, firstTerm]),
                new M3ConstructionCatalogView("fixture_null", "building", null, "unavailable", [], null, []),
                new M3ConstructionCatalogView("fixture_empty", "building", null, "unavailable", [], null, [], CompatibleSlotIds: [], SourceRecipe: [])
            };
            var original = new M3DevelopmentView("fixture", [new("colony", 1, 1, 0, "individualist", false, null, null, [], entries, [], [], [], [], [])], []);
            var compactMethod = typeof(M3GameSession).Assembly.GetType("Stellaris.Session.M3.M3DevelopmentProjection")!.GetMethod("CompactCatalog", BindingFlags.Static | BindingFlags.NonPublic)!;
            var compact = (M3DevelopmentView)compactMethod.Invoke(null, [original])!;
            var compactJson = JsonSerializer.Serialize(compact, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using (var compactDocument = JsonDocument.Parse(compactJson))
                Check(compactDocument.RootElement.GetProperty("sourceRecipeTerms")[0].GetArrayLength() == 6, "Source recipe wire table must contain six-field tuples.");
            compact = JsonSerializer.Deserialize<M3DevelopmentView>(compactJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var expanded = compact with { SourceRecipeTerms = null, CatalogSlotSets = null, SourceRecipeEncoding = null, SourceRecipeStrings = null, Colonies = compact.Colonies.Select(colony => colony with {
                Catalog = colony.Catalog.Select(entry => entry with {
                    SourceRecipe = entry.SourceRecipeIndices?.Select(index => compact.SourceRecipeTerms![index]).ToArray(),
                    CompatibleSlotIds = entry.CompatibleSlotSetIndex is { } index ? compact.CatalogSlotSets![index] : null,
                    SourceRecipeIndices = null, CompatibleSlotSetIndex = null }).ToArray() }).ToArray() };
            Check(JsonSerializer.Serialize(original) == JsonSerializer.Serialize(expanded), "Catalog tables lost source metadata/order/multiplicity or gate reasons.");
            Check(compact.SourceRecipeTerms!.Count == 2 && compact.CatalogSlotSets!.Count == 2 && compact.Colonies.Single().Catalog.Count == entries.Length, "Tables failed structural dedup or dropped catalog entries.");
            var general = original with { Colonies = [] };
            Check(ReferenceEquals(compactMethod.Invoke(null, [general]), general), "General/pending projection acquired private catalog metadata.");
            try { ((IList<M3ConstructionSourceTermView>)compact.SourceRecipeTerms)[0] = unknownTerm; throw new Exception("Mutable table."); }
            catch (NotSupportedException) { }
            try { JsonSerializer.Deserialize<M3DevelopmentView>("""{"contentProfileId":"fixture","colonies":[],"gates":[],"sourceRecipeTerms":[["bad-row"]]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web)); throw new Exception("Malformed source table accepted."); }
            catch (JsonException) { }
            var legacyTable = compact with { SourceRecipeEncoding = null, SourceRecipeStrings = null };
            var legacyRoundtrip = JsonSerializer.Deserialize<M3DevelopmentView>(JsonSerializer.Serialize(legacyTable, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Check(legacyRoundtrip.SourceRecipeTerms!.SequenceEqual(compact.SourceRecipeTerms), "Legacy string tuple6 table failed explicit decoding.");
            var aliases = System.Text.Json.Nodes.JsonNode.Parse(compactJson)!;
            var aliasStrings = aliases["sourceRecipeStrings"]!.AsArray();
            aliasStrings.Add(aliasStrings[0]!.DeepClone()); aliases["sourceRecipeTerms"]![0]![0] = aliasStrings.Count - 1;
            var aliased = JsonSerializer.Deserialize<M3DevelopmentView>(aliases.ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var aliasRoundtrip = JsonSerializer.Deserialize<M3DevelopmentView>(JsonSerializer.Serialize(aliased, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Check(aliasRoundtrip.SourceRecipeTerms!.SequenceEqual(compact.SourceRecipeTerms), "Duplicate string aliases changed expanded source terms on read/write.");
            foreach (var field in new[] { "encoding", "string", "recipe", "slot", "table" }) {
                var invalid = System.Text.Json.Nodes.JsonNode.Parse(compactJson)!;
                if (field == "encoding") invalid["sourceRecipeEncoding"] = "unknown-encoding";
                if (field == "string") invalid["sourceRecipeTerms"]![0]![0] = 99999;
                if (field == "recipe") invalid["colonies"]![0]!["catalog"]![0]!["sourceRecipeIndices"]![0] = -1;
                if (field == "slot") invalid["colonies"]![0]!["catalog"]![0]!["compatibleSlotSetIndex"] = 99999;
                if (field == "table") invalid.AsObject().Remove("catalogSlotSets");
                try { JsonSerializer.Deserialize<M3DevelopmentView>(invalid.ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web)); throw new Exception("Invalid catalog reference accepted: " + field); }
                catch (JsonException) { }
            }
            var empty = (M3DevelopmentView)compactMethod.Invoke(null, [original with { Colonies = [original.Colonies.Single() with { Catalog = [] }] }])!;
            var emptyRoundtrip = JsonSerializer.Deserialize<M3DevelopmentView>(JsonSerializer.Serialize(empty, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Check(emptyRoundtrip.SourceRecipeTerms!.Count == 0 && emptyRoundtrip.CatalogSlotSets!.Count == 0 && emptyRoundtrip.SourceRecipeStrings!.Count == 0, "Empty shared tables changed null/empty semantics.");
        });

        Test("player roster меняет ревизию и consent; observer roster не голосует и не меняет ревизию", () => {
            var isolated = NewSession();
            var first = isolated.Join("Первый", "player", null, now);
            M3CommandResult Submit(M3JoinGrant player, M3ClientCommand command) => isolated.Execute(player.ParticipantId, player.ConnectionId, command with { CommandId = "roster-" + ++serial }, now);
            Check(Submit(first, new("", "selectEmpireTemplate", TemplateId: "economic")).Accepted, "Preset до peer не принят.");
            var revision = isolated.View(first.ParticipantId).Galaxy!.LobbyRevision;
            Check(Submit(first, new("", "ready", Ready: true, ExpectedLobbyRevision: revision)).Accepted, "Первый игрок не смог Ready без peer/assets.");
            Check(isolated.View(first.ParticipantId).Galaxy!.CanPrepare, "Одиночная готовность не разблокировала Start.");
            var second = isolated.Join("Второй", "player", null, now);
            var changed = isolated.View(first.ParticipantId);
            Check(changed.Galaxy!.LobbyRevision > revision && changed.Participants.All(p => !p.Ready), "Player join сохранил consent.");
            Check(Submit(first, new("", "ready", Ready: true, ExpectedLobbyRevision: revision)).Code == "STALE_LOBBY_REVISION", "Stale consent после join принят.");
            var watcher = isolated.Join("Зритель", "observer", null, now);
            Check(isolated.View(first.ParticipantId).Galaxy!.LobbyRevision == changed.Galaxy.LobbyRevision, "Observer join изменил player revision.");
            isolated.Disconnect(watcher.ParticipantId, watcher.ConnectionId, now);
            Check(isolated.View(first.ParticipantId).Galaxy!.LobbyRevision == changed.Galaxy.LobbyRevision, "Observer disconnect изменил player revision.");
            Check(Submit(second, new("", "ready", Ready: true, ExpectedLobbyRevision: changed.Galaxy.LobbyRevision)).Code == "INVALID_PHASE", "Ready без preset принят.");
        });
        Test("overflow ревизии draft не публикует частичное изменение", () => {
            var field = typeof(M3GameSession).GetField("_lobbyRevision", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var saved = field.GetValue(session);
            try {
                field.SetValue(session, long.MaxValue);
                var before = State();
                Reject(host, Settings(settings), "INVALID_TARGET");
                Check(State() == before, "Revision overflow изменил draft/readiness.");
            } finally { field.SetValue(session, saved); }
        });
        Test("Ready до assets, host Start после общего согласия; observer и ручной prepare запрещены", () => {
            Reject(host, Bound("assetReady", true), "STALE_GALAXY_CANDIDATE");
            Reject(host, new("", "prepareGalaxy"), "INVALID_COMMAND");
            Reject(host, InitialStart(), "INVALID_PHASE");
            Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
            Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
            Check(!View(host).Galaxy!.CanPrepare, "CanPrepare до согласия.");
            Reject(peer, Settings(settings), "OUT_OF_SCOPE");
            var before = State();
            Reject(host, Settings(settings with { Density = -1 }), "INVALID_GALAXY_SETTINGS");
            Check(State() == before, "Invalid settings changed state.");
            Accept(host, Settings(settings with { Seed = "" }));
            Check(!string.IsNullOrWhiteSpace(View(host).Galaxy!.Settings.Seed), "Blank seed unresolved.");
            Accept(host, Settings(settings));
            var draft = View(host);
            Reject(observer, Bound("ready", true), "OUT_OF_SCOPE");
            Accept(peer, Bound("ready", true));
            Reject(peer, InitialStart(), "OUT_OF_SCOPE");
            Reject(host, InitialStart(), "INVALID_PHASE");
            Check(View(host).Galaxy!.Stage == "unprepared" && !View(peer).Participants.Single(p => p.Id == peer.ParticipantId).AssetReady, "Ready сгенерировал мир/подтвердил assets.");
            Accept(host, Bound("ready", true));
            Check(View(host).Galaxy!.CanPrepare, "All current consent не разблокировал Start.");
            Accept(host, InitialStart());
            Check(View(host).Galaxy!.LobbyRevision == draft.Galaxy!.LobbyRevision && View(host).Galaxy!.StartPending && View(host).Participants.Where(p => p.Role == "player").All(p => p.Ready), "Генерация сбросила согласие.");
            Check(View(host).Galaxy!.Stage == "awaitingAssets" && View(host).Spatial.Systems.Count == 0 && View(host).Research!.Empires.Count == 0, "Prepared candidate leaked running state.");
            Check(ReferenceEquals(session.CurrentManifest, manifest), "Current manifest endpoint source mismatch.");
        });
        Test("generation failure атомарный; template/settings инвалидируют ревизию и всё согласие", () => {
            Accept(host, Settings(settings)); Consent();
            var before = State(); failManifest = true;
            Reject(host, InitialStart(), "DEPENDENCY_UNRESOLVED"); failManifest = false;
            Check(State() == before && ReferenceEquals(session.CurrentManifest, manifest), "Failed manifest published partial state.");
            Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic"));
            Check(View(host).Galaxy!.Stage == "unprepared" && View(host).Participants.All(p => !p.AssetReady && !p.Ready), "Template kept readiness.");
            Generate(); AckReady();
            Accept(host, Settings(settings));
            Check(View(host).Participants.All(p => !p.AssetReady && !p.Ready), "Settings kept readiness.");
            Generate();
        });
        Test("stale revision/candidate, duplicate Start, ready withdrawal и reconnect", () => {
            var old = View(host); AckReady();
            Accept(host, Settings(settings with { Seed = "new-candidate-same-profile" }));
            Generate();
            Check(View(host).AssetProfile == old.AssetProfile && View(host).Galaxy!.WorldFingerprint != old.Galaxy!.WorldFingerprint, "Test lacks same-profile/new-world.");
            Reject(host, Bound("assetReady", true, old), "STALE_GALAXY_CANDIDATE");
            Reject(host, Bound("ready", true, old), "STALE_LOBBY_REVISION");
            Reject(host, Bound("start", view: old), "STALE_LOBBY_REVISION");
            var pending = State();
            Reject(host, InitialStart(), "INVALID_PHASE");
            Reject(host, new("", "prepareGalaxy"), "INVALID_COMMAND");
            Check(State() == pending, "Повторный Start/prepare изменил candidate.");
            Accept(peer, Bound("ready", false));
            Check(View(host).Participants.All(p => !p.Ready && !p.AssetReady) && !View(host).Galaxy!.StartPending && View(host).Galaxy!.WorldFingerprint is null, "Withdrawal сохранил pending consent.");
            Generate(); AckReady();
            Accept(host, Bound("assetReady", false));
            Check(View(host).Galaxy!.StartPending && View(host).Participants.Where(p => p.Role == "player").All(p => p.Ready), "Asset withdrawal потребовал повторный Ready.");
            Reject(host, Bound("start"), "INVALID_PHASE");
            AckReady();
            var beforeDisconnect = View(host).Galaxy!.LobbyRevision;
            session.Disconnect(peer.ParticipantId, peer.ConnectionId, TimeSpan.Zero);
            Check(View(host).Galaxy!.LobbyRevision > beforeDisconnect && !View(host).Galaxy!.StartPending && View(host).Participants.All(p => !p.Ready), "Disconnect сохранил consent/pending.");
            peer = session.Join("Игрок", "player", peer.Token, TimeSpan.Zero);
            Check(!View(peer).Participants.Single(p => p.Id == peer.ParticipantId).AssetReady, "Reconnect kept ACK.");
            Reject(host, InitialStart(), "INVALID_PHASE"); Generate(); AckReady();
        });
        Test("production preset-only отвергает custom traits/buildings атомарно", () => {
            var before = State();
            Reject(host, new("", "configure", Traits: [], Buildings: []), "PRESET_CONFIGURATION_ONLY");
            Reject(host, new("", "configure", Traits: ["trait_agrarian"]), "PRESET_CONFIGURATION_ONLY");
            Check(State() == before && View(host).EmpireSetup!.SelectedTemplateId == "economic", "Custom configure mutated preset state.");
            AckReady();
        });
        Test("atomic startup failure до publish", () => {
            var plansField = typeof(M3StartupCatalog).GetField("_plans", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var plans = plansField.GetValue(startup);
            Accept(host, Settings(settings)); Consent();
            var before = State();
            try {
                plansField.SetValue(startup, new Dictionary<(string, int), StartupBuildingPlan>());
                Reject(host, InitialStart(), "DEPENDENCY_UNRESOLVED");
                Check(State() == before, "Startup failure mutated state.");
            } finally { plansField.SetValue(startup, plans); }
            Accept(host, InitialStart()); AckReady();
        });
        Test("atomic construction/research failure сохраняет economy/phase/readiness", () => {
            var candidateField = typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var candidate = (M3GalaxyStart)candidateField.GetValue(session)!;
            var colonies = candidate.Startup.ToDictionary(p => p.Key, p => p.Value);
            var first = colonies.Values.First();
            colonies[first.ColonyId] = first with { Buildings = first.Buildings.Select(b => b with { SlotInstanceId = null, CompatibleZoneInstanceIds = [] }).ToArray() };
            var broken = (M3GalaxyStart)typeof(M3GalaxyStart).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke([candidate.Generated, candidate.Space, colonies, candidate.Visuals, candidate.BodySemantics]);
            try {
                candidateField.SetValue(session, broken);
                var before = State(); Reject(host, Bound("start"), "DEPENDENCY_UNRESOLVED");
                Check(State() == before, "Construction failure mutated published state.");
            } finally { candidateField.SetValue(session, candidate); }
            var field = typeof(M3GameSession).GetField("_researchCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!;
            try {
                field.SetValue(session, new TechnologyCatalog([], [], 3, 1, 0));
                var before = State(); Reject(host, Bound("start"), "DEPENDENCY_UNRESOLVED");
                Check(State() == before, "Research failure mutated published state.");
            } finally { field.SetValue(session, research); }
        });
        Test("huge maxdensity FULL Session budget, exact homes/economy IDs and privacy", () => {
            var maximum = galaxy.Catalog.Scenarios.Single(s => s.Id == "huge").HyperlaneDensity.Maximum;
            useGeneratedManifest = true;
            Accept(host, Settings(settings with { ScenarioId = "huge", Density = maximum }));
            Generate(); AckReady();
            var frozen = View(host).Galaxy!.WorldFingerprint;
            Check(View(observer).Spatial.Systems.Count == 0 && View(observer).Research!.Empires.Count == 0, "Pending observer leaked world.");
            Reject(observer, new("", "inspectGalaxySystem", SystemId: "", GalaxyWorldFingerprint: frozen), "ASSETS_NOT_READY");
            Accept(host, new("", "approveObserver", TargetParticipantId: observer.ParticipantId));
            Accept(host, Bound("start"));
            var candidate = (M3GalaxyStart)typeof(M3GameSession).GetField("_galaxyCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            Check(session.CurrentManifest.GeneratedRequirements!.ClassIds.SequenceEqual(candidate.ClassIds), "Full Session budget used a fixture manifest.");
            Check(View(host).Galaxy!.WorldFingerprint == frozen && View(host).Spatial.Systems.Count == 1000, "Start regenerated/replaced world.");
            var actualPlanets = View(host).Planets.Concat(View(peer).Planets).ToDictionary(p => p.OwnerId, p => p.Id);
            Check(candidate.Space.Colonies.All(c => actualPlanets[c.OwnerParticipantId] == c.EconomyPlanetId), "Actual economy IDs lost.");
            Console.WriteLine("Actual economy planet IDs: " + string.Join(",", candidate.Space.Colonies.Select(c => c.EconomyPlanetId)));
            foreach (var home in candidate.Generated.Homes) {
                var colony = candidate.Startup.Values.Single(c => c.OwnerParticipantId == home.ParticipantId);
                Check(colony.Homeworld.PlanetSize == home.Size && colony.Plan.Context.PlanetClass == home.ClassId && colony.Homeworld.InitializerId == home.InitializerId, "Exact home changed.");
            }
            Check(View(host).Research!.Empires.Count == 1 && View(host).Development!.Colonies.Count == 1, "Player private economy/research leaked.");
            Accept(observer, Bound("assetReady", true));
            Check(View(observer).Research!.Empires.Count == 2, "General observer lacks economies.");
            var homeSystems = candidate.Generated.Homes.Select(h => h.SystemId).ToHashSet(StringComparer.Ordinal);
            var inspected = candidate.Space.Systems.Where(s => !homeSystems.Contains(s.Id)).OrderByDescending(s => candidate.Space.Bodies.Count(b => b.SystemId == s.Id)).First().Id;
            Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: frozen));
            foreach (var player in new[] { host, peer }) Accept(player, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: frozen));
            var view = View(observer);
            var budgetViews = new List<(string Label, M3SessionView View)> { ("host", View(host)), ("peer", View(peer)), ("general", view) };
            foreach (var pov in new[] { host.ParticipantId, peer.ParticipantId }) {
                Accept(observer, new("", "observerView", TargetParticipantId: pov));
                Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: frozen));
                var povView = View(observer);
                Check(povView.Planets.Count == 1 && povView.Development!.Colonies.Count == 1 && povView.Development.Colonies.Single().OwnerParticipantId == pov
                    && povView.Research!.Empires.Single().ParticipantId == pov, "Matching POV catalog tables crossed private owner boundary.");
                budgetViews.Add(("observer-player-" + pov, povView));
            }
            Accept(observer, new("", "observerView", TargetParticipantId: 0));
            Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: frozen));
            foreach (var (label, participantView) in budgetViews) {
                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var fullBytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "snapshot", state = participantView }, jsonOptions).Length;
                Console.WriteLine($"FULL production {label}: {fullBytes} bytes; spatial={JsonSerializer.SerializeToUtf8Bytes(participantView.Spatial, jsonOptions).Length}; development={JsonSerializer.SerializeToUtf8Bytes(participantView.Development, jsonOptions).Length}; research={JsonSerializer.SerializeToUtf8Bytes(participantView.Research, jsonOptions).Length}");
                foreach (var colony in participantView.Development!.Colonies)
                {
                    Console.WriteLine($"Colony sections {label}: gates={JsonSerializer.SerializeToUtf8Bytes(colony.Gates, jsonOptions).Length}; catalog={JsonSerializer.SerializeToUtf8Bytes(colony.Catalog, jsonOptions).Length}; economy={JsonSerializer.SerializeToUtf8Bytes(colony.Economy, jsonOptions).Length}");
                    var fields = colony.Catalog.Select(entry => JsonSerializer.SerializeToElement(entry, jsonOptions))
                        .SelectMany(entry => entry.EnumerateObject()).GroupBy(property => property.Name)
                        .ToDictionary(group => group.Key, group => group.Sum(property => System.Text.Encoding.UTF8.GetByteCount(property.Value.GetRawText())));
                    Console.WriteLine($"Catalog field sizes {label}: " + JsonSerializer.Serialize(fields));
                }
            }
            Check(budgetViews.All(item => JsonSerializer.SerializeToUtf8Bytes(new { type = "snapshot", state = item.View }, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length < 1024 * 1024), "Actual production player/peer/general/matching POV FULL snapshot exceeds1MiB.");
            foreach (var player in new[] { host, peer }) Accept(player, new("", "inspectGalaxySystem", SystemId: "", GalaxyWorldFingerprint: frozen));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "snapshot", state = view }, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length;
            Console.WriteLine($"FULL Session huge density={maximum}: {bytes} bytes with economy/research/metadata.");
            Check(bytes < 1024 * 1024, "FULL Session exceeds1MiB.");
            Check(view.Spatial.Systems.All(s => s.StarClassId == candidate.Generated.Systems.Single(g => g.Id == s.Id).StarClassId), "System sc_* mismatch.");
            Check(view.Spatial.Bodies.Any(b => b.Kind == "moon" && b.ParentBodyId is not null && b.WorldClassId is not null), "Moon metadata lost.");
            Reject(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: "stale"), "STALE_GALAXY_CANDIDATE");
            Reject(observer, new("", "inspectGalaxySystem", SystemId: "unknown", GalaxyWorldFingerprint: frozen), "OUT_OF_SCOPE");
            Accept(observer, new("", "inspectGalaxySystem", SystemId: "", GalaxyWorldFingerprint: frozen));
            Check(!View(observer).Spatial.Bodies.Any(b => b.SystemId == inspected && b.Kind != "star"), "Empty inspect did not clear details.");
            Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: frozen));
            session.Disconnect(observer.ParticipantId, observer.ConnectionId, now);
            observer = session.Join("Наблюдатель", "observer", observer.Token, now);
            Accept(observer, Bound("assetReady", true));
            Check(!View(observer).Spatial.Bodies.Any(b => b.SystemId == inspected && b.Kind != "star"), "Reconnect kept inspect details.");
            Accept(observer, new("", "inspectGalaxySystem", SystemId: inspected, GalaxyWorldFingerprint: frozen));
            Accept(observer, new("", "observerView", TargetParticipantId: host.ParticipantId));
            Check(View(observer).Research!.Empires.Count == 1, "Player POV research leaked.");
            Check(!View(observer).Spatial.Bodies.Any(b => b.SystemId == inspected && b.Kind != "star") || candidate.Space.Colonies.Any(c => candidate.Space.Bodies.Single(b => b.Id == c.BodyId).SystemId == inspected), "POV kept inspect detail.");
            Reject(host, Settings(settings), "INVALID_PHASE");
        });
        Test("actual fleet/day/month/construction/research сохраняют runtime", () => {
            var view = View(host); var fleet = view.Spatial.Fleets.Single(f => f.OwnerParticipantId == host.ParticipantId);
            var location = (M3SystemLocationView)fleet.Location;
            var lane = view.Spatial.Hyperlanes.First(l => l.FromSystemId == location.SystemId || l.ToSystemId == location.SystemId);
            var target = lane.FromSystemId == location.SystemId ? lane.ToSystemId : lane.FromSystemId;
            Accept(host, new("", "moveFleetToSystem", FleetId: fleet.Id, TargetSystemId: target));
            var area = view.Research!.Empires.Single().Areas.Where(a => a.Choices.Count > 0)
                .OrderBy(a => a.Choices.Min(t => t.Cost ?? decimal.MaxValue)).First();
            var choice = area.Choices.OrderBy(t => t.Cost ?? decimal.MaxValue).First();
            Accept(host, new("", "selectResearch", Area: area.Id, TechnologyId: choice.Id, ExpectedResearchRevision: view.Research.Empires.Single().Revision));
            for (var day = 0; day < 30; day++) Accept(host, new("", "stepDay"));
            view = View(host);
            Check(view.ElapsedDay == 30 && view.Month == 1 && view.Development!.Colonies.Single().Economy!.Jobs.Any(j => j.Assigned > 0), "Real month/economy lost.");
            Check(JsonSerializer.Serialize(view.Spatial.Fleets.Single(f => f.Id == fleet.Id).Location) != JsonSerializer.Serialize(fleet.Location), "Current-world fleet projection frozen.");
            Check(view.Research!.Empires.Single().Areas.Single(a => a.Id == area.Id).Progress > 0, "Real research made no progress.");
            foreach (var installed in view.Development!.Colonies.Single().Installed.Where(i => i.Upgrades is { Count: > 0 }))
                Console.WriteLine("Upgrade source edge: " + installed.DefinitionId + " → " + string.Join(",", installed.Upgrades!.Select(u => u.DefinitionId + ":" + string.Join("/", u.Gates.Select(g => g.DependencyId)))));
        });
        Test("source quote оплачивается и отменяется без искусственного funding", () => {
            // Используем те же команды Workforce, что доступны игроку. Специалисты
            // на максимуме вытесняют работников и могут исчерпать стартовые minerals.
            foreach (var player in new[] { host, peer }) {
                var jobs = View(player).Development!.Colonies.Single().Economy!.Jobs;
                var activeArea = View(player).Research!.Empires.Single().Areas.FirstOrDefault(a => a.ActiveTechnologyId is not null)?.Id;
                var scienceJob = activeArea switch { "physics" => "physicist", "engineering" => "engineer", "society" => "biologist", _ => null };
                Console.WriteLine("Source capacities " + player.ParticipantId + ": " + string.Join(",", jobs.Select(j => j.JobId + "=" + j.Capacity)));
                foreach (var job in jobs.Where(j => j.Capacity > 0)) {
                    var current = View(player).Development!.Colonies.Single();
                    var limit = job.JobId is "miner" or "technician" or "farmer" ? job.Capacity :
                        job.JobId == scienceJob ? job.Capacity : job.JobId == "artisan" ? job.Capacity / 4 : 0;
                    Accept(player, new("", "setWorkforceLimit", ColonyId: current.ColonyId, ExpectedColonyRevision: current.Revision,
                        Job: job.JobId, Count: limit));
                }
            }
            var view = View(host); var colony = view.Development!.Colonies.Single();
            M3ConstructionCatalogView? Available() => colony.Catalog.FirstOrDefault(c => c.Eligibility == "available" && c.CanAfford == true && c.SlotIds.Count > 0 && c.Quote is not null);
            var item = Available();
            Accept(host, new("", "speed", Speed: 4)); Accept(host, new("", "pause", Paused: false));
            for (var month = 0; item is null && month < 120; month++) {
                for (var day = 0; day < 30; day++) { now += TimeSpan.FromSeconds(.25); session.Tick(now); }
                view = View(host); colony = view.Development!.Colonies.Single(); item = Available();
                Check(view.LastError is null, "Source funding tick failed: " + view.LastError);
            }
            Accept(host, new("", "pause", Paused: true));
            Check(item is not null, "No affordable source construction within120months: " + string.Join(";", colony.Catalog.Where(c => c.Kind == "district").Select(c => c.DefinitionId + "=" + string.Join(",", c.Gates.Select(g => g.DependencyId)))));
            var before = view.Planets.Single().Balances;
            Accept(host, new("", "enqueueConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
                DefinitionId: item!.DefinitionId, SlotId: item.SlotIds.First(), OrderId: "genuine-source-order"));
            view = View(host); colony = view.Development!.Colonies.Single();
            var order = colony.Queue.Single(q => q.OrderId == "genuine-source-order");
            CheckFullestBudget("active-research-funded-month-pending-construction");
            Check(order.PaidCosts.Count > 0 && order.PaidCosts.All(p => before[p.Key] - view.Planets.Single().Balances[p.Key] == p.Value), "Source quote was not paid.");
            Console.WriteLine("Genuine paid source quote " + item.DefinitionId + ": " + JsonSerializer.Serialize(order.PaidCosts));
            Accept(host, new("", "cancelConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision, OrderId: order.OrderId));
            Check(View(host).Development!.Colonies.Single().Queue.Single(q => q.OrderId == order.OrderId).Status == "cancelled", "Source cancellation left pending order.");
            colony = View(host).Development!.Colonies.Single();
            Accept(host, new("", "enqueueConstruction", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
                DefinitionId: item.DefinitionId, SlotId: item.SlotIds.First(), OrderId: "genuine-source-completion"));
            Accept(host, new("", "pause", Paused: false));
            for (var day = 0; day < decimal.Ceiling(item.Quote!.DurationDays) + 2; day++) {
                now += TimeSpan.FromSeconds(.25); session.Tick(now);
                Check(session.ObserveTiming().ElapsedDay > view.ElapsedDay + day, "Source construction day rejected.");
            }
            Accept(host, new("", "pause", Paused: true));
            Check(View(host).Development!.Colonies.Single().Queue.Single(q => q.OrderId == "genuine-source-completion").Status == "completed", "Source construction did not complete.");
            Console.WriteLine("Genuine construction completed " + item.DefinitionId);
        });
        Test("genuine research completion через source science/reserve и обычные ticks", () => {
            var view = View(host); var active = view.Research!.Empires.Single().Areas.First(a => a.ActiveTechnologyId is not null || a.LastCompletedTechnologyId is not null);
            var tech = active.ActiveTechnologyId ?? active.LastCompletedTechnologyId!;
            Console.WriteLine($"Genuine research {tech}: monthly={active.MonthlyResearch}, reserve={active.Reserve}, progress={active.Progress}");
            Check(active.MonthlyResearch > 0, "No genuine source science.");
            Accept(host, new("", "speed", Speed: 4)); Accept(host, new("", "pause", Paused: false));
            var completed = view.Research.Empires.Single().Researched.Any(t => t.Id == tech);
            for (var month = 0; !completed && month < 720; month++) {
                for (var day = 0; day < 30; day++) { now += TimeSpan.FromSeconds(.25); session.Tick(now); }
                view = View(host);
                Check(view.LastError is null, "Daily transaction gate: " + view.LastError);
                if (view.Research!.Empires.Single().Researched.Any(t => t.Id == tech)) { completed = true; break; }
            }
            Accept(host, new("", "pause", Paused: true));
            Check(completed, "Source research did not complete within720months; no fake unlock allowed.");
            Console.WriteLine($"Genuine research completed {tech}: day={view.ElapsedDay}, month={view.Month}");
            CheckFullestBudget("genuine-research-and-construction-completed");
            var upgradeOptions = view.Development!.Colonies.Single().Installed.SelectMany(i => i.Upgrades ?? []).ToArray();
            var available = upgradeOptions.FirstOrDefault(u => u.CanEnqueue);
            if (available is null) {
                Console.WriteLine("PENDING genuine paid upgrade: " + string.Join("; ", upgradeOptions.Select(u => u.DefinitionId + "=" + string.Join(",", u.Gates.Select(g => g.DependencyId)))));
            } else {
                var colony = view.Development.Colonies.Single(); var balances = view.Planets.Single().Balances;
                Accept(host, new("", "enqueueUpgrade", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision,
                    SourceDefinitionId: available.SourceDefinitionId, DefinitionId: available.DefinitionId, SlotId: available.SlotId,
                    ZoneInstanceId: available.ZoneInstanceId, OrderId: "genuine-source-upgrade"));
                var after = View(host);
                Check(after.Development!.Colonies.Single().Queue.Single(q => q.OrderId == "genuine-source-upgrade").PaidCosts.All(p => balances[p.Key] - after.Planets.Single().Balances[p.Key] == p.Value), "Source upgrade quote unpaid.");
                Console.WriteLine("Genuine paid upgrade " + available.DefinitionId);
            }
        });
        Test("huge maxdensity finite64history+pending1 каждого owner сохраняет все orders", () => {
            CheckHistoryBudget("huge-density3-research-completed-owner1-pending1-history64");
            CheckHistoryBudget("huge-density3-research-completed-both-pending1-history64", peer);
        });
        Test("small density1 all5POVs с source-funded queue/research/history64", () => {
            var saved = (session, host, peer, observer, now);
            try {
                session = NewSession();
                host = session.Join("Хост", "player", null, now); peer = session.Join("Игрок", "player", null, now); observer = session.Join("Наблюдатель", "observer", null, now);
                Accept(host, new("", "selectEmpireTemplate", TemplateId: "economic")); Accept(peer, new("", "selectEmpireTemplate", TemplateId: "scientific"));
                Accept(host, Settings(settings with { ScenarioId = "small", Density = 1 })); Generate(); AckReady();
                Accept(host, new("", "approveObserver", TargetParticipantId: observer.ParticipantId)); Accept(observer, Bound("assetReady", true)); Accept(host, Bound("start"));
                CheckFullestBudget("small-density1-start");
                var view = View(host);
                var area = view.Research!.Empires.Single().Areas.Where(a => a.Choices.Count > 0).OrderBy(a => a.Choices.Min(t => t.Cost ?? decimal.MaxValue)).First();
                Accept(host, new("", "selectResearch", Area: area.Id, TechnologyId: area.Choices.OrderBy(t => t.Cost ?? decimal.MaxValue).First().Id, ExpectedResearchRevision: view.Research.Empires.Single().Revision));
                foreach (var player in new[] { host, peer }) {
                    var activeArea = View(player).Research!.Empires.Single().Areas.FirstOrDefault(a => a.ActiveTechnologyId is not null)?.Id;
                    var scienceJob = activeArea switch { "physics" => "physicist", "engineering" => "engineer", "society" => "biologist", _ => null };
                    foreach (var job in View(player).Development!.Colonies.Single().Economy!.Jobs.Where(j => j.Capacity > 0)) {
                        var colony = View(player).Development!.Colonies.Single();
                        var limit = job.JobId is "miner" or "technician" or "farmer" ? job.Capacity : job.JobId == scienceJob ? job.Capacity : job.JobId == "artisan" ? job.Capacity / 4 : 0;
                        Accept(player, new("", "setWorkforceLimit", ColonyId: colony.ColonyId, ExpectedColonyRevision: colony.Revision, Job: job.JobId, Count: limit));
                    }
                }
                Accept(host, new("", "speed", Speed: 4)); Accept(host, new("", "pause", Paused: false));
                M3ConstructionCatalogView? available = null;
                for (var month = 0; month < 120; month++) {
                    for (var day = 0; day < 30; day++) { now += TimeSpan.FromSeconds(.25); session.Tick(now); }
                    view = View(host); Check(view.LastError is null, "Small source month gate: " + view.LastError);
                    available = view.Development!.Colonies.Single().Catalog.FirstOrDefault(c => c.Kind == "building" && c.Eligibility == "available" && c.CanAfford == true && c.SlotIds.Count > 0 && c.Quote is not null);
                    if (available is not null) break;
                }
                Accept(host, new("", "pause", Paused: true)); Check(available is not null, "Small source profile cannot fund a construction quote within120months.");
                CheckHistoryBudget("small-density1-source-month-active-research-pending1-history64");
                CheckHistoryBudget("small-density1-source-month-active-research-both-pending1-history64", peer);
            } finally { (session, host, peer, observer, now) = saved; }
        });
        return count;
    }
}
