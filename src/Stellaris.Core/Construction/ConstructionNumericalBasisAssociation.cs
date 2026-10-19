using Stellaris.Core.Content.Construction;

namespace Stellaris.Core.Construction;

public sealed record ConstructionCostModifierCandidate(ConstructionResourceCategoryBranch Branch, ConstructionAmount CivicOperand, ConstructionEconomicCategory Declaration);

public sealed class ConstructionNumericalBasisAssociation
{
    public ConstructionResourceCategoryBinding Resources { get; }
    public ConstructionFunctionalCivicCollection Civic { get; }
    public ConstructionOrdinaryTemplate Template { get; }
    public IReadOnlyList<ConstructionCostModifierCandidate> CostCandidates { get; }
    public IReadOnlyList<ConstructionAmount> UnassociatedOperands { get; }
    public IReadOnlyList<ConstructionGate> AssociationGates { get; }

    private ConstructionNumericalBasisAssociation(ConstructionResourceCategoryBinding resources, ConstructionFunctionalCivicCollection civic, ConstructionOrdinaryTemplate template, IEnumerable<ConstructionCostModifierCandidate> candidates, IEnumerable<ConstructionAmount> unassociated, IEnumerable<ConstructionGate> gates)
    {
        Resources = resources;
        Civic = civic;
        Template = template;
        CostCandidates = Array.AsReadOnly(candidates.ToArray());
        UnassociatedOperands = Array.AsReadOnly(unassociated.ToArray());
        AssociationGates = Array.AsReadOnly(gates.ToArray());
    }

    public static ConstructionNumericalBasisAssociation Bind(ConstructionResourceCategoryBinding resources, ConstructionFunctionalCivicCollection civic, string templateId)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(civic);
        var template = ConstructionOrdinaryTemplate.Get(templateId);
        if (!ReferenceEquals(template, civic.Template)) { return Rejected("template-snapshot-mismatch"); }
        if (!ReferenceEquals(resources.Assessment.Context, civic.Context)) { return Rejected("context-snapshot-mismatch"); }
        if (!ConstructionResourceCategoryBinding.SourcesMatch([resources.Assessment.Source, resources.Assessment.ConditionSource, resources.CategoryCatalog.Source, civic.Source])) { return Rejected("source-profile-mismatch"); }
        if (resources.BindingGates.Count != 0) { return Rejected("resource-binding-unavailable"); }
        var costs = civic.ConstructionOperands.Where(operand => operand.Resource == "planet_structures_cost_mult").ToArray();
        var speeds = civic.ConstructionOperands.Where(operand => operand.Resource == "planet_building_build_speed_mult").ToArray();
        if (costs.Length != 1 || speeds.Length != 1 || civic.ConstructionOperands.Count != 2) { return Rejected("civic-operands-unavailable"); }
        var candidates = new List<ConstructionCostModifierCandidate>();
        foreach (var branch in resources.Branches.Where(branch => branch.Source.Kind == "cost"))
        {
            var declarations = branch.Mult.Declarations.Where(declaration => declaration.Source.Id == "planet_structures" && declaration.Mult.Contains(ConstructionEconomicOperation.Cost)).ToArray();
            if (declarations.Length != 1) { return Rejected("civic-cost-declaration-unresolved"); }
            candidates.Add(new(branch, costs[0], declarations[0]));
        }
        if (candidates.Count == 0) { return Rejected("cost-branch-unavailable"); }
        return new(resources, civic, template, candidates, speeds, new[] { new ConstructionGate("dependency-unresolved", "building-speed-target-and-time-rule") });

        ConstructionNumericalBasisAssociation Rejected(string dependency) => new(resources, civic, template, [], [], new[] { new ConstructionGate("basis-association-unavailable", dependency) });
    }
}
