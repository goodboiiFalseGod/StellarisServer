using System.Collections.Frozen;

namespace Stellaris.Core.Content.PlanetEconomy;

public enum PlanetEconomyDefinitionKind
{
    Building,
    District,
    Zone
}

public enum PlanetEconomyDefinitionCoverageStatus
{
    Included,
    ExcludedSpecific,
    Unresolved
}

public sealed record PlanetEconomyDefinitionCoverage(
    PlanetEconomyDefinitionKind Kind,
    string Id,
    PlanetEconomyDefinitionCoverageStatus Status,
    string? Reason,
    PlanetEconomyProvenance Provenance);

public enum PlanetEconomyTermKind
{
    ResourceProduction,
    ResourceUpkeep,
    ResourceMultiplier,
    PlanetModifierMultiplier,
    BuildingUpkeep,
    DistrictUpkeep,
    ZoneUpkeep,
    WorkforceCapacity,
    WorkforceCapacityMultiplier,
    PlanetModifier,
    StructureModifier,
    ConstructionCost,
    ConstructionTime,
    ConstructionCostMultiplier
}

public enum PlanetEconomyConditionDisposition
{
    Unconditional,
    ActiveInOrdinaryContext,
    InactiveInOrdinaryContext,
    RequiresRuntimeContext
}

public enum PlanetEconomyStoragePolicy
{
    SourceCapped,
    UnlimitedReserve,
    Unspecified
}

public enum PlanetEconomyMaximumKind
{
    Unknown,
    Generic,
    Fixed,
    Special
}

public enum PlanetEconomyApplicationScope
{
    PerDefinitionInstance,
    PerAttachedDistrictLevel
}

public sealed record PlanetEconomySourceFile(string RelativePath, long ByteLength, string Sha256);

public sealed record PlanetEconomyProvenance(string RelativePath, string Sha256, int Line, int Column);

/// <summary>Неизменяемый узел исходного условия с сохранённой структурой операторов и значений.</summary>
public sealed class PlanetEconomyCondition
{
    public string Key { get; }
    public string Operator { get; }
    public string? Value { get; }
    public IReadOnlyList<PlanetEconomyCondition> Children { get; }
    public PlanetEconomyProvenance Provenance { get; }

    internal PlanetEconomyCondition(string key, string op, string? value, IEnumerable<PlanetEconomyCondition> children, PlanetEconomyProvenance provenance)
    {
        Key = key;
        Operator = op;
        Value = value;
        Children = Array.AsReadOnly(children.ToArray());
        Provenance = provenance;
    }
}

public sealed record PlanetEconomyTerm(
    PlanetEconomyTermKind Kind,
    string TargetId,
    decimal? Value,
    string? SourceKey,
    string? SourceExpression,
    PlanetEconomyConditionDisposition ConditionDisposition,
    PlanetEconomyCondition? Condition,
    PlanetEconomyProvenance Provenance,
    PlanetEconomyApplicationScope ApplicationScope = PlanetEconomyApplicationScope.PerDefinitionInstance);

public sealed class PlanetEconomyResource
{
    public string Id { get; }
    public string Category { get; }
    public PlanetEconomyStoragePolicy StoragePolicy { get; }
    public decimal? SourceMaximum { get; }
    public PlanetEconomyProvenance Provenance { get; }
    public PlanetEconomyMaximumKind MaximumKind { get; }
    public PlanetEconomyCondition? DynamicCapacity { get; }

    internal PlanetEconomyResource(string id, string category, PlanetEconomyStoragePolicy storagePolicy, decimal? sourceMaximum,
        PlanetEconomyProvenance provenance, PlanetEconomyMaximumKind maximumKind = PlanetEconomyMaximumKind.Unknown,
        PlanetEconomyCondition? dynamicCapacity = null)
    {
        Id = id;
        Category = category;
        StoragePolicy = storagePolicy;
        SourceMaximum = sourceMaximum;
        Provenance = provenance;
        MaximumKind = maximumKind;
        DynamicCapacity = dynamicCapacity;
    }
}

