using System.Globalization;
using Stellaris.Core.Galaxy;

namespace Stellaris.Session.M3;

public sealed partial class SpaceWorld
{
    /// <summary>Presentation policy M3GeneratedDisplayV1: отдельные galaxy/local units, source size/class сохранены.</summary>
    internal static SpaceWorld FromGenerated(GalaxyWorldSnapshot generated, IReadOnlyList<M3GalaxyPlayer> players,
        IReadOnlyDictionary<string, M3StartupColony> startup, IReadOnlyDictionary<string, double> relativeScales)
    {
        var homeByBody = generated.Homes.ToDictionary(h => h.BodyId, StringComparer.Ordinal);
        var bodies = generated.Bodies.Select(b => {
            var home = homeByBody.GetValueOrDefault(b.Id);
            var size = home is null ? b.Size : startup["colony/" + home.ParticipantId.ToString(CultureInfo.InvariantCulture)].Plan.FinalPlanetSize;
            return new BodyDefinition(b.Id, b.SystemId, b.Kind == GalaxyBodyKind.Star ? "star" : "planet",
                b.Name ?? "Название недоступно", new(b.X, b.Y), "world." + b.ClassId,
                // SourceClassCommonScaleV2 — единый source baseline pc_continental; size multiplier остаётся локальной политикой.
                Math.Clamp(size / 20d, .1, 5) * relativeScales[b.ClassId], home?.ParticipantId);
        }).ToList();
        var colonies = new List<ColonyDefinition>(); var fleets = new List<FleetState>();
        foreach (var home in generated.Homes)
        {
            var player = players.Single(p => p.ParticipantId == home.ParticipantId);
            var suffix = home.ParticipantId.ToString(CultureInfo.InvariantCulture);
            var star = bodies.Single(b => b.Id == home.StarId);
            bodies.Add(new("station/" + suffix, home.SystemId, "station", "Станция", new(star.Position.X + 30, star.Position.Z), "station.outpost.mammalian", 1, home.ParticipantId));
            colonies.Add(new("colony/" + suffix, home.BodyId, home.ParticipantId, player.EconomyPlanetId));
            fleets.Add(new("fleet/" + suffix, home.ParticipantId, "Флот " + suffix, "ship.corvette.mammalian", 3, FleetPhase.Idle,
                new SystemLocation(home.SystemId, new(star.Position.X, star.Position.Z - 100)), new(0, null, Array.Empty<string>(), 0, null, null)));
        }
        var systems = generated.Systems.Select((s, index) => new SystemDefinition(s.Id, s.Name ?? "Система " + (index + 1).ToString(CultureInfo.InvariantCulture),
            new(s.Position.X / 100000d, 0, s.Position.Y / 100000d),
            Math.Max(EXIT_RADIUS + 20, bodies.Where(b => b.SystemId == s.Id).Select(b => b.Position)
                .Concat(fleets.Select(f => (SystemLocation)f.Location).Where(l => l.SystemId == s.Id).Select(l => l.Position))
                .Concat(generated.Gateways.Where(g => g.SystemId == s.Id).Select(g => new LocalPoint(g.X, g.Z)))
                .Concat(generated.Wormholes.Where(w => w.SystemId == s.Id).Select(w => new LocalPoint(w.X, w.Z)))
                .Max(p => Math.Sqrt(p.X * p.X + p.Z * p.Z)) + 40))).ToArray();
        var lanes = generated.Lanes.Select(l => StringComparer.Ordinal.Compare(l.A, l.B) < 0
            ? new HyperlaneDefinition(l.Id, l.A, l.B) : new HyperlaneDefinition(l.Id, l.B, l.A)).ToArray();
        return new(systems, lanes, bodies.ToArray(), colonies.ToArray(), fleets.ToArray(), 0);
    }
}
