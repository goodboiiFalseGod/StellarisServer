using Stellaris.Core.Construction;
using Stellaris.Core.Economy;

static class DailyConstructionChecks
{
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task Advance()
    {
        var initial = Fixture(100);
        var hash = initial.EconomyHash();
        ConstructionColonyWork[] plans = [new("a", 2, [new("first", decimal.MaxValue)]), new("b", 1, [new("first", 10)])];
        var day = initial.AdvanceDay("boundary", 3, true, plans);
        Check(day.Accepted && day.State.Month == 1 && day.State.GetBalance(1, "energy") == 93, "Completions did not participate in one boundary settlement.");
        Check(day.State.Ledgers["a"].Orders["first"].CompletedWork == 10 && day.State.Ledgers["a"].Orders["first"].Status == ConstructionOrderStatus.Completed && day.State.Ledgers["a"].Orders["next"].CompletedWork == 0, "Oversized work carried to another order or overflowed.");
        Check(day.State.Ledgers["b"].Accounts[1]["energy"] == 93 && initial.EconomyHash() == hash && initial.Ledgers["a"].Installed.Count == 0, "Candidate isolation or shared account reconciliation failed.");
        var replay = day.State.AdvanceDay("boundary", 3, true, plans);
        Check(replay.Accepted && replay.Code == "duplicate" && ReferenceEquals(replay.State, day.State), "Daily retry was not idempotent.");
        Rejected(day.State.AdvanceDay("boundary", 3, true, [new("a", 2, [new("first", 11)]), plans[1]]), day.State, "command-id-conflict");
        Rejected(day.State.AdvanceDay("boundary", 3, false, plans), day.State, "command-id-conflict");
        var next = day.State.AdvanceDay("next-day", 4, false, [new("a", 3, [new("next", 1)])]);
        Check(next.Accepted && next.State.Month == 1 && next.State.Ledgers["a"].Orders["next"].CompletedWork == 1, "Daily excess persisted into a later day.");
        Rejected(initial.AdvanceDay("later-invalid", 3, true, [plans[0], new("b", 1, [new("missing", 1)])]), initial, "invalid-progress");
        Rejected(initial.AdvanceDay("unknown", 3, true, [plans[0], new("unknown", 0, [new("first", 1)])]), initial, "unknown-colony");
        Rejected(initial.AdvanceDay("stale-colony", 3, true, [plans[0], new("b", 0, [new("first", 1)])]), initial, "stale-revision");
        Rejected(initial.AdvanceDay("duplicate-order", 3, false, [new("a", 2, [new("first", 1), new("first", 2)])]), initial, "invalid-progress");
        Rejected(initial.AdvanceDay("duplicate-colony", 3, false, [plans[0], plans[0]]), initial, "invalid-progress");
        foreach (var amount in new[] { 0m, -1m })
        {
            Rejected(initial.AdvanceDay("nonpositive", 3, false, [new("a", 2, [new("first", amount)])]), initial, "invalid-progress");
        }
        Rejected(day.State.AdvanceDay("completed", 4, false, [new("a", 3, [new("first", 1)])]), day.State, "invalid-progress");
        Rejected(initial.AdvanceDay("stale", 2, false, plans), initial, "stale-revision");
        Rejected(initial.Advance("strict", 3, "synthetic", ConstructionSettlementPhase.CompletionBeforeSettlement, false, [new("a", 2, [new("first", 11)])]), initial, "work-overrun-policy-unresolved");
        var partial = initial.AdvanceDay("partial", 3, false, [new("a", 2, [new("first", 3)])]);
        Check(partial.Accepted && partial.State.Ledgers["a"].Orders["first"].CompletedWork == 3, "Partial daily work was not preserved.");
        var finished = partial.State.AdvanceDay("finish", 4, false, [new("a", 3, [new("first", decimal.MaxValue)])]);
        Check(finished.Accepted && finished.State.Ledgers["a"].Orders["first"].CompletedWork == 10 && finished.State.Ledgers["a"].Orders["next"].CompletedWork == 0, "Remaining-work clamp failed after partial progress.");
        var noWork = initial.AdvanceDay("no-work", 3, false, []);
        Check(noWork.Accepted && noWork.State.Month == 0 && noWork.State.EconomyHash() == hash, "No-work day changed economy/month.");
        var monthOnly = initial.AdvanceDay("month-only", 3, true, []);
        Check(monthOnly.Accepted && monthOnly.State.Month == 1 && monthOnly.State.GetBalance(1, "energy") == 98, "Settlement-only day failed.");
        var deficit = Fixture(5);
        Rejected(deficit.AdvanceDay("deficit", 3, true, plans), deficit, "economy-settlement-rejected");
        Check(deficit.Month == 0 && deficit.GetBalance(1, "energy") == 5 && deficit.Ledgers.Values.All(ledger => ledger.Installed.Count == 0), "Settlement failure published completion/month.");
        return Task.CompletedTask;
    }

    private static ConstructionAggregate Fixture(decimal balance)
    {
        var world = new EconomyWorld([new("energy", 100)], [], []);
        var country = world.CreateCountry();
        var a = world.CreatePlanet(country);
        var b = world.CreatePlanet(country);
        world.SetBalance(country, "energy", balance);
        world.SetBuildingUpkeep(a, new Dictionary<string, decimal> { ["energy"] = 1 });
        world.SetBuildingUpkeep(b, new Dictionary<string, decimal> { ["energy"] = 1 });
        var aggregate = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country }, [new("a", 1, a), new("b", 1, b)], [Ledger("a"), Ledger("b")]);
        return aggregate.Enqueue("a-first", 1, 0, "a", 0, "first", "slot", Quote(2)).State.Enqueue("a-next", 1, 1, "a", 1, "next", "next", Quote(4)).State.Enqueue("b-first", 1, 2, "b", 0, "first", "slot", Quote(3)).State;
    }

    private static ConstructionLedger Ledger(string colony) => new(new(colony, ConstructionAuthorityMode.Individualist, false, null, null), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = _empty }, ["slot", "next"]);
    private static ConstructionQuote Quote(decimal upkeep) => new("synthetic", "fixed", "building", ConstructionKind.Building, 10, _empty, new Dictionary<string, decimal> { ["energy"] = upkeep }, _empty, []);
    private static void Rejected(ConstructionAggregateTransition result, ConstructionAggregate original, string code) => Check(!result.Accepted && result.Code == code && ReferenceEquals(result.State, original), "Expected atomic " + code + ", got " + result.Code);
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
