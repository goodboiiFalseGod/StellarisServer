namespace Stellaris.Core.Galaxy;

/// <summary>Pre-relaxation fixed helpers 4.5.2, scale100000. Wide products, отдельное усечение каждого FM.</summary>
public static class NativeFixedMath
{
    public const long Scale = 100000;
    public const long Period = 628319;

    public static long Multiply(long a, long b) => checked((long)((Int128)a * b / Scale));

    public static long Divide(long a, long b)
    {
        if (b == 0) throw new DivideByZeroException();
        return checked((long)((Int128)a * Scale / b));
    }

    public static long DegreesToRadians(long degrees) => Multiply(degrees, 314159) / 180;

    public static long Sin(long angle)
    {
        var x = angle % Period;
        if (x < 0) x += Period;
        var sign = 1;
        if (x > 314159) { x -= 314159; sign = -1; }
        if (x > 157080) x = 314160 - x;
        // power progression строго FM(x, FM(x,power)); алгебраически не сворачивать.
        var power3 = Multiply(x, Multiply(x, x));
        var power5 = Multiply(x, Multiply(x, power3));
        return sign * (x - power3 / 6 + power5 / 120);
    }

    public static long Cos(long angle) => Sin(checked(angle + 157080));
}
