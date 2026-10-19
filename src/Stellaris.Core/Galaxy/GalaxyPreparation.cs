using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public sealed record GalaxySetupPlan(GalaxyDefinition Scenario, GalaxyDefinition Shape,
    int TargetStars, decimal Radius);

public enum GalaxyStage { Coordinates, SystemMaterialization, Topology, EmpirePlacement,
    Initializers, PlanetFillAndStartup, ContentMerge, ResourceRegions, NebulaeWormholesGateways }

/// <summary>Стадия здесь не исполнена. IDs включают недоказанные и ещё не реализованные зависимости.</summary>
public sealed record UnsupportedGalaxyStage(GalaxyStage Stage, IReadOnlyList<string> RequiredContracts);

public static class GalaxyPreparation
{
    public static GalaxySetupPlan Prepare(GalaxyCatalog catalog, string scenarioId, string shapeId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.VerifyStable();
        var scenario = catalog.Scenarios.SingleOrDefault(item => item.Id == scenarioId)
            ?? throw new KeyNotFoundException($"Нет scenario {scenarioId}.");
        var shape = catalog.Shapes.SingleOrDefault(item => item.Id == shapeId)
            ?? throw new KeyNotFoundException($"Нет shape {shapeId}.");
        if (!scenario.Fields.Named("supports_shape").Select(GalaxyCatalog.Scalar).Contains(shapeId, StringComparer.Ordinal))
            throw new InvalidDataException($"Scenario {scenarioId} не поддерживает shape {shapeId}.");
        var stars = GalaxyCatalog.Number(scenario.Fields.Required("num_stars"));
        var radius = GalaxyCatalog.Number(scenario.Fields.Required("radius"));
        if (stars <= 0 || stars > int.MaxValue || decimal.Truncate(stars) != stars || radius <= 0)
            throw new InvalidDataException("num_stars должен быть положительным int32, radius — положительным числом.");
        return new(scenario, shape, (int)stars, radius);
    }

    /// <summary>Граница покрытия; не создаёт фиктивную галактику и не использует свой sampler.</summary>
    public static UnsupportedGalaxyStage GenerateCoordinates(GalaxySetupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Unsupported(GalaxyStage.Coordinates);
    }

    public static int PrepareClusterCount(GalaxySetupPlan plan, int empireCount)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return NativeGenerationRules.CountClusters(GalaxyClusterProjection.Read(plan.Scenario), empireCount);
    }

    public static UnsupportedGalaxyStage Unsupported(GalaxyStage stage)
    {
        var contracts = stage switch
        {
            GalaxyStage.Coordinates => new[] { "M03", "M05", "M06", "M07", "M08", "M09", "M10", "M11", "M25" },
            GalaxyStage.SystemMaterialization => new[] { "M12", "M37" },
            GalaxyStage.Topology => new[] { "M13", "M14", "M15", "M17" },
            GalaxyStage.EmpirePlacement => new[] { "M18" },
            GalaxyStage.Initializers => new[] { "M19", "M21", "M22", "M23", "M24", "M25", "M26", "M27", "M28" },
            GalaxyStage.PlanetFillAndStartup => new[] { "M29", "M30", "M31", "M32", "M33", "M34", "M35", "M36", "M37" },
            GalaxyStage.ContentMerge => new[] { "M38" },
            GalaxyStage.ResourceRegions => new[] { "M39" },
            GalaxyStage.NebulaeWormholesGateways => new[] { "M40" },
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        return new(stage, Array.AsReadOnly(contracts));
    }
}
