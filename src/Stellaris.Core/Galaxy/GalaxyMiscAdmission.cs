using System.Collections.ObjectModel;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public sealed record GalaxyMiscAdmissionSummary(string PolicyId, IReadOnlyDictionary<string, int> UseCounts,
    IReadOnlyDictionary<string, int> AttemptCounts);

internal sealed record GalaxyMiscAdmissionResult(GalaxyCompiledInitializer Initializer,
    GalaxyMiscAdmissionSummary Summary, long SelectionDraws, long SpawnDraws);
internal sealed record GalaxyMiscAdmissionCandidate(string Id, long Weight, int? EffectiveMax, long Chance);
internal sealed record GalaxyMiscAdmissionAttempt(string Id, bool Spawned, int Draws);

internal static class GalaxyMiscAdmissionPolicy
{
    public static void EnsureProgress(string id, long weight, int? effectiveMax, long chance, GalaxySourceLocation location)
    {
        if (weight >= 0 && effectiveMax is null && chance <= 0)
            throw new GalaxyStructureException("misc-admission-no-progress", id + ": неограниченное определение с нулевым шансом не может продвинуть admission.", location);
    }

    public static int SelectIndex(IReadOnlyList<GalaxyMiscAdmissionCandidate> candidates, IReadOnlyDictionary<string, int> used, uint draw)
    {
        var eligible = candidates.Where(candidate => candidate.Weight >= 0 &&
            (candidate.EffectiveMax is null || used.GetValueOrDefault(candidate.Id) < candidate.EffectiveMax)).ToArray();
        if (eligible.Length == 0) throw new InvalidOperationException("misc-admission-empty");
        var total = eligible.Aggregate(0L, (sum, candidate) => checked(sum + candidate.Weight));
        var indexes = candidates.Select((candidate, index) => (candidate.Id, index)).ToDictionary(item => item.Id, item => item.index, StringComparer.Ordinal);
        var cursor = (long)(draw & 0x7FFFFFFF);
        if (total > 0)
        {
            cursor %= total;
            for (var i = 0; i < eligible.Length; i++)
            {
                if (cursor < eligible[i].Weight) return indexes[eligible[i].Id];
                cursor -= eligible[i].Weight;
            }
            throw new InvalidOperationException("misc-admission-weight-range");
        }
        return indexes[eligible[(int)(cursor % (uint)eligible.Length)].Id];
    }

    public static GalaxyMiscAdmissionAttempt TryAttempt(IReadOnlyList<GalaxyMiscAdmissionCandidate> candidates,
        IDictionary<string, int> used, IDictionary<string, int> attempts, Func<uint> next)
    {
        var selected = candidates[SelectIndex(candidates,
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(used, StringComparer.Ordinal)), next())];
        attempts[selected.Id] = checked((attempts.TryGetValue(selected.Id, out var attemptCount) ? attemptCount : 0) + 1);
        var spawned = NativeGenerationRules.PassesSpawnChance(selected.Chance, next());
        if (selected.EffectiveMax is not null) used[selected.Id] = checked((used.TryGetValue(selected.Id, out var useCount) ? useCount : 0) + 1);
        return new(selected.Id, spawned, 2);
    }

    public static bool ClusterGate(GalaxySyntaxValue modifier, bool empireCluster)
    {
        var ors = modifier.Entries.Where(entry => entry.Key == "OR").ToArray();
        if (ors.Length != 1 || ors[0].Operator != "=" || ors[0].Value.Kind != GalaxySyntaxKind.Block || modifier.Entries.Count != 2)
            throw new GalaxyStructureException("misc-condition", "Поддерживается только исходный cluster gate с OR.", modifier.Location);
        var matched = false;
        foreach (var condition in ors[0].Value.Entries)
        {
            if (condition.Operator != "=" || condition.Value.Kind != GalaxySyntaxKind.Scalar)
                throw new GalaxyStructureException("misc-condition", "Условие кластера должно быть scalar.", condition.Location);
            var truth = condition.Key switch
            {
                "is_fe_cluster" when condition.Value.Text is "yes" or "no" => false,
                "is_marauder_cluster" when condition.Value.Text is "yes" or "no" => false,
                "has_star_flag" when condition.Value.Text == "empire_cluster" => empireCluster,
                _ => throw new GalaxyStructureException("misc-condition", "Неизвестное активное условие кластера " + condition.Key + ".", condition.Location)
            };
            matched |= truth;
        }
        if (ors[0].Value.Entries.Count != 3) throw new GalaxyStructureException("misc-condition", "Ожидались все три исходных cluster predicates.", ors[0].Location);
        return matched;
    }
}

