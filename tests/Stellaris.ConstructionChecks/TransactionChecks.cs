using System.Text.Json;
using Stellaris.Core.Construction;
using Stellaris.Session.M3;

static class TransactionChecks
{
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task Orders()
    {
        var initial = Ledger();
        Reject(initial.Enqueue("gate", 1, 0, "o", "a", Quote(gated: true)), initial, "dependency-unresolved");
        Reject(initial.Enqueue("poor", 1, 0, "o", "a", Quote(2000)), initial, "insufficient-resources");
        Reject(initial.Enqueue("foreign", 2, 0, "o", "a", Quote()), initial, "not-owner");
        Reject(initial.Enqueue("stale", 1, 3, "o", "a", Quote()), initial, "stale-revision");
        var queued = Accept(initial.Enqueue("enqueue", 1, 0, "o", "a", Quote()));
        Assert(initial.Accounts[1]["minerals"] == 1000 && initial.Orders.Count == 0, "Prior ledger mutated.");
        Assert(queued.Accounts[1]["minerals"] == 600 && queued.Orders["o"].Status == ConstructionOrderStatus.Pending, "Debit/reservation missing.");
        var duplicate = queued.Enqueue("enqueue", 1, 0, "o", "a", Quote());
        Assert(duplicate.Accepted && duplicate.Code == "duplicate" && duplicate.AppliedRevision == 1 && ReferenceEquals(duplicate.State, queued), "Retry charged again.");
        Reject(queued.Enqueue("enqueue", 1, 0, "other", "b", Quote()), queued, "command-id-conflict");
        Reject(queued.Enqueue("busy", 1, 1, "other", "a", Quote()), queued, "slot-unavailable");
        Reject(queued.Progress("overrun", 1, 1, "test-phase", [new("o", 11)]), queued, "work-overrun-policy-unresolved");
        var finished = Accept(queued.Progress("finish", 1, 1, "test-phase", [new("o", 10)]));
        Assert(finished.Orders["o"].Status == ConstructionOrderStatus.Completed && finished.Installed["a"].InfrastructureUpkeep["energy"] == 2 && finished.Installed["a"].JobCapacity["farmer"] == 200, "Completion contributions conflated.");
        Reject(finished.Progress("again", 1, 2, "test-phase", [new("o", 1)]), finished, "invalid-progress");
        Assert(finished.Progress("finish", 1, 1, "test-phase", [new("o", 10)]).Code == "duplicate", "Completion retry failed.");
        Reject(finished.Enqueue("installed", 1, 2, "new", "a", Quote()), finished, "slot-unavailable");
        return Task.CompletedTask;
    }

    public static Task Refunds()
    {
        var queued = Accept(Ledger().Enqueue("e", 1, 0, "o", "a", Quote()));
        foreach (var reason in new[] { ConstructionCancellationReason.Player, ConstructionCancellationReason.Destroyed, ConstructionCancellationReason.ConditionLost })
        {
            Reject(queued.Cancel("missing", 1, 1, "o", reason, null), queued, "dependency-unresolved");
        }
        Reject(queued.Cancel("tooMuch", 1, 1, "o", ConstructionCancellationReason.Player, Refund(401)), queued, "invalid-refund");
        Reject(queued.Cancel("cap", 1, 1, "o", ConstructionCancellationReason.Player, Refund(400, 999)), queued, "refund-cap-policy-unresolved");
        Reject(queued.Cancel("noCap", 1, 1, "o", ConstructionCancellationReason.Player, new("test-refund", Map("minerals", 400), _empty)), queued, "dependency-unresolved");
        var cancelled = Accept(queued.Cancel("cancel", 1, 1, "o", ConstructionCancellationReason.Player, Refund(400)));
        Assert(cancelled.Accounts[1]["minerals"] == 1000 && cancelled.Orders["o"].Status == ConstructionOrderStatus.Cancelled, "Cancel not atomic.");
        var cancelRetry = cancelled.Cancel("cancel", 1, 1, "o", ConstructionCancellationReason.Player, Refund(400));
        Assert(cancelRetry.Accepted && cancelRetry.Code == "duplicate" && cancelRetry.AppliedRevision == 2 && ReferenceEquals(cancelRetry.State, cancelled), "Cancellation retry refunded twice.");
        Accept(cancelled.Enqueue("reuse", 1, 2, "second", "a", Quote()));
        Reject(queued.ChangeOwner("change", 1, 1, 2, new Dictionary<string, ConstructionRefund>()), queued, "dependency-unresolved");
        var changed = Accept(queued.ChangeOwner("change", 1, 1, 2, new Dictionary<string, ConstructionRefund> { ["o"] = Refund(400) }));
        Assert(changed.OwnerId == 2 && changed.Accounts[1]["minerals"] == 1000 && changed.Accounts[2]["minerals"] == 1000 && changed.Orders["o"].Status == ConstructionOrderStatus.Cancelled, "Original payer/ownership lost.");
        Reject(changed.Enqueue("e", 1, 0, "o", "a", Quote()), changed, "not-owner");
        Reject(changed.ChangeOwner("change", 1, 1, 2, new Dictionary<string, ConstructionRefund> { ["o"] = Refund(400) }), changed, "not-owner");
        var batch = Accept(queued.Enqueue("second", 1, 1, "second", "b", Quote()));
        Reject(batch.ChangeOwner("batch-refund", 1, 2, 2, new Dictionary<string, ConstructionRefund> { ["o"] = Refund(400), ["second"] = Refund(401) }), batch, "invalid-refund");
        Assert(batch.OwnerId == 1 && batch.Accounts[1]["minerals"] == 200 && batch.Orders.Values.All(order => order.Status == ConstructionOrderStatus.Pending), "Partial ownership refund escaped candidate.");
        return Task.CompletedTask;
    }

