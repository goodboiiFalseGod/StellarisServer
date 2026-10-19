using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Globalization;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;

namespace Stellaris.Core.Construction;

public enum ConstructionTruth { Unknown, False, True }
public enum ConstructionOwnerPredicate { UsesFood, Ai, Wilderness, Regular, SpiritualistFallenEmpire, Gestalt, Nomadic, Tankbound, Hive, UsesConsumerGoods }
public enum ConstructionPlanetPredicate { HabitatEquivalent, EcuEquivalent, RingWorldEquivalent, SpecialColony, Capital, UpgradedCapital }

public sealed class ConstructionSourceFacts
{
    public string? DistrictSet { get; }
    public string? OwnerCountryType { get; }
    /// <summary>Selected membership, not evidence of current civic applicability.</summary>
    public IReadOnlySet<string>? Civics { get; }
    /// <summary>Explicit trusted applicability facts; null means validation is unknown.</summary>
    public IReadOnlySet<string>? ValidCivics { get; }
    /// <summary>Complete trusted owner flag snapshot; null means unavailable, not empty.</summary>
    public IReadOnlySet<string>? OwnerFlags { get; }
    /// <summary>Полный доверенный набор этик страны; null означает неизвестный контекст.</summary>
    public IReadOnlySet<string>? OwnerEthics { get; }
    /// <summary>Доверенное наличие федерации владельца; null означает неизвестный контекст.</summary>
    public bool? OwnerFederationExists { get; }
    public string? OwnerOrigin { get; }
    /// <summary>Известное членство владельца в галактическом сообществе; null означает неизвестный контекст.</summary>
    public bool? OwnerGalacticCommunityMember { get; }
    public IReadOnlySet<string>? Traditions { get; }
    public IReadOnlySet<string>? MenacePerks { get; }
    /// <summary>Полный доверенный набор ascension perks владельца; null означает неизвестный контекст.</summary>
    public IReadOnlySet<string>? OwnerAscensionPerks { get; }
    public IReadOnlySet<string>? Deposits { get; }
    /// <summary>Полный snapshot полученных традиций владельца; null означает неизвестный контекст.</summary>
    public IReadOnlySet<string>? OwnerHeldTraditions { get; }
    /// <summary>Полный snapshot определений присутствующих завершённых зон; null означает неизвестный контекст.</summary>
    public IReadOnlySet<string>? InstalledZones { get; }
    /// <summary>Известные результаты native has_authority; отсутствующий ключ остаётся неизвестным.</summary>
    public IReadOnlyDictionary<string, bool>? OwnerAuthorityPredicates { get; }
    public IReadOnlyDictionary<ConstructionPlanetPredicate, bool?> PlanetFacts { get; }
    public ConstructionShelterFacts? Shelter { get; }
    public decimal? SapientPopulation { get; }
    public IReadOnlyList<PopulationSpeciesSnapshot>? OwnerSpecies { get; }
    public EntityId? OwnerFounderSpeciesId { get; }

