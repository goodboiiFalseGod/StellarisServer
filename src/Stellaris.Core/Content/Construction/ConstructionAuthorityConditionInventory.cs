namespace Stellaris.Core.Content.Construction;

/// <summary>Native helpers частичного authority closure M3.</summary>
internal static class ConstructionAuthorityConditionInventory
{
    internal const string Profile = "construction-conditions-authority-catalog-452-v4";
    internal const string Source = "common/scripted_triggers/00_scripted_triggers.txt";
    internal static readonly IReadOnlyList<string> Helpers = Array.AsReadOnly(new[] { "is_megacorp", "has_corporate_government", "is_machine_empire" });
    internal static readonly IReadOnlyList<(string Source, string[] Helpers)> TraditionHelpers = Array.AsReadOnly(new[]
    {
        ("common/scripted_triggers/05_scripted_triggers_biogenesis.txt", new[] { "has_cloning_tradition", "has_genetic_stage_1_tradition", "has_purity_heightened_attributes_tradition" }),
        ("common/scripted_triggers/08_scripted_triggers_shroud.txt", new[] { "has_psi_corps_tradition" })
    });
}
