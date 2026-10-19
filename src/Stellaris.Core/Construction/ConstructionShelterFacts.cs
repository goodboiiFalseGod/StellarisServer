namespace Stellaris.Core.Construction;

/// <summary>Trusted realized runtime facts, not template defaults or client assertions.</summary>
public sealed class ConstructionShelterFacts
{
    public string? OwnerOrigin { get; }
    public IReadOnlySet<string>? OwnerEthics { get; }
    public IReadOnlySet<string>? HeldTraditions { get; }
    public IReadOnlySet<string>? OwnerEdicts { get; }
    public IReadOnlySet<string>? OwnerPolicyFlags { get; }
    public IReadOnlySet<string>? OwnerModifiers { get; }
    public IReadOnlySet<string>? PlanetFlags { get; }
    public IReadOnlySet<string>? Zones { get; }
    public IReadOnlySet<string>? HostDlc { get; }
    public string? CarrierType { get; }
    public bool? FounderSpeciesExists { get; }
    public string? FounderSpeciesArchetype { get; }
    public int? CapitalTier { get; }
    public IReadOnlySet<string>? ActiveResolutions { get; }
    public bool? OwnerGalacticCommunityMember { get; }
    public bool? SystemExists { get; }
    public IReadOnlySet<string>? SystemStarFlags { get; }
    public ConstructionCapitalLinks? CapitalLinks { get; }
    public bool? LeaderExists { get; }
    public ConstructionGrowingSpeciesLinks? GrowingSpecies { get; }

    public ConstructionShelterFacts(string? ownerOrigin = null, IEnumerable<string>? ownerEthics = null, IEnumerable<string>? heldTraditions = null, IEnumerable<string>? ownerEdicts = null, IEnumerable<string>? ownerPolicyFlags = null, IEnumerable<string>? ownerModifiers = null, IEnumerable<string>? planetFlags = null, IEnumerable<string>? zones = null, IEnumerable<string>? hostDlc = null, string? carrierType = null, bool? founderSpeciesExists = null, string? founderSpeciesArchetype = null, int? capitalTier = null, IEnumerable<string>? activeResolutions = null, bool? ownerGalacticCommunityMember = null, bool? systemExists = null, IEnumerable<string>? systemStarFlags = null, ConstructionCapitalLinks? capitalLinks = null, bool? leaderExists = null, ConstructionGrowingSpeciesLinks? growingSpecies = null)
    {
        if (capitalTier is < 0 or > 4096) { throw new ArgumentOutOfRangeException(nameof(capitalTier)); }
        if (carrierType is not (null or "planet" or "ship")) { throw new ArgumentException("Unsupported realized carrier type.", nameof(carrierType)); }
        OwnerOrigin = ownerOrigin is null ? null : ConstructionValues.Id(ownerOrigin);
        OwnerEthics = ConstructionContext.Set(ownerEthics);
        HeldTraditions = ConstructionContext.Set(heldTraditions);
        OwnerEdicts = ConstructionContext.Set(ownerEdicts);
        OwnerPolicyFlags = ConstructionContext.Set(ownerPolicyFlags);
        OwnerModifiers = ConstructionContext.Set(ownerModifiers);
        PlanetFlags = ConstructionContext.Set(planetFlags);
        Zones = ConstructionContext.Set(zones);
        HostDlc = ConstructionContext.Set(hostDlc);
        CarrierType = carrierType;
        FounderSpeciesExists = founderSpeciesExists;
        FounderSpeciesArchetype = founderSpeciesArchetype is null ? null : ConstructionValues.Id(founderSpeciesArchetype);
        CapitalTier = capitalTier;
        ActiveResolutions = ConstructionContext.Set(activeResolutions);
        OwnerGalacticCommunityMember = ownerGalacticCommunityMember;
        SystemExists = systemExists;
        SystemStarFlags = ConstructionContext.Set(systemStarFlags);
        CapitalLinks = capitalLinks;
        LeaderExists = leaderExists;
        GrowingSpecies = growingSpecies;
    }
}

/// <summary>Resolved growing target and its species link; this does not select or simulate growth.</summary>
public sealed class ConstructionGrowingSpeciesLinks
{
    public bool? TargetExists { get; }
    public string? TargetId { get; }
    public string? SpeciesId { get; }
    public IReadOnlySet<string>? SpeciesTraits { get; }

    public ConstructionGrowingSpeciesLinks(bool? targetExists = null, string? targetId = null, string? speciesId = null, IEnumerable<string>? speciesTraits = null)
    {
        TargetExists = targetExists;
        TargetId = targetId is null ? null : ConstructionValues.Id(targetId);
        SpeciesId = speciesId is null ? null : ConstructionValues.Id(speciesId);
        SpeciesTraits = ConstructionContext.Set(speciesTraits);
    }
}

/// <summary>Actual local identity links from the originating colony and its owner; null links remain unresolved.</summary>
public sealed class ConstructionCapitalLinks
{
    public string? ColonyId { get; }
    public string? SystemId { get; }
    public string? OwnerCapitalColonyId { get; }
    public string? OwnerCapitalSystemId { get; }

    public ConstructionCapitalLinks(string? colonyId = null, string? systemId = null, string? ownerCapitalColonyId = null, string? ownerCapitalSystemId = null)
    {
        ColonyId = colonyId is null ? null : ConstructionValues.Id(colonyId);
        SystemId = systemId is null ? null : ConstructionValues.Id(systemId);
        OwnerCapitalColonyId = ownerCapitalColonyId is null ? null : ConstructionValues.Id(ownerCapitalColonyId);
        OwnerCapitalSystemId = ownerCapitalSystemId is null ? null : ConstructionValues.Id(ownerCapitalSystemId);
        if (ColonyId is not null && ColonyId == OwnerCapitalColonyId && SystemId is not null && OwnerCapitalSystemId is not null && SystemId != OwnerCapitalSystemId) { throw new ArgumentException("Same colony cannot have contradictory actual system identities."); }
    }
}
