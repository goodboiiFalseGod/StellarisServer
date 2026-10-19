using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Stellaris.Core.Content;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Research;

public sealed record TechnologyIcon(string RelativePath, string Sha256, long ByteLength);
public sealed record TechnologyEffect(string Key, decimal? Value, string? Diagnostic);

public sealed record TechnologyDefinition(string Id, string Area, decimal? Cost, int Tier, IReadOnlyList<string> Categories,
    IReadOnlyList<string> Prerequisites, bool Starting, bool Rare, ScriptBlock? Potential, ScriptBlock? StartingPotential,
    decimal Weight, ScriptBlock? WeightRules, IReadOnlyList<string> Effects, string Source, int Line,
    IReadOnlyList<string> Gates)
{
    public IReadOnlyList<ScriptBlock> SwapTriggers { get; init; } = Array.AsReadOnly(Array.Empty<ScriptBlock>());
    public IReadOnlyList<ScriptEntry> SourceFields { get; init; } = Array.AsReadOnly(Array.Empty<ScriptEntry>());
    public int MaximumLevels { get; init; } = 1;
    public decimal CostPerLevel { get; init; }
    public IReadOnlyList<string> WeightGroups { get; init; } = Array.AsReadOnly(Array.Empty<string>());
    public IReadOnlyDictionary<string, decimal> GroupWeightMultipliers { get; init; } = new Dictionary<string, decimal>().ToFrozenDictionary();
    public TechnologyIcon? Icon { get; init; }
    public string? IconDiagnostic { get; init; }
    public IReadOnlyList<TechnologyEffect> EffectDetails { get; init; } = Array.AsReadOnly(Array.Empty<TechnologyEffect>());
    public ScriptBlock? CostRules { get; init; }
}
public sealed record ResearchTier(int Tier, int PreviouslyUnlocked, ScriptBlock? WeightRules);
public sealed class TechnologyCatalog
{
    public IReadOnlyDictionary<string, TechnologyDefinition> Technologies { get; }
    public IReadOnlyDictionary<int, ResearchTier> Tiers { get; }
    public int Alternatives { get; }
    public decimal PreviousOptionMultiplier { get; }
    public decimal NoLeaderPenalty { get; }
    public ContentManifest? Manifest { get; }
    public IReadOnlyDictionary<string, string?> CategoryParents { get; }
    public ScriptBlock WorkforceComposites { get; }
    public IReadOnlyDictionary<string, ScriptBlock> EmpireModifiers { get; }
    public TechnologyCatalog(IEnumerable<TechnologyDefinition> technologies, IEnumerable<ResearchTier> tiers,
        int alternatives, decimal previousOptionMultiplier, decimal noLeaderPenalty, ContentManifest? manifest = null,
        IReadOnlyDictionary<string, string?>? categoryParents = null, ScriptBlock? workforceComposites = null,
        IReadOnlyDictionary<string, ScriptBlock>? empireModifiers = null)
    {
        if (alternatives < 1 || alternatives > 100 || previousOptionMultiplier < 0) { throw new ArgumentOutOfRangeException(nameof(alternatives)); }
        Technologies = technologies.Select(item => item with { Categories = Array.AsReadOnly(item.Categories.ToArray()),
            Prerequisites = Array.AsReadOnly(item.Prerequisites.ToArray()), Effects = Array.AsReadOnly(item.Effects.ToArray()),
            Gates = Array.AsReadOnly(item.Gates.ToArray()), SwapTriggers = Array.AsReadOnly(item.SwapTriggers.ToArray()),
            SourceFields = Array.AsReadOnly(item.SourceFields.ToArray()), WeightGroups = Array.AsReadOnly(item.WeightGroups.ToArray()),
            GroupWeightMultipliers = item.GroupWeightMultipliers.ToFrozenDictionary(StringComparer.Ordinal), EffectDetails = Array.AsReadOnly(item.EffectDetails.ToArray())
        }).ToFrozenDictionary(item => item.Id, StringComparer.Ordinal);
        Tiers = tiers.ToFrozenDictionary(item => item.Tier);
        Alternatives = alternatives; PreviousOptionMultiplier = previousOptionMultiplier; NoLeaderPenalty = noLeaderPenalty; Manifest = manifest;
        CategoryParents = (categoryParents ?? new Dictionary<string, string?>()).ToFrozenDictionary(StringComparer.Ordinal);
        WorkforceComposites = workforceComposites ?? new ScriptBlock([]);
        EmpireModifiers = (empireModifiers ?? new Dictionary<string, ScriptBlock>()).ToFrozenDictionary(StringComparer.Ordinal);
        foreach (var item in Technologies.Values)
        {
            if (!ResearchCountry.AreaIds.Contains(item.Area) || item.Cost < 0 || item.Weight < 0 || item.Tier < 0
                || item.MaximumLevels < -1 || item.MaximumLevels == 0 || item.CostPerLevel < 0) { throw new ArgumentException("Недопустимое определение технологии."); }
        }
    }
    public IReadOnlyList<ResearchEligibility> AssessStarting(ResearchFacts facts)
    {
        var empty = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);
        return Array.AsReadOnly(Technologies.Values.Where(item => item.Starting).OrderBy(item => item.Id, StringComparer.Ordinal).Select(item =>
        {
            var gates = new List<string>();
            foreach (var condition in new[] { ("potential", item.Potential), ("starting-potential", item.StartingPotential) })
            {
                var result = ResearchRules.Condition(condition.Item2, facts, empty);
                if (result != true) { gates.Add(condition.Item1 + (result is null ? "-unknown" : "-false")); }
            }
            foreach (var swap in item.SwapTriggers)
            {
                var result = ResearchRules.Condition(swap, facts, empty);
                if (result != false) { gates.Add(result is null ? "swap-unknown" : "swap-unsupported"); }
            }
            gates.AddRange(item.Gates.Where(gate => gate is "ambiguous-field:start_tech" or "ambiguous-field:potential" or "ambiguous-field:starting_potential"));
            return new ResearchEligibility(item.Id, gates.Count == 0, null, Array.AsReadOnly(gates.ToArray()));
        }).ToArray());
    }
}

