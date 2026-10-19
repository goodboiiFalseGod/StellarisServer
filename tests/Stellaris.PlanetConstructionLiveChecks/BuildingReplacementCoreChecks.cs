using Stellaris.Core.Construction;

internal static class BuildingReplacementCoreChecks
{
    public static void Run()
    {
        var (initial, bank, replacementQuote, zoneQuote, replacementPayload) = CreateFixture();
        var enqueued = initial.Enqueue("replace-building", 1, initial.Revision, "building-order", "building-slot-a",
            replacementQuote, bank, requestIdentity: "building-replacement-request", buildingReplacement: replacementPayload);
        Check(enqueued.Accepted && enqueued.ResourceDeltas.Single() is { PayerId: 1, ResourceId: "minerals", Amount: -40m },
            "Trusted exact same-slot replacement is enqueued and paid");
        var pending = enqueued.State;
        var old = initial.Installed["building-slot-a"];
        Check(pending.Orders["building-order"].Status == ConstructionOrderStatus.Pending
            && pending.Orders["building-order"].CompletedWork == 0m && pending.PendingOrderIds.SequenceEqual(["building-order"])
            && pending.Installed["building-slot-a"] == old && !pending.Installed.ContainsKey("building-new-slot"),
            "Enqueue preserves the old disabled building graph and has no early target effects");

        var paidBank = Apply(bank, enqueued.ResourceDeltas);
        var firstDay = pending.AdvanceDay("building-day-1", pending.Revision, 1,
            new Dictionary<string, decimal?> { ["building-order"] = 0m },
            new Dictionary<string, bool?> { ["building-order"] = true }, paidBank);
        Check(firstDay.Accepted && firstDay.State.Orders["building-order"].CompletedWork == 1m
            && firstDay.State.Orders["building-order"].Status == ConstructionOrderStatus.Pending
            && firstDay.State.Installed["building-slot-a"].DefinitionId == "building_old"
            && !firstDay.State.Installed["building-slot-a"].Enabled
            && !firstDay.State.Installed.ContainsKey("building-new-slot"),
            "Partial work leaves the old disabled instance installed");

        var enabled = firstDay.State.SetBuildingEnabled("toggle-on", 1, firstDay.State.Revision,
            "building-slot-a", "building_old", true);
        Check(enabled.Accepted && enabled.State.Installed["building-slot-a"].Enabled
            && enabled.State.Orders["building-order"].Status == ConstructionOrderStatus.Pending,
            "Pending replacement allows toggle on");
        var disabled = enabled.State.SetBuildingEnabled("toggle-off", 1, enabled.State.Revision,
            "building-slot-a", "building_old", false);
        Check(disabled.Accepted && !disabled.State.Installed["building-slot-a"].Enabled
            && disabled.State.Installed["building-slot-a"].DefinitionId == "building_old"
            && disabled.State.Orders["building-order"].CompletedWork == 1m,
            "Pending replacement allows toggle off without resetting work or replacing early");

        var demolition = disabled.State.DemolishBuilding("same-slot-demolition", 1, disabled.State.Revision,
            "building-slot-a", "building_old");
        Check(!demolition.Accepted && demolition.Code == "building-order-cancel-required"
            && ReferenceEquals(demolition.State, disabled.State) && demolition.ResourceDeltas.Count == 0,
            "Same-slot demolition is blocked while replacement is pending");
        var secondReplacement = disabled.State.Enqueue("second-replacement", 1, disabled.State.Revision, "second-order",
            "building-slot-a", replacementQuote, paidBank, buildingReplacement: replacementPayload);
        Check(!secondReplacement.Accepted && secondReplacement.Code == "slot-unavailable"
            && ReferenceEquals(secondReplacement.State, disabled.State) && secondReplacement.ResourceDeltas.Count == 0,
            "Second same-slot replacement is blocked while first remains pending");
        var upgrade = disabled.State.Enqueue("same-slot-upgrade", 1, disabled.State.Revision, "upgrade-order",
            "building-slot-a", replacementQuote, paidBank, upgradeFrom: "building_old", upgradeZoneInstanceId: "zone-a");
        Check(!upgrade.Accepted && upgrade.Code == "slot-unavailable" && ReferenceEquals(upgrade.State, disabled.State),
            "Same-slot upgrade is blocked while replacement is pending");

        var firstDayBank = paidBank;
        var secondDay = disabled.State.AdvanceDay("building-day-2", disabled.State.Revision, 1,
            new Dictionary<string, decimal?> { ["building-order"] = 0m },
            new Dictionary<string, bool?> { ["building-order"] = true }, firstDayBank);
        Check(secondDay.Accepted && secondDay.ResourceDeltas.Count == 0
            && secondDay.State.Orders["building-order"].Status == ConstructionOrderStatus.Completed
            && secondDay.State.Orders["building-order"].CompletedWork == 2m,
            "Completion settles exactly the required work without a refund");
        Check(secondDay.State.Installed["building-slot-a"] is { DefinitionId: "building_new", Kind: ConstructionKind.Building, Enabled: true }
            && secondDay.State.Installed["building-slot-a"].InfrastructureUpkeep["energy"] == 4m
            && secondDay.State.Installed["building-slot-a"].JobCapacity["artisan"] == 30m
            && secondDay.State.Installed["building-slot-other"] == initial.Installed["building-slot-other"],
            "Completion installs the enabled target in the exact slot and leaves unrelated installation unchanged");

        var cancelledEnqueue = initial.Enqueue("cancel-replacement", 1, initial.Revision, "cancel-order", "building-slot-a",
            replacementQuote, bank, requestIdentity: "cancel-replacement-request", buildingReplacement: replacementPayload);
        var cancelPaid = Apply(bank, cancelledEnqueue.ResourceDeltas);
        var cancelToggleOn = cancelledEnqueue.State.SetBuildingEnabled("cancel-toggle-on", 1, cancelledEnqueue.State.Revision,
            "building-slot-a", "building_old", true);
        var cancelToggleOff = cancelToggleOn.State.SetBuildingEnabled("cancel-toggle-off", 1, cancelToggleOn.State.Revision,
            "building-slot-a", "building_old", false);
        var cancelled = cancelToggleOff.State.Cancel("cancel-building-replacement", 1, cancelToggleOff.State.Revision,
            "cancel-order", cancelPaid);
        Check(cancelled.Accepted && cancelled.ResourceDeltas.Single() is { PayerId: 1, ResourceId: "minerals", Amount: 40m }
            && cancelled.State.Installed["building-slot-a"] == old
            && cancelled.State.Orders["cancel-order"].Status == ConstructionOrderStatus.Cancelled,
            "Cancel refunds the paid amount and retains the original disabled instance state");
        var cancelReplay = cancelled.State.Cancel("cancel-building-replacement", 1, cancelToggleOff.State.Revision, "cancel-order", cancelPaid);
        Check(cancelReplay.Accepted && cancelReplay.Code == "duplicate" && cancelReplay.ResourceDeltas.Count == 0,
            "Cancel replay cannot refund twice");
        var enqueueReplay = cancelled.State.Enqueue("cancel-replacement", 1, initial.Revision, "cancel-order", "building-slot-a",
            replacementQuote, bank, requestIdentity: "cancel-replacement-request", buildingReplacement: replacementPayload);
        Check(enqueueReplay.Accepted && enqueueReplay.Code == "duplicate" && enqueueReplay.ResourceDeltas.Count == 0,
            "Original replacement enqueue replays as duplicate after cancel");

        Check(initial.Enqueue("wrong-source", 1, initial.Revision, "wrong-source-order", "building-slot-a", replacementQuote, bank,
                buildingReplacement: new("building_other", "zone-a")).Code == "building-replacement-source-mismatch",
            "Source definition mismatch is rejected");
        Check(initial.Enqueue("wrong-zone", 1, initial.Revision, "wrong-zone-order", "building-slot-a", replacementQuote, bank,
                buildingReplacement: new("building_old", "zone-other")).Code == "building-replacement-source-mismatch",
            "Zone occurrence mismatch is rejected");
        Check(initial.Enqueue("self-target", 1, initial.Revision, "self-target-order", "building-slot-a",
                Quote("building_old", ConstructionKind.Building, 2m, 40m), bank, buildingReplacement: replacementPayload).Code == "building-replacement-source-mismatch",
            "Replacement cannot target its own source definition");
        Check(initial.Enqueue("stale-replacement", 1, initial.Revision + 1, "stale-order", "building-slot-a", replacementQuote, bank,
                buildingReplacement: replacementPayload).Code == "stale-revision", "Stale revision is rejected");
        Check(initial.Enqueue("foreign-replacement", 2, initial.Revision, "foreign-order", "building-slot-a", replacementQuote, bank,
                buildingReplacement: replacementPayload).Code == "not-owner", "Foreign actor is rejected");
        Check(initial.Enqueue("conflicting-replacement", 1, initial.Revision, "conflicting-order", "building-slot-a", replacementQuote, bank,
                zoneReplacement: new("zone_old", "zone-a", new Dictionary<string, string>()), buildingReplacement: replacementPayload).Code == "building-replacement-source-mismatch",
            "Conflicting zone and building replacement payloads are rejected");

        var (zoneInitial, zoneBank, _, parentZoneQuote, _) = CreateFixture();
        var pendingZone = zoneInitial.Enqueue("parent-zone", 1, zoneInitial.Revision, "parent-zone-order", "zone-slot-a", parentZoneQuote, zoneBank,
            zoneReplacement: new("zone_old", "zone-a", new Dictionary<string, string>())).State;
        var childBlocked = pendingZone.Enqueue("parent-block-child", 1, pendingZone.Revision, "child-replacement-order", "building-slot-a",
            replacementQuote, zoneBank, buildingReplacement: replacementPayload);
        Check(!childBlocked.Accepted && childBlocked.Code == "zone-order-cancel-required"
            && ReferenceEquals(childBlocked.State, pendingZone),
            "Pending parent zone replacement blocks building replacement in its occurrence");

        Console.WriteLine("PASS Core building replacement: exact source/slot/zone, old-instance continuity and toggles, scoped locks, cancellation/refund/replay, enabled completion and zone-parent lock");
    }

