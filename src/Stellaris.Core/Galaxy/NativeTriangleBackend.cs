using System.Diagnostics;
using System.Security.Cryptography;

namespace Stellaris.Core.Galaxy;

public enum TriangleCallPhase { Initial, Rebuild }
public readonly record struct NativeTriangle(int A, int B, int C);
public readonly record struct NativeTriangleNeighbors(int A, int B, int C);

/// <summary>Неизменённый ordered output Triangle; индексы относятся к supplied site order.</summary>
public sealed class NativeTessellation
{
    public IReadOnlyList<NativeFloatPoint> Sites { get; }
    public IReadOnlyList<NativeTriangle> Triangles { get; }
    public IReadOnlyList<NativeTriangleNeighbors> Neighbors { get; }
    public IReadOnlyList<NativeFloatPoint> VoronoiVertices { get; }
    public IReadOnlyList<NativeIncidentEdge> Edges { get; }
    public IReadOnlyList<NativeFloatPoint> VoronoiNormals { get; }

    internal NativeTessellation(NativeFloatPoint[] sites, NativeTriangle[] triangles,
        NativeTriangleNeighbors[] neighbors, NativeFloatPoint[] vertices,
        NativeIncidentEdge[] edges, NativeFloatPoint[] normals)
    {
        Sites = Array.AsReadOnly(sites);
        Triangles = Array.AsReadOnly(triangles);
        Neighbors = Array.AsReadOnly(neighbors);
        VoronoiVertices = Array.AsReadOnly(vertices);
        Edges = Array.AsReadOnly(edges);
        VoronoiNormals = Array.AsReadOnly(normals);
    }
}

public sealed class TriangleBackendException(string message) : Exception(message);

/// <summary>Triangle1.6/SINGLE с Win64 pointer port в isolated worker; без fallback и game RNG.</summary>
public sealed class NativeTriangleBackend
{
    public const int MaximumSites = 2047; // Проверенный one-block domain, не lobby define.
    private const int Magic = 0x31525453;
    private readonly string workerPath, expectedSha256, scratchRoot;
    private readonly TimeSpan timeout;

