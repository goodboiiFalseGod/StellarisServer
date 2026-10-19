using System.Text.Json;
using Stellaris.Session.M3;

var checks = 0;
var all = new HashSet<string>(Enumerable.Range(1, 16).Select(index => $"S{index:00}"), StringComparer.Ordinal);
SpaceWorld Fixture() => SpaceWorld.CreateFixture(7, 42);
FleetState Fleet(SpaceWorld world) => world.Fleets[0];
string Snapshot(SpaceWorld value) => JsonSerializer.Serialize(new
{
    value.ElapsedDay,
    value.Systems,
    value.Hyperlanes,
    value.Bodies,
    value.Colonies,
    Fleets = value.Fleets.Select(fleet => new
    {
        fleet.Id,
        fleet.OwnerParticipantId,
        fleet.Phase,
        Location = JsonSerializer.Serialize(fleet.Location, fleet.Location.GetType()),
        fleet.Movement.OrderVersion,
        fleet.Movement.PhaseStartedDay,
        fleet.Movement.PhaseEndsDay,
        fleet.Movement.RouteSystemIds,
        Target = fleet.Movement.Target == null ? null : JsonSerializer.Serialize(fleet.Movement.Target, fleet.Movement.Target.GetType()),
        Pending = fleet.Movement.PendingAction == null ? null : JsonSerializer.Serialize(fleet.Movement.PendingAction, fleet.Movement.PendingAction.GetType())
    })
});
SpaceWorld Day(SpaceWorld world, IReadOnlySet<string>? visible = null) => world.AdvanceDay(world.ElapsedDay, _ => visible ?? all);
SpaceWorld Until(SpaceWorld world, FleetPhase phase)
{
    for (var i = 0; i < 1000; i++)
    {
        if (Fleet(world).Phase == phase) { return world; }
        world = Day(world);
    }
    throw new Exception("Фаза не достигнута.");
}
void Check(bool condition, string message)
{
    if (!condition) { throw new Exception(message); }
    checks++;
}
void Reject(Action action, string code)
{
    try { action(); }
    catch (SpatialRejected error)
    {
        Check(error.Code == code, "Неверный безопасный code.");
        Check(!error.Message.Contains("S01", StringComparison.Ordinal) && !error.Message.Contains("fleet", StringComparison.Ordinal), "Утечка ID.");
        return;
    }
    throw new Exception("Ожидался отказ " + code);
}
var world = Fixture();
Check(world.Systems.Count == 16 && world.Hyperlanes.Count == 24 && world.Bodies.Count == 34 && world.Colonies.Count == 2 && world.Fleets.Count == 2, "Fixture counts.");
Check(Fleet(world).OwnerParticipantId == 7 && world.Fleets[1].OwnerParticipantId == 42 && world.Colonies[1].EconomyPlanetId == 2, "Player slots.");
Check(world.Fleets.All(fleet => fleet.ShipCount == 3), "Ship count.");
foreach (var (elapsed, date) in new[] { (0, new GameDate(2200, 1, 1, 0)), (29, new GameDate(2200, 1, 30, 0)), (30, new GameDate(2200, 2, 1, 1)), (359, new GameDate(2200, 12, 30, 11)), (360, new GameDate(2201, 1, 1, 12)) })
{
    Check(GameCalendar.FromElapsedDay(elapsed) == date, "Calendar boundary.");
}
try { GameCalendar.FromElapsedDay(-1); throw new Exception("Negative day accepted."); }
catch (ArgumentOutOfRangeException) { checks++; }
Check(GameCalendar.FromElapsedDay(int.MaxValue).Year > 2200, "Calendar max.");
try { GameCalendar.FromElapsedDay(long.MaxValue); throw new Exception("Calendar overflow accepted."); }
catch (OverflowException) { checks++; }
try { world.AdvanceDay(1, _ => all); throw new Exception("Wrong day accepted."); }
catch (ArgumentException) { checks++; }
try { SpaceWorld.CreateFixture(1, 1); throw new Exception("Same slots accepted."); }
catch (ArgumentException) { checks++; }
try { ((IList<SystemDefinition>)world.Systems)[0] = world.Systems[1]; throw new Exception("Mutable systems."); }
catch (NotSupportedException) { checks++; }
var snapshot = Snapshot(world);
Reject(() => world.MoveToPoint(42, "fleet:1", "S01", new LocalPoint(1, 0), all), "OUT_OF_SCOPE");
Reject(() => world.Stop(7, "missing", all), "OUT_OF_SCOPE");
Reject(() => world.MoveToSystem(7, "fleet:1", "S02", new HashSet<string> { "S01" }), "OUT_OF_SCOPE");
Reject(() => world.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(double.NaN, 0), all), "INVALID_TARGET");
Reject(() => world.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(double.PositiveInfinity, 0), all), "INVALID_TARGET");
Reject(() => world.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(151, 0), all), "INVALID_TARGET");
Reject(() => world.MoveToSystem(7, "fleet:1", "S16", new HashSet<string> { "S01", "S16" }), "NO_ROUTE");
Check(Snapshot(world) == snapshot, "Rejected commands mutate source.");
Check(Fleet(world.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(150, 0), all)).Phase == FleetPhase.MovingLocal, "Radius boundary allowed.");
Reject(() => world.MoveToSystem(7, "fleet:1", "S02", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "s01", "s02" }), "OUT_OF_SCOPE");
var zero = world.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(0, -100), all);
Check(Fleet(zero).Phase == FleetPhase.Idle && Fleet(zero).Movement.OrderVersion == 1, "Zero target.");
var moving = world.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(0, -96.4), all);
Check(Fleet(moving).Phase == FleetPhase.MovingLocal, "Moving local.");
var arrived = Day(moving);
Check(Fleet(arrived).Phase == FleetPhase.Idle && ((SystemLocation)Fleet(arrived).Location).Position == new LocalPoint(0, -96.4), "Exact local arrival.");
var replaced = moving.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(0, -110), all);
Check(((SystemLocation)Fleet(Day(replaced)).Location).Position.Z < -100 && Fleet(replaced).Movement.OrderVersion == 2, "Replace local order.");
Check(Fleet(moving.Stop(7, "fleet:1", all)).Phase == FleetPhase.Idle, "Stop local.");
Check(world.FindRoute("S01", "S16", all).SequenceEqual(new[] { "S01", "S02", "S03", "S04", "S08", "S12", "S16" }), "Ordinal shortest BFS.");
var route = world.MoveToSystem(7, "fleet:1", "S02", all);
Check(Fleet(route).Phase == FleetPhase.ApproachingExit, "Approach phase.");
var charging = Until(route, FleetPhase.Charging);
Check(Fleet(charging).Movement.PhaseStartedDay % 1 != 0 && Fleet(charging).Movement.PhaseEndsDay == Fleet(charging).Movement.PhaseStartedDay + 15, "Fractional approach remainder.");
Check(Fleet(charging.Stop(7, "fleet:1", all)).Phase == FleetPhase.Idle, "Stop charging.");
Check(Fleet(charging.MoveToPoint(7, "fleet:1", "S01", new LocalPoint(0, 0), all)).Phase == FleetPhase.MovingLocal, "Local cancels charging.");
Check(Fleet(charging.MoveToSystem(7, "fleet:1", "S01", all)).Phase == FleetPhase.Idle, "Current system cancels route.");
var transit = Until(charging, FleetPhase.InTransit);
Check(Fleet(transit).Location is TransitLocation { Progress: > 0 and < 1 }, "Fractional charge remainder in transit.");
Reject(() => transit.MoveToPoint(7, "fleet:1", "S02", new LocalPoint(0, 0), all), "INVALID_PHASE");
var stopping = transit.Stop(7, "fleet:1", all);
Check(Fleet(stopping).Phase == FleetPhase.InTransit && Fleet(stopping).Movement.PendingAction is PendingStop, "Deferred stop.");
var entry = Until(stopping, FleetPhase.Idle);
Check(Fleet(entry).Location is SystemLocation { SystemId: "S02", Position: { X: -140, Z: 0 } }, "Arrival entry.");
var redirected = stopping.MoveToSystem(7, "fleet:1", "S03", all);
Check(Fleet(redirected).Movement.PendingAction is PendingDestination { SystemId: "S03" }, "Replace pending stop.");
Reject(() => redirected.MoveToSystem(7, "fleet:1", "S16", new HashSet<string> { "S01", "S02", "S16" }), "NO_ROUTE");
Check(Fleet(redirected).Movement.PendingAction is PendingDestination { SystemId: "S03" }, "Invalid pending preserved.");
var redirectedAfter = redirected;
while (Fleet(redirectedAfter).Phase == FleetPhase.InTransit) { redirectedAfter = Day(redirectedAfter); }
Check(Fleet(redirectedAfter).Phase == FleetPhase.ApproachingExit && ((SystemLocation)Fleet(redirectedAfter).Location).Position.X > -140, "Transit remainder applies new route.");
var destinationStop = transit.MoveToSystem(7, "fleet:1", "S02", all);
Check(Fleet(destinationStop).Movement.PendingAction is PendingStop, "Current destination defers stop.");
var hidden = new HashSet<string>(all); hidden.Remove("S02");
var lostApproach = route;
while (Fleet(lostApproach).Phase != FleetPhase.Idle) { lostApproach = Day(lostApproach, hidden); }
Check(Fleet(lostApproach).Location is SystemLocation { SystemId: "S01", Position: { X: 140, Z: 0 } }, "Visibility loss stops at exit.");
var lostCharge = charging;
while (Fleet(lostCharge).Phase != FleetPhase.Idle) { lostCharge = Day(lostCharge, hidden); }
Check(Fleet(lostCharge).Location is SystemLocation { SystemId: "S01" }, "Visibility loss stops after charge.");
var lostTransit = transit;
while (Fleet(lostTransit).Phase != FleetPhase.Idle) { lostTransit = Day(lostTransit, hidden); }
Check(Fleet(lostTransit).Location is SystemLocation { SystemId: "S02" }, "Started transit completes despite visibility loss.");
var far = world.MoveToSystem(7, "fleet:1", "S16", all);
var withoutS02 = new HashSet<string>(all); withoutS02.Remove("S02");
Check(world.FindRoute("S01", "S16", withoutS02).Count > 1, "Alternative BFS exists after intermediate loss.");
var farApproachLost = far;
while (Fleet(farApproachLost).Phase != FleetPhase.Idle) { farApproachLost = Day(farApproachLost, withoutS02); }
Check(Fleet(farApproachLost).Location is SystemLocation { SystemId: "S01", Position: { X: 140, Z: 0 } }, "Approach must not reroute automatically.");
var farChargeLost = Until(far, FleetPhase.Charging);
while (Fleet(farChargeLost).Phase != FleetPhase.Idle) { farChargeLost = Day(farChargeLost, withoutS02); }
Check(Fleet(farChargeLost).Location is SystemLocation { SystemId: "S01", Position: { X: 140, Z: 0 } }, "Charging must not reroute automatically.");
var farTransitLost = Until(far, FleetPhase.InTransit);
var withoutS03 = new HashSet<string>(all); withoutS03.Remove("S03");
Check(world.FindRoute("S02", "S16", withoutS03).Count > 1, "Alternative BFS from entry exists.");
while (Fleet(farTransitLost).Phase != FleetPhase.Idle) { farTransitLost = Day(farTransitLost, withoutS03); }
Check(Fleet(farTransitLost).Location is SystemLocation { SystemId: "S02", Position: { X: -140, Z: 0 } }, "Transit arrival must not reroute automatically.");
var final = Until(far, FleetPhase.Idle);
Check(Fleet(final).Location is SystemLocation { SystemId: "S16" }, "Six-hop travel.");
Check(Snapshot(Day(far)) == Snapshot(Day(far)), "Repeatability.");
Check(Snapshot(world) == snapshot && world.ElapsedDay == 0, "Candidates do not mutate fixture.");
var farBefore = Snapshot(far);
try { far.AdvanceDay(far.ElapsedDay, owner => owner == 42 ? throw new InvalidOperationException("Тестовый отказ callback.") : all); throw new Exception("Callback failure swallowed."); }
catch (InvalidOperationException) { checks++; }
Check(Snapshot(far) == farBefore, "Failed candidate mutates source.");
try { ((IList<string>)Fleet(far).Movement.RouteSystemIds)[0] = "S16"; throw new Exception("Mutable route."); }
catch (NotSupportedException) { checks++; }
Console.WriteLine($"M3 spatial/calendar: {checks} проверок PASS.");
