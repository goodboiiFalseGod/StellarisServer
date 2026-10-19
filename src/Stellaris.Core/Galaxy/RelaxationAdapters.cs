namespace Stellaris.Core.Galaxy;

public readonly record struct RelaxationAdapterSettings(int CenterDummyPoints, int Iterations);

/// <summary>Immutable ordered input: ordinary prefix, затем dummy suffix. Это не Triangle output.</summary>
public sealed class PreparedRelaxationInput
{
    public int OrdinaryCount { get; }
    public int DummyCount => Settings.CenterDummyPoints;
    public RelaxationAdapterSettings Settings { get; }
    public IReadOnlyList<NativeFloatPoint> Sites { get; }

    internal PreparedRelaxationInput(int ordinaryCount, RelaxationAdapterSettings settings, NativeFloatPoint[] sites)
    {
        OrdinaryCount = ordinaryCount;
        Settings = settings;
        Sites = Array.AsReadOnly(sites);
    }
}

/// <summary>M11 input/output adapters по замороженному §16.5. Нет RNG, Triangle, iteration или materialization.</summary>
public static class RelaxationAdapters
{
    public const float SiteBound = 50000;

    public static PreparedRelaxationInput PrepareInput(ReadOnlySpan<NativeFixedPoint> ordinaryStored,
        long coreRadius, RelaxationAdapterSettings settings)
    {
        if (coreRadius <= 0) throw new ArgumentOutOfRangeException(nameof(coreRadius), "Требуется положительный resolved vanilla core radius.");
        if (settings.CenterDummyPoints < 0 || settings.Iterations < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Defines должны быть неотрицательны.");
        var count = checked(ordinaryStored.Length + settings.CenterDummyPoints);
        var sites = new NativeFloatPoint[count];
        for (var i = 0; i < ordinaryStored.Length; i++)
            sites[i] = new(ConvertInputCoordinate(ordinaryStored[i].X), ConvertInputCoordinate(ordinaryStored[i].Y));
        if (settings.CenterDummyPoints > 0)
        {
            var step = 62831800000L / checked((long)settings.CenterDummyPoints * 100000);
            for (var i = 0; i < settings.CenterDummyPoints; i++)
            {
                var angle = checked((long)i * step);
                var point = new NativeFloatPoint(DummyComponent(NativeFixedMath.Sin(angle), coreRadius),
                    DummyComponent(NativeFixedMath.Cos(angle), coreRadius));
                RequireValidSite(point);
                sites[ordinaryStored.Length + i] = point;
            }
        }
        return new(ordinaryStored.Length, settings, sites);
    }

    public static IReadOnlyList<NativeFixedPoint> RestoreOrdinaryCoordinates(PreparedRelaxationInput prepared,
        ReadOnlySpan<NativeFloatPoint> suppliedSites)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (suppliedSites.Length != prepared.Sites.Count)
            throw new ArgumentException("Supplied site count не совпадает с prepared input.", nameof(suppliedSites));
        foreach (var site in suppliedSites) RequireValidSite(site);
        var result = new NativeFixedPoint[prepared.OrdinaryCount];
        for (var i = 0; i < result.Length; i++)
            result[i] = new(ConvertOutputCoordinate(suppliedSites[i].X), ConvertOutputCoordinate(suppliedSites[i].Y));
        return Array.AsReadOnly(result);
    }

    public static float ConvertInputCoordinate(long raw)
    {
        if (raw < -50000000 || raw > 50000000)
            throw new ArgumentOutOfRangeException(nameof(raw), "Требуется stored coordinate в native clamp bounds.");
        return FixedToIntegerFloat(NativeFixedMath.Multiply(raw, 10000000));
    }

    public static long ConvertOutputCoordinate(float coordinate)
    {
        RequireValidCoordinate(coordinate);
        var truncated = checked((int)coordinate); // cvttss2si для finite поддержанного bbox.
        var raw = checked((long)truncated * 100000);
        return NativeFixedMath.Divide(raw, 10000000); // FixedDiv на100.0, не clamp.
    }

    private static float DummyComponent(long trig, long coreRadius)
    {
        var raw = NativeFixedMath.Multiply(trig, coreRadius);
        raw = NativeFixedMath.Multiply(raw, 90000);
        raw = NativeFixedMath.Multiply(raw, 10000000);
        return FixedToIntegerFloat(raw);
    }

    private static float FixedToIntegerFloat(long raw) => (float)checked((int)(raw / 100000));

    private static void RequireValidSite(NativeFloatPoint site)
    {
        RequireValidCoordinate(site.X);
        RequireValidCoordinate(site.Y);
    }

    private static void RequireValidCoordinate(float coordinate)
    {
        if (!float.IsFinite(coordinate) || coordinate < -SiteBound || coordinate > SiteBound)
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Требуется finite Triangle site в inclusive bbox±50000.");
    }
}
