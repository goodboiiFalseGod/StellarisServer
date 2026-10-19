using System.Security.Cryptography;
using Stellaris.Core.Galaxy;

internal static class TriangleBackendChecks
{
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Ожидалось {expected}, получено {actual}");
    }
    private static void Throws<T>(Action body) where T : Exception
    {
        try { body(); }
        catch (T) { return; }
        throw new Exception($"Ожидалось {typeof(T).Name}");
    }
    internal static string Fingerprint(NativeTessellation output) =>
        string.Join(';', output.Edges.Select(e => $"{e.SiteA},{e.SiteB},{e.VoronoiA},{e.VoronoiB}")) + "|" +
        string.Join(';', output.VoronoiVertices.Select(p => $"{BitConverter.SingleToInt32Bits(p.X):X8},{BitConverter.SingleToInt32Bits(p.Y):X8}"));

    internal static void Run(NativeTriangleBackend backend, Action<string, Action> check, string workerPath)
    {
        NativeFloatPoint[] triangle = [new(0, 0), new(10, 0), new(0, 10)];
        NativeFloatPoint[] square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        check("Triangle source-backed ordered primal edges and duplicate survivor", () =>
        {
            var vectors = new (NativeFloatPoint[] Sites, string Edges)[]
            {
                (triangle, "0,1;1,2;2,0"),
                (square, "3,0;0,1;1,3;1,2;2,3"),
                ([new(0,10),new(10,10),new(10,0),new(0,0)], "0,3;3,2;2,0;2,1;1,0"),
                ([new(0,0),new(10,0),new(0,10),new(0,0)], "3,1;1,2;2,3")
            };
            foreach (var vector in vectors)
            {
                var result = backend.Tessellate(vector.Sites, TriangleCallPhase.Initial);
                Equal(vector.Edges, string.Join(';', result.Edges.Select(e => $"{e.SiteA},{e.SiteB}")));
                Equal(true, vector.Sites.SequenceEqual(result.Sites));
                Equal(result.Triangles.Count, result.Neighbors.Count);
                Throws<NotSupportedException>(() => ((IList<NativeIncidentEdge>)result.Edges)[0] = default);
            }
        });
        check("Triangle geometric Voronoi center and exterior rays", () =>
        {
            var result = backend.Tessellate(triangle, TriangleCallPhase.Initial);
            Equal(1, result.VoronoiVertices.Count);
            Equal(new NativeFloatPoint(5, 5), result.VoronoiVertices[0]);
            Equal(true, result.Edges.All(e => e.VoronoiA == 0 && e.VoronoiB == -1));
            Equal(true, triangle.SequenceEqual(SuppliedTessellationPass.Apply(triangle,
                result.VoronoiVertices.ToArray(), result.Edges.ToArray())));
            Equal(new NativeFloatPoint(0, -10), result.VoronoiNormals[0]);
            Equal(new NativeFloatPoint(10, 10), result.VoronoiNormals[1]);
            Equal(new NativeFloatPoint(-10, 0), result.VoronoiNormals[2]);
        });
        check("Triangle independent full arrays, dual edge slots and sensitive Voronoi bits", () =>
        {
            // Frozen reverse104AC035 §19.7; source-side vectors, не embedded runtime capture.
            var result = backend.Tessellate(square, TriangleCallPhase.Initial);
            Equal("3,0,1;1,2,3", string.Join(';', result.Triangles.Select(t => $"{t.A},{t.B},{t.C}")));
            Equal("-1,1,-1;-1,0,-1", string.Join(';', result.Neighbors.Select(t => $"{t.A},{t.B},{t.C}")));
            Equal("3,0,0,-1;0,1,0,-1;1,3,0,1;1,2,1,-1;2,3,1,-1|40A00000,40A00000;40A00000,40A00000",
                Fingerprint(result));
            NativeFloatPoint[] squareNormals = [new(-10,0), new(0,-10), new(0,0), new(10,0), new(0,10)];
            Equal(true, squareNormals.SequenceEqual(result.VoronoiNormals));
            NativeFloatPoint[] five = [new(0,0), new(10,0), new(10,10), new(0,10), new(4,3)];
            var fiveResult = backend.Tessellate(five, TriangleCallPhase.Initial);
            Equal("3,0,4;4,1,2;1,4,0;4,2,3", string.Join(';', fiveResult.Triangles.Select(t => $"{t.A},{t.B},{t.C}")));
            Equal("2,3,-1;-1,3,2;0,-1,1;-1,0,1", string.Join(';', fiveResult.Neighbors.Select(t => $"{t.A},{t.B},{t.C}")));
            Equal("3,0,0,-1;0,4,0,2;4,3,0,3;4,1,1,2;1,2,1,-1;2,4,1,3;0,1,2,-1;2,3,3,-1|BF200000,40A00000;410C0000,40A00000;409FFFFF,C0200001;40A00000,41036DB7",
                Fingerprint(fiveResult));
            NativeFloatPoint[] fiveNormals = [new(-10,0), new(0,0), new(0,0), new(0,0),
                new(10,0), new(0,0), new(0,-10), new(0,10)];
            Equal(true, fiveNormals.SequenceEqual(fiveResult.VoronoiNormals));
        });
        check("Triangle repeat, phase and parallel process isolation", () =>
        {
            var expected = Fingerprint(backend.Tessellate(square, TriangleCallPhase.Initial));
            Equal(expected, Fingerprint(backend.Tessellate(square, TriangleCallPhase.Rebuild)));
            var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
                Fingerprint(backend.Tessellate(square, TriangleCallPhase.Initial)))).ToArray();
            Task.WaitAll(tasks);
            foreach (var task in tasks) Equal(expected, task.Result);
        });
        check("Triangle exact-sign diagonal across one-ulp cocircular perturbation", () =>
        {
            foreach (var outside in new[] { false, true })
            {
                var y = outside ? MathF.BitIncrement(10) : MathF.BitDecrement(10);
                NativeFloatPoint[] sites = [new(0,0), new(10,0), new(10,10), new(0,y)];
                var result = backend.Tessellate(sites, TriangleCallPhase.Initial);
                var diagonal = result.Edges.Single(e => e.VoronoiA >= 0 && e.VoronoiB >= 0);
                Equal(outside ? "0,2" : "1,3", string.Join(',', new[] { diagonal.SiteA, diagonal.SiteB }.Order()));
            }
        });
        check("Triangle rejects invalid domain and wrong executable provenance", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => backend.Tessellate([new(0, 0)], TriangleCallPhase.Initial));
            Throws<ArgumentOutOfRangeException>(() => backend.Tessellate([new(float.NaN, 0), new(1, 0), new(0, 1)], TriangleCallPhase.Initial));
            Throws<ArgumentOutOfRangeException>(() => backend.Tessellate([new(50001, 0), new(1, 0), new(0, 1)], TriangleCallPhase.Initial));
            Throws<ArgumentOutOfRangeException>(() => backend.Tessellate(triangle, (TriangleCallPhase)99));
            Throws<ArgumentOutOfRangeException>(() => backend.Tessellate(new NativeFloatPoint[2048], TriangleCallPhase.Initial));
            Throws<ArgumentException>(() => backend.Tessellate([new(0, 0), new(0, 0), new(0, 0)], TriangleCallPhase.Initial));
            Throws<TriangleBackendException>(() => new NativeTriangleBackend(workerPath, new string('0', 64),
                Path.Combine(AppContext.BaseDirectory, "triangle-scratch"), TimeSpan.FromSeconds(30)));
        });
        check("Triangle collinear returns zero topology and pins sites", () =>
        {
            NativeFloatPoint[] sites = [new(-1, 0), new(0, 0), new(1, 0), new(0, 0)];
            var result = backend.Tessellate(sites, TriangleCallPhase.Initial);
            Equal(0, result.Edges.Count);
            Equal(0, result.Triangles.Count);
            Equal(0, result.VoronoiVertices.Count);
            Equal(true, sites.SequenceEqual(SuppliedTessellationPass.Apply(sites, [], [])));
        });
        check("Triangle subprocess nonzero, dual pipe drain, timeout and corrupt protocol", () =>
        {
            var fixtureWorker = Path.Combine(Path.GetDirectoryName(workerPath)!, "triangle-failure-worker.exe");
            if (!File.Exists(fixtureWorker)) throw new Exception("Build worker с -WithTestFixtures обязателен для native checks.");
            var root = Path.Combine(AppContext.BaseDirectory, "triangle-failures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                foreach (var mode in new[] { "exit", "crash", "timeout", "corrupt" })
                {
                    var executable = Path.Combine(root, mode + ".exe");
                    File.Copy(fixtureWorker, executable);
                    var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable)));
                    var scratch = Path.Combine(root, "scratch");
                    var failure = new NativeTriangleBackend(executable, sha, scratch,
                        mode == "timeout" ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(5));
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        failure.Tessellate(triangle, TriangleCallPhase.Initial);
                        throw new Exception("Ожидалась backend failure.");
                    }
                    catch (TriangleBackendException error)
                    {
                        var marker = mode switch { "exit" => "42", "crash" => "-1073741819", "timeout" => "timeout", _ => "counts/FP/ABI" };
                        Equal(true, error.Message.Contains(marker, StringComparison.Ordinal));
                    }
                    Equal(true, timer.Elapsed < TimeSpan.FromSeconds(3));
                    Equal(0, Directory.GetDirectories(scratch).Length);
                }
            }
            finally
            {
                if (Path.GetDirectoryName(root) == AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
                    Directory.Delete(root, true);
            }
        });
        check("Triangle zero and multiple relaxation passes rebuild final topology", () =>
        {
            NativeFixedPoint[] stored = [new(-100000, -100000), new(100000, -100000),
                new(100000, 100000), new(-100000, 100000), new(20000, 10000)];
            var zero = RelaxationAdapters.PrepareInput(stored, 2500000, new(0, 0));
            var zeroResult = NativeGalaxyRelaxation.Run(zero, backend);
            Equal(1, zeroResult.TessellationCalls);
            Equal(true, stored.SequenceEqual(zeroResult.OrdinaryCoordinates));
            var prepared = RelaxationAdapters.PrepareInput(stored, 2500000, new(0, 2));
            var result = NativeGalaxyRelaxation.Run(prepared, backend);
            Equal(3, result.TessellationCalls);
            Equal(true, prepared.Sites.SequenceEqual(zero.Sites));
            for (var i = 0; i < 4; i++) Equal(prepared.Sites[i], result.Sites[i]);
            Equal(false, prepared.Sites[4] == result.Sites[4]);
            Equal(true, result.Sites.SequenceEqual(result.FinalTessellation.Sites));
            Equal(Fingerprint(backend.Tessellate(result.Sites.ToArray(), TriangleCallPhase.Initial)), Fingerprint(result.FinalTessellation));
        });
    }
}
