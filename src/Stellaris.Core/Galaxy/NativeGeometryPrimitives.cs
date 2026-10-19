using System.Runtime.CompilerServices;

namespace Stellaris.Core.Galaxy;

public readonly record struct NativeFloatPoint(float X, float Y);
public readonly record struct NativeFixedPoint(long X, long Y);
public readonly record struct NativePolygonCentroid(NativeFloatPoint Position, double SignedArea);

/// <summary>M11/M13 dependency-free primitives. Не строят Triangle tessellation и не исполняют relaxation.</summary>
public static class NativeGeometryPrimitives
{
    /// <summary>M13 input: signed integer truncation до binary32 conversion, не округление fixed напрямую.</summary>
    public static NativeFloatPoint QuantizeTriangleInput(NativeFixedPoint point) =>
        new((float)(point.X / 100000), (float)(point.Y / 100000));

    /// <summary>M11 ordered polygon fan. Zero-area guard намеренно отсутствует согласно native evidence.</summary>
    public static NativePolygonCentroid Centroid(ReadOnlySpan<NativeFloatPoint> vertices)
    {
        if (vertices.Length < 2) throw new ArgumentException("Нужно минимум две точки.", nameof(vertices));
        foreach (var p in vertices)
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y))
                throw new ArgumentOutOfRangeException(nameof(vertices), "Входные координаты должны быть конечными.");
        var origin = vertices[0];
        double sumCross = 0, sumX = 0, sumY = 0;
        for (var i = 1; i < vertices.Length - 1; i++)
        {
            var a = vertices[i];
            var b = vertices[i + 1];
            var cross = Sub(Mul(Sub(a.X, origin.X), Sub(b.Y, origin.Y)),
                Mul(Sub(a.Y, origin.Y), Sub(b.X, origin.X)));
            var x = Mul(Add(Add(origin.X, a.X), b.X), cross);
            var y = Mul(Add(Add(origin.Y, a.Y), b.Y), cross);
            sumCross += cross;
            sumX += x;
            sumY += y;
        }
        var divisor = 3 * sumCross;
        return new(new((float)(sumX / divisor), (float)(sumY / divisor)), sumCross / 2);
    }

    // Каждая native float instruction — отдельный rounding stage; не сливать в FMA/double expression.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Mul(float a, float b) => a * b;
}