/// <summary>Ограниченный импорт метаданных: неподдержанный cost/trigger не становится доступным контентом.</summary>
public sealed class TechnologyImporter
{
    public TechnologyCatalog Import(string gameRoot)
    {
        var source = new ContentSession(gameRoot);
        if (source.Version.TrimStart('v') != "4.5.2") { throw new InvalidDataException("Исследования требуют локальную Stellaris 4.5.2."); }
        source.LoadFolder("common/scripted_variables");
        source.LoadFolder("common/technology");
        source.LoadFolder("common/economic_categories");
        source.LoadFolder("common/ethics");
        source.LoadFolder("common/governments/civics");
        var composites = source.LoadFile("common/static_modifiers/24_static_modifiers_jobs.txt");
        var defines = source.LoadFile("common/defines/00_defines.txt");
        ScriptEntry Define(string key) => Find(defines.Root, key).Single();
        var expandedEntries = 0;
        ScriptBlock Normalize(ScriptBlock block, int depth = 0, IReadOnlyDictionary<string,string>? arguments = null)
        {
            if (depth > 32 || expandedEntries > 500000) { throw new InvalidDataException("Превышен бюджет inline scripts технологий."); }
            string Substitute(string value)
            {
                if (arguments is not null) { foreach (var argument in arguments) { value = value.Replace("$" + argument.Key + "$", argument.Value, StringComparison.Ordinal); } }
                return value;
            }
            var entries = new List<ScriptEntry>();
            foreach (var entry in block.Entries)
            {
                expandedEntries++;
                if (entry.Key == "inline_script")
                {
                    var script = entry.Scalar ?? entry.Block?.Optional("script")?.Scalar;
                    if (script is not null) { script = Substitute(script); }
                    if (script is null || !Regex.IsMatch(script, "^[A-Za-z0-9_/]+$", RegexOptions.CultureInvariant))
                    { entries.Add(entry); continue; }
                    var bindings = entry.Block?.Entries.Where(field => field.Key != "script" && field.Scalar is not null)
                        .ToDictionary(field => field.Key, field => Substitute(field.Scalar!), StringComparer.Ordinal) ?? new Dictionary<string,string>();
                    var inline = source.LoadFile("common/inline_scripts/" + script + ".txt");
                    entries.AddRange(Normalize(inline.Root, depth + 1, bindings).Entries); continue;
                }
                var scalar = entry.Scalar is null ? null : Substitute(entry.Scalar);
                var substituted = new ScriptEntry(Substitute(entry.Key), entry.Operator, scalar, entry.Block, entry.Line, entry.Column, entry.Source);
                if (scalar?.StartsWith('@') == true) { scalar = source.Number(substituted).ToString(System.Globalization.CultureInfo.InvariantCulture); }
                entries.Add(new(substituted.Key, entry.Operator, scalar, entry.Block is null ? null : Normalize(entry.Block, depth + 1, arguments), entry.Line, entry.Column, entry.Source));
            }
            return new(entries);
        }
        var technologies = new List<TechnologyDefinition>();
        var icons = new Dictionary<string, TechnologyIcon>(StringComparer.Ordinal);
        TechnologyIcon? Icon(string name)
        {
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant)) { return null; }
            var relative = "gfx/interface/icons/technologies/" + name + ".dds";
            if (icons.TryGetValue(relative, out var existing)) { return existing; }
            var path = Path.Combine(Path.GetFullPath(gameRoot), relative);
            if (!File.Exists(path)) { return null; }
            var length = new FileInfo(path).Length;
            if (length is <= 0 or > 16 * 1024 * 1024) { return null; }
            var bytes = File.ReadAllBytes(path);
            var result = new TechnologyIcon(relative, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);
            icons.Add(relative, result); return result;
        }
        foreach (var file in source.Sources.Where(file => file.Path.StartsWith("common/technology/", StringComparison.Ordinal)
            && file.Path.Count(character => character == '/') == 2).ToArray())
        {
            foreach (var entry in file.Root.Entries.Where(entry => entry.Block is not null && !entry.Key.StartsWith('@')))
            {
                var block = entry.AsBlock();
                var gates = new List<string>();
                ScriptEntry? Field(string key)
                {
                    var fields = block.Named(key).ToArray();
                    if (fields.Length > 1 && (fields.Any(field => field.Block is not null) || fields.Select(field => field.Scalar).Distinct().Count() > 1)) { gates.Add("ambiguous-field:" + key); }
                    return fields.FirstOrDefault();
                }
                var area = Field("area")?.Scalar;
                if (area is null) { continue; }
                var cost = Field("cost");
                decimal? number = cost?.Scalar is not null ? source.Number(cost) : null;
                if (cost is null) { gates.Add("conditional-or-missing-cost"); }
                var maximumLevels = Field("levels") is { } levels ? (int)source.Number(levels) : 1;
                var costPerLevel = Field("cost_per_level") is { } perLevel && perLevel.Scalar is not null ? source.Number(perLevel) : 0;
                if (Field("cost_per_level") is { Block: not null }) { gates.Add("conditional-cost-per-level"); }
                var weight = Field("weight");
                var weightRules = block.Named("weight_modifier").ToArray();
                if (weightRules.Length > 1) { gates.Add("ambiguous-weight-modifier"); }
                var potential = Field("potential")?.AsBlock();
                var startingPotential = Field("starting_potential")?.AsBlock();
                var effects = block.Entries.Where(field => field.Key is "modifier" or "feature_flags" or "on_research_complete"
                    or "unlock_edict" or "unlock_army" or "unlock_policy" or "unlock_decision").SelectMany(field => field.Block is null
                        ? new[] { field.Key + ":" + field.Scalar } : field.AsBlock().Entries.Select(child => field.Key + ":" + child.Key)).ToArray();
                var effectDetails = new List<TechnologyEffect>();
                foreach (var modifier in block.Named("modifier").Where(field => field.Block is not null).SelectMany(field => field.AsBlock().Entries))
                {
                    decimal? value = null;
                    if (modifier.Scalar is not null)
                    {
                        try { value = source.Number(modifier); } catch (InvalidDataException) { /* Нечисловое выражение остаётся метаданными. */ }
                    }
                    effectDetails.Add(new(modifier.Key, value, value is null ? "unsupported-effect-expression" : null));
                }
                var groups = List(Field("weight_groups"));
                var groupMultipliers = (Field("mod_weight_if_group_picked")?.AsBlock().Entries ?? []).ToFrozenDictionary(field => field.Key, source.Number, StringComparer.Ordinal);
                var iconFields = block.Named("icon").ToArray();
                var ambiguousIcon = iconFields.Length > 1 && iconFields.Select(field => field.Scalar).Distinct().Count() > 1;
                var icon = ambiguousIcon ? null : Icon(iconFields.FirstOrDefault()?.Scalar ?? entry.Key);
                technologies.Add(new(entry.Key, area, number, (int)(Field("tier") is { } tier ? source.Number(tier) : 0),
                    List(Field("category")), List(Field("prerequisites")), Field("start_tech")?.Scalar == "yes",
                    Field("is_rare")?.Scalar == "yes", potential is null ? null : Normalize(potential),
                    startingPotential is null ? null : Normalize(startingPotential), weight is null ? source.Number(Define("TECH_WEIGHT_BASE")) : source.Number(weight),
                    weightRules.Length == 1 ? Normalize(weightRules[0].AsBlock()) : null,
                    Array.AsReadOnly(effects), entry.Source, entry.Line, Array.AsReadOnly(gates.ToArray()))
                { SwapTriggers = Array.AsReadOnly(block.Named("technology_swap").Select(swap => Normalize(swap.AsBlock().Required("trigger").AsBlock())).ToArray()), SourceFields = Normalize(block).Entries,
                    MaximumLevels = maximumLevels, CostPerLevel = costPerLevel, WeightGroups = groups, GroupWeightMultipliers = groupMultipliers,
                    Icon = icon, IconDiagnostic = icon is null ? ambiguousIcon ? "ambiguous-icon" : "icon-missing-or-invalid" : null,
                    EffectDetails = Array.AsReadOnly(effectDetails.ToArray()), CostRules = cost?.Block is { } costBlock ? Normalize(costBlock) : null });
            }
        }
        var ids = technologies.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        technologies = technologies.Select(item => item with { Gates = Array.AsReadOnly(item.Gates.Concat(item.Prerequisites.Where(id => !ids.Contains(id)).Select(id => "missing-prerequisite:" + id)).ToArray()) }).ToList();
        var tiers = source.Sources.Where(file => file.Path.StartsWith("common/technology/tier/", StringComparison.Ordinal))
            .SelectMany(file => file.Root.Entries).Where(entry => int.TryParse(entry.Key, out _)).Select(entry => new ResearchTier(int.Parse(entry.Key),
                (int)(entry.AsBlock().Optional("previously_unlocked") is { } count ? source.Number(count) : 0),
                entry.AsBlock().Optional("weight_modifier") is { } rules ? Normalize(rules.AsBlock()) : null)).ToArray();
        var parents = source.Sources.Where(file => file.Path.StartsWith("common/economic_categories/", StringComparison.Ordinal))
            .SelectMany(file => file.Root.Entries).Where(entry => entry.Block is not null && !entry.Key.StartsWith('@'))
            .ToDictionary(entry => entry.Key, entry => entry.AsBlock().Optional("parent")?.Scalar, StringComparer.Ordinal);
        foreach (var id in parents.Keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (string? current = id; current is not null; current = parents.TryGetValue(current, out var parent) ? parent : throw new InvalidDataException("Нет economic category: " + current))
            { if (!seen.Add(current)) { throw new InvalidDataException("Цикл economic category: " + id); } }
        }
        var identities = source.Sources.Where(file => file.Path.StartsWith("common/ethics/", StringComparison.Ordinal) || file.Path.StartsWith("common/governments/civics/", StringComparison.Ordinal))
            .SelectMany(file => file.Root.Entries).Where(entry => entry.Block is not null && !entry.Key.StartsWith('@'))
            .ToDictionary(entry => entry.Key, entry => Normalize(new ScriptBlock(entry.AsBlock().Entries
                .Where(field => field.Key is "modifier" or "country_modifier").Where(field => field.Block is not null).SelectMany(field => field.AsBlock().Entries).ToArray())), StringComparer.Ordinal);
        var normalizedComposites = Normalize(composites.Root);
        var manifest = source.Manifest("technology-452-v1", ["Неизвестные условия и эффекты сохраняют адресную диагностику."]);
        foreach (var icon in icons.Values)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(gameRoot), icon.RelativePath));
            if (bytes.LongLength != icon.ByteLength || Convert.ToHexString(SHA256.HashData(bytes)) != icon.Sha256) { throw new IOException("Иконка технологии изменилась во время импорта: " + icon.RelativePath); }
        }
        manifest = manifest with { Files = Array.AsReadOnly(manifest.Files.Concat(icons.Values.Select(icon => new ManifestFile(icon.RelativePath, icon.Sha256, icon.ByteLength))).OrderBy(file => file.Path, StringComparer.Ordinal).ToArray()) };
        return new(technologies, tiers, (int)source.Number(Define("NUM_TECH_ALTERNATIVES")), source.Number(Define("TECH_WEIGHT_PREVIOUS_OPTION")), source.Number(Define("TECH_RESEARCH_NO_LEADER_PENALTY")), manifest, parents, normalizedComposites, identities);
    }
    private static IReadOnlyList<string> List(ScriptEntry? entry) => Array.AsReadOnly(entry?.AsBlock().Entries.Select(child => child.Scalar ?? child.Key).ToArray() ?? []);
    private static IEnumerable<ScriptEntry> Find(ScriptBlock block, string key)
    {
        foreach (var entry in block.Entries)
        {
            if (entry.Key == key) { yield return entry; }
            if (entry.Block is not null) { foreach (var child in Find(entry.Block, key)) { yield return child; } }
        }
    }
}
