namespace Stellaris.Core.Galaxy;

public enum ClusterCountMethod { OneEveryXEmpire, Constant }
public readonly record struct ClusterCountRule(ClusterCountMethod Method, int Value, int Cap);

/// <summary>Восстановленные M16/M20 operators. Не размещают systems и не выбирают initializer.</summary>
public static class NativeGenerationRules
{
    /// <summary>M16, 0x140980782–0x1409807CC. Cap −1 отключает ограничение; минимум всегда 1.</summary>
    public static int CountClusters(ClusterCountRule rule, int empireCount)
    {
        if (empireCount < 0) throw new ArgumentOutOfRangeException(nameof(empireCount));
        checked
        {
            var count = rule.Method switch
            {
                ClusterCountMethod.OneEveryXEmpire when rule.Value > 0 => (empireCount - 1 + rule.Value) / rule.Value,
                ClusterCountMethod.OneEveryXEmpire => throw new ArgumentOutOfRangeException(nameof(rule), "Делитель должен быть положительным."),
                ClusterCountMethod.Constant => rule.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(rule), "Не поддержан method.")
            };
            if (rule.Cap != -1) count = Math.Min(count, rule.Cap);
            return Math.Max(count, 1);
        }
    }

    /// <summary>M20, 0x140508F60–0x1405090EF. Factor в raw fixed scale 100000; без clamp/defaults/RNG.
    /// Первый product обязан помещаться в int32, второй — в int64; native overflow вне покрытия.</summary>
    public static long EvaluateSpawnChance(int spawnChance, int scaledSpawnChance, int totalSystems, long factor)
    {
        if (totalSystems < 0) throw new ArgumentOutOfRangeException(nameof(totalSystems));
        checked
        {
            var baseChance = scaledSpawnChance > 0
                ? RoundHalfAwayFromZero(scaledSpawnChance * totalSystems, 100)
                : (long)spawnChance;
            return RoundHalfAwayFromZero(baseChance * factor, 100000);
        }
    }

    /// <summary>Source max_instances scaled by the resolved fixed5 setup factor.</summary>
    public static int EffectiveInstanceLimit(int sourceLimit, long factor)
    {
        if (factor < 0) throw new ArgumentOutOfRangeException(nameof(factor));
        checked { return (int)RoundHalfAwayFromZero((long)sourceLimit * factor, 100000); }
    }

    /// <summary>Caller predicate 0x14097FC10–0x14097FC26. Draw уже потрачен, даже если chance вне 0..100.</summary>
    public static bool PassesSpawnChance(long chance, uint draw)
    {
        if (draw > 0x7FFFFFFFu) throw new ArgumentOutOfRangeException(nameof(draw));
        return draw % 100 < chance;
    }

    private static long RoundHalfAwayFromZero(long numerator, long denominator)
    {
        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        // Remainder ограничен denominator<=100000; Abs не переполняется на long.MinValue numerator.
        return Math.Abs(remainder) * 2 >= denominator
            ? checked(quotient + Math.Sign(numerator))
            : quotient;
    }
}
