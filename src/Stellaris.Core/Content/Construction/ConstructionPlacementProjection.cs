using System.Collections.ObjectModel;

namespace Stellaris.Core.Content.Construction;

public sealed class ConstructionPlacementDefinition
{
    public string Id { get; }
    public string Kind { get; }
    public ConstructionOrigin Origin { get; }
    public IReadOnlyList<ConstructionField> Fields { get; }

    internal ConstructionPlacementDefinition(string id, string kind, ConstructionOrigin origin, IEnumerable<ConstructionField> fields)
    {
        Id = id;
        Kind = kind;
        Origin = origin;
        Fields = Array.AsReadOnly(fields.ToArray());
    }
}

public sealed class ConstructionPlacementProjection
{
    public string GameVersion => "4.5.2";
    public string ProfileId => "construction-placement-452-v1";
    public IReadOnlyDictionary<string, ConstructionPlacementDefinition> Definitions { get; }
    public IReadOnlyList<ConstructionFile> Files { get; }

    internal ConstructionPlacementProjection(IEnumerable<ConstructionPlacementDefinition> definitions, IEnumerable<ConstructionFile> files)
    {
        Definitions = new ReadOnlyDictionary<string, ConstructionPlacementDefinition>(definitions.ToDictionary(item => item.Id, StringComparer.Ordinal));
        Files = Array.AsReadOnly(files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
    }
}
