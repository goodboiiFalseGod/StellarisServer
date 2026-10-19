namespace Stellaris.Core.Galaxy;

public readonly record struct NativeIncidentEdge(int SiteA, int SiteB, int VoronoiA, int VoronoiB);

/// <summary>Один M11 pass над уже предоставленным exact Triangle output, без самого backend.</summary>
public static class SuppliedTessellationPass
{
    private readonly record struct Segment(int Start, int End);

    public static IReadOnlyList<NativeFloatPoint> Apply(ReadOnlySpan<NativeFloatPoint> inputSites,
        ReadOnlySpan<NativeFloatPoint> voronoiVertices, ReadOnlySpan<NativeIncidentEdge> orderedEdges)
    {
        foreach (var site in inputSites) RequireFinite(site);
        foreach (var vertex in voronoiVertices) RequireFinite(vertex);
        foreach (var edge in orderedEdges)
        {
            if ((uint)edge.SiteA >= inputSites.Length || (uint)edge.SiteB >= inputSites.Length || edge.SiteA == edge.SiteB ||
                edge.VoronoiA >= voronoiVertices.Length || edge.VoronoiB >= voronoiVertices.Length)
                throw new ArgumentException("Некорректные индексы Triangle output.", nameof(orderedEdges));
        }
        var result = inputSites.ToArray();
        for (var site = 0; site < inputSites.Length; site++)
        {
            var segments = new List<Segment>();
            var pinned = false;
            foreach (var edge in orderedEdges)
            {
                if (edge.SiteA != site && edge.SiteB != site) continue;
                if (edge.VoronoiA < 0 || edge.VoronoiB < 0) { pinned = true; break; }
                segments.Add(edge.SiteA == site
                    ? new(edge.VoronoiA, edge.VoronoiB)
                    : new(edge.VoronoiB, edge.VoronoiA));
            }
            if (pinned || segments.Count <= 1) continue;
            for (var k = 1; k < segments.Count; k++)
            {
                var match = k;
                while (match < segments.Count && segments[match].Start != segments[k - 1].End) match++;
                if (match == segments.Count) { pinned = true; break; }
                (segments[k], segments[match]) = (segments[match], segments[k]);
            }
            if (pinned) continue;
            var polygon = new NativeFloatPoint[segments.Count + 1];
            polygon[0] = voronoiVertices[segments[0].Start];
            for (var k = 0; k < segments.Count; k++) polygon[k + 1] = voronoiVertices[segments[k].End];
            foreach (var vertex in polygon)
                if (vertex.X < -50000 || vertex.X > 50000 || vertex.Y < -50000 || vertex.Y > 50000)
                { pinned = true; break; }
            if (pinned) continue;
            // Native не проверяет замыкание: P[m] считается closing point и отбрасывается unconditional.
            result[site] = NativeGeometryPrimitives.Centroid(polygon.AsSpan(0, segments.Count)).Position;
        }
        return Array.AsReadOnly(result);
    }

    private static void RequireFinite(NativeFloatPoint point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point), "Требуются конечные Triangle input/output vertices.");
    }
}
