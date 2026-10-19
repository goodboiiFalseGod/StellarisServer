namespace Stellaris.Core.Content.Construction;

public sealed record ConstructionOrigin(string Source, int Line, int Column);
public sealed record ConstructionAmount(string Resource, decimal Value, ConstructionOrigin Origin);
public sealed record ConstructionFile(string RelativePath, long ByteLength, string Sha256);
public sealed record ConstructionCoverage(string Field, string Status, string Reason, ConstructionOrigin? Origin);

public sealed class ConstructionField
{
    public string Key { get; }
    public string Operator { get; }
    public string? Scalar { get; }
    public IReadOnlyList<ConstructionField> Children { get; }
    public ConstructionOrigin Origin { get; }
    public IReadOnlyList<ConstructionOrigin> InlineCallSites { get; }

    internal ConstructionField(string key, string operation, string? scalar, IEnumerable<ConstructionField> children, ConstructionOrigin origin, IEnumerable<ConstructionOrigin> inlineCallSites)
    {
        Key = key;
        Operator = operation;
        Scalar = scalar;
        Children = Array.AsReadOnly(children.ToArray());
        Origin = origin;
        InlineCallSites = Array.AsReadOnly(inlineCallSites.ToArray());
    }
}

public sealed class ConstructionCost
{
    public IReadOnlyList<ConstructionAmount> Amounts { get; }
    public ConstructionField? Trigger { get; }
    public decimal? RawMultiplier { get; }

    internal ConstructionCost(IEnumerable<ConstructionAmount> amounts, ConstructionField? trigger, decimal? rawMultiplier)
    {
        Amounts = Array.AsReadOnly(amounts.ToArray());
        Trigger = trigger;
        RawMultiplier = rawMultiplier;
    }
}

public sealed class ConstructionDefinition
{
    public string Id { get; }
    public ConstructionOrigin Origin { get; }
    public decimal BaseBuildtime { get; }
    public string Category { get; }
    public string EconomicCategory { get; }
    public IReadOnlyList<string> BuildingSets { get; }
    public IReadOnlyList<string> Prerequisites { get; }
    public IReadOnlyList<ConstructionAmount> Upkeep { get; }
    public IReadOnlyList<ConstructionCost> Costs { get; }
    public IReadOnlyList<ConstructionField> Fields { get; }

    internal ConstructionDefinition(string id, ConstructionOrigin origin, decimal baseBuildtime, string category, string economicCategory, IEnumerable<string> buildingSets, IEnumerable<string> prerequisites, IEnumerable<ConstructionAmount> upkeep, IEnumerable<ConstructionCost> costs, IEnumerable<ConstructionField> fields)
    {
        Id = id;
        Origin = origin;
        BaseBuildtime = baseBuildtime;
        Category = category;
        EconomicCategory = economicCategory;
        BuildingSets = Array.AsReadOnly(buildingSets.ToArray());
        Prerequisites = Array.AsReadOnly(prerequisites.ToArray());
        Upkeep = Array.AsReadOnly(upkeep.ToArray());
        Costs = Array.AsReadOnly(costs.ToArray());
        Fields = Array.AsReadOnly(fields.ToArray());
    }
}

public sealed class ConstructionProjection
{
    public string GameVersion => "4.5.2";
    public string ProfileId => "construction-hydroponics-452-v1";
    public ConstructionDefinition Definition { get; }
    public IReadOnlyList<ConstructionFile> Files { get; }
    public IReadOnlyList<ConstructionCoverage> Coverage { get; }

    internal ConstructionProjection(ConstructionDefinition definition, IEnumerable<ConstructionFile> files, IEnumerable<ConstructionCoverage> coverage)
    {
        Definition = definition;
        Files = Array.AsReadOnly(files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
        Coverage = Array.AsReadOnly(coverage.ToArray());
    }
}

public sealed class ConstructionImportException : Exception
{
    public ConstructionImportException(string message) : base(message)
    {
    }
}
