using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Galaxy;

internal static class GalaxyWorldChecks
{
    public static void Run(string root, NativeTriangleBackend backend, Action<string, Action> check)
    {
        var catalog = GalaxyLobbyCatalog.Import(root, new([], []));
        var generator = new GalaxyWorldGenerator(catalog, backend);
        check("guaranteed neighbor source closure, 0/1/2 contexts, ties, availability and hop bounds", () => GalaxyGuaranteedNeighborChecks.Run(catalog.Content));
        GalaxyLobbyRequest Request(string seed = "integration-452", string shape = "elliptical", decimal density = 1, int players = 2,
            string? home = null) => new(seed, "tiny", shape, density, Enumerable.Range(1, players)
                .Select(i => new GalaxyPlayerSlot(i, i == 1 ? "economic" : "scientific", "pc_continental", home)));
        GalaxyWorldSnapshot Generate(GalaxyLobbyRequest request)
        {
            var resolved = catalog.Resolve(request);
            if (!resolved.Success) throw new Exception(resolved.Failure!.ToString());
            var result = generator.Generate(resolved.Request!);
            if (!result.Success) throw new Exception(result.Failure!.ToString());
            var world = result.World!;
            var expectedCount = catalog.Scenarios.Single(s => s.Id == request.ScenarioId).Stars;
            Equal(expectedCount, world.Systems.Count); Equal(request.Players.Count, world.Homes.Count);
            Equal(true, world.Bodies.Count > expectedCount * 2); Equal(true, world.Deferred.Count > 0);
            Equal("ReadyForBootstrap", world.Readiness);
            var ids = world.Systems.Select(s => s.Id).Concat(world.Bodies.Select(b => b.Id)).Concat(world.Lanes.Select(l => l.Id)).ToArray();
            Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
            var systems = world.Systems.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            var bodies = world.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            foreach (var body in world.Bodies)
            {
                Equal(true, systems.Contains(body.SystemId)); Equal(true, body.Size > 0);
                Equal(true, double.IsFinite(body.X) && double.IsFinite(body.Y));
                if (body.ParentId is { } parent)
                {
                    Equal(body.SystemId, bodies[parent].SystemId);
                    var seen = new HashSet<string> { body.Id }; var cursor = parent;
                    while (cursor is not null) { Equal(true, seen.Add(cursor)); cursor = bodies[cursor].ParentId; }
                }
            }
            var reached = new HashSet<string> { world.Systems[0].Id }; var pending = new Queue<string>(); pending.Enqueue(world.Systems[0].Id);
            while (pending.TryDequeue(out var id))
                foreach (var lane in world.Lanes.Where(l => l.A == id || l.B == id))
                { Equal(true, systems.Contains(lane.A) && systems.Contains(lane.B)); var other = lane.A == id ? lane.B : lane.A; if (reached.Add(other)) pending.Enqueue(other); }
            Equal(expectedCount, reached.Count);
            Equal(world.Homes.Count, world.Homes.Select(h => h.SystemId).Distinct().Count());
            foreach (var home in world.Homes)
            { Equal(home.ClassId, bodies[home.BodyId].ClassId); Equal(home.Size, bodies[home.BodyId].Size); Equal(GalaxyBodyKind.Star, bodies[home.StarId].Kind); }
            Console.WriteLine($"World {request.Seed}/{request.ShapeId}/{request.HyperlaneDensity}: systems={world.Systems.Count}, bodies={world.Bodies.Count}, lanes={world.Lanes.Count}, homes={world.Homes.Count}, initializers={world.Systems.Select(s => s.InitializerId).Distinct().Count()}, hash={world.WorldFingerprint}");
            return world;
        }
        check("lobby source options, profile limits and typed request failures", () =>
        {
            Equal(5, catalog.Scenarios.Count); Equal(200, catalog.Scenarios.Single(s => s.Id == "tiny").Stars);
            Equal(4, catalog.Scenarios.Single(s => s.Id == "tiny").Shapes.Count);
            Equal(1, catalog.Scenarios.Single(s => s.Id == "tiny").MarauderDefault);
            Equal("scenario-shape", catalog.Resolve(Request(shape: "spiral_6")).Failure!.Code);
            Equal("density", catalog.Resolve(Request(density: .49m)).Failure!.Code);
            Equal("players", catalog.Resolve(new("x", "tiny", "elliptical", 1, [new(1, "economic", "pc_continental"), new(1, "economic", "pc_continental")])).Failure!.Code);
            Equal("unsupported-special-content", catalog.Resolve(new("x", "tiny", "elliptical", 1, Request().Players, specialContent: new(MarauderEmpires: 1))).Failure!.Code);
            Equal("unsupported-profile", catalog.Resolve(new("x", "tiny", "elliptical", 1, Request().Players, profile: GalaxyContentProfile.VanillaComplete)).Failure!.Code);
            var generated = catalog.Resolve(new(null, "tiny", "elliptical", 1, Request().Players)).Request!;
            Equal(generated.SettingsFingerprint, catalog.Resolve(generated.Settings).Request!.SettingsFingerprint);
            foreach (var emptySeed in new[] { "", "  " })
            {
                var automatic = catalog.Resolve(Request(emptySeed)).Request!;
                Equal(false, string.IsNullOrWhiteSpace(automatic.Seeds.UserSeed));
                Equal(automatic.SettingsFingerprint, catalog.Resolve(automatic.Settings).Request!.SettingsFingerprint);
            }
            var seed = new GalaxySeedBundle("deterministic/галактика");
            Equal(seed.Galaxy, new GalaxySeedBundle(seed.UserSeed).Galaxy);
            // Golden vector independently calculated with Python hashlib/struct, UTF8 and little-endian length prefixes.
            Equal("2142723239|697565560|340804119|1945824746|575747949|2068387942|960220500",
                string.Join("|", new long[] { seed.Galaxy, seed.PruneA, seed.PruneB, seed.Script0, seed.Script1, seed.Names, seed.Content }));
            Equal(7, new long[] { seed.Galaxy, seed.PruneA, seed.PruneB, seed.Script0, seed.Script1, seed.Names, seed.Content }.Distinct().Count());
            var culture = CultureInfo.CurrentCulture;
            try
            {
                var first = catalog.Resolve(Request()).Request!.SettingsFingerprint;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Equal(first, catalog.Resolve(Request()).Request!.SettingsFingerprint);
            }
            finally { CultureInfo.CurrentCulture = culture; }
        });
        GalaxyWorldSnapshot? sample = null;
        check("world all tiny shapes, two seeds, density0.5/1/3, one/two players and deterministic replay", () =>
        {
            sample = Generate(Request()); Equal(sample.WorldFingerprint, Generate(Request()).WorldFingerprint);
            Equal(true, sample.Systems.Select(s => s.InitializerId).Distinct().Count() >= 8);
            foreach (var shape in catalog.Scenarios.Single(s => s.Id == "tiny").Shapes.Where(s => s != "elliptical")) Generate(Request(shape: shape));
            foreach (var density in new[] { .5m, 3m }) Generate(Request("second-seed", density: density, players: 1));
            var nested = Generate(Request("nested-home", players: 1, home: "random_empire_init_03"));
            Equal(true, nested.Bodies.Single(b => b.Id == nested.Homes[0].BodyId).ParentId is not null);
            Generate(new("huge-smoke", "huge", "spiral_4", 1, Request(players: 1).Players));
        });
        check("genuine nebula source dimensions, seeded replay, membership and zero stream regression", () =>
        {
            Equal(2, catalog.Scenarios.Single(s => s.Id == "tiny").NebulaeDefault);
            var fields = catalog.Geometry.Scenarios.Single(s => s.Id == "tiny").Fields;
            Equal("60", fields.Required("nebula_size").AsScalar()); Equal("100", fields.Required("nebula_min_dist").AsScalar());
            Equal(true, catalog.NebulaNamesFile is not null);
            var ordinary = Request();
            var active = new GalaxyLobbyRequest(ordinary.Seed, ordinary.ScenarioId, ordinary.ShapeId, ordinary.HyperlaneDensity,
                ordinary.Players, specialContent: new(Nebulae: 2));
            var first = Generate(active); var second = Generate(active);
            Equal(first.WorldFingerprint, second.WorldFingerprint); Equal(2, first.Nebulae.Nebulae.Count);
            Equal(0, first.Nebulae.Diagnostics.Count); Equal(2L, first.Nebulae.NameDraws);
            Equal(2, first.Nebulae.Nebulae.Select(n => n.NameKey).Distinct().Count());
            Equal(true, first.Nebulae.Nebulae.All(n => n.Radius == 6000000));
            var centers = first.Nebulae.Nebulae.Select(n => n.Center).ToArray();
            var x = (System.Numerics.BigInteger)centers[0].X - centers[1].X;
            var y = (System.Numerics.BigInteger)centers[0].Y - centers[1].Y;
            Equal(true, x * x + y * y >= (System.Numerics.BigInteger)10000000 * 10000000);
            GalaxyNebulaChecks.VerifyWorld(first);
            Equal(first.Nebulae.PlacementDraws, first.Stages.Single(s => s.Stage == "nebula-placement").Count);
            Equal(first.Nebulae.NameDraws, first.Stages.Single(s => s.Stage == "nebula-names").Count);
            var zero = Generate(new(ordinary.Seed, ordinary.ScenarioId, ordinary.ShapeId, ordinary.HyperlaneDensity,
                ordinary.Players, specialContent: new(Nebulae: 0)));
            Equal(sample!.WorldFingerprint, zero.WorldFingerprint);
            Equal(System.Text.Json.JsonSerializer.Serialize(sample.Systems), System.Text.Json.JsonSerializer.Serialize(zero.Systems));
            Equal(System.Text.Json.JsonSerializer.Serialize(sample.Bodies), System.Text.Json.JsonSerializer.Serialize(zero.Bodies));
            Equal(true, sample.Lanes.SequenceEqual(zero.Lanes)); Equal(true, sample.Homes.SequenceEqual(zero.Homes));
            Equal(0L, zero.Nebulae.PlacementDraws); Equal(0L, zero.Nebulae.NameDraws);
            Equal(NativeRandom.FromSpecifiedSeed(zero.Request.Seeds.Content), zero.Stages.Single(s => s.Stage == "nebula-placement").NextState!.Value);
            Equal(NativeRandom.FromSpecifiedSeed(zero.Request.Seeds.Names), zero.Stages.Single(s => s.Stage == "nebula-names").NextState!.Value);
        });
        check("generated home exact class/size maps to existing planet startup importer without reselection", () =>
        {
            foreach (var home in sample!.Homes)
            {
                var template = ConstructionOrdinaryTemplate.Get(home.StartupProfileId);
                var facts = new Dictionary<string, bool?> {
                    ["is_apocalyptic_empire"] = false, ["is_environmental_architects_empire"] = false,
                    ["is_lithoid_empire"] = false, ["is_lithoid_or_infernal_empire"] = false, ["is_lithoid_devouring_swarm"] = false,
                    ["has_carrier_flag:planet_unity"] = false, ["has_carrier_flag:uncapped_energy"] = false,
                    ["has_carrier_flag:wilderness_planet"] = false, ["has_carrier_flag:uncapped_mining"] = false, ["has_carrier_flag:uncapped_farming"] = false };
                var context = new StartupOwnerContext(StartupAuthority.Regular, template.Ethics, template.Civics, template.OriginId,
                    home.Size, planetClass: home.ClassId, countryUsesFoodGameStart: true, blockerChoiceSeed: sample.Request.Seeds.Script0, sourcePredicateFacts: facts);
                var plan = new StartupBuildingImporter().GenerateAsync(root, context).GetAwaiter().GetResult();
                Equal(home.Size, plan.Context.PlanetSize); Equal(home.ClassId, plan.Context.PlanetClass);
                Equal(true, plan.Districts.Count > 0 && plan.Buildings.Count > 0 && plan.Zones.Count > 0);
                Equal(0, plan.Gates.Count);
                Console.WriteLine($"Bootstrap seam {home.ParticipantId}: {home.InitializerId}/{home.ClassId}/{home.Size} -> finalSize={plan.FinalPlanetSize}, districts={plan.Districts.Count}, buildings={plan.Buildings.Count}, zones={plan.Zones.Count}");
            }
        });
        check("consumer validation rejects stale fingerprints, empty economy, dangling actors and undisposed content", () =>
        {
            // Synthetic protocol fixture, не runtime запуск и не доказательство играбельности M3.
            var world = sample!; var entities = new List<GalaxyConsumerEntity>(); var bindings = new List<GalaxyConsumerBinding>();
            foreach (var home in world.Homes)
            {
                var prefix = "fixture/" + home.ParticipantId; var country = prefix + "/country"; var species = prefix + "/species";
                var colony = prefix + "/colony"; var economy = prefix + "/economy"; var district = prefix + "/district"; var fleet = prefix + "/fleet";
                bindings.Add(new(home.ParticipantId, country, species, colony, economy));
                entities.AddRange([
                    new(country, GalaxyConsumerEntityKind.Country, SpeciesId: species, ColonyId: colony, ParticipantId: home.ParticipantId, DefinitionId: home.StartupProfileId),
                    new(species, GalaxyConsumerEntityKind.Species, CountryId: country),
                    new(colony, GalaxyConsumerEntityKind.Colony, CountryId: country, SpeciesId: species, EconomyPlanetId: economy, SystemId: home.SystemId,
                        BodyId: home.BodyId, PlanetClass: home.ClassId, InitialSize: home.Size, FinalSize: home.Size),
                    new(economy, GalaxyConsumerEntityKind.EconomyPlanet, CountryId: country, ColonyId: colony, Amount: 1),
                    new(district, GalaxyConsumerEntityKind.District, CountryId: country, ParentId: economy, DefinitionId: "fixture-district", Amount: 1),
                    new(prefix + "/building", GalaxyConsumerEntityKind.Building, CountryId: country, ParentId: economy, DefinitionId: "fixture-building"),
                    new(prefix + "/zone", GalaxyConsumerEntityKind.Zone, CountryId: country, ParentId: district, DefinitionId: "fixture-zone"),
                    new(fleet, GalaxyConsumerEntityKind.Fleet, CountryId: country, SystemId: home.SystemId),
                    new(prefix + "/ship", GalaxyConsumerEntityKind.Ship, CountryId: country, FleetId: fleet, SystemId: home.SystemId, DefinitionId: "fixture-design", Amount: 3)]);
            }
            var dispositions = world.Deferred.Select(d => new GalaxyDeferredDisposition(d.Id, GalaxyDeferredDispositionKind.AcceptedApproximation,
                GalaxyBootstrapValidation.PrototypeOmissionPolicy, "synthetic validation fixture; no runtime claim")).ToArray();
            GalaxyConsumerSnapshot Snapshot(IEnumerable<GalaxyConsumerEntity>? data = null, string? hash = null,
                IEnumerable<GalaxyDeferredDisposition>? requests = null) => new(hash ?? world.WorldFingerprint,
                    world.Request.SettingsFingerprint, world.Request.ContentFingerprint, data ?? entities, bindings,
                    requests ?? dispositions, ["fixture-district", "fixture-building", "fixture-zone", "fixture-design"], GalaxyBootstrapValidation.NavigationActorPolicy);
            Equal(true, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot()).Accepted);
            Equal(false, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot(hash: "stale")).Accepted);
            Equal(false, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot(entities.Select(e => e.Kind == GalaxyConsumerEntityKind.EconomyPlanet ? e with { Amount = 0 } : e))).Accepted);
            Equal(false, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot(entities.Select(e => e.Kind == GalaxyConsumerEntityKind.Ship ? e with { FleetId = "missing" } : e))).Accepted);
            var foreignBody = world.Bodies.First(b => b.SystemId != world.Homes[0].SystemId);
            var extra = new GalaxyConsumerEntity("extra-colony", GalaxyConsumerEntityKind.Colony, CountryId: bindings[0].CountryId,
                BodyId: foreignBody.Id, SystemId: world.Homes[0].SystemId);
            Equal(true, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot(entities.Append(extra))).Errors.Any(e => e.StartsWith("body-system-mismatch:", StringComparison.Ordinal)));
            Equal(false, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot(requests: dispositions.Skip(1))).Accepted);
            Equal(false, GalaxyBootstrapValidation.ValidateForPlay(world, Snapshot(requests: dispositions.Select(d => d with { Kind = GalaxyDeferredDispositionKind.Unsupported }))).Accepted);
        });
    }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}"); }
}
