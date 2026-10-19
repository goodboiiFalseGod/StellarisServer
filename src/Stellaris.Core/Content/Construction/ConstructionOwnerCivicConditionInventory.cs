namespace Stellaris.Core.Content.Construction;

/// <summary>Native civic-only owner helpers; выбранные и действующие civic не смешиваются.</summary>
internal static class ConstructionOwnerCivicConditionInventory
{
    internal const string Profile = "construction-conditions-civics-catalog-452-v2";
    internal static readonly IReadOnlyList<string> Helpers = Array.AsReadOnly(Create().SelectMany(group => group.Ids).ToArray());
    internal static (string Path, string Kind, string[] Ids)[] Create() =>
    [
        ("common/scripted_triggers/00_scripted_triggers.txt", "ownerCondition",
            ["is_idyllic_bloom_empire", "is_death_cult_empire", "is_memorialist_empire",
             "is_relentless_industrialist_empire", "is_toxic_baths_empire", "is_memory_vault_empire",
             "is_astrometeorologist_empire", "is_storm_callers_empire"]),
        ("common/scripted_triggers/02_scripted_triggers_machine_age.txt", "ownerCondition",
            ["is_natural_design_empire", "is_guided_sapience_empire"]),
        ("common/scripted_triggers/08_scripted_triggers_shroud.txt", "ownerCondition",
            ["is_entropy_drinkers_empire", "is_twisted_experimenters_empire", "is_tankbound_empire"]),
        ("common/scripted_triggers/08_scripted_triggers_infernals.txt", "ownerCondition",
            ["is_world_forger_empire", "is_galvanic_empire"])
    ];
}
