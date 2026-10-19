using System.Collections.ObjectModel;

namespace Stellaris.Core.Content.Construction;

public sealed class ConstructionFeatureProjection
{
    public string GameVersion => "4.5.2";
    public string ProfileId => "construction-features-regular-continental-452-v1";
    public IReadOnlyDictionary<string, ConstructionPlacementDefinition> Definitions { get; }
    public IReadOnlyList<ConstructionFile> Files { get; }

    internal ConstructionFeatureProjection(IEnumerable<ConstructionPlacementDefinition> definitions, IEnumerable<ConstructionFile> files)
    {
        Definitions = new ReadOnlyDictionary<string, ConstructionPlacementDefinition>(definitions.ToDictionary(item => item.Id, StringComparer.Ordinal));
        Files = Array.AsReadOnly(files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
    }
}
