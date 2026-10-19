using System.Collections.Frozen;
using System.Globalization;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Research;

/// <summary>Ключ факта: scope/оператор:аргумент; отсутствующий ключ означает неизвестность.</summary>
public sealed class ResearchFacts
{
    public IReadOnlyDictionary<string, bool?> Values { get; }
    public IReadOnlyDictionary<string, decimal> Numbers { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<ResearchFacts>> Collections { get; }
    public ResearchFacts(IReadOnlyDictionary<string, bool?> values, IReadOnlyDictionary<string, decimal>? numbers = null,
        IReadOnlyDictionary<string, IReadOnlyList<ResearchFacts>>? collections = null)
    {
        Values = values.ToFrozenDictionary(StringComparer.Ordinal);
        Numbers = (numbers ?? new Dictionary<string, decimal>()).ToFrozenDictionary(StringComparer.Ordinal);
        Collections = (collections ?? new Dictionary<string, IReadOnlyList<ResearchFacts>>()).ToFrozenDictionary(pair => pair.Key,
            pair => (IReadOnlyList<ResearchFacts>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.Ordinal);
    }
    public static string NumericKey(string scope, string predicate, string? argument = null) => scope + "/" + predicate + (argument is null ? "" : ":" + argument);
    public static string Key(string scope, string predicate, string argument) => scope + "/" + predicate + ":" + argument;
    public bool? Get(string scope, string predicate, string argument) => Values.GetValueOrDefault(Key(scope, predicate, argument));
}
public sealed record ResearchEligibility(string TechnologyId, bool Available, decimal? Weight, IReadOnlyList<string> Gates);
public interface IResearchWeightProvider
{
    decimal Multiplier(TechnologyDefinition technology, ResearchFacts facts, IReadOnlySet<string> researched);
}

internal static class ResearchRules
{
    public static IEnumerable<string> MissingFacts(ScriptBlock? block, ResearchFacts facts, IReadOnlySet<string> researched, string scope = "country")
    {
        if (block is null) { yield break; }
        foreach (var field in block.Entries)
        {
            if (field.Key is "factor" or "add" or "base" or "complex_trigger_modifier") { continue; }
            if (field.Key == "modifier" && field.Block is not null)
            { foreach (var key in MissingFacts(field.Block, facts, researched, scope)) { yield return key; } continue; }
            if (Field(field, facts, researched, scope) is not null) { continue; }
            if (field.Key is "AND" or "OR" or "NOT" or "NOR" or "owner" or "owner?" or "country" or "hidden_trigger")
            {
                foreach (var key in MissingFacts(field.Block, facts, researched, field.Key is "owner" or "owner?" or "country" ? "country" : scope)) { yield return key; }
            }
            else { yield return ResearchFacts.Key(scope, field.Key, field.Scalar is null ? Serialize(field.AsBlock()) : field.Operator == "=" ? field.Scalar : field.Operator + field.Scalar); }
        }
    }
    public static bool? Condition(ScriptBlock? block, ResearchFacts facts, IReadOnlySet<string> researched, string scope = "country")
        => block is null ? true : All(block.Entries.Select(field => Field(field, facts, researched, scope)));
    private static bool? All(IEnumerable<bool?> input) { var values = input.ToArray(); return values.Contains(false) ? false : values.Contains(null) ? null : true; }
    private static bool? Any(IEnumerable<bool?> input) { var values = input.ToArray(); return values.Contains(true) ? true : values.Contains(null) ? null : false; }
    private static bool? Field(ScriptEntry entry, ResearchFacts facts, IReadOnlySet<string> researched, string scope)
    {
        if (entry.Scalar is not null && decimal.TryParse(entry.Scalar, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var threshold)
            && facts.Numbers.TryGetValue(ResearchFacts.NumericKey(scope, entry.Key), out var actual))
        { return Compare(actual, threshold, entry.Operator); }
        if (entry.Operator != "=") { return entry.Scalar is null ? null : facts.Get(scope, entry.Key, entry.Operator + entry.Scalar); }
        if (entry.Key is "AND" or "OR" or "NOT" or "NOR")
        {
            if (entry.Block is null) { return null; }
            var children = entry.Block.Entries.Select(child => Field(child, facts, researched, scope));
            var value = entry.Key is "AND" or "NOT" ? All(children) : Any(children);
            return entry.Key is "NOT" or "NOR" ? value is null ? null : !value.Value : value;
        }
        if (entry.Key is "owner" or "owner?" or "country" or "hidden_trigger") { return entry.Block is null ? null : Condition(entry.Block, facts, researched, entry.Key == "hidden_trigger" ? scope : "country"); }
        if (entry.Key == "always" && entry.Scalar is "yes" or "no") { return entry.Scalar == "yes"; }
        if (entry.Key == "has_technology" && entry.Scalar is not null) { return researched.Contains(entry.Scalar); }
        if (entry.Key is "any_owned_planet" or "every_owned_planet" or "any_owned_species" or "any_owned_pop_group")
        {
            if (entry.Block is null || !facts.Collections.TryGetValue(ResearchFacts.NumericKey(scope, entry.Key), out var collection))
            { return entry.Block is null ? null : facts.Get(scope, entry.Key, Serialize(entry.Block)); }
            var childScope = entry.Key is "any_owned_planet" or "every_owned_planet" ? "planet" : entry.Key == "any_owned_species" ? "species" : "pop_group";
            var matches = collection.Select(child => Condition(entry.Block, child, researched, childScope));
            return entry.Key == "every_owned_planet" ? All(matches) : Any(matches);
        }
        if (entry.Key is "num_districts" or "num_buildings" && entry.Block is not null && entry.Block.Entries.Count == 2
            && entry.Block.Optional("type")?.Scalar is { } type && entry.Block.Optional("value") is { Scalar: not null } comparison
            && decimal.TryParse(comparison.Scalar, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var required)
            && facts.Numbers.TryGetValue(ResearchFacts.NumericKey(scope, entry.Key, type), out var count))
        { return Compare(count, required, comparison.Operator); }
        // Сложные scoped triggers допускаются только как явно предоставленные сервером факты.
        if (entry.Block is not null) { return facts.Get(scope, entry.Key, Serialize(entry.Block)); }
        return entry.Scalar is null ? null : facts.Get(scope, entry.Key, entry.Scalar);
    }
    private static bool? Compare(decimal actual, decimal required, string operation) => operation switch
    { "=" => actual == required, ">" => actual > required, ">=" => actual >= required, "<" => actual < required, "<=" => actual <= required, "!=" => actual != required, _ => null };
    private static string Serialize(ScriptBlock block) => string.Join(";", block.Entries.Select(entry => entry.Key + entry.Operator + (entry.Scalar ?? "{" + Serialize(entry.AsBlock()) + "}")));
    public static decimal? Weight(ScriptBlock? block, decimal initial, ResearchFacts facts, IReadOnlySet<string> researched,
        TechnologyCatalog catalog, string area, IReadOnlyDictionary<string, int>? completedLevels = null)
    {
        if (block is null) { return initial; }
        decimal value = initial;
        foreach (var field in block.Entries)
        {
            if (field.Operator != "=") { return null; }
            if (field.Key is "factor" or "base" or "add")
            {
                if (!decimal.TryParse(field.Scalar, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) { return null; }
                value = field.Key == "factor" ? checked(value * number) : field.Key == "base" ? number : checked(value + number);
            }
            else if (field.Key == "modifier" && field.Block is not null)
            {
                var condition = new ScriptBlock(field.Block.Entries.Where(child => child.Key is not "factor" and not "add" and not "base"));
                var applies = Condition(condition, facts, researched);
                if (applies is null) { return null; }
                if (applies == true)
                {
                    var modified = Weight(new ScriptBlock(field.Block.Entries.Where(child => child.Key is "factor" or "add" or "base")), value, facts, researched, catalog, area, completedLevels);
                    if (modified is null) { return null; }
                    value = modified.Value;
                }
            }
            else if (field.Key == "complex_trigger_modifier" && field.Block is not null)
            {
                var content = field.Block;
                if (content.Optional("mode")?.Scalar != "add" || !decimal.TryParse(content.Optional("mult")?.Scalar, CultureInfo.InvariantCulture, out var mult)) { return null; }
                var trigger = content.Optional("trigger")?.Scalar;
                int count;
                if (trigger == "num_researched_techs_of_tier" && int.TryParse(content.Optional("parameters")?.Block?.Optional("tier")?.Scalar, out var tier))
                { count = researched.Count(id => catalog.Technologies.TryGetValue(id, out var definition) && definition.Area == area && definition.Tier == tier); }
                else if (trigger == "num_repeatable_techs") { count = (completedLevels ?? researched.ToDictionary(id => id, _ => 1)).Where(pair => catalog.Technologies.TryGetValue(pair.Key, out var definition)
                    && definition.Area == area && definition.MaximumLevels != 1).Sum(pair => pair.Value); }
                else { return null; }
                value = checked(value + count * mult);
            }
            else { return null; }
        }
        return value;
    }
}
