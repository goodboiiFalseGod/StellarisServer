using Stellaris.Core.Construction;
using Stellaris.Core.Galaxy;

namespace Stellaris.Session.M3.Construction;

public static class M3ConstructionSystemGraph
{
    /// <summary>Связывает только реальные generated тела; station и отложенные flags не становятся фактами.</summary>
    public static ConstructionSystemGraph? Bind(GalaxyWorldSnapshot? generated, SpaceWorld? space, string colonyId,
        IReadOnlyDictionary<string, ConstructionContext> colonyContexts)
    {
        if (generated is null || space is null) return null;
        var root = space.Colonies.SingleOrDefault(colony => colony.Id == colonyId);
        if (root is null) return null;
        var body = generated.Bodies.SingleOrDefault(candidate => candidate.Id == root.BodyId);
        var spatialBody = space.Bodies.SingleOrDefault(candidate => candidate.Id == root.BodyId);
        if (body is null || spatialBody is null || body.Kind == GalaxyBodyKind.Star
            || body.SystemId != spatialBody.SystemId || spatialBody.OwnerParticipantId != root.OwnerParticipantId
            || !generated.Systems.Any(system => system.Id == body.SystemId))
            throw new ArgumentException("Generated construction system binding is inconsistent.");
        var planets = new Dictionary<string, ConstructionContext?>(StringComparer.Ordinal);
        foreach (var member in generated.Bodies.Where(candidate => candidate.SystemId == body.SystemId))
        {
            var colony = space.Colonies.SingleOrDefault(candidate => candidate.BodyId == member.Id);
            // Класс materialized body известен; владельца и отсутствие зданий из него не выводим.
            ConstructionContext? context = new(planetClass: member.ClassId);
            if (colony is not null && colonyContexts.TryGetValue(colony.Id, out var known))
            {
                if (known.SystemGraph is not null || known.ForeignOwnership is not { } ownership
                    || ownership.Planet.Index != colony.EconomyPlanetId)
                    throw new ArgumentException("Construction planet context is not bound to the generated colony.");
                context = known;
            }
            planets.Add(member.Id, context);
        }
        // Producer явно оставляет игровые effects/deferred незавершёнными. Отсутствующие факты не равны нулю.
        return new(body.Id, body.SystemId, planets, planetsComplete: false,
            generated.Bodies.Where(member => member.SystemId == body.SystemId && member.Kind == GalaxyBodyKind.Star).Select(member => member.Id));
    }
}
