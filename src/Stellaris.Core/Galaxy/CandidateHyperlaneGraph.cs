namespace Stellaris.Core.Galaxy;

public enum CandidateGraphPath { SmallDirect, TriangleCandidates, DegenerateZeroTopology }
public readonly record struct CandidateGroupInput(int Id, int InitialIncidentCounter);
public readonly record struct CandidateGroupCounter(int Initial, int Delta, int Effective);

public sealed class CandidateHyperlaneGraphResult
{
    public CandidateGraphPath Path { get; }
    public int TriangleCalls { get; }
    public IReadOnlyList<NativeFixedPoint> Positions { get; }
    public IReadOnlyList<uint> Handles { get; }
    public IReadOnlyList<int> MappedGroups { get; }
    public IReadOnlyList<NativeTriangleEdge> OrderedTriangleEdges { get; }
    public NativeCandidateSplit Candidates { get; }
    public IReadOnlyList<NativeAdjacency> DirectAdjacency { get; }
    public IReadOnlyList<NativeAdjacency> DiagnosticCandidateAdjacency { get; }
    public IReadOnlyList<int> ComponentBySite { get; }
    public int ComponentCount { get; }
    public IReadOnlyDictionary<int, int> InterGroupCounterDeltas { get; }
    public IReadOnlyDictionary<int, CandidateGroupCounter> GroupCounters { get; }
    public bool RequiresPruningAndBridges => Path != CandidateGraphPath.SmallDirect;

    internal CandidateHyperlaneGraphResult(CandidateGraphPath path, NativeFixedPoint[] positions, uint[] handles,
        int[] groups, NativeTriangleEdge[] edges, NativeCandidateSplit split, NativeAdjacency[] adjacency,
        int[] components, int componentCount, CandidateGroupInput[] groupRecords)
    {
        Path = path; TriangleCalls = path == CandidateGraphPath.SmallDirect ? 0 : 1;
        Positions = Array.AsReadOnly(positions); Handles = Array.AsReadOnly(handles);
        MappedGroups = Array.AsReadOnly(groups); OrderedTriangleEdges = Array.AsReadOnly(edges);
        Candidates = split;
        DirectAdjacency = Array.AsReadOnly(path == CandidateGraphPath.SmallDirect ? adjacency : []);
        DiagnosticCandidateAdjacency = Array.AsReadOnly(path == CandidateGraphPath.SmallDirect ? [] : adjacency);
        ComponentBySite = Array.AsReadOnly(components); ComponentCount = componentCount;
        var deltas = groupRecords.ToDictionary(g => g.Id, _ => 0);
        foreach (var edge in split.DifferentGroup)
        {
            deltas[groups[edge.A]] = checked(deltas[groups[edge.A]] + 1);
            deltas[groups[edge.B]] = checked(deltas[groups[edge.B]] + 1);
        }
        InterGroupCounterDeltas = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(deltas);
        GroupCounters = new System.Collections.ObjectModel.ReadOnlyDictionary<int, CandidateGroupCounter>(
            groupRecords.ToDictionary(g => g.Id, g => new CandidateGroupCounter(g.InitialIncidentCounter,
                deltas[g.Id], checked(g.InitialIncidentCounter + deltas[g.Id]))));
    }
}

