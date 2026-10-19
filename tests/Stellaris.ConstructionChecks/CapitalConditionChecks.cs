using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;

static class CapitalConditionChecks
{
    private static readonly ConstructionOrigin _origin = new("checks/capital-condition", 1, 1);

    public static ConstructionCondition Evaluate(ConstructionCatalogProjection catalog, string? type, ConstructionField? call = null, bool? ownerExists = true)
    {
        var context = new ConstructionContext(ownerExists: ownerExists, sourceFacts: new ConstructionSourceFacts(ownerCountryType: type));
        return new ConstructionAssessor.Evaluator(context, catalog).Condition([Field("owner", children: [call ?? Field("is_fallen_empire", "yes")])], ConstructionAssessor.Scope.Planet);
    }

    public static void Membership(ConstructionCatalogProjection catalog)
    {
        foreach (var (type, expected) in new (string?, ConstructionTruth)[] { ("fallen_empire", ConstructionTruth.True), ("awakened_fallen_empire", ConstructionTruth.True), ("default", ConstructionTruth.False), ("other", ConstructionTruth.False), (null, ConstructionTruth.Unknown) })
        {
            Check(Evaluate(catalog, type).Truth == expected, "Source country-type helper mis-evaluated: " + type);
            Check(Evaluate(catalog, type, Field("is_fallen_empire", "no")).Truth == (expected == ConstructionTruth.Unknown ? expected : expected == ConstructionTruth.True ? ConstructionTruth.False : ConstructionTruth.True), "Inverted source helper mis-evaluated.");
        }
        foreach (var exists in new bool?[] { false, null }) { Check(Evaluate(catalog, "fallen_empire", ownerExists: exists).Truth == ConstructionTruth.Unknown, "Unbound owner consumed country type."); }
        foreach (var call in new[] { Field("is_fallen_empire", "fallen_empire"), Field("is_fallen_empire", "yes", "!="), Field("is_fallen_empire", children: [Field("always", "yes")]), Field("is_fallen_empire", "yes", children: [Field("always", "yes")]) })
        {
            Check(Evaluate(catalog, "default", call).Truth == ConstructionTruth.Unknown, "Unsupported source helper call shape guessed.");
        }
        var evaluator = new ConstructionAssessor.Evaluator(new(ownerExists: true, sourceFacts: new ConstructionSourceFacts(ownerCountryType: "fallen_empire")), catalog);
        Check(evaluator.Condition([Field("is_fallen_empire", "yes")], ConstructionAssessor.Scope.Planet).Truth == ConstructionTruth.Unknown, "Owner helper evaluated in planet scope.");
    }

    public static void Real(ConstructionCatalogProjection building, ConstructionCatalogProjection conditions, ConstructionEconomicCategoryProjection categories)
    {
        var definition = building.Definitions.Values.Single();
        var owner = definition.Fields.Single(field => field.Key == "potential").Children.Single(field => field.Key == "owner");
        var call = owner.Children.Single(field => field.Key == "is_fallen_empire");
        Check(call.Scalar == "no" && call.Origin.Source == "common/buildings/00_capital_buildings.txt", "Actual capital source helper call changed.");
        foreach (var (type, expected) in new[] { ("default", ConstructionTruth.True), ("fallen_empire", ConstructionTruth.False), ("awakened_fallen_empire", ConstructionTruth.False) })
        {
            Check(Evaluate(conditions, type, call).Truth == expected, "Real selected capital owner helper failed.");
            var context = new ConstructionContext(ownerExists: true, ownerFacts: new Dictionary<ConstructionOwnerPredicate, bool?> { [ConstructionOwnerPredicate.Regular] = true }, sourceFacts: new ConstructionSourceFacts(ownerCountryType: type));
            var assessment = ConstructionCatalogAssessor.Assess(building, definition.Id, context, conditions);
            Check(ReferenceEquals(assessment.ConditionSource, conditions), "Exact condition catalog lost.");
            if (expected == ConstructionTruth.False) { Check(assessment.Potential.Truth == ConstructionTruth.False && assessment.Potential.BlockingGates.Count == 0, "Fallen owner did not decisively reject actual capital potential."); }
            else { Check(assessment.Potential.Truth == ConstructionTruth.Unknown, "Unrelated capital conditions were defaulted by helper closure."); }
            Check(ConstructionResourceCategoryBinding.Bind(assessment, categories).BindingGates.Count == 0, "Matching startup condition source binding failed.");
            var mismatch = new ConstructionCatalogProjection(conditions.ProfileId, conditions.Definitions.Values, conditions.Parameters, conditions.Files.Select(file => file.RelativePath == "launcher-settings.json" ? file with { Sha256 = new string('0', 64) } : file), conditions.Coverage);
            Check(ConstructionResourceCategoryBinding.Bind(ConstructionCatalogAssessor.Assess(building, definition.Id, context, mismatch), categories).BindingGates.Any(gate => gate.DependencyId == "source-profile-mismatch"), "Mismatched startup source combined with actual building operands.");
        }
        Console.WriteLine($"Capital helper {definition.Id}: explicit ordinary helper true; fallen/awakened potentials false; ordinary overall potential remains unknown.");
    }

    private static ConstructionField Field(string key, string? scalar = null, string operation = "=", params ConstructionField[] children) => new(key, operation, scalar, children, _origin, []);
    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
