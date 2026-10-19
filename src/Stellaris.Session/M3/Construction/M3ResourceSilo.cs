using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3.Construction;

// Отдельный country consumer: эти поля не являются потоком профессий/планеты.
internal sealed record M3ResourceSilo(decimal GenericMaximumAddition, IReadOnlyList<PlanetEconomyCondition> Consumed)
{
    internal bool Consumes(ConstructionField field) => Consumed.Any(node => node.Key == field.Key
        && node.Provenance.RelativePath == field.Origin.Source && node.Provenance.Line == field.Origin.Line && node.Provenance.Column == field.Origin.Column);

    internal static M3ResourceSilo? Resolve(PlanetEconomyDefinition definition, ConstructionContext? context, ConstructionCatalogProjection? conditions)
    {
        if (definition.Id != "building_resource_silo") return null;
        if (definition.Kind != PlanetEconomyDefinitionKind.Building
            || definition.Provenance.RelativePath != "common/buildings/03_resource_buildings.txt"
            || context is null || conditions?.ProfileId != "construction-conditions-authority-catalog-452-v4") throw Unresolved();
        var direct = definition.SourceFields.GetValueOrDefault("country_modifier", []);
        var triggered = definition.SourceFields.GetValueOrDefault("triggered_country_modifier", []);
        if (direct is not [var unconditional] || triggered.Count != 4) throw Unresolved();
        RequireBlock(unconditional);
        if (unconditional.Children is not [var baseline] || baseline.Key != "country_resource_max_add" || Value(baseline) != 5000) throw Unresolved();
        decimal addition = Value(baseline);
        var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in triggered)
        {
            RequireBlock(block);
            var potentials = block.Children.Where(node => node.Key == "potential").ToArray();
            var effects = block.Children.Where(node => node.Key != "potential").ToArray();
            if (potentials is not [var potential] || effects is not [var effect]) throw Unresolved();
            RequireBlock(potential);
            var amount = Value(effect);
            if (effect.Key is "country_resource_max_add" or "country_resource_max_consumer_goods_add")
            { if (amount != 5000) throw Unresolved(); }
            else if (effect.Key != "situation_nomad_economy_max_progress_add" || amount != 500) throw Unresolved();
            kinds[effect.Key] = kinds.GetValueOrDefault(effect.Key) + 1;
            var truth = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(potential, context, conditions, evaluateBody: true).Truth;
            if (truth == ConstructionTruth.Unknown || truth == ConstructionTruth.True && effect.Key != "country_resource_max_add") throw Unresolved();
            if (truth == ConstructionTruth.True) addition = checked(addition + amount);
        }
        if (kinds.GetValueOrDefault("country_resource_max_add") != 1 || kinds.GetValueOrDefault("country_resource_max_consumer_goods_add") != 1
            || kinds.GetValueOrDefault("situation_nomad_economy_max_progress_add") != 2) throw Unresolved();
        return new(addition, Array.AsReadOnly(direct.Concat(triggered).ToArray()));
    }

    private static void RequireBlock(PlanetEconomyCondition node)
    { if (node.Operator != "=" || node.Value is not null || node.Children.Count == 0) throw Unresolved(); }
    private static decimal Value(PlanetEconomyCondition node) => node.Operator == "=" && node.Children.Count == 0
        && decimal.TryParse(node.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : throw Unresolved();
    private static InvalidDataException Unresolved() => new("resource-silo-country-effects-unresolved");
}
