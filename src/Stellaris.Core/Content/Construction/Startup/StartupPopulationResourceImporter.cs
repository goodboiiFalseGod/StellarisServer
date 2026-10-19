using System.Collections.ObjectModel;
using Stellaris.Core.Content;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Construction.Startup;

public sealed record StartupPopulationResourcePlan(decimal Population, IReadOnlyDictionary<string, decimal> StartingStocks,
    IReadOnlyDictionary<string, decimal> StartingResourceMaximums, ContentManifest Sources);

/// <summary>Импортирует только стартовые organic-pop/resource ветви для обычного origin_default.</summary>
public sealed class StartupPopulationResourceImporter
{
    private const string StartEffects = "common/scripted_effects/01_start_of_game_effects.txt";
    private const string StartTriggers = "common/scripted_triggers/00_scripted_triggers_ai.txt";
    private const string OriginScript = "common/inline_scripts/game_start/origin_default.txt";
    private const string Defines = "common/defines/00_defines.txt";

    public StartupPopulationResourcePlan Import(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        var source = new ContentSession(gameRoot);
        if (source.Version != "v4.5.2") { throw new InvalidDataException("M3 ordinary start требует точный Stellaris 4.5.2."); }
        var effects = source.LoadFile(StartEffects).Root;
        var triggers = source.LoadFile(StartTriggers).Root;
        var origin = source.LoadFile(OriginScript).Root;
        var defines = source.LoadFile(Defines).Root;
        var resourceDefinitions = source.LoadFile("common/strategic_resources/00_strategic_resources.txt").Root;

        var bulk = RequiredNumber(source, defines, "POP_BULK_UNIT_SIZE");
        if (bulk <= 0) { throw new InvalidDataException("POP_BULK_UNIT_SIZE должен быть положительным."); }
        var popEffect = Unique(effects, "generate_start_pops").AsBlock();
        var regularCap = ResolveRegularPopulationCap(source, popEffect, bulk);
        var originBonus = ResolveOriginPopulationAddition(source, popEffect);
        ValidateOriginFoodTrigger(origin);

        var resourceEffect = Unique(effects, "give_starting_resources_effect").AsBlock();
        var baseline = ResolveBaselineStocks(source, resourceEffect);
        var food = ResolveTriggerStocks(source, resourceEffect, "country_uses_food_game_start", triggers, expectedTrigger: "food");
        var consumerGoods = ResolveTriggerStocks(source, resourceEffect, "country_uses_consumer_goods_game_start", triggers, expectedTrigger: "consumer_goods");
        Merge(baseline, food);
        Merge(baseline, consumerGoods);
        var stocks = new ReadOnlyDictionary<string, decimal>(baseline.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        var maximums = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var resource in stocks.Keys)
        {
            var definition = Unique(resourceDefinitions, resource).AsBlock();
            var maximum = definition.Optional("max");
            if (maximum is not null) { maximums.Add(resource, source.Number(maximum)); }
        }
        var manifest = source.Manifest("m3-start-population-resources-452-v1", new[]
        {
            "Обычный organic origin_default, regular empire без reduced-pop origin; origin-default food trigger подтверждён source inline script.",
            "Запасы включают только общий start resource block и проверенные food/consumer-goods trigger ветви; population upkeep не импортируется."
        });
        return new StartupPopulationResourcePlan(checked(regularCap + originBonus), stocks,
            new ReadOnlyDictionary<string, decimal>(maximums), manifest);
    }