    public NativeTriangleBackend(string workerPath, string expectedSha256, string scratchDirectory, TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture !=
            System.Runtime.InteropServices.Architecture.X64)
            throw new PlatformNotSupportedException("Этот verified worker рассчитан на Windows x64.");
        if (!Path.IsPathFullyQualified(workerPath) || !Path.IsPathFullyQualified(scratchDirectory))
            throw new ArgumentException("Worker и scratch должны быть explicit absolute paths.");
        if (expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Требуется SHA256 built worker.", nameof(expectedSha256));
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        this.workerPath = Path.GetFullPath(workerPath);
        this.expectedSha256 = expectedSha256;
        scratchRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scratchDirectory));
        this.timeout = timeout;
        VerifyWorker();
    }

    public NativeTessellation Tessellate(ReadOnlySpan<NativeFloatPoint> sites, TriangleCallPhase phase)
    {
        if (sites.Length < 3 || sites.Length > MaximumSites)
            throw new ArgumentOutOfRangeException(nameof(sites), "Native-contract domain: 3..2047 sites.");
        if (phase is not (TriangleCallPhase.Initial or TriangleCallPhase.Rebuild))
            throw new ArgumentOutOfRangeException(nameof(phase));
        foreach (var site in sites)
            if (!float.IsFinite(site.X) || !float.IsFinite(site.Y) ||
                MathF.Abs(site.X) > 50000 || MathF.Abs(site.Y) > 50000)
                throw new ArgumentOutOfRangeException(nameof(sites), "Требуется finite input в bbox±50000.");
        // Upstream divide-and-conquer не имеет base case для одного unique vertex.
        // Это explicit safe rejection, а не имитация native fatal behavior.
        var first = sites[0];
        var hasDifferent = false;
        foreach (var site in sites) hasDifferent |= site.X != first.X || site.Y != first.Y;
        if (!hasDifferent) throw new ArgumentException("Один unique site вне safe Triangle domain.", nameof(sites));
        VerifyWorker();
        Directory.CreateDirectory(scratchRoot);
        var directory = Path.Combine(scratchRoot, "triangle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var completed = false;
        try
        {
            var inputPath = Path.Combine(directory, "input.bin");
            var outputPath = Path.Combine(directory, "output.bin");
            using (var input = new BinaryWriter(File.Create(inputPath)))
            {
                input.Write(Magic); input.Write(1); input.Write(sites.Length); input.Write((int)phase);
                foreach (var site in sites) { input.Write(site.X); input.Write(site.Y); }
            }
            var start = new ProcessStartInfo(workerPath)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(inputPath);
            start.ArgumentList.Add(outputPath);
            using var process = Process.Start(start) ?? throw new TriangleBackendException("Worker не запущен.");
            // Drain обоих pipes одновременно: non-Q rebuild печатает статистику.
            // Генерация синхронна; при множестве клиентов pool может быть занят ожидающими сессию.
            // Отдельные readers гарантируют drain pipes, не ожидая свободного thread-pool continuation.
            var stdout = Task.Factory.StartNew(() => process.StandardOutput.ReadToEnd(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var stderr = Task.Factory.StartNew(() => process.StandardError.ReadToEnd(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (!process.WaitForExit(checked((int)Math.Ceiling(timeout.TotalMilliseconds))))
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                if (!process.WaitForExit(5000))
                    throw new TriangleBackendException("Triangle worker не подтвердил завершение после timeout/kill.");
                if (!Task.WhenAll(stdout, stderr).Wait(TimeSpan.FromSeconds(5)))
                    throw new TriangleBackendException("Triangle worker pipes не закрылись после timeout/kill.");
                throw new TriangleBackendException("Triangle worker превысил timeout; fallback отсутствует.");
            }
            if (!Task.WhenAll(stdout, stderr).Wait(TimeSpan.FromSeconds(5)))
                throw new TriangleBackendException("Triangle worker pipes не закрылись после exit.");
            if (process.ExitCode != 0 || !File.Exists(outputPath))
                throw new TriangleBackendException($"Triangle worker завершился с кодом {process.ExitCode}; topology не опубликована.");
            var result = ReadOutput(outputPath, sites);
            completed = true;
            return result;
        }
        finally
        {
            // Удаляется только собственный уникальный child, абсолютный target проверен перед recursion.
            if (!Path.GetFullPath(directory).StartsWith(scratchRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Scratch child вышел за разрешённый root.");
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) when (!completed) { /* Сохраняем первоначальную backend error. */ }
            catch (UnauthorizedAccessException) when (!completed) { /* Сохраняем первоначальную backend error. */ }
        }
    }

    private void VerifyWorker()
    {
        using var stream = File.OpenRead(workerPath);
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new TriangleBackendException("SHA256 worker не совпадает с explicit build manifest.");
    }

    private static NativeTessellation ReadOutput(string path, ReadOnlySpan<NativeFloatPoint> input)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (reader.BaseStream.Length < 32 || reader.ReadInt32() != Magic || reader.ReadInt32() != 1)
            throw new TriangleBackendException("Некорректный Triangle protocol header.");
        var count = reader.ReadInt32();
        var trianglesCount = reader.ReadInt32();
        var edgesCount = reader.ReadInt32();
        var verticesCount = reader.ReadInt32();
        if (reader.ReadInt32() != 0 || reader.ReadInt32() != 0xA8 || count != input.Length ||
            trianglesCount < 0 || trianglesCount > 2 * count || edgesCount < 0 || edgesCount > 3 * count ||
            verticesCount != trianglesCount)
            throw new TriangleBackendException("Некорректные counts/FP/ABI Triangle output.");
        var expectedLength = 32L + 8L * count + 32L * trianglesCount + 24L * edgesCount;
        if (reader.BaseStream.Length != expectedLength)
            throw new TriangleBackendException("Некорректный размер Triangle output.");
        NativeFloatPoint Point()
        {
            var point = new NativeFloatPoint(reader.ReadSingle(), reader.ReadSingle());
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                throw new TriangleBackendException("Nonfinite Triangle output вне safe domain.");
            return point;
        }
        int Index(int limit, bool exterior = false)
        {
            var index = reader.ReadInt32();
            if (index < (exterior ? -1 : 0) || index >= limit)
                throw new TriangleBackendException("Некорректный индекс Triangle output.");
            return index;
        }
        var sites = new NativeFloatPoint[count];
        for (var i = 0; i < count; i++)
        {
            sites[i] = Point();
            if (BitConverter.SingleToInt32Bits(sites[i].X) != BitConverter.SingleToInt32Bits(input[i].X) ||
                BitConverter.SingleToInt32Bits(sites[i].Y) != BitConverter.SingleToInt32Bits(input[i].Y))
                throw new TriangleBackendException("Triangle изменил supplied site order/values.");
        }
        var triangles = new NativeTriangle[trianglesCount];
        for (var i = 0; i < triangles.Length; i++) triangles[i] = new(Index(count), Index(count), Index(count));
        var neighbors = new NativeTriangleNeighbors[trianglesCount];
        for (var i = 0; i < neighbors.Length; i++)
            neighbors[i] = new(Index(trianglesCount, true), Index(trianglesCount, true), Index(trianglesCount, true));
        var edges = new NativeIncidentEdge[edgesCount];
        for (var i = 0; i < edges.Length; i++) edges[i] = new(Index(count), Index(count), 0, 0);
        var vertices = new NativeFloatPoint[verticesCount];
        for (var i = 0; i < vertices.Length; i++) vertices[i] = Point();
        for (var i = 0; i < edges.Length; i++) edges[i] = edges[i] with
        { VoronoiA = Index(verticesCount, true), VoronoiB = Index(verticesCount, true) };
        var normals = new NativeFloatPoint[edgesCount];
        for (var i = 0; i < normals.Length; i++) normals[i] = Point();
        return new(sites, triangles, neighbors, vertices, edges, normals);
    }
}
