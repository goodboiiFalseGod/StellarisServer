using Stellaris.Core.Construction;

internal static class CalculationFailureChecks
{
    public static void Run()
    {
        var slots = new[] { "a", "b", "c" }.Select(id => new ConstructionLiveSlot(id, ConstructionKind.Building, "zone", "zone"));
        var state = new ConstructionLiveRuntime("calculation-failure", 1, slots, []);
        var bank = Bank(100m);
        var quote = new ConstructionQuote("fixture", "fixture", "building", ConstructionKind.Building, 10m,
            new Dictionary<string, decimal> { ["minerals"] = 20m }, new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), []);
        foreach (var id in new[] { "a", "b", "c" })
        {
            var accepted = state.Enqueue("enqueue-" + id, 1, state.Revision, id, id, quote, bank);
            Check(accepted.Accepted, "Fixture enqueue");
            state = accepted.State;
        }
        var speed = new Dictionary<string, decimal?> { ["a"] = decimal.MaxValue, ["b"] = 0m, ["c"] = null };
        var conditions = new Dictionary<string, bool?> { ["a"] = null, ["b"] = true, ["c"] = true };
        foreach (var capacity in new[] { 0, 2 })
        {
            var result = state.AdvanceDay("day", state.Revision, capacity, speed, conditions, Bank(95m));
            Check(result.Accepted && result.State.Orders["a"] is { Status: ConstructionOrderStatus.Cancelled, CancellationReason: ConstructionCancellationReason.CalculationFailed, CompletedWork: 0 },
                "Известный overflow не отменил точный заказ при N=" + capacity);
            Check(result.ResourceDeltas.Single() is { PayerId: 1, ResourceId: "minerals", Amount: 5m }, "Capped refund");
            Check(result.State.Orders["b"].CompletedWork == (capacity == 0 ? 0 : 1) && result.State.Orders["c"].CompletedWork == 0
                && result.State.Installed.Count == 0 && result.State.PendingOrderIds.SequenceEqual(["b", "c"]), "Изменена чужая работа/active boundary/installed");
            var replay = result.State.AdvanceDay("day", state.Revision, capacity, speed, conditions, Bank(100m));
            Check(replay.Accepted && replay.Code == "duplicate" && replay.ResourceDeltas.Count == 0, "Повторный credit");
        }
        var refresh = state.RefreshPendingConditions(speed, conditions, Bank(95m));
        Check(refresh.Accepted && refresh.State.Revision == state.Revision && refresh.State.Orders["b"].CompletedWork == 0
            && refresh.State.Orders["a"].CancellationReason == ConstructionCancellationReason.CalculationFailed, "Refresh начислил работу/изменил revision");
        var unknown = state.AdvanceDay("unknown", state.Revision, 2, speed, withUnknown("b"), Bank(95m));
        Check(!unknown.Accepted && ReferenceEquals(unknown.State, state) && unknown.ResourceDeltas.Count == 0, "Чужой Unknown не откатил candidate");
        var missingBank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [2] = new Dictionary<string, decimal>() }, bank.Storage);
        var missing = state.RefreshPendingConditions(speed, conditions, missingBank);
        Check(!missing.Accepted && ReferenceEquals(missing.State, state) && missing.ResourceDeltas.Count == 0, "Account hole выдан за refund");
        var waitingSpeed = new Dictionary<string, decimal?> { ["a"] = 0m, ["b"] = 0m, ["c"] = decimal.MaxValue };
        var waitingConditions = new Dictionary<string, bool?> { ["a"] = true, ["b"] = true, ["c"] = null };
        var waiting = state.AdvanceDay("waiting", state.Revision, 1, waitingSpeed, waitingConditions, Bank(95m));
        Check(waiting.Accepted && waiting.State.Orders["c"].CancellationReason == ConstructionCancellationReason.CalculationFailed
            && waiting.State.Orders["a"].CompletedWork == 1m && waiting.State.Orders["b"].CompletedWork == 0m, "Waiting overflow заблокировал работающий head");
        speed["a"] = decimal.MaxValue - 1; conditions["a"] = true;
        var representable = state.AdvanceDay("max-factor", state.Revision, 1, speed, conditions, bank);
        Check(representable.Accepted && representable.State.Orders["a"].Status == ConstructionOrderStatus.Completed
            && representable.State.Orders["a"].CompletedWork == 10m, "Представимый Max factor не ограничен оставшейся работой");
        Console.WriteLine("PASS Q04 calculation-failed: active/waiting/N0, exact cancellation/refund/replay, Unknown/bank rollback, representable maximum");

        Dictionary<string, bool?> withUnknown(string id) => conditions.ToDictionary(pair => pair.Key, pair => pair.Key == id ? null : pair.Value);
    }

    private static ConstructionBankSnapshot Bank(decimal minerals) => new(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        { [1] = new Dictionary<string, decimal> { ["minerals"] = minerals } }, new Dictionary<string, ConstructionStorage> { ["minerals"] = new(false, 100m) });

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