    public static Task Safety()
    {
        foreach (var mode in Enum.GetValues<ConstructionAuthorityMode>())
        {
            var initial = Ledger(mode, true);
            var queued = Accept(initial.Enqueue("one", 1, 0, "one", "a", Quote()));
            queued = Accept(queued.Enqueue("two", 1, 1, "two", "b", Quote()));
            Reject(queued.Progress("batch", 1, 2, "phase", [new("one", 10), new("two", 11)]), queued, "work-overrun-policy-unresolved");
            Assert(queued.Installed.Count == 0 && queued.Orders["one"].CompletedWork == 0 && queued.Identity == initial.Identity, "Partial batch or identity mutation.");
            Reject(queued.Progress("phase", 1, 2, "bad\nphase", [new("one", 1)]), queued, "invalid-identifier");
            Reject(queued.Progress("missing", 1, 2, "", [new("one", 1)]), queued, "dependency-unresolved");
        }
        var costs = new Dictionary<string, decimal> { ["minerals"] = 400 };
        var quote = new ConstructionQuote("test-profile", "test-policy", "test-building", ConstructionKind.Building, 10, costs, _empty, _empty, []);
        costs["minerals"] = 0;
        Assert(quote.Costs["minerals"] == 400, "Quote references mutable input.");
        var invalid = Ledger();
        Reject(invalid.Enqueue("bad-slot", 1, 0, "o", new string('x', 161), Quote()), invalid, "invalid-identifier");
        Reject(invalid.Enqueue("bad-rev", 1, -1, "o", "a", Quote()), invalid, "invalid-revision");
        Reject(invalid.Cancel("null-order", 1, 0, null!, ConstructionCancellationReason.Player, null), invalid, "invalid-identifier");
        Reject(invalid.Progress("null-step", 1, 0, "phase", [new(null!, 1)]), invalid, "invalid-identifier");
        Reject(invalid.Progress("large-step", 1, 0, "phase", [new(new string('x', 161), 1)]), invalid, "invalid-identifier");
        Reject(invalid.Progress("large-blank-policy", 1, 0, new string(' ', 161), [new("o", 1)]), invalid, "invalid-identifier");
        Reject(invalid.ChangeOwner("large-key", 1, 0, 2, new Dictionary<string, ConstructionRefund> { [new string('x', 161)] = Refund(0) }), invalid, "invalid-identifier");
        Throws(() => new ConstructionQuote("p", "policy", "d", ConstructionKind.Building, 0, _empty, _empty, _empty, []));
        Throws(() => new ConstructionQuote("p", "policy", "d", ConstructionKind.Building, 1, Map("x", -1), _empty, _empty, []));
        Throws(() => new ConstructionLedger(new("c", ConstructionAuthorityMode.Hive, true, null, "s"), 1, Accounts(), ["a"]));
        Throws(() => new ConstructionLedger(new("c", ConstructionAuthorityMode.Hive, false, null, "s"), 1, Accounts(), Enumerable.Range(0, 4097).Select(i => i.ToString())));
        var huge = Accept(Ledger().Enqueue("huge", 1, 0, "huge", "a", new("p", "policy", "d", ConstructionKind.Building, decimal.MaxValue, _empty, _empty, _empty, [])));
        huge = Accept(huge.Progress("first", 1, 1, "phase", [new("huge", decimal.MaxValue - 1)]));
        Reject(huge.Progress("overflow", 1, 2, "phase", [new("huge", 2)]), huge, "arithmetic-overflow");
        return Task.CompletedTask;
    }

