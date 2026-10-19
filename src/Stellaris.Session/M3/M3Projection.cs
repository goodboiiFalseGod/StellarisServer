namespace Stellaris.Session.M3;

public static class M3Projection
{
    public static M3SpatialView Empty => new(Array.Empty<M3SystemView>(), Array.Empty<HyperlaneDefinition>(), Array.Empty<M3BodyView>(), Array.Empty<M3ColonyView>(), Array.Empty<M3FleetView>(), Array.Empty<M3SystemExitView>());

    public static M3SpatialView Project(SpaceWorld world, int viewerId, bool isObserver, IReadOnlySet<string> permittedSystems, IReadOnlySet<string> permittedBodies, IReadOnlyDictionary<int, int> economyPlanetIds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(permittedSystems);
        ArgumentNullException.ThrowIfNull(permittedBodies);
        ArgumentNullException.ThrowIfNull(economyPlanetIds);
        var systems = new HashSet<string>(world.Systems.Where(system => permittedSystems.Contains(system.Id)).Select(system => system.Id), StringComparer.Ordinal);
        var bodies = world.Bodies.Where(body => systems.Contains(body.SystemId) && permittedBodies.Contains(body.Id)).ToArray();
        var bodyIds = new HashSet<string>(bodies.Select(body => body.Id), StringComparer.Ordinal);
        var systemViews = world.Systems.Where(system => systems.Contains(system.Id)).Select(system => new M3SystemView(system.Id, system.Name, system.GalaxyPosition, system.Radius, bodies.FirstOrDefault(body => body.Kind == "star" && body.SystemId == system.Id)?.Id)).ToArray();
        var lanes = world.Hyperlanes.Where(lane => systems.Contains(lane.FromSystemId) && systems.Contains(lane.ToSystemId)).ToArray();
        var exits = lanes.SelectMany(lane => new[] { new M3SystemExitView(lane.FromSystemId, lane.ToSystemId, world.GetExitPosition(lane.FromSystemId, lane.ToSystemId)), new M3SystemExitView(lane.ToSystemId, lane.FromSystemId, world.GetExitPosition(lane.ToSystemId, lane.FromSystemId)) }).ToArray();
        var bodyViews = bodies.Select(body => new M3BodyView(body.Id, body.SystemId, body.Kind, body.Name, body.Position, body.VisualId, body.Scale, body.OwnerParticipantId)).ToArray();
        var colonies = world.Colonies.Where(colony => bodyIds.Contains(colony.BodyId)).Select(colony => new M3ColonyView(colony.Id, colony.BodyId, colony.OwnerParticipantId, (isObserver || colony.OwnerParticipantId == viewerId) && economyPlanetIds.TryGetValue(colony.OwnerParticipantId, out var planetId) ? planetId : null)).ToArray();
        var fleets = new List<M3FleetView>();
        foreach (var fleet in world.Fleets)
        {
            object location;
            switch (fleet.Location)
            {
                case SystemLocation local when systems.Contains(local.SystemId):
                    location = new M3SystemLocationView(local.SystemId, local.Position);
                    break;
                case TransitLocation transit when systems.Contains(transit.FromSystemId) && systems.Contains(transit.ToSystemId):
                    location = new M3TransitLocationView(transit.FromSystemId, transit.ToSystemId, transit.Progress);
                    break;
                default:
                    continue;
            }
            var movement = fleet.Movement;
            var safeTarget = movement.Target switch { null => true, PointTarget point => systems.Contains(point.SystemId), SystemTarget target => systems.Contains(target.SystemId), _ => false };
            var safePending = movement.PendingAction switch { null or PendingStop => true, PendingDestination destination => systems.Contains(destination.SystemId), _ => false };
            M3MovementView? movementView = null;
            if ((isObserver || fleet.OwnerParticipantId == viewerId) && safeTarget && safePending && movement.RouteSystemIds.All(systems.Contains))
            {
                object? target = movement.Target switch { PointTarget point => new M3PointTargetView(point.SystemId, point.Position), SystemTarget system => new M3SystemTargetView(system.SystemId), _ => null };
                object? pending = movement.PendingAction switch { PendingStop => new M3PendingStopView(), PendingDestination destination => new M3PendingDestinationView(destination.SystemId), _ => null };
                movementView = new M3MovementView(movement.OrderVersion, target, Array.AsReadOnly(movement.RouteSystemIds.ToArray()), movement.PhaseStartedDay, movement.PhaseEndsDay, pending);
            }
            fleets.Add(new M3FleetView(fleet.Id, fleet.OwnerParticipantId, fleet.Name, fleet.VisualId, fleet.ShipCount, Phase(fleet.Phase), location, movementView));
        }
        return new M3SpatialView(Array.AsReadOnly(systemViews), Array.AsReadOnly(lanes), Array.AsReadOnly(bodyViews), Array.AsReadOnly(colonies), Array.AsReadOnly(fleets.ToArray()), Array.AsReadOnly(exits));
    }

    private static string Phase(FleetPhase phase)
    {
        return phase switch { FleetPhase.Idle => "idle", FleetPhase.MovingLocal => "movingLocal", FleetPhase.ApproachingExit => "approachingExit", FleetPhase.Charging => "charging", FleetPhase.InTransit => "inTransit", _ => throw new InvalidOperationException("Неизвестная фаза флота.") };
    }
}