/// <summary>Finite, source-ordered admission for the explicit playable initializer pool.</summary>
internal sealed class GalaxyMiscAdmission
{
    public const string PolicyId = "SourceMiscAdmissionV1";
    public const long FixedScale = 100000;
    private static readonly HashSet<string> SupportedIds = new(StringComparer.Ordinal)
    {
        "basic_init_01", "basic_init_02", "basic_init_03", "basic_init_04", "basic_init_05", "basic_init_06",
        "asteroid_init_01", "binary_init_01", "binary_init_02", "trinary_init_01", "trinary_init_02",
        "special_init_01", "special_init_08", "special_init_09", "trappist_initializer", "abandoned_gateways_01"
    };
    private readonly GalaxyRuntimeCatalog catalog;
    private readonly GalaxyNumericValues numeric;
    private readonly ResolvedGalaxyRequest request;
    private readonly IReadOnlyList<GalaxyCompiledInitializer> pool;
    private readonly Dictionary<string, int> used = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> attempts = new(StringComparer.Ordinal);

    public GalaxyMiscAdmission(GalaxyRuntimeCatalog catalog, ResolvedGalaxyRequest request)
    {
        this.catalog = catalog; this.request = request; numeric = new(catalog);
        var definitions = catalog.Initializers.Where(definition => SupportedIds.Contains(definition.Id))
            .OrderBy(definition => definition.SourceOrder).ToArray();
        if (definitions.Length != SupportedIds.Count || definitions.Select(definition => definition.Id).Distinct(StringComparer.Ordinal).Count() != SupportedIds.Count)
            throw new GalaxyStructureException("misc-pool-closure", "Поддержанный initializer pool неполон или неоднозначен.", new("common/solar_system_initializers", 1, 1));
        pool = Array.AsReadOnly(definitions.Select(definition => GalaxyInitializerExecution.Compile(catalog, definition.Id)).ToArray());
        foreach (var initializer in pool)
            if (GalaxyInitializerExecution.Scalar(initializer.Fields, "usage") != "misc_system_init")
                throw new GalaxyStructureException("misc-usage", initializer.Definition.Id + ": usage должен быть misc_system_init.", initializer.Definition.Location);
    }

    public GalaxyMiscAdmissionResult Select(Func<uint> next, bool empireCluster, NativeFixedPoint position,
        IReadOnlyList<(int Site, NativeFixedPoint Position)> gateways, IReadOnlySet<int> adjacentSites)
    {
        long selectionDraws = 0, spawnDraws = 0;
        while (true)
        {
            var eligible = new List<(GalaxyCompiledInitializer Initializer, long Weight, int? EffectiveMax, long Chance)>();
            foreach (var initializer in pool)
            {
                var max = EffectiveMax(initializer);
                if (max is { } maximum && used.GetValueOrDefault(initializer.Definition.Id) >= maximum) continue;
                var weight = UsageWeight(initializer, empireCluster, position, gateways, adjacentSites);
                if (weight < 0) continue;
                var chance = SpawnChance(initializer);
                GalaxyMiscAdmissionPolicy.EnsureProgress(initializer.Definition.Id, weight, max, chance, initializer.Definition.Location);
                eligible.Add((initializer, weight, max, chance));
            }
            if (eligible.Count == 0)
                throw new GalaxyStructureException("misc-admission-empty", "Все поддержанные initializer исчерпаны или имеют отрицательный вес.", new("common/solar_system_initializers", 1, 1));

            var candidates = eligible.Select(item => new GalaxyMiscAdmissionCandidate(item.Initializer.Definition.Id, item.Weight, item.EffectiveMax, item.Chance)).ToArray();
            var attempt = GalaxyMiscAdmissionPolicy.TryAttempt(candidates, used, attempts, next);
            selectionDraws++; spawnDraws++;
            var choice = eligible.Single(item => item.Initializer.Definition.Id == attempt.Id);
            if (attempt.Spawned)
                return new(choice.Initializer, Snapshot(), selectionDraws, spawnDraws);
        }
    }

