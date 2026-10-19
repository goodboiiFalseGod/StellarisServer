namespace Stellaris.Core.Content.Construction;

internal static class ConstructionShelterClosureInventory
{
    public const string PROFILE = "construction-shelter-closure-catalog-452-v1";

    public static (string Path, string Kind, string[] Ids)[] Create() =>
    [
        ("common/scripted_triggers/01_scripted_triggers_buildings.txt", "planetCondition", ["planet_is_habitat_equivalent", "planet_is_ecu_equivalent", "planet_is_ring_world_equivalent", "has_knights_zone"]),
        ("common/scripted_triggers/00_scripted_triggers.txt", "planetCondition", ["is_special_colony_type", "uses_habitat_capitals", "has_cosmic_storms_dlc", "has_ancrel", "has_zone"]),
        ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition", ["has_functional_civic", "has_tech_or_functional_civic", "is_fallen_empire", "is_regular_empire", "is_gestalt", "is_hive_empire", "is_wilderness_empire", "is_machine_empire", "is_megacorp", "has_corporate_government", "is_imperial_authority", "is_oligarchic_authority", "is_democratic_authority", "is_cyber_imperial", "is_synth_imperial", "is_worker_coop_empire"]),
        ("common/scripted_triggers/05_scripted_triggers_biogenesis.txt", "ownerCondition", ["is_purity_authority", "is_cloning_authority", "is_mutation_authority"]),
        ("common/scripted_triggers/08_scripted_triggers_shroud.txt", "ownerCondition", ["is_tankbound_empire", "is_transcendent_authority", "is_corporeal_authority"]),
        ("common/scripted_triggers/02_scripted_triggers_machine_age.txt", "ownerCondition", ["is_individual_machine"]),
        ("common/scripted_triggers/01_scripted_triggers_jobs.txt", "groupCondition", ["is_robotic_species"]),
        ("common/scripted_triggers/00_scripted_triggers.txt", "popCondition", ["is_specialist_category"]),
        ("common/script_values/00_script_values.txt", "scriptValue", ["count_specialists", "count_any_districts", "imperious_architecture_scaling_value", "capital_tier_minus_1_value"]),
        ("common/script_values/03_script_values_cosmic_storms.txt", "scriptValue", ["storm_building_production_multiplier"]),
        ("common/script_values/06_script_values_biogenesis.txt", "scriptValue", ["species_traits_evopred_count_value"]),
        ("common/scripted_effects/biogenesis_effects.txt", "storedVariableEffect", ["set_species_traits_evopred_count_variable"])
    ];

    public static void Validate(ConstructionCatalogProjection source)
    {
        var expected = Create().SelectMany(item => item.Ids.Select(id => (Id: id, item.Kind, item.Path))).ToArray();
        if (source.ProfileId != PROFILE || source.Definitions.Count != expected.Length || expected.Any(item => !source.Definitions.TryGetValue(item.Id, out var definition) || definition.Kind != item.Kind || definition.Origin.Source != item.Path))
        {
            throw new ArgumentException("Invalid selected shelter closure inventory.", nameof(source));
        }
    }
}
