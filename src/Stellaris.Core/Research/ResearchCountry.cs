using System.Collections.Frozen;

namespace Stellaris.Core.Research;

public sealed record ResearchAreaState(string? ActiveTechnologyId, IReadOnlyDictionary<string, decimal> Progress,
    IReadOnlyList<string> Choices, long Revision, string? LastCompletedTechnologyId = null);
public sealed record ResearchMonthResult(ResearchCountry Country, IReadOnlyDictionary<string, decimal> Reserves, IReadOnlyList<string> Completed);

/// <summary>Неизменяемая страна. Новый кандидат публикуется только после успешной общей месячной транзакции.</summary>
public sealed class ResearchCountry
{
    public static IReadOnlyList<string> AreaIds { get; } = Array.AsReadOnly(new[] { "physics", "society", "engineering" });
    public TechnologyCatalog Catalog { get; }
    public IReadOnlyDictionary<string, ResearchAreaState> Areas { get; }
    public IReadOnlySet<string> Researched { get; }
    public ulong Seed { get; }
    public long Revision { get; }
    public ResearchFacts Facts { get; }
    public IReadOnlyDictionary<string, int> Levels { get; }
    private readonly IReadOnlySet<string>? _allowed;
    private readonly IResearchWeightProvider? _adaptive;
    private ResearchCountry(TechnologyCatalog catalog, IReadOnlyDictionary<string, ResearchAreaState> areas, IEnumerable<string> researched,
        ulong seed, long revision, ResearchFacts facts, IReadOnlySet<string>? allowed, IResearchWeightProvider? adaptive, IReadOnlyDictionary<string, int>? levels = null)
    {
        Catalog = catalog; Areas = areas.ToFrozenDictionary(StringComparer.Ordinal); Researched = researched.ToFrozenSet(StringComparer.Ordinal);
        Seed = seed; Revision = revision; Facts = facts; _allowed = allowed?.ToFrozenSet(StringComparer.Ordinal); _adaptive = adaptive;
        Levels = (levels ?? Researched.ToDictionary(id => id, _ => 1)).ToFrozenDictionary(StringComparer.Ordinal);
    }
    public static ResearchCountry Create(TechnologyCatalog catalog, ulong seed, IEnumerable<string> startingTechnologies,
        ResearchFacts facts, IReadOnlySet<string>? allowedIds = null, IResearchWeightProvider? adaptive = null)
    {
        var starting = startingTechnologies.ToFrozenSet(StringComparer.Ordinal);
        if (starting.Any(id => !catalog.Technologies.ContainsKey(id))) { throw new ArgumentException("Неизвестная стартовая технология."); }
        var country = new ResearchCountry(catalog, AreaIds.ToDictionary(area => area, _ => new ResearchAreaState(null,
            new Dictionary<string, decimal>().ToFrozenDictionary(), Array.AsReadOnly(Array.Empty<string>()), 0)), starting, seed, 0, facts, allowedIds, adaptive);
        var areas = country.Areas.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var area in AreaIds) { areas[area] = areas[area] with { Choices = country.Draw(area, [], ref seed) }; }
        return new(catalog, areas, starting, seed, 0, facts, allowedIds, adaptive);
    }
    public ResearchCountry Fork() => this;
    public ResearchCountry WithFacts(ResearchFacts facts, bool fillEmptyChoices = true)
    {
        var candidate = new ResearchCountry(Catalog, Areas, Researched, Seed, Revision, facts, _allowed, _adaptive, Levels);
        if (!fillEmptyChoices) { return candidate; }
        var areas = Areas.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var seed = Seed; var changed = false;
        foreach (var area in AreaIds.Where(area => areas[area].Choices.Count == 0 && areas[area].ActiveTechnologyId is null))
        {
            var choices = candidate.Draw(area, [], ref seed);
            if (choices.Count != 0) { areas[area] = areas[area] with { Choices = choices, Revision = checked(Revision + 1) }; changed = true; }
        }
        return new(Catalog, areas, Researched, seed, changed ? checked(Revision + 1) : Revision, facts, _allowed, _adaptive, Levels);
    }
    public decimal? CurrentCost(string technologyId)
    {
        if (!Catalog.Technologies.TryGetValue(technologyId, out var technology)) { return null; }
        var cost = technology.Cost ?? ResearchRules.Weight(technology.CostRules, 1m, Facts, Researched, Catalog, technology.Area, Levels);
        if (technology.Cost is null && technology.CostRules is null || cost is null || cost < 0) { return null; }
        return checked(cost.Value + technology.CostPerLevel * Levels.GetValueOrDefault(technologyId));
    }
    public IReadOnlyList<string> MissingFacts(string technologyId)
    {
        var technology = Catalog.Technologies[technologyId];
        return Array.AsReadOnly(new[] { technology.Potential, technology.WeightRules, technology.CostRules }.Concat(technology.SwapTriggers)
            .SelectMany(block => ResearchRules.MissingFacts(block, Facts, Researched)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
    public ResearchCountry Select(string area, string technologyId, long expectedRevision)
    {
        if (expectedRevision != Revision) { throw new InvalidOperationException("STALE_REVISION"); }
        if (!Areas.TryGetValue(area, out var current) || !current.Choices.Contains(technologyId)
            || !Catalog.Technologies.TryGetValue(technologyId, out var definition) || definition.Area != area)
        { throw new ArgumentException("INVALID_RESEARCH_SELECTION"); }
        if (!Eligibility(technologyId).Available) { throw new InvalidOperationException("RESEARCH_UNAVAILABLE"); }
        var nextRevision = checked(Revision + 1);
        var areas = Areas.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        areas[area] = current with { ActiveTechnologyId = technologyId, Revision = nextRevision, LastCompletedTechnologyId = null };
        return new(Catalog, areas, Researched, Seed, nextRevision, Facts, _allowed, _adaptive, Levels);
    }
    public ResearchEligibility Eligibility(string technologyId)
    {
        if (!Catalog.Technologies.TryGetValue(technologyId, out var technology)) { return new(technologyId, false, null, ["unknown-technology"]); }
        var gates = new List<string>(technology.Gates.Where(gate => gate is not "ambiguous-weight-modifier" and not "ambiguous-field:weight"));
        if (CurrentCost(technologyId) is null && !gates.Contains("conditional-or-missing-cost")) { gates.Add("conditional-or-missing-cost"); }
        if (_allowed is not null && !_allowed.Contains(technologyId)) { gates.Add("outside-pool"); }
        if (technology.MaximumLevels != -1 && Levels.GetValueOrDefault(technologyId) >= technology.MaximumLevels) { gates.Add("already-researched"); }
        foreach (var prerequisite in technology.Prerequisites.Where(id => !Researched.Contains(id))) { gates.Add("prerequisite:" + prerequisite); }
        var potential = ResearchRules.Condition(technology.Potential, Facts, Researched);
        if (potential != true) { gates.Add(potential is null ? "potential-unknown" : "potential-false"); }
        foreach (var trigger in technology.SwapTriggers)
        {
            var swap = ResearchRules.Condition(trigger, Facts, Researched);
            if (swap != false) { gates.Add(swap is null ? "swap-unknown" : "swap-unsupported"); }
        }
        if (!Catalog.Tiers.TryGetValue(technology.Tier, out var tier)) { gates.Add("tier-unknown"); }
        else if (technology.Tier > 0 && Researched.Count(id => Catalog.Technologies.TryGetValue(id, out var known)
            && known.Area == technology.Area && known.Tier == technology.Tier - 1) < tier.PreviouslyUnlocked) { gates.Add("tier-locked"); }
        return new(technologyId, gates.Count == 0, null, Array.AsReadOnly(gates.ToArray()));
    }
    public ResearchEligibility Assess(string technologyId, IReadOnlySet<string>? previousChoices = null)
    {
        if (!Catalog.Technologies.TryGetValue(technologyId, out var technology)) { return new(technologyId, false, null, ["unknown-technology"]); }
        var gates = new List<string>(Eligibility(technologyId).Gates);
        gates.AddRange(technology.Gates.Where(gate => gate is "ambiguous-weight-modifier" or "ambiguous-field:weight"));
        Catalog.Tiers.TryGetValue(technology.Tier, out var tier);
        var weight = ResearchRules.Weight(technology.WeightRules, technology.Weight, Facts, Researched, Catalog, technology.Area, Levels);
        var tierWeight = tier is null ? null : ResearchRules.Weight(tier.WeightRules, 1m, Facts, Researched, Catalog, technology.Area, Levels);
        if (weight is null || tierWeight is null) { gates.Add("weight-unknown"); }
        else
        {
            weight = checked(weight.Value * tierWeight.Value);
            if (previousChoices?.Contains(technologyId) == true) { weight = checked(weight.Value * Catalog.PreviousOptionMultiplier); }
            if (_adaptive is not null)
            {
                var factor = _adaptive.Multiplier(technology, Facts, Researched);
                if (factor < 0) { throw new InvalidOperationException("Отрицательный адаптивный вес."); }
                weight = checked(weight.Value * factor);
            }
            if (weight <= 0) { gates.Add("zero-weight"); }
        }
        return new(technologyId, gates.Count == 0, weight, Array.AsReadOnly(gates.ToArray()));
    }
    public ResearchMonthResult AdvanceMonth(IReadOnlyDictionary<string, decimal> production, IReadOnlyDictionary<string, decimal> oldReserve, decimal leaderMultiplier)
        => AdvanceMonth(production, oldReserve, AreaIds.ToDictionary(area => area, _ => leaderMultiplier, StringComparer.Ordinal));

    public ResearchMonthResult AdvanceMonth(IReadOnlyDictionary<string, decimal> production, IReadOnlyDictionary<string, decimal> oldReserve,
        IReadOnlyDictionary<string, decimal> areaMultipliers)
    {
        if (AreaIds.Any(area => !areaMultipliers.ContainsKey(area)) || areaMultipliers.Any(pair => !AreaIds.Contains(pair.Key) || pair.Value < 0)
            || AreaIds.Any(area => !production.ContainsKey(area) || !oldReserve.ContainsKey(area))
            || production.Keys.Concat(oldReserve.Keys).Any(area => !AreaIds.Contains(area))
            || production.Values.Concat(oldReserve.Values).Any(value => value < 0)) { throw new ArgumentException("Недопустимые потоки науки."); }
        var revision = checked(Revision + 1);
        var areas = Areas.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var researched = Researched.ToHashSet(StringComparer.Ordinal);
        var reserves = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var completed = new List<string>();
        var levels = Levels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var area in AreaIds)
        {
            var state = areas[area]; var reserve = oldReserve[area];
            if (state.ActiveTechnologyId is not { } active) { reserves[area] = checked(reserve + production[area]); continue; }
            var output = checked(production[area] * areaMultipliers[area]);
            var cost = CurrentCost(active) ?? throw new InvalidOperationException("Не разрешена стоимость активной технологии.");
            var consumed = Math.Min(reserve, output); var gain = checked(output + consumed);
            var previous = state.Progress.GetValueOrDefault(active); var remaining = Math.Max(0, cost - previous);
            var applied = Math.Min(gain, remaining);
            var progress = state.Progress.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            progress[active] = checked(previous + applied);
            reserves[area] = checked(reserve - consumed);
            var done = progress[active] >= cost;
            if (done)
            {
                researched.Add(active); completed.Add(active); levels[active] = checked(levels.GetValueOrDefault(active) + 1);
                if (Catalog.Technologies[active].MaximumLevels != 1) { progress[active] = 0; }
            }
            areas[area] = state with { ActiveTechnologyId = done ? null : active, Progress = progress.ToFrozenDictionary(StringComparer.Ordinal), Revision = revision,
                LastCompletedTechnologyId = done ? active : state.LastCompletedTechnologyId };
        }
        var seed = Seed;
        var candidate = new ResearchCountry(Catalog, areas, researched, seed, revision, Facts, _allowed, _adaptive, levels);
        foreach (var area in AreaIds.Where(area => completed.Any(id => Catalog.Technologies[id].Area == area)))
        { areas[area] = areas[area] with { Choices = candidate.Draw(area, Areas[area].Choices, ref seed) }; }
        if (completed.Count != 0)
        {
            foreach (var area in AreaIds.Where(area => areas[area].Choices.Count == 0 && areas[area].ActiveTechnologyId is null))
            { areas[area] = areas[area] with { Choices = candidate.Draw(area, [], ref seed), Revision = revision }; }
        }
        return new(new(Catalog, areas, researched, seed, revision, Facts, _allowed, _adaptive, levels), reserves.ToFrozenDictionary(StringComparer.Ordinal), Array.AsReadOnly(completed.ToArray()));
    }
    private IReadOnlyList<string> Draw(string area, IReadOnlyList<string> previous, ref ulong seed)
    {
        var old = previous.ToFrozenSet(StringComparer.Ordinal);
        var pool = Catalog.Technologies.Values.Where(item => item.Area == area).OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => Assess(item.Id, old)).Where(item => item.Available).ToList();
        var selected = new List<string>();
        while (pool.Count != 0 && selected.Count < Catalog.Alternatives)
        {
            var sum = pool.Aggregate(0m, (total, item) => checked(total + item.Weight!.Value));
            seed = unchecked(seed + 0x9E3779B97F4A7C15UL);
            var random = seed; random = unchecked((random ^ (random >> 30)) * 0xBF58476D1CE4E5B9UL);
            random = unchecked((random ^ (random >> 27)) * 0x94D049BB133111EBUL); random ^= random >> 31;
            var target = (decimal)(random >> 11) / 9007199254740992m * sum;
            var index = 0;
            while (index < pool.Count - 1 && target >= pool[index].Weight!.Value) { target -= pool[index].Weight!.Value; index++; }
            var picked = pool[index].TechnologyId;
            selected.Add(picked); pool.RemoveAt(index);
            foreach (var group in Catalog.Technologies[picked].WeightGroups)
            {
                for (var remaining = pool.Count - 1; remaining >= 0; remaining--)
                {
                    var item = pool[remaining];
                    var multiplier = Catalog.Technologies[item.TechnologyId].GroupWeightMultipliers.GetValueOrDefault(group, 1m);
                    if (multiplier < 0) { throw new InvalidDataException("Отрицательный source group weight."); }
                    var weight = checked(item.Weight!.Value * multiplier);
                    if (weight == 0) { pool.RemoveAt(remaining); }
                    else { pool[remaining] = item with { Weight = weight }; }
                }
            }
        }
        return Array.AsReadOnly(selected.ToArray());
    }
}