public sealed class PlanetEconomyJob
{
    public string Id { get; }
    public string? NameKey { get; }
    public string? DescriptionKey { get; }
    public string? PluralKey { get; }
    public string JobStratum { get; }
    public IReadOnlyList<string> SourceJobTags { get; }
    public string? EconomicCategory { get; }
    public string? IconId { get; }
    public string? BuildingIconId { get; }
    public string? IconConceptId { get; }
    public string? IconAlias { get; }
    public string? LocalIconPath { get; }
    public IReadOnlyList<PlanetEconomyTerm> Terms { get; }
    public IReadOnlyList<PlanetEconomyProvenance> OmittedSpecialBranches { get; }
    public PlanetEconomyCondition? Availability { get; }
    public PlanetEconomyConditionDisposition AvailabilityDisposition { get; }
    public string? PossiblePrecalculation { get; }
    public PlanetEconomyProvenance Provenance { get; }

    internal PlanetEconomyJob(string id, string jobStratum, IEnumerable<string> sourceJobTags, string? economicCategory, string? iconId, string? buildingIconId,
        string? iconConceptId, string? iconAlias, string? localIconPath,
        IEnumerable<PlanetEconomyTerm> terms, IEnumerable<PlanetEconomyProvenance> omittedSpecialBranches,
        PlanetEconomyCondition? availability, PlanetEconomyConditionDisposition availabilityDisposition, string? possiblePrecalculation,
        PlanetEconomyProvenance provenance, string? sourceNameToken = null, string? sourceDescriptionToken = null)
    {
        Id = id;
        var name = PlanetLocalizationBindings.Get("job", sourceNameToken ?? id);
        var description = PlanetLocalizationBindings.Get("job", sourceDescriptionToken ?? sourceNameToken ?? id);
        NameKey = name.NameKey;
        PluralKey = name.PluralKey;
        DescriptionKey = description.DescriptionKey;
        JobStratum = jobStratum;
        SourceJobTags = Array.AsReadOnly(sourceJobTags.ToArray());
        EconomicCategory = economicCategory;
        IconId = iconId;
        BuildingIconId = buildingIconId;
        IconConceptId = iconConceptId;
        IconAlias = iconAlias;
        LocalIconPath = localIconPath;
        Terms = Array.AsReadOnly(terms.ToArray());
        OmittedSpecialBranches = Array.AsReadOnly(omittedSpecialBranches.ToArray());
        Availability = availability;
        AvailabilityDisposition = availabilityDisposition;
        PossiblePrecalculation = possiblePrecalculation;
        Provenance = provenance;
    }
}

public enum PlanetEconomyJobClosureStatus
{
    Covered,
    Gated,
    Missing
}

/// <summary>Source-backed closure record for a job capacity referenced by an included planet definition.</summary>
public sealed record PlanetEconomyJobClosure(string JobId, PlanetEconomyJobClosureStatus Status, string? Reason, PlanetEconomyProvenance Provenance);

