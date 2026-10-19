namespace Stellaris.Core.Content.Construction;

/// <summary>Замкнутый набор native helpers наличия зон, без подмены их тел.</summary>
internal static class ConstructionZoneConditionInventory
{
    internal const string Profile = "construction-conditions-zones-catalog-452-v1";
    internal static readonly IReadOnlyList<string> Helpers = Array.AsReadOnly(new[]
    {
        "has_any_unspecialized_research_zone", "has_any_research_unity_zone",
        "has_any_research_physics_zone", "has_any_research_society_zone",
        "has_any_research_engineering_zone", "has_any_specialized_research_zone",
        "has_any_research_zone", "has_any_agriculture_zone", "has_any_normal_agriculture_zone",
        "has_any_exotic_gases_zone", "has_any_generator_zone", "has_any_normal_generator_zone",
        "has_volatile_motes_zone", "has_any_mining_zone", "has_any_normal_mining_zone",
        "has_any_rare_crystals_zone", "has_any_betharian_zone", "has_any_factory_zone",
        "has_any_foundry_zone", "has_mixed_industrial_zone", "has_any_industrial_zone",
        "has_any_unity_zone", "has_any_trade_zone", "has_any_fortress_zone"
    });
}
