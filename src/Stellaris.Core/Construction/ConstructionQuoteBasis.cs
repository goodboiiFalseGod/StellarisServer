using System.Numerics;
using Stellaris.Core.Content.Construction;

namespace Stellaris.Core.Construction;

public sealed record ConstructionQuoteBasis(decimal RawBuildtime, IReadOnlyDictionary<string, decimal> RawCosts, IReadOnlyList<ConstructionAmount> RawInfrastructureUpkeep, IReadOnlyList<ConstructionAssessedJobs> ActiveJobContributions);
public sealed record ConstructionQuoteBasisResult(ConstructionQuoteBasis? Basis, string Code);
/// <summary>Trusted resolved applicability for this exact source/context snapshot; null fraction is unknown.</summary>
public sealed record ConstructionResolvedSpeed(ConstructionProjection Source, ConstructionContext Context, decimal? Fraction);
/// <summary>Exact nominal duration is NumeratorDays / Denominator, without division or day quantization.</summary>
public sealed record ConstructionNominalQuoteBasis(ConstructionProjection Source, ConstructionContext Context, ConstructionQuoteBasis Basis, decimal NumeratorDays, decimal Denominator);
public sealed record ConstructionNominalQuoteBasisResult(ConstructionNominalQuoteBasis? Nominal, string Code);

public static class ConstructionQuoteBasisEvaluator
{
    public static ConstructionNominalQuoteBasisResult Evaluate(ConstructionProjection content, ConstructionAssessment assessment, ConstructionResolvedSpeed? speed)
    {
        var raw = Evaluate(content, assessment);
        if (raw.Basis is null) { return new(null, raw.Code); }
        if (speed is null) { return new(null, "nominal-speed-unavailable"); }
        if (!ReferenceEquals(speed.Source, content)) { return new(null, "speed-source-snapshot-mismatch"); }
        if (!ReferenceEquals(speed.Context, assessment.Context)) { return new(null, "speed-context-snapshot-mismatch"); }
        if (speed.Fraction is not { } fraction) { return new(null, "nominal-speed-unavailable"); }
        if (fraction <= -1) { return new(null, "nonpositive-speed-factor"); }
        try
        {
            return new(new(content, assessment.Context, raw.Basis, raw.Basis.RawBuildtime, SpeedFactorExactly(fraction)), "nominal-ratio-only");
        }
        catch (OverflowException) { return new(null, "speed-factor-representation-unavailable"); }
    }

    public static ConstructionQuoteBasisResult Evaluate(ConstructionProjection content, ConstructionAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(assessment);
        if (assessment.DefinitionId != content.Definition.Id) { return new(null, "definition-mismatch"); }
        if (!ReferenceEquals(assessment.Source, content)) { return new(null, "source-snapshot-mismatch"); }
        if (assessment.Potential.Truth == ConstructionTruth.False || assessment.Prerequisites.Truth == ConstructionTruth.False) { return new(null, "not-eligible"); }
        if (assessment.Potential.Truth == ConstructionTruth.Unknown || assessment.Prerequisites.Truth == ConstructionTruth.Unknown || assessment.Costs.Any(cost => cost.Condition.Truth == ConstructionTruth.Unknown) || assessment.Jobs.Any(job => job.Condition.Truth == ConstructionTruth.Unknown)) { return new(null, "quote-basis-context-unresolved"); }
        var active = assessment.Costs.Where(cost => cost.Condition.Truth == ConstructionTruth.True).ToArray();
        if (active.Length != 1) { return new(null, "cost-branch-combination-unresolved"); }
        var branch = active[0];
        if (branch.RawMultiplier < 0 || branch.RawAmounts.Any(amount => amount.Value < 0) || content.Definition.BaseBuildtime <= 0 || content.Definition.Upkeep.Any(amount => amount.Value < 0)) { return new(null, "invalid-raw-quote-basis"); }
        try
        {
            var amounts = branch.RawAmounts.ToDictionary(amount => amount.Resource, amount => checked(amount.Value * (branch.RawMultiplier ?? 1)), StringComparer.Ordinal);
            return new(new(content.Definition.BaseBuildtime, ConstructionValues.Amounts(amounts), content.Definition.Upkeep, Array.AsReadOnly(assessment.Jobs.Where(job => job.Condition.Truth == ConstructionTruth.True).ToArray())), "raw-basis-only");
        }
        catch (OverflowException) { return new(null, "arithmetic-overflow"); }
    }

    private static decimal SpeedFactorExactly(decimal fraction)
    {
        var bits = decimal.GetBits(fraction);
        var scale = (bits[3] >> 16) & 255;
        var coefficient = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        if (bits[3] < 0) { coefficient = -coefficient; }
        coefficient += BigInteger.Pow(10, scale);
        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }
        if (coefficient > (BigInteger.One << 96) - 1) { throw new OverflowException("Exact speed factor exceeds decimal representation."); }
        return new(unchecked((int)(uint)(coefficient & uint.MaxValue)), unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)), unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue)), false, (byte)scale);
    }
}
