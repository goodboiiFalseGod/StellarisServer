using Stellaris.Core.Construction;
using Stellaris.Core.Economy;

static class QueueConstructionChecks
{
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task Lifecycle()
    {
        var state = Fixture();
        Check(state.Ledgers["c"].Queues is null, "Unbound queue silently acquired native topology.");
        var ids = new List<string> { "x", "y", "z" };
        var snapshot = new ConstructionQueueSnapshot(3, [new("pool", "synthetic-binding", 1, ids)]);
        ids.Reverse();
        state = Configure(state, "bind", snapshot);
        Check(state.Ledgers["c"].Queues!.Queues[0].OrderIds.SequenceEqual(new[] { "x", "y", "z" }) && state.Ledgers["c"].Queues!.Revision == 4, "Snapshot mutated or lost revision binding.");
        state = Step(state, "work", [new("x", 3), new("y", 3), new("z", 3)]);
        Check(Work(state, "x") == 3 && Work(state, "y") == 3 && Work(state, "z") == 0, "Capacity did not constrain actual daily work.");
        state = Configure(state, "drop", Snapshot(state, 0, "x", "y", "z"));
        state = Step(state, "after-drop", [new("x", 1), new("y", 2), new("z", 2)]);
        Check(Work(state, "x") == 4 && Work(state, "y") == 3 && Work(state, "z") == 0, "Capacity drop lost progress or advanced excess orders.");
        state = Configure(state, "reorder", Snapshot(state, 0, "z", "x", "y"));
        state = Step(state, "after-reorder", [new("x", 1), new("z", 2)]);
        Check(Work(state, "x") == 4 && Work(state, "y") == 3 && Work(state, "z") == 2, "Reorder did not change active membership while preserving work.");
        var pausedBase = state;
        var pausedRevision = state.Ledgers["c"].Revision;
        var paused = state.AdvanceDay("paused", state.Revision, false, [new("c", pausedRevision, [new("x", 2)])]);
        Check(paused.Accepted && paused.State.Ledgers["c"].Revision == pausedRevision && Work(paused.State, "x") == 4, "Paused-only plan advanced work/revision.");
        Check(paused.State.AdvanceDay("paused", pausedBase.Revision, false, [new("c", pausedRevision, [new("x", 2)])]).Code == "duplicate", "Paused-only receipt was lost.");
        Rejected(paused.State.AdvanceDay("paused", pausedBase.Revision, false, [new("c", pausedRevision, [new("x", 3)])]), paused.State, "command-id-conflict");
        Rejected(state.AdvanceDay("paused-stale", state.Revision, false, [new("c", pausedRevision - 1, [new("x", 1)])]), state, "stale-revision");
        Rejected(state.AdvanceDay("paused-invalid", state.Revision, false, [new("c", pausedRevision, [new("x", 0)])]), state, "invalid-progress");
        state = Step(state, "finish-z", [new("z", decimal.MaxValue), new("x", decimal.MaxValue)]);
        Check(Work(state, "z") == 10 && Work(state, "x") == 4 && state.Ledgers["c"].Queues!.Queues[0].OrderIds.SequenceEqual(new[] { "x", "y" }), "Completed slot recycled inside day or terminal membership retained.");
        state = Step(state, "later-x", [new("x", 1), new("y", 1)]);
        Check(Work(state, "x") == 5 && Work(state, "y") == 3, "Next-day active selection failed.");
        var cancelled = state.CancelPaid("cancel-y", 1, state.Revision, "c", state.Ledgers["c"].Revision, "y");
        Check(cancelled.Accepted && cancelled.State.Ledgers["c"].Queues!.Queues[0].OrderIds.SequenceEqual(new[] { "x" }), "Cancellation did not filter terminal membership.");
        var enqueued = cancelled.State.Enqueue("new-order", 1, cancelled.State.Revision, "c", cancelled.State.Ledgers["c"].Revision, "w", "w", Quote());
        Check(enqueued.Accepted && enqueued.State.Ledgers["c"].Queues is null, "Enqueue guessed a destination queue.");

        var pools = Fixture();
        pools = Configure(pools, "two-pools", new(3, [new("one", "synthetic", 0, ["x", "y"]), new("two", "synthetic", 0, ["z"])]));
        var two = Step(pools, "two-work", [new("x", 1), new("y", 1), new("z", 1)]);
        Check(Work(two, "x") == 1 && Work(two, "y") == 0 && Work(two, "z") == 1, "Distinct explicit pools were silently merged.");
        var hash = pools.EconomyHash();
        Rejected(pools.AdvanceDay("later-invalid", pools.Revision, true, [new("c", pools.Ledgers["c"].Revision, [new("x", 1)]), new("unknown", 0, [new("x", 1)])]), pools, "unknown-colony");
        Check(pools.EconomyHash() == hash && Work(pools, "x") == 0 && pools.Month == 0, "Later invalid plan published work/month.");
        var unbound = Fixture();
        Rejected(unbound.ConfigureQueues("foreign", 2, unbound.Revision, "c", 3, Snapshot(unbound, 0, "x", "y", "z")), unbound, "not-owner");
        Rejected(unbound.ConfigureQueues("missing", 1, unbound.Revision, "c", 3, Snapshot(unbound, 0, "x", "y")), unbound, "invalid-queue-membership");
        Rejected(unbound.ConfigureQueues("foreign-order", 1, unbound.Revision, "c", 3, Snapshot(unbound, 0, "x", "y", "foreign")), unbound, "invalid-queue-membership");
        Rejected(unbound.ConfigureQueues("stale-snapshot", 1, unbound.Revision, "c", 3, new(2, [new("pool", "synthetic", 0, ["x", "y", "z"])])), unbound, "stale-queue-revision");
        Rejected(unbound.ConfigureQueues("null", 1, unbound.Revision, "c", 3, null), unbound, "dependency-unresolved");
        var bound = Configure(unbound, "receipt", Snapshot(unbound, 0, "x", "y", "z"));
        Check(bound.ConfigureQueues("receipt", 1, unbound.Revision, "c", 3, Snapshot(unbound, 0, "x", "y", "z")).Code == "duplicate", "Configuration retry failed.");
        Rejected(bound.ConfigureQueues("receipt", 1, unbound.Revision, "c", 3, Snapshot(unbound, 0, "z", "y", "x")), bound, "command-id-conflict");
        var ledger = bound.Ledgers["c"];
        var refunds = ledger.Orders.Keys.ToDictionary(id => id, id => new ConstructionRefund("synthetic-transfer", _empty, _empty), StringComparer.Ordinal);
        var changed = ledger.ChangeOwner("owner", 1, ledger.Revision, 2, refunds);
        Check(changed.Accepted && changed.State.Queues is null && changed.State.Orders.Values.All(order => order.Status == ConstructionOrderStatus.Cancelled), "Ownership transfer retained old queue binding.");
        Invalid(() => new ConstructionQueue("p", "s", -1, []));
        Invalid(() => new ConstructionQueue("p", "s", 4096, []));
        Invalid(() => new ConstructionQueue("p", "s", 0, ["x", "x"]));
        Invalid(() => new ConstructionQueueSnapshot(0, [new("a", "s", 0, ["x"]), new("b", "s", 0, ["x"])]));
        Invalid(() => new ConstructionQueueSnapshot(0, [new("a", "s", 0, []), new("a", "s", 0, [])]));
        Invalid(() => new ConstructionQueue("", "s", 0, []));
        return Task.CompletedTask;
    }

