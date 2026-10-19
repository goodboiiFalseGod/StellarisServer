using System.Globalization;
using Stellaris.Core.Content.Construction;
using static Stellaris.Core.Construction.ConstructionAssessor;

namespace Stellaris.Core.Construction;

public sealed class ConstructionFunctionalCivicCollection
{
    public ConstructionCatalogProjection Source { get; }
    public ConstructionOrdinaryTemplate Template { get; }
    public ConstructionContext Context { get; }
    public ConstructionCondition Potential { get; }
    public ConstructionCondition Possible { get; }
    public ConstructionCondition ValidatedApplicability { get; }
    public IReadOnlyList<ConstructionAmount> ConstructionOperands { get; }
    public IReadOnlyList<ConstructionField> ExcludedModifiers { get; }
    public IReadOnlyList<ConstructionGate> Gates { get; }

    private ConstructionFunctionalCivicCollection(ConstructionCatalogProjection source, ConstructionOrdinaryTemplate template, ConstructionContext context)
    {
        Source = source;
        Template = template;
        Context = context;
        var definition = source.Definitions["civic_functional_architecture"];
        var evaluator = new Evaluator(context, governmentTemplate: template);
        Potential = Requirements("potential");
        Possible = Requirements("possible");
        ValidatedApplicability = !template.Civics.Contains(definition.Id) ? Known(false) : context.SourceFacts?.ValidCivics is { } valid ? Known(valid.Contains(definition.Id)) : Unknown("validated-civic-applicability");
        var gates = new List<ConstructionGate>();
        gates.AddRange(Potential.BlockingGates.Concat(Possible.BlockingGates).Concat(ValidatedApplicability.BlockingGates));
        if (definition.Fields.Any(field => field.Key is not ("potential" or "possible" or "modifier" or "random_weight" or "ai_weight" or "description"))) { gates.Add(new("dependency-unresolved", "civic-additional-declaration")); }
        var modifier = definition.Fields.Select((field, index) => (field, index)).Where(pair => pair.field.Key == "modifier").ToArray();
        var amounts = new List<ConstructionAmount>();
        var excluded = new List<ConstructionField>();
        var malformed = modifier.Length != 1 || modifier[0].field.Operator != "=" || modifier[0].field.Scalar is not null;
        if (!malformed)
        {
            var path = "/" + modifier[0].index.ToString(CultureInfo.InvariantCulture) + ":modifier";
            foreach (var (field, index) in modifier[0].field.Children.Select((field, index) => (field, index)))
            {
                if (field.Key == "add_attunement_the_composer_of_strands") { excluded.Add(field); continue; }
                if (field.Key is not ("planet_structures_cost_mult" or "planet_building_build_speed_mult")) { malformed = true; continue; }
                var fieldPath = path + "/" + index.ToString(CultureInfo.InvariantCulture) + ":" + field.Key;
                var parameters = source.Parameters.Where(parameter => parameter.DefinitionId == definition.Id && parameter.FieldPath == fieldPath).ToArray();
                if (field.Operator != "=" || field.Scalar is null || field.Children.Count != 0 || parameters.Length != 1 || parameters[0].Operator != "=" || parameters[0].Origin != field.Origin || amounts.Any(amount => amount.Resource == field.Key)) { malformed = true; continue; }
                amounts.Add(new(field.Key, parameters[0].Value, field.Origin));
            }
            if (amounts.Count != 2) { malformed = true; }
        }
        if (malformed)
        {
            amounts.Clear();
            gates.Insert(0, new("source-collection-unavailable", "civic-modifier-shape"));
        }
        ConstructionOperands = Array.AsReadOnly(amounts.ToArray());
        ExcludedModifiers = Array.AsReadOnly(excluded.ToArray());
        Gates = Array.AsReadOnly(gates.Distinct().Take(256).ToArray());

        ConstructionCondition Requirements(string key)
        {
            var fields = definition.Fields.Where(field => field.Key == key).ToArray();
            return fields.Length != 1 || fields[0].Operator != "=" || fields[0].Scalar is not null ? Unknown("civic-" + key + "-not-declared-or-malformed") : evaluator.Condition(fields[0].Children, Scope.Government);
        }
    }

    public static ConstructionFunctionalCivicCollection Collect(ConstructionCatalogProjection source, string templateId, ConstructionContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (source.ProfileId != "construction-functional-civic-catalog-452-v1" || source.Definitions.Count != 1 || !source.Definitions.TryGetValue("civic_functional_architecture", out var civic) || civic.Kind != "civic" || civic.Origin.Source != "common/governments/civics/00_civics.txt") { throw new ArgumentException("Invalid functional civic source catalog.", nameof(source)); }
        return new(source, ConstructionOrdinaryTemplate.Get(templateId), context);
    }
}