    public ConstructionSourceFacts(string? districtSet = null, string? ownerCountryType = null, IEnumerable<string>? civics = null, IEnumerable<string>? traditions = null, IEnumerable<string>? menacePerks = null, IEnumerable<string>? deposits = null, IReadOnlyDictionary<ConstructionPlanetPredicate, bool?>? planetFacts = null, IEnumerable<string>? validCivics = null, IEnumerable<string>? ownerFlags = null, ConstructionShelterFacts? shelter = null, decimal? sapientPopulation = null, IEnumerable<string>? ownerEthics = null, bool? ownerFederationExists = null, string? ownerOrigin = null, IEnumerable<string>? ownerAscensionPerks = null, IEnumerable<string>? installedZones = null, IReadOnlyDictionary<string, bool>? ownerAuthorityPredicates = null, IEnumerable<string>? ownerHeldTraditions = null, bool? ownerGalacticCommunityMember = null, IReadOnlyList<PopulationSpeciesSnapshot>? ownerSpecies = null, EntityId? ownerFounderSpeciesId = null, EntityId? ownerCountryEntityId = null)
    {
        if (planetFacts is not null && (planetFacts.Count > Enum.GetValues<ConstructionPlanetPredicate>().Length || planetFacts.Keys.Any(key => !Enum.IsDefined(key)))) { throw new ArgumentException("Invalid planet facts."); }
        DistrictSet = districtSet is null ? null : ConstructionValues.Id(districtSet);
        OwnerCountryType = ownerCountryType is null ? null : ConstructionValues.Id(ownerCountryType);
        Civics = ConstructionContext.Set(civics);
        ValidCivics = ConstructionContext.Set(validCivics);
        OwnerFlags = ConstructionContext.Set(ownerFlags);
        OwnerEthics = ConstructionContext.Set(ownerEthics);
        OwnerFederationExists = ownerFederationExists;
        OwnerOrigin = ownerOrigin is null ? null : ConstructionValues.Id(ownerOrigin);
        OwnerGalacticCommunityMember = ownerGalacticCommunityMember;
        Shelter = shelter;
        if (sapientPopulation < 0) { throw new ArgumentException("Invalid sapient population."); }
        SapientPopulation = sapientPopulation;
        if (ownerSpecies is not null)
        {
            var copied = ownerSpecies.Take(257).ToArray();
            if (ownerCountryEntityId is null || copied.Length > 256 || copied.Any(species => species is null)
                || copied.Select(species => species.Id).Distinct().Count() != copied.Length
                || copied.Any(species => species.OwnerCountry != ownerCountryEntityId.Value)
                || ownerFounderSpeciesId is { } founder && !copied.Any(species => species.Id == founder))
                throw new ArgumentException("Invalid owner species snapshot.", nameof(ownerSpecies));
            OwnerSpecies = Array.AsReadOnly(copied);
        }
        OwnerFounderSpeciesId = ownerFounderSpeciesId;
        Traditions = ConstructionContext.Set(traditions);
        MenacePerks = ConstructionContext.Set(menacePerks);
        OwnerAscensionPerks = ConstructionContext.Set(ownerAscensionPerks);
        OwnerHeldTraditions = ConstructionContext.Set(ownerHeldTraditions);
        Deposits = ConstructionContext.Set(deposits);
        InstalledZones = ConstructionContext.Set(installedZones);
        if (ownerAuthorityPredicates?.Count > 256) { throw new ArgumentException("Authority predicate snapshot exceeds limit.", nameof(ownerAuthorityPredicates)); }
        OwnerAuthorityPredicates = ownerAuthorityPredicates?.ToFrozenDictionary(pair => ConstructionValues.Id(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        PlanetFacts = new ReadOnlyDictionary<ConstructionPlanetPredicate, bool?>((planetFacts ?? new Dictionary<ConstructionPlanetPredicate, bool?>()).ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}

public sealed class ConstructionContext
{
    public bool? OwnerExists { get; }
    public IReadOnlyDictionary<ConstructionOwnerPredicate, bool?> OwnerFacts { get; }
    public bool? FromNomadic { get; }
    public string? PlanetClass { get; }
    public IReadOnlySet<string>? Modifiers { get; }
    public IReadOnlySet<string>? CarrierFlags { get; }
    public IReadOnlySet<string>? Technologies { get; }
    public IReadOnlyDictionary<string, decimal>? BuildingCounts { get; }
    public ConstructionSourceFacts? SourceFacts { get; }
    public int? CapitalTier { get; }
    public PlanetForeignOwnershipSnapshot? ForeignOwnership { get; }
    public ConstructionSystemGraph? SystemGraph { get; }
    /// <summary>Полный снимок завершённых районов; null означает неизвестную полноту.</summary>
    public IReadOnlyDictionary<string, decimal>? DistrictCounts { get; }

    public ConstructionContext(bool? ownerExists = null, IReadOnlyDictionary<ConstructionOwnerPredicate, bool?>? ownerFacts = null, bool? fromNomadic = null, string? planetClass = null, IEnumerable<string>? modifiers = null, IEnumerable<string>? carrierFlags = null, IEnumerable<string>? technologies = null, IReadOnlyDictionary<string, decimal>? buildingCounts = null, ConstructionSourceFacts? sourceFacts = null, int? capitalTier = null, PlanetForeignOwnershipSnapshot? foreignOwnership = null, EntityId? expectedPlanet = null, EntityId? expectedPlanetOwner = null, ConstructionSystemGraph? systemGraph = null, string? expectedSystemBodyId = null, IReadOnlyDictionary<string, decimal>? districtCounts = null)
    {
        if (ownerFacts is not null && (ownerFacts.Count > Enum.GetValues<ConstructionOwnerPredicate>().Length || ownerFacts.Keys.Any(key => !Enum.IsDefined(key)))) { throw new ArgumentException("Invalid owner facts."); }
        OwnerExists = ownerExists;
        OwnerFacts = new ReadOnlyDictionary<ConstructionOwnerPredicate, bool?>((ownerFacts ?? new Dictionary<ConstructionOwnerPredicate, bool?>()).ToDictionary(pair => pair.Key, pair => pair.Value));
        FromNomadic = fromNomadic;
        PlanetClass = planetClass is null ? null : ConstructionValues.Id(planetClass);
        Modifiers = Set(modifiers);
        CarrierFlags = Set(carrierFlags);
        Technologies = Set(technologies, 4096);
        BuildingCounts = buildingCounts is null ? null : ConstructionValues.Amounts(buildingCounts, 4096);
        SourceFacts = sourceFacts;
        if (capitalTier is < 0 or > 4096) throw new ArgumentException("Invalid capital tier.", nameof(capitalTier));
        CapitalTier = capitalTier;
        if (foreignOwnership is not null && (expectedPlanet is null || expectedPlanetOwner is null
            || foreignOwnership.Planet != expectedPlanet || foreignOwnership.PlanetOwner != expectedPlanetOwner || ownerExists != true))
            throw new ArgumentException("Foreign ownership snapshot does not match planet and owner.", nameof(foreignOwnership));
        ForeignOwnership = foreignOwnership;
        if (systemGraph is not null && systemGraph.RootBodyId != expectedSystemBodyId)
            throw new ArgumentException("System graph does not match expected body.", nameof(systemGraph));
        SystemGraph = systemGraph;
        DistrictCounts = districtCounts is null ? null : ConstructionValues.Amounts(districtCounts, 4096);
        if (DistrictCounts?.Values.Any(value => value != decimal.Truncate(value)) == true)
            throw new ArgumentException("District counts must be integral.", nameof(districtCounts));
    }

    public ConstructionContext WithSystemGraph(ConstructionSystemGraph graph, string expectedBodyId)
        => new(OwnerExists, OwnerFacts, FromNomadic, PlanetClass, Modifiers, CarrierFlags, Technologies,
            BuildingCounts, SourceFacts, CapitalTier, ForeignOwnership, ForeignOwnership?.Planet,
            ForeignOwnership?.PlanetOwner, graph, expectedBodyId, DistrictCounts);

    internal static IReadOnlySet<string>? Set(IEnumerable<string>? values, int maximum = 256)
    {
        if (values is null) { return null; }
        var copy = values.Take(maximum + 1).Select(ConstructionValues.Id).ToArray();
        if (copy.Length > maximum) { throw new ArgumentException("Construction context set exceeds limit."); }
        return copy.ToFrozenSet(StringComparer.Ordinal);
    }
}

public sealed record ConstructionCondition(ConstructionTruth Truth, IReadOnlyList<ConstructionGate> Gates)
{
    public IReadOnlyList<ConstructionGate> BlockingGates => Truth == ConstructionTruth.Unknown ? Gates : Array.Empty<ConstructionGate>();
}
public sealed record ConstructionAssessedCost(ConstructionCondition Condition, IReadOnlyList<ConstructionAmount> RawAmounts, decimal? RawMultiplier);
public sealed record ConstructionAssessedJobs(ConstructionCondition Condition, IReadOnlyList<ConstructionAmount> RawCapacity);
public sealed class ConstructionAssessment
{
    internal ConstructionProjection Source { get; }
    internal ConstructionContext Context { get; }
    public string DefinitionId => Source.Definition.Id;
    public ConstructionCondition Potential { get; }
    public ConstructionCondition Destroy { get; }
    public ConstructionCondition Prerequisites { get; }
    public IReadOnlyList<ConstructionAssessedCost> Costs { get; }
    public IReadOnlyList<ConstructionAssessedJobs> Jobs { get; }
    public IReadOnlyList<ConstructionGate> Gates { get; }

    internal ConstructionAssessment(ConstructionProjection source, ConstructionContext context, ConstructionCondition potential, ConstructionCondition destroy, ConstructionCondition prerequisites, IReadOnlyList<ConstructionAssessedCost> costs, IReadOnlyList<ConstructionAssessedJobs> jobs, IReadOnlyList<ConstructionGate> gates)
    {
        Source = source;
        Context = context;
        Potential = potential;
        Destroy = destroy;
        Prerequisites = prerequisites;
        Costs = costs;
        Jobs = jobs;
        Gates = gates;
    }
}

public static class ConstructionAssessor
{
    public static ConstructionAssessment Assess(ConstructionProjection projection, ConstructionContext context)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(context);
        var definition = projection.Definition;
        if (definition.Id != "building_hydroponics_farm") { throw new ArgumentException("Unsupported construction assessment profile."); }
        var evaluator = new Evaluator(context);
        var potential = evaluator.Required(definition.Fields, "potential");
        var destroy = evaluator.Required(definition.Fields, "destroy_trigger");
        var prerequisites = context.Technologies is null ? Unknown("technology-context") : Known(definition.Prerequisites.All(context.Technologies.Contains));
        var costs = definition.Costs.Select(cost => new ConstructionAssessedCost(cost.Trigger is null ? Unknown("cost-condition") : evaluator.Condition(cost.Trigger.Children, Scope.Planet), cost.Amounts, cost.RawMultiplier)).ToArray();
        var jobs = new List<ConstructionAssessedJobs>();
        foreach (var block in definition.Fields.Where(field => field.Key == "triggered_planet_modifier"))
        {
            var condition = evaluator.Required(block.Children, "potential");
            var modifiers = block.Children.Where(field => field.Key == "modifier").ToArray();
            var amounts = new List<ConstructionAmount>();
            var invalid = block.Operator != "=" || block.Scalar is not null || modifiers.Length != 1 || modifiers[0].Scalar is not null || modifiers[0].Operator != "=";
            if (!invalid)
            {
                foreach (var field in modifiers[0].Children)
                {
                    var id = field.Key switch { "job_farmer_add" => "farmer", "job_agri_drone_add" => "agri_drone", "job_fe_acolyte_farm_add" => "fe_acolyte_farm", _ => null };
                    if (id is null || amounts.Any(amount => amount.Resource == id) || field.Operator != "=" || !decimal.TryParse(field.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0) { invalid = true; continue; }
                    amounts.Add(new ConstructionAmount(id, value, field.Origin));
                }
            }
            if (invalid || block.Children.Any(field => field.Key is not ("potential" or "modifier")))
            {
                var unsupported = Unknown("job-capacity-operator");
                condition = block.Operator == "=" && block.Scalar is null && condition.Truth == ConstructionTruth.False ? condition with { Gates = Array.AsReadOnly(condition.Gates.Concat(unsupported.Gates).Distinct().ToArray()) } : unsupported;
            }
            jobs.Add(new ConstructionAssessedJobs(condition, Array.AsReadOnly(amounts.ToArray())));
        }
        var gates = new List<ConstructionGate>
        {
            new("dependency-unresolved", "construction-profile-closure"),
            new("dependency-unresolved", "colony-layout-context"),
            new("dependency-unresolved", "construction-price-time-policy"),
            new("dependency-unresolved", "construction-queue-policy"),
            new("dependency-unresolved", "construction-completion-phase"),
            new("dependency-unresolved", "construction-job-definition-closure")
        };
        gates.AddRange(potential.BlockingGates.Concat(destroy.BlockingGates).Concat(prerequisites.BlockingGates));
        gates.AddRange(costs.SelectMany(cost => cost.Condition.BlockingGates));
        gates.AddRange(jobs.SelectMany(job => job.Condition.BlockingGates));
        return new(projection, context, potential, destroy, prerequisites, Array.AsReadOnly(costs), Array.AsReadOnly(jobs.ToArray()), Array.AsReadOnly(gates.Distinct().Take(256).ToArray()));
    }

    internal enum Scope { Planet, Owner, From, Government, Ethics, Authority, FounderSpecies, System, CapitalPlanet, CapitalSystem, GrowingTarget, GrowingSpecies, OwnerSpecies }
    internal static ConstructionCondition Known(bool value) => new(value ? ConstructionTruth.True : ConstructionTruth.False, Array.Empty<ConstructionGate>());
    internal static ConstructionCondition Unknown(string dependency) => new(ConstructionTruth.Unknown, Array.AsReadOnly(new[] { new ConstructionGate("dependency-unresolved", dependency) }));

    internal sealed class Evaluator
    {
        private ConstructionContext _context;
        private readonly ConstructionSystemGraph? _systemGraph;
        private string? _systemBodyId;
        private readonly ConstructionCatalogProjection? _conditionSource;
        private readonly ConstructionOrdinaryTemplate? _governmentTemplate;
        private readonly bool _shelterClosure;
        private readonly List<Scope> _scopeFrames = new();
        private string? _zoneArgument;
        private PopulationSpeciesSnapshot? _speciesArgument;
        private readonly HashSet<string> _scriptedCalls = new(StringComparer.Ordinal);
        private int _remaining = 50000;

        public Evaluator(ConstructionContext context, ConstructionCatalogProjection? conditionSource = null, ConstructionOrdinaryTemplate? governmentTemplate = null)
        {
            if (conditionSource is not null)
            {
                _shelterClosure = conditionSource.ProfileId == ConstructionShelterClosureInventory.PROFILE;
                if (_shelterClosure) { ConstructionShelterClosureInventory.Validate(conditionSource); }
                else
                {
                    var authorityConditions = conditionSource.ProfileId == ConstructionAuthorityConditionInventory.Profile;
                    if (authorityConditions && (ConstructionAuthorityConditionInventory.Helpers.Any(id => !conditionSource.Definitions.TryGetValue(id, out var helper) || helper.Kind != "ownerCondition" || helper.Origin.Source != ConstructionAuthorityConditionInventory.Source)
                        || ConstructionAuthorityConditionInventory.TraditionHelpers.Any(group => group.Helpers.Any(id => !conditionSource.Definitions.TryGetValue(id, out var helper) || helper.Kind != "ownerCondition" || helper.Origin.Source != group.Source)))) { throw new ArgumentException("Invalid construction authority condition catalog.", nameof(conditionSource)); }
                    var civicConditions = authorityConditions || conditionSource.ProfileId == ConstructionOwnerCivicConditionInventory.Profile;
                    if (civicConditions && ConstructionOwnerCivicConditionInventory.Create().Any(group => group.Ids.Any(id => !conditionSource.Definitions.TryGetValue(id, out var helper) || helper.Kind != group.Kind || helper.Origin.Source != group.Path))) { throw new ArgumentException("Invalid construction civic condition catalog.", nameof(conditionSource)); }
                    var zoneConditions = civicConditions || conditionSource.ProfileId == ConstructionZoneConditionInventory.Profile;
                    if (zoneConditions && ConstructionZoneConditionInventory.Helpers.Append("has_zone").Any(id => !conditionSource.Definitions.TryGetValue(id, out var helper) || helper.Kind != "planetCondition")) { throw new ArgumentException("Invalid construction zone condition catalog.", nameof(conditionSource)); }
                    var depositConditions = zoneConditions || conditionSource.ProfileId == "construction-conditions-deposits-catalog-452-v1";
                    if (depositConditions && new[] { "has_volatile_motes_deposit", "has_exotic_gases_deposit", "has_rare_crystals_deposit" }.Any(id => !conditionSource.Definitions.TryGetValue(id, out var helper) || helper.Kind != "planetCondition")) { throw new ArgumentException("Invalid construction deposit condition catalog.", nameof(conditionSource)); }
                    var federationConditions = depositConditions || conditionSource.ProfileId == "construction-conditions-federation-catalog-452-v1";
                    if (federationConditions && (!conditionSource.Definitions.TryGetValue("has_make_spiritualist_perk", out var federationHelper) || federationHelper.Kind != "ownerCondition")) { throw new ArgumentException("Invalid construction federation condition catalog.", nameof(conditionSource)); }
                    var cityConditions = federationConditions || conditionSource.ProfileId == "construction-conditions-city-catalog-452-v1";
                    var ruralConditions = cityConditions || conditionSource.ProfileId == "construction-conditions-rural-catalog-452-v1";
                    var rareConditions = ruralConditions || conditionSource.ProfileId == "construction-conditions-rare-catalog-452-v1";
                    var capitalConditions = rareConditions || conditionSource.ProfileId == "construction-conditions-capital-catalog-452-v1";
                    var startupConditions = capitalConditions || conditionSource.ProfileId == "construction-conditions-startup-catalog-452-v1";
                    var ownerConditions = startupConditions || conditionSource.ProfileId == "construction-conditions-owner-catalog-452-v1";
                    if (cityConditions && new[] { "is_spiritualist", "is_fanatic_spiritualist" }.Any(id => !conditionSource.Definitions.TryGetValue(id, out var helper) || helper.Kind != "ownerCondition") || ruralConditions && (!conditionSource.Definitions.TryGetValue("is_anglers_empire", out var anglers) || anglers.Kind != "ownerCondition") || (!ownerConditions && conditionSource.ProfileId != "construction-conditions-catalog-452-v1") || conditionSource.Definitions.Count != (authorityConditions ? 17 + ConstructionZoneConditionInventory.Helpers.Count + 1 + ConstructionOwnerCivicConditionInventory.Helpers.Count + ConstructionAuthorityConditionInventory.Helpers.Count + ConstructionAuthorityConditionInventory.TraditionHelpers.Sum(group => group.Helpers.Length) : civicConditions ? 17 + ConstructionZoneConditionInventory.Helpers.Count + 1 + ConstructionOwnerCivicConditionInventory.Helpers.Count : zoneConditions ? 17 + ConstructionZoneConditionInventory.Helpers.Count + 1 : depositConditions ? 17 : federationConditions ? 14 : cityConditions ? 13 : ruralConditions ? 11 : rareConditions ? 10 : capitalConditions ? 8 : startupConditions ? 7 : ownerConditions ? 6 : 4) || new[] { "planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent", "is_special_colony_type" }.Any(id => !conditionSource.Definitions.TryGetValue(id, out var definition) || definition.Kind != "planetCondition") || ownerConditions && new[] { "has_functional_civic", "has_tech_or_functional_civic" }.Any(id => !conditionSource.Definitions.TryGetValue(id, out var definition) || definition.Kind != "ownerCondition") || startupConditions && (!conditionSource.Definitions.TryGetValue("is_fallen_empire", out var fallen) || fallen.Kind != "ownerCondition") || capitalConditions && (!conditionSource.Definitions.TryGetValue("uses_habitat_capitals", out var habitat) || habitat.Kind != "planetCondition")
                        || rareConditions && (!conditionSource.Definitions.TryGetValue("industrial_jobs_are_catalytic_trigger", out var industrial) || industrial.Kind != "planetCondition" || !conditionSource.Definitions.TryGetValue("is_catalytic_empire", out var catalytic) || catalytic.Kind != "ownerCondition")) { throw new ArgumentException("Invalid construction condition catalog.", nameof(conditionSource)); }
                }
            }
            _context = context;
            _systemGraph = context.SystemGraph;
            _conditionSource = conditionSource;
            _governmentTemplate = governmentTemplate;
        }

        public ConstructionCondition Required(IReadOnlyList<ConstructionField> fields, string key)
        {
            var selected = fields.Where(field => field.Key == key).ToArray();
            return selected.Length != 1 || selected[0].Scalar is not null || selected[0].Operator != "=" ? Unknown("required-condition-shape") : Condition(selected[0].Children, Scope.Planet);
        }

        internal decimal? Value(ConstructionField operand, List<ConstructionGate> gates)
        {
            if (!_shelterClosure || operand.Operator != "=" || operand.Children.Count != 0 || operand.Scalar != "value:storm_building_production_multiplier") { return Refuse("catalog-number-unresolved"); }
            const string ID = "storm_building_production_multiplier";
            var fields = _conditionSource!.Definitions[ID].Fields;
            if (fields.Count is 0 or > 256 || fields[0].Key != "base") { return Refuse("script-value-shape"); }
            var parameters = _conditionSource.Parameters.Where(item => item.DefinitionId == ID).ToArray();
            var value = Number(fields[0], "/0:base");
            if (value is null) { return Refuse("script-value-number"); }
            for (var index = 1; index < fields.Count; index++)
            {
                var field = fields[index];
                if (field.Key != "modifier" || field.Operator != "=" || field.Scalar is not null || field.Children.Count < 2 || field.Children[0].Key != "add" || field.Children.Count(child => child.Key == "add") != 1) { return Refuse("script-value-shape"); }
                var add = Number(field.Children[0], "/" + index.ToString(CultureInfo.InvariantCulture) + ":modifier/0:add");
                if (add is null) { return Refuse("script-value-number"); }
                var condition = Condition(field.Children.Skip(1).ToArray(), Scope.Planet);
                if (condition.Truth == ConstructionTruth.Unknown) { gates.AddRange(condition.BlockingGates); return Refuse("script-value-condition"); }
                if (condition.Truth == ConstructionTruth.False) { continue; }
                try { value = checked(value.Value + add.Value); }
                catch (OverflowException) { return Refuse("script-value-overflow"); }
            }
            return value;

            decimal? Number(ConstructionField field, string path)
            {
                var matches = parameters.Where(item => item.FieldPath == path).ToArray();
                if (field.Operator != "=" || field.Scalar is null || field.Children.Count != 0 || matches.Length != 1 || matches[0].Operator != "=" || matches[0].Origin != field.Origin) { return null; }
                if (decimal.TryParse(field.Scalar, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var literal)) { return matches[0].Value == literal ? literal : null; }
                return field.Scalar.Length > 1 && field.Scalar[0] == '@' && field.Scalar.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_') ? matches[0].Value : null;
            }

            decimal? Refuse(string dependency)
            {
                gates.AddRange(Unknown(dependency).Gates);
                return null;
            }
        }

        public ConstructionCondition Condition(IReadOnlyList<ConstructionField> fields, Scope scope, bool or = false, int depth = 0, string? technologyArgument = null)
        {
            var entered = _scopeFrames.Count == 0 || _scopeFrames[^1] != scope;
            if (entered) { _scopeFrames.Add(scope); }
            try
            {
                if (depth > 64 || fields.Count > _remaining) { return Unknown("condition-budget"); }
                _remaining -= fields.Count;
                var results = new List<ConstructionCondition>();
                for (var index = 0; index < fields.Count; index++)
                {
                    var field = fields[index];
                    if (field.Key != "if") { results.Add(ResolvedField(field, scope, depth + 1, technologyArgument)); continue; }
                    var branches = new List<ConstructionField> { field };
                    while (index + 1 < fields.Count && fields[index + 1].Key is "else_if" or "else") branches.Add(fields[++index]);
                    var valid = true; var ended = false;
                    foreach (var branch in branches)
                    {
                        if (ended || branch.Operator != "=" || branch.Scalar is not null
                            || branch.Children.Count(child => child.Key == "limit") != (branch.Key == "else" ? 0 : 1)
                            || branch.Children.Where(child => child.Key == "limit").Any(child => child.Operator != "=" || child.Scalar is not null)) valid = false;
                        ended = branch.Key == "else";
                    }
                    if (!valid) { results.Add(Unknown("conditional-chain-shape")); continue; }
                    var paths = new List<ConstructionCondition>();
                    var limits = new List<ConstructionCondition>();
                    var reachesNext = true;
                    foreach (var branch in branches)
                    {
                        var limit = branch.Key == "else" ? Known(true) : Condition(branch.Children.Single(child => child.Key == "limit").Children, scope, depth: depth + 1, technologyArgument: technologyArgument);
                        limits.Add(limit);
                        if (limit.Truth == ConstructionTruth.False) continue;
                        paths.Add(Condition(branch.Children.Where(child => child.Key != "limit").ToArray(), scope, depth: depth + 1, technologyArgument: technologyArgument));
                        if (limit.Truth == ConstructionTruth.True) { reachesNext = false; break; }
                    }
                    if (reachesNext) paths.Add(Known(true));
                    // Неизвестный limit не выбирает ветку. Итог известен только если
                    // все достижимые исходы совпадают; иначе зависимость сохраняется.
                    var outcomes = paths.Select(path => path.Truth).Distinct().ToArray();
                    results.Add(outcomes is [ConstructionTruth.True] ? Known(true) : outcomes is [ConstructionTruth.False] ? Known(false)
                        : new(ConstructionTruth.Unknown, Array.AsReadOnly(paths.Concat(limits).SelectMany(path => path.Gates).Distinct().Take(256).ToArray())));
                }
                var truth = or ? results.Any(result => result.Truth == ConstructionTruth.True) ? ConstructionTruth.True : results.Any(result => result.Truth == ConstructionTruth.Unknown) ? ConstructionTruth.Unknown : ConstructionTruth.False
                    : results.Any(result => result.Truth == ConstructionTruth.False) ? ConstructionTruth.False : results.Any(result => result.Truth == ConstructionTruth.Unknown) ? ConstructionTruth.Unknown : ConstructionTruth.True;
                return new(truth, Array.AsReadOnly(results.SelectMany(result => result.Gates).Distinct().Take(256).ToArray()));
            }
            finally
            {
                if (entered) { _scopeFrames.RemoveAt(_scopeFrames.Count - 1); }
            }
        }

        private ConstructionCondition ResolvedField(ConstructionField field, Scope scope, int depth, string? technologyArgument)
        {
            var result = Field(field, scope, depth, technologyArgument);
            if (result.Truth != ConstructionTruth.Unknown || scope is not (Scope.Planet or Scope.Owner or Scope.OwnerSpecies or Scope.System)
                || field.Operator != "=" || field.Children.Count != 0 || field.Scalar is not ("yes" or "no")
                || _conditionSource is null || _conditionSource.Definitions.ContainsKey(field.Key)
                || !_conditionSource.ScriptedConditions.TryGetValue(field.Key, out var helper)
                || !result.Gates.All(gate => gate.DependencyId is "planet-condition" or "owner-predicate-context" or "species-condition" or "system-condition")) return result;
            if (!_scriptedCalls.Add(field.Key)) return Unknown("scripted-condition-cycle");
            try
            {
                var value = Condition(helper.Fields, scope, depth: depth, technologyArgument: technologyArgument);
                return field.Scalar == "yes" ? value : value with { Truth = value.Truth switch
                { ConstructionTruth.True => ConstructionTruth.False, ConstructionTruth.False => ConstructionTruth.True, _ => ConstructionTruth.Unknown } };
            }
            finally { _scriptedCalls.Remove(field.Key); }
        }

        private ConstructionCondition Field(ConstructionField field, Scope scope, int depth, string? technologyArgument)
        {
            if (scope == Scope.Planet && field.Key == "capital_tier")
            {
                if (field.Operator != ">=" || field.Children.Count != 0 || !int.TryParse(field.Scalar, NumberStyles.None, CultureInfo.InvariantCulture, out var tier) || tier is < 0 or > 4096) { return Unknown("capital-tier-comparison-shape"); }
                return (_shelterClosure ? _context.SourceFacts?.Shelter?.CapitalTier : _context.CapitalTier) is { } actual ? Known(actual >= tier) : Unknown("capital-tier-context");
            }
            if (scope == Scope.Planet && field.Key == "sapient_pop_amount")
            {
                if (_context.SourceFacts?.SapientPopulation is not { } population) { return Unknown("sapient-population-context"); }
                if (field.Children.Count != 0 || !decimal.TryParse(field.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) { return Unknown("sapient-population-value"); }
                return field.Operator switch
                {
                    ">=" => Known(population >= amount), ">" => Known(population > amount),
                    "<=" => Known(population <= amount), "<" => Known(population < amount),
                    "=" => Known(population == amount), "!=" => Known(population != amount),
                    _ => Unknown("sapient-population-operator")
                };
            }
            if (field.Operator != "=") { return Unknown("condition-operator"); }
            if (scope is Scope.Government or Scope.Ethics or Scope.Authority)
            {
                if (field.Scalar is not null)
                {
                    if (field.Key != "value" || _governmentTemplate is null) { return Unknown("government-requirement-value"); }
                    return scope switch
                    {
                        Scope.Ethics => Known(_governmentTemplate.Ethics.Contains(field.Scalar)),
                        Scope.Authority => Known(_governmentTemplate.AuthorityId == field.Scalar),
                        _ => Unknown("government-requirement-selector")
                    };
                }
                if (scope == Scope.Government && field.Key is "ethics" or "authority") { return Condition(field.Children, field.Key == "ethics" ? Scope.Ethics : Scope.Authority, depth: depth); }
                if (field.Key is not ("AND" or "OR" or "NOT" or "NOR")) { return Unknown("government-requirement-shape"); }
            }
            if (field.Scalar is null)
            {
                if (field.Key == "AND") { return Condition(field.Children, scope, depth: depth, technologyArgument: technologyArgument); }
                if (field.Key == "OR") { return Condition(field.Children, scope, true, depth, technologyArgument); }
                if (field.Key == "NOR")
                {
                    var value = Condition(field.Children, scope, true, depth, technologyArgument);
                    return value with { Truth = value.Truth switch { ConstructionTruth.True => ConstructionTruth.False, ConstructionTruth.False => ConstructionTruth.True, _ => ConstructionTruth.Unknown } };
                }
                if (field.Key == "hidden_trigger") { return Condition(field.Children, scope, depth: depth, technologyArgument: technologyArgument); }
                if (field.Key == "custom_tooltip")
                {
                    var metadata = field.Children.Where(child => child.Key == "fail_text").ToArray();
                    if (metadata.Length > 1 || metadata.Any(child => child.Operator != "=" || child.Scalar is null)) { return Unknown("tooltip-metadata-shape"); }
                    return Condition(field.Children.Where(child => child.Key != "fail_text").ToArray(), scope, depth: depth, technologyArgument: technologyArgument);
                }
                if (field.Key == "NAND")
                {
                    var value = Condition(field.Children, scope, depth: depth, technologyArgument: technologyArgument);
                    return value with { Truth = value.Truth switch { ConstructionTruth.True => ConstructionTruth.False, ConstructionTruth.False => ConstructionTruth.True, _ => ConstructionTruth.Unknown } };
                }
                if (field.Key == "NOT" && field.Children.Count == 1)
                {
                    var value = Condition(field.Children, scope, depth: depth, technologyArgument: technologyArgument);
                    return value with { Truth = value.Truth switch { ConstructionTruth.True => ConstructionTruth.False, ConstructionTruth.False => ConstructionTruth.True, _ => ConstructionTruth.Unknown } };
                }
                if ((field.Key == "owner" || (_shelterClosure || _conditionSource?.ProfileId is "construction-conditions-rural-catalog-452-v1" or "construction-conditions-city-catalog-452-v1" or "construction-conditions-federation-catalog-452-v1" or "construction-conditions-deposits-catalog-452-v1" or ConstructionZoneConditionInventory.Profile or ConstructionOwnerCivicConditionInventory.Profile or ConstructionAuthorityConditionInventory.Profile) && field.Key == "owner?") && scope == Scope.Planet)
                {
                    return _context.OwnerExists == true ? Condition(field.Children, Scope.Owner, depth: depth, technologyArgument: technologyArgument) : Unknown("owner-scope");
                }
                if (_shelterClosure && field.Key == "prev" && scope == Scope.Owner)
                {
                    return _scopeFrames.Count > 1 && _scopeFrames[^2] == Scope.Planet ? Condition(field.Children, Scope.Planet, depth: depth) : Unknown("previous-colony-scope");
                }
                if (_shelterClosure && field.Key == "founder_species" && scope == Scope.Owner)
                {
                    return _context.SourceFacts?.Shelter?.FounderSpeciesExists == true ? Condition(field.Children, Scope.FounderSpecies, depth: depth) : Unknown("founder-species-scope");
                }
                if (_conditionSource?.ProfileId == ConstructionAuthorityConditionInventory.Profile && scope == Scope.Owner
                    && field.Key is "founder_species" or "any_owned_species")
                {
                    var species = _context.SourceFacts?.OwnerSpecies;
                    if (species is null) return Unknown("owner-species-context");
                    if (field.Key == "founder_species")
                    {
                        if (_context.SourceFacts?.OwnerFounderSpeciesId is not { } founder) return Unknown("founder-species-context");
                        species = species.Where(item => item.Id == founder).ToArray();
                        if (species.Count != 1) return Unknown("founder-species-context");
                    }
                    var previous = _speciesArgument;
                    try
                    {
                        var values = new List<ConstructionCondition>();
                        foreach (var item in species)
                        {
                            _speciesArgument = item;
                            values.Add(Condition(field.Children, Scope.OwnerSpecies, depth: depth));
                        }
                        return values.Any(value => value.Truth == ConstructionTruth.True) ? Known(true)
                            : values.Any(value => value.Truth == ConstructionTruth.Unknown)
                                ? new(ConstructionTruth.Unknown, Array.AsReadOnly(values.SelectMany(value => value.Gates).Distinct().Take(256).ToArray())) : Known(false);
                    }
                    finally { _speciesArgument = previous; }
                }
                if (_shelterClosure && scope == Scope.Planet && field.Key is "solar_system" or "solar_system?")
                {
                    return _context.SourceFacts?.Shelter?.SystemExists == true ? Condition(field.Children, Scope.System, depth: depth) : Unknown("originating-system-scope");
                }
                if (!_shelterClosure && scope == Scope.Planet && field.Key is "solar_system" or "solar_system?")
                    return _systemGraph is not null ? Condition(field.Children, Scope.System, depth: depth) : Unknown("system-graph-context");
                if (!_shelterClosure && scope == Scope.System && field.Key == "any_system_planet")
                    return SystemPlanets(field.Children, depth);
                if (_shelterClosure && scope == Scope.Owner && field.Key is "capital_scope" or "capital_scope.solar_system?")
                {
                    var links = _context.SourceFacts?.Shelter?.CapitalLinks;
                    if (_context.OwnerExists != true || links?.OwnerCapitalColonyId is null) { return Unknown("owner-capital-scope"); }
                    if (field.Key == "capital_scope") { return Condition(field.Children, Scope.CapitalPlanet, depth: depth); }
                    return links.OwnerCapitalSystemId is not null ? Condition(field.Children, Scope.CapitalSystem, depth: depth) : Unknown("owner-capital-system-scope");
                }
                if (_shelterClosure && scope == Scope.Planet && field.Key == "growing_species")
                {
                    var links = _context.SourceFacts?.Shelter?.GrowingSpecies;
                    return links?.TargetExists == true && links.TargetId is not null ? Condition(field.Children, Scope.GrowingTarget, depth: depth) : Unknown("growing-target-scope");
                }
                if (_shelterClosure && scope == Scope.GrowingTarget && field.Key == "species")
                {
                    return _context.SourceFacts?.Shelter?.GrowingSpecies?.SpeciesId is not null ? Condition(field.Children, Scope.GrowingSpecies, depth: depth) : Unknown("growing-species-link");
                }
                if ((_shelterClosure || _conditionSource?.ProfileId is ConstructionZoneConditionInventory.Profile or ConstructionOwnerCivicConditionInventory.Profile or ConstructionAuthorityConditionInventory.Profile) && field.Key == "has_zone" && scope == Scope.Planet)
                {
                    if (field.Children.Count != 1 || field.Children[0].Key != "zone" || field.Children[0].Operator != "=" || !Literal(field.Children[0]) || _conditionSource is null) { return Unknown("zone-argument-shape"); }
                    var zone = field.Children[0].Scalar!;
                    var body = _conditionSource.Definitions["has_zone"].Fields;
                    return ZoneBody(body, zone, depth);
                }
                if ((_shelterClosure || _conditionSource?.ProfileId is ConstructionZoneConditionInventory.Profile or ConstructionOwnerCivicConditionInventory.Profile or ConstructionAuthorityConditionInventory.Profile) && field.Key == "num_zones" && scope == Scope.Planet)
                {
                    var types = field.Children.Where(child => child.Key == "type").ToArray();
                    var values = field.Children.Where(child => child.Key == "value").ToArray();
                    if (field.Children.Count != 2 || types.Length != 1 || values.Length != 1 || types[0].Operator != "=" || types[0].Children.Count != 0 || types[0].Scalar != "$zone$" || _zoneArgument is null || values[0].Operator != ">" || values[0].Scalar != "0" || values[0].Children.Count != 0) { return Unknown("zone-count-shape"); }
                    return Contains(_shelterClosure ? _context.SourceFacts?.Shelter?.Zones : _context.SourceFacts?.InstalledZones, _zoneArgument, "installed-zones-context");
                }
                if (field.Key == "from?" && scope == Scope.Planet)
                {
                    return _context.FromNomadic is null ? Unknown("from-scope") : Condition(field.Children, Scope.From, depth: depth, technologyArgument: technologyArgument);
                }
                if (scope == Scope.Owner && field.Key == "has_tech_or_functional_civic" && _conditionSource?.Definitions.TryGetValue(field.Key, out var ownerScripted) == true)
                {
                    if (field.Children.Count != 1 || field.Children[0].Key != "TECH" || field.Children[0].Operator != "=" || field.Children[0].Scalar is not { } technology || technology.Length is 0 or > 128 || technology.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_') || field.Children[0].Children.Count != 0) { return Unknown("owner-scripted-argument-shape"); }
                    return Condition(ownerScripted.Fields, Scope.Owner, depth: depth, technologyArgument: technology);
                }
                if (field.Key == "num_buildings" && scope == Scope.Planet) { return BuildingCount(field); }
                if (field.Key == "has_holding" && scope == Scope.Planet) { return Holding(field); }
                return Unknown("condition-block");
            }
            if (_shelterClosure && field.Children.Count != 0) { return Unknown("scalar-condition-children"); }
            if (field.Key == "always" && field.Scalar is "yes" or "no") { return Known(field.Scalar == "yes"); }
            if (scope == Scope.OwnerSpecies)
            {
                if (_speciesArgument is null || !Literal(field)) return Unknown("species-condition-context");
                return field.Key switch
                {
                    "has_trait" => Known(_speciesArgument.Definition.Traits.Contains(field.Scalar!)),
                    "is_archetype" => Known(_speciesArgument.Definition.ArchetypeId == field.Scalar),
                    _ => Unknown("species-condition")
                };
            }
            if (_shelterClosure && scope is Scope.GrowingTarget or Scope.GrowingSpecies)
            {
                return scope == Scope.GrowingSpecies && field.Key == "has_trait" && Literal(field) ? Contains(_context.SourceFacts?.Shelter?.GrowingSpecies?.SpeciesTraits, field.Scalar!, "growing-species-traits") : Unknown("growing-scope-condition");
            }
            if (_shelterClosure && scope is Scope.CapitalPlanet or Scope.CapitalSystem)
            {
                if (field.Key != "is_same_value" || field.Scalar != (scope == Scope.CapitalPlanet ? "ROOT" : "ROOT.solar_system")) { return Unknown("capital-identity-condition"); }
                var links = _context.SourceFacts?.Shelter?.CapitalLinks;
                if (_context.OwnerExists != true || links?.OwnerCapitalColonyId is null) { return Unknown("owner-capital-scope"); }
                if (scope == Scope.CapitalPlanet) { return links.ColonyId is not null ? Known(links.OwnerCapitalColonyId == links.ColonyId) : Unknown("root-colony-identity"); }
                return _context.SourceFacts?.Shelter?.SystemExists == true && links.SystemId is not null && links.OwnerCapitalSystemId is not null ? Known(links.OwnerCapitalSystemId == links.SystemId) : Unknown("capital-system-identities");
            }
            if (_shelterClosure && scope == Scope.System)
            {
                return field.Key == "has_star_flag" && Literal(field) ? Contains(_context.SourceFacts?.Shelter?.SystemStarFlags, field.Scalar!, "system-star-flags-context") : Unknown("system-condition");
            }
            if (scope == Scope.System)
                return Unknown(field.Key == "has_star_flag" ? "system-star-flags-context" : "system-condition");
            if (_shelterClosure && scope == Scope.FounderSpecies)
            {
                return field.Key == "is_archetype" && Literal(field) ? _context.SourceFacts?.Shelter?.FounderSpeciesArchetype is { } archetype ? Known(archetype == field.Scalar) : Unknown("founder-archetype-context") : Unknown("founder-species-condition");
            }
            if (scope == Scope.Planet)
            {
                if (_conditionSource is not null && _conditionSource.Definitions.TryGetValue(field.Key, out var scripted) && scripted.Kind == "planetCondition")
                {
                    if (field.Scalar is not ("yes" or "no") || field.Children.Count != 0) { return Unknown("scripted-condition-value"); }
                    var value = Condition(scripted.Fields, Scope.Planet, depth: depth);
                    return field.Scalar == "yes" ? value : value with { Truth = value.Truth switch { ConstructionTruth.True => ConstructionTruth.False, ConstructionTruth.False => ConstructionTruth.True, _ => ConstructionTruth.Unknown } };
                }
                ConstructionPlanetPredicate? planetPredicate = field.Key switch
                {
                    "planet_is_habitat_equivalent" => ConstructionPlanetPredicate.HabitatEquivalent,
                    "planet_is_ecu_equivalent" => ConstructionPlanetPredicate.EcuEquivalent,
                    "planet_is_ring_world_equivalent" => ConstructionPlanetPredicate.RingWorldEquivalent,
                    "is_special_colony_type" => ConstructionPlanetPredicate.SpecialColony,
                    "is_capital" => ConstructionPlanetPredicate.Capital,
                    "has_upgraded_capital" => ConstructionPlanetPredicate.UpgradedCapital,
                    _ => null
                };
                if (planetPredicate is { } planetKey && field.Scalar is "yes" or "no") { return _context.SourceFacts is { } facts && facts.PlanetFacts.TryGetValue(planetKey, out var planetFact) ? Boolean(planetFact, field.Scalar == "yes", "planet-predicate-context") : Unknown("planet-predicate-context"); }
                return field.Key switch
                {
                    "is_active_resolution" when _shelterClosure && Literal(field) => Contains(_context.SourceFacts?.Shelter?.ActiveResolutions, field.Scalar, "active-resolutions-context"),
                    "exists" when field.Scalar == "owner" => Boolean(_context.OwnerExists, true, "owner-context"),
                    "has_branch_office" when field.Scalar is "yes" or "no" && Literal(field) => Boolean(
                        _context.ForeignOwnership is { } foreign ? foreign.BranchOfficeOwner is not null : null,
                        field.Scalar == "yes", "branch-office-context"),
                    "exists" when _shelterClosure && field.Scalar == "solar_system" => Boolean(_context.SourceFacts?.Shelter?.SystemExists, true, "originating-system-context"),
                    "exists" when field.Scalar == "solar_system" && Literal(field) => _systemGraph is not null ? Known(true) : Unknown("system-graph-context"),
                    "exists" when _shelterClosure && field.Scalar == "leader" => Boolean(_context.SourceFacts?.Shelter?.LeaderExists, true, "colony-leader-context"),
                    "exists" when _shelterClosure && field.Scalar == "growing_species" => Boolean(_context.SourceFacts?.Shelter?.GrowingSpecies?.TargetExists, true, "growing-target-context"),
                    "is_planet_class" => _context.PlanetClass is null ? Unknown("planet-class-context") : Known(_context.PlanetClass == field.Scalar),
                    "is_star" when field.Scalar is "yes" or "no" && Literal(field) => _systemGraph?.StarBodies is { } stars
                        ? Known(stars.Contains(_systemBodyId ?? _systemGraph.RootBodyId) == (field.Scalar == "yes")) : Unknown("system-body-kind-context"),
                    "has_modifier" => _context.Modifiers is null ? Unknown("planet-modifiers-context") : Known(_context.Modifiers.Contains(field.Scalar)),
                    "has_building" when Literal(field) => _context.BuildingCounts is not null && _context.BuildingCounts.TryGetValue(field.Scalar!, out var buildingCount) ? Known(buildingCount > 0) : Unknown("building-count-context"),
                    "has_district" when Literal(field) && field.Scalar!.StartsWith("district_", StringComparison.Ordinal)
                        && field.Scalar.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
                        => _context.DistrictCounts is { } districts ? Known(districts.TryGetValue(field.Scalar!, out var count) && count > 0) : Unknown("district-count-context"),
                    "has_carrier_flag" => _context.CarrierFlags is null ? Unknown("carrier-flags-context") : Known(_context.CarrierFlags.Contains(field.Scalar)),
                    "uses_district_set" => _context.SourceFacts?.DistrictSet is { } set ? Known(set == field.Scalar) : Unknown("district-set-context"),
                    "has_deposit" => Contains(_context.SourceFacts?.Deposits, field.Scalar, "deposits-context"),
                    "has_planet_flag" when _shelterClosure && Literal(field) => Contains(_context.SourceFacts?.Shelter?.PlanetFlags, field.Scalar, "planet-flags-context"),
                    "host_has_dlc" when _shelterClosure && Literal(field) => Contains(_context.SourceFacts?.Shelter?.HostDlc, field.Scalar, "host-dlc-context"),
                    "carrier_is_type" when _shelterClosure && Literal(field) => _context.SourceFacts?.Shelter?.CarrierType is { } carrier ? Known(carrier == field.Scalar) : Unknown("carrier-type-context"),
                    _ => Unknown("planet-condition")
                };
            }
            if (scope == Scope.Owner)
            {
                if ((field.Key is "has_functional_civic" or "is_fallen_empire" or "is_catalytic_empire" or "is_anglers_empire" or "is_spiritualist" or "is_fanatic_spiritualist" or "has_make_spiritualist_perk" || _shelterClosure || (_conditionSource?.ProfileId is ConstructionOwnerCivicConditionInventory.Profile or ConstructionAuthorityConditionInventory.Profile) && ConstructionOwnerCivicConditionInventory.Helpers.Contains(field.Key) || _conditionSource?.ProfileId == ConstructionAuthorityConditionInventory.Profile && (ConstructionAuthorityConditionInventory.Helpers.Contains(field.Key) || ConstructionAuthorityConditionInventory.TraditionHelpers.Any(group => group.Helpers.Contains(field.Key)))) && _conditionSource?.Definitions.TryGetValue(field.Key, out var functional) == true && functional.Kind == "ownerCondition")
                {
                    if (field.Scalar is not ("yes" or "no") || field.Children.Count != 0) { return Unknown("owner-scripted-condition-value"); }
                    var value = Condition(functional.Fields, Scope.Owner, depth: depth, technologyArgument: technologyArgument);
                    return field.Scalar == "yes" ? value : value with { Truth = value.Truth switch { ConstructionTruth.True => ConstructionTruth.False, ConstructionTruth.False => ConstructionTruth.True, _ => ConstructionTruth.Unknown } };
                }
                switch (field.Key)
                {
                    case "exists" when field.Scalar == "federation" && Literal(field): return Boolean(_context.SourceFacts?.OwnerFederationExists, true, "owner-federation-context");
                    case "is_galactic_community_member" when (field.Scalar is "yes" or "no") && Literal(field): return Boolean(_shelterClosure ? _context.SourceFacts?.Shelter?.OwnerGalacticCommunityMember : _context.SourceFacts?.OwnerGalacticCommunityMember, field.Scalar == "yes", "galactic-community-member-context");
                    case "has_origin" when Literal(field): return (_shelterClosure ? _context.SourceFacts?.Shelter?.OwnerOrigin : _context.SourceFacts?.OwnerOrigin) is { } origin ? Known(origin == field.Scalar) : Unknown("owner-origin-context");
                    case "has_ethic" when Literal(field): return Contains(_context.SourceFacts?.OwnerEthics ?? (_shelterClosure ? _context.SourceFacts?.Shelter?.OwnerEthics : null), field.Scalar, "owner-ethics-context");
                    case "has_civic" when (_shelterClosure || _conditionSource?.ProfileId is ConstructionOwnerCivicConditionInventory.Profile or ConstructionAuthorityConditionInventory.Profile) && Literal(field): return Contains(_context.SourceFacts?.Civics, field.Scalar, "selected-civics-context");
                    case "has_tradition" when Literal(field): return Contains(_shelterClosure ? _context.SourceFacts?.Shelter?.HeldTraditions : _context.SourceFacts?.OwnerHeldTraditions, field.Scalar, "held-traditions-context");
                    case "has_edict" when _shelterClosure && Literal(field): return Contains(_context.SourceFacts?.Shelter?.OwnerEdicts, field.Scalar, "owner-edicts-context");
                    case "has_policy_flag" when _shelterClosure && Literal(field): return Contains(_context.SourceFacts?.Shelter?.OwnerPolicyFlags, field.Scalar, "owner-policy-context");
                    case "has_modifier" when _shelterClosure && Literal(field): return Contains(_context.SourceFacts?.Shelter?.OwnerModifiers, field.Scalar, "owner-modifiers-context");
                    case "has_authority" when _shelterClosure: return Unknown("native-authority-matching");
                    case "has_authority" when Literal(field): return _context.SourceFacts?.OwnerAuthorityPredicates?.TryGetValue(field.Scalar!, out var authorityMatch) == true ? Known(authorityMatch) : Unknown("owner-authority-predicate-context");
                    case "has_country_flag":
                        if (string.IsNullOrWhiteSpace(field.Scalar) || field.Scalar.Length > 160 || field.Scalar.Any(char.IsControl) || field.Scalar.Contains('$') || field.Children.Count != 0) { return Unknown("owner-flag-argument-shape"); }
                        return _context.OwnerExists == true ? Contains(_context.SourceFacts?.OwnerFlags, field.Scalar, "owner-flags-context") : Unknown("owner-scope");
                    case "has_technology": return field.Scalar == "$TECH$" ? technologyArgument is null ? Unknown("owner-scripted-argument-unbound") : Contains(_context.Technologies, technologyArgument, "technology-context") : Contains(_context.Technologies, field.Scalar, "technology-context");
                    case "has_valid_civic": return Contains(_context.SourceFacts?.ValidCivics, field.Scalar, "valid-civics-context");
                    case "has_active_tradition": return Contains(_context.SourceFacts?.Traditions, field.Scalar, "traditions-context");
                    case "has_menace_perk": return Contains(_context.SourceFacts?.MenacePerks, field.Scalar, "menace-perks-context");
                    case "has_ascension_perk" when Literal(field): return Contains(_context.SourceFacts?.OwnerAscensionPerks, field.Scalar, "owner-ascension-perks-context");
                    case "is_country_type": return _context.SourceFacts?.OwnerCountryType is { } type ? Known(type == field.Scalar) : Unknown("country-type-context");
                }
            }
            if (field.Scalar is not ("yes" or "no")) { return Unknown("boolean-condition-value"); }
            if (scope == Scope.From) { return field.Key == "is_nomadic" ? Boolean(_context.FromNomadic, field.Scalar == "yes", "from-scope") : Unknown("from-condition"); }
            ConstructionOwnerPredicate? predicate = field.Key switch
            {
                "country_uses_food" => ConstructionOwnerPredicate.UsesFood,
                "country_uses_consumer_goods" => ConstructionOwnerPredicate.UsesConsumerGoods,
                "is_ai" => ConstructionOwnerPredicate.Ai,
                "is_wilderness_empire" => ConstructionOwnerPredicate.Wilderness,
                "is_regular_empire" => ConstructionOwnerPredicate.Regular,
                "is_fallen_empire_spiritualist" => ConstructionOwnerPredicate.SpiritualistFallenEmpire,
                "is_gestalt" => ConstructionOwnerPredicate.Gestalt,
                "is_nomadic" => ConstructionOwnerPredicate.Nomadic,
                "is_tankbound_empire" => ConstructionOwnerPredicate.Tankbound,
                "is_hive_empire" => ConstructionOwnerPredicate.Hive,
                _ => null
            };
            return predicate is { } key && _context.OwnerFacts.TryGetValue(key, out var fact) ? Boolean(fact, field.Scalar == "yes", "owner-predicate-context") : Unknown("owner-predicate-context");
        }

        private ConstructionCondition BuildingCount(ConstructionField field)
        {
            var types = field.Children.Where(child => child.Key == "type").ToArray();
            var values = field.Children.Where(child => child.Key == "value").ToArray();
            if (field.Children.Count != 2 || types.Length != 1 || values.Length != 1 || types[0].Operator != "=" || types[0].Scalar is null || values[0].Operator != "<" || !decimal.TryParse(values[0].Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var limit)) { return Unknown("building-count-operator"); }
            return _context.BuildingCounts is not null && _context.BuildingCounts.TryGetValue(types[0].Scalar!, out var count) ? Known(count < limit) : Unknown("building-count-context");
        }

        private ConstructionCondition Holding(ConstructionField field)
        {
            var types = field.Children.Where(child => child.Key == "holding").ToArray();
            var owners = field.Children.Where(child => child.Key == "owner").ToArray();
            if (field.Children.Count != 2 || types.Length != 1 || owners.Length != 1
                || !Literal(types[0]) || types[0].Scalar!.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')
                || !Literal(owners[0]) || owners[0].Scalar != "branch_office_owner")
                return Unknown("holding-condition-shape");
            if (_context.ForeignOwnership is not { } snapshot) return Unknown("planet-holdings-context");
            var candidates = snapshot.Holdings.Where(item => item.DefinitionId == types[0].Scalar).ToArray();
            if (candidates.Length == 0) return Known(false);
            return snapshot.BranchOfficeOwner is { } owner ? Known(candidates.Any(item => item.OwnerCountry == owner))
                : Unknown("branch-office-owner-context");
        }

        private ConstructionCondition SystemPlanets(IReadOnlyList<ConstructionField> fields, int depth)
        {
            if (_systemGraph is null) return Unknown("system-graph-context");
            var previous = _context;
            var previousBody = _systemBodyId;
            var values = new List<ConstructionCondition>();
            try
            {
                foreach (var pair in _systemGraph.Planets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (--_remaining < 0) return Unknown("condition-budget");
                    var context = pair.Value;
                    if (context is null) { values.Add(Unknown("system-planet-context")); continue; }
                    _context = context;
                    _systemBodyId = pair.Key;
                    var value = Condition(fields, Scope.Planet, depth: depth);
                    if (value.Truth == ConstructionTruth.True) return Known(true);
                    values.Add(value);
                }
                if (!_systemGraph.PlanetsComplete) values.Add(Unknown("system-planet-membership"));
                return values.Any(value => value.Truth == ConstructionTruth.Unknown)
                    ? new(ConstructionTruth.Unknown, Array.AsReadOnly(values.SelectMany(value => value.Gates).Distinct().Take(256).ToArray()))
                    : Known(false);
            }
            finally { _context = previous; _systemBodyId = previousBody; }
        }

        private ConstructionCondition ZoneBody(IReadOnlyList<ConstructionField> fields, string zone, int depth)
        {
            var previous = _zoneArgument;
            _zoneArgument = zone;
            try { return Condition(fields, Scope.Planet, depth: depth); }
            finally { _zoneArgument = previous; }
        }

        private static bool Literal(ConstructionField field) => field.Operator == "=" && field.Children.Count == 0 && field.Scalar is { Length: > 0 and <= 160 } value && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl) && !value.Contains('$');

        private static ConstructionCondition Boolean(bool? actual, bool expected, string dependency) => actual is { } value ? Known(value == expected) : Unknown(dependency);
        private static ConstructionCondition Contains(IReadOnlySet<string>? values, string value, string dependency) => values is null ? Unknown(dependency) : Known(values.Contains(value));
    }
}
