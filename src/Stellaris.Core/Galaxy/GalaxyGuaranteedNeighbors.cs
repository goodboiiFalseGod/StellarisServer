using System.Globalization;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public sealed record GalaxyNeighborDescriptor(int MinimumColonies, int MaximumColonies,
    int MinimumHops, int MaximumHops, int SpawnChance, bool HasTrigger, string InitializerId, GalaxySourceLocation Source);
public sealed record GalaxyNeighborAllocation(int ParticipantId, string RootHomeSystemId,
    string ParentSystemId, string ChildSystemId, string InitializerId, int Hops, string? GuaranteedBodyId);
public sealed record GalaxyNeighborSite(string Id, NativeFixedPoint Position, bool Initialized, bool ReservedHome);
public sealed record GalaxyNeighborCandidate(string Id, long Ordinal, NativeFixedPoint Position,
    bool Initialized = false, bool ReservedHome = false);
public sealed record GalaxyNeighborSelection(string? CandidateId, int Hops, long CandidateOrdinal);

/// <summary>Компилятор только source grammar generic-home neighbor tree; это не общий script VM.</summary>
public static class GalaxyGuaranteedNeighbors
{
    private static readonly string[] NeighborFields = ["trigger", "hyperlane_jumps", "initializer", "spawn_chance"];

    public static IReadOnlyList<GalaxyNeighborDescriptor> Compile(GalaxyRuntimeCatalog content, GalaxyCompiledInitializer initializer)
    {
        var numeric = new GalaxyNumericValues(content);
        var output = new List<GalaxyNeighborDescriptor>();
        foreach (var source in initializer.Neighbors)
        {
            void Fail(string code, string detail, GalaxySourceLocation location) =>
                throw new GalaxyStructureException("neighbor-" + code, detail, location);
            if (source.Value.Kind != GalaxySyntaxKind.Block || source.Value.Entries.Any(e => e.Operator != "=" || e.Key is null || !NeighborFields.Contains(e.Key, StringComparer.Ordinal)))
                Fail("descriptor-field", "Unsupported active neighbor_system field.", source.Location);
            var trigger = source.Value.Entries.Where(e => e.Key == "trigger").ToArray();
            var hasTrigger = trigger.Length != 0;
            var colonies = 0;
            if (hasTrigger)
            {
                if (trigger.Length != 1 || trigger[0].Value.Kind != GalaxySyntaxKind.Block || trigger[0].Value.Entries.Count != 1)
                    Fail("trigger", "Expected one num_guaranteed_colonies comparison.", source.Location);
                var condition = trigger[0].Value.Entries[0];
                if (condition.Key != "num_guaranteed_colonies" || condition.Operator != ">=" || condition.Value.Kind != GalaxySyntaxKind.Scalar ||
                    !int.TryParse(condition.Value.Text, NumberStyles.None, CultureInfo.InvariantCulture, out colonies) || colonies is < 1 or > 2)
                    Fail("trigger", "Only literal num_guaranteed_colonies >= 1|2 is supported.", condition.Location);
            }
            var jumps = source.Value.Entries.Where(e => e.Key == "hyperlane_jumps").ToArray();
            if (jumps.Length != 1 || jumps[0].Value.Kind != GalaxySyntaxKind.Block)
                Fail("hops", "Expected one hyperlane_jumps range.", source.Location);
            var hopRange = numeric.Range(jumps[0].Value, initializer.Definition.File.Path);
            var minHops = CheckedInteger(hopRange.Minimum, jumps[0].Location);
            var maxHops = CheckedInteger(hopRange.Maximum, jumps[0].Location);
            if (minHops < 1 || maxHops < minHops) Fail("hops", "Invalid inclusive hop range.", jumps[0].Location);
            var target = source.Value.Entries.Where(e => e.Key == "initializer").ToArray();
            if (target.Length != 1 || target[0].Value.Kind != GalaxySyntaxKind.Scalar || string.IsNullOrWhiteSpace(target[0].Value.Text))
                Fail("initializer", "Expected one literal initializer reference.", source.Location);
            var chanceEntries = source.Value.Entries.Where(e => e.Key == "spawn_chance").ToArray();
            var chance = chanceEntries.Length == 0 ? 100 : chanceEntries.Length == 1 && chanceEntries[0].Value.Kind == GalaxySyntaxKind.Scalar &&
                int.TryParse(chanceEntries[0].Value.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedChance) ? parsedChance : -1;
            if (chance is < 0 or > 100) Fail("spawn-chance", "Expected integer spawn_chance in0..100.", chanceEntries.FirstOrDefault()?.Location ?? source.Location);
            output.Add(new(colonies, colonies, minHops, maxHops, chance, hasTrigger, target[0].Value.Text!, source.Location));
        }
        return Array.AsReadOnly(output.ToArray());
    }

