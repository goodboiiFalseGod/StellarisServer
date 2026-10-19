using System.Collections.Frozen;

namespace Stellaris.Core.Content.Construction.Startup;

public enum StartupAuthority { Regular, Hive, Machine }

/// <summary>Только факты, необходимые импортированным стартовым эффектам 4.5.2; неизвестные значения не подменяются.</summary>
public sealed class StartupOwnerContext
{
    public StartupAuthority Authority { get; }
    public IReadOnlySet<string> Ethics { get; }
    public IReadOnlySet<string> Civics { get; }
    public string Origin { get; }
    public bool IsNomadic { get; }
    public bool IsWilderness { get; }
    public bool IsIndividualMachine { get; }
    public int PlanetSize { get; }
    public int DistrictCapacityModifier { get; }
    public string PlanetClass { get; }
    public bool? CountryUsesFoodGameStart { get; }
    public int BlockerChoiceSeed { get; }
    public IReadOnlyDictionary<string, bool?> SourcePredicateFacts { get; }

    public StartupOwnerContext(StartupAuthority authority, IEnumerable<string> ethics, IEnumerable<string> civics, string origin, int planetSize, int districtCapacityModifier = 0, bool isNomadic = false, bool isWilderness = false, bool isIndividualMachine = false, string planetClass = "pc_continental", bool? countryUsesFoodGameStart = null, int blockerChoiceSeed = 0, IReadOnlyDictionary<string, bool?>? sourcePredicateFacts = null)
    {
        ArgumentNullException.ThrowIfNull(ethics);
        ArgumentNullException.ThrowIfNull(civics);
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(planetClass) || planetSize is < 1 or > 100 || (isIndividualMachine && authority != StartupAuthority.Machine)) { throw new ArgumentException("Invalid source startup context."); }
        Authority = authority;
        Ethics = ethics.ToFrozenSet(StringComparer.Ordinal);
        Civics = civics.ToFrozenSet(StringComparer.Ordinal);
        Origin = origin;
        PlanetSize = planetSize;
        DistrictCapacityModifier = districtCapacityModifier;
        IsNomadic = isNomadic;
        IsWilderness = isWilderness;
        IsIndividualMachine = isIndividualMachine;
        PlanetClass = planetClass;
        CountryUsesFoodGameStart = countryUsesFoodGameStart;
        BlockerChoiceSeed = blockerChoiceSeed;
        var predicateFacts = sourcePredicateFacts ?? new Dictionary<string, bool?>(StringComparer.Ordinal);
        if (predicateFacts.Keys.Any(string.IsNullOrWhiteSpace)) { throw new ArgumentException("Source predicate fact keys must be non-empty.", nameof(sourcePredicateFacts)); }
        SourcePredicateFacts = predicateFacts.ToFrozenDictionary(StringComparer.Ordinal);
    }
}

public sealed record StartupSourceFile(string RelativePath, long ByteLength, string Sha256);
public sealed record StartupDistrictResult(string DefinitionId, int Level);
public sealed record StartupSlotResult(string DistrictId, string SlotType, int Occurrence, string? InitialZoneId);
public sealed record StartupZoneResult(string DistrictId, string SlotType, int Occurrence, string ZoneId, int SlotNumber);
public sealed record StartupBuildingResult(string DefinitionId, string? ZoneId, string? DistrictId, int? SlotNumber, int Order, IReadOnlyList<string>? CompatibleZoneKeys = null);
public sealed record StartupFeatureResult(string InstanceId, string DefinitionId, int Order, bool IsBlocker, bool IsHidden, string? HiddenByBlockerInstanceId, string? HiddenFeatureInstanceId);
public sealed record StartupDistrictCapResult(string ModifierId, int Value);
public sealed record StartupZoneCompatibilityResult(string ZoneId, bool Compatible);
public sealed record StartupZoneRecurringEconomyProof(string ZoneId, string DefinitionPath, string DefinitionSha256, string InlinePath, string InlineSha256);
public sealed record StartupGate(string Code, string SourcePath, string Detail);

/// <summary>Неизменяемый результат явно поддержанного набора исходных эффектов.</summary>
public sealed class StartupBuildingPlan
{
    public string ProfileId { get; }
    public StartupOwnerContext Context { get; }
    public int FinalPlanetSize { get; }
    public IReadOnlyList<StartupDistrictResult> Districts { get; }
    public IReadOnlyList<StartupSlotResult> Slots { get; }
    public IReadOnlyList<StartupZoneResult> Zones { get; }
    public IReadOnlyList<StartupBuildingResult> Buildings { get; }
    public IReadOnlyList<StartupFeatureResult> Features { get; }
    public IReadOnlyList<StartupDistrictCapResult> DistrictCaps { get; }
    public IReadOnlyList<StartupZoneRecurringEconomyProof> ZoneRecurringEconomyProofs { get; }
    public IReadOnlyList<StartupSourceFile> Sources { get; }
    public IReadOnlyList<StartupGate> Gates { get; }

    internal StartupBuildingPlan(StartupOwnerContext context, int finalPlanetSize, IEnumerable<StartupDistrictResult> districts, IEnumerable<StartupSlotResult> slots, IEnumerable<StartupZoneResult> zones, IEnumerable<StartupBuildingResult> buildings, IEnumerable<StartupSourceFile> sources, IEnumerable<StartupGate> gates, IEnumerable<StartupFeatureResult>? features = null, IEnumerable<StartupDistrictCapResult>? districtCaps = null, IEnumerable<StartupZoneRecurringEconomyProof>? zoneRecurringEconomyProofs = null)
    {
        ProfileId = "startup-buildings-452-v1";
        Context = context;
        FinalPlanetSize = finalPlanetSize;
        Districts = Array.AsReadOnly(districts.ToArray());
        Slots = Array.AsReadOnly(slots.ToArray());
        Zones = Array.AsReadOnly(zones.ToArray());
        Buildings = Array.AsReadOnly(buildings.ToArray());
        Features = Array.AsReadOnly((features ?? Array.Empty<StartupFeatureResult>()).ToArray());
        DistrictCaps = Array.AsReadOnly((districtCaps ?? Array.Empty<StartupDistrictCapResult>()).ToArray());
        ZoneRecurringEconomyProofs = Array.AsReadOnly((zoneRecurringEconomyProofs ?? Array.Empty<StartupZoneRecurringEconomyProof>()).ToArray());
        Sources = Array.AsReadOnly(sources.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
        Gates = Array.AsReadOnly(gates.ToArray());
    }
}

public sealed class StartupBuildingImportException(string message) : Exception(message);
