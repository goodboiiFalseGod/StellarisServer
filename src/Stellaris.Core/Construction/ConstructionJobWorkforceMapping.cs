namespace Stellaris.Core.Construction;

// Раскрытие трёх static modifiers из 4.5.2 common/static_modifiers/24_static_modifiers_jobs.txt.
public static class ConstructionJobWorkforceMapping
{
    private static readonly IReadOnlyList<string> Physicists = Array.AsReadOnly(new[] { "physicist", "brain_drone_physicist", "calculator_physicist" });
    private static readonly IReadOnlyList<string> Biologists = Array.AsReadOnly(new[] { "biologist", "brain_drone_biologist", "calculator_biologist" });
    private static readonly IReadOnlyList<string> Engineers = Array.AsReadOnly(new[] { "engineer", "brain_drone_engineer", "calculator_engineer" });

    public static IReadOnlyList<string>? Find(string key) => key switch
    {
        "physicist_jobs_bonus_workforce_mult" => Physicists,
        "biologist_jobs_bonus_workforce_mult" => Biologists,
        "engineer_jobs_bonus_workforce_mult" => Engineers,
        _ => null
    };
}
