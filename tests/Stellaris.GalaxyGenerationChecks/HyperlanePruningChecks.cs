using Stellaris.Core.Galaxy;

internal static class HyperlanePruningChecks
{
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}"); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception($"Ожидалось {typeof(T).Name}"); }
    private static HyperlanePruningOptions Options(long h = 100000, long max = 300000, long distance = 10000000, long remove = 15000) =>
        new(h, 50000, max, distance, remove, 4);
    private static NativeCandidateEdge[] Complete(int count) => Enumerable.Range(0, count)
        .SelectMany(a => Enumerable.Range(a + 1, count - a - 1).Select(b => (a, b)))
        .Select((pair, index) => new NativeCandidateEdge(pair.a, pair.b, (index + 1) * 100000, 0)).ToArray();
    internal static void Run(Action<string, Action> check)
    {
        check("pruning A native seed, target, holes and start=L", () =>
        {
            var handles = Enumerable.Range(0, 5).Select(i => (uint)i).ToArray();
            var edges = Complete(5);
            var result = HyperlanePruning.RunSuppliedCandidates(handles, new int[5], [new(0, 0, 0)],
                edges.Reverse().ToArray(), [], Options(max: 100000), 0, 1);
            Equal(9, result.Edges.Count); Equal(-1, result.PhysicalA[2].A);
            Equal(1, result.Draws.Count); Equal(0x4BAE8956u, result.Draws[0].Value);
            Equal(2, result.Draws[0].PhysicalIndex); Equal(0xEDE419C7u, result.FinalStateA!.Value.Counter);
            Equal(0xE5F10AFAu, result.FinalStateB!.Value.Counter);
            Equal(2, result.GlobalDrawSites); Equal(18, result.Adjacency.Count);
            Equal(18, result.AcceptedGroupLinks.Count); Equal(2, result.RejectedGroupLinks.Count);
            Equal(true, result.Edges.All(e => e.Flag == 0));
            Equal(true, edges.All(e => e.A >= 0));
            var twenty = Complete(7).Take(20).ToArray();
            var offset = HyperlanePruning.RunSuppliedCandidates(Enumerable.Range(0, 7).Select(i => (uint)i).ToArray(), new int[7],
                [new(0, 0, 0)], twenty, [], Options(max: 100000, distance: 1900000), 0, 0);
            Equal(3, offset.Rejected.Count); Equal(2, offset.Draws.Count);
            Equal(true, offset.PhysicalA[0].A >= 0);
            Equal(true, offset.Draws.All(d => d.PhysicalIndex >= 1 && d.PoolSize <= 18));
            var mandatory = HyperlanePruning.RunSuppliedCandidates(Enumerable.Range(0, 7).Select(i => (uint)i).ToArray(), new int[7],
                [new(0, 0, 0)], twenty, [], Options(max: 100000, distance: 1600000), 0, 0);
            Equal(4, mandatory.Rejected.Count); Equal(0, mandatory.Draws.Count);
        });
        check("pruning B strict group targets, distance override and materialized flag", () =>
        {
            NativeCandidateEdge[] edges = [new(0, 2, 100000, 0), new(1, 3, 200000, 0)];
            var result = HyperlanePruning.RunSuppliedCandidates([0, 1, 2, 3], [7, 7, 9, 9],
                [new(7, 1, 2), new(9, 1, 2)], [], edges, Options(), 0, 0);
            Equal(1, result.Edges.Count); Equal(1, result.Edges[0].A); Equal(3, result.Edges[0].B);
            Equal((byte)1, result.Edges[0].Flag); Equal(1, result.SelectionCounters[7]); Equal(1, result.SelectionCounters[9]);
            Equal(2, result.Draws.Count); Equal(0, result.Draws[0].PhysicalIndex);
            Equal(true, result.Adjacency.All(e => e.Flag == 1));
            var forced = HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [7, 7, 9],
                [new(7, 1, 1), new(9, 1, 1)], [], [new(0, 2, 200000, 0)], Options(h: 300000, distance: 100000), 0, 0);
            Equal(0, forced.Edges.Count); Equal(0, forced.SelectionCounters[7]);
            Equal("distance", forced.Rejected[0].Reason);
            var equality = HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [7, 7, 9],
                [new(7, 1, 1), new(9, 1, 1)], [], [new(0, 2, 100000, 0)], Options(h: 300000, distance: 100000), 0, 0);
            Equal(1, equality.Edges.Count); Equal(1, equality.SelectionCounters[7]);
        });
        check("pruning degree equality and separation from group selection counters", () =>
        {
            var same = HyperlanePruning.RunSuppliedCandidates([0, 1, 2, 3, 4], new int[5], [new(0, 0, 0)],
                Complete(5), [], Options(remove: 0), 0, 0);
            Equal(-1, same.PhysicalA[0].A); Equal(2, same.Rejected.Count);
            Equal(true, same.Rejected.All(r => r.Reason == "degree"));
            NativeCandidateEdge[] cross = Enumerable.Range(0, 4).SelectMany(a => Enumerable.Range(4, 4)
                .Select(b => new NativeCandidateEdge(a, b, 100000, 0))).ToArray();
            var result = HyperlanePruning.RunSuppliedCandidates(Enumerable.Range(0, 8).Select(i => (uint)i).ToArray(),
                [7, 7, 7, 7, 9, 9, 9, 9], [new(7, 100, 16), new(9, 100, 16)], [], cross, Options(), 0, 0);
            Equal(12, result.Edges.Count); Equal(16, result.SelectionCounters[7]);
            Equal(16, result.SelectionCounters[9]); Equal(16, result.Draws.Count);
            Equal(4, result.Rejected.Count); Equal(true, result.Rejected.All(r => r.Reason == "degree"));
        });
        check("pruning empty vectors, preflight errors and immutable state", () =>
        {
            var empty = HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [0, 0, 0], [new(0, 0, 0)], [], [], Options(), 0, 1);
            Equal(2, empty.GlobalDrawSites); Equal(0, empty.Draws.Count); Equal(0, empty.Edges.Count);
            Equal(0xEDE419C6u, empty.FinalStateA!.Value.Counter);
            Throws<NotSupportedException>(() => ((IList<GeneratedHyperlane>)empty.Edges).Clear());
            Throws<ArgumentOutOfRangeException>(() => HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [0, 0, 0], [new(0, 0, 0)], [], [], Options(), uint.MaxValue, 0));
            Throws<ArgumentOutOfRangeException>(() => HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [0, 0, 0], [new(0, 0, 0)], [], [], Options(h: 400000), 0, 0));
            Throws<ArgumentException>(() => HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [0, 0, 0], [new(0, 0, 0)],
                [new(0, 1, 100000, 0), new(1, 0, 100000, 0)], [], Options(), 0, 0));
            Throws<ArgumentException>(() => HyperlanePruning.RunSuppliedCandidates([0, 1, 2], [0, 0, 1], [new(0, 0, 0), new(1, 0, 0)],
                [], [new(0, 2, 100000, 0)], Options(), 0, 0));
        });
    }

    internal static void RunWithBackend(NativeTriangleBackend backend, Action<string, Action> check)
    {
        check("pruning N2 terminal bypass and explicit connectivity approximation", () =>
        {
            var two = CandidateHyperlaneGraph.Build([new(0, 0), new(1000000, 1000000)], [0, 1], [], [], null);
            var small = HyperlanePruning.Run(two, [], default, uint.MaxValue, uint.MaxValue);
            Equal(0, small.GlobalDrawSites); Equal(1414213L, small.Edges[0].Length);
            NativeFixedPoint[] points = [new(-100000, 0), new(0, 0), new(100000, 0)];
            var graph = CandidateHyperlaneGraph.Build(points, [0, 1, 2], [0, 0, 0], [new(0, 0)], backend);
            var pruned = HyperlanePruning.Run(graph, [new(0, 0)], Options(), 0, 0);
            var strict = HyperlaneConnectivity.Complete(graph, pruned, 5000000, HyperlaneConnectivityPolicy.Disabled);
            Equal(3, strict.ComponentCount); Equal(false, strict.IsConnected); Equal(0, strict.Repairs.Count);
            var connected = HyperlaneConnectivity.Complete(graph, pruned, 0, HyperlaneConnectivityPolicy.RestoreCandidatesThenNearestV1);
            Equal(1, connected.ComponentCount); Equal(2, connected.Repairs.Count);
            Equal(true, connected.Repairs.All(r => r.Reason == "nearest-component" && r.ExceedsMaximumDistance));
            Equal(4, connected.Adjacency.Count); Equal(true, connected.UsesApproximation);
            Equal("0,1;1,2", string.Join(';', connected.Edges.Select(e => $"{e.A},{e.B}")));
            Throws<NotSupportedException>(() => ((IList<HyperlaneConnectivityRepair>)connected.Repairs).Clear());
        });
    }
}
