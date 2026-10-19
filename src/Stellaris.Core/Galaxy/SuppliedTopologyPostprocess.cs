namespace Stellaris.Core.Galaxy;

public readonly record struct NativeTriangleEdge(int A, int B);
public readonly record struct NativeCandidateEdge(int A, int B, long StoredLength, byte Flag);
public readonly record struct NativeAdjacency(uint SiteHandle, uint NeighborHandle, long Length, byte Flag);
public sealed record NativeCandidateSplit(IReadOnlyList<NativeCandidateEdge> SameGroup,
    IReadOnlyList<NativeCandidateEdge> DifferentGroup);

/// <summary>M13 postprocess supplied Triangle writeedges order. Не строит triangulation и не делает pruning.</summary>
public static class SuppliedTopologyPostprocess
{
    public static NativeCandidateSplit FilterAndSplit(ReadOnlySpan<NativeFixedPoint> points,
        ReadOnlySpan<int> mappedGroups, ReadOnlySpan<NativeTriangleEdge> orderedEdges)
    {
        if (points.Length < 3) throw new ArgumentException("Этот postprocess предназначен только для N>=3.", nameof(points));
        if (mappedGroups.Length != points.Length) throw new ArgumentException("Неверное число mapped groups.", nameof(mappedGroups));
        long minimum = long.MaxValue, maximum = long.MinValue;
        foreach (var point in points)
        {
            var radius = SquaredRadius(point);
            minimum = Math.Min(minimum, radius);
            maximum = Math.Max(maximum, radius);
        }
        var same = new List<NativeCandidateEdge>();
        var different = new List<NativeCandidateEdge>();
        foreach (var edge in orderedEdges)
        {
            if ((uint)edge.A >= points.Length || (uint)edge.B >= points.Length || edge.A == edge.B)
                throw new ArgumentException("Некорректные supplied Triangle indices.", nameof(orderedEdges));
            var middle = Midpoint(points[edge.A], points[edge.B]);
            var midRadius = SquaredRadius(middle);
            if (minimum > 0 && !(minimum < midRadius && midRadius < maximum)) continue;
            var distance = RawDistance(points[edge.A], points[edge.B]);
            var record = new NativeCandidateEdge(edge.A, edge.B, distance / 100000 * 100000, 0);
            (mappedGroups[edge.A] == mappedGroups[edge.B] ? same : different).Add(record);
        }
        return new(same.AsReadOnly(), different.AsReadOnly());
    }

    /// <summary>N<2 empty; N2 — два raw-length adjacency inserts, без Triangle/annulus/pruning.</summary>
    public static IReadOnlyList<NativeAdjacency> DirectSmallGraph(ReadOnlySpan<NativeFixedPoint> points,
        ReadOnlySpan<uint> handles)
    {
        if (handles.Length != points.Length) throw new ArgumentException("Неверное число handles.", nameof(handles));
        if (points.Length > 2) throw new NotSupportedException("N>=3 требует exact Triangle 1.6/SINGLE backend.");
        if (points.Length < 2) return Array.AsReadOnly(Array.Empty<NativeAdjacency>());
        if (handles[0] == handles[1]) throw new ArgumentException("Требуются разные handles.", nameof(handles));
        var length = RawDistance(points[0], points[1]);
        return Array.AsReadOnly(new[] { new NativeAdjacency(handles[0], handles[1], length, 0),
            new NativeAdjacency(handles[1], handles[0], length, 0) });
    }

    public static NativeFixedPoint Midpoint(NativeFixedPoint a, NativeFixedPoint b) =>
        new(checked(a.X + b.X) / 2, checked(a.Y + b.Y) / 2);

    public static long RawDistance(NativeFixedPoint a, NativeFixedPoint b)
    {
        var dx = (Int128)checked(b.X - a.X);
        var dy = (Int128)checked(b.Y - a.Y);
        var squared = (UInt128)(dx * dx) + (UInt128)(dy * dy);
        if (squared == 0) return 0;
        var root = squared;
        var next = (root + 1) / 2;
        while (next < root)
        {
            root = next;
            next = (root + squared / root) / 2;
        }
        return checked((long)root);
    }

    private static long SquaredRadius(NativeFixedPoint p) =>
        checked(NativeFixedMath.Multiply(p.X, p.X) + NativeFixedMath.Multiply(p.Y, p.Y));
}
