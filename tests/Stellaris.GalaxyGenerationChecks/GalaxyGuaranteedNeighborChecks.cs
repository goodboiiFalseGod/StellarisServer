using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Galaxy;

internal static class GalaxyGuaranteedNeighborChecks
{
    public static void Run(GalaxyRuntimeCatalog content)
    {
        GalaxyNeighborDescriptor[] roots = GalaxyLobbyCatalog.HomeInitializers
            .SelectMany(id => GalaxyGuaranteedNeighbors.Compile(content, GalaxyInitializerExecution.Compile(content, id)))
            .ToArray();
        if (roots.Length != 12) throw new Exception($"Expected two source neighbor branches for each generic home; got{roots.Length}.");
        foreach (var colonies in new[] { 0, 1, 2 })
        {
            var active = roots.Count(descriptor => !descriptor.HasTrigger || colonies >= descriptor.MinimumColonies);
            var expected = colonies switch { 0 => 0, 1 => 6, _ => 12 };
            if (active != expected) throw new Exception($"Source triggers at colonies={colonies} expected{expected} active branches, got{active}.");
        }
        var first = GalaxyGuaranteedNeighbors.Compile(content, GalaxyInitializerExecution.Compile(content, "neighbor_t1_first_colony")).Single();
        var second = GalaxyGuaranteedNeighbors.Compile(content, GalaxyInitializerExecution.Compile(content, "neighbor_t1")).Single();
        if (first.InitializerId != "neighbor_t2" || first.HasTrigger || second.InitializerId != "neighbor_t2_second_colony" || second.HasTrigger)
            throw new Exception("The source generic-home chains changed.");

        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["root"] = ["one", "two"], ["one"] = ["root", "three"], ["two"] = ["root"],
            ["three"] = ["one", "four"], ["four"] = ["three", "five"], ["five"] = ["four"]
        };
        var candidates = new[]
        {
            new GalaxyNeighborCandidate("one", 1, new(1, 0)),
            new GalaxyNeighborCandidate("two", 2, new(-1, 0)),
            new GalaxyNeighborCandidate("three", 3, new(2, 0), Initialized: true),
            new GalaxyNeighborCandidate("four", 4, new(3, 0), ReservedHome: true),
            new GalaxyNeighborCandidate("five", 5, new(4, 0))
        };
        var selected = GalaxyGuaranteedNeighbors.SelectNearest("root", new(0, 0), candidates, adjacency, new HashSet<string>(), 1, 3);
        if (selected is null || selected.CandidateId != "one") throw new Exception("Nearest tie did not use lower source ordinal.");
        selected = GalaxyGuaranteedNeighbors.SelectNearest("root", new(0, 0), candidates, adjacency, new HashSet<string> { "one" }, 1, 3);
        if (selected is null || selected.CandidateId != "two") throw new Exception("Scheduled candidate was not skipped.");
        selected = GalaxyGuaranteedNeighbors.SelectNearest("root", new(0, 0), candidates, adjacency, new HashSet<string> { "one", "two" }, 1, 3);
        if (selected is not null) throw new Exception("Initialized/reserved/scheduled or beyond-three-hop candidates were selected.");
    }
}
