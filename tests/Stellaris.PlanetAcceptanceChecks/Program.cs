using System.Text.Json;
using Stellaris.Core.Assets;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;
using Stellaris.Session.M3;

// Deliberately synthetic arithmetic policies: these fixtures establish no native prices, work rates or refund fractions.
var checks = new List<(string Name, Action Run)>
{
    ("Independent ledger: replay after later changes preserves current state", ReplayAfterProgress),
    ("Independent ledger: late multi-resource refund failure is atomic and retryable", RefundCandidate),
    ("Independent ledger: mixed completed/pending ownership change refunds only original payer", MixedOwnership),
    ("Independent ledger: nested published collections reject mutation", ImmutablePublication),
    ("Independent live session: unavailable development stays absent and reserved commands reject", UnavailableDevelopment),
    ("Independent movement baseline: inclusive boundary, rejection, duplicate and POV privacy", MovementBoundary),
    ("Independent template selection: privacy, retry, reconnect and running refusal", TemplateSelection)
};
if (args.Contains("--navigation-capture", StringComparer.Ordinal))
{
    Console.WriteLine(JsonSerializer.Serialize(CaptureNavigation(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    return 0;
}
var gameRoot = Environment.GetEnvironmentVariable("STELLARIS_GAME_ROOT");
var skipped = string.IsNullOrWhiteSpace(gameRoot) ? 1 : 0;
if (skipped == 0)
{
    checks.Add(("Independent real-source binding, placement occupancy and diagnostic POV", () => SourceBindingAndPlacement(gameRoot!)));
}
else
{
    Console.WriteLine("SKIP: real-source binding/placement/diagnostic POV requires STELLARIS_GAME_ROOT.");
}
var failed = 0;
foreach (var (name, run) in checks)
{
    try
    {
        run();
        Console.WriteLine("PASS: " + name);
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine("FAIL: " + name + "\n" + exception);
    }
}
Console.WriteLine($"Independent acceptance groups: {checks.Count}; failed: {failed}; skipped: {skipped}; synthetic policies, no native/browser acceptance.");
return failed == 0 ? 0 : 1;

void ReplayAfterProgress()
{
    var initial = Ledger();
    var quote = Quote();
    var queued = Applied(initial.Enqueue("enqueue", 1, 0, "order", "first", quote));
    var advanced = Applied(queued.Progress("advance", 1, 1, "qa-explicit-phase", [new("order", 1.25m)]));
    var replay = advanced.Enqueue("enqueue", 1, 0, "order", "first", Quote());
    Check(replay.Accepted && replay.Code == "duplicate" && replay.AppliedRevision == 1 && ReferenceEquals(replay.State, advanced), "Replay must acknowledge its original revision without rolling back later progress.");
    Check(advanced.Revision == 2 && advanced.Orders["order"].CompletedWork == 1.25m && advanced.Accounts[1]["qa-a"] == 16.75m, "Replay changed debit/progress.");
    AtomicReject(advanced.Enqueue("enqueue", 1, 0, "order", "first", Quote(3.5m)), advanced, "command-id-conflict");
    var finished = Applied(advanced.Progress("complete", 1, 2, "qa-explicit-phase", [new("order", 3.5m)]));
    var later = Applied(finished.Enqueue("later", 1, 3, "later", "second", Quote()));
    var completionReplay = later.Progress("complete", 1, 2, "qa-explicit-phase", [new("order", 3.5m)]);
    Check(completionReplay.Accepted && completionReplay.AppliedRevision == 3 && ReferenceEquals(completionReplay.State, later), "Completion replay replaced current ledger.");
    Check(later.Installed.Count == 1 && later.Orders["later"].Status == ConstructionOrderStatus.Pending, "Completion replay installed twice or lost subsequent order.");
}

void RefundCandidate()
{
    var queued = Applied(Ledger().Enqueue("enqueue", 1, 0, "order", "first", Quote()));
    var bad = new ConstructionRefund("qa-explicit-refund", Map(("qa-a", 1.5m), ("qa-b", 0.75m)), Map(("qa-a", 20m), ("qa-b", 0m)));
    AtomicReject(queued.Cancel("cancel", 1, 1, "order", ConstructionCancellationReason.ConditionLost, bad), queued, "refund-cap-policy-unresolved");
    Check(queued.Accounts[1]["qa-a"] == 16.75m && queued.Accounts[1]["qa-b"] == 7.5m && queued.Orders["order"].Status == ConstructionOrderStatus.Pending, "Earlier resource refund leaked from failed candidate.");
    var corrected = new ConstructionRefund("qa-explicit-refund", bad.Amounts, Map(("qa-a", 20m), ("qa-b", 12m)));
    var cancelled = Applied(queued.Cancel("cancel", 1, 1, "order", ConstructionCancellationReason.ConditionLost, corrected));
    Check(cancelled.Accounts[1]["qa-a"] == 18.25m && cancelled.Accounts[1]["qa-b"] == 8.25m, "Corrected explicit amounts were not applied exactly.");
    Check(cancelled.Orders["order"].Status == ConstructionOrderStatus.Cancelled && cancelled.Revision == 2, "Failed command consumed a successful receipt or revision.");
    Applied(cancelled.Enqueue("reserve-again", 1, 2, "replacement", "first", Quote()));
}

void MixedOwnership()
{
    foreach (var authority in Enum.GetValues<ConstructionAuthorityMode>())
    {
        var initial = Ledger(authority);
        var queued = Applied(initial.Enqueue("enqueue-1", 1, 0, "built", "first", Quote()));
        queued = Applied(queued.Enqueue("enqueue-2", 1, 1, "pending", "second", Quote()));
        var mixed = Applied(queued.Progress("complete", 1, 2, "qa-explicit-phase", [new("built", 4.75m)]));
        var refund = new ConstructionRefund("qa-explicit-refund", Map(("qa-a", 3.25m), ("qa-b", 2.5m)), Map(("qa-a", 20m), ("qa-b", 12m)));
        AtomicReject(mixed.ChangeOwner("owner", 1, 3, 2, new Dictionary<string, ConstructionRefund> { ["built"] = refund, ["pending"] = refund }), mixed, "dependency-unresolved");
        var changed = Applied(mixed.ChangeOwner("owner", 1, 3, 2, new Dictionary<string, ConstructionRefund> { ["pending"] = refund }));
        Check(changed.OwnerId == 2 && changed.Accounts[1]["qa-a"] == 16.75m && changed.Accounts[2]["qa-a"] == 41m, "Refund went to new owner or included completed work.");
        Check(changed.Orders["built"].Status == ConstructionOrderStatus.Completed && changed.Orders["pending"].Status == ConstructionOrderStatus.Cancelled && changed.Installed.Count == 1, "Ownership change removed installation or retained pending reservation.");
        Check(changed.Identity == initial.Identity && changed.Identity.IsMobile && changed.Identity.CarrierId == "qa-carrier", "Authority/mobile identity was replaced.");
        AtomicReject(changed.Enqueue("enqueue-2", 1, 1, "pending", "second", Quote()), changed, "not-owner");
        var next = Applied(changed.Enqueue("new-owner", 2, 4, "new-owner", "second", Quote()));
        Check(next.Orders["new-owner"].PayerId == 2 && next.Accounts[1]["qa-a"] == changed.Accounts[1]["qa-a"], "New order changed original payer account.");
    }
}

void ImmutablePublication()
{
    var balances = new Dictionary<string, decimal> { ["qa-a"] = 20m, ["qa-b"] = 10m };
    var accounts = new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = balances };
    var slots = new List<string> { "first" };
    var ledger = new ConstructionLedger(new("qa-colony", ConstructionAuthorityMode.Hive, false, null, null), 1, accounts, slots);
    balances["qa-a"] = 0;
    accounts.Clear();
    slots.Clear();
    Check(ledger.Accounts[1]["qa-a"] == 20m && ledger.Slots.Count == 1, "Constructor retained caller-owned collection.");
    var finished = Applied(Applied(ledger.Enqueue("enqueue", 1, 0, "order", "first", Quote())).Progress("complete", 1, 1, "qa-explicit-phase", [new("order", 4.75m)]));
    RefusesMutation(() => ((IDictionary<int, IReadOnlyDictionary<string, decimal>>)finished.Accounts).Clear());
    RefusesMutation(() => ((IDictionary<string, decimal>)finished.Accounts[1])["qa-a"] = 0);
    RefusesMutation(() => ((IList<string>)finished.Slots).Clear());
    RefusesMutation(() => ((IDictionary<string, ConstructionOrder>)finished.Orders).Clear());
    RefusesMutation(() => ((IDictionary<string, ConstructionInstalled>)finished.Installed).Clear());
    RefusesMutation(() => ((IDictionary<string, decimal>)finished.Installed["first"].InfrastructureUpkeep)["qa-upkeep"] = 99);
    RefusesMutation(() => ((IDictionary<string, decimal>)finished.Installed["first"].JobCapacity)["qa-job"] = 99);
    Check(finished.Installed["first"].InfrastructureUpkeep["qa-upkeep"] == 0.375m && finished.Installed["first"].JobCapacity["qa-job"] == 6.5m, "Published nested contributions changed.");
}

void UnavailableDevelopment()
{
    var (session, host, second, observer) = RunningSession();
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    foreach (var grant in new[] { host, second, observer })
    {
        var before = JsonSerializer.Serialize(session.View(grant.ParticipantId), options);
        using var view = JsonDocument.Parse(before);
        Check(!view.RootElement.TryGetProperty("development", out _), "Unavailable development was published as live data.");
        foreach (var type in new[] { "enqueueConstruction", "clearBlocker", "cancelConstruction" })
        {
            var targetOwner = grant.ParticipantId == observer.ParticipantId ? host.ParticipantId : grant.ParticipantId;
            var colony = session.View(grant.ParticipantId).Spatial.Colonies.Single(item => item.OwnerParticipantId == targetOwner);
            var command = new M3ClientCommand("qa-" + type, type, ColonyId: colony.Id, ExpectedColonyRevision: 0, DefinitionId: type == "cancelConstruction" ? null : "qa-definition", SlotId: type == "cancelConstruction" ? null : "first", OrderId: "qa-order");
            var result = session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.Zero);
            Check(!result.Accepted && result.Code == (grant.ParticipantId == observer.ParticipantId ? "OUT_OF_SCOPE" : "DEPENDENCY_UNRESOLVED"), "Construction activated without native/source gates or revealed observer details.");
            Check(session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.Zero) == result, "Rejected live command retry changed outcome.");
        }
        Check(before == JsonSerializer.Serialize(session.View(grant.ParticipantId), options), "Unavailable command changed published simulation state.");
    }
}

