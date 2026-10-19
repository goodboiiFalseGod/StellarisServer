namespace Stellaris.Session.M3;

public readonly record struct LocalPoint(double X, double Z);
public readonly record struct GalaxyPoint(double X, double Y, double Z);
public readonly record struct GameDate(int Year, int Month, int Day, int CompletedMonths);
public sealed record SystemDefinition(string Id, string Name, GalaxyPoint GalaxyPosition, double Radius);
public sealed record HyperlaneDefinition(string Id, string FromSystemId, string ToSystemId);
public sealed record BodyDefinition(string Id, string SystemId, string Kind, string Name, LocalPoint Position, string VisualId, double Scale, int? OwnerParticipantId = null);
public sealed record ColonyDefinition(string Id, string BodyId, int OwnerParticipantId, int EconomyPlanetId);
public enum FleetPhase { Idle, MovingLocal, ApproachingExit, Charging, InTransit }
public abstract record FleetLocation;
public sealed record SystemLocation(string SystemId, LocalPoint Position) : FleetLocation;
public sealed record TransitLocation(string FromSystemId, string ToSystemId, double Progress) : FleetLocation;
public abstract record MovementTarget;
public sealed record PointTarget(string SystemId, LocalPoint Position) : MovementTarget;
public sealed record SystemTarget(string SystemId) : MovementTarget;
public abstract record PendingAction;
public sealed record PendingStop : PendingAction;
public sealed record PendingDestination(string SystemId) : PendingAction;
public sealed record FleetMovement(long OrderVersion, MovementTarget? Target, IReadOnlyList<string> RouteSystemIds, double PhaseStartedDay, double? PhaseEndsDay, PendingAction? PendingAction);
public sealed record FleetState(string Id, int OwnerParticipantId, string Name, string VisualId, int ShipCount, FleetPhase Phase, FleetLocation Location, FleetMovement Movement);

public sealed class SpatialRejected(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class GameCalendar
{
    public static GameDate FromElapsedDay(long elapsedDay)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedDay);
        return new GameDate(checked((int)(2200 + elapsedDay / 360)), (int)(elapsedDay / 30 % 12 + 1), (int)(elapsedDay % 30 + 1), checked((int)(elapsedDay / 30)));
    }
}
