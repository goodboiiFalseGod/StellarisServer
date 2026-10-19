using System.Collections.ObjectModel;

namespace Stellaris.Core.Content.Construction;

public enum ConstructionEconomicOperation { Produces, Cost, Upkeep }
public enum ConstructionEconomicModifierFamily { Mult, Add }

public sealed class ConstructionEconomicCategory
{
    public ConstructionPlacementDefinition Source { get; }
    public string? Parent { get; }
    public string? ModifierCategory { get; }
    public IReadOnlyList<ConstructionEconomicOperation> Mult { get; }
    public IReadOnlyList<ConstructionEconomicOperation> Add { get; }

    internal ConstructionEconomicCategory(ConstructionPlacementDefinition source)
    {
        Source = source;
        Parent = ConstructionEconomicCategoryProjection.ReadParent(source);
        ModifierCategory = ConstructionEconomicCategoryProjection.ReadScalar(source, "modifier_category");
        Mult = ReadOperations(source, "generate_mult_modifiers");
        Add = ReadOperations(source, "generate_add_modifiers");
        if (source.Fields.Any(field => field.Key.StartsWith("triggered_", StringComparison.Ordinal))) { throw new InvalidDataException("Triggered economic category generation is unsupported."); }
    }

    private static IReadOnlyList<ConstructionEconomicOperation> ReadOperations(ConstructionPlacementDefinition source, string key)
    {
        var fields = source.Fields.Where(field => field.Key == key).ToArray();
        if (fields.Length > 1 || fields.Any(field => field.Operator != "=" || field.Scalar is not null)) { throw new InvalidDataException("Invalid economic category generation declaration."); }
        var result = new List<ConstructionEconomicOperation>();
        foreach (var field in fields.SelectMany(field => field.Children))
        {
            if (field.Operator.Length != 0 || field.Scalar is null || field.Children.Count != 0) { throw new InvalidDataException("Invalid economic category generation operation."); }
            var operation = field.Scalar switch
            {
                "produces" => ConstructionEconomicOperation.Produces,
                "cost" => ConstructionEconomicOperation.Cost,
                "upkeep" => ConstructionEconomicOperation.Upkeep,
                _ => throw new InvalidDataException("Unsupported economic category generation operation.")
            };
            if (result.Contains(operation)) { throw new InvalidDataException("Duplicate economic category generation operation."); }
            result.Add(operation);
        }
        return Array.AsReadOnly(result.ToArray());
    }
}

public sealed class ConstructionEconomicCategorySelection
{
    public ConstructionEconomicCategoryProjection Catalog { get; }
    public string TargetCategory { get; }
    public ConstructionEconomicOperation Operation { get; }
    public ConstructionEconomicModifierFamily Family { get; }
    public IReadOnlyList<ConstructionEconomicCategory> Declarations { get; }

    internal ConstructionEconomicCategorySelection(ConstructionEconomicCategoryProjection catalog, string targetCategory, ConstructionEconomicOperation operation, ConstructionEconomicModifierFamily family, IEnumerable<ConstructionEconomicCategory> declarations)
    {
        Catalog = catalog;
        TargetCategory = targetCategory;
        Operation = operation;
        Family = family;
        Declarations = Array.AsReadOnly(declarations.ToArray());
    }
}

public sealed class ConstructionEconomicCategoryProjection
{
    internal static readonly IReadOnlyList<string> Seeds = Array.AsReadOnly(new[] { "planet_buildings", "planet_buildings_strongholds", "planet_districts_cities", "planet_districts_generator", "planet_districts_mining", "planet_districts_farming", "planet_zones" });
    public ConstructionCatalogProjection Source { get; }
    public IReadOnlyDictionary<string, ConstructionEconomicCategory> Categories { get; }

    internal ConstructionEconomicCategoryProjection(ConstructionCatalogProjection source)
    {
        Source = source;
        if (source.ProfileId != "construction-economic-categories-452-v1" || source.Definitions.Count > 32 || Seeds.Any(id => !source.Definitions.ContainsKey(id))) { throw new InvalidDataException("Invalid economic category source profile."); }
        var categories = source.Definitions.Values.ToDictionary(item => item.Id, item => new ConstructionEconomicCategory(item), StringComparer.Ordinal);
        var reached = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seed in Seeds)
        {
            var chain = new HashSet<string>(StringComparer.Ordinal);
            var current = seed;
            while (true)
            {
                if (!chain.Add(current) || !categories.TryGetValue(current, out var category)) { throw new InvalidDataException("Missing or cyclic economic category parent."); }
                reached.Add(current);
                if (category.Parent is null)
                {
                    if (current != "country") { throw new InvalidDataException("Disconnected economic category root."); }
                    break;
                }
                current = category.Parent;
            }
        }
        if (reached.Count != categories.Count) { throw new InvalidDataException("Unreached economic category definition."); }
        Categories = new ReadOnlyDictionary<string, ConstructionEconomicCategory>(categories);
    }

    public ConstructionEconomicCategorySelection Select(string targetCategory, ConstructionEconomicOperation operation, ConstructionEconomicModifierFamily family)
    {
        if (targetCategory is null || !Categories.TryGetValue(targetCategory, out var category) || !Enum.IsDefined(operation) || !Enum.IsDefined(family)) { throw new ArgumentException("Unknown economic category or modifier selection."); }
        var declarations = new List<ConstructionEconomicCategory>();
        while (true)
        {
            if ((family == ConstructionEconomicModifierFamily.Mult ? category.Mult : category.Add).Contains(operation)) { declarations.Add(category); }
            if (family == ConstructionEconomicModifierFamily.Add || category.Parent is null) { break; }
            category = Categories[category.Parent];
        }
        return new(this, targetCategory, operation, family, declarations);
    }

    internal static string? ReadParent(ConstructionPlacementDefinition source) => ReadScalar(source, "parent");

    internal static string? ReadScalar(ConstructionPlacementDefinition source, string key)
    {
        var fields = source.Fields.Where(field => field.Key == key).ToArray();
        if (fields.Length == 0) { return null; }
        if (fields.Length != 1 || fields[0].Operator != "=" || fields[0].Scalar is null || fields[0].Children.Count != 0 || fields[0].Scalar!.Length is 0 or > 128 || fields[0].Scalar!.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')) { throw new InvalidDataException("Invalid economic category scalar declaration."); }
        return fields[0].Scalar;
    }
}
