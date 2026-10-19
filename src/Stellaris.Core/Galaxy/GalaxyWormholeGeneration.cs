using System.Globalization;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

/// <summary>Детерминированная локальная модель ограниченного game_start.31.</summary>
public static class GalaxyWormholeGeneration
{
    public const string PolicyId = "WormholeEuclideanMapDistanceV1";
    private const long MinimumDistanceRaw = 500L * 100_000;
    private const double SurfaceRadius = 6;

    public static IReadOnlyList<GalaxyWorldWormhole> Generate(IReadOnlyList<GalaxyWorldSystem> systems,
        IReadOnlyList<GalaxyWorldBody> bodies, IReadOnlyList<GalaxyWorldGateway> gateways, IReadOnlySet<string> homeSystems, int factor,
        ref NativeRandomState selectionState, ref NativeRandomState placementState,
        ref long selectionDraws, ref long placementDraws)
    {
        if (factor is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(factor));
        if (factor == 0) return Array.Empty<GalaxyWorldWormhole>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var output = new List<GalaxyWorldWormhole>();
        var placement = placementState; var localPlacementDraws = placementDraws;
        var attemptCount = factor * 5;
        for (var attempt = 0; attempt < attemptCount; attempt++)
        {
            var eligible = systems.Where(system => !used.Contains(system.Id) &&
                !homeSystems.Contains(system.Id) &&
                !system.Flags.Contains("starting_system", StringComparer.Ordinal) &&
                !system.Flags.Contains("empire_cluster", StringComparer.Ordinal)).OrderBy(system => system.Id, StringComparer.Ordinal).ToArray();
            if (eligible.Length < 2) break;
            var firstDraw = Next(ref selectionState); selectionDraws++;
            var first = eligible[(int)(firstDraw % (uint)eligible.Length)];
            var secondCandidates = eligible.Where(candidate => candidate.Id != first.Id && DistanceAtLeast500(first.Position, candidate.Position)).ToArray();
            if (secondCandidates.Length == 0) continue;
            var secondDraw = Next(ref selectionState); selectionDraws++;
            var second = secondCandidates[(int)(secondDraw % (uint)secondCandidates.Length)];
            used.Add(first.Id); used.Add(second.Id);
            var pairId = "wormhole-pair/" + attempt.ToString("D4", CultureInfo.InvariantCulture);
            AddEndpoint(first, pairId); AddEndpoint(second, pairId);
        }
        var ordered = output.OrderBy(endpoint => endpoint.Id, StringComparer.Ordinal).ToArray();
        if (ordered.GroupBy(endpoint => endpoint.SystemId, StringComparer.Ordinal).Any(group => group.Count() != 1) ||
            ordered.GroupBy(endpoint => endpoint.PairId, StringComparer.Ordinal).Any(group => group.Count() != 2))
            throw new InvalidDataException("Wormhole endpoints must be unique and paired.");
        placementState = placement; placementDraws = localPlacementDraws;
        return Array.AsReadOnly(ordered);

        void AddEndpoint(GalaxyWorldSystem system, string pairId)
        {
            var envelope = bodies.Where(body => body.SystemId == system.Id)
                .Select(body => Math.Sqrt(body.X * body.X + body.Y * body.Y) + body.Size / 2d)
                .Concat(gateways.Where(gateway => gateway.SystemId == system.Id)
                    .Select(gateway => Math.Sqrt(gateway.X * gateway.X + gateway.Z * gateway.Z) + gateway.SurfaceRadius))
                .DefaultIfEmpty(0).Max();
            var radius = envelope + 40;
            var draw = Next(ref placement); localPlacementDraws++;
            var angle = (decimal)draw * 360m / 2147483648m;
            var radians = (double)angle * Math.PI / 180d;
            output.Add(new("wormhole/" + system.Id.AsSpan("system/".Length).ToString(), system.Id,
                Math.Cos(radians) * radius, Math.Sin(radians) * radius, angle, pairId, "WormholeOutsideOrbitalEnvelopeV1",
                SurfaceRadius, envelope));
        }
    }

    public static bool DistanceAtLeast500(NativeFixedPoint first, NativeFixedPoint second)
    {
        var dx = (decimal)first.X - second.X; var dy = (decimal)first.Y - second.Y;
        return dx * dx + dy * dy >= (decimal)MinimumDistanceRaw * MinimumDistanceRaw;
    }

    private static uint Next(ref NativeRandomState state)
    {
        var draw = NativeRandom.Next(state); state = draw.NextState; return draw.Value;
    }
}
