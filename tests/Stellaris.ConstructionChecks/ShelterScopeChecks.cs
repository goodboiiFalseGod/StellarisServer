using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;

static class ShelterScopeChecks
{
    private static readonly ConstructionOrigin _origin = new("checks/shelter-scopes", 1, 1);

    public static Task Roots()
    {
        ConstructionField Scalar(string key, string value) => new(key, "=", value, [], _origin, []);
        ConstructionField Block(string key, params ConstructionField[] fields) => new(key, "=", null, fields, _origin, []);
        var country = Block("triggered_country_modifier", Block("potential", Scalar("has_deposit", "relic")), Scalar("commercial_pact_mult", "0.01"));
        var group = Block("triggered_planet_pop_group_modifier_for_species", Block("potential", Scalar("always", "no")), Scalar("bonus_pop_growth", "1"));
        var source = new ConstructionCatalogProjection("checks", [new ConstructionPlacementDefinition("shelter", "building", _origin, [country, group])], [], [], []);
        foreach (var deposits in new IEnumerable<string>?[] { null, [], ["relic"] })
        {
            var assessment = ConstructionCatalogAssessor.Assess(source, "shelter", new(sourceFacts: new ConstructionSourceFacts(deposits: deposits)));
            var countries = assessment.Contributions.Where(item => item.Kind == "triggered_country_modifier").ToArray();
            var groups = assessment.Contributions.Where(item => item.Kind == "triggered_planet_pop_group_modifier_for_species").ToArray();
            Check(countries.Length == 1 && groups.Length == 1 && countries[0].FieldPath == "/0:triggered_country_modifier" && groups[0].FieldPath == "/1:triggered_planet_pop_group_modifier_for_species", "Cross-scope roots or source order lost.");
            var expected = deposits is null ? ConstructionTruth.Unknown : deposits.Any() ? ConstructionTruth.True : ConstructionTruth.False;
            Check(countries[0].Condition.Truth == expected && countries[0].Gates.Any(gate => gate.DependencyId == "catalog-country-consumer"), "Country potential did not use the colony context or lost its consumer gate.");
            Check((countries[0].BlockingGates.Count == 0) == (expected == ConstructionTruth.False), "Inactive country diagnostics blocked or unknown/active country effects escaped refusal.");
            Check(groups[0].Condition.Truth == ConstructionTruth.Unknown && groups[0].BlockingGates.Any(gate => gate.DependencyId == "catalog-species-group-context") && groups[0].Gates.Any(gate => gate.DependencyId == "catalog-species-group-consumer"), "Species potential was executed as a colony condition or target context defaulted.");
        }
        foreach (var kind in new[] { "triggered_country_modifier", "triggered_planet_pop_group_modifier_for_species" })
        {
            var saturated = Block(kind, [Block("potential", Scalar("always", "yes")), .. Enumerable.Range(0, 300).Select(index => Scalar("unsupported_" + index, "1"))]);
            var saturatedSource = new ConstructionCatalogProjection("checks", [new ConstructionPlacementDefinition("shelter", "building", _origin, [saturated])], [], [], []);
            var contribution = ConstructionCatalogAssessor.Assess(saturatedSource, "shelter", new()).Contributions.Single(item => item.Kind == kind);
            var consumer = kind == "triggered_country_modifier" ? "catalog-country-consumer" : "catalog-species-group-consumer";
            Check(contribution.Gates.Count == 256 && contribution.Gates[0].DependencyId == consumer && contribution.BlockingGates.Any(gate => gate.DependencyId == consumer), "Saturated cross-scope diagnostics exceeded budget or lost mandatory consumer marker.");
        }
        return Task.CompletedTask;
    }

    public static void Real(ConstructionCatalogAssessment assessment)
    {
        var roots = assessment.Source.Definitions[assessment.DefinitionId].Fields.Where(field => field.Key is "triggered_country_modifier" or "triggered_planet_pop_group_modifier_for_species").ToArray();
        var contributions = assessment.Contributions.Where(item => item.Kind is "triggered_country_modifier" or "triggered_planet_pop_group_modifier_for_species").ToArray();
        Check(roots.Length == 3 && contributions.Length == 3 && roots.Select(field => field.Key).SequenceEqual(contributions.Select(item => item.Kind)) && roots.Select(field => field.Origin).SequenceEqual(contributions.Select(item => item.Origin)), "Real mandatory country/species roots were dropped, reordered or detached from original source.");
        Check(contributions.Count(item => item.Kind == "triggered_country_modifier") == 2 && contributions.Single(item => item.Kind == "triggered_planet_pop_group_modifier_for_species").Condition.Truth == ConstructionTruth.Unknown, "Real cross-scope target inventory or unknown species context changed.");
        Check(contributions[0].Gates.Any(gate => gate.DependencyId == "catalog-number-unresolved") && contributions.All(item => item.Gates.Any(gate => gate.DependencyId is "catalog-country-consumer" or "catalog-species-group-consumer")), "Real dynamic operand or mandatory target consumer gate lost.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