/// <summary>M13: relaxed coordinates → actual Triangle → candidate graph. Не final pruning/partition.</summary>
public static class CandidateHyperlaneGraph
{
    public static CandidateHyperlaneGraphResult Build(ReadOnlySpan<NativeFixedPoint> positions,
        ReadOnlySpan<uint> handles, ReadOnlySpan<int> mappedGroups,
        ReadOnlySpan<CandidateGroupInput> groupRecords, NativeTriangleBackend? backend)
    {
        if (positions.Length != handles.Length || positions.Length >= 3 && positions.Length != mappedGroups.Length)
            throw new ArgumentException("Positions/handles должны совпадать; N>=3 требует explicit mappedGroups той же длины.");
        if (positions.Length > NativeTriangleBackend.MaximumSites)
            throw new ArgumentOutOfRangeException(nameof(positions));
        var knownGroups = new HashSet<int>();
        var maximumDelta = positions.Length < 3 ? 0 : checked(3 * positions.Length);
        foreach (var group in positions.Length >= 3 ? groupRecords : ReadOnlySpan<CandidateGroupInput>.Empty)
        {
            if (group.Id < 0 || group.InitialIncidentCounter < 0 || !knownGroups.Add(group.Id))
                throw new ArgumentException("Требуются unique resolved group records и nonnegative counters.", nameof(groupRecords));
            _ = checked(group.InitialIncidentCounter + maximumDelta);
        }
        foreach (var group in positions.Length >= 3 ? mappedGroups : ReadOnlySpan<int>.Empty)
            if (!knownGroups.Contains(group)) throw new ArgumentException("Mapped group отсутствует в supplied records.", nameof(mappedGroups));
        var unique = new HashSet<uint>();
        foreach (var handle in handles)
            if ((handle & 0x00FFFFFF) >= 0x00FFFFFE || !unique.Add(handle))
                throw new ArgumentException("Требуются unique non-sentinel system handles.", nameof(handles));
        foreach (var point in positions)
            if (point.X < -50000000 || point.X > 50000000 || point.Y < -50000000 || point.Y > 50000000)
                throw new ArgumentOutOfRangeException(nameof(positions), "Требуются stored relaxed coordinates в bbox±50000000.");
        var points = positions.ToArray();
        var ids = handles.ToArray();
        var groups = points.Length >= 3 ? mappedGroups.ToArray() : [];
        NativeTriangleEdge[] orderedEdges = [];
        NativeCandidateSplit split = new(Array.AsReadOnly(Array.Empty<NativeCandidateEdge>()),
            Array.AsReadOnly(Array.Empty<NativeCandidateEdge>()));
        NativeAdjacency[] adjacency;
        var path = CandidateGraphPath.SmallDirect;
        if (points.Length < 3)
            adjacency = SuppliedTopologyPostprocess.DirectSmallGraph(points, ids).ToArray();
        else
        {
            ArgumentNullException.ThrowIfNull(backend);
            var quantized = points.Select(NativeGeometryPrimitives.QuantizeTriangleInput).ToArray();
            var tessellation = backend.Tessellate(quantized, TriangleCallPhase.Initial);
            orderedEdges = tessellation.Edges.Select(e => new NativeTriangleEdge(e.SiteA, e.SiteB)).ToArray();
            split = SuppliedTopologyPostprocess.FilterAndSplit(points, groups, orderedEdges);
            path = tessellation.Triangles.Count == 0 ? CandidateGraphPath.DegenerateZeroTopology : CandidateGraphPath.TriangleCandidates;
            adjacency = split.SameGroup.Concat(split.DifferentGroup).SelectMany(e => new[]
            {
                new NativeAdjacency(ids[e.A], ids[e.B], e.StoredLength, e.Flag),
                new NativeAdjacency(ids[e.B], ids[e.A], e.StoredLength, e.Flag)
            }).ToArray();
        }
        var byHandle = ids.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        var neighbors = Enumerable.Range(0, ids.Length).Select(_ => new List<int>()).ToArray();
        foreach (var edge in adjacency) neighbors[byHandle[edge.SiteHandle]].Add(byHandle[edge.NeighborHandle]);
        var components = Enumerable.Repeat(-1, ids.Length).ToArray();
        var count = 0;
        for (var root = 0; root < ids.Length; root++)
        {
            if (components[root] != -1) continue;
            var queue = new Queue<int>();
            queue.Enqueue(root); components[root] = count;
            while (queue.TryDequeue(out var site))
                foreach (var neighbor in neighbors[site])
                    if (components[neighbor] == -1) { components[neighbor] = count; queue.Enqueue(neighbor); }
            count++;
        }
        return new(path, points, ids, groups, orderedEdges, split, adjacency, components, count,
            points.Length >= 3 ? groupRecords.ToArray() : []);
    }
}
