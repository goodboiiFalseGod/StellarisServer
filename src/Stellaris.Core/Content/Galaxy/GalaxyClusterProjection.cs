using Stellaris.Core.Galaxy;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Galaxy;

public static class GalaxyClusterProjection
{
    /// <summary>Только explicit cluster_count fields; отсутствующий cap не угадывается.</summary>
    public static ClusterCountRule Read(GalaxyDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var entry = scenario.Fields.Required("cluster_count");
        if (entry.Operator != "=") throw new InvalidDataException($"Ожидалось cluster_count =: {entry.Source}:{entry.Line}");
        var fields = entry.AsBlock();
        var method = GalaxyCatalog.Scalar(fields.Required("method")) switch
        {
            "one_every_x_empire" => ClusterCountMethod.OneEveryXEmpire,
            "constant" => ClusterCountMethod.Constant,
            var unknown => throw new NotSupportedException($"Не поддержан cluster_count method: {unknown}")
        };
        return new(method, Integer(fields.Required("value")), Integer(fields.Required("max")));
    }

    private static int Integer(ScriptEntry entry)
    {
        var number = GalaxyCatalog.Number(entry);
        if (number < int.MinValue || number > int.MaxValue || decimal.Truncate(number) != number)
            throw new InvalidDataException($"Требуется int32 literal: {entry.Source}:{entry.Line} {entry.Key}");
        return (int)number;
    }
}
