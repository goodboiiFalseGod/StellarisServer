using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3.Construction;

// Поэтапное подключение strict runtime условий; остальные definitions сохраняют прежний consumer.
internal static class M3ConstructionEconomyConditions
{
    internal static Func<PlanetEconomyCondition?, bool?>? Create(PlanetEconomyDefinition definition,
        ConstructionContext? context, ConstructionCatalogProjection? conditions)
    {
        if (definition.Kind != PlanetEconomyDefinitionKind.Building
            || definition.Provenance.RelativePath != "common/buildings/08_unity_buildings.txt"
            || definition.Id is not ("building_galactic_memorial_1" or "building_galactic_memorial_2" or "building_galactic_memorial_3")) { return null; }
        if (context is null || conditions is null) { throw new InvalidDataException("Memorial runtime condition context is unavailable."); }
        if (conditions.ProfileId != "construction-conditions-authority-catalog-452-v4") { return null; }
        return condition => condition is null ? true : ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(condition, context, conditions).Truth switch
        { ConstructionTruth.True => true, ConstructionTruth.False => false, _ => null };
    }
}
