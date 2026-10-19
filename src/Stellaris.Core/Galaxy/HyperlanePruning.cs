namespace Stellaris.Core.Galaxy;

public readonly record struct HyperlaneGroupState(int Id, int Target, int Current);
public readonly record struct HyperlaneGroupTarget(int Id, int Target);
public readonly record struct HyperlanePruningOptions(long Density, long MinimumDensity, long MaximumDensity,
    long MaximumDistance, long RemovalPercent, int DegreeBase);
public readonly record struct GeneratedHyperlane(int A, int B, long Length, byte Flag);
public readonly record struct HyperlanePruningDraw(string Pass, int PoolSize, int PhysicalIndex, uint Value);
public readonly record struct HyperlaneRejectedEdge(string Pass, NativeCandidateEdge Edge, string Reason);
public readonly record struct HyperlaneGroupLink(int Group, int OtherGroup, int From, int To, long Length);

public sealed class HyperlanePruningResult
{
    public IReadOnlyList<uint> Handles { get; }
    public IReadOnlyList<GeneratedHyperlane> Edges { get; }
    public IReadOnlyList<NativeAdjacency> Adjacency { get; }
    public IReadOnlyList<NativeCandidateEdge> PhysicalA { get; }
    public IReadOnlyList<NativeCandidateEdge> PhysicalB { get; }
    public IReadOnlyList<HyperlaneRejectedEdge> Rejected { get; }
    public IReadOnlyList<HyperlanePruningDraw> Draws { get; }
    public IReadOnlyList<HyperlaneGroupLink> AcceptedGroupLinks { get; }
    public IReadOnlyList<HyperlaneGroupLink> RejectedGroupLinks { get; }
    public IReadOnlyDictionary<int, int> SelectionCounters { get; }
    public NativeRandomState? FinalStateA { get; }
    public NativeRandomState? FinalStateB { get; }
    public uint? GlobalResultA { get; }
    public uint? GlobalResultB { get; }
    public int GlobalDrawSites => GlobalResultA is null ? 0 : 2;
    public int TopologyStateMarker => GlobalResultA is null ? 0 : 2;

    internal HyperlanePruningResult(uint[] handles, GeneratedHyperlane[] edges, NativeCandidateEdge[] a,
        NativeCandidateEdge[] b, HyperlaneRejectedEdge[] rejected, HyperlanePruningDraw[] draws,
        HyperlaneGroupLink[] acceptedLinks, HyperlaneGroupLink[] rejectedLinks, Dictionary<int, int> counters,
        NativeRandomState? stateA, NativeRandomState? stateB, uint? globalA, uint? globalB)
    {
        Handles = Array.AsReadOnly(handles); Edges = Array.AsReadOnly(edges);
        Adjacency = Array.AsReadOnly(edges.SelectMany(e => new[]
        {
            new NativeAdjacency(handles[e.A], handles[e.B], e.Length, e.Flag),
            new NativeAdjacency(handles[e.B], handles[e.A], e.Length, e.Flag)
        }).ToArray());
        PhysicalA = Array.AsReadOnly(a); PhysicalB = Array.AsReadOnly(b);
        Rejected = Array.AsReadOnly(rejected); Draws = Array.AsReadOnly(draws);
        AcceptedGroupLinks = Array.AsReadOnly(acceptedLinks); RejectedGroupLinks = Array.AsReadOnly(rejectedLinks);
        SelectionCounters = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(counters);
        FinalStateA = stateA; FinalStateB = stateB; GlobalResultA = globalA; GlobalResultB = globalB;
    }
}