    public static Task Protocol()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var command = JsonSerializer.Deserialize<M3ClientCommand>("{\"commandId\":\"c\",\"type\":\"enqueueConstruction\",\"colonyId\":\"colony\",\"expectedColonyRevision\":3,\"definitionId\":\"building_hydroponics_farm\",\"slotId\":\"slot\",\"orderId\":\"order\"}", options)!;
        Assert(command.ColonyId == "colony" && command.ExpectedColonyRevision == 3 && command.OrderId == "order" && command.Count is null, "Command fields changed.");
        var entry = new M3ConstructionCatalogView("building_hydroponics_farm", "building", null, "unresolved", [], null, [new("dependency-unresolved", "native-cost")]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(entry, options));
        Assert(json.RootElement.GetProperty("quote").ValueKind == JsonValueKind.Null && json.RootElement.GetProperty("imageDefinitionKey").ValueKind == JsonValueKind.Null, "Unresolved values became fabricated defaults.");
        var job = new M3DevelopmentJobView("farmer", "worker", null, null, null, null, []);
        using var jobs = JsonDocument.Parse(JsonSerializer.Serialize(job, options));
        Assert(jobs.RootElement.GetProperty("workforce").ValueKind == JsonValueKind.Null, "Unknown workforce became zero.");
        var unmapped = new M3DevelopmentFeatureView("feature-instance", "d_failing_infrastructure", null, true, "d_failing_infrastructure", []);
        using var feature = JsonDocument.Parse(JsonSerializer.Serialize(unmapped, options));
        Assert(feature.RootElement.GetProperty("clearanceSlotId").ValueKind == JsonValueKind.Null, "Missing clearance binding became an actionable slot.");
        using var mapped = JsonDocument.Parse(JsonSerializer.Serialize(unmapped with { ClearanceSlotId = "clearance-slot" }, options));
        Assert(mapped.RootElement.GetProperty("clearanceSlotId").GetString() == "clearance-slot" && mapped.RootElement.GetProperty("instanceId").GetString() == "feature-instance", "Explicit clearance mapping replaced feature identity.");
        return Task.CompletedTask;
    }

    private static ConstructionLedger Ledger(ConstructionAuthorityMode mode = ConstructionAuthorityMode.Individualist, bool mobile = false) => new(new("colony", mode, mobile, mobile ? "carrier" : null, "system"), 1, Accounts(), ["a", "b"]);
    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> Accounts() => new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = Map("minerals", 1000), [2] = Map("minerals", 1000) };
    private static IReadOnlyDictionary<string, decimal> Map(string key, decimal value) => new Dictionary<string, decimal> { [key] = value };
    private static ConstructionQuote Quote(decimal cost = 400, bool gated = false) => new("test-profile", "test-policy", "test-building", ConstructionKind.Building, 10, Map("minerals", cost), Map("energy", 2), Map("farmer", 200), gated ? [new("dependency-unresolved", "native-policy")] : []);
    private static ConstructionRefund Refund(decimal amount, decimal cap = 1000) => new("test-refund-policy", Map("minerals", amount), Map("minerals", cap));
    private static ConstructionLedger Accept(ConstructionTransition result) { Assert(result.Accepted, result.Code); return result.State; }
    private static void Reject(ConstructionTransition result, ConstructionLedger prior, string code) => Assert(!result.Accepted && result.Code == code && ReferenceEquals(result.State, prior), "Expected atomic rejection " + code + ", received " + result.Code);
    private static void Assert(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected invalid argument rejection."); }
}
