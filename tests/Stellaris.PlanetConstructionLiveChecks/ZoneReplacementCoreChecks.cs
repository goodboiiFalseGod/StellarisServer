using Stellaris.Core.Construction;

internal static class ZoneReplacementCoreChecks
{
    public static void Run()
    {
        var (initial, bank) = CreateFixture();
        var replacementPairs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["building-slot-factory"] = "building_factory",
        };
        var replacement = new ConstructionZoneReplacement("zone_industrial", "zone-occurrence-a", replacementPairs);
        replacementPairs["building-slot-foundry"] = "building_foundry";
        Check(replacement.RemovedBuildings.Count == 1 && !replacement.RemovedBuildings.ContainsKey("building-slot-foundry"),
            "Replacement payload does not retain a mutable caller dictionary");

        var zoneQuote = Quote("zone_foundry", ConstructionKind.Zone, 1m, 40m);
        var enqueued = initial.Enqueue("replace-zone", 1, initial.Revision, "zone-order", "zone-slot-a", zoneQuote, bank,
            requestIdentity: "zone-replacement-request", zoneReplacement: replacement);
        Check(enqueued.Accepted && enqueued.Code == "accepted" && enqueued.ResourceDeltas.Single().Amount == -40m,
            "Compatible replacement is enqueued and paid");
        var pending = enqueued.State;
        Check(pending.Revision == initial.Revision + 1 && pending.Orders["zone-order"].Status == ConstructionOrderStatus.Pending
            && pending.Orders["zone-order"].CompletedWork == 0m && pending.PendingOrderIds.SequenceEqual(["zone-order"]),
            "Replacement begins pending without early work");
        Check(pending.Installed["zone-slot-a"].DefinitionId == "zone_industrial"
            && pending.Installed["building-slot-factory"].DefinitionId == "building_factory"
            && pending.Installed["building-slot-foundry"].DefinitionId == "building_foundry"
            && pending.Slots["building-slot-foundry"].ZoneDefinitionId == "zone_industrial",
            "Old zone graph remains installed until completion");

        var childQuote = Quote("building_new", ConstructionKind.Building, 1m, 0m);
        var childEnqueue = pending.Enqueue("child-enqueue", 1, pending.Revision, "child-order", "building-slot-foundry", childQuote, bank);
        Check(!childEnqueue.Accepted && childEnqueue.Code == "zone-order-cancel-required"
            && ReferenceEquals(childEnqueue.State, pending) && childEnqueue.ResourceDeltas.Count == 0,
            "Parent replacement blocks child enqueue without mutation");
        var childUpgrade = pending.Enqueue("child-upgrade", 1, pending.Revision, "child-upgrade-order", "building-slot-foundry",
            childQuote, bank, upgradeFrom: "building_foundry", upgradeZoneInstanceId: "zone-occurrence-a");
        Check(!childUpgrade.Accepted && childUpgrade.Code == "zone-order-cancel-required"
            && ReferenceEquals(childUpgrade.State, pending), "Parent replacement blocks child upgrade");
        var childDemolition = pending.DemolishBuilding("child-demolition", 1, pending.Revision,
            "building-slot-foundry", "building_foundry");
        Check(!childDemolition.Accepted && childDemolition.Code == "zone-order-cancel-required"
            && ReferenceEquals(childDemolition.State, pending) && childDemolition.ResourceDeltas.Count == 0,
            "Parent replacement blocks child demolition");

        var toggled = pending.SetBuildingEnabled("child-toggle", 1, pending.Revision,
            "building-slot-foundry", "building_foundry", true);
        Check(toggled.Accepted && toggled.State.Installed["building-slot-foundry"].Enabled
            && toggled.State.Orders["zone-order"].Status == ConstructionOrderStatus.Pending,
            "Toggle remains available inside pending replacement");
        var unrelatedDemolition = toggled.State.DemolishBuilding("unrelated-demolition", 1, toggled.State.Revision,
            "building-slot-other", "building_other");
        Check(unrelatedDemolition.Accepted && !unrelatedDemolition.State.Installed.ContainsKey("building-slot-other")
            && unrelatedDemolition.State.Installed.ContainsKey("building-slot-factory"),
            "Unrelated zone demolition remains available");

