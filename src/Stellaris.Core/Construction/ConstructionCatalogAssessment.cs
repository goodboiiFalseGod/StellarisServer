using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using static Stellaris.Core.Construction.ConstructionAssessor;

namespace Stellaris.Core.Construction;

public sealed record ConstructionRawContribution(string Kind, string FieldPath, ConstructionOrigin Origin, ConstructionCondition Condition, IReadOnlyList<ConstructionAmount> Amounts, decimal? RawMultiplier, IReadOnlyList<ConstructionGate> Gates)
{
    public IReadOnlyList<ConstructionGate> BlockingGates => Condition.Truth == ConstructionTruth.False ? Array.Empty<ConstructionGate>() : Array.AsReadOnly(Condition.BlockingGates.Concat(Gates).Distinct().ToArray());
}

public sealed class ConstructionCatalogAssessment
{
    public ConstructionCatalogProjection Source { get; }
    public ConstructionCatalogProjection? ConditionSource { get; }
    public ConstructionContext Context { get; }
    public string DefinitionId { get; }
    public ConstructionCondition Potential { get; }
    public ConstructionCondition Destroy { get; }
    public ConstructionCondition Allow { get; }
    public ConstructionCondition Unlock { get; }
    public ConstructionCondition Prerequisites { get; }
    public IReadOnlyList<ConstructionRawContribution> Contributions { get; }

    internal ConstructionCatalogAssessment(ConstructionCatalogProjection source, ConstructionCatalogProjection? conditionSource, string definitionId, Evaluator evaluator, ConstructionContext context, IReadOnlyList<ConstructionRawContribution> contributions)
    {
        Source = source;
        ConditionSource = conditionSource;
        Context = context;
        DefinitionId = definitionId;
        var fields = source.Definitions[definitionId].Fields;
        Potential = evaluator.Required(fields, "potential");
        Destroy = evaluator.Required(fields, "destroy_trigger");
        Allow = evaluator.Required(fields, "allow");
        Unlock = evaluator.Required(fields, "unlock");
        var prerequisites = fields.Where(field => field.Key == "prerequisites").ToArray();
        Prerequisites = prerequisites.Length == 0 ? Known(true) : prerequisites.Length != 1 || prerequisites[0].Operator != "=" || prerequisites[0].Scalar is not null || prerequisites[0].Children.Any(child => child.Operator != "" || child.Scalar is null) ? Unknown("prerequisites-shape") : context.Technologies is null ? Unknown("technology-context") : Known(prerequisites[0].Children.All(child => context.Technologies.Contains(child.Scalar!)));
        Contributions = contributions;
    }
}

public static class ConstructionCatalogAssessor
{
    /// <summary>Видимость определяется potential; отсутствие поля не скрывает определение.</summary>
    public static ConstructionCondition Visibility(ConstructionCatalogProjection source, string id, ConstructionContext context, ConstructionCatalogProjection conditions)
    {
        var fields = source.Definitions[id].Fields;
        return fields.Any(field => field.Key == "potential") ? new Evaluator(context, conditions).Required(fields, "potential") : Known(true);
    }

    /// <summary>Условия доступа отделены от наличия реализации экономики и профессий.</summary>
    public static ConstructionCondition Eligibility(ConstructionCatalogProjection source, string id, ConstructionContext context, ConstructionCatalogProjection conditions)
    {
        var fields = source.Definitions[id].Fields;
        var evaluator = new Evaluator(context, conditions);
        var values = new List<ConstructionCondition> { Visibility(source, id, context, conditions) };
        foreach (var key in new[] { "allow", "unlock", "possible", "possible_precalc" })
            if (fields.Any(field => field.Key == key)) values.Add(evaluator.Required(fields, key));
        var prerequisites = fields.Where(field => field.Key == "prerequisites").ToArray();
        if (prerequisites.Length != 0)
            values.Add(prerequisites.Length != 1 || prerequisites[0].Operator != "=" || prerequisites[0].Scalar is not null
                || prerequisites[0].Children.Any(child => child.Operator != "" || child.Scalar is null)
                ? Unknown("prerequisites-shape") : context.Technologies is null ? Unknown("technology-context")
                : Known(prerequisites[0].Children.All(child => context.Technologies.Contains(child.Scalar!))));
        return values.Any(value => value.Truth == ConstructionTruth.False) ? Known(false)
            : values.Any(value => value.Truth == ConstructionTruth.Unknown)
                ? new(ConstructionTruth.Unknown, Array.AsReadOnly(values.SelectMany(value => value.Gates).Distinct().Take(256).ToArray())) : Known(true);
    }

