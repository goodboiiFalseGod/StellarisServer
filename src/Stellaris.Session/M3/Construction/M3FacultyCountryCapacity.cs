using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3.Construction;

// Обрабатывает только storage-часть faculty. Остальные поля country modifier
// остаются непотреблёнными: этот результат не разрешает строительство здания.
internal static class M3FacultyCountryCapacity
{
    internal static decimal Resolve(PlanetEconomyDefinition definition, ConstructionContext context,
        ConstructionCatalogProjection conditions)
    {
        if (definition.Id != "building_archaeostudies_faculty" || definition.Kind != PlanetEconomyDefinitionKind.Building
            || definition.Provenance.RelativePath != "common/buildings/05_research_buildings.txt"
            || conditions.ProfileId != "construction-conditions-authority-catalog-452-v4") throw Unresolved();
        var direct = definition.SourceFields.GetValueOrDefault("country_modifier", []);
        var triggered = definition.SourceFields.GetValueOrDefault("triggered_country_modifier", []);
        if (direct is not [var unconditional] || triggered.Count != 2) throw Unresolved();
        foreach (var block in direct.Concat(triggered)) RequireProvenance(block, definition.Provenance);
        RequireBlock(unconditional);
        if (unconditional.Children.Count != 2
            || unconditional.Children.Count(node => node.Key == "category_archaeostudies_research_speed_mult" && Value(node) == .25m) != 1
            || unconditional.Children.Where(node => node.Key == "country_resource_max_minor_artifacts_add").ToArray() is not [var baseline]
            || Value(baseline) != 2000) throw Unresolved();
        decimal addition = Value(baseline);
        var storageBlocks = 0;
        var speedBlocks = 0;
        foreach (var block in triggered)
        {
            RequireBlock(block);
            var potentials = block.Children.Where(node => node.Key == "potential").ToArray();
            var effects = block.Children.Where(node => node.Key != "potential").ToArray();
            if (potentials is not [var potential] || effects is not [var effect]) throw Unresolved();
            RequireBlock(potential);
            if (effect.Key == "category_archaeostudies_research_speed_mult" && Value(effect) == .25m)
            {
                if (potential.Children is not [var relic] || !Scalar(relic, "is_planet_class", "pc_relic")) throw Unresolved();
                speedBlocks++;
                // Эта ветвь не меняет cap; её условие/эффект потребит research consumer.
                continue;
            }
            if (effect.Key != "country_resource_max_minor_artifacts_add" || Value(effect) != 3000) throw Unresolved();
            if (potential.Children is not [var exists, var owner] || !Scalar(exists, "exists", "owner")
                || owner.Key != "owner") throw Unresolved();
            RequireBlock(owner);
            if (owner.Children is not [var perk] || !Scalar(perk, "has_ascension_perk", "ap_archaeoengineers")) throw Unresolved();
            storageBlocks++;
            var truth = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(potential, context, conditions, evaluateBody: true).Truth;
            if (truth == ConstructionTruth.Unknown) throw Unresolved();
            if (truth == ConstructionTruth.True) addition = checked(addition + Value(effect));
        }
        if (storageBlocks != 1 || speedBlocks != 1) throw Unresolved();
        return addition;
    }

    private static void RequireBlock(PlanetEconomyCondition node)
    { if (node.Operator != "=" || node.Value is not null || node.Children.Count == 0) throw Unresolved(); }
    private static bool Scalar(PlanetEconomyCondition node, string key, string value)
        => node.Key == key && node.Operator == "=" && node.Value == value && node.Children.Count == 0;
    private static void RequireProvenance(PlanetEconomyCondition node, PlanetEconomyProvenance definition, int depth = 0)
    {
        if (depth > 8 || node.Provenance.RelativePath != definition.RelativePath || node.Provenance.Sha256 != definition.Sha256
            || node.Provenance.Line < definition.Line || node.Provenance.Column <= 0) throw Unresolved();
        foreach (var child in node.Children) RequireProvenance(child, definition, depth + 1);
    }
    private static decimal Value(PlanetEconomyCondition node) => node.Operator == "=" && node.Children.Count == 0
        && decimal.TryParse(node.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value : throw Unresolved();
    private static InvalidDataException Unresolved() => new("faculty-country-capacity-unresolved");
}
