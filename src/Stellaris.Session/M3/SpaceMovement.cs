namespace Stellaris.Session.M3;

public sealed partial class SpaceWorld
{
    public SpaceWorld MoveToPoint(int participantId, string fleetId, string systemId, LocalPoint target, IReadOnlySet<string> permittedSystems)
    {
        var allowed = Allowed(permittedSystems);
        var fleet = OwnedFleet(participantId, fleetId, allowed);
        RequireVisible(systemId, allowed);
        if (fleet.Location is not SystemLocation location) { throw Reject("INVALID_PHASE"); }
        if (location.SystemId != systemId || !Inside(target, _systems[systemId].Radius)) { throw Reject("INVALID_TARGET"); }
        var version = checked(fleet.Movement.OrderVersion + 1);
        var distance = Distance(location.Position, target);
        var changed = fleet with { Phase = FleetPhase.MovingLocal, Movement = new FleetMovement(version, new PointTarget(systemId, target), Array.Empty<string>(), ElapsedDay, ElapsedDay + distance / SPEED_UNITS_PER_DAY, null) };
        if (distance <= EPSILON) { changed = Idle(changed with { Location = new SystemLocation(systemId, target) }, ElapsedDay); }
        return Replace(changed);
    }

    public SpaceWorld MoveToSystem(int participantId, string fleetId, string targetSystemId, IReadOnlySet<string> permittedSystems)
    {
        var allowed = Allowed(permittedSystems);
        var fleet = OwnedFleet(participantId, fleetId, allowed);
        RequireVisible(targetSystemId, allowed);
        var source = fleet.Location switch { SystemLocation local => local.SystemId, TransitLocation transit => transit.ToSystemId, _ => throw Reject("INVALID_PHASE") };
        var route = Route(source, targetSystemId, allowed);
        var version = checked(fleet.Movement.OrderVersion + 1);
        var movement = fleet.Movement with { OrderVersion = version };
        if (fleet.Location is TransitLocation)
        {
            return Replace(fleet with { Movement = movement with { PendingAction = source == targetSystemId ? new PendingStop() : new PendingDestination(targetSystemId) } });
        }
        if (source == targetSystemId) { return Replace(Idle(fleet with { Movement = movement }, ElapsedDay)); }
        movement = new FleetMovement(version, new SystemTarget(targetSystemId), Array.AsReadOnly(route), ElapsedDay, null, null);
        return Replace(BeginLeg(fleet with { Movement = movement }, ElapsedDay));
    }

    public SpaceWorld Stop(int participantId, string fleetId, IReadOnlySet<string> permittedSystems)
    {
        var fleet = OwnedFleet(participantId, fleetId, Allowed(permittedSystems));
        var movement = fleet.Movement with { OrderVersion = checked(fleet.Movement.OrderVersion + 1) };
        return Replace(fleet.Phase == FleetPhase.InTransit ? fleet with { Movement = movement with { PendingAction = new PendingStop() } } : Idle(fleet with { Movement = movement }, ElapsedDay));
    }

    public SpaceWorld AdvanceDay(int currentElapsedDay, Func<int, IReadOnlySet<string>> permittedSystems)
    {
        ArgumentNullException.ThrowIfNull(permittedSystems);
        if (currentElapsedDay != ElapsedDay) { throw new ArgumentException("Текущий день не совпадает с staged-миром.", nameof(currentElapsedDay)); }
        var nextDay = checked(currentElapsedDay + 1);
        _ = GameCalendar.FromElapsedDay(nextDay);
        var permissions = Fleets.Select(fleet => fleet.OwnerParticipantId).Distinct().ToDictionary(owner => owner, owner => Allowed(permittedSystems(owner)));
        var candidates = Fleets.Select(fleet => AdvanceFleet(fleet, currentElapsedDay, permissions[fleet.OwnerParticipantId])).ToArray();
        return new SpaceWorld(this, candidates, nextDay);
    }