    private static ConstructionAggregate Fixture()
    {
        var world = new EconomyWorld([new("energy", 100)], [], []);
        var country = world.CreateCountry();
        var other = world.CreateCountry();
        var planet = world.CreatePlanet(country);
        var ledger = new ConstructionLedger(new("c", ConstructionAuthorityMode.Individualist, false, null, null), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = _empty, [2] = _empty }, ["x", "y", "z", "w"]);
        var state = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country, [2] = other }, [new("c", 1, planet)], [ledger]);
        foreach (var id in new[] { "x", "y", "z" })
        {
            var result = state.Enqueue(id, 1, state.Revision, "c", state.Ledgers["c"].Revision, id, id, Quote());
            Check(result.Accepted, result.Code);
            state = result.State;
        }
        return state;
    }

    private static ConstructionQuote Quote() => new("synthetic", "fixed", "building", ConstructionKind.Building, 10, _empty, _empty, _empty, []);
    private static decimal Work(ConstructionAggregate state, string id) => state.Ledgers["c"].Orders[id].CompletedWork;
    private static ConstructionQueueSnapshot Snapshot(ConstructionAggregate state, int additional, params string[] ids) => new(state.Ledgers["c"].Revision, [new("pool", "synthetic-binding", additional, ids)]);
    private static ConstructionAggregate Configure(ConstructionAggregate state, string id, ConstructionQueueSnapshot snapshot)
    {
        var result = state.ConfigureQueues(id, 1, state.Revision, "c", state.Ledgers["c"].Revision, snapshot);
        Check(result.Accepted, result.Code);
        return result.State;
    }
    private static ConstructionAggregate Step(ConstructionAggregate state, string id, IReadOnlyList<ConstructionWork> work)
    {
        var result = state.AdvanceDay(id, state.Revision, false, [new("c", state.Ledgers["c"].Revision, work)]);
        Check(result.Accepted, result.Code);
        return result.State;
    }
    private static void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid queue shape accepted.");
    }
    private static void Rejected(ConstructionAggregateTransition result, ConstructionAggregate original, string code) => Check(!result.Accepted && result.Code == code && ReferenceEquals(result.State, original), "Expected atomic " + code + ", got " + result.Code);
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
