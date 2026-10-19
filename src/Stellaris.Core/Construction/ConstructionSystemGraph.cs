using System.Collections.Frozen;

namespace Stellaris.Core.Construction;

/// <summary>Системные связи и известные факты планет; полнота игрового графа задаётся отдельно.</summary>
public sealed class ConstructionSystemGraph
{
    public string RootBodyId { get; }
    public string SystemId { get; }
    public IReadOnlyDictionary<string, ConstructionContext?> Planets { get; }
    public bool PlanetsComplete { get; }
    public IReadOnlySet<string>? StarBodies { get; }

    public ConstructionSystemGraph(string rootBodyId, string systemId,
        IReadOnlyDictionary<string, ConstructionContext?> planets, bool planetsComplete, IEnumerable<string>? starBodies = null)
    {
        RootBodyId = ConstructionValues.Id(rootBodyId);
        SystemId = ConstructionValues.Id(systemId);
        ArgumentNullException.ThrowIfNull(planets);
        if (planets.Count is 0 or > 10000 || !planets.ContainsKey(rootBodyId)
            || planets.Values.Any(context => context?.SystemGraph is not null))
            throw new ArgumentException("Invalid system planet graph.", nameof(planets));
        Planets = planets.ToFrozenDictionary(pair => ConstructionValues.Id(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        PlanetsComplete = planetsComplete;
        StarBodies = ConstructionContext.Set(starBodies, 10000);
        if (StarBodies?.Any(id => !Planets.ContainsKey(id)) == true) throw new ArgumentException("Star body is outside system graph.");
    }
}
