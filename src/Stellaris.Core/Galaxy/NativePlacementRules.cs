namespace Stellaris.Core.Galaxy;

/// <summary>Scalar collision и acceptance clamp, без allocation, RNG и relaxation.</summary>
public static class NativePlacementRules
{
    public const long CoordinateBound = 50000000;
    public const int RetryBudget = 10000;

    public static bool HasCollision(NativeFixedPoint rawCandidate, ReadOnlySpan<NativeFixedPoint> acceptedStored,
        long minimumDistance)
    {
        if (minimumDistance < 0) throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        var threshold = NativeFixedMath.Multiply(minimumDistance, minimumDistance);
        foreach (var point in acceptedStored)
        {
            var dx = checked(rawCandidate.X - point.X);
            var dy = checked(rawCandidate.Y - point.Y);
            if (dx > -100000 && dx < 100000 && dy > -100000 && dy < 100000)
            {
                if (minimumDistance >= 1000000) return true;
                var scaledX = NativeFixedMath.Multiply(dx, 10000000);
                var scaledY = NativeFixedMath.Multiply(dy, 10000000);
                var scaledMinimum = checked(minimumDistance * 100);
                var scaledThreshold = NativeFixedMath.Multiply(scaledMinimum, scaledMinimum);
                var scaledDistance = checked(NativeFixedMath.Multiply(scaledX, scaledX) + NativeFixedMath.Multiply(scaledY, scaledY));
                if (scaledDistance < scaledThreshold) return true;
                continue;
            }
            var distance = checked(NativeFixedMath.Multiply(dx, dx) + NativeFixedMath.Multiply(dy, dy));
            if (distance < threshold) return true;
        }
        return false;
    }

    public static NativeFixedPoint ClampAccepted(NativeFixedPoint point) =>
        new(Math.Clamp(point.X, -CoordinateBound, CoordinateBound), Math.Clamp(point.Y, -CoordinateBound, CoordinateBound));
}

public enum PlacementJournalStatus { TargetReached, InputExhausted, RetryBudgetExhausted }
public readonly record struct AcceptedCoordinate(PreRelaxationCandidate RawCandidate, NativeFixedPoint StoredPosition);
public sealed record PlacementJournal(IReadOnlyList<AcceptedCoordinate> Accepted, PlacementJournalStatus Status,
    long EvaluatedCandidates, long RejectedCandidates, int RemainingRetryBudget);
public sealed record SeededPlacementJournal(PlacementJournal Journal, NativeRandomState NextState, long Rotation);

/// <summary>Supplied-candidate pre-relaxation journal; не заменяет native system allocation.</summary>
public static class PreRelaxationPlacementJournal
{
    public static PlacementJournal Replay(ReadOnlySpan<PreRelaxationCandidate> candidates, int targetStars, long minimumDistance)
    {
        var snapshot = candidates.ToArray();
        var position = 0;
        return Run(_ => position < snapshot.Length ? snapshot[position++] : null, targetStars, minimumDistance);
    }

    public static SeededPlacementJournal GenerateJournal(GalaxySetupPlan plan, int specifiedSeed)
    {
        var shape = Stellaris.Core.Content.Galaxy.GalaxyShapeProjection.Read(plan);
        var rotation = PreRelaxationCandidates.InitializeRotation(shape, NativeRandom.FromSpecifiedSeed(specifiedSeed));
        var state = rotation.NextState;
        var journal = Run(index =>
        {
            var next = PreRelaxationCandidates.Next(shape, index, rotation.Angle, state);
            state = next.NextState;
            return next.Candidate;
        }, plan.TargetStars, shape.MinimumDistance);
        return new(journal, state, rotation.Angle);
    }

    private static PlacementJournal Run(Func<int, PreRelaxationCandidate?> next, int targetStars, long minimumDistance)
    {
        if (targetStars < 0) throw new ArgumentOutOfRangeException(nameof(targetStars));
        if (minimumDistance < 0) throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        var accepted = new List<AcceptedCoordinate>();
        var stored = new List<NativeFixedPoint>();
        var budget = NativePlacementRules.RetryBudget;
        long evaluated = 0;
        long rejected = 0;
        while (accepted.Count < targetStars)
        {
            var supplied = next(accepted.Count);
            if (supplied is not { } candidate) break;
            evaluated++;
            var rawPosition = new NativeFixedPoint(candidate.X, candidate.Y);
            if (NativePlacementRules.HasCollision(rawPosition, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(stored), minimumDistance))
            {
                rejected++;
                if (budget < 1)
                    return new(accepted.AsReadOnly(), PlacementJournalStatus.RetryBudgetExhausted, evaluated, rejected, budget);
                budget--;
                continue;
            }
            var clamped = NativePlacementRules.ClampAccepted(rawPosition);
            accepted.Add(new(candidate, clamped));
            stored.Add(clamped);
            budget = NativePlacementRules.RetryBudget;
        }
        return new(accepted.AsReadOnly(), accepted.Count == targetStars
            ? PlacementJournalStatus.TargetReached : PlacementJournalStatus.InputExhausted, evaluated, rejected, budget);
    }
}
