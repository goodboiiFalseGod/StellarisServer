using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;

static class CapitalHabitatChecks
{
    private static readonly ConstructionOrigin _origin = new("checks/capital-habitat", 1, 1);

    public static ConstructionCondition Evaluate(ConstructionCatalogProjection source, string? districtSet, ConstructionField? call = null, ConstructionAssessor.Scope scope = ConstructionAssessor.Scope.Planet)
    {
        var context = new ConstructionContext(sourceFacts: new ConstructionSourceFacts(districtSet: districtSet));
        return new ConstructionAssessor.Evaluator(context, source).Condition([call ?? Field("uses_habitat_capitals", "yes")], scope);
    }

    public static void Facts(ConstructionCatalogProjection source)
    {
        foreach (var (set, expected) in new (string?, ConstructionTruth)[] { ("habitat", ConstructionTruth.True), ("standard", ConstructionTruth.False), ("cosmogenesis_world", ConstructionTruth.False), ("HABITAT", ConstructionTruth.False), (null, ConstructionTruth.Unknown) })
        {
            Check(Evaluate(source, set).Truth == expected, "Explicit district-set helper mis-evaluated.");
            Check(Evaluate(source, set, Field("uses_habitat_capitals", "no")).Truth == (expected == ConstructionTruth.Unknown ? expected : expected == ConstructionTruth.True ? ConstructionTruth.False : ConstructionTruth.True), "Inverted district-set helper mis-evaluated.");
        }
        foreach (var call in new[] { Field("uses_habitat_capitals", "habitat"), Field("uses_habitat_capitals", "yes", "!="), Field("uses_habitat_capitals", children: [Field("always", "yes")]), Field("uses_habitat_capitals", "yes", children: [Field("always", "yes")]) })
        {
            Check(Evaluate(source, "habitat", call).Truth == ConstructionTruth.Unknown, "Malformed habitat helper call was guessed.");
        }
        Check(Evaluate(source, "habitat", scope: ConstructionAssessor.Scope.Owner).Truth == ConstructionTruth.Unknown && Evaluate(source, "habitat", scope: ConstructionAssessor.Scope.From).Truth == ConstructionTruth.Unknown, "Habitat helper evaluated outside planet scope.");
    }

    public static void Real(ConstructionCatalogProjection building, ConstructionCatalogProjection conditions, ConstructionEconomicCategoryProjection categories)
    {
        var definition = building.Definitions.Values.Single();
        var call = definition.Fields.Single(field => field.Key == "potential").Children.Single(field => field.Key == "NOR").Children.Single(field => field.Key == "uses_habitat_capitals");
        Check(call.Scalar == "yes" && call.Origin.Source == "common/buildings/00_capital_buildings.txt", "Real capital habitat exclusion changed.");
        ConstructionCatalogAssessment Assess(string? set, string? planetClass = "pc_continental", IEnumerable<string>? modifiers = null)
        {
            var context = new ConstructionContext(ownerExists: true, ownerFacts: new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.Regular] = true }, planetClass: planetClass, modifiers: modifiers ?? [], sourceFacts: new ConstructionSourceFacts(districtSet: set, ownerCountryType: "default"));
            return ConstructionCatalogAssessor.Assess(building, definition.Id, context, conditions);
        }
        foreach (var (set, expected) in new (string?, ConstructionTruth)[] { ("standard", ConstructionTruth.True), ("habitat", ConstructionTruth.False), (null, ConstructionTruth.Unknown) })
        {
            var assessment = Assess(set);
            Check(assessment.Potential.Truth == expected && ReferenceEquals(assessment.ConditionSource, conditions), "Real capital potential did not preserve explicit habitat/ordinary/unknown facts.");
            Check(ConstructionResourceCategoryBinding.Bind(assessment, categories).BindingGates.Count == 0, "Matching capital condition source binding refused.");
            var mismatch = new ConstructionCatalogProjection(conditions.ProfileId, conditions.Definitions.Values, conditions.Parameters, conditions.Files.Select(file => file.RelativePath == "launcher-settings.json" ? file with { Sha256 = new string('0', 64) } : file), conditions.Coverage);
            Check(ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(building, definition.Id, assessment.Context, mismatch), categories).BindingGates.Any(gate => gate.DependencyId == "source-profile-mismatch"), "Mismatched capital helper source combined with building operands.");
        }
        Check(Assess("standard", modifiers: ["resort_colony"]).Potential.Truth == ConstructionTruth.False && Assess("standard", modifiers: ["slave_colony"]).Potential.Truth == ConstructionTruth.False, "Habitat closure discarded other NOR exclusions.");
        Check(Assess("cosmogenesis_world").Potential.Truth == (definition.Id == "building_colony_shelter" ? ConstructionTruth.False : ConstructionTruth.True), "Shelter district-set exclusion was conflated with capital planet class.");
        Check(Assess("standard", "pc_cosmogenesis_world").Potential.Truth == (definition.Id == "building_capital" ? ConstructionTruth.False : ConstructionTruth.True), "Capital class exclusion was conflated with shelter district set.");
        Console.WriteLine($"Capital habitat {definition.Id}: explicit standard potential true, habitat false, absent set unknown; class/set exclusions remain distinct.");
    }

    private static ConstructionField Field(string key, string? scalar = null, string operation = "=", params ConstructionField[] children) => new(key, operation, scalar, children, _origin, []);
    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
