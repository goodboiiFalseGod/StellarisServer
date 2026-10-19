using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Core.Construction;

public static class ConstructionEconomyTermPolicy
{
    // Принятое bounded исключение governor/leader term; базовый soldier не исключается.
    public static bool IsLeaderDependentSoldierTerm(PlanetEconomyTerm term) => term.TargetId == "soldier"
        && term.Provenance.RelativePath == "common/inline_scripts/buildings/on_all_capital_buildings.txt"
        && (term.Kind == PlanetEconomyTermKind.WorkforceCapacity && term.SourceKey == "job_soldier_add"
            || term.Kind == PlanetEconomyTermKind.WorkforceCapacityMultiplier && term.SourceExpression == "modifier:job_soldier_or_warrior_add");
}
