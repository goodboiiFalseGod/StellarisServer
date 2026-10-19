using System.Collections.Frozen;
using System.Globalization;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Research;

public sealed record ResearchConstructionModifier(string SourceKey, decimal Value);

public sealed class ResearchEffectProjection
{
    public decimal AllTechnologySpeedBonus { get; init; }
    /// <summary>Содержит общий бонус ровно один раз; без единицы и штрафа отсутствующего руководителя.</summary>
    public required IReadOnlyDictionary<string, decimal> AreaTechnologySpeedBonuses { get; init; }
    public required IReadOnlyDictionary<string, decimal> AreaResearchMultipliers { get; init; }
    public required IReadOnlyList<Modifier> JobModifiers { get; init; }
    public required IReadOnlyDictionary<string, decimal> JobBonusWorkforce { get; init; }
    public required IReadOnlyList<ResearchConstructionModifier> ConstructionModifiers { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlySet<string>> AppliedTechnologyKeys { get; init; }
    public required IReadOnlyList<string> Coverage { get; init; }
}

/// <summary>Чистая проекция эффектов единственного ResearchCountry, без мутации экономики и прогресса.</summary>
public static class ResearchEffects
{
    public static ResearchEffectProjection Project(ResearchCountry country, PlanetEconomyCatalog economy,
        IReadOnlyList<JobDefinition> runtimeJobs, IReadOnlySet<string> empireIdentities, IReadOnlyDictionary<string, JobResourceClosure>? resourceClosure = null)
        => ProjectCore(country, economy, runtimeJobs, empireIdentities, null, resourceClosure);

    /// <summary>Поддержанные потребителем ключи карточки; не изучает технологию и не меняет уровень.</summary>
    public static IReadOnlySet<string> AppliedKeys(ResearchCountry country, string technologyId, PlanetEconomyCatalog economy,
        IReadOnlyList<JobDefinition> runtimeJobs, IReadOnlySet<string> empireIdentities, IReadOnlyDictionary<string, JobResourceClosure>? resourceClosure = null)
        => ProjectCore(country, economy, runtimeJobs, empireIdentities, technologyId, resourceClosure).AppliedTechnologyKeys.GetValueOrDefault(technologyId)
            ?? Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Эффективные значения одного уровня для карточки, с той же веткой swap, что у численной проекции.</summary>
    public static IReadOnlyList<TechnologyEffect> EffectDetails(ResearchCountry country, string technologyId)
    {
        var technology = country.Catalog.Technologies[technologyId];
        var blocks = EffectiveBlocks(country, technology, out var unknown);
        var entries = (unknown ? new[] { new ScriptBlock(technology.SourceFields) } : blocks)
            .SelectMany(block => block.Named("modifier").Where(field => field.Block is not null))
            .SelectMany(field => field.AsBlock().Entries).Where(entry => entry.Key is not "custom_tooltip" and not "show_only_custom_tooltip");
        return Array.AsReadOnly(entries.Select(entry => new TechnologyEffect(entry.Key,
            !unknown && Number(entry, out var value) ? value : null,
            unknown ? "unknown-effect-swap" : Number(entry, out _) ? null : "unsupported-effect-expression")).ToArray());
    }

    private static IReadOnlyList<ScriptBlock> EffectiveBlocks(ResearchCountry country, TechnologyDefinition technology, out bool unknown)
    {
        var definition = new ScriptBlock(technology.SourceFields);
        var blocks = new List<ScriptBlock> { definition };
        unknown = false;
        foreach (var swap in definition.Named("technology_swap"))
        {
            var body = swap.AsBlock();
            var match = body.Optional("trigger")?.Block is { } trigger ? ResearchRules.Condition(trigger, country.Facts, country.Researched) : null;
            if (match is null) { unknown = true; return []; }
            if (match != true) { continue; }
            if (body.Optional("inherit_effects")?.Scalar == "no") { blocks.Clear(); }
            blocks.Add(body); break;
        }
        return blocks;
    }

