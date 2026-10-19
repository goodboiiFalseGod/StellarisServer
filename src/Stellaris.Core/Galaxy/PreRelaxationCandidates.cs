namespace Stellaris.Core.Galaxy;

public sealed record GalaxyArms(int Count, long Tightness, long Width, long Fuzz, long Separation);
public sealed record GalaxyRing(long Width, long Offset);
public sealed record PreRelaxationShape(int OuterStars, long CoreRadius, long OuterSpan,
    long MinimumDistance, GalaxyArms? Arms, GalaxyRing? Ring);
public readonly record struct PreRelaxationCandidate(long X, long Y, long Radius, long Angle, int Arm);
public readonly record struct PreRelaxationDrawResult(PreRelaxationCandidate Candidate, NativeRandomState NextState);
public readonly record struct PreRelaxationRotation(long Angle, NativeRandomState NextState);

/// <summary>Только raw candidate stage. Никаких acceptance/allocator/clamp/relaxation/world writes.</summary>
public static class PreRelaxationCandidates
{
    public static PreRelaxationRotation InitializeRotation(PreRelaxationShape shape, NativeRandomState state)
    {
        Validate(shape);
        if (shape.Arms is null) return new(0, state);
        var a = NativeRandom.Next(state);
        var b = NativeRandom.Next(a.NextState);
        return new(EvaluateArmsRotation(a.Value, b.Value), b.NextState);
    }

    public static long EvaluateArmsRotation(uint firstDraw, uint secondDraw) =>
        checked(2 * NativeFixedMath.Multiply(Unit(firstDraw, secondDraw), 314200));

    public static PreRelaxationDrawResult Next(PreRelaxationShape shape, int acceptedIndex,
        long rotation, NativeRandomState state)
    {
        Validate(shape);
        if (acceptedIndex < 0) throw new ArgumentOutOfRangeException(nameof(acceptedIndex));
        Span<uint> draws = stackalloc uint[shape.Arms is null ? 4 : 7];
        foreach (ref var draw in draws)
        {
            var next = NativeRandom.Next(state);
            draw = next.Value;
            state = next.NextState;
        }
        return new(Evaluate(shape, acceptedIndex, rotation, draws), state);
    }

    public static PreRelaxationCandidate Evaluate(PreRelaxationShape shape, int acceptedIndex,
        long rotation, ReadOnlySpan<uint> draws)
    {
        Validate(shape);
        if (acceptedIndex < 0) throw new ArgumentOutOfRangeException(nameof(acceptedIndex));
        if (draws.Length != (shape.Arms is null ? 4 : 7)) throw new ArgumentException("Неверное число draws.", nameof(draws));
        foreach (var draw in draws)
            if (draw > 0x7FFFFFFF) throw new ArgumentOutOfRangeException(nameof(draws), "Требуются uint31 draws.");
        checked
        {
            var radial = Unit(draws[0], draws[1]);
            var radius = shape.Ring is { } ring
                ? shape.CoreRadius + ring.Offset + NativeFixedMath.Multiply(ring.Width, radial)
                : acceptedIndex < shape.OuterStars
                    ? shape.CoreRadius + NativeFixedMath.Multiply(shape.OuterSpan, radial)
                    : NativeFixedMath.Multiply(shape.CoreRadius, radial);
            long degrees;
            var arm = -1;
            if (shape.Arms is { } arms)
            {
                var twist = shape.OuterSpan == 0 ? 0
                    : NativeFixedMath.Multiply(arms.Tightness, NativeFixedMath.Divide(radius, shape.OuterSpan));
                var width = NativeFixedMath.Multiply(arms.Width, Unit(draws[2], draws[3]));
                arm = (int)(draws[4] % arms.Count);
                var fuzz = 2 * NativeFixedMath.Multiply(arms.Fuzz, Unit(draws[5], draws[6])) - arms.Fuzz;
                degrees = twist + width + arm * arms.Separation + fuzz;
            }
            else
            {
                if (rotation != 0) throw new ArgumentException("No-arms rotation должна быть нулём.", nameof(rotation));
                degrees = NativeFixedMath.Multiply(36000000, Unit(draws[2], draws[3]));
            }
            var angle = (NativeFixedMath.DegreesToRadians(degrees) + rotation) % NativeFixedMath.Period;
            return new(NativeFixedMath.Multiply(radius, NativeFixedMath.Cos(angle)),
                NativeFixedMath.Multiply(radius, NativeFixedMath.Sin(angle)), radius, angle, arm);
        }
    }

    private static long Unit(uint first, uint second)
    {
        if (first > 0x7FFFFFFF || second > 0x7FFFFFFF) throw new ArgumentOutOfRangeException(nameof(first));
        return (long)((((ulong)first << 32) | second) % 100000);
    }

    private static void Validate(PreRelaxationShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.OuterStars < 0 || shape.CoreRadius < 0 || shape.OuterSpan < 0 || shape.MinimumDistance < 0 ||
            (shape.Ring is { } ring && (ring.Width < 0 || ring.Offset < 0)) ||
            (shape.Arms is { } arms && (arms.Count <= 0 || arms.Width < 0 || arms.Fuzz < 0)))
            throw new ArgumentOutOfRangeException(nameof(shape), "Неподдержанные raw shape parameters.");
    }
}
