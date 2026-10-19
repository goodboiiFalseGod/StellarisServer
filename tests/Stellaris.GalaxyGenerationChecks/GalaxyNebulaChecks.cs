using System.Numerics;
using System.Text.Json;
using Stellaris.Core.Galaxy;

internal static class GalaxyNebulaChecks
{
    public static void Run(Action<string, Action> check)
    {
        GalaxyNebulaSite[] sites = [new("a", new(0, 0)), new("b", new(10, 0)), new("c", new(20, 0)), new("d", new(9, 0))];
        check("nebula strict radius, inclusive center separation, overlap ownership and swap names", () =>
        {
            var draws = new Queue<uint>([0, 1, 2]);
            var names = new List<string> { "one", "two", "three" };
            var result = GalaxyNebulaGeneration.Generate(sites, 3, 11, 10, names, () => draws.Dequeue(), () => 0);
            Equal("one|three|two", string.Join("|", result.Nebulae.Select(n => n.NameKey)));
            Equal("a|b|d", string.Join("|", result.Nebulae[0].SystemIds));
            Equal("c", string.Join("|", result.Nebulae[1].SystemIds));
            Equal(0, result.Nebulae[2].SystemIds.Count); Equal(3L, result.PlacementDraws); Equal(3L, result.NameDraws);
            Equal(3, names.Count); names.Clear(); Equal("one", result.Nebulae[0].NameKey);
            Throws<NotSupportedException>(() => ((IList<string>)result.Nebulae[0].SystemIds).Add("foreign"));
            Throws<NotSupportedException>(() => ((IList<GalaxyNebula>)result.Nebulae).Clear());
            var boundary = GalaxyNebulaGeneration.Generate(sites, 1, 10, 0, [], () => 0, () => throw new Exception("Пустой pool не тратит draw"));
            Equal("a|d", string.Join("|", boundary.Nebulae[0].SystemIds));
            Equal("", boundary.Nebulae[0].NameKey); Equal("name-pool-exhausted", boundary.Diagnostics.Single().Code);
            Equal(0L, boundary.NameDraws);
        });
        check("nebula retry budget tests candidate10001 before abort, accepts candidate10000", () =>
        {
            var draws = 0;
            var result = GalaxyNebulaGeneration.Generate(sites, 3, 1, 10, ["one", "two"],
                () => ++draws == 10002 ? 2u : 0u, () => 0);
            Equal(1, result.Nebulae.Count); Equal(10002, draws); Equal(10002L, result.PlacementDraws);
            Equal("placement-budget-exhausted", result.Diagnostics.Single().Code); Equal(1, result.Diagnostics.Single().Ordinal);
            draws = 0;
            result = GalaxyNebulaGeneration.Generate(sites, 2, 1, 10, ["one", "two"],
                () => ++draws == 10001 ? 2u : 0u, () => 0);
            Equal(2, result.Nebulae.Count); Equal(10001L, result.PlacementDraws); Equal(0, result.Diagnostics.Count);
            var singleton = GalaxyNebulaGeneration.Generate([sites[0]], 2, 1, 1, ["one"], () => 0, () => 0);
            Equal(1, singleton.Nebulae.Count); Equal(10002L, singleton.PlacementDraws);
        });
        check("nebula zero draws, invalid inputs and full long coordinate arithmetic", () =>
        {
            var zero = GalaxyNebulaGeneration.Generate([], 0, 0, 0, [], () => throw new Exception("draw"), () => throw new Exception("draw"));
            Equal(0L, zero.PlacementDraws); Equal(0L, zero.NameDraws); Equal(0, zero.Nebulae.Count);
            foreach (var values in new[] { (-1, 1L, 0L), (1, 0L, 0L), (1, 1L, -1L) })
                Throws<ArgumentOutOfRangeException>(() => GalaxyNebulaGeneration.Generate(sites, values.Item1, values.Item2, values.Item3, [], () => 0, () => 0));
            Throws<ArgumentOutOfRangeException>(() => GalaxyNebulaGeneration.Generate([], 1, 1, 0, [], () => 0, () => 0));
            Throws<ArgumentException>(() => GalaxyNebulaGeneration.Generate([sites[0], sites[0]], 1, 1, 0, [], () => 0, () => 0));
            Throws<ArgumentException>(() => GalaxyNebulaGeneration.Generate(sites, 1, 1, 0, [""], () => 0, () => 0));
            var extreme = GalaxyNebulaGeneration.Generate([new("min", new(long.MinValue, 0)), new("max", new(long.MaxValue, 0))],
                1, long.MaxValue, long.MaxValue, [], () => 0, () => 0);
            Equal("min", string.Join("|", extreme.Nebulae[0].SystemIds));
        });
        RunSources(check);
    }
    private static void RunSources(Action<string, Action> check)
    {
        check("nebula optional source fingerprint, stability, invalid pool and scenario failures", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "nebula-source-check-" + Guid.NewGuid().ToString("N"));
            void Write(string path, string text) { var target = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, text); }
            const string scenario = "setup_scenario = { name = tiny priority = 1 num_stars = 20 radius = 20 supports_shape = round num_empires = { min = 1 max = 2 } num_empire_default = 1 num_hyperlanes = { min = .5 max = 3 } num_hyperlanes_default = 1 num_gateways = { min = 0 max = 5 } num_gateways_default = 0 num_wormhole_pairs = { min = 0 max = 5 } num_wormhole_pairs_default = 0 fallen_empire_default = 0 marauder_empire_default = 0 nomad_empire_default = 0 advanced_empire_default = 0 num_nebulas = 2 nebula_size = 6 nebula_min_dist = 10 }";
            const string namesPath = "common/random_names/base/00_random_names.txt";
            GalaxyLobbyCatalog Import() => GalaxyLobbyCatalog.Import(root, new([], []));
            GalaxyLobbyRequest Request(int count) => new("source", "tiny", "round", 1, [new(1, "test", "pc_test")], specialContent: new(Nebulae: count));
            try
            {
                Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}"); Write("map/setup_scenarios/test.txt", scenario);
                Write("map/galaxy/test.txt", "round = { stars_min_dist = 1 }");
                Write("common/planet_classes/test.txt", "pc_test = { initial = yes }");
                Write("common/solar_system_initializers/test.txt", "test = {}"); Write("common/star_classes/test.txt", "sc_test = {}");
                Write("common/defines/00_defines.txt", "GALAXY_GENERATION_CENTER_DUMMY_POINTS = 0 GALAXY_GENERATION_RELAX_ITERATIONS = 0 HYPERLANE_GEN_REMOVE_PERC = .15 HYPERLANE_GEN_REMOVE_TOO_MANY_EDGES = 4");
                var absent = Import(); Equal(true, absent.Resolve(Request(0)).Success); Equal("invalid-nebula-source", absent.Resolve(Request(1)).Failure!.Code);
                Equal("nebula-count", absent.Resolve(Request(-1)).Failure!.Code); Equal("nebula-count", absent.Resolve(Request(3)).Failure!.Code);
                Write(namesPath, "nebula_names = { synthetic_a synthetic_b }"); Throws<IOException>(absent.VerifyStable);
                var present = Import(); Equal(true, present.Resolve(Request(2)).Success); Equal(64, present.NebulaNamesFile!.Sha256.Length);
                Equal(false, absent.ContentFingerprint == present.ContentFingerprint);
                Write(namesPath, "nebula_names = { synthetic_c }"); Throws<IOException>(present.VerifyStable);
                var changed = Import(); Equal(false, changed.ContentFingerprint == present.ContentFingerprint);
                File.Delete(Path.Combine(root, namesPath)); Throws<IOException>(changed.VerifyStable);
                foreach (var malformed in new[] { "nebula_names = x", "nebula_names = {} nebula_names = {}", "nebula_names = { x = y }", "nebula_names = {", "other = {}" })
                { Write(namesPath, malformed); var invalid = Import(); Equal(true, invalid.Resolve(Request(0)).Success); Equal("invalid-nebula-source", invalid.Resolve(Request(1)).Failure!.Code); }
                Write(namesPath, "nebula_names = {}"); Equal(true, Import().Resolve(Request(1)).Success);
                foreach (var value in new[] { "-1", "0", "0.000001", "999999999999999999999999", "{ value = 1 }", "1 nebula_size = 2" })
                { Write("map/setup_scenarios/test.txt", scenario.Replace("nebula_size = 6", "nebula_size = " + value)); Equal("invalid-nebula-source", Import().Resolve(Request(1)).Failure!.Code); }
                Write("map/setup_scenarios/test.txt", scenario.Replace("nebula_min_dist = 10", "nebula_min_dist = -1")); Equal("invalid-nebula-source", Import().Resolve(Request(1)).Failure!.Code);
                Write("map/setup_scenarios/test.txt", scenario.Replace("num_nebulas = 2", "num_nebulas = 1.5")); Throws<InvalidDataException>(() => Import());
                Write("map/setup_scenarios/test.txt", scenario);
                File.Delete(Path.Combine(root, namesPath));
                try
                {
                    File.CreateSymbolicLink(Path.Combine(root, namesPath), Path.Combine(root, "absent-names.txt"));
                    Throws<IOException>(() => Import());
                    Console.WriteLine("PASS nebula dangling source symlink rejected");
                }
                catch (UnauthorizedAccessException)
                { Console.WriteLine("SKIP nebula symlink fixture: нет права создания symbolic link"); }
                catch (IOException error) when (error.HResult == unchecked((int)0x80070522))
                { Console.WriteLine("SKIP nebula symlink fixture: Windows privilege1314"); }
                finally { File.Delete(Path.Combine(root, namesPath)); }
            }
            finally
            {
                var target = Path.GetFullPath(root);
                var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (Directory.Exists(target) && string.Equals(Path.GetDirectoryName(target), temp, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(target).StartsWith("nebula-source-check-", StringComparison.Ordinal)) Directory.Delete(target, true);
            }
        });
    }
    public static void VerifyWorld(GalaxyWorldSnapshot world)
    {
        var owned = new HashSet<string>(); var systems = world.Systems.ToDictionary(s => s.Id);
        foreach (var nebula in world.Nebulae.Nebulae)
        {
            foreach (var id in nebula.SystemIds)
            {
                Equal(true, owned.Add(id)); Equal(true, systems.ContainsKey(id));
                var p = systems[id].Position; var dx = (BigInteger)p.X - nebula.Center.X; var dy = (BigInteger)p.Y - nebula.Center.Y;
                Equal(true, dx * dx + dy * dy < (BigInteger)nebula.Radius * nebula.Radius);
            }
        }
        using var json = JsonDocument.Parse(world.ToMetadataJson()); Equal("galaxy-world-v2", json.RootElement.GetProperty("version").GetString());
        Equal(world.Nebulae.Nebulae.Count, json.RootElement.GetProperty("nebulae").GetProperty("Nebulae").GetArrayLength());
    }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}"); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Ожидалось " + typeof(T).Name); }
}