    public GalaxyMiscAdmissionSummary Snapshot() => new(PolicyId,
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(used, StringComparer.Ordinal)),
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(attempts, StringComparer.Ordinal)));

    internal long EvaluateWeight(string initializerId, bool empireCluster) => UsageWeight(
        pool.Single(initializer => initializer.Definition.Id == initializerId), empireCluster, default, [], new HashSet<int>());

    private long UsageWeight(GalaxyCompiledInitializer initializer, bool empireCluster, NativeFixedPoint position,
        IReadOnlyList<(int Site, NativeFixedPoint Position)> gateways, IReadOnlySet<int> adjacentSites)
    {
        var entries = initializer.Fields.Entries.Where(entry => entry.Key == "usage_odds").ToArray();
        if (entries.Length != 1 || entries[0].Operator != "=") throw Error("misc-usage-odds", initializer, "usage_odds должен встречаться ровно один раз.");
        var value = entries[0].Value;
        if (value.Kind == GalaxySyntaxKind.Scalar) return Raw(numeric.Evaluate(value, initializer.Definition.File.Path), value.Location);
        if (value.Kind != GalaxySyntaxKind.Block) throw Error("misc-usage-odds", initializer, "Ожидался scalar или block.");
        long weight = 0;
        var baseEntries = value.Entries.Where(entry => entry.Key == "base").ToArray();
        if (baseEntries.Length != 1 || baseEntries[0].Operator != "=") throw Error("misc-usage-base", initializer, "Ожидался один base=.");
        weight = Raw(numeric.Evaluate(baseEntries[0].Value, initializer.Definition.File.Path), baseEntries[0].Location);
        foreach (var entry in value.Entries.Where(entry => entry.Key != "base"))
        {
            if (entry.Key == "inline_script" && entry.Value.Kind == GalaxySyntaxKind.Block)
            {
                weight = ApplyHabitableInline(initializer, entry, weight, empireCluster);
                continue;
            }
            if (entry.Key == "modifier" && entry.Value.Kind == GalaxySyntaxKind.Block)
            {
                if (entry.Operator != "=") throw Error("misc-weight-field", initializer, "modifier должен использовать =.");
                weight = initializer.Definition.Id == "abandoned_gateways_01"
                    ? ApplyGatewayModifier(initializer, entry, weight, position, gateways, adjacentSites)
                    : ApplyClusterModifier(initializer, entry, weight, empireCluster);
                continue;
            }
            throw Error("misc-weight-field", initializer, "Не поддержано активное поле usage_odds " + entry.Key + ".");
        }
        return weight;
    }

    private long ApplyGatewayModifier(GalaxyCompiledInitializer initializer, GalaxySyntaxEntry entry, long weight,
        NativeFixedPoint position, IReadOnlyList<(int Site, NativeFixedPoint Position)> gateways, IReadOnlySet<int> adjacentSites)
    {
        if (entry.Operator != "=") throw Error("gateway-modifier", initializer, "modifier должен использовать =.");
        var factor = GalaxyInitializerExecution.Optional(entry.Value, "factor");
        if (factor is null || factor.Operator != "=" || factor.Value.Kind != GalaxySyntaxKind.Scalar || Raw(numeric.Evaluate(factor.Value, initializer.Definition.File.Path), factor.Value.Location) != 0)
            throw Error("gateway-modifier", initializer, "Ожидался source factor=0.");
        var sourceFields = initializer.Fields.Entries.Where(field => field.Key == "max_instances").ToArray();
        if (sourceFields.Length != 1 || sourceFields[0].Operator != "=") throw Error("gateway-source", initializer, "Ожидался один max_instances=.");
        var closest = GalaxyInitializerExecution.Optional(entry.Value, "closest_system");
        var neighbor = GalaxyInitializerExecution.Optional(entry.Value, "any_neighbor_system");
        if (entry.Value.Entries.Count != 2 || (closest is null) == (neighbor is null)) throw Error("gateway-modifier", initializer, "Ожидался factor и один gateway proximity predicate.");
        bool Exists(GalaxySyntaxValue block)
        {
            if (GalaxyInitializerExecution.Optional(block, "has_megastructure") is not { Operator: "=", Value.Kind: GalaxySyntaxKind.Scalar } structure || structure.Value.Text != "gateway_ruined")
                throw Error("gateway-modifier", initializer, "Поддерживается только gateway_ruined predicate.");
            if (block.Entries.Count != 1) throw Error("gateway-modifier", initializer, "Лишнее поле proximity predicate.");
            return true;
        }
        if (closest is { Value.Kind: GalaxySyntaxKind.Block } c)
        {
            var distance = GalaxyInitializerExecution.Optional(c.Value, "distance");
            var structure = GalaxyInitializerExecution.Optional(c.Value, "has_megastructure");
            if (c.Operator != "=" || c.Value.Entries.Count != 2 || structure?.Operator != "=" || structure.Value.Kind != GalaxySyntaxKind.Scalar || structure.Value.Text != "gateway_ruined" || distance?.Operator != "=" || distance.Value.Kind != GalaxySyntaxKind.Block)
                throw Error("gateway-modifier", initializer, "Неизвестная closest_system форма.");
            var source = GalaxyInitializerExecution.Optional(distance.Value, "source");
            var max = GalaxyInitializerExecution.Optional(distance.Value, "max_distance");
            if (source?.Operator != "=" || source.Value.Kind != GalaxySyntaxKind.Scalar || source.Value.Text != "prev" || max?.Operator != "<=" || max.Value.Kind != GalaxySyntaxKind.Scalar || numeric.Evaluate(max.Value, initializer.Definition.File.Path) != 200 || distance.Value.Entries.Count != 2)
                throw Error("gateway-modifier", initializer, "Ожидался max_distance <= 200.");
            // Локальная метрика покрытия; исходник не задаёт type, поэтому это не утверждение о native Euclidean metric.
            if (gateways.Any(g => Squared(position, g.Position) <= 200L * 100000 * 200L * 100000)) return 0;
            return weight;
        }
        if (neighbor is { Value.Kind: GalaxySyntaxKind.Block } n)
        {
            if (n.Operator != "=" || n.Value.Entries.Count != 1 || !Exists(n.Value)) throw Error("gateway-modifier", initializer, "Неизвестная any_neighbor_system форма.");
            if (gateways.Any(g => adjacentSites.Contains(g.Site))) return 0;
            return weight;
        }
        throw Error("gateway-modifier", initializer, "Неверный proximity block.");
    }

    private static decimal Squared(NativeFixedPoint a, NativeFixedPoint b) =>
        (decimal)(a.X - b.X) * (a.X - b.X) + (decimal)(a.Y - b.Y) * (a.Y - b.Y);

    private long ApplyHabitableInline(GalaxyCompiledInitializer initializer, GalaxySyntaxEntry inline, long weight, bool empireCluster)
    {
        var script = GalaxyInitializerExecution.Optional(inline.Value, "script");
        if (script?.Value.Kind != GalaxySyntaxKind.Scalar || script.Value.Text != "solar_system_initializers/initializer_modifiers_habitable_world_systems_mult")
            throw Error("misc-inline-script", initializer, "Не поддержан inline script.");
        var values = inline.Value.Entries.Where(entry => entry.Key == "VALUE").ToArray();
        if (values.Length != 1 || values[0].Value.Kind != GalaxySyntaxKind.Scalar)
            throw Error("misc-inline-parameter", initializer, "Ожидался один scalar VALUE.");
        var source = catalog.Lookup(GalaxyRuntimeFamily.InlineScript, script.Value.Text!);
        if (source.Status != GalaxyLookupStatus.Unique) throw Error("misc-inline-closure", initializer, script.Value.Text! + ": " + source.Status);
        var expanded = GalaxyInitializerExecution.ExpandSource(catalog, source.Candidates[0].Syntax.Value,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["VALUE"] = values[0].Value.Text! });
        var modifier = expanded.Entries.Where(entry => entry.Key == "modifier").ToArray();
        var trigger = expanded.Entries.Where(entry => entry.Key == "complex_trigger_modifier").ToArray();
        if (modifier.Length != 1 || trigger.Length != 1 || expanded.Entries.Count != 2)
            throw Error("misc-inline-closure", initializer, "Форма inline source отличается от поддержанного factor script.");
        if (GalaxyMiscAdmissionPolicy.ClusterGate(modifier[0].Value, empireCluster)) return 0;
        var complex = trigger[0].Value;
        var triggerId = GalaxyInitializerExecution.Scalar(complex, "trigger");
        var mode = GalaxyInitializerExecution.Scalar(complex, "mode");
        var setting = GalaxyInitializerExecution.Optional(complex, "parameters") is { Value.Kind: GalaxySyntaxKind.Block } parameters
            ? GalaxyInitializerExecution.Scalar(parameters.Value, "setting") : null;
        var sourceMult = GalaxyInitializerExecution.Optional(complex, "mult");
        if (triggerId != "check_galaxy_setup_value" || mode != "factor" || setting != "habitable_worlds_scale" || sourceMult is null)
            throw Error("misc-complex-trigger", initializer, "Не поддержан complex_trigger_modifier.");
        var factor = Multiply(Raw(request.Settings.HabitableMultiplier, inline.Location),
            Raw(numeric.Evaluate(sourceMult.Value, source.Candidates[0].File.Path), sourceMult.Value.Location), inline.Location);
        // mode=factor multiplies the previous weight by trigger-result * VALUE;
        // after expansion VALUE is the `mult` read above, so it is applied once.
        return Multiply(weight, factor, inline.Location);
    }

    private long ApplyClusterModifier(GalaxyCompiledInitializer initializer, GalaxySyntaxEntry entry, long weight, bool empireCluster)
    {
        var factor = GalaxyInitializerExecution.Optional(entry.Value, "factor");
        if (factor is not { Operator: "=", Value.Kind: GalaxySyntaxKind.Scalar }) throw Error("misc-modifier-factor", initializer, "Ожидался scalar factor=.");
        if (!GalaxyMiscAdmissionPolicy.ClusterGate(entry.Value, empireCluster)) return weight;
        return Multiply(weight, Raw(numeric.Evaluate(factor.Value, initializer.Definition.File.Path), factor.Value.Location), entry.Location);
    }

    private int? EffectiveMax(GalaxyCompiledInitializer initializer)
    {
        var entries = initializer.Fields.Entries.Where(entry => entry.Key == "max_instances").ToArray();
        if (entries.Length == 0) return null;
        if (entries.Length != 1 || entries[0].Value.Kind != GalaxySyntaxKind.Scalar) throw Error("misc-max-instances", initializer, "Ожидался один scalar max_instances.");
        var value = numeric.Evaluate(entries[0].Value, initializer.Definition.File.Path);
        if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue) throw Error("misc-max-instances", initializer, "max_instances должен помещаться в int32.");
        var setting = initializer.Fields.Entries.Where(entry => entry.Key == "scaled_spawn_settings").ToArray();
        if (initializer.Definition.Id == "abandoned_gateways_01")
        {
            if (setting.Length != 1 || setting[0].Operator != "=" || setting[0].Value.Kind != GalaxySyntaxKind.Scalar || setting[0].Value.Text != "num_gateways")
                throw Error("gateway-source", initializer, "Ожидался scaled_spawn_settings=num_gateways.");
        }
        else if (setting.Length != 0) throw Error("misc-scaled-setting", initializer, "scaled_spawn_settings не поддержан активным профилем.");
        var scale = initializer.Definition.Id == "abandoned_gateways_01"
            ? Raw(request.Settings.SpecialContent.Gateways, initializer.Definition.Location) : FixedScale;
        return NativeGenerationRules.EffectiveInstanceLimit((int)value, scale);
    }

    private long SpawnChance(GalaxyCompiledInitializer initializer)
    {
        var standard = initializer.Fields.Entries.Where(entry => entry.Key == "spawn_chance").ToArray();
        var scaled = initializer.Fields.Entries.Where(entry => entry.Key == "scaled_spawn_chance").ToArray();
        if (standard.Length > 1 || scaled.Length > 1) throw Error("misc-spawn-chance", initializer, "Повторяется поле spawn chance.");
        int N(GalaxySyntaxEntry entry)
        {
            if (entry.Value.Kind != GalaxySyntaxKind.Scalar) throw Error("misc-spawn-chance", initializer, "Chance должна быть scalar.");
            var value = numeric.Evaluate(entry.Value, initializer.Definition.File.Path);
            if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue) throw Error("misc-spawn-chance", initializer, "Chance должна помещаться в int32.");
            return (int)value;
        }
        var spawn = standard.Length == 0 ? 100 : N(standard[0]);
        var scaledChance = scaled.Length == 0 ? 0 : N(scaled[0]);
        if (initializer.Definition.Id != "abandoned_gateways_01" && initializer.Fields.Entries.Any(entry => entry.Key == "scaled_spawn_settings"))
            throw Error("misc-scaled-setting", initializer, "scaled_spawn_settings не поддержан активным профилем.");
        if (initializer.Definition.Id == "abandoned_gateways_01")
        {
            var factor = Raw(request.Settings.SpecialContent.Gateways, initializer.Definition.Location);
            var limit = GalaxyInitializerExecution.Optional(initializer.Fields, "max_instances");
            if (scaled.Length != 1) throw Error("gateway-source", initializer, "Ожидался один scaled_spawn_chance.");
            var sourceMax = numeric.Evaluate(scaled[0].Value, initializer.Definition.File.Path);
            if (limit?.Operator != "=" || limit.Value.Kind != GalaxySyntaxKind.Scalar || numeric.Evaluate(limit.Value, initializer.Definition.File.Path) != 15 ||
                scaledChance != 5 || scaled[0].Operator != "=" || sourceMax != 5 ||
                standard.Length != 0 && standard[0].Operator != "=")
                throw Error("gateway-source", initializer, "Ожидались source max_instances=15 и scaled_spawn_chance=5.");
            return NativeGenerationRules.EvaluateSpawnChance(spawn, scaledChance, request.Plan.TargetStars, factor);
        }
        return NativeGenerationRules.EvaluateSpawnChance(spawn, scaledChance, request.Plan.TargetStars, FixedScale);
    }

    private static long Raw(decimal value, GalaxySourceLocation location)
    {
        var scaled = checked(value * FixedScale);
        if (scaled != decimal.Truncate(scaled)) throw new GalaxyStructureException("misc-weight-precision", "Weight превышает точность fixed5.", location);
        return checked((long)scaled);
    }
    private static long Multiply(long left, long right, GalaxySourceLocation location)
    {
        try { return checked((long)(((Int128)left * right) / FixedScale)); }
        catch (OverflowException error) { throw new GalaxyStructureException("misc-weight-overflow", error.Message, location); }
    }
    private static GalaxyStructureException Error(string code, GalaxyCompiledInitializer initializer, string message) =>
        new(code, initializer.Definition.Id + ": " + message, initializer.Definition.Location);
}