    private static decimal ResolveRegularPopulationCap(ContentSession source, ScriptBlock effect, decimal bulk)
    {
        var candidates = Descendants(effect).Where(entry => entry.Key == "if" && entry.Block is not null)
            .Select(entry => entry.AsBlock())
            .Where(block => HasPredicate(block, "is_regular_empire", "yes") && HasPredicate(block, "has_reduced_pop_origin", "no"))
            .SelectMany(block => Descendants(block).Where(entry => entry.Key == "while" && entry.Block is not null).Select(entry => entry.AsBlock()))
            .Where(block => HasComparison(block, "pop_amount", "<"))
            .ToArray();
        if (candidates.Length != 1) { throw new InvalidDataException($"Ожидалась одна regular-pop source ветвь, найдено {candidates.Length}."); }
        var limit = FindComparisonValue(source, candidates[0], "pop_amount", "<");
        if (limit <= 0 || limit % bulk != 0) { throw new InvalidDataException("Regular-pop cap несовместим с source POP_BULK_UNIT_SIZE."); }
        return limit;
    }

    private static decimal ResolveOriginPopulationAddition(ContentSession source, ScriptBlock effect)
    {
        var candidates = Descendants(effect).Where(entry => entry.Key == "if" && entry.Block is not null)
            .Select(entry => entry.AsBlock()).Where(block => HasPredicate(block, "has_origin", "origin_default"))
            .ToArray();
        if (candidates.Length != 1) { throw new InvalidDataException($"Ожидалась одна origin_default pop source ветвь, найдено {candidates.Length}."); }
        var groups = Descendants(candidates[0]).Where(entry => entry.Key == "create_pop_group" && entry.Block is not null)
            .Select(entry => entry.AsBlock()).ToArray();
        if (groups.Length != 1) { throw new InvalidDataException("origin_default должен задавать ровно один стартовый pop group."); }
        var size = groups[0].Optional("size");
        if (size is null) { throw new InvalidDataException("origin_default starting pop group lacks explicit size."); }
        return source.Number(size);
    }

    private static Dictionary<string, decimal> ResolveBaselineStocks(ContentSession source, ScriptBlock effect)
    {
        var grants = Descendants(effect).Where(entry => entry.Key == "if" && entry.Block is not null)
            .Select(entry => entry.AsBlock()).Where(block => HasPredicate(block, "has_country_flag", "start_resources_granted", negated: true))
            .ToArray();
        if (grants.Length != 1) { throw new InvalidDataException($"Ожидалась одна guarded start resource branch, найдено {grants.Length}."); }
        var owner = Descendants(grants[0]).Where(entry => entry.Key == "owner" && entry.Block is not null)
            .Select(entry => entry.AsBlock()).FirstOrDefault(block => block.Named("add_resource").Any())
            ?? throw new InvalidDataException("Базовая start resource branch не содержит owner scope.");
        var result = ReadResourceGrants(source, owner);
        if (result.Count == 0) { throw new InvalidDataException("Базовый start resource block пуст."); }
        return result;
    }

    private static Dictionary<string, decimal> ResolveTriggerStocks(ContentSession source, ScriptBlock effect, string triggerId,
        ScriptBlock triggerSource, string expectedTrigger)
    {
        var triggerDefinition = Unique(triggerSource, triggerId).AsBlock();
        if (expectedTrigger == "food" && !HasPredicate(triggerDefinition, "has_trait", "trait_organic"))
        {
            throw new InvalidDataException("country_uses_food_game_start source no longer confirms organic species.");
        }
        if (expectedTrigger == "consumer_goods" && !HasPredicate(triggerDefinition, "is_gestalt", "no"))
        {
            throw new InvalidDataException("country_uses_consumer_goods_game_start source no longer confirms non-gestalt empires.");
        }
        var startGuard = Descendants(effect).Where(entry => entry.Key == "if" && entry.Block is not null)
            .Select(entry => entry.AsBlock()).SingleOrDefault(block => HasPredicate(block, "has_country_flag", "start_resources_granted", negated: true))
            ?? throw new InvalidDataException("Source start-resource guard is missing or ambiguous.");
        var candidateOwners = Descendants(startGuard).Where(entry => entry.Key is "if" or "else_if" && entry.Block is not null)
            .Select(entry => entry.AsBlock()).Where(block => block.Optional("limit") is { Block: not null } limit
                && HasPredicate(limit.AsBlock(), triggerId, "yes"))
            .SelectMany(block => Descendants(block).Where(entry => entry.Key == "owner" && entry.Block is not null)
                .Where(entry => entry.AsBlock().Named("add_resource").Any()))
            .GroupBy(entry => entry.Source + ":" + entry.Line, StringComparer.Ordinal)
            .Select(group => group.First().AsBlock()).ToArray();
        var matches = candidateOwners.Select(owner => ReadResourceGrants(source, owner))
            .Where(result => result.Count == 1 && result.TryGetValue(expectedTrigger, out var amount) && amount > 0).ToArray();
        if (matches.Length != 1) { throw new InvalidDataException($"Ожидался один положительный {expectedTrigger} start grant под {triggerId}, найдено {matches.Length}."); }
        return matches[0];
    }

