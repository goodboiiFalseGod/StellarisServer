using Stellaris.Core.Construction;

internal readonly record struct CatalogConditionTransition(string Id, ConstructionTruth Before, ConstructionTruth After);

internal sealed record CatalogConditionTransitionSummary(
    IReadOnlyDictionary<ConstructionTruth, int> BeforeCounts,
    IReadOnlyDictionary<ConstructionTruth, int> AfterCounts,
    IReadOnlyList<CatalogConditionTransition> Changed,
    IReadOnlyList<CatalogConditionTransition> KnownLosses);

internal static class CatalogConditionTransitions
{
    public static bool TryReconcile(
        IEnumerable<string> expectedIds,
        IEnumerable<CatalogConditionTransition> transitions,
        IEnumerable<CatalogConditionTransition> expectedChanges,
        IReadOnlyDictionary<ConstructionTruth, int> expectedBeforeCounts,
        IReadOnlyDictionary<ConstructionTruth, int> expectedAfterCounts,
        out CatalogConditionTransitionSummary summary,
        out string error)
    {
        var ids = expectedIds.ToArray();
        var rows = transitions.ToArray();
        var duplicates = rows.GroupBy(row => row.Id, StringComparer.Ordinal).Where(group => group.Count() != 1)
            .Select(group => group.Key).Order(StringComparer.Ordinal).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || duplicates.Length > 0)
            return Fail($"duplicate IDs: {string.Join(",", duplicates)}", out summary, out error);

        var expected = ids.ToHashSet(StringComparer.Ordinal);
        var actual = rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var extra = actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
            return Fail($"catalog ID mismatch; missing=[{string.Join(",", missing)}], extra=[{string.Join(",", extra)}]", out summary, out error);

        var knownLosses = rows.Where(row => row.Before != ConstructionTruth.Unknown && row.Before != row.After)
            .OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
        if (knownLosses.Length > 0)
            return Fail($"known truth changed: {Format(knownLosses)}", out summary, out error);

        var changed = rows.Where(row => row.Before != row.After).OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
        var expectedChangeRows = expectedChanges.OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
        if (!changed.SequenceEqual(expectedChangeRows))
            return Fail($"changed IDs mismatch; actual=[{Format(changed)}], expected=[{Format(expectedChangeRows)}]", out summary, out error);

        var beforeCounts = Counts(rows.Select(row => row.Before));
        var afterCounts = Counts(rows.Select(row => row.After));
        if (!SameCounts(beforeCounts, expectedBeforeCounts) || !SameCounts(afterCounts, expectedAfterCounts))
            return Fail($"truth totals mismatch; before=[{Format(beforeCounts)}], after=[{Format(afterCounts)}]", out summary, out error);

