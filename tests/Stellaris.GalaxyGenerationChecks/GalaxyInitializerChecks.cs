using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Galaxy;

internal static class GalaxyInitializerChecks
{
    public static GalaxyInitializerContext Context() => new(
        GalaxyInitializerDefaults.VanillaDescriptor("sc_test", (_, _) => GalaxyNumericRange.Constant(10), 0, 0),
        "pc_test", "pc_test", 1, d => !d.Syntax.Value.Entries.Any(e => e.Key == "potential"), (_, _) => true);

    public static void Run(Action<string, Action> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "initializer-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Write(string path, string text)
            {
                var full = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, text);
            }
            Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
            Write("common/star_classes/a.txt", "sc_test = { planet = { key = pc_star } spawn_odds = 1 } sc_two = { planet = { key = pc_star } planet = { key = pc_second_star } spawn_odds = 1 } rl_test = { stars = { sc_test } }");
            Write("common/planet_classes/a.txt", "pc_test = { planet_size = 12 moon_size = 4 colonizable = yes min_distance_from_sun = 0 max_distance_from_sun = 1000 spawn_odds = 10 } pc_star = { planet_size = 20 moon_size = 2 spawn_odds = 0 } pc_second_star = { planet_size = 30 spawn_odds = 0 } random_list = { name = rl_planets planets = { pc_test } }");
            Write("common/scripted_variables/a.txt", "@global = 8 @a = @b @b = @a");
            Write("common/inline_scripts/structure.txt", "class = sc_test planet = { class = pc_test size = $SIZE$ [[HOME] starting_planet = yes ] [[!HOME] flags = { ordinary } ] } init_effect = { marker = $SIZE$ }");
            Write("common/inline_scripts/cycle.txt", "inline_script = cycle");
            Write("common/solar_system_initializers/a.txt", """
                @local = 3
                ordering = {
                    class = sc_test flags = { synthetic } name = NAME_Test
                    planet = { class = star count = 1 orbit_distance = 0 orbit_angle = 1 size = 20 }
                    change_orbit = 10
                    planet = {
                        class = pc_test orbit_distance = 5 orbit_angle = 6 starting_planet = yes
                        modifiers = none deposit_blockers = none anomaly = none init_effect = { marker = yes }
                        moon = { class = pc_test orbit_distance = 2 orbit_angle = 3 }
                        change_orbit = 7
                        planet = { class = pc_test orbit_distance = 4 orbit_angle = 5 }
                        moon = { class = pc_test orbit_distance = 6 orbit_angle = 7 }
                        orbital_line = { orbit_distance_from_parent = 30 }
                    }
                    asteroid_belt = { type = test_belt radius = 40 }
                    orbital_line = { orbit_distance_from_parent = 60 }
                    neighbor_system = { initializer = ordering }
                    init_effect = { system_marker = yes }
                }
                missing = { class = sc_test planet = {} planet = { class = none } }
                multi = { class = sc_two planet = { class = star count = 2 } planet = { class = star planet = { class = star } } }
                template = { inline_script = { script = structure SIZE = 17 HOME = yes } }
                cycle = { inline_script = cycle }
                unknown = { novel_structural_field = 1 }
                negative = { class = sc_test planet = { class = pc_test orbit_angle = { min = 10 max = -10 } } }
                ranges = { class = sc_test planet = { class = pc_test count = { min = 2 } size = { min = 9 max = 4 } orbit_distance = 100 orbit_distance = 8 } }
                oversized_size = { class = sc_test planet = { class = pc_test size = 2147483648 } }
                oversized_descending_size = { class = sc_test planet = { class = pc_test size = { min = 2147483648 max = 0 } } }
                negative_count = { class = sc_test planet = { class = pc_test count = -1 } planet = { class = pc_test count = 0 } }
                """);
            var catalog = GalaxyRuntimeCatalog.Import(root, new GalaxyEntitlements([], []));
            check("initializer numeric arithmetic, local/global variables, cycles and unsafe expressions", () =>
            {
                var numeric = new GalaxyNumericValues(catalog);
                decimal Read(string value) => numeric.Evaluate(GalaxyScriptReader.Parse("x = " + value, "common/solar_system_initializers/a.txt").Entries.Single().Value);
                Equal(17m, Read("@[(@local + global) * 2 - 10 / 2]"));
                Equal(-4m, Read("-4"));
                Throws("numeric-cycle", () => Read("@a"));
                Throws("numeric-arithmetic", () => Read("@[1 / 0]"));
                Throws("numeric-expression", () => Read("@[1; 2]"));
                Throws("numeric-variable", () => Read("@missing"));
            });
            check("initializer source-order hierarchy, gaps, home aliases, content deferral and RNG trace", () =>
            {
                var compiled = GalaxyInitializerExecution.Compile(catalog, "ordering");
                var seed = NativeRandom.FromSpecifiedSeed(17);
                var result = GalaxyInitializerExecution.Execute(catalog, compiled, Context(), seed);
                Equal(5, result.Bodies.Count); Equal(1, result.HomeCandidates.Count); Equal(1, result.HomeCandidates[0]);
                Equal(15m, result.Bodies[1].OrbitDistance);
                Equal(GalaxyBodyKind.Moon, result.Bodies[2].Kind); Equal(2m, result.Bodies[2].OrbitDistance);
                Equal(GalaxyBodyKind.Planet, result.Bodies[3].Kind); Equal(13m, result.Bodies[3].OrbitDistance);
                Equal(GalaxyBodyKind.Moon, result.Bodies[4].Kind); Equal(19m, result.Bodies[4].OrbitDistance);
                Equal(4, result.Bodies[2].Size); Equal(12, result.Bodies[3].Size);
                Equal(1, result.Bodies[2].ParentId); Equal(1, result.Bodies[3].ParentId); Equal(1, result.Bodies[4].ParentId);
                Equal(2, result.OrbitalLines.Count); Equal(1, result.Belts.Count); Equal(1, result.Neighbors.Count);
                Equal(false, result.GameplayComplete);
                Equal("count", result.Draws[0].Purpose); Equal("orbit-distance", result.Draws[1].Purpose);
                Equal("size", result.Draws[2].Purpose); Equal("orbit-angle", result.Draws[3].Purpose);
                Equal(2, result.Draws.Count(d => d.Purpose == "orbit-distance" && d.Source == compiled.Descriptors[1].Source.Location || d.Purpose == "count" && d.Source == compiled.Descriptors[1].Source.Location));
                Equal(true, result.Deferred.Last().Phase == "system-init_effect");
                var phases = result.Deferred.Where(d => d.BodyId == 1).Select(d => d.Phase).ToArray();
                Equal(true, Array.IndexOf(phases, "modifiers") < Array.IndexOf(phases, "random-deposits-policy"));
                Equal(true, Array.IndexOf(phases, "anomaly") < Array.IndexOf(phases, "init_effect"));
                var repeat = GalaxyInitializerExecution.Execute(catalog, compiled, Context(), seed);
                Equal(string.Join("|", result.Draws), string.Join("|", repeat.Draws)); Equal(result.NextState, repeat.NextState);
            });
            check("initializer missing class differs from none and native star descriptor ordinals", () =>
            {
                var missing = GalaxyInitializerExecution.Compile(catalog, "missing");
                Equal(GalaxyDescriptorClassKind.Missing, missing.Descriptors[0].ClassKind);
                Equal(GalaxyDescriptorClassKind.Explicit, missing.Descriptors[1].ClassKind);
                var result = GalaxyInitializerExecution.Execute(catalog, missing, Context(), NativeRandom.FromSpecifiedSeed(9));
                Equal(1, result.Bodies.Count); Equal("pc_test", result.Bodies[0].ClassId);
                Equal(true, result.Bodies[0].OrbitDistance >= 10 && result.Bodies[0].OrbitDistance < 20);
                var multi = GalaxyInitializerExecution.Compile(catalog, "multi");
                Equal(0, multi.Descriptors[0].StarOrdinal); Equal(1, multi.Descriptors[1].StarOrdinal);
                Equal(-1, multi.Descriptors[1].Children[0].StarOrdinal);
                var stars = GalaxyInitializerExecution.Execute(catalog, multi, Context(), NativeRandom.FromSpecifiedSeed(1));
                Equal(3, stars.Bodies.Count); Equal("pc_star", stars.Bodies[0].ClassId);
                Equal("pc_star", stars.Bodies[1].ClassId); Equal("pc_second_star", stars.Bodies[2].ClassId);
            });
            check("initializer template bindings/conditional expansion, signed fixed spans and typed errors", () =>
            {
                var compiled = GalaxyInitializerExecution.Compile(catalog, "template");
                var result = GalaxyInitializerExecution.Execute(catalog, compiled, Context(), NativeRandom.FromSpecifiedSeed(2));
                Equal(17, result.Bodies.Single().Size); Equal(1, result.HomeCandidates.Count);
                Equal(0, result.Bodies.Single().Flags.Count);
                var negative = GalaxyInitializerExecution.Execute(catalog, GalaxyInitializerExecution.Compile(catalog, "negative"), Context(), NativeRandom.FromSpecifiedSeed(2));
                Equal(true, negative.Bodies.Single().OrbitAngle <= 10 && negative.Bodies.Single().OrbitAngle > -10);
                var ranges = GalaxyInitializerExecution.Execute(catalog, GalaxyInitializerExecution.Compile(catalog, "ranges"), Context(), NativeRandom.FromSpecifiedSeed(2));
                Equal(true, ranges.Bodies.Count <= 2);
                Equal(true, ranges.Bodies.All(b => b.Size >= 4 && b.Size <= 9));
                if (ranges.Bodies.Count > 0) Equal(8m, ranges.Bodies[0].OrbitDistance);
                Throws("inline-cycle", () => GalaxyInitializerExecution.Compile(catalog, "cycle"));
                Throws("structural-field", () => GalaxyInitializerExecution.Compile(catalog, "unknown"));
            });
            check("initializer int32 endpoints are checked in both directions and nonpositive count skips creation", () =>
            {
                var missing = GalaxyInitializerExecution.Compile(catalog, "missing");
                foreach (var range in new[] {
                    new GalaxyNumericRange(0, 2147483648m), new GalaxyNumericRange(2147483648m, 0),
                    new GalaxyNumericRange(-2147483649m, 0), new GalaxyNumericRange(0, -2147483649m),
                    new GalaxyNumericRange(2147483648m, 2147483648m), new GalaxyNumericRange(-2147483649m, -2147483649m) })
                {
                    var context = Context();
                    context = context with { Defaults = context.Defaults with { Count = range } };
                    Throws("integer-range", () => GalaxyInitializerExecution.Execute(catalog, missing, context, NativeRandom.FromSpecifiedSeed(2)));
                }
                foreach (var id in new[] { "oversized_size", "oversized_descending_size" })
                    Throws("integer-range", () => GalaxyInitializerExecution.Execute(catalog, GalaxyInitializerExecution.Compile(catalog, id), Context(), NativeRandom.FromSpecifiedSeed(2)));
                var negative = GalaxyInitializerExecution.Execute(catalog, GalaxyInitializerExecution.Compile(catalog, "negative_count"), Context(), NativeRandom.FromSpecifiedSeed(2));
                Equal(0, negative.Bodies.Count); Equal(2, negative.Draws.Count);
                Equal(true, negative.Draws.All(d => d.Purpose == "count"));
            });
        }
        finally { Directory.Delete(root, true); }
    }

    public static void RunVanilla(GalaxyRuntimeCatalog catalog, Action<string, Action> check)
    {
        check("initializer all 360 vanilla definitions compile", () =>
        {
            var compiled = 0; var gaps = new List<string>();
            foreach (var definition in catalog.Initializers)
            {
                try { GalaxyInitializerExecution.Compile(catalog, definition.Id); compiled++; }
                catch (GalaxyStructureException error) { gaps.Add(definition.Id + ": " + error.Message); }
            }
            Console.WriteLine($"Initializer compilation: {compiled}/{catalog.Initializers.Count}; gaps={gaps.Count}");
            foreach (var gap in gaps) Console.WriteLine("STRUCTURAL GAP " + gap);
            Equal(360, compiled + gaps.Count);
            Equal(0, gaps.Count);
        });
        check("initializer genuine ordinary, home, multi-star and top-level inline structures execute", () =>
        {
            var context = Context() with { IdealPlanetClass = "pc_continental", IdealDesignClass = "pc_continental" };
            var ordinary = Enumerable.Range(1, 6).Select(i => $"basic_init_{i:00}")
                .Concat(["asteroid_init_01", "binary_init_01", "binary_init_02", "trinary_init_01", "trinary_init_02"]);
            var homes = Enumerable.Range(1, 6).Select(i => $"random_empire_init_{i:00}");
            foreach (var id in ordinary.Concat(homes).Append("voidworms_spawn_system_small"))
            {
                var result = GalaxyInitializerExecution.Execute(catalog, GalaxyInitializerExecution.Compile(catalog, id), context, NativeRandom.FromSpecifiedSeed(13));
                Equal(true, result.Bodies.Count > 1);
                Equal(true, result.Bodies.All(b => b.ParentId is null || b.ParentId < b.Id));
                if (id.StartsWith("random_empire_init", StringComparison.Ordinal)) Equal(true, result.HomeCandidates.Count > 0);
                if (id.StartsWith("binary_init", StringComparison.Ordinal)) Equal(true, result.Bodies.Count(b => b.Kind == GalaxyBodyKind.Star) >= 2);
                if (id.StartsWith("trinary_init", StringComparison.Ordinal)) Equal(true, result.Bodies.Count(b => b.Kind == GalaxyBodyKind.Star) >= 3);
                Console.WriteLine($"Initializer {id}: bodies={result.Bodies.Count}, homes={result.HomeCandidates.Count}, deferred={result.Deferred.Count}, draws={result.Draws.Count}");
            }
        });
    }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}"); }
    private static void Throws(string code, Action action)
    {
        try { action(); } catch (GalaxyStructureException error) when (error.Code == code) { return; }
        throw new Exception("Ожидался typed error " + code);
    }
}