    private static Dictionary<string, decimal> ReadResourceGrants(ContentSession source, ScriptBlock owner)
    {
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var grant in owner.Named("add_resource"))
        {
            if (grant.Block is null) { throw new InvalidDataException($"{grant.Source}:{grant.Line}: add_resource должен быть блоком."); }
            foreach (var amount in grant.AsBlock().Entries)
            {
                if (!result.TryAdd(amount.Key, source.Number(amount)))
                {
                    throw new InvalidDataException($"Повторный start resource key {amount.Key}.");
                }
            }
        }
        return result;
    }

    private static void ValidateOriginFoodTrigger(ScriptBlock origin)
    {
        if (!HasPredicate(origin, "has_origin", "origin_default") || !HasPredicate(origin, "country_uses_food_game_start", "yes"))
        {
            throw new InvalidDataException("origin_default inline script no longer selects food districts for food-using founders.");
        }
    }

    private static decimal RequiredNumber(ContentSession source, ScriptBlock block, string key)
    {
        var entries = Descendants(block).Where(entry => entry.Key == key && entry.Operator == "=" && entry.Scalar is not null).ToArray();
        if (entries.Length != 1) { throw new InvalidDataException($"Expected one source define {key}, found {entries.Length}."); }
        return source.Number(entries[0]);
    }

    private static decimal FindComparisonValue(ContentSession source, ScriptBlock block, string key, string op)
    {
        var entries = Descendants(block).Where(entry => entry.Key == key && entry.Operator == op && entry.Scalar is not null).ToArray();
        if (entries.Length != 1) { throw new InvalidDataException($"Expected one source comparison {key} {op}, found {entries.Length}."); }
        return source.Number(entries[0]);
    }

    private static bool HasComparison(ScriptBlock block, string key, string op) => Descendants(block).Any(entry => entry.Key == key && entry.Operator == op && entry.Scalar is not null);

    private static bool HasPredicate(ScriptBlock block, string key, string value, bool negated = false)
    {
        var matches = Descendants(block).Where(entry => entry.Key == key && entry.Scalar == value && entry.Operator == "=").ToArray();
        return matches.Length == 1 && (!negated || Descendants(block).Any(entry => entry.Key == "NOT" && entry.Block is not null));
    }

    private static ScriptEntry Unique(ScriptBlock block, string key)
    {
        var entries = block.Named(key).ToArray();
        if (entries.Length != 1 || entries[0].Operator != "=") { throw new InvalidDataException($"Expected one source entry {key}, found {entries.Length}."); }
        return entries[0];
    }

    private static IEnumerable<ScriptEntry> Descendants(ScriptBlock block)
    {
        foreach (var entry in block.Entries)
        {
            yield return entry;
            if (entry.Block is not null)
            {
                foreach (var nested in Descendants(entry.AsBlock())) { yield return nested; }
            }
        }
    }

    private static void Merge(Dictionary<string, decimal> target, IReadOnlyDictionary<string, decimal> add)
    {
        foreach (var item in add) { target[item.Key] = checked(target.GetValueOrDefault(item.Key) + item.Value); }
    }
}