    private static ResearchEffectProjection ProjectCore(ResearchCountry country, PlanetEconomyCatalog economy,
        IReadOnlyList<JobDefinition> runtimeJobs, IReadOnlySet<string> identities, string? previewId, IReadOnlyDictionary<string, JobResourceClosure>? resourceClosure)
    {
        var catalog = country.Catalog;
        var modifiers = new List<Modifier>();
        var workforce = runtimeJobs.ToDictionary(job => job.Id, _ => 0m, StringComparer.Ordinal);
        var construction = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var coverage = new List<string>();
        var appliedKeys = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        decimal speed = 0;
        var areaOnly = ResearchCountry.AreaIds.ToDictionary(area => area, _ => 0m, StringComparer.Ordinal);
        foreach (var identity in identities.Order(StringComparer.Ordinal))
        {
            if (!catalog.EmpireModifiers.TryGetValue(identity, out var block)) { coverage.Add(identity + ":unknown-empire-modifiers"); continue; }
            // Остальные identity modifiers принадлежат исходному preset/trait overlay, не повторяем их здесь.
            foreach (var entry in block.Entries.Where(entry => IsSpeed(entry.Key)))
            { if (Number(entry, out var value)) { ApplySpeed(entry.Key, value); } else { coverage.Add(identity + ":unknown-speed-expression:" + entry.Key); } }
        }
        foreach (var id in previewId is null ? country.Researched.Order(StringComparer.Ordinal) : new[] { previewId }.AsEnumerable())
        {
            if (!catalog.Technologies.TryGetValue(id, out var technology)) { throw new InvalidDataException("Нет исследованной технологии: " + id); }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            appliedKeys[id] = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);
            var blocks = EffectiveBlocks(country, technology, out var unknownSwap);
            if (unknownSwap) { coverage.Add(id + ":unknown-effect-swap"); continue; }
            var levels = previewId is null ? country.Levels.GetValueOrDefault(id, 1) : 1;
            foreach (var block in blocks)
            {
                foreach (var modifier in block.Named("modifier").Where(entry => entry.Block is not null))
                {
                    foreach (var entry in modifier.AsBlock().Entries)
                    { if (Apply(entry, levels, new HashSet<string>(StringComparer.Ordinal))) { keys.Add(entry.Key); } }
                }
                foreach (var field in block.Entries.Where(field => field.Key is "feature_flags" or "on_research_complete" or "country_modifier" or "unlock_edict" or "unlock_army" or "unlock_policy" or "unlock_decision"))
                { coverage.Add(id + ":deferred-effect:" + field.Key); }
            }
            appliedKeys[id] = keys.ToFrozenSet(StringComparer.Ordinal);

            bool Apply(ScriptEntry entry, decimal scale, HashSet<string> stack)
            {
                var key = entry.Key;
                if (key is "custom_tooltip" or "show_only_custom_tooltip") { return false; }
                if (!Number(entry, out var raw)) { coverage.Add(id + ":unknown-effect-expression:" + key); return false; }
                var value = checked(raw * scale);
                if (IsSpeed(key)) { ApplySpeed(key, value); return true; }
                if (key is "planet_building_build_speed_mult" or "planet_district_build_speed_mult" or "planet_structures_cost_mult")
                { construction[key] = checked(construction.GetValueOrDefault(key) + value); return true; }
                if (catalog.WorkforceComposites.Optional(key) is { Block: not null } composite)
                {
                    if (!stack.Add(key)) { throw new InvalidDataException("Цикл Workforce composite: " + key); }
                    var supported = false;
                    foreach (var child in composite.AsBlock().Entries) { supported |= Apply(child, value, stack); }
                    stack.Remove(key); return supported;
                }
                if (key.StartsWith("pop_", StringComparison.Ordinal) && key.EndsWith("_bonus_workforce_mult", StringComparison.Ordinal))
                {
                    var target = key[4..^"_bonus_workforce_mult".Length];
                    var matches = runtimeJobs.Where(job => target.StartsWith("cat_", StringComparison.Ordinal)
                        ? economy.Jobs[job.Id].JobStratum == target[4..] : job.Id == target).ToArray();
                    foreach (var job in matches) { workforce[job.Id] = checked(workforce[job.Id] + value); }
                    if (matches.Length == 0) { coverage.Add(id + ":no-runtime-job:" + key); }
                    return matches.Length != 0;
                }
                var applied = new HashSet<(string Category, string Resource, string Operation)>();
                foreach (var job in runtimeJobs)
                {
                    var source = economy.Jobs[job.Id];
                    var categories = new HashSet<string>(StringComparer.Ordinal);
                    var unknownCategory = false;
                    for (string? category = source.EconomicCategory; category is not null;)
                    {
                        if (!categories.Add(category) || !catalog.CategoryParents.TryGetValue(category, out var parent))
                        { coverage.Add(id + ":unknown-economic-category:" + category + ":" + key); unknownCategory = true; break; }
                        category = parent;
                    }
                    if (unknownCategory) { continue; }
                    foreach (var operation in new[] { "produces", "upkeep" })
                    {
                        var baseline = operation == "produces" ? job.Production.Keys : job.Upkeep.Keys;
                        var effective = resourceClosure?.GetValueOrDefault(job.Id);
                        var resources = baseline.Concat(effective is null ? Array.Empty<string>() : operation == "produces" ? effective.Produced : effective.Upkeep).Distinct(StringComparer.Ordinal);
                        foreach (var resource in resources)
                        {
                            if (effective is not null && !economy.Resources.ContainsKey(resource)) { throw new ArgumentException("Unknown effective job resource: " + resource, nameof(resourceClosure)); }
                            var mult = categories.Any(category => key == category + "_" + operation + "_mult" || key == category + "_" + resource + "_" + operation + "_mult");
                            var add = categories.Any(category => key == category + "_" + resource + "_" + operation + "_add");
                            if ((mult || add) && applied.Add((job.Category, resource, operation)))
                            { modifiers.Add(new(job.Category, resource, operation, add ? value : 0, mult ? value : 0)); }
                        }
                    }
                }
                if (applied.Count == 0) { coverage.Add(id + ":deferred-effect:" + key); }
                return applied.Count != 0;
            }
        }
        var areas = areaOnly.ToFrozenDictionary(pair => pair.Key, pair => checked(pair.Value + speed), StringComparer.Ordinal);
        return new()
        {
            AllTechnologySpeedBonus = speed, AreaTechnologySpeedBonuses = areas,
            AreaResearchMultipliers = areas.ToFrozenDictionary(pair => pair.Key, pair => checked(1 + catalog.NoLeaderPenalty + pair.Value), StringComparer.Ordinal),
            JobModifiers = Array.AsReadOnly(modifiers.ToArray()), JobBonusWorkforce = workforce.ToFrozenDictionary(StringComparer.Ordinal),
            ConstructionModifiers = Array.AsReadOnly(construction.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new ResearchConstructionModifier(pair.Key, pair.Value)).ToArray()),
            AppliedTechnologyKeys = appliedKeys.ToFrozenDictionary(StringComparer.Ordinal), Coverage = Array.AsReadOnly(coverage.Distinct(StringComparer.Ordinal).ToArray())
        };

        void ApplySpeed(string key, decimal value)
        {
            if (key == "all_technology_research_speed") { speed = checked(speed + value); }
            else { var area = ResearchCountry.AreaIds.Single(area => key == "country_" + area + "_tech_research_speed"); areaOnly[area] = checked(areaOnly[area] + value); }
        }
    }
    private static bool IsSpeed(string key) => key == "all_technology_research_speed"
        || ResearchCountry.AreaIds.Any(area => key == "country_" + area + "_tech_research_speed");
    private static bool Number(ScriptEntry entry, out decimal value)
        => decimal.TryParse(entry.Scalar, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
}
