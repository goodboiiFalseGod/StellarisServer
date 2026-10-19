using Stellaris.Core.Content.Construction;

namespace Stellaris.Core.Construction;

public sealed record ConstructionResourceCategoryBranch(ConstructionRawContribution Source, string ResourceTablePath, ConstructionField CategorySource, ConstructionEconomicCategorySelection Mult, ConstructionEconomicCategorySelection Add);

public sealed class ConstructionResourceCategoryBinding
{
    public ConstructionCatalogAssessment Assessment { get; }
    public ConstructionEconomicCategoryProjection CategoryCatalog { get; }
    public IReadOnlyList<ConstructionResourceCategoryBranch> Branches { get; }
    public IReadOnlyList<ConstructionGate> BindingGates { get; }

    private ConstructionResourceCategoryBinding(ConstructionCatalogAssessment assessment, ConstructionEconomicCategoryProjection categoryCatalog, IEnumerable<ConstructionResourceCategoryBranch> branches, IEnumerable<ConstructionGate> gates)
    {
        Assessment = assessment;
        CategoryCatalog = categoryCatalog;
        Branches = Array.AsReadOnly(branches.ToArray());
        BindingGates = Array.AsReadOnly(gates.ToArray());
    }

    public static ConstructionResourceCategoryBinding Bind(ConstructionCatalogAssessment assessment, ConstructionEconomicCategoryProjection categoryCatalog)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(categoryCatalog);
        if (!SourcesMatch([assessment.Source, assessment.ConditionSource, categoryCatalog.Source])) { return Rejected("source-profile-mismatch"); }
        var definition = assessment.Source.Definitions[assessment.DefinitionId];
        if (definition.Kind is not ("building" or "district" or "zone")) { return Rejected("resource-definition-kind"); }
        var resources = definition.Fields.Select((field, index) => (field, index)).Where(pair => pair.field.Key == "resources").ToArray();
        if (resources.Length != 1 || resources[0].field.Operator != "=" || resources[0].field.Scalar is not null) { return Rejected("resource-table-shape"); }
        var categories = resources[0].field.Children.Where(field => field.Key == "category").ToArray();
        if (categories.Length != 1 || categories[0].Operator != "=" || categories[0].Scalar is null || categories[0].Children.Count != 0 || !categoryCatalog.Categories.ContainsKey(categories[0].Scalar!)) { return Rejected("resource-category-unresolved"); }
        var tablePath = "/" + resources[0].index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":resources";
        var branches = new List<ConstructionResourceCategoryBranch>();
        foreach (var contribution in assessment.Contributions.Where(item => item.Kind is "cost" or "upkeep"))
        {
            var operation = contribution.Kind == "cost" ? ConstructionEconomicOperation.Cost : ConstructionEconomicOperation.Upkeep;
            branches.Add(new(contribution, tablePath, categories[0], categoryCatalog.Select(categories[0].Scalar!, operation, ConstructionEconomicModifierFamily.Mult), categoryCatalog.Select(categories[0].Scalar!, operation, ConstructionEconomicModifierFamily.Add)));
        }
        return new(assessment, categoryCatalog, branches, []);

        ConstructionResourceCategoryBinding Rejected(string dependency) => new(assessment, categoryCatalog, [], new[] { new ConstructionGate("source-binding-unavailable", dependency) });
    }

    internal static bool SourcesMatch(IEnumerable<ConstructionCatalogProjection?> catalogs)
    {
        var files = new Dictionary<string, ConstructionFile>(StringComparer.Ordinal);
        foreach (var catalog in catalogs)
        {
            if (catalog is null) { continue; }
            foreach (var file in catalog.Files)
            {
                if (files.TryGetValue(file.RelativePath, out var previous) && (previous.ByteLength != file.ByteLength || previous.Sha256 != file.Sha256)) { return false; }
                files[file.RelativePath] = file;
            }
        }
        return true;
    }
}
