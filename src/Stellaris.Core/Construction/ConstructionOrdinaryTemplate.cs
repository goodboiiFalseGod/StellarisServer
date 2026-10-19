using System.Collections.Frozen;

namespace Stellaris.Core.Construction;

/// <summary>Accepted quick-start presets, not exclusive empire choices, native startup generation or applied modifiers.</summary>
public sealed class ConstructionOrdinaryTemplate
{
    private static readonly IReadOnlySet<string> _startingTechnologies = new[]
    {
        "tech_solar_panel_network", "tech_corvettes", "tech_starbase_1", "tech_starbase_2", "tech_assault_armies", "tech_ship_armor_1", "tech_thrusters_1", "tech_space_defense_station_1", "tech_basic_industry", "tech_mechanized_mining", "tech_space_construction", "tech_mass_drivers_1", "tech_flak_batteries_1", "tech_missiles_1",
        "tech_space_exploration", "tech_basic_science_lab_1", "tech_fission_power", "tech_reactor_boosters_1", "tech_shields_1", "tech_power_plant_1", "tech_hyper_drive_1", "tech_lasers_1", "tech_pd_tracking_1",
        "tech_planetary_defenses", "tech_industrial_farming", "tech_hydroponics", "tech_colonization_1", "tech_basic_health", "tech_planetary_government", "tech_holo_entertainment", "tech_interplanetary_commerce"
    }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, ConstructionOrdinaryTemplate> _templates = new[]
    {
        new ConstructionOrdinaryTemplate("economic", ["ethic_fanatic_authoritarian", "ethic_materialist"], "civic_mining_guilds", ["trait_ingenious"]),
        new ConstructionOrdinaryTemplate("scientific", ["ethic_fanatic_materialist", "ethic_authoritarian"], "civic_hyperspace_specialty", ["trait_intelligent"]),
        new ConstructionOrdinaryTemplate("military", ["ethic_fanatic_militarist", "ethic_authoritarian"], "civic_distinguished_admiralty", ["trait_strong", "trait_resilient"])
    }.ToFrozenDictionary(template => template.Id, StringComparer.Ordinal);

    public string Id { get; }
    public string AuthorityId => "auth_imperial";
    public string OriginId => "origin_default";
    public string SpeciesClassId => "HUM";
    public string PreferenceTraitId => "trait_pc_continental_preference";
    /// <summary>Founder creation preference, not the actual class of a runtime colony.</summary>
    public string IdealPlanetClass => "pc_continental";
    public IReadOnlySet<string> Ethics { get; }
    public IReadOnlySet<string> Civics { get; }
    public IReadOnlySet<string> Traits { get; }
    public IReadOnlySet<string> StartingTechnologies => _startingTechnologies;

    private ConstructionOrdinaryTemplate(string id, string[] ethics, string civic, string[] traits)
    {
        Id = id;
        Ethics = ethics.ToFrozenSet(StringComparer.Ordinal);
        Civics = new[] { civic, "civic_functional_architecture" }.ToFrozenSet(StringComparer.Ordinal);
        Traits = traits.Append(PreferenceTraitId).ToFrozenSet(StringComparer.Ordinal);
    }

    public static ConstructionOrdinaryTemplate Get(string id)
    {
        if (id is null || !_templates.TryGetValue(id, out var template)) { throw new ArgumentException("Unknown accepted ordinary empire template.", nameof(id)); }
        return template;
    }

    /// <summary>Initializes researched prerequisites only; runtime scope/layout facts remain explicit.</summary>
    public ConstructionContext CreateInitialConstructionContext(ConstructionContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        return new(runtimeContext.OwnerExists, runtimeContext.OwnerFacts, runtimeContext.FromNomadic, runtimeContext.PlanetClass, runtimeContext.Modifiers, runtimeContext.CarrierFlags, StartingTechnologies, runtimeContext.BuildingCounts, runtimeContext.SourceFacts, runtimeContext.CapitalTier,
            runtimeContext.ForeignOwnership, runtimeContext.ForeignOwnership?.Planet, runtimeContext.ForeignOwnership?.PlanetOwner,
            runtimeContext.SystemGraph, runtimeContext.SystemGraph?.RootBodyId, runtimeContext.DistrictCounts);
    }
}
