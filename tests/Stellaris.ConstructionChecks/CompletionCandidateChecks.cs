using Stellaris.Core.Construction;
using Stellaris.Core.Economy;

static class CompletionCandidateChecks
{
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task Settlement()
    {
        var world = new EconomyWorld([new("energy", 1000), new("food", 1000)], [new("farmer", "worker", 100, new Dictionary<string, decimal> { ["food"] = 6 }, _empty)], [new("worker", null)]);
        var owner = world.CreateCountry();
        var planet = world.CreatePlanet(owner);
        world.SetBalance(owner, "energy", 100);
        world.AddGroup(planet, 100, 1);
        world.SetJobs(planet, "farmer", 1);
        world.SetBuildingUpkeep(planet, new Dictionary<string, decimal> { ["energy"] = 1 });
        var ledger = Queued("energy", 2);
        var original = world.StateHash();
        ConstructionEconomyTransition Stage(ConstructionSettlementPhase phase, EconomyWorld? source = null, ConstructionLedger? orders = null, string? policy = "synthetic-explicit-phase", EntityId? binding = null) => ConstructionEconomyCandidate.Stage(source ?? world, orders ?? ledger, binding ?? planet, "complete", 1, 1, policy, phase, true, [new("order", 10)]);
        var before = Stage(ConstructionSettlementPhase.CompletionBeforeSettlement);
        var after = Stage(ConstructionSettlementPhase.CompletionAfterSettlement);
        Check(before.Accepted && after.Accepted && before.Economy.GetBalance(owner, "energy") == 97 && after.Economy.GetBalance(owner, "energy") == 99, "Explicit completion phase not respected.");
        Check(before.Economy.GetBuildingUpkeep(planet)["energy"] == 3 && after.Economy.GetBuildingUpkeep(planet)["energy"] == 3 && world.GetBuildingUpkeep(planet)["energy"] == 1, "Infrastructure baseline overwritten or input mutated.");
        Check(before.Ledger.Installed["slot"].JobCapacity["farmer"] == 200 && before.Economy.GetPlanetReport(planet).Allocated["farmer"].Count == 1 && before.Economy.GetPlanetReport(planet).Produced["food"] == 6, "Capacity automatically employed or job/upkeep changed.");
        Check(world.StateHash() == original && ledger.Orders["order"].Status == ConstructionOrderStatus.Pending, "Fresh candidate mutated aggregate inputs.");
        var replay = Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, before.Economy, before.Ledger);
        Check(replay.Accepted && replay.Code == "duplicate" && ReferenceEquals(replay.Economy, before.Economy) && ReferenceEquals(replay.Ledger, before.Ledger) && replay.Economy.Month == 1 && replay.Economy.GetBuildingUpkeep(planet)["energy"] == 3, "Receipt replay applied completion/month twice.");
        var changed = Stage(ConstructionSettlementPhase.CompletionAfterSettlement, before.Economy, before.Ledger);
        Check(!changed.Accepted && changed.Code == "command-id-conflict", "Changed retry phase silently reused completion.");
        var changedFlag = ConstructionEconomyCandidate.Stage(before.Economy, before.Ledger, planet, "complete", 1, 1, "synthetic-explicit-phase", ConstructionSettlementPhase.CompletionBeforeSettlement, false, [new("order", 10)]);
        Check(!changedFlag.Accepted && changedFlag.Code == "command-id-conflict", "Changed retry monthly flag silently reused completion.");
        var changedPolicy = Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, before.Economy, before.Ledger, "other-policy");
        var changedPlanet = Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, before.Economy, before.Ledger, binding: new EntityId(999, 1));
        Check(changedPolicy.Code == "command-id-conflict" && changedPlanet.Code == "command-id-conflict", "Changed retry policy or planet silently reused completion.");
        var partial = ConstructionEconomyCandidate.Stage(world, ledger, planet, "partial", 1, 1, "synthetic", ConstructionSettlementPhase.CompletionBeforeSettlement, false, [new("order", 5)]);
        Check(partial.Accepted && partial.Ledger.Installed.Count == 0 && partial.Ledger.Orders["order"].CompletedWork == 5 && partial.Economy.Month == 0 && partial.Economy.GetBuildingUpkeep(planet)["energy"] == 1, "Partial progress installed effects or settled implicitly.");
        var overflow = Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, orders: Queued("energy", decimal.MaxValue));
        Check(!overflow.Accepted && overflow.Code == "arithmetic-overflow" && ReferenceEquals(overflow.Economy, world) && world.StateHash() == original, "Upkeep overflow escaped rollback.");
        var baseline = world.GetBuildingUpkeep(planet);
        Check(baseline is not IDictionary<string, decimal> mutable || mutable.IsReadOnly, "Upkeep accessor exposes mutable state.");
        foreach (var failed in new[]
        {
            Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, policy: null),
            Stage((ConstructionSettlementPhase)99),
            Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, binding: new EntityId(999, 1)),
            Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, orders: Queued("unknown_resource", 2)),
            Stage(ConstructionSettlementPhase.CompletionBeforeSettlement, orders: Queued("energy", 1000))
        })
        {
            Check(!failed.Accepted && ReferenceEquals(failed.Economy, world) && failed.Ledger.Orders["order"].Status == ConstructionOrderStatus.Pending && world.StateHash() == original, "Failed candidate escaped rollback.");
        }
        var invalidWork = ConstructionEconomyCandidate.Stage(world, ledger, planet, "overrun", 1, 1, "synthetic", ConstructionSettlementPhase.CompletionBeforeSettlement, false, [new("order", 11)]);
        Check(!invalidWork.Accepted && invalidWork.Code == "work-overrun-policy-unresolved" && ReferenceEquals(invalidWork.Ledger, ledger) && ReferenceEquals(invalidWork.Economy, world), "Progress rejection lost original references.");
        before.Economy.SetBalance(owner, "energy", 50);
        Check(world.StateHash() == original, "Published candidate still aliases original.");
        return Task.CompletedTask;
    }

    private static ConstructionLedger Queued(string upkeepResource, decimal upkeep)
    {
        var ledger = new ConstructionLedger(new("colony", ConstructionAuthorityMode.Individualist, false, null, "system"), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = _empty }, ["slot"]);
        var quote = new ConstructionQuote("synthetic-source", "synthetic-quote", "building_hydroponics_farm", ConstructionKind.Building, 10, _empty, new Dictionary<string, decimal> { [upkeepResource] = upkeep }, new Dictionary<string, decimal> { ["farmer"] = 200 }, []);
        var enqueue = ledger.Enqueue("enqueue", 1, 0, "order", "slot", quote);
        Check(enqueue.Accepted, "Synthetic trusted enqueue failed.");
        return enqueue.State;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