    private static (ConstructionLiveRuntime State, ConstructionBankSnapshot Bank, ConstructionQuote ReplacementQuote,
        ConstructionQuote ZoneQuote, ConstructionBuildingReplacement Replacement) CreateFixture()
    {
        var slots = new[]
        {
            new ConstructionLiveSlot("zone-slot-a", ConstructionKind.Zone, "zone-a", "zone_old", "district-a", "zone-slot-def"),
            new ConstructionLiveSlot("building-slot-a", ConstructionKind.Building, "zone-a", "zone_old"),
            new ConstructionLiveSlot("zone-slot-other", ConstructionKind.Zone, "zone-other", "zone_other", "district-b", "zone-slot-def"),
            new ConstructionLiveSlot("building-slot-other", ConstructionKind.Building, "zone-other", "zone_other"),
        };
        var installed = new[]
        {
            new ConstructionInstalled("zone-slot-a", "zone_old", ConstructionKind.Zone, Empty, Empty),
            new ConstructionInstalled("building-slot-a", "building_old", ConstructionKind.Building,
                new Dictionary<string, decimal> { ["energy"] = 9m }, new Dictionary<string, decimal> { ["foundry"] = 80m }, Enabled: false),
            new ConstructionInstalled("zone-slot-other", "zone_other", ConstructionKind.Zone, Empty, Empty),
            new ConstructionInstalled("building-slot-other", "building_other", ConstructionKind.Building,
                new Dictionary<string, decimal> { ["energy"] = 3m }, new Dictionary<string, decimal> { ["miner"] = 25m }),
        };
        var state = new ConstructionLiveRuntime("building-replacement-colony", 1, slots, installed);
        var bank = Bank(100m);
        var replacementQuote = Quote("building_new", ConstructionKind.Building, 2m, 40m);
        var zoneQuote = Quote("zone_new", ConstructionKind.Zone, 2m, 0m);
        return (state, bank, replacementQuote, zoneQuote, new ConstructionBuildingReplacement("building_old", "zone-a"));
    }

    private static IReadOnlyDictionary<string, decimal> Empty { get; } = new Dictionary<string, decimal>();

    private static ConstructionQuote Quote(string definitionId, ConstructionKind kind, decimal work, decimal minerals)
        => new("fixture-profile", "fixture-policy", definitionId, kind, work,
            new Dictionary<string, decimal> { ["minerals"] = minerals },
            kind == ConstructionKind.Building ? new Dictionary<string, decimal> { ["energy"] = 4m } : Empty,
            kind == ConstructionKind.Building ? new Dictionary<string, decimal> { ["artisan"] = 30m } : Empty, []);

    private static ConstructionBankSnapshot Bank(decimal minerals)
        => new(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [1] = new Dictionary<string, decimal> { ["minerals"] = minerals },
        }, new Dictionary<string, ConstructionStorage>
        {
            ["minerals"] = new(false, 100m), ["energy"] = new(true, null),
        });

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