public sealed class PlanetEconomyDefinition
{
    public PlanetEconomyDefinitionKind Kind { get; }
    public string Id { get; }
    public string? EconomicCategory { get; }
    public IReadOnlyList<string> BuildingSets { get; }
    public IReadOnlyList<string> ZoneSets { get; }
    public IReadOnlyList<string> IncludedBuildingSets { get; }
    public IReadOnlyList<string> ExcludedBuildingSets { get; }
    public IReadOnlyList<string> IncludedBuildings { get; }
    public IReadOnlyList<string> ExcludedBuildings { get; }
    public IReadOnlyList<PlanetEconomyTerm> Terms { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SourceProperties { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<PlanetEconomyCondition>> SourceFields { get; }
    public PlanetEconomyCondition? Availability { get; }
    public PlanetEconomyConditionDisposition AvailabilityDisposition { get; }
    public string? NameKey { get; }
    public string? LocalizationKey => NameKey ?? PlanetLocalizationBindings.Get(Kind.ToString().ToLowerInvariant(), Id).NameKey;
    public string? DescriptionKey => PlanetLocalizationBindings.Get(Kind.ToString().ToLowerInvariant(), Id).DescriptionKey;
    public string? IconId { get; }
    public PlanetEconomyProvenance Provenance { get; }

    internal PlanetEconomyDefinition(PlanetEconomyDefinitionKind kind, string id, string? economicCategory,
        IEnumerable<string> buildingSets, IEnumerable<string> zoneSets, IEnumerable<string> includedBuildingSets,
        IEnumerable<string> excludedBuildingSets, IEnumerable<string> includedBuildings, IEnumerable<string> excludedBuildings,
        IEnumerable<PlanetEconomyTerm> terms, IReadOnlyDictionary<string, IReadOnlyList<string>> sourceProperties,
        IReadOnlyDictionary<string, IReadOnlyList<PlanetEconomyCondition>> sourceFields,
        PlanetEconomyCondition? availability, PlanetEconomyConditionDisposition availabilityDisposition, string? nameKey, string? iconId, PlanetEconomyProvenance provenance)
    {
        Kind = kind;
        Id = id;
        EconomicCategory = economicCategory;
        BuildingSets = Array.AsReadOnly(buildingSets.ToArray());
        ZoneSets = Array.AsReadOnly(zoneSets.ToArray());
        IncludedBuildingSets = Array.AsReadOnly(includedBuildingSets.ToArray());
        ExcludedBuildingSets = Array.AsReadOnly(excludedBuildingSets.ToArray());
        IncludedBuildings = Array.AsReadOnly(includedBuildings.ToArray());
        ExcludedBuildings = Array.AsReadOnly(excludedBuildings.ToArray());
        Terms = Array.AsReadOnly(terms.ToArray());
        SourceProperties = sourceProperties.ToFrozenDictionary(StringComparer.Ordinal);
        SourceFields = sourceFields.ToFrozenDictionary(StringComparer.Ordinal);
        Availability = availability;
        AvailabilityDisposition = availabilityDisposition;
        NameKey = nameKey;
        IconId = iconId;
        Provenance = provenance;
    }
}

/// <summary>Неизменяемый source-профиль выбранной экономики для будущего планировщика M3 без регистрации мира или сессии.</summary>
public sealed class PlanetEconomyCatalog
{
    public string ProfileId { get; }
    public string GameVersion { get; }
    public IReadOnlyDictionary<string, PlanetEconomyResource> Resources { get; }
    public IReadOnlyDictionary<string, PlanetEconomyJob> Jobs { get; }
    public IReadOnlyList<PlanetEconomyJobClosure> JobClosure { get; }
    public IReadOnlyDictionary<string, PlanetEconomyDefinition> Buildings { get; }
    public IReadOnlyDictionary<string, PlanetEconomyDefinition> Districts { get; }
    public IReadOnlyDictionary<string, PlanetEconomyDefinition> Zones { get; }
    public IReadOnlyList<PlanetEconomySourceFile> Sources { get; }
    public IReadOnlyList<PlanetEconomyDefinitionCoverage> DefinitionCoverage { get; }

    internal PlanetEconomyCatalog(string gameVersion, IEnumerable<PlanetEconomyResource> resources,
        IEnumerable<PlanetEconomyJob> jobs, IEnumerable<PlanetEconomyDefinition> definitions,
        IEnumerable<PlanetEconomySourceFile> sources, IEnumerable<PlanetEconomyDefinitionCoverage> definitionCoverage,
        IEnumerable<PlanetEconomyJobClosure> jobClosure)
    {
        ProfileId = "planet-economy-basic-452-v1";
        GameVersion = gameVersion;
        Resources = resources.ToFrozenDictionary(item => item.Id, StringComparer.Ordinal);
        Jobs = jobs.ToFrozenDictionary(item => item.Id, StringComparer.Ordinal);
        JobClosure = Array.AsReadOnly(jobClosure.OrderBy(item => item.JobId, StringComparer.Ordinal).ToArray());
        var all = definitions.ToArray();
        Buildings = all.Where(item => item.Kind == PlanetEconomyDefinitionKind.Building).ToFrozenDictionary(item => item.Id, StringComparer.Ordinal);
        Districts = all.Where(item => item.Kind == PlanetEconomyDefinitionKind.District).ToFrozenDictionary(item => item.Id, StringComparer.Ordinal);
        Zones = all.Where(item => item.Kind == PlanetEconomyDefinitionKind.Zone).ToFrozenDictionary(item => item.Id, StringComparer.Ordinal);
        Sources = Array.AsReadOnly(sources.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
        DefinitionCoverage = Array.AsReadOnly(definitionCoverage.OrderBy(item => item.Kind).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray());
    }
}
