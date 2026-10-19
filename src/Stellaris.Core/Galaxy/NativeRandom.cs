namespace Stellaris.Core.Galaxy;

/// <summary>Native state, предоставленный вызывающей стороной; это не world seed.</summary>
public readonly record struct NativeRandomState(uint SeedBase, uint Counter);
public readonly record struct NativeRandomDraw(uint Value, NativeRandomState NextState);

public static class NativeRandom
{
    /// <summary>M01, explicit entry 0x14098D35F–0x14098D39A. Обязательный seed 0..INT_MAX.
    /// Создаёт локальный state без draws; auto-seed и global RNG сюда не входят.</summary>
    public static NativeRandomState FromSpecifiedSeed(int specifiedSeed)
    {
        if (specifiedSeed < 0) throw new ArgumentOutOfRangeException(nameof(specifiedSeed), "Требуется явный seed 0..INT_MAX.");
        unchecked
        {
            // В native local object физический порядок {counter,seedBase}; API использует имена.
            return new(SeedBase: 0, Counter: Mix32(0x5EA6BA9Fu - (uint)specifiedSeed * 0x4AD685B3u));
        }
    }

    /// <summary>M02, 4.5.2 NextRandom 0x141A7BAD0. Все операции modulo 2^32.</summary>
    public static NativeRandomDraw Next(NativeRandomState state)
    {
        unchecked
        {
            var x = Mix32(state.SeedBase - state.Counter * 0x4AD685B3u);
            return new(x & 0x7FFFFFFFu, new(state.SeedBase, state.Counter + 1));
        }
    }

    private static uint Mix32(uint x)
    {
        unchecked
        {
            x ^= x >> 8;
            x += 0x68E31DA4u;
            x ^= x << 8;
            x *= 0x1B56C4E9u;
            x ^= x >> 8;
            x *= 0x92D68CA2u;
            x ^= x >> 8;
            return x;
        }
    }

    /// <summary>M04 raw fixed range 0x140A0AA20. Draw уже получен caller; новых draws нет.
    /// Span задаётся явно в fixed int64 units; script min/max encoding не реализован (M25).
    /// Непредставимые int64 intermediate/sum отклоняются: native overflow вне покрытия.</summary>
    public static long EvaluateRawFixedRange(long baseValue, long span, uint draw)
    {
        if (draw > 0x7FFFFFFFu) throw new ArgumentOutOfRangeException(nameof(draw), "Требуется uint31 draw.");
        checked
        {
            return baseValue + span * (draw % 1000) / 1000;
        }
    }
}
