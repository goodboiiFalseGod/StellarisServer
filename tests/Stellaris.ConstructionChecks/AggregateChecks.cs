using Stellaris.Core.Construction;
using Stellaris.Core.Economy;

static class AggregateChecks
{
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task Transactions()
    {
        var (initial, world, country, first, second) = Fixture();
        var original = initial.EconomyHash();
        world.SetBalance(country, "energy", 1);
        var detached = initial.ForkEconomy();
        detached.SetBalance(country, "energy", 2);
        Check(initial.GetBalance(1, "energy") == 100 && initial.EconomyHash() == original, "Aggregate economy leaked to source or detached world.");
        var one = initial.Enqueue("one", 1, 0, "first", 0, "order", "slot", Quote(60, 2));
        Check(one.Accepted && one.State.GetBalance(1, "energy") == 40 && one.State.Ledgers["second"].Accounts[1]["energy"] == 40, "Shared account debit not reconciled.");
        var insufficient = one.State.Enqueue("two", 1, 1, "second", 0, "order", "slot", Quote(60, 3));
        Check(!insufficient.Accepted && insufficient.Code == "insufficient-resources" && ReferenceEquals(insufficient.State, one.State), "Stale second-colony account permitted double spend.");
        var refund = new ConstructionRefund("explicit-refund", new Dictionary<string, decimal> { ["energy"] = 60 }, new Dictionary<string, decimal> { ["energy"] = 1000 });
        var cancelled = one.State.Cancel("cancel", 1, 1, "first", 1, "order", ConstructionCancellationReason.Player, refund);
        Check(cancelled.Accepted && cancelled.State.GetBalance(1, "energy") == 100 && cancelled.State.Ledgers["second"].Accounts[1]["energy"] == 100, "Refund not reconciled across colonies.");
        var replay = cancelled.State.Enqueue("one", 1, 0, "first", 0, "order", "slot", Quote(60, 2));
        Check(replay.Accepted && replay.Code == "duplicate" && replay.AppliedRevision == 1 && ReferenceEquals(replay.State, cancelled.State) && replay.State.GetBalance(1, "energy") == 100, "Old retry charged again after refund/reconciliation.");
        Check(cancelled.State.Enqueue("one", 2, 0, "first", 0, "order", "slot", Quote(60, 2)).Code == "not-owner", "Retry bypassed ownership guard.");
        Check(cancelled.State.Enqueue("one", 1, 0, "first", 0, "different", "slot", Quote(60, 2)).Code == "command-id-conflict", "Conflicting receipt accepted.");
        Check(cancelled.State.Enqueue("stale", 1, 0, "second", 0, "order", "slot", Quote(1, 1)).Code == "stale-revision", "Stale aggregate accepted.");
        var seeded = initial.Enqueue("a", 1, 0, "first", 0, "order", "slot", Quote(10, 2)).State.Enqueue("b", 1, 1, "second", 0, "order", "slot", Quote(20, 3)).State;
        var hash = seeded.EconomyHash();
        ConstructionColonyWork[] plans = [new("first", 1, [new("order", 10)]), new("second", 1, [new("order", 10)])];
        var before = seeded.Advance("day", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, plans);
        var after = seeded.Advance("day", 2, "explicit-phase", ConstructionSettlementPhase.CompletionAfterSettlement, true, plans);
        Check(before.Accepted && after.Accepted && before.State.Month == 1 && after.State.Month == 1 && before.State.GetBalance(1, "energy") == 63 && after.State.GetBalance(1, "energy") == 68, "Multi-colony phase or single settlement incorrect.");
        Check(before.State.ForkEconomy().GetBuildingUpkeep(first)["energy"] == 3 && before.State.ForkEconomy().GetBuildingUpkeep(second)["energy"] == 4 && before.State.Ledgers.Values.All(ledger => ledger.Accounts[1]["energy"] == 63), "Completed upkeep or final accounts incorrect.");
        var dayReplay = before.State.Advance("day", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, plans);
        Check(dayReplay.Accepted && dayReplay.Code == "duplicate" && dayReplay.AppliedRevision == 3 && ReferenceEquals(dayReplay.State, before.State), "Day replay settled twice.");
        Check(before.State.Advance("day", 2, "explicit-phase", ConstructionSettlementPhase.CompletionAfterSettlement, true, plans).Code == "command-id-conflict", "Changed phase replay accepted.");
        Check(before.State.Enqueue("a", 1, 0, "first", 0, "order", "slot", Quote(10, 2)).Code == "duplicate", "Receipt lost after settlement/reconciliation.");
        var ledgerCommand = "aggregate-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("a")));
        var ledgerReplay = before.State.Ledgers["first"].Enqueue(ledgerCommand, 1, 0, "order", "slot", Quote(10, 2));
        Check(ledgerReplay.Code == "duplicate" && ledgerReplay.AppliedRevision == 1 && ledgerReplay.State.Accounts[1]["energy"] == 63, "Ledger receipt lost or restored stale balances during reconciliation.");
        var invalid = seeded.Advance("bad", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, [plans[0], new("second", 1, [new("order", 11)])]);
        Check(!invalid.Accepted && ReferenceEquals(invalid.State, seeded) && seeded.EconomyHash() == hash && seeded.Ledgers.Values.All(ledger => ledger.Installed.Count == 0), "Second-colony failure published first completion.");
        var deficit = initial.Enqueue("huge", 1, 0, "first", 0, "order", "slot", Quote(0, 1000)).State;
        var rejectedMonth = deficit.Advance("month", 1, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, [new("first", 1, [new("order", 10)])]);
        Check(!rejectedMonth.Accepted && ReferenceEquals(rejectedMonth.State, deficit) && deficit.Month == 0 && deficit.Ledgers["first"].Installed.Count == 0, "Monthly failure escaped aggregate rollback.");
        var overflow = initial.Enqueue("overflow", 1, 0, "first", 0, "order", "slot", Quote(0, decimal.MaxValue)).State;
        Check(overflow.Advance("overflow-day", 1, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, false, [new("first", 1, [new("order", 10)])]).Code == "arithmetic-overflow" && overflow.Ledgers["first"].Installed.Count == 0, "Aggregate overflow published completion.");
        var laterOverflow = initial.Enqueue("first-ok", 1, 0, "first", 0, "order", "slot", Quote(0, 2)).State.Enqueue("second-overflow", 1, 1, "second", 0, "order", "slot", Quote(0, decimal.MaxValue)).State;
        var overflowHash = laterOverflow.EconomyHash();
        var overflowDay = laterOverflow.Advance("later-overflow", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, false, plans);
        Check(!overflowDay.Accepted && overflowDay.Code == "arithmetic-overflow" && ReferenceEquals(overflowDay.State, laterOverflow) && laterOverflow.EconomyHash() == overflowHash && laterOverflow.Ledgers.Values.All(ledger => ledger.Installed.Count == 0), "Later arithmetic overflow published earlier colony completion.");
        Check(seeded.Advance("double", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, false, [plans[0], plans[0]]).Code == "invalid-progress", "Duplicate colony plan accepted.");
        Check(seeded.Advance("missing", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, [plans[0], new("unknown", 0, [new("order", 1)])]).Code == "unknown-colony" && seeded.EconomyHash() == hash, "Unknown later colony escaped rollback.");
        var settlement = seeded.Advance("only-month", 2, "explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, true, []);
        Check(settlement.Accepted && settlement.State.Month == 1 && settlement.State.GetBalance(1, "energy") == 68 && settlement.State.Ledgers.Values.All(ledger => ledger.Accounts[1]["energy"] == 68), "Settlement-only step did not reconcile.");
        Check(seeded.Advance("missing-policy", 2, null, ConstructionSettlementPhase.CompletionBeforeSettlement, false, plans).Code == "dependency-unresolved", "Default policy invented.");
        Check(initial.EconomyHash() == original && seeded.EconomyHash() == hash, "Aggregate inputs mutated.");
        return Task.CompletedTask;
    }

    public static Task Bindings()
    {
        var (_, world, country, first, second) = Fixture();
        var foreign = world.CreateCountry();
        var foreignPlanet = world.CreatePlanet(foreign);
        world.SetBalance(foreign, "energy", 200);
        var foreignLedger = new ConstructionLedger(new("foreign", ConstructionAuthorityMode.Hive, false, null, "system"), 2, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = _empty, [2] = _empty }, ["slot"]);
        var shared = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country, [2] = foreign }, [new("first", 1, first), new("foreign", 2, foreignPlanet)], [Ledger("first"), foreignLedger]);
        var charged = shared.Enqueue("foreign-build", 2, 0, "foreign", 0, "order", "slot", Quote(25, 2));
        Check(charged.Accepted && charged.State.GetBalance(2, "energy") == 175 && charged.State.GetBalance(1, "energy") == 100 && charged.State.Ledgers["foreign"].Accounts[1]["energy"] == 100, "Different-owner account was debited or overwritten.");
        foreach (var bindings in new[]
        {
            new[] { new ConstructionColonyBinding("first", 2, first), new("second", 1, second) },
            new[] { new ConstructionColonyBinding("first", 1, foreignPlanet), new("second", 1, second) },
            new[] { new ConstructionColonyBinding("first", 1, first), new("second", 1, first) },
            new[] { new ConstructionColonyBinding("first", 1, new EntityId(999, 1)), new("second", 1, second) }
        })
        {
            try { _ = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country, [2] = foreign }, bindings, [Ledger("first"), Ledger("second")]); }
            catch (ArgumentException) { continue; }
            throw new InvalidOperationException("Invalid owner/planet binding accepted.");
        }
        return Task.CompletedTask;
    }

    private static (ConstructionAggregate Aggregate, EconomyWorld World, EntityId Country, EntityId First, EntityId Second) Fixture()
    {
        var world = new EconomyWorld([new("energy", 1000)], [], []);
        var country = world.CreateCountry();
        var first = world.CreatePlanet(country);
        var second = world.CreatePlanet(country);
        world.SetBalance(country, "energy", 100);
        world.SetBuildingUpkeep(first, new Dictionary<string, decimal> { ["energy"] = 1 });
        world.SetBuildingUpkeep(second, new Dictionary<string, decimal> { ["energy"] = 1 });
        var aggregate = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country }, [new("first", 1, first), new("second", 1, second)], [Ledger("first"), Ledger("second")]);
        return (aggregate, world, country, first, second);
    }

    private static ConstructionLedger Ledger(string colony) => new(new(colony, ConstructionAuthorityMode.Individualist, false, null, "system"), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = _empty }, ["slot"]);
    private static ConstructionQuote Quote(decimal price, decimal upkeep) => new("synthetic", "explicit-quote", "building_hydroponics_farm", ConstructionKind.Building, 10, new Dictionary<string, decimal> { ["energy"] = price }, new Dictionary<string, decimal> { ["energy"] = upkeep }, _empty, []);
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
