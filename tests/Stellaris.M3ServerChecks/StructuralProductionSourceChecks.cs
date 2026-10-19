using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Сверяет direct produces, сохранённое дерево и numeric terms с native source.
internal static class StructuralProductionSourceChecks
{
    private const string Path = "common/buildings/08_unity_buildings.txt";
    private static readonly (string Id, decimal Amount)[] Buildings =
    [
        ("building_autochthon_monument", 1m), ("building_corporate_monument", 1m), ("building_galactic_memorial_1", 1m),
        ("building_heritage_site", 2m), ("building_corporate_site", 2m), ("building_galactic_memorial_2", 2m),
        ("building_hypercomms_forum", 3m), ("building_corporate_forum", 3m), ("building_galactic_memorial_3", 3m)
    ];
    private static readonly string[] DefinitionGatePass =
    [
        "building_autochthon_monument", "building_corporate_monument", "building_heritage_site",
        "building_corporate_site", "building_hypercomms_forum", "building_corporate_forum"
    ];
    private static readonly string[] DefinitionGateOther =
        ["building_galactic_memorial_1", "building_galactic_memorial_2", "building_galactic_memorial_3"];

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var catalog = await new PlanetEconomyImporter().ImportAsync(root);
        var construction = await M3ConstructionContent.ImportAsync(root, catalog);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(catalog, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("structural-production-source", 1, home.Homeworld, home.Plan);
        var startupContributions = profile.CreateConstructionContributions(startup);
        var runtime = construction.Create(startup, startupContributions);
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var context = construction.Context(startup, template, runtime,
            M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
        check(startupContributions.Count == startup.Buildings.Count + startup.Districts.Count
            && startup.Buildings.All(building => startupContributions.ContainsKey(building.InstanceId))
            && startup.Districts.All(district => startupContributions.ContainsKey(district.InstanceId)),
            "ordinary startup использует contributions только своих исходных buildings/districts");
        check(DefinitionGatePass.Length == 6 && DefinitionGateOther.Length == 3
            && DefinitionGatePass.Concat(DefinitionGateOther).Order(StringComparer.Ordinal)
                .SequenceEqual(Buildings.Select(item => item.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "девять native monument definitions разделены на шесть проходящих и три с другим gate");
        var source = new ContentSession(root).LoadFile(Path).Root;
        foreach (var (id, amount) in Buildings)
        {
            var raw = source.Named(id).Single();
            var produces = raw.AsBlock().Optional("resources")?.AsBlock().Named("produces").Single()
                ?? throw new InvalidOperationException($"{id} lacks direct resources.produces.");
            var block = produces.AsBlock();
            var trigger = block.Named("trigger").Single();
            var owner = trigger.AsBlock().Entries.Single();
            var directAmount = block.Named("unity").Single();
            var multiplier = block.Named("multiplier").Single();
            check(produces.Operator == "=" && trigger.Operator == "=" && owner.Key == "exists"
                && owner.Operator == "=" && owner.Scalar == "owner" && owner.Block is null
                && directAmount.Operator == "=" && directAmount.AsScalar() == amount.ToString("0", CultureInfo.InvariantCulture)
                && multiplier.Operator == "=" && multiplier.AsScalar() == "owner.trigger:num_ascension_perks",
                "native direct unity source shape/coefficient: " + id);

            var definition = catalog.Buildings[id];
            check(definition.SourceFields.TryGetValue("resource_produces", out var fields) && fields.Count == 1
                && Matches(fields[0], produces, Path), "preserved produces SourceTree and leaf provenance: " + id);
            var production = definition.Terms.Single(term => term.Kind == PlanetEconomyTermKind.ResourceProduction && term.TargetId == "unity");
            var resourceMultiplier = definition.Terms.Single(term => term.Kind == PlanetEconomyTermKind.ResourceMultiplier && term.TargetId == "unity");
            check(production.Value == amount && SameLocation(production.Provenance, directAmount, Path)
                && resourceMultiplier.SourceExpression == "owner.trigger:num_ascension_perks"
                && SameLocation(resourceMultiplier.Provenance, multiplier, Path)
                && HasOwnerTrigger(production.Condition, trigger, Path) && HasOwnerTrigger(resourceMultiplier.Condition, trigger, Path),
                "production and multiplier terms retain coefficient, trigger, and leaf provenance: " + id);

            if (DefinitionGatePass.Contains(id, StringComparer.Ordinal))
            {
                check(!construction.RuntimeGates.ContainsKey(id) && !construction.RuntimeGateEvidence.ContainsKey(id),
                    "native monument проходит текущий M3 DefinitionGate: " + id);
            }
            else
            {
                var gate = construction.RuntimeGates.GetValueOrDefault(id);
                var evidence = construction.RuntimeGateEvidence.GetValueOrDefault(id);
                var ethicTerm = definition.Terms.FirstOrDefault(term => term.TargetId == "pop_government_ethic_attraction");
                check(DefinitionGateOther.Contains(id, StringComparer.Ordinal)
                    && gate == "definition-source-economy-unresolved:" + id
                    && ethicTerm is not null && ethicTerm.Provenance.RelativePath == Path
                    && evidence is not null && evidence.Contains("/pop_government_ethic_attraction@" + Path + ":"
                        + ethicTerm.Provenance.Line, StringComparison.Ordinal),
                    "native memorial сохраняет отдельный conditional ethic modifier gate: " + id);
            }
        }

        var culturalMonument = construction.Content.Catalogs["building_autochthon_monument"];
        var assessment = ConstructionCatalogAssessor.Assess(culturalMonument, "building_autochthon_monument", context,
            construction.Content.Conditions);
        check(culturalMonument.Definitions["building_autochthon_monument"].Fields
                .Where(field => field.Key == "prerequisites").SelectMany(field => field.Children)
                .Any(field => field.Scalar == "tech_cultural_heritage")
            && context.Technologies is not null && !context.Technologies.Contains("tech_cultural_heritage")
            && assessment.Prerequisites?.Truth == ConstructionTruth.False,
            "отдельный native cultural heritage prerequisite остаётся False в реальном старте");

        check(context.PlanetClass == "pc_continental"
            && context.OwnerFacts[ConstructionOwnerPredicate.Gestalt] == false,
            "мемориальные условия проверяются на фактическом continental startup non-gestalt владельце");
        foreach (var id in DefinitionGateOther)
        {
            var raw = source.Named(id).Single();
            var classModifiers = raw.AsBlock().Named("triggered_planet_modifier").Where(modifier =>
                modifier.AsBlock().Named("potential").Any(potential => Flatten(potential.AsBlock())
                    .Any(field => field.Key == "is_planet_class" && (field.Scalar is "pc_nuked" or "pc_relic")))).ToArray();
            var nativeClassLeaves = classModifiers.SelectMany(modifier => modifier.AsBlock().Named("potential")
                    .SelectMany(potential => Flatten(potential.AsBlock())))
                .Where(field => field.Key == "is_planet_class" && (field.Scalar is "pc_nuked" or "pc_relic")).ToArray();
            var definition = catalog.Buildings[id];
            var termConditions = definition.Terms.SelectMany(term => FlattenCondition(term.Condition).Select(condition => (term, condition))).ToArray();
            check(classModifiers.Length == 2 && nativeClassLeaves.Length == 4
                && nativeClassLeaves.All(rawLeaf => termConditions.Any(pair => pair.condition.Key == rawLeaf.Key
                    && pair.condition.Operator == rawLeaf.Operator && pair.condition.Value == rawLeaf.Scalar
                    && SameLocation(pair.condition.Provenance, rawLeaf, Path))),
                "оба native class branch и все pc_nuked/pc_relic leaf provenance сохранены: " + id);

            var conditionEvaluator = construction.EconomyConditionEvaluator(id, context)
                ?? throw new InvalidOperationException("Memorial source condition evaluator unavailable: " + id);
            var classTerms = definition.Terms.Where(term => FlattenCondition(term.Condition)
                .Any(condition => condition.Key == "is_planet_class" && (condition.Value is "pc_nuked" or "pc_relic"))).ToArray();
            check(classTerms.Length != 0 && classTerms.All(term => conditionEvaluator(term.Condition) == false),
                "реальный continental class делает каждое native tomb/relic term неактивным: " + id);
            check(construction.EvaluateRuntimeGate(id, context) is null,
                "реальная class-оценка снимает исходный memorial DefinitionGate без другого unresolved term: " + id);

            var nativeAssessment = ConstructionCatalogAssessor.Assess(construction.Content.Catalogs[id], id, context,
                construction.Content.Conditions);
            var sourceQuote = construction.Content.Quote(id, context, 0m, 0m, M3ConstructionContent.EconomyFacts(startup),
                economyConditionEvaluator: conditionEvaluator);
            check(nativeAssessment.Potential.Truth == ConstructionTruth.False
                && ConstructionCatalogAssessor.EvaluateOwnerHelper(construction.Content.Conditions, "is_memorialist_empire", context).Truth == ConstructionTruth.False
                && nativeAssessment.Prerequisites.Truth == ConstructionTruth.False
                && sourceQuote.Quote is null,
                "actual non-memorialist owner and missing native prerequisite keep quote unavailable: " + id);
        }
    }

    private static bool HasOwnerTrigger(PlanetEconomyCondition? condition, ScriptEntry trigger, string path)
        => condition is { Key: "AND", Children.Count: 1 } && SameLocation(condition.Provenance, trigger, path)
            && condition.Children[0] is { Key: "exists", Operator: "=", Value: "owner", Children.Count: 0 };

    private static bool Matches(PlanetEconomyCondition condition, ScriptEntry raw, string path)
        => condition.Key == raw.Key && condition.Operator == raw.Operator && condition.Value == raw.Scalar
            && SameLocation(condition.Provenance, raw, path)
            && condition.Children.Count == (raw.Block?.Entries.Count ?? 0)
            && condition.Children.Zip(raw.Block?.Entries ?? []).All(pair => Matches(pair.First, pair.Second, path));

    private static bool SameLocation(PlanetEconomyProvenance provenance, ScriptEntry raw, string path)
        => provenance.RelativePath == path && provenance.Line == raw.Line && provenance.Column == raw.Column;

    private static IEnumerable<PlanetEconomyCondition> FlattenCondition(PlanetEconomyCondition? condition)
    {
        if (condition is null) { yield break; }
        yield return condition;
        foreach (var child in condition.Children)
        {
            foreach (var nested in FlattenCondition(child)) { yield return nested; }
        }
    }

    private static IEnumerable<ScriptEntry> Flatten(ScriptBlock block)
    {
        foreach (var entry in block.Entries)
        {
            yield return entry;
            if (entry.Block is { } nested)
            {
                foreach (var child in Flatten(nested)) { yield return child; }
            }
        }
    }
}