        var paidBank = Apply(bank, enqueued.ResourceDeltas);
        var cancelled = pending.Cancel("cancel-zone", 1, pending.Revision, "zone-order", paidBank);
        Check(cancelled.Accepted && cancelled.ResourceDeltas.Single() is { PayerId: 1, ResourceId: "minerals", Amount: 40m },
            "Cancellation refunds the original payer exactly the paid amount");
        Check(cancelled.State.Installed["zone-slot-a"].DefinitionId == "zone_industrial"
            && cancelled.State.Installed.ContainsKey("building-slot-factory")
            && cancelled.State.Installed["building-slot-foundry"] == pending.Installed["building-slot-foundry"]
            && cancelled.State.Slots["building-slot-foundry"].ZoneDefinitionId == "zone_industrial"
            && cancelled.State.Orders["zone-order"].Status == ConstructionOrderStatus.Cancelled,
            "Cancellation retains original graph and bindings");
        var cancelReplay = cancelled.State.Cancel("cancel-zone", 1, pending.Revision, "zone-order", paidBank);
        Check(cancelReplay.Accepted && cancelReplay.Code == "duplicate" && cancelReplay.ResourceDeltas.Count == 0,
            "Cancellation replay cannot refund twice");
        var enqueueReplay = cancelled.State.Enqueue("replace-zone", 1, initial.Revision, "zone-order", "zone-slot-a", zoneQuote,
            bank, requestIdentity: "zone-replacement-request", zoneReplacement: replacement);
        Check(enqueueReplay.Accepted && enqueueReplay.Code == "duplicate" && enqueueReplay.ResourceDeltas.Count == 0,
            "Original enqueue receipt replays as duplicate after cancellation");

        Check(initial.Enqueue("wrong-source", 1, initial.Revision, "wrong-source-order", "zone-slot-a", zoneQuote, bank,
                zoneReplacement: new ConstructionZoneReplacement("zone_city", "zone-occurrence-a", replacement.RemovedBuildings)).Code == "zone-replacement-source-mismatch",
            "Source definition mismatch is rejected");
        Check(initial.Enqueue("wrong-removed", 1, initial.Revision, "wrong-removed-order", "zone-slot-a", zoneQuote, bank,
                zoneReplacement: new ConstructionZoneReplacement("zone_industrial", "zone-occurrence-a",
                    new Dictionary<string, string> { ["building-slot-factory"] = "building_other" })).Code == "zone-replacement-source-mismatch",
            "Removed slot/definition pair must match the exact installed graph");
        Check(initial.Enqueue("stale-zone", 1, initial.Revision + 1, "stale-zone-order", "zone-slot-a", zoneQuote, bank,
                zoneReplacement: replacement).Code == "stale-revision", "Stale replacement revision is rejected");
        Check(initial.Enqueue("foreign-zone", 2, initial.Revision, "foreign-zone-order", "zone-slot-a", zoneQuote, bank,
                zoneReplacement: replacement).Code == "not-owner", "Foreign replacement actor is rejected");

