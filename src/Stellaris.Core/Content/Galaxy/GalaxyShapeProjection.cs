using Stellaris.Core.Galaxy;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Galaxy;

public static class GalaxyShapeProjection
{
    public static PreRelaxationShape Read(GalaxySetupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var fields = plan.Shape.Fields;
        var radius = ToFixed(plan.Radius);
        var coreFraction = Fixed(fields.Required("core_radius_perc"));
        var coreStarsFraction = Fixed(fields.Required("num_stars_core_perc"));
        if (plan.TargetStars <= 0 || radius <= 0 || coreFraction < 0 || coreFraction > 100000 ||
            coreStarsFraction < 0 || coreStarsFraction > 100000)
            throw new InvalidDataException("Неподдержанные source radius/core fractions.");
        var coreRadius = NativeFixedMath.Multiply(radius, coreFraction);
        var coreStars = checked((int)((long)plan.TargetStars * coreStarsFraction / 100000));
        GalaxyArms? arms = null;
        if (fields.Optional("arms") is { } armsEntry)
        {
            var block = Block(armsEntry);
            var countValue = GalaxyCatalog.Number(fields.Required("num_arms"));
            if (countValue <= 0 || countValue > int.MaxValue || decimal.Truncate(countValue) != countValue)
                throw new InvalidDataException("num_arms должен быть положительным int32.");
            arms = new((int)countValue, Fixed(block.Required("tightness_winding")), Fixed(block.Required("width")),
                Fixed(block.Required("fuzz")), Fixed(block.Required("seperation")));
            if (arms.Width < 0 || arms.Fuzz < 0) throw new InvalidDataException("width/fuzz должны быть неотрицательны.");
        }
        else if (fields.Optional("num_arms") is not null)
            throw new InvalidDataException("num_arms без explicit arms block не поддержан.");
        GalaxyRing? ring = null;
        if (fields.Optional("ring") is { } ringEntry)
        {
            var block = Block(ringEntry);
            var width = Fixed(block.Required("width"));
            var offset = Fixed(block.Required("offset"));
            if (width < 0 || offset < 0) throw new InvalidDataException("Ring width/offset должны быть неотрицательны.");
            ring = new(NativeFixedMath.Multiply(radius, width), NativeFixedMath.Multiply(radius, offset));
        }
        var minDistance = Fixed(fields.Required("stars_min_dist"));
        if (minDistance < 0) throw new InvalidDataException("stars_min_dist должен быть неотрицателен.");
        return new(plan.TargetStars - coreStars, coreRadius, radius - coreRadius, minDistance, arms, ring);
    }

    private static ScriptBlock Block(ScriptEntry entry)
    {
        if (entry.Operator != "=") throw new InvalidDataException($"Ожидалось =: {entry.Source}:{entry.Line}");
        return entry.AsBlock();
    }

    private static long Fixed(ScriptEntry entry) => ToFixed(GalaxyCatalog.Number(entry));

    private static long ToFixed(decimal value)
    {
        var raw = checked(value * 100000);
        if (raw < long.MinValue || raw > long.MaxValue || decimal.Truncate(raw) != raw)
            throw new InvalidDataException("Число нельзя точно представить в fixed scale100000.");
        return (long)raw;
    }
}