void MovementBoundary()
{
    var (session, host, second, observer) = RunningSession();
    var own = session.View(host.ParticipantId).Spatial.Fleets.Single(fleet => fleet.OwnerParticipantId == host.ParticipantId);
    var location = (M3SystemLocationView)own.Location;
    var system = session.View(host.ParticipantId).Spatial.Systems.Single(item => item.Id == location.SystemId);
    var command = new M3ClientCommand("boundary", "moveFleetToPoint", FleetId: own.Id, SystemId: system.Id, X: system.Radius, Z: 0);
    var accepted = session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero);
    Check(accepted.Accepted, "Inclusive authoritative radius rejected.");
    var movement = session.View(host.ParticipantId).Spatial.Fleets.Single(fleet => fleet.Id == own.Id).Movement;
    Check(movement?.Target is M3PointTargetView target && target.SystemId == system.Id && target.Position.X == system.Radius && target.Position.Z == 0, "Accepted boundary order is missing its authoritative target.");
    var beforeRetry = JsonSerializer.Serialize(session.View(host.ParticipantId));
    Check(session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero) == accepted, "Boundary retry was applied twice.");
    Check(beforeRetry == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Duplicate ACK concealed a state mutation.");
    Check(session.Execute(host.ParticipantId, host.ConnectionId, new("advance", "stepDay"), TimeSpan.Zero).Accepted, "Paused movement progression failed.");
    var progressed = session.View(host.ParticipantId).Spatial.Fleets.Single(fleet => fleet.Id == own.Id);
    Check(JsonSerializer.Serialize(progressed.Location) != JsonSerializer.Serialize(own.Location), "Movement fixture did not actually advance.");
    beforeRetry = JsonSerializer.Serialize(session.View(host.ParticipantId));
    Check(session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero) == accepted && beforeRetry == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Late retry restarted accepted movement.");
    var before = JsonSerializer.Serialize(session.View(host.ParticipantId));
    var outside = session.Execute(host.ParticipantId, host.ConnectionId, command with { CommandId = "outside", X = system.Radius + 1 }, TimeSpan.Zero);
    Check(!outside.Accepted && before == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Outside boundary clamped/replaced the accepted order.");
    Check(!session.Execute(second.ParticipantId, second.ConnectionId, command with { CommandId = "foreign" }, TimeSpan.Zero).Accepted, "Foreign player can move fleet.");
    Check(before == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Foreign rejection changed owner's accepted order/state.");
    Check(session.Execute(observer.ParticipantId, observer.ConnectionId, new("pov", "observerView", TargetParticipantId: host.ParticipantId), TimeSpan.Zero).Accepted, "Approved observer POV failed.");
    var observed = session.View(observer.ParticipantId);
    Check(observed.You == observer.ParticipantId && observed.ViewContext.ParticipantId == host.ParticipantId, "Observer control identity changed.");
    Check(JsonSerializer.Serialize(observed.Spatial.Fleets.Single(fleet => fleet.Id == own.Id).Movement) == JsonSerializer.Serialize(session.View(host.ParticipantId).Spatial.Fleets.Single(fleet => fleet.Id == own.Id).Movement), "Observer POV differs from player's accepted order.");
    Check(session.View(second.ParticipantId).Spatial.Fleets.Single(fleet => fleet.Id == own.Id).Movement is null, "Foreign movement details leaked.");
    before = JsonSerializer.Serialize(session.View(host.ParticipantId));
    Check(!session.Execute(observer.ParticipantId, observer.ConnectionId, command with { CommandId = "observer-order" }, TimeSpan.Zero).Accepted, "Observer display POV grants command authority.");
    Check(before == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Observer rejection changed owner's accepted order/state.");
}

object CaptureNavigation()
{
    var (session, host, second, observer) = RunningSession();
    var captures = new List<object>();
    var fleet = session.View(host.ParticipantId).Spatial.Fleets.Single(item => item.OwnerParticipantId == host.ParticipantId);
    var location = (M3SystemLocationView)fleet.Location;
    var exits = session.View(host.ParticipantId).Spatial.Exits!.Where(exit => exit.SystemId == location.SystemId).OrderBy(exit => exit.DestinationSystemId, StringComparer.Ordinal).ToArray();
    Check(exits.Length >= 2, "Navigation fixture needs two received neighboring exits.");
    Send(observer, new("capture-pov", "observerView", TargetParticipantId: host.ParticipantId));
    Capture("idle");
    var radius = session.View(host.ParticipantId).Spatial.Systems.Single(system => system.Id == location.SystemId).Radius;
    Send(host, new("capture-local", "moveFleetToPoint", FleetId: fleet.Id, SystemId: location.SystemId, X: radius - 1, Z: 0));
    Capture("local");
    Send(host, new("capture-replace-local", "moveFleetToPoint", FleetId: fleet.Id, SystemId: location.SystemId, X: 0, Z: radius));
    Capture("localReplacement");
    Send(host, new("capture-stop-local", "stopFleet", FleetId: fleet.Id));
    Capture("localStopped");
    Send(host, new("capture-system", "moveFleetToSystem", FleetId: fleet.Id, TargetSystemId: exits[0].DestinationSystemId));
    Capture("system");
    Send(host, new("capture-replace-system", "moveFleetToSystem", FleetId: fleet.Id, TargetSystemId: exits[1].DestinationSystemId));
    Capture("systemReplacement");
    var stages = new HashSet<string>(StringComparer.Ordinal);
    for (var day = 0; day < 512; day++)
    {
        Send(host, new("capture-day-" + day, "stepDay"));
        var current = session.View(host.ParticipantId).Spatial.Fleets.Single(item => item.Id == fleet.Id);
        if (stages.Add(current.Phase)) { Capture(current.Phase); }
        if (current.Location is M3TransitLocationView transit && transit.Progress is > 0 and < 1 && stages.Add("midTransit")) { Capture("midTransit"); }
        if (current.Phase == "idle")
        {
            Check(current.Location is M3SystemLocationView arrived && arrived.SystemId == exits[1].DestinationSystemId, "Route did not arrive at the accepted replacement target.");
            Check(stages.Contains("charging") && stages.Contains("midTransit"), "Capture did not exercise charge and nonzero transit progression.");
            Send(observer, new("capture-general", "observerView", TargetParticipantId: 0));
            return new { FleetId = fleet.Id, StartSystemId = location.SystemId, Captures = captures, General = session.View(observer.ParticipantId) };
        }
    }
    throw new InvalidOperationException("Navigation fixture did not arrive within its bounded QA safety limit.");

    void Send(M3JoinGrant grant, M3ClientCommand command) => Check(session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.Zero).Accepted, "Navigation capture command failed: " + command.Type);
    void Capture(string label) => captures.Add(new { Label = label, Owner = session.View(host.ParticipantId), Foreign = session.View(second.ParticipantId), Observer = session.View(observer.ParticipantId) });
}

void TemplateSelection()
{
    var (session, host, second, observer) = RunningSession(start: false);
    Check(session.View(host.ParticipantId).EmpireSetup?.SelectedTemplateId is null && session.View(second.ParticipantId).EmpireSetup?.SelectedTemplateId is null, "An ordinary template was silently assigned.");
    var command = new M3ClientCommand("select-template", "selectEmpireTemplate", TemplateId: "economic");
    var accepted = session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero);
    Check(accepted.Accepted, "Explicit accepted template selection failed.");
    Check(session.Execute(second.ParticipantId, second.ConnectionId, command with { TemplateId = "military" }, TimeSpan.Zero).Accepted, "Second explicit template selection failed.");
    Check(session.View(host.ParticipantId).EmpireSetup!.SelectedTemplateId == "economic" && session.View(second.ParticipantId).EmpireSetup!.SelectedTemplateId == "military" && session.View(observer.ParticipantId).EmpireSetup!.SelectedTemplateId is null, "Template selection leaked or cross-bound.");
    Check(session.Execute(host.ParticipantId, host.ConnectionId, new("ready-before-retry", "ready", Ready: true), TimeSpan.Zero).Accepted, "Ready failed.");
    var before = JsonSerializer.Serialize(session.View(host.ParticipantId));
    Check(session.Execute(host.ParticipantId, host.ConnectionId, command, TimeSpan.Zero) == accepted && before == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Template retry reset later readiness/state.");
    Check(!session.Execute(host.ParticipantId, host.ConnectionId, command with { CommandId = "unknown-template", TemplateId = "qa-unknown" }, TimeSpan.Zero).Accepted && before == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Unknown template replaced prior selection/readiness.");
    var observerBefore = JsonSerializer.Serialize(session.View(observer.ParticipantId));
    Check(!session.Execute(observer.ParticipantId, observer.ConnectionId, command, TimeSpan.Zero).Accepted && before == JsonSerializer.Serialize(session.View(host.ParticipantId)) && observerBefore == JsonSerializer.Serialize(session.View(observer.ParticipantId)) && session.View(observer.ParticipantId).EmpireSetup!.SelectedTemplateId is null, "Observer refusal changed host or observer template state.");
    session.Disconnect(host.ParticipantId, host.ConnectionId, TimeSpan.Zero);
    host = session.Join("Independent A", "player", host.Token, TimeSpan.Zero);
    Check(session.View(host.ParticipantId).EmpireSetup!.SelectedTemplateId == "economic" && !session.View(host.ParticipantId).Participants.Single(item => item.Id == host.ParticipantId).Ready, "Reconnect lost template or retained readiness without import.");
    var profile = session.View(host.ParticipantId).AssetProfile;
    Check(session.Execute(host.ParticipantId, host.ConnectionId, new("reimport", "assetReady", Ready: true, ProfileId: profile.ProfileId, ProfileDigest: profile.ProfileDigest), TimeSpan.Zero).Accepted, "Reconnect asset readiness failed.");
    Check(session.Execute(observer.ParticipantId, observer.ConnectionId, new("template-pov", "observerView", TargetParticipantId: host.ParticipantId), TimeSpan.Zero).Accepted && session.View(observer.ParticipantId).EmpireSetup!.SelectedTemplateId == "economic" && session.View(observer.ParticipantId).You == observer.ParticipantId, "Observer POV lost template binding or actual identity.");
    foreach (var grant in new[] { host, second }) { Check(session.Execute(grant.ParticipantId, grant.ConnectionId, new("ready-to-start", "ready", Ready: true), TimeSpan.Zero).Accepted, "Post-selection readiness failed."); }
    Check(session.Execute(host.ParticipantId, host.ConnectionId, new("start", "start"), TimeSpan.Zero).Accepted, "Explicit-template session failed to start.");
    before = JsonSerializer.Serialize(session.View(host.ParticipantId));
    Check(!session.Execute(host.ParticipantId, host.ConnectionId, command with { CommandId = "running-template", TemplateId = "scientific" }, TimeSpan.Zero).Accepted && before == JsonSerializer.Serialize(session.View(host.ParticipantId)), "Running selection reset startup context.");
}

void SourceBindingAndPlacement(string root)
{
    var importer = new ConstructionDefinitionImporter();
    var source = importer.ImportAsync(root).GetAwaiter().GetResult();
    var fresh = importer.ImportAsync(root).GetAwaiter().GetResult();
    Check(source.Files.SequenceEqual(fresh.Files) && source.Definition.Id == fresh.Definition.Id, "Source changed during snapshot identity fixture.");
    // Hypothetical fully bound ordinary context, not the current live template or native startup.
    var facts = new Dictionary<ConstructionOwnerPredicate, bool?>
    {
        [ConstructionOwnerPredicate.UsesFood] = true, [ConstructionOwnerPredicate.Ai] = false,
        [ConstructionOwnerPredicate.Wilderness] = false, [ConstructionOwnerPredicate.Regular] = true,
        [ConstructionOwnerPredicate.SpiritualistFallenEmpire] = false, [ConstructionOwnerPredicate.Gestalt] = false
    };
    ConstructionContext Context(bool? from) => new(true, facts, from, "pc_continental", [], [], source.Definition.Prerequisites, Map((source.Definition.Id, 0)));
    var assessment = ConstructionAssessor.Assess(source, Context(false));
    var basis = ConstructionQuoteBasisEvaluator.Evaluate(source, assessment);
    Check(basis.Basis is not null && basis.Code == "raw-basis-only", "Same-source fully bound raw basis unavailable.");
    Check(ConstructionQuoteBasisEvaluator.Evaluate(fresh, assessment).Code == "source-snapshot-mismatch", "Same definition and hashes bypassed exact assessment/source binding.");
    var missingFrom = ConstructionAssessor.Assess(source, Context(null));
    Check(ConstructionQuoteBasisEvaluator.Evaluate(source, missingFrom).Basis is null && missingFrom.Costs.Any(cost => cost.Condition.Truth == ConstructionTruth.Unknown), "Owner facts silently supplied missing from context.");
    Check(assessment.Gates.Count > 0, "Raw basis was confused with activated final quote.");

    var placement = importer.ImportPlacementAsync(root).GetAwaiter().GetResult();
    var empty = new Dictionary<string, string>();
    // Explicit QA capacity/slot plan, intentionally unrelated to source max_buildings arithmetic.
    ConstructionPlacementState State(string zone, int? capacity = 2, IReadOnlyDictionary<string, string>? installed = null, IReadOnlyDictionary<string, string>? reserved = null) => new(zone, true, capacity, capacity is null ? null : "qa-capacity-policy", ["qa-first", "qa-second"], installed ?? empty, reserved ?? empty);
    var compatible = placement.Definitions.Values.Where(item => item.Kind == "zone").Select(item => item.Id).FirstOrDefault(id => ConstructionPlacementValidator.Validate(placement, source.Definition, State(id), "qa-first").Allowed == ConstructionTruth.True);
    Check(compatible is not null, "No imported zone accepts the building under an explicit QA capacity plan.");
    var unknown = ConstructionPlacementValidator.Validate(placement, source.Definition, State(compatible!, null), "qa-first");
    Check(unknown.Allowed == ConstructionTruth.Unknown && unknown.Code == "zone-effective-capacity-unresolved", "Declared source maximum filled unknown effective capacity.");
    var inconsistent = ConstructionPlacementValidator.Validate(placement, source.Definition, State(compatible!, 3), "qa-first");
    Check(inconsistent.Allowed == ConstructionTruth.Unknown, "Capacity/slot disagreement was treated as a valid free slot.");
    var installed = new Dictionary<string, string> { ["qa-second"] = source.Definition.Id };
    var reserved = new Dictionary<string, string> { ["qa-second"] = "qa-order" };
    var overlapping = ConstructionPlacementValidator.Validate(placement, source.Definition, State(compatible!, 2, installed, reserved), "qa-first");
    Check(overlapping.Allowed == ConstructionTruth.Unknown && overlapping.Code == "zone-instance-state-invalid", "Overlap outside requested slot escaped global occupancy validation.");
    var detached = State(compatible!, 2, installed);
    installed.Clear();
    Check(detached.Installed.ContainsKey("qa-second"), "Placement state retained mutable instance input.");

    var (session, host, second, observer) = RunningSession(source);
    var own = session.View(host.ParticipantId).Development ?? throw new InvalidOperationException("Imported source development is missing.");
    var other = session.View(second.ParticipantId).Development ?? throw new InvalidOperationException("Second player source development is missing.");
    Check(own.Colonies.Count == 1 && other.Colonies.Count == 1 && own.Colonies[0].ColonyId != other.Colonies[0].ColonyId, "Source catalog was not projected per owner.");
    Check(own.Colonies[0].OwnerParticipantId == host.ParticipantId && other.Colonies[0].OwnerParticipantId == second.ParticipantId, "Distinct development colonies were assigned to the wrong owners.");
    Check(own.Colonies[0].ColonyId == session.View(host.ParticipantId).Spatial.Colonies.Single(colony => colony.OwnerParticipantId == host.ParticipantId).Id && other.Colonies[0].ColonyId == session.View(second.ParticipantId).Spatial.Colonies.Single(colony => colony.OwnerParticipantId == second.ParticipantId).Id, "Development colony IDs do not match each recipient's spatial colony.");
    Check(session.View(observer.ParticipantId).Development!.Colonies.Count == 0, "General observer receives private construction colonies.");
    Check(session.Execute(observer.ParticipantId, observer.ConnectionId, new("source-pov", "observerView", TargetParticipantId: host.ParticipantId), TimeSpan.Zero).Accepted, "Source observer POV failed.");
    Check(JsonSerializer.Serialize(session.View(observer.ParticipantId).Development) == JsonSerializer.Serialize(own), "Observer diagnostic catalog differs from selected player's projection.");
    var catalog = own.Colonies[0].Catalog.Single();
    Check(catalog.DefinitionId == source.Definition.Id && catalog.Quote is null && catalog.Eligibility == "unresolved" && catalog.Gates.Count > 0, "Diagnostic definition acquired invented executable values.");
    Check(own.Colonies[0].Slots.Count == 0 && own.Colonies[0].Queue.Count == 0 && own.Colonies[0].Installed.Count == 0 && own.Colonies[0].Gates.Count > 0, "Unprojected layout was populated with synthetic live entries.");
    Console.WriteLine($"Real source: {source.ProfileId}, {source.Files.Count} files; placement: {placement.ProfileId}, {placement.Files.Count} files; contexts/capacity synthetic, no native execution.");
}

ConstructionLedger Ledger(ConstructionAuthorityMode mode = ConstructionAuthorityMode.Individualist) => new(new("qa-colony", mode, true, "qa-carrier", null), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = Map(("qa-a", 20m), ("qa-b", 10m)), [2] = Map(("qa-a", 41m), ("qa-b", 30m)) }, ["first", "second"]);
ConstructionQuote Quote(decimal cost = 3.25m) => new("qa-synthetic-content", "qa-explicit-arithmetic", "qa-definition", ConstructionKind.Building, 4.75m, Map(("qa-a", cost), ("qa-b", 2.5m)), Map(("qa-upkeep", 0.375m)), Map(("qa-job", 6.5m)), []);
IReadOnlyDictionary<string, decimal> Map(params (string Id, decimal Value)[] values) => values.ToDictionary(pair => pair.Id, pair => pair.Value, StringComparer.Ordinal);
ConstructionLedger Applied(ConstructionTransition result) { Check(result.Accepted && result.Code == "applied", result.Code); return result.State; }
void AtomicReject(ConstructionTransition result, ConstructionLedger prior, string code) => Check(!result.Accepted && result.Code == code && result.AppliedRevision is null && ReferenceEquals(result.State, prior), "Expected unchanged rejected candidate: " + code + ", got " + result.Code);
void Check(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
void RefusesMutation(Action action) { try { action(); } catch (NotSupportedException) { return; } throw new InvalidOperationException("Published collection accepted mutation."); }

(M3GameSession Session, M3JoinGrant Host, M3JoinGrant Second, M3JoinGrant Observer) RunningSession(ConstructionProjection? construction = null, bool start = true)
{
    // Synthetic files validate session manifest plumbing only, never image decoding or real-game content.
    var root = Path.Combine(Path.GetTempPath(), "Stellaris-IndependentPlanetQA-" + Guid.NewGuid().ToString("N"));
    M3AssetManifest manifest;
    Directory.CreateDirectory(root);
    try
    {
        File.WriteAllText(Path.Combine(root, "launcher-settings.json"), "{\"rawVersion\":\"v4.5.2\"}");
        foreach (var relative in M3AssetManifestBuilder.ProfilePaths)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "qa-synthetic-not-an-asset");
        }
        manifest = new M3AssetManifestBuilder().BuildAsync(root).GetAwaiter().GetResult();
    }
    finally
    {
        Directory.Delete(root, true);
    }
    var source = new ContentManifest("4.5.2", "qa-synthetic", [], []);
    string[] jobIds = ["technician", "miner", "farmer"];
    string[] buildingIds = ["building_energy_grid", "building_mineral_purification_plant", "building_food_processing_facility"];
    string[] traitIds = ["trait_agrarian", "trait_industrious", "trait_ingenious"];
    var resources = new[] { "energy", "minerals", "food" }.Select(id => new ResourceDefinition(id, 2000)).ToArray();
    var jobs = jobIds.Select((id, index) => new JobDefinition(id, "worker", 100, Map((resources[index].Id, 1m)), Map())).ToArray();
    var buildings = buildingIds.Select(id => new BuildingDefinition(id, Map(), [])).ToArray();
    var content = new EconomyProjection(source, resources, jobs, [new("worker", null)], buildings, [], 100);
    var traits = traitIds.ToDictionary(id => id, id => new TraitWorkforceProjection(source, [id], Map(), []));
    var session = new M3GameSession(content, traits, manifest, construction);
    var host = session.Join("Independent A", "player", null, TimeSpan.Zero);
    var second = session.Join("Independent B", "player", null, TimeSpan.Zero);
    var observer = session.Join("Independent observer", "observer", null, TimeSpan.Zero);
    var pending = session.View(observer.ParticipantId);
    Check((pending.Development is null || pending.Development.Colonies.Count == 0) && pending.Planets.Count == 0, "Pending observer receives development/economy.");
    Send(host, new("approve", "approveObserver", TargetParticipantId: observer.ParticipantId));
    foreach (var grant in new[] { host, second, observer }) { Send(grant, new("assets", "assetReady", Ready: true, ProfileId: manifest.ProfileId, ProfileDigest: manifest.ProfileDigest)); }
    if (!start) { return (session, host, second, observer); }
    Send(host, new("ready", "ready", Ready: true));
    Send(second, new("ready", "ready", Ready: true));
    Send(host, new("start", "start"));
    return (session, host, second, observer);

    void Send(M3JoinGrant grant, M3ClientCommand command) => Check(session.Execute(grant.ParticipantId, grant.ConnectionId, command, TimeSpan.Zero).Accepted, "Synthetic session setup failed: " + command.Type);
}
