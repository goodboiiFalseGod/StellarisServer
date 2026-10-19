using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Content.Construction;

namespace Stellaris.Core.Construction;

/// <summary>Трёхзначная оценка сохранённых economy conditions по явным scoped facts.</summary>
public static class ConstructionLiveConditions
{
    internal static bool? EvaluateScopeModifierPotential(ConstructionField field, ConstructionContext context, IReadOnlyDictionary<string, bool> facts, string scope = "planet")
    {
        if (field.Operator != "=") return null;
        if (field.Scalar is { } value)
        {
            if (scope == "planet" && field.Key == "has_deposit") return context.SourceFacts?.Deposits?.Contains(value);
            if (scope == "planet" && field.Key == "has_modifier") return context.Modifiers?.Contains(value);
            if (value is "yes" or "no") return facts.TryGetValue(scope + ":" + field.Key, out var flag) ? flag == (value == "yes") : null;
            return facts.TryGetValue(scope + ":" + field.Key + ":" + value, out var literal) ? literal : null;
        }
        var nextScope = field.Key is "owner" or "owner?" ? "owner" : field.Key is "planet" or "planet?" ? "planet" : scope;
        var values = field.Children.Select(child => EvaluateScopeModifierPotential(child, context, facts, nextScope)).ToArray();
        bool? and = values.Contains(false) ? false : values.Contains(null) ? null : true;
        bool? or = values.Contains(true) ? true : values.Contains(null) ? null : false;
        return field.Key switch
        {
            "potential" or "AND" or "owner" or "owner?" or "planet" or "planet?" or "hidden_trigger" => and,
            "OR" => or, "NOT" when values.Length == 1 => and is null ? null : !and.Value,
            "NOR" => or is null ? null : !or.Value, _ => null
        };
    }

    public static bool? Evaluate(PlanetEconomyCondition? condition, IReadOnlyDictionary<string, bool> facts, string scope = "planet")
    {
        if (condition is null) { return true; }
        if (condition.Operator != "=") { return null; }
        bool? And(IEnumerable<bool?> values) { var all = values.ToArray(); return all.Contains(false) ? false : all.Contains(null) ? null : true; }
        bool? Or(IEnumerable<bool?> values) { var all = values.ToArray(); return all.Contains(true) ? true : all.Contains(null) ? null : false; }
        bool? Not(bool? value) => value is null ? null : !value.Value;
        var key = condition.Key;
        if (condition.Value is null)
        {
            var children = condition.Children.Select(child => Evaluate(child, facts, key == "owner" ? "owner" : scope));
            return key switch { "AND" or "owner" or "hidden_trigger" => And(children), "OR" => Or(children), "NOT" when condition.Children.Count == 1 => Not(And(children)), "NOR" => Not(Or(children)), "NAND" => Not(And(children)), _ => null };
        }
        var value = condition.Value;
        if (value is "yes" or "no") { return facts.TryGetValue(scope + ":" + key, out var boolean) ? boolean == (value == "yes") : null; }
        return facts.TryGetValue(scope + ":" + key + ":" + value, out var found) ? found : null;
    }
}