    public static GalaxyNeighborSelection? SelectNearest(string parentId, NativeFixedPoint parent,
        IReadOnlyList<GalaxyNeighborCandidate> candidates, IReadOnlyDictionary<string, List<string>> adjacency,
        IReadOnlySet<string> scheduled, int minimumHops, int maximumHops)
    {
        var distances = BreadthFirst(parentId, adjacency, maximumHops);
        foreach (var candidate in candidates.OrderBy(c => Squared(parent, c.Position)).ThenBy(c => c.Ordinal))
        {
            if (candidate.Initialized || candidate.ReservedHome || scheduled.Contains(candidate.Id) || !distances.TryGetValue(candidate.Id, out var hops) ||
                hops < minimumHops || hops > maximumHops) continue;
            return new(candidate.Id, hops, candidate.Ordinal);
        }
        return null;
    }

    public static IReadOnlyDictionary<string, int> BreadthFirst(string root,
        IReadOnlyDictionary<string, List<string>> adjacency, int maximumDepth = int.MaxValue)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal) { [root] = 0 };
        var queue = new Queue<string>(); queue.Enqueue(root);
        while (queue.TryDequeue(out var current))
        {
            var depth = result[current];
            if (depth >= maximumDepth || !adjacency.TryGetValue(current, out var neighbors)) continue;
            foreach (var next in neighbors.Order(StringComparer.Ordinal))
                if (result.TryAdd(next, depth + 1)) queue.Enqueue(next);
        }
        return result;
    }

    private static decimal Squared(NativeFixedPoint left, NativeFixedPoint right) =>
        (decimal)(left.X - right.X) * (left.X - right.X) + (decimal)(left.Y - right.Y) * (left.Y - right.Y);
    private static int CheckedInteger(decimal value, GalaxySourceLocation location) =>
        value == decimal.Truncate(value) && value >= int.MinValue && value <= int.MaxValue
            ? (int)value : throw new GalaxyStructureException("neighbor-range", "Expected integer range.", location);
}

/// <summary>Исполняет только source-shaped cluster flag traversal; никакой общий effect VM.</summary>
public static class GalaxyEmpireClusterEffects
{
    public static void Apply(string systemId, GalaxySyntaxValue effect,
        IReadOnlyDictionary<string, List<string>> adjacency, Action<string, string> setSystemFlag)
    {
        ArgumentNullException.ThrowIfNull(adjacency); ArgumentNullException.ThrowIfNull(setSystemFlag);
        Visit(systemId, effect, 0);
        void Visit(string current, GalaxySyntaxValue block, int depth)
        {
            if (depth > 2) throw new GalaxyStructureException("neighbor-spatial-depth", "every_neighbor_system depth exceeds2.", block.Location);
            if (block.Kind != GalaxySyntaxKind.Block) throw new GalaxyStructureException("neighbor-spatial-form", "Expected effect block.", block.Location);
            foreach (var entry in block.Entries)
            {
                if (entry.Key == "generate_home_system_resources" && entry.Value.Kind == GalaxySyntaxKind.Scalar && entry.Value.Text == "yes") continue;
                if (entry.Key == "set_star_flag" && entry.Operator == "=" && entry.Value.Kind == GalaxySyntaxKind.Scalar && entry.Value.Text == "empire_cluster")
                { setSystemFlag(current, "empire_cluster"); continue; }
                if (entry.Key == "every_neighbor_system" && entry.Operator == "=" && entry.Value.Kind == GalaxySyntaxKind.Block)
                {
                    if (!adjacency.TryGetValue(current, out var neighbors))
                        throw new GalaxyStructureException("neighbor-spatial-system", current, entry.Location);
                    foreach (var neighbor in neighbors.Order(StringComparer.Ordinal)) Visit(neighbor, entry.Value, depth + 1);
                    continue;
                }
                throw new GalaxyStructureException("neighbor-spatial-effect", "Unsupported active spatial effect " + entry.Key, entry.Location);
            }
        }
    }
}
