using Stellaris.Core.Galaxy;

internal static class GalaxyNamingChecks
{
    public static void Run(string root, NativeTriangleBackend backend)
    {
        GalaxyWorldSystem System(string id, string? name = null) => new(id, 1, new(0, 0), 0, "fixture", "sc_g", name, [], [], []);
        GalaxyWorldBody Body(string id, GalaxyBodyKind kind, string? parent = null, string? name = null, string? policy = null) =>
            new(id, "s", parent, kind, "pc_fixture", 10, 10, 0, 0, 0, name, [], SatelliteNamingPolicy: policy);
        var fixture = new[] { Body("star", GalaxyBodyKind.Star), Body("second", GalaxyBodyKind.Star),
            Body("p10", GalaxyBodyKind.Planet), Body("m2", GalaxyBodyKind.Moon, "p10"),
            Body("m1", GalaxyBodyKind.Moon, "p10", "Literal_override"), Body("m3", GalaxyBodyKind.Moon, "p10"),
            Body("p2", GalaxyBodyKind.Planet), Body("literal", GalaxyBodyKind.Planet, name: "Reserved") };
        var named = GalaxyNaming.Assign([System("s"), System("t", "Explicit")], fixture, ["Reserved", "Alpha", "Alpha", "Beta"], () => 0);
        Require(named.Systems[0].Name == "Alpha" && named.Systems[1].Name == "Explicit", "explicit/pool dedup");
        Require(string.Join('|', named.Bodies.Select(b => b.Name)) == "Alpha|Alpha B|Alpha I|Alpha I a|Literal_override|Alpha I c|Alpha II|Reserved", "creation-order/overrides");
        foreach (var (policy, suffix) in new[] { ("upper_case_roman_numerals", "I"), ("lower_case_roman_numerals", "I"),
            ("arabic_numerals", "1"), ("lower_case_latin_letters", "a"), ("upper_case_latin_letters", "A") })
        {
            var policyNames = GalaxyNaming.Assign([System("s", "Literal")],
                [Body("p", GalaxyBodyKind.Planet, policy: policy), Body("m", GalaxyBodyKind.Moon, "p")], ["Other"], () => 0);
            Require(policyNames.Bodies[1].Name == "Literal I " + suffix, policy);
        }
        var fallback = GalaxyNaming.Assign([System("a"), System("b"), System("c")], [], ["Only"], () => 0);
        Require(fallback.Systems.Select(s => s.Name).Distinct().Count() == 3 && fallback.Diagnostics.Count == 2, "pool exhaustion");
        Throws(() => GalaxyNaming.Assign([System("s")], [], [], () => 0));
        Throws(() => GalaxyNaming.Assign([System("s")], [Body("p", GalaxyBodyKind.Planet, policy: "unknown"), Body("m", GalaxyBodyKind.Moon, "p")], ["Only"], () => 0));
        var inherited = GalaxyNaming.Assign([System("s")], [Body("star", GalaxyBodyKind.Star, name: "Primary literal")], ["Other"], () => 0);
        Require(inherited.Systems[0].Name == "Primary literal", "primary override");
        Console.WriteLine("PASS naming synthetic overrides/order/policies/pool failure");

        var catalog = GalaxyLobbyCatalog.Import(root, new([], []));
        var request = catalog.Resolve(new("names-452", "tiny", "elliptical", 1,
            [new(1, "economic", "pc_continental"), new(2, "scientific", "pc_continental")])).Request!;
        var generator = new GalaxyWorldGenerator(catalog, backend);
        GalaxyWorldSnapshot Generate()
        {
            var result = generator.Generate(request);
            if (!result.Success) throw new Exception(result.Failure!.ToString());
            return result.World!;
        }
        var world = Generate(); var repeat = Generate();
        Require(world.WorldFingerprint == repeat.WorldFingerprint, "repeat seed");
        Require(world.Systems.All(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.NameKey)) &&
            world.Bodies.All(b => !string.IsNullOrWhiteSpace(b.Name) && !string.IsNullOrWhiteSpace(b.NameKey) && b.Name != b.ClassId), "all names");
        var ids = world.Systems.Select(s => s.Id).Concat(world.Bodies.Select(b => b.Id)).ToArray();
        Require(ids.Distinct().Count() == ids.Length, "IDs");
        Require(world.Systems.Select(s => s.NameKey).Distinct().Count() == world.Systems.Count, "unique pool");
        Console.WriteLine($"PASS naming genuine tiny replay systems={world.Systems.Count} bodies={world.Bodies.Count} hash={world.WorldFingerprint}");
    }
    private static void Require(bool valid, string label) { if (!valid) throw new Exception(label); }
    private static void Throws(Action body)
    {
        try { body(); } catch (InvalidDataException) { return; }
        throw new Exception("Ожидался typed naming failure.");
    }
}
