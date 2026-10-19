namespace Stellaris.Core.Galaxy;

public sealed record NativeRelaxationResult(
    IReadOnlyList<NativeFloatPoint> Sites,
    IReadOnlyList<NativeFixedPoint> OrdinaryCoordinates,
    NativeTessellation FinalTessellation,
    int TessellationCalls);

/// <summary>M11 loop: initial Triangle, centroid pass, rebuild после каждого pass.</summary>
public static class NativeGalaxyRelaxation
{
    public static NativeRelaxationResult Run(PreparedRelaxationInput prepared, NativeTriangleBackend backend)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(backend);
        var sites = prepared.Sites.ToArray();
        var tessellation = backend.Tessellate(sites, TriangleCallPhase.Initial);
        for (var iteration = 0; iteration < prepared.Settings.Iterations; iteration++)
        {
            sites = SuppliedTessellationPass.Apply(sites, tessellation.VoronoiVertices.ToArray(),
                tessellation.Edges.ToArray()).ToArray();
            tessellation = backend.Tessellate(sites, TriangleCallPhase.Rebuild);
        }
        return new(Array.AsReadOnly(sites), RelaxationAdapters.RestoreOrdinaryCoordinates(prepared, sites),
            tessellation, checked(prepared.Settings.Iterations + 1));
    }
}
