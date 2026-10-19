using System.Collections.Frozen;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;

namespace Stellaris.Session.M3;

internal sealed record M3StructuralProductionResolution(StructuralProductionSource? Source, IReadOnlySet<PlanetEconomyTerm> Consumed);

// Первый source consumer: прямое единство с native множителем числа полученных perks.
internal static class M3StructuralProduction
{
    private static readonly M3StructuralProductionResolution Empty = new(null, Array.Empty<PlanetEconomyTerm>().ToFrozenSet());
    internal static M3StructuralProductionResolution Resolve(PlanetEconomyDefinition definition, string sourceId,
        IReadOnlySet<string>? perks, Func<PlanetEconomyTerm, PlanetEconomyConditionDisposition> disposition)
    {
        if (!definition.Terms.Any(term => term.Kind == PlanetEconomyTermKind.ResourceProduction)) { return Empty; }
        var active = new List<PlanetEconomyTerm>();
        foreach (var term in definition.Terms.Where(term => term.Kind == PlanetEconomyTermKind.ResourceProduction))
        {
            var state = disposition(term);
            if (state == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext) { continue; }
            if (state == PlanetEconomyConditionDisposition.RequiresRuntimeContext) { throw Gate(definition, "production-condition"); }
            active.Add(term);
        }
        var consumed = new HashSet<PlanetEconomyTerm>();
        if (active.Count == 0) { return Empty; }
        if (definition.Kind != PlanetEconomyDefinitionKind.Building || definition.EconomicCategory != "planet_buildings"
            || perks is null || !definition.SourceFields.TryGetValue("resource_produces", out var blocks))
        { throw Gate(definition, "production-source-context"); }
        decimal unity = 0;
        foreach (var block in blocks)
        {
            var amountFields = block.Children.Where(field => field.Key == "unity").ToArray();
            if (amountFields.Length != 1) { continue; }
            var amount = amountFields[0];
            var productionTerms = active.Where(term => SameLocation(term.Provenance, amount.Provenance)).ToArray();
            if (productionTerms.Length == 0) { continue; }
            var triggers = block.Children.Where(field => field.Key == "trigger").ToArray();
            var multipliers = block.Children.Where(field => field.Key == "multiplier").ToArray();
            if (block.Key != "produces" || block.Operator != "=" || block.Value is not null || block.Children.Count != 3
                || amount.Operator != "=" || amount.Children.Count != 0 || triggers.Length != 1 || multipliers.Length != 1
                || triggers[0] is not { Operator: "=", Value: null, Children.Count: 1 }
                || triggers[0].Children[0] is not { Key: "exists", Operator: "=", Value: "owner", Children.Count: 0 }
                || multipliers[0] is not { Operator: "=", Value: "owner.trigger:num_ascension_perks", Children.Count: 0 }
                || productionTerms.Length != 1)
            { throw Gate(definition, "production-block-shape"); }
            var production = productionTerms[0];
            var multiplierTerms = definition.Terms.Where(term => term.Kind == PlanetEconomyTermKind.ResourceMultiplier
                && term.TargetId == "unity" && SameLocation(term.Provenance, multipliers[0].Provenance)).ToArray();
            if (production.TargetId != "unity" || production.ApplicationScope != PlanetEconomyApplicationScope.PerDefinitionInstance
                || production.Value is not { } coefficient || coefficient < 0
                || multiplierTerms.Length != 1 || multiplierTerms[0].Value is not null
                || multiplierTerms[0].ApplicationScope != PlanetEconomyApplicationScope.PerDefinitionInstance
                || multiplierTerms[0].SourceExpression != "owner.trigger:num_ascension_perks"
                || disposition(multiplierTerms[0]) is PlanetEconomyConditionDisposition.InactiveInOrdinaryContext or PlanetEconomyConditionDisposition.RequiresRuntimeContext)
            { throw Gate(definition, "production-leaf-pairing"); }
            if (!consumed.Add(production) || !consumed.Add(multiplierTerms[0])) { throw Gate(definition, "production-leaf-reused"); }
            unity = checked(unity + coefficient * perks.Count);
        }
        if (active.Any(term => !consumed.Contains(term))) { throw Gate(definition, "production-source-unmatched"); }
        var source = new StructuralProductionSource(sourceId, definition.Id, definition.EconomicCategory,
            new Dictionary<string, decimal>(StringComparer.Ordinal) { ["unity"] = unity }.ToFrozenDictionary(StringComparer.Ordinal));
        return new(source, consumed.ToFrozenSet());
    }

    private static bool SameLocation(PlanetEconomyProvenance first, PlanetEconomyProvenance second)
        => first.RelativePath == second.RelativePath && first.Line == second.Line && first.Column == second.Column;

    private static InvalidDataException Gate(PlanetEconomyDefinition definition, string reason)
        => new("Structural production unresolved: " + definition.Id + "/" + reason);
}
