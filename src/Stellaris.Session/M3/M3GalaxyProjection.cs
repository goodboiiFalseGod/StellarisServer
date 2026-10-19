namespace Stellaris.Session.M3;

/// <summary>Ограниченная wire-проекция; полный generated world остаётся на server.</summary>
public static class M3GalaxyProjection
{
    public static M3SpatialView Project(M3GalaxyStart start, string? inspectedSystemId, int viewerId, bool isObserver,
        IReadOnlySet<string> permittedSystems, IReadOnlySet<string> permittedBodies, IReadOnlyDictionary<int, int> economyPlanetIds, SpaceWorld? currentWorld = null)
    {
        ArgumentNullException.ThrowIfNull(start); ArgumentNullException.ThrowIfNull(permittedSystems); ArgumentNullException.ThrowIfNull(permittedBodies);
        if (inspectedSystemId is not null && !permittedSystems.Contains(inspectedSystemId))
            throw new ArgumentException("Inspected system недоступна текущей perspective.", nameof(inspectedSystemId));
        var world = currentWorld ?? start.Space;
        var bodies = world.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var details = world.Colonies.Where(c => permittedBodies.Contains(c.BodyId) && permittedSystems.Contains(bodies[c.BodyId].SystemId))
            .Select(c => bodies[c.BodyId].SystemId).ToHashSet(StringComparer.Ordinal);
        if (inspectedSystemId is not null) details.Add(inspectedSystemId);
        var visibleBodies = world.Bodies.Where(b => permittedBodies.Contains(b.Id) &&
            (b.Kind == "star" || details.Contains(b.SystemId))).Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var view = M3Projection.Project(world, viewerId, isObserver, permittedSystems, visibleBodies, economyPlanetIds);
        var systems = start.Generated.Systems.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var names = start.Generated.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var semantics = start.BodySemantics.ToDictionary(b => b.BodyId, StringComparer.Ordinal);
        var belts = start.Generated.Systems.Where(s => permittedSystems.Contains(s.Id) && details.Contains(s.Id))
            .SelectMany(s => s.Belts.Select(b => new M3BeltView(b.Id!, s.Id, b.Type, (double)b.Radius,
                (double)b.Width, (double)b.Density, "world." + M3GalaxyRuntime.BeltClass(b)))).ToArray();
        var gateways = start.Generated.Gateways.Where(g => permittedSystems.Contains(g.SystemId) && details.Contains(g.SystemId))
            .Select(g => new M3GatewayView(g.Id, g.SystemId, new(g.X, g.Z), "object." + g.Type, "Заброшенные врата", "Неактивны")).ToArray();
        var wormholes = start.Generated.Wormholes.Where(w => permittedSystems.Contains(w.SystemId) && details.Contains(w.SystemId))
            .Select(w => new M3WormholeView(w.Id, w.SystemId, new(w.X, w.Z), "object.natural_wormhole", "Червоточина", "Не исследована")).ToArray();
        return view with {
            GalaxyRadius = (double)start.Generated.Request.Plan.Radius,
            Belts = belts.Length == 0 ? null : Array.AsReadOnly(belts),
            Gateways = gateways.Length == 0 ? null : Array.AsReadOnly(gateways),
            Wormholes = wormholes.Length == 0 ? null : Array.AsReadOnly(wormholes),
            Nebulae = Array.AsReadOnly(start.Generated.Nebulae.Nebulae.Select(n => new M3NebulaView(n.Id, n.NameKey,
                new(n.Center.X / 100000d, 0, n.Center.Y / 100000d), n.Radius / 100000d,
                Array.AsReadOnly(n.SystemIds.Where(permittedSystems.Contains).ToArray())))
                .Where(n => n.SystemIds.Count > 0).ToArray()),
            Systems = Array.AsReadOnly(view.Systems.Select(s => s with { StarClassId = systems[s.Id].StarClassId,
                NameKey = systems[s.Id].NameKey, NameSuffix = systems[s.Id].NameSuffix }).ToArray()),
            Bodies = Array.AsReadOnly(view.Bodies.Select(b => semantics.TryGetValue(b.Id, out var semantic)
                ? b with { Kind = semantic.Kind, WorldClassId = semantic.ClassId, ParentBodyId = semantic.ParentBodyId,
                    NameKey = names[b.Id].NameKey, NameSuffix = names[b.Id].NameSuffix, PlanetSize = semantic.PlanetSize,
                    HasRing = details.Contains(b.SystemId) && names[b.Id].HasRing ? true : null,
                    Modifiers = details.Contains(b.SystemId) && semantic.Modifiers is { Count: > 0 } modifiers
                        ? Array.AsReadOnly(modifiers.Select(modifier => new M3BodyModifierView(modifier.Id, modifier.Days, modifier.Id)).ToArray()) : null } : b).ToArray()),
            Exits = Array.AsReadOnly((view.Exits ?? Array.Empty<M3SystemExitView>()).Where(e => details.Contains(e.SystemId)).ToArray()) };
    }
}