        summary = new(beforeCounts, afterCounts, changed, knownLosses);
        error = string.Empty;
        return true;
    }

    public static void VerifyAccountingChecks(Action<bool, string> check)
    {
        var initial = new Dictionary<ConstructionTruth, int>
        {
            [ConstructionTruth.False] = 1,
            [ConstructionTruth.Unknown] = 1,
            [ConstructionTruth.True] = 0
        };
        var resolved = new Dictionary<ConstructionTruth, int>
        {
            [ConstructionTruth.False] = 1,
            [ConstructionTruth.Unknown] = 0,
            [ConstructionTruth.True] = 1
        };
        var expectedIds = new[] { "alpha", "beta" };
        var expectedChanges = new[] { new CatalogConditionTransition("beta", ConstructionTruth.Unknown, ConstructionTruth.True) };
        bool Accept(IEnumerable<string> ids, IEnumerable<CatalogConditionTransition> rows,
            IEnumerable<CatalogConditionTransition> changed, IReadOnlyDictionary<ConstructionTruth, int> before,
            IReadOnlyDictionary<ConstructionTruth, int> after)
            => TryReconcile(ids, rows, changed, before, after, out _, out _);
        bool Reject(IEnumerable<string> ids, IEnumerable<CatalogConditionTransition> rows,
            IEnumerable<CatalogConditionTransition> changed, IReadOnlyDictionary<ConstructionTruth, int> before,
            IReadOnlyDictionary<ConstructionTruth, int> after, string expectedError)
        {
            var accepted = TryReconcile(ids, rows, changed, before, after, out _, out var error);
            return !accepted && error.StartsWith(expectedError, StringComparison.Ordinal);
        }

        var valid = new[]
        {
            new CatalogConditionTransition("alpha", ConstructionTruth.False, ConstructionTruth.False),
            new CatalogConditionTransition("beta", ConstructionTruth.Unknown, ConstructionTruth.True)
        };
        check(Accept(expectedIds, valid, expectedChanges, initial, resolved), "pure accounting accepts exact rows and reconciled truth totals");
        check(Reject(expectedIds, valid, [], initial, resolved, "changed IDs mismatch"), "pure accounting rejects unexpected changed IDs");
        check(Reject(expectedIds, [.. valid, new("unexpected", ConstructionTruth.False, ConstructionTruth.False)],
                expectedChanges, initial, resolved, "catalog ID mismatch"), "pure accounting rejects IDs outside the catalog");
        check(Reject(expectedIds,
                [new("alpha", ConstructionTruth.False, ConstructionTruth.Unknown), new("beta", ConstructionTruth.Unknown, ConstructionTruth.True)],
                [new("alpha", ConstructionTruth.False, ConstructionTruth.Unknown), .. expectedChanges],
                new Dictionary<ConstructionTruth, int> { [ConstructionTruth.False] = 1, [ConstructionTruth.Unknown] = 1, [ConstructionTruth.True] = 0 },
                new Dictionary<ConstructionTruth, int> { [ConstructionTruth.False] = 0, [ConstructionTruth.Unknown] = 1, [ConstructionTruth.True] = 1 },
                "known truth changed"),
            "pure accounting rejects known-truth losses");
        check(Reject(expectedIds, [valid[0]], [],
                new Dictionary<ConstructionTruth, int> { [ConstructionTruth.False] = 1, [ConstructionTruth.Unknown] = 0, [ConstructionTruth.True] = 0 },
                new Dictionary<ConstructionTruth, int> { [ConstructionTruth.False] = 1, [ConstructionTruth.Unknown] = 0, [ConstructionTruth.True] = 0 },
                "catalog ID mismatch"), "pure accounting rejects missing catalog rows");
        check(Reject(expectedIds, [.. valid, valid[0]], expectedChanges, initial, resolved, "duplicate IDs"),
            "pure accounting rejects duplicate catalog rows");
        check(Reject(expectedIds, valid, expectedChanges, initial,
                new Dictionary<ConstructionTruth, int> { [ConstructionTruth.False] = 0, [ConstructionTruth.Unknown] = 0, [ConstructionTruth.True] = 2 },
                "truth totals mismatch"),
            "pure accounting rejects inconsistent aggregate totals");
    }

    private static bool Fail(string message, out CatalogConditionTransitionSummary summary, out string error)
    {
        summary = new(new Dictionary<ConstructionTruth, int>(), new Dictionary<ConstructionTruth, int>(), [], []);
        error = message;
        return false;
    }

    private static Dictionary<ConstructionTruth, int> Counts(IEnumerable<ConstructionTruth> truths)
        => Enum.GetValues<ConstructionTruth>().ToDictionary(truth => truth,
            truth => truths.Count(item => item == truth));

    private static bool SameCounts(IReadOnlyDictionary<ConstructionTruth, int> actual,
        IReadOnlyDictionary<ConstructionTruth, int> expected)
        => Enum.GetValues<ConstructionTruth>().All(truth => actual.GetValueOrDefault(truth) == expected.GetValueOrDefault(truth));

    private static string Format(IEnumerable<CatalogConditionTransition> rows)
        => string.Join(",", rows.Select(row => $"{row.Id}:{row.Before}->{row.After}"));

    private static string Format(IReadOnlyDictionary<ConstructionTruth, int> counts)
        => string.Join(",", Enum.GetValues<ConstructionTruth>().Select(truth => $"{truth}:{counts.GetValueOrDefault(truth)}"));
}
