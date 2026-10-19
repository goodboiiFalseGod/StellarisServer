namespace Stellaris.Core.Galaxy;

public enum HyperlaneConnectivityPolicy { Disabled, RestoreCandidatesThenNearestV1 }
public readonly record struct HyperlaneConnectivityRepair(GeneratedHyperlane Edge, string Reason, bool ExceedsMaximumDistance);

public sealed class ConnectedHyperlaneGraph
{
    public IReadOnlyList<GeneratedHyperlane> Edges { get; }
    public IReadOnlyList<NativeAdjacency> Adjacency { get; }
    public IReadOnlyList<HyperlaneConnectivityRepair> Repairs { get; }
    public IReadOnlyList<int> ComponentBySite { get; }
    public int ComponentCount { get; }
    public int PrunedComponentCount { get; }
    public bool IsConnected => ComponentCount <= 1;
    public bool UsesApproximation => Repairs.Count > 0;
    public HyperlaneConnectivityPolicy Policy { get; }

    internal ConnectedHyperlaneGraph(uint[] handles, GeneratedHyperlane[] edges, HyperlaneConnectivityRepair[] repairs,
        int[] components, int count, int prunedCount, HyperlaneConnectivityPolicy policy)
    {
        Edges = Array.AsReadOnly(edges); Repairs = Array.AsReadOnly(repairs);
        ComponentBySite = Array.AsReadOnly(components); ComponentCount = count;
        PrunedComponentCount = prunedCount; Policy = policy;
        Adjacency = Array.AsReadOnly(edges.SelectMany(e => new[]
        {
            new NativeAdjacency(handles[e.A], handles[e.B], e.Length, e.Flag),
            new NativeAdjacency(handles[e.B], handles[e.A], e.Length, e.Flag)
        }).ToArray());
    }
}

/// <summary>Явный prototype completion policy; не выдаётся за native partition bridges.</summary>
public static class HyperlaneConnectivity
{
    private readonly record struct Pair(int A, int B, UInt128 Squared);

    public static ConnectedHyperlaneGraph Complete(CandidateHyperlaneGraphResult candidates,
        HyperlanePruningResult pruned, long maximumDistance, HyperlaneConnectivityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(candidates); ArgumentNullException.ThrowIfNull(pruned);
        if (!candidates.Handles.SequenceEqual(pruned.Handles) || maximumDistance < 0 ||
            policy is not (HyperlaneConnectivityPolicy.Disabled or HyperlaneConnectivityPolicy.RestoreCandidatesThenNearestV1))
            throw new ArgumentException("Нужны тот же ordered world, valid cutoff и explicit policy.");
        var count = candidates.Handles.Count; var union = new Union(count);
        var final = pruned.Edges.ToList(); var repairs = new List<HyperlaneConnectivityRepair>();
        foreach (var edge in final) union.Join(edge.A, edge.B);
        var prunedCount = union.Count;
        if (policy == HyperlaneConnectivityPolicy.RestoreCandidatesThenNearestV1 && union.Count > 1)
        {
            foreach (var edge in candidates.Candidates.SameGroup.Concat(candidates.Candidates.DifferentGroup)
                .OrderBy(e => e.StoredLength).ThenBy(e => Math.Min(e.A, e.B)).ThenBy(e => Math.Max(e.A, e.B)))
            {
                if (!union.Join(edge.A, edge.B)) continue;
                var added = new GeneratedHyperlane(edge.A, edge.B, edge.StoredLength,
                    candidates.MappedGroups[edge.A] == candidates.MappedGroups[edge.B] ? (byte)0 : (byte)1);
                final.Add(added); repairs.Add(new(added, "discarded-candidate", added.Length > maximumDistance));
                if (union.Count == 1) break;
            }
            if (union.Count > 1)
            {
                var pairs = new List<Pair>();
                for (var a = 0; a < count; a++) for (var b = a + 1; b < count; b++)
                {
                    if (union.Find(a) == union.Find(b)) continue;
                    var dx = (Int128)candidates.Positions[a].X - candidates.Positions[b].X;
                    var dy = (Int128)candidates.Positions[a].Y - candidates.Positions[b].Y;
                    pairs.Add(new(a, b, (UInt128)(dx * dx + dy * dy)));
                }
                foreach (var pair in pairs.OrderBy(p => p.Squared).ThenBy(p => p.A).ThenBy(p => p.B))
                {
                    if (!union.Join(pair.A, pair.B)) continue;
                    var length = SuppliedTopologyPostprocess.RawDistance(candidates.Positions[pair.A], candidates.Positions[pair.B]) / 100000 * 100000;
                    var added = new GeneratedHyperlane(pair.A, pair.B, length,
                        candidates.MappedGroups[pair.A] == candidates.MappedGroups[pair.B] ? (byte)0 : (byte)1);
                    final.Add(added); repairs.Add(new(added, "nearest-component", length > maximumDistance));
                    if (union.Count == 1) break;
                }
            }
        }
        var labels = new Dictionary<int, int>(); var components = new int[count];
        for (var i = 0; i < count; i++)
        {
            var root = union.Find(i);
            if (!labels.TryGetValue(root, out var label)) { label = labels.Count; labels.Add(root, label); }
            components[i] = label;
        }
        return new(candidates.Handles.ToArray(), final.ToArray(), repairs.ToArray(), components, union.Count, prunedCount, policy);
    }

    private sealed class Union
    {
        private readonly int[] parent, size;
        public int Count { get; private set; }
        public Union(int count) { parent = Enumerable.Range(0, count).ToArray(); size = Enumerable.Repeat(1, count).ToArray(); Count = count; }
        public int Find(int node)
        {
            while (parent[node] != node) { parent[node] = parent[parent[node]]; node = parent[node]; }
            return node;
        }
        public bool Join(int a, int b)
        {
            a = Find(a); b = Find(b); if (a == b) return false;
            if (size[a] < size[b]) (a, b) = (b, a);
            parent[b] = a; size[a] += size[b]; Count--; return true;
        }
    }
}
