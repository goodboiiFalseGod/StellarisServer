using System.Collections.ObjectModel;

namespace Stellaris.Core.Content.Construction;

public sealed record ConstructionCatalogParameter(string DefinitionId, string FieldPath, string Operator, decimal Value, ConstructionOrigin Origin);

public sealed class ConstructionCatalogProjection
{
    public string GameVersion => "4.5.2";
    public string ProfileId { get; }
    public IReadOnlyDictionary<string, ConstructionPlacementDefinition> Definitions { get; }
    public IReadOnlyDictionary<string, ConstructionPlacementDefinition> ScriptedConditions { get; }
    public IReadOnlyList<ConstructionCatalogParameter> Parameters { get; }
    public IReadOnlyList<ConstructionFile> Files { get; }
    public IReadOnlyList<ConstructionCoverage> Coverage { get; }

    internal ConstructionCatalogProjection(string profileId, IEnumerable<ConstructionPlacementDefinition> definitions, IEnumerable<ConstructionCatalogParameter> parameters, IEnumerable<ConstructionFile> files, IEnumerable<ConstructionCoverage> coverage, IEnumerable<ConstructionPlacementDefinition>? scriptedConditions = null)
    {
        ProfileId = profileId;
        Definitions = new ReadOnlyDictionary<string, ConstructionPlacementDefinition>(definitions.ToDictionary(item => item.Id, StringComparer.Ordinal));
        var scripted = (scriptedConditions ?? []).Take(2049).ToArray();
        if (scripted.Length > 2048 || scripted.Any(item => item.Kind != "scriptedCondition" || !item.Origin.Source.StartsWith("common/scripted_triggers/", StringComparison.Ordinal)))
            throw new ArgumentException("Недопустимый реестр scripted условий.");
        ScriptedConditions = new ReadOnlyDictionary<string, ConstructionPlacementDefinition>(scripted.ToDictionary(item => item.Id, StringComparer.Ordinal));
        Parameters = Array.AsReadOnly(parameters.ToArray());
        Files = Array.AsReadOnly(files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
        Coverage = Array.AsReadOnly(coverage.ToArray());
    }
}