/// <summary>Normal simple M13 graph → exact A/B pruning; ownedcopies, no outer RNG/world mutation.</summary>
public static class HyperlanePruning
{
    public static HyperlanePruningResult Run(CandidateHyperlaneGraphResult graph,
        ReadOnlySpan<HyperlaneGroupTarget> targets, HyperlanePruningOptions options, uint globalA, uint globalB)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.Path == CandidateGraphPath.SmallDirect)
        {
            var edges = graph.Handles.Count == 2
                ? new[] { new GeneratedHyperlane(0, 1, graph.DirectAdjacency[0].Length, 0) } : [];
            return new(graph.Handles.ToArray(), edges, [], [], [], [], [], [], [], null, null, null, null);
        }
        var targetMap = new Dictionary<int, int>();
        foreach (var target in targets)
            if (!targetMap.TryAdd(target.Id, target.Target)) throw new ArgumentException("Duplicate group target.", nameof(targets));
        if (targetMap.Count != graph.GroupCounters.Count || graph.GroupCounters.Keys.Any(id => !targetMap.ContainsKey(id)))
            throw new ArgumentException("Targets должны описывать все supplied group records.", nameof(targets));
        var groups = graph.GroupCounters.Select(g => new HyperlaneGroupState(g.Key, targetMap[g.Key], g.Value.Effective)).ToArray();
        return RunSuppliedCandidates(graph.Handles.ToArray(), graph.MappedGroups.ToArray(), groups,
            graph.Candidates.SameGroup.ToArray(), graph.Candidates.DifferentGroup.ToArray(), options, globalA, globalB);
    }

    /// <summary>Explicit M13 seam для source vectors; V>=3, whole-unit weights, clean simple edges.</summary>
    public static HyperlanePruningResult RunSuppliedCandidates(ReadOnlySpan<uint> handles, ReadOnlySpan<int> mappedGroups,
        ReadOnlySpan<HyperlaneGroupState> groupRecords, ReadOnlySpan<NativeCandidateEdge> sameGroup,
        ReadOnlySpan<NativeCandidateEdge> crossGroup, HyperlanePruningOptions options, uint globalA, uint globalB)
    {
        if (handles.Length < 3 || handles.Length > NativeTriangleBackend.MaximumSites || mappedGroups.Length != handles.Length)
            throw new ArgumentException("Pruning seam требует3..2047 systems и complete mapping.");
        if (globalA > int.MaxValue || globalB > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(globalA), "Global results должны быть uint31.");
        if (options.MinimumDensity <= 0 || options.Density < options.MinimumDensity || options.Density > options.MaximumDensity ||
            options.MaximumDistance < 0 || options.RemovalPercent < 0 || options.RemovalPercent > 100000 || options.DegreeBase <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        var ids = handles.ToArray(); var mapping = mappedGroups.ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(h => (h & 0xFFFFFF) >= 0xFFFFFE))
            throw new ArgumentException("Invalid/duplicate system handles.", nameof(handles));
        var groups = new Dictionary<int, HyperlaneGroupState>();
        foreach (var group in groupRecords)
            if (group.Id < 0 || group.Target < 0 || group.Current < 0 || !groups.TryAdd(group.Id, group))
                throw new ArgumentException("Invalid/duplicate group records.", nameof(groupRecords));
        if (mapping.Any(g => !groups.ContainsKey(g))) throw new ArgumentException("Unresolved group mapping.", nameof(mappedGroups));
        var simple = new HashSet<(int, int)>();
        var incident = groups.Keys.ToDictionary(g => g, _ => 0);
        void Validate(ReadOnlySpan<NativeCandidateEdge> edges, bool cross)
        {
            foreach (var edge in edges)
            {
                if ((uint)edge.A >= ids.Length || (uint)edge.B >= ids.Length || edge.A == edge.B || edge.Flag != 0 ||
                    edge.StoredLength < 0 || edge.StoredLength % 100000 != 0 || edge.StoredLength / 100000 > int.MaxValue ||
                    (mapping[edge.A] != mapping[edge.B]) != cross || !simple.Add(Key(edge.A, edge.B)))
                    throw new ArgumentException("Invalid/non-simple M13 candidate.");
                if (cross) { incident[mapping[edge.A]]++; incident[mapping[edge.B]]++; }
            }
        }
        Validate(sameGroup, false); Validate(crossGroup, true);
        if (groups.Any(g => g.Value.Current < incident[g.Key])) throw new ArgumentException("Current counter меньше incident B records.");
        var threshold = options.Density <= 100000 ? options.DegreeBase : checked((int)((checked(options.DegreeBase * options.Density) + 50000) / 100000));
        var a = sameGroup.ToArray(); var b = crossGroup.ToArray();
        Array.Sort(a, Compare); Array.Sort(b, Compare);
        var stateA = NativeRandom.FromSpecifiedSeed((int)globalA); var stateB = NativeRandom.FromSpecifiedSeed((int)globalB);
        var draws = new List<HyperlanePruningDraw>();
        var rejectedA = new List<HyperlaneRejectedEdge>(); var rejectedB = new List<HyperlaneRejectedEdge>();
        void Remove(NativeCandidateEdge[] source, int index, string pass, string reason, List<HyperlaneRejectedEdge> rejected)
        {
            rejected.Add(new(pass, source[index], reason)); source[index] = source[index] with { A = -1 };
        }
        var mandatory = 0;
        for (var i = 0; i < a.Length; i++) if (a[i].StoredLength > options.MaximumDistance)
        { Remove(a, i, "A", "distance", rejectedA); mandatory++; }
        var requested = checked((int)((long)a.Length * options.RemovalPercent / options.Density)) - mandatory;
        // Native selector start=L: защищает первые L shortest physical records, не start0.
        var pool = Enumerable.Range(mandatory, a.Length - mandatory).Where(i => a[i].A >= 0).ToList();
        var selections = Math.Min(Math.Max(requested, 0), pool.Count);
        for (var i = 0; i < selections; i++)
        {
            var index = Select(pool, "A", ref stateA, draws);
            Remove(a, index, "A", "random", rejectedA);
        }
        rejectedA.Sort((x, y) => Compare(x.Edge, y.Edge));
        DegreePass(a, [], threshold, options.Density < options.MaximumDensity, "A", rejectedA, ids.Length);
        var counters = groups.ToDictionary(g => g.Key, g => g.Value.Current);
        pool = Enumerable.Range(0, b.Length).ToList();
        while (pool.Count > 0)
        {
            var index = Select(pool, "B", ref stateB, draws); var edge = b[index];
            var ga = mapping[edge.A]; var gb = mapping[edge.B];
            var distant = edge.StoredLength > options.MaximumDistance;
            if (!distant && !(options.Density < options.MaximumDensity && counters[ga] > groups[ga].Target && counters[gb] > groups[gb].Target)) continue;
            Remove(b, index, "B", distant ? "distance" : "group-target", rejectedB);
            counters[ga] = checked(counters[ga] - 1); counters[gb] = checked(counters[gb] - 1);
        }
        rejectedB.Sort((x, y) => Compare(x.Edge, y.Edge));
        // Scratch degree pass не получает group records и не меняет selection counters.
        DegreePass(b, a, threshold, options.Density < options.MaximumDensity, "B", rejectedB, ids.Length);
        var final = a.Where(e => e.A >= 0).Select(e => new GeneratedHyperlane(e.A, e.B, e.StoredLength, 0))
            .Concat(b.Where(e => e.A >= 0).Select(e => new GeneratedHyperlane(e.A, e.B, e.StoredLength, 1))).ToArray();
        HyperlaneGroupLink[] Links(IEnumerable<NativeCandidateEdge> edges) => edges.SelectMany(e => new[]
        {
            new HyperlaneGroupLink(mapping[e.A], mapping[e.B], e.A, e.B, e.StoredLength),
            new HyperlaneGroupLink(mapping[e.B], mapping[e.A], e.B, e.A, e.StoredLength)
        }).ToArray();
        return new(ids, final, a, b, rejectedA.Concat(rejectedB).ToArray(), draws.ToArray(),
            Links(a.Concat(b).Where(e => e.A >= 0)), Links(rejectedA.Concat(rejectedB).Select(r => r.Edge)),
            counters, stateA, stateB, globalA, globalB);
    }

    private static int Select(List<int> pool, string pass, ref NativeRandomState state, List<HyperlanePruningDraw> trace)
    {
        var draw = NativeRandom.Next(state); state = draw.NextState;
        var position = (int)(draw.Value % (uint)pool.Count); var index = pool[position];
        trace.Add(new(pass, pool.Count, index, draw.Value));
        pool[position] = pool[^1]; pool.RemoveAt(pool.Count - 1); return index;
    }
    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
    private static int Compare(NativeCandidateEdge a, NativeCandidateEdge b)
    {
        var weight = a.StoredLength.CompareTo(b.StoredLength);
        if (weight != 0) return weight;
        var first = a.A.CompareTo(b.A); return first != 0 ? first : a.B.CompareTo(b.B);
    }
    private static void DegreePass(NativeCandidateEdge[] source, NativeCandidateEdge[] other, int threshold,
        bool enabled, string pass, List<HyperlaneRejectedEdge> rejected, int count)
    {
        var degrees = new int[count]; var scratch = new HashSet<(int, int)>();
        foreach (var edge in source.Concat(other).Where(e => e.A >= 0))
        { scratch.Add(Key(edge.A, edge.B)); degrees[edge.A]++; degrees[edge.B]++; }
        if (enabled) for (var i = 0; i < source.Length; i++)
        {
            var edge = source[i];
            if (edge.A < 0 || degrees[edge.A] < threshold || degrees[edge.B] < threshold) continue;
            rejected.Add(new(pass, edge, "degree")); source[i] = edge with { A = -1 };
            if (!scratch.Remove(Key(edge.A, edge.B))) throw new InvalidOperationException("DefensiveInvariantViolation.");
            degrees[edge.A]--; degrees[edge.B]--;
        }
        // Direct reverse adjacency существует у каждого survivor: native defensive helper
        // на clean simple graph всегда видит one-hop path; malformed state не эмулируем.
        foreach (var edge in source.Where(e => e.A >= 0))
            if (!scratch.Contains(Key(edge.A, edge.B))) throw new InvalidOperationException("DefensiveInvariantViolation.");
    }
}
