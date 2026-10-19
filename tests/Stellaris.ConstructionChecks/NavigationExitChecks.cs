using System.Collections;
using Stellaris.Session.M3;

static class NavigationExitChecks
{
    public static Task Projection()
    {
        var world = SpaceWorld.CreateFixture(1, 2);
        var systems = world.Systems.Select(system => system.Id).ToHashSet(StringComparer.Ordinal);
        var bodies = world.Bodies.Select(body => body.Id).ToHashSet(StringComparer.Ordinal);
        var economy = new Dictionary<int, int> { [1] = 1, [2] = 4 };
        var all = M3Projection.Project(world, 1, false, systems, bodies, economy);
        Check(all.Exits!.Count == 2 * all.Hyperlanes.Count && all.Exits is IList list && list.IsReadOnly, "Missing directional exits or mutable projection.");
        foreach (var exit in all.Exits)
        {
            var sourceRadius=world.Systems.Single(system=>system.Id==exit.SystemId).Radius;
            Check(systems.Contains(exit.SystemId) && systems.Contains(exit.DestinationSystemId) && exit.Position == world.GetExitPosition(exit.SystemId, exit.DestinationSystemId), "Exit differs from authoritative movement geometry.");
            Check(double.IsFinite(exit.Position.X) && double.IsFinite(exit.Position.Z) && Math.Abs(Math.Sqrt(exit.Position.X * exit.Position.X + exit.Position.Z * exit.Position.Z) - (sourceRadius-SpaceWorld.EXIT_INSET)) < 1e-8, "Exit radius/coordinates changed.");
        }
        Check(world.Systems.All(system=>system.Radius==150), "Fixture system radius changed from150.");
        var lane = world.Hyperlanes[0];
        var pair = M3Projection.Project(world, 1, false, new HashSet<string> { lane.FromSystemId, lane.ToSystemId }, bodies, economy);
        Check(pair.Hyperlanes.Count == 1 && pair.Exits!.Count == 2 && pair.Exits.All(exit => exit.SystemId == lane.FromSystemId && exit.DestinationSystemId == lane.ToSystemId || exit.SystemId == lane.ToSystemId && exit.DestinationSystemId == lane.FromSystemId), "Filtered exit exposes another system.");
        var hidden = M3Projection.Project(world, 1, false, new HashSet<string> { lane.FromSystemId }, bodies, economy);
        Check(hidden.Exits!.Count == 0 && M3Projection.Empty.Exits!.Count == 0, "Hidden endpoint or empty projection leaks exits.");
        foreach (var (from, to) in new[] { (lane.FromSystemId, lane.FromSystemId), (lane.FromSystemId, "unknown"), ("unknown", lane.ToSystemId), (lane.FromSystemId, (string)null!) })
        {
            try { world.GetExitPosition(from, to); }
            catch (ArgumentException) { continue; }
            throw new InvalidOperationException("Invalid adjacent pair produced an exit.");
        }
        return Task.CompletedTask;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
