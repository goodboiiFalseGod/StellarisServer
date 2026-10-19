using System.Text.Json;
using System.Text.Json.Serialization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Research;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

internal static class CatalogCoverageSourceChecks
{
    private sealed record GateValue(string Code, string DependencyId);
    private sealed record ConditionValue(bool Declared, string Truth, IReadOnlyList<GateValue> BlockingGates);
    private sealed record ContributionValue(string Kind, string FieldPath, string ConditionTruth, IReadOnlyList<GateValue> ActiveContributionBlockingGates);
    private sealed record DefinitionValue(string Id, string Kind, string Status, string? Reason,
        string? RuntimeGate, string? RuntimeGateEvidence, string? ContextRuntimeGate, ConditionValue? Potential, ConditionValue? Destroy,
        ConditionValue? Allow, ConditionValue? Unlock, ConditionValue? Prerequisites,
        IReadOnlyList<ContributionValue>? Contributions);
    private sealed record JobValue(string Id, string Status, string? Reason);
    private sealed record CatalogReport(string GameVersion, object Summary, IReadOnlyList<DefinitionValue> Definitions,
        IReadOnlyList<JobValue> JobClosure);

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var home = startups.Select("economic", 123);
        var startup = M3StartupColony.Create("source-catalog-coverage", 1, home.Homeworld, home.Plan);
        var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
        var template = ConstructionOrdinaryTemplate.Get("economic");
        var context = content.Context(startup, template, runtime, M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));

        var relevantCoverage = economy.DefinitionCoverage
            .Where(item => item.Kind is PlanetEconomyDefinitionKind.Building or PlanetEconomyDefinitionKind.Zone)
            .ToArray();
        var definitions = new List<DefinitionValue>(relevantCoverage.Length);
        foreach (var coverage in relevantCoverage)
        {
            var hasProjection = content.Content.Catalogs.TryGetValue(coverage.Id, out var projection);
            var hasGate = content.RuntimeGates.ContainsKey(coverage.Id) || content.RuntimeGateEvidence.ContainsKey(coverage.Id);
            var contextRuntimeGate = coverage.Status == PlanetEconomyDefinitionCoverageStatus.Included && hasProjection
                ? content.EvaluateRuntimeGate(coverage.Id, context) : null;
            if (coverage.Status == PlanetEconomyDefinitionCoverageStatus.Included)
            {
                check(hasProjection || hasGate, "included source definition has compiled catalog or explicit runtime gate: " + coverage.Id);
            }

            ConstructionCatalogAssessment? assessment = null;
            if (coverage.Status == PlanetEconomyDefinitionCoverageStatus.Included && hasProjection)
            {
                assessment = ConstructionCatalogAssessor.Assess(projection!, coverage.Id, context, content.Content.Conditions);
            }
            ConditionValue? Condition(ConstructionCondition? condition, string? field = null) => condition is null ? null :
                new(field is not null && projection!.Definitions[coverage.Id].Fields.Any(item => item.Key == field),
                    condition.Truth.ToString(), condition.BlockingGates.Select(gate => new GateValue(gate.Code, gate.DependencyId)).ToArray());
            var contributions = assessment?.Contributions.Select(contribution => new ContributionValue(
                contribution.Kind, contribution.FieldPath, contribution.Condition.Truth.ToString(),
                contribution.Condition.Truth == ConstructionTruth.False ? Array.Empty<GateValue>() :
                    contribution.BlockingGates.Select(gate => new GateValue(gate.Code, gate.DependencyId)).ToArray())).ToArray();

            definitions.Add(new(coverage.Id, coverage.Kind.ToString(), coverage.Status.ToString(), coverage.Reason,
                content.RuntimeGates.GetValueOrDefault(coverage.Id), content.RuntimeGateEvidence.GetValueOrDefault(coverage.Id),
                contextRuntimeGate,
                Condition(assessment?.Potential, "potential"), Condition(assessment?.Destroy, "destroy_trigger"),
                Condition(assessment?.Allow, "allow"), Condition(assessment?.Unlock, "unlock"),
                Condition(assessment?.Prerequisites), contributions));
        }

        var importedDefinitions = economy.Buildings.Values.Concat(economy.Zones.Values).ToArray();
        foreach (var definition in importedDefinitions)
        {
            check(content.Content.Catalogs.ContainsKey(definition.Id) || content.RuntimeGateEvidence.ContainsKey(definition.Id),
                "imported economy definition has compiled catalog or explicit runtime gate: " + definition.Id);
        }

        var summary = new
        {
            definitions = definitions.Count,
            catalogImported = relevantCoverage.Count(item => content.Content.Catalogs.ContainsKey(item.Id)),
            economyIncluded = relevantCoverage.Count(item => item.Status == PlanetEconomyDefinitionCoverageStatus.Included),
            byKind = definitions.GroupBy(item => item.Kind).OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            byStatus = definitions.GroupBy(item => item.Status).OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            runtimeGated = definitions.Count(item => item.RuntimeGate is not null),
            runtimeGateEvidence = definitions.Count(item => item.RuntimeGateEvidence is not null),
            contextRuntimeGated = definitions.Count(item => item.ContextRuntimeGate is not null),
            compiledAndAssessed = definitions.Count(item => item.Potential is not null),
            jobClosure = economy.JobClosure.Count,
            jobClosureByStatus = economy.JobClosure.GroupBy(item => item.Status.ToString()).OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
        };
        var report = new CatalogReport(economy.GameVersion, summary, definitions,
            economy.JobClosure.Select(item => new JobValue(item.JobId, item.Status.ToString(), item.Reason)).ToArray());
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }));
    }
}