        var completed = pending.AdvanceDay("complete-zone", pending.Revision, 1,
            new Dictionary<string, decimal?> { ["zone-order"] = 0m },
            new Dictionary<string, bool?> { ["zone-order"] = true }, paidBank);
        Check(completed.Accepted && completed.Code == "accepted" && completed.ResourceDeltas.Count == 0
            && completed.State.Orders["zone-order"].Status == ConstructionOrderStatus.Completed
            && completed.State.Orders["zone-order"].CompletedWork == zoneQuote.RequiredWork,
            "Completion settles the zone order without another refund");
        var graph = completed.State;
        Check(graph.Installed["zone-slot-a"].DefinitionId == "zone_foundry"
            && !graph.Installed.ContainsKey("building-slot-factory")
            && graph.Installed["building-slot-foundry"].DefinitionId == "building_foundry"
            && !graph.Installed["building-slot-foundry"].Enabled
            && graph.Installed["building-slot-keep"].DefinitionId == "building_keep",
            "Completion changes the zone, removes only approved building, and preserves compatible/disabled instances");
        Check(new[] { "building-slot-factory", "building-slot-foundry", "building-slot-keep" }
                .All(id => graph.Slots[id].ZoneDefinitionId == "zone_foundry")
            && graph.Slots["building-slot-other"].ZoneDefinitionId == "zone_city"
            && graph.Installed["zone-slot-other"].DefinitionId == "zone_city"
            && graph.Installed["building-slot-other"].DefinitionId == "building_other",
            "Completion atomically updates only all child bindings in the target occurrence");

        Console.WriteLine("PASS Core zone replacement: immutable exact payload, scoped locks/toggle, cancel/refund/replay, atomic completion and preserved child instances");
    }

    private static (ConstructionLiveRuntime State, ConstructionBankSnapshot Bank) CreateFixture()
    {
        var slots = new[]
        {
            new ConstructionLiveSlot("zone-slot-a", ConstructionKind.Zone, "zone-occurrence-a", "zone_industrial", "district-a", "zone_slot_01"),
            new ConstructionLiveSlot("building-slot-factory", ConstructionKind.Building, "zone-occurrence-a", "zone_industrial"),
            new ConstructionLiveSlot("building-slot-foundry", ConstructionKind.Building, "zone-occurrence-a", "zone_industrial"),
            new ConstructionLiveSlot("building-slot-keep", ConstructionKind.Building, "zone-occurrence-a", "zone_industrial"),
            new ConstructionLiveSlot("zone-slot-other", ConstructionKind.Zone, "zone-occurrence-b", "zone_city", "district-a", "zone_slot_02"),
            new ConstructionLiveSlot("building-slot-other", ConstructionKind.Building, "zone-occurrence-b", "zone_city"),
        };
        var installed = new[]
        {
            new ConstructionInstalled("zone-slot-a", "zone_industrial", ConstructionKind.Zone, Empty, Empty),
            new ConstructionInstalled("building-slot-factory", "building_factory", ConstructionKind.Building, Empty, Empty),
            new ConstructionInstalled("building-slot-foundry", "building_foundry", ConstructionKind.Building, Empty, Empty, Enabled: false),
            new ConstructionInstalled("building-slot-keep", "building_keep", ConstructionKind.Building, Empty, Empty),
            new ConstructionInstalled("zone-slot-other", "zone_city", ConstructionKind.Zone, Empty, Empty),
            new ConstructionInstalled("building-slot-other", "building_other", ConstructionKind.Building, Empty, Empty),
        };
        return (new ConstructionLiveRuntime("zone-replacement-colony", 1, slots, installed), Bank(100m));
    }

    private static IReadOnlyDictionary<string, decimal> Empty { get; } = new Dictionary<string, decimal>();

    private static ConstructionQuote Quote(string definitionId, ConstructionKind kind, decimal work, decimal minerals)
        => new("fixture-profile", "fixture-policy", definitionId, kind, work,
            new Dictionary<string, decimal> { ["minerals"] = minerals }, Empty, Empty, []);

    private static ConstructionBankSnapshot Bank(decimal minerals)
        => new(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [1] = new Dictionary<string, decimal> { ["minerals"] = minerals },
        }, new Dictionary<string, ConstructionStorage> { ["minerals"] = new(false, 100m) });

    private static ConstructionBankSnapshot Apply(ConstructionBankSnapshot bank, IReadOnlyList<ConstructionResourceDelta> deltas)
    {
        var accounts = bank.Accounts.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
        foreach (var delta in deltas)
        {
            var account = (Dictionary<string, decimal>)accounts[delta.PayerId];
            account[delta.ResourceId] = checked(account.GetValueOrDefault(delta.ResourceId) + delta.Amount);
        }
        return new(accounts, bank.Storage);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
