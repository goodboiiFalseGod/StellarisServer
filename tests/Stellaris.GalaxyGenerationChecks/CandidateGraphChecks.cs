using Stellaris.Core.Galaxy;

internal static class CandidateGraphChecks
{
    private static CandidateGroupInput[] Groups(params int[] ids) => ids.Select(id => new CandidateGroupInput(id, 0)).ToArray();
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Ожидалось {typeof(T).Name}");
    }
    internal static void Run(NativeTriangleBackend backend, Action<string, Action> check)
    {
        check("candidate graph small paths bypass Triangle and retain raw length", () =>
        {
            var empty = CandidateHyperlaneGraph.Build([], [], [], [], null);
            Equal(0, empty.ComponentCount); Equal(0, empty.TriangleCalls);
            var one = CandidateHyperlaneGraph.Build([new(0, 0)], [1], [], [], null);
            Equal(1, one.ComponentCount); Equal(0, one.DirectAdjacency.Count);
            var two = CandidateHyperlaneGraph.Build([new(0, 0), new(1000000, 1000000)], [7, 8], [], [], null);
            Equal(0, two.TriangleCalls); Equal(CandidateGraphPath.SmallDirect, two.Path);
            Equal(1414213L, two.DirectAdjacency[0].Length);
            Equal(new NativeAdjacency(7, 8, 1414213, 0), two.DirectAdjacency[0]);
            Equal(new NativeAdjacency(8, 7, 1414213, 0), two.DirectAdjacency[1]);
            Equal(1, two.ComponentCount); Equal(0, two.Candidates.DifferentGroup.Count);
            Equal(0, two.InterGroupCounterDeltas.Count); Equal(0, two.GroupCounters.Count);
        });
        check("candidate graph actual ordered Triangle, explicit group split and reciprocal adjacency", () =>
        {
            NativeFixedPoint[] points = [new(0, 0), new(1000000, 0), new(1000000, 1000000), new(0, 1000000)];
            uint[] handles = [100, 101, 102, 103]; int[] groups = [1, 1, 2, 2];
            CandidateGroupInput[] records = [new(1, 7), new(2, 9), new(3, 11)];
            var result = CandidateHyperlaneGraph.Build(points, handles, groups, records, backend);
            Equal("3,0;0,1;1,3;1,2;2,3", string.Join(';', result.OrderedTriangleEdges.Select(e => $"{e.A},{e.B}")));
            Equal("0,1;2,3", string.Join(';', result.Candidates.SameGroup.Select(e => $"{e.A},{e.B}")));
            Equal("3,0;1,3;1,2", string.Join(';', result.Candidates.DifferentGroup.Select(e => $"{e.A},{e.B}")));
            Equal(1400000L, result.Candidates.DifferentGroup[1].StoredLength);
            Equal(10, result.DiagnosticCandidateAdjacency.Count); Equal(0, result.DirectAdjacency.Count); Equal(1, result.ComponentCount);
            Equal(3, result.InterGroupCounterDeltas[1]); Equal(3, result.InterGroupCounterDeltas[2]);
            Equal(new CandidateGroupCounter(7, 3, 10), result.GroupCounters[1]);
            Equal(new CandidateGroupCounter(9, 3, 12), result.GroupCounters[2]);
            Equal(new CandidateGroupCounter(11, 0, 11), result.GroupCounters[3]);
            Equal(new CandidateGroupInput(1, 7), records[0]);
            Throws<NotSupportedException>(() => ((IDictionary<int, int>)result.InterGroupCounterDeltas).Clear());
            Equal(true, result.RequiresPruningAndBridges);
            foreach (var edge in result.DiagnosticCandidateAdjacency)
                Equal(true, result.DiagnosticCandidateAdjacency.Contains(new(edge.NeighborHandle, edge.SiteHandle, edge.Length, edge.Flag)));
            points[0] = new(999, 999); handles[0] = 555; groups[0] = 999;
            Equal(new NativeFixedPoint(0, 0), result.Positions[0]); Equal(100u, result.Handles[0]); Equal(1, result.MappedGroups[0]);
            Throws<NotSupportedException>(() => ((IList<NativeAdjacency>)result.DiagnosticCandidateAdjacency).Clear());
        });
        check("candidate graph quantization, annulus gaps, collinear components and invalid inputs", () =>
        {
            NativeFixedPoint[] points = [new(199999, 100000), new(199999, -100000), new(-100000, 0)];
            var result = CandidateHyperlaneGraph.Build(points, [0, 1, 2], [0, 0, 0], Groups(0), backend);
            Equal(3, result.OrderedTriangleEdges.Count);
            Equal(true, result.Candidates.SameGroup.All(e => e.StoredLength % 100000 == 0));
            var collinear = CandidateHyperlaneGraph.Build([new(-100000, 0), new(0, 0), new(100000, 0)], [0, 1, 2], [0, 0, 0], Groups(0), backend);
            Equal(CandidateGraphPath.DegenerateZeroTopology, collinear.Path);
            Equal(3, collinear.ComponentCount); Equal(0, collinear.DiagnosticCandidateAdjacency.Count);
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build([new(0, 0)], [], [], [], null));
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build([new(0, 0), new(1, 1)], [0, 0], [0, 0], Groups(0), null));
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build([new(0, 0)], [uint.MaxValue], [0], Groups(0), null));
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build(points, [0, 1, 2], [-1, 0, 0], Groups(0), null));
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build(points, [0, 1, 2], [1, 0, 0], Groups(0), null));
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build(points, [0, 1, 2], [0, 0, 0], Groups(0, 0), null));
            Throws<OverflowException>(() => CandidateHyperlaneGraph.Build(points, [0, 1, 2], [0, 0, 0], [new(0, int.MaxValue)], backend));
            Throws<ArgumentOutOfRangeException>(() => CandidateHyperlaneGraph.Build([new(50000001, 0)], [0], [0], Groups(0), null));
            Throws<ArgumentNullException>(() => CandidateHyperlaneGraph.Build([new(0, 0), new(100000, 0), new(0, 100000)], [0, 1, 2], [0, 0, 0], Groups(0), null));
            Throws<ArgumentException>(() => CandidateHyperlaneGraph.Build([new(0, 0), new(1, 0), new(0, 1)], [0, 1, 2], [0, 0, 0], Groups(0), backend));
        });
    }
}