    /// <summary>Строгая оценка native owner AST; факты вида передаёт только фактический мир.</summary>
    public static ConstructionCondition EvaluateOwnerSourceCondition(IReadOnlyList<ConstructionField> fields,
        ConstructionContext context, ConstructionCatalogProjection conditions)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return context.OwnerExists == true ? new Evaluator(context, conditions).Condition(fields, Scope.Owner) : Unknown("owner-scope");
    }

    /// <summary>Оценка сохранённого economy condition тем же strict source evaluator, что и каталог.</summary>
    public static ConstructionCondition EvaluatePlanetEconomyCondition(PlanetEconomyCondition condition,
        ConstructionContext context, ConstructionCatalogProjection conditions, bool evaluateBody = false)
    {
        var remaining = 50000;
        ConstructionField Convert(PlanetEconomyCondition node, int depth)
        {
            if (depth > 96 || --remaining < 0) { throw new InvalidDataException("Economy condition exceeds source budget."); }
            return new(node.Key, node.Operator, node.Value, node.Children.Select(child => Convert(child, depth + 1)),
                new(node.Provenance.RelativePath, node.Provenance.Line, node.Provenance.Column), []);
        }
        var field = Convert(condition, 0);
        return new Evaluator(context, conditions).Condition(evaluateBody ? field.Children : [field], Scope.Planet);
    }

    /// <summary>Оценка тела импортированного planet helper без аргументов в доверенном контексте.</summary>
    public static ConstructionCondition EvaluatePlanetHelper(ConstructionCatalogProjection conditions, string helperId, ConstructionContext context)
    {
        var helper = conditions.Definitions.GetValueOrDefault(helperId) ?? conditions.ScriptedConditions.GetValueOrDefault(helperId);
        if (helper is null || helper.Kind is not ("planetCondition" or "scriptedCondition")) { throw new ArgumentException("Planet condition helper is unavailable.", nameof(helperId)); }
        return new Evaluator(context, conditions).Condition(helper.Fields, Scope.Planet);
    }

    /// <summary>Оценка тела импортированного owner helper без аргументов в доверенном контексте.</summary>
    public static ConstructionCondition EvaluateOwnerHelper(ConstructionCatalogProjection conditions, string helperId, ConstructionContext context)
    {
        if (!conditions.Definitions.TryGetValue(helperId, out var helper) || helper.Kind != "ownerCondition") { throw new ArgumentException("Owner condition helper is unavailable.", nameof(helperId)); }
        return context.OwnerExists == true ? new Evaluator(context, conditions).Condition(helper.Fields, Scope.Owner) : Unknown("owner-scope");
    }

    public static ConstructionCatalogAssessment Assess(ConstructionCatalogProjection source, string definitionId, ConstructionContext context, ConstructionCatalogProjection? conditionSource = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (!source.Definitions.TryGetValue(definitionId, out var definition)) { throw new ArgumentException("Definition is not present in this construction catalog.", nameof(definitionId)); }
        var evaluator = new Evaluator(context, conditionSource);
        var parameters = source.Parameters.Where(parameter => parameter.DefinitionId == definitionId).ToDictionary(parameter => parameter.FieldPath, StringComparer.Ordinal);
        var contributions = new List<ConstructionRawContribution>();
        foreach (var (field, index) in definition.Fields.Select((field, index) => (field, index)))
        {
            var path = Path("", index, field);
            if (field.Key == "base_buildtime")
            {
                var gates = new List<ConstructionGate>();
                var amount = Number(field, path, true, false, parameters, gates);
                contributions.Add(new("buildtime", path, field.Origin, Known(true), Array.AsReadOnly(amount is null ? Array.Empty<ConstructionAmount>() : new[] { amount }), null, Array.AsReadOnly(gates.ToArray())));
            }
            else if (field.Key == "resources")
            {
                var categories = field.Children.Where(child => child.Key == "category").ToArray();
                if (field.Operator != "=" || field.Scalar is not null || categories.Length != 1 || categories[0].Operator != "=" || categories[0].Scalar is null)
                {
                    contributions.Add(Unsupported("resources", path, field));
                    continue;
                }
                foreach (var (child, childIndex) in field.Children.Select((child, childIndex) => (child, childIndex)))
                {
                    if (child.Key == "category")
                    {
                        if (child.Operator != "=" || child.Scalar is null) { contributions.Add(Unsupported("resources", Path(path, childIndex, child), child)); }
                        continue;
                    }
                    var childPath = Path(path, childIndex, child);
                    contributions.Add(child.Key is "cost" or "upkeep" ? Extract(child.Key, childPath, child, "trigger", evaluator, parameters, false) : Unsupported("resources", childPath, child));
                }
            }
            else if (field.Key is "planet_modifier" or "district_planet_modifier" or "triggered_planet_modifier" or "triggered_district_planet_modifier")
            {
                contributions.Add(Extract(field.Key, path, field, "potential", evaluator, parameters, true, definition.Kind == "zone"));
            }
            else if (field.Key is "triggered_country_modifier" or "triggered_planet_pop_group_modifier_for_species")
            {
                var speciesGroup = field.Key == "triggered_planet_pop_group_modifier_for_species";
                var contribution = Extract(field.Key, path, field, "potential", evaluator, parameters, true, speciesGroup: speciesGroup);
                contributions.Add(contribution with
                {
                    Condition = speciesGroup ? Unknown("catalog-species-group-context") : contribution.Condition,
                    Gates = Array.AsReadOnly(Unknown(speciesGroup ? "catalog-species-group-consumer" : "catalog-country-consumer").Gates.Concat(contribution.Gates).Distinct().Take(256).ToArray())
                });
            }
        }
        // Missing or duplicate mandatory numerical containers remain visible, not a zero-valued quote.
        foreach (var key in new[] { "base_buildtime", "resources" })
        {
            if (definition.Kind != "zoneSlot" && definition.Fields.Count(field => field.Key == key) != 1)
            {
                contributions.Add(new(key, "", definition.Origin, Known(true), Array.Empty<ConstructionAmount>(), null, Unknown("catalog-container-" + key).Gates));
            }
        }
        return new(source, conditionSource, definitionId, evaluator, context, Array.AsReadOnly(contributions.ToArray()));
    }

    private static ConstructionRawContribution Extract(string kind, string path, ConstructionField block, string conditionKey, Evaluator evaluator, IReadOnlyDictionary<string, ConstructionCatalogParameter> parameters, bool modifier, bool zone = false, bool speciesGroup = false)
    {
        var gates = new List<ConstructionGate>();
        var amounts = new List<ConstructionAmount>();
        if (block.Operator != "=" || block.Scalar is not null) { return Unsupported(kind, path, block); }
        var conditions = block.Children.Where(field => field.Key == conditionKey).ToArray();
        var condition = speciesGroup ? Unknown("catalog-species-group-context") : conditions.Length == 0 ? kind.StartsWith("triggered_", StringComparison.Ordinal) ? Unknown("catalog-potential-not-declared") : Known(true) : evaluator.Required(block.Children, conditionKey);
        decimal? multiplier = null;
        var triggeredModifier = modifier && kind.StartsWith("triggered_", StringComparison.Ordinal);
        var multipliers = block.Children.Select((field, index) => (field, index)).Where(pair => pair.field.Key == "mult" || triggeredModifier && pair.field.Key == "multiplier").ToArray();
        if (multipliers.Length > 1) { gates.AddRange(Unknown("catalog-multiplier-shape").Gates); }
        else if (multipliers.Length == 1)
        {
            var (field, index) = multipliers[0];
            multiplier = field.Scalar == "value:storm_building_production_multiplier" ? evaluator.Value(field, gates) : Number(field, Path(path, index, field), false, triggeredModifier, parameters, gates)?.Value;
        }
        var nested = block.Children.Select((field, index) => (field, index)).Where(pair => pair.field.Key == "modifier").ToArray();
        var values = block.Children.Select((field, index) => (field, path: Path(path, index, field))).Where(pair => pair.field.Key != conditionKey && pair.field.Key != "mult" && !(triggeredModifier && pair.field.Key == "multiplier")).ToArray();
        if (nested.Length != 0)
        {
            if (!modifier || nested.Length != 1 || values.Length != 1 || nested[0].field.Operator != "=" || nested[0].field.Scalar is not null) { gates.AddRange(Unknown("catalog-modifier-shape").Gates); }
            else { values = nested[0].field.Children.Select((field, index) => (field, path: Path(Path(path, nested[0].index, nested[0].field), index, field))).ToArray(); }
        }
        foreach (var (field, fieldPath) in values)
        {
            var housing = field.Key == "planet_housing_add";
            var job = field.Key.StartsWith("job_", StringComparison.Ordinal) && field.Key.EndsWith("_add", StringComparison.Ordinal) && field.Key.Length > 8;
            var slots = zone && field.Key == "zone_building_slots_add";
            var economic = !zone && (kind is "planet_modifier" or "triggered_planet_modifier") && (field.Key is "planet_jobs_produces_mult" or "pop_bonus_workforce_mult" || ConstructionJobWorkforceMapping.Find(field.Key) is not null);
            var categoryJob = (kind is "planet_modifier" or "triggered_planet_modifier" or "district_planet_modifier" or "triggered_district_planet_modifier") && ConstructionJobEconomyMapping.Find(field.Key) is not null;
            if (modifier && !(housing || job || slots || economic || categoryJob))
            {
                gates.AddRange(Unknown("catalog-modifier-" + field.Key).Gates);
                continue;
            }
            if (amounts.Any(amount => amount.Resource == field.Key)) { gates.AddRange(Unknown("catalog-duplicate-amount").Gates); }
            var amount = Number(field, fieldPath, false, modifier && (housing || job || slots || categoryJob || economic && field.Key == "planet_jobs_produces_mult"), parameters, gates);
            if (amount is not null) { amounts.Add(amount); }
        }
        if (amounts.Count == 0) { gates.AddRange(Unknown("catalog-empty-amounts").Gates); }
        return new(kind, path, block.Origin, condition, Array.AsReadOnly(amounts.ToArray()), multiplier, Array.AsReadOnly(gates.Distinct().Take(256).ToArray()));
    }

    private static ConstructionAmount? Number(ConstructionField field, string path, bool positive, bool signed, IReadOnlyDictionary<string, ConstructionCatalogParameter> parameters, List<ConstructionGate> gates)
    {
        if (field.Operator != "=" || field.Scalar is null || field.Children.Count != 0 || !parameters.TryGetValue(path, out var number) || number.Operator != "=" || number.Origin != field.Origin || (positive ? number.Value <= 0 : !signed && number.Value < 0))
        {
            gates.AddRange(Unknown("catalog-number-unresolved").Gates);
            return null;
        }
        return new(field.Key, number.Value, field.Origin);
    }

    private static ConstructionRawContribution Unsupported(string kind, string path, ConstructionField field) => new(kind, path, field.Origin, Known(true), Array.Empty<ConstructionAmount>(), null, Unknown("catalog-container-shape").Gates);
    private static string Path(string parent, int index, ConstructionField field) => parent + "/" + index + ":" + field.Key;
}