    private FleetState AdvanceFleet(FleetState original, double startDay, HashSet<string> allowed)
    {
        var fleet = original;
        var now = startDay;
        var end = startDay + 1;
        for (var transitions = 0; transitions < Systems.Count * 4 + 8; transitions++)
        {
            if (fleet.Phase == FleetPhase.Idle || now >= end) { return fleet; }
            if (fleet.Phase is FleetPhase.MovingLocal or FleetPhase.ApproachingExit)
            {
                if (fleet.Location is not SystemLocation local) { throw new InvalidOperationException("Локальная фаза без локальной позиции."); }
                var target = fleet.Phase == FleetPhase.MovingLocal && fleet.Movement.Target is PointTarget point ? point.Position : Exit(local.SystemId, fleet.Movement.RouteSystemIds[1]);
                var distance = Distance(local.Position, target);
                var duration = distance <= EPSILON ? 0 : distance / SPEED_UNITS_PER_DAY;
                if (duration > end - now)
                {
                    var fraction = (end - now) / duration;
                    return fleet with { Location = local with { Position = new LocalPoint(local.Position.X + (target.X - local.Position.X) * fraction, local.Position.Z + (target.Z - local.Position.Z) * fraction) } };
                }
                now += duration;
                fleet = fleet with { Location = local with { Position = target } };
                if (fleet.Phase == FleetPhase.MovingLocal) { return Idle(fleet, now); }
                if (!TryAcceptedRoute(fleet, allowed, out var route)) { return Idle(fleet, now); }
                fleet = fleet with { Phase = FleetPhase.Charging, Movement = fleet.Movement with { RouteSystemIds = Array.AsReadOnly(route), PhaseStartedDay = now, PhaseEndsDay = now + CHARGE_DAYS } };
                continue;
            }
            if (fleet.Phase is not (FleetPhase.Charging or FleetPhase.InTransit) || fleet.Movement.PhaseEndsDay is not { } phaseEnd || !double.IsFinite(phaseEnd) || phaseEnd < now)
            {
                throw new InvalidOperationException("Некорректная длительность пространственной фазы.");
            }
            if (phaseEnd > end)
            {
                if (fleet.Location is TransitLocation transit) { fleet = fleet with { Location = transit with { Progress = Math.Clamp((end - fleet.Movement.PhaseStartedDay) / TRANSIT_DAYS, 0, 1) } }; }
                return fleet;
            }
            now = phaseEnd;
            if (fleet.Phase == FleetPhase.Charging)
            {
                if (!TryAcceptedRoute(fleet, allowed, out var route)) { return Idle(fleet, now); }
                fleet = fleet with { Phase = FleetPhase.InTransit, Location = new TransitLocation(route[0], route[1], 0), Movement = fleet.Movement with { RouteSystemIds = Array.AsReadOnly(route), PhaseStartedDay = now, PhaseEndsDay = now + TRANSIT_DAYS } };
                continue;
            }
            if (fleet.Location is not TransitLocation crossing) { throw new InvalidOperationException("Transit не имеет endpoints."); }
            var pending = fleet.Movement.PendingAction;
            fleet = fleet with { Location = new SystemLocation(crossing.ToSystemId, Exit(crossing.ToSystemId, crossing.FromSystemId)), Movement = fleet.Movement with { PendingAction = null, RouteSystemIds = Array.AsReadOnly(fleet.Movement.RouteSystemIds.Skip(1).ToArray()) } };
            if (pending is PendingStop) { return Idle(fleet, now); }
            if (pending is PendingDestination destination)
            {
                fleet = fleet with { Movement = fleet.Movement with { Target = new SystemTarget(destination.SystemId) } };
                if (!allowed.Contains(crossing.ToSystemId) || !allowed.Contains(destination.SystemId)) { return Idle(fleet, now); }
                try
                {
                    fleet = fleet with { Movement = fleet.Movement with { RouteSystemIds = Array.AsReadOnly(Route(crossing.ToSystemId, destination.SystemId, allowed)) } };
                }
                catch (SpatialRejected error) when (error.Code == "NO_ROUTE")
                {
                    return Idle(fleet, now);
                }
            }
            if (fleet.Movement.Target is SystemTarget desired && desired.SystemId == crossing.ToSystemId) { return Idle(fleet, now); }
            if (!TryAcceptedRoute(fleet, allowed, out var nextRoute)) { return Idle(fleet, now); }
            fleet = BeginLeg(fleet with { Movement = fleet.Movement with { RouteSystemIds = Array.AsReadOnly(nextRoute) } }, now);
        }
        throw new InvalidOperationException("Превышен защитный предел переходов фаз за день.");
    }

    private bool TryAcceptedRoute(FleetState fleet, HashSet<string> allowed, out string[] route)
    {
        route = [];
        if (fleet.Location is not SystemLocation local || fleet.Movement.Target is not SystemTarget target) { return false; }
        var accepted = fleet.Movement.RouteSystemIds;
        if (accepted.Count < 2 || accepted[0] != local.SystemId || accepted[^1] != target.SystemId || accepted.Any(id => !allowed.Contains(id))) { return false; }
        for (var index = 1; index < accepted.Count; index++)
        {
            if (!_neighbors[accepted[index - 1]].Contains(accepted[index], StringComparer.Ordinal)) { return false; }
        }
        route = accepted.ToArray();
        return true;
    }

    private FleetState BeginLeg(FleetState fleet, double now)
    {
        if (fleet.Location is not SystemLocation local || fleet.Movement.RouteSystemIds.Count < 2) { throw new InvalidOperationException("Сегмент не имеет локального источника/маршрута."); }
        var distance = Distance(local.Position, Exit(local.SystemId, fleet.Movement.RouteSystemIds[1]));
        var charging = distance <= EPSILON;
        return fleet with { Phase = charging ? FleetPhase.Charging : FleetPhase.ApproachingExit, Movement = fleet.Movement with { PhaseStartedDay = now, PhaseEndsDay = now + (charging ? CHARGE_DAYS : distance / SPEED_UNITS_PER_DAY) } };
    }

    private static FleetState Idle(FleetState fleet, double now)
    {
        return fleet with { Phase = FleetPhase.Idle, Movement = new FleetMovement(fleet.Movement.OrderVersion, null, Array.Empty<string>(), now, null, null) };
    }

    private static double Distance(LocalPoint from, LocalPoint to)
    {
        var x = to.X - from.X;
        var z = to.Z - from.Z;
        var distance = Math.Sqrt(x * x + z * z);
        if (!double.IsFinite(distance)) { throw new InvalidOperationException("Нечисловая дистанция."); }
        return distance;
    }
}
