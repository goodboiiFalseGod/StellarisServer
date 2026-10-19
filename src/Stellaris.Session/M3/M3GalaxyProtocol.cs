using System.Text.Json.Serialization;

namespace Stellaris.Session.M3;

public sealed record M3GalaxyScenarioView(string Id, int Stars, IReadOnlyList<string> Shapes,
    decimal MinimumDensity, decimal MaximumDensity, decimal DefaultDensity, int MaximumNebulae = 0,
    int MaximumGuaranteedColonies = 2, Stellaris.Core.Galaxy.GalaxyOptionRange? GatewayFrequency = null,
    Stellaris.Core.Galaxy.GalaxyOptionRange? WormholePairs = null);
/// <summary>Sanitized lobby DTO. Stage: unprepared / awaitingAssets / running.</summary>
public sealed record M3GalaxyView(string Stage, M3GalaxySettings Settings, IReadOnlyList<M3GalaxyScenarioView> Scenarios,
    bool CanPrepare, string ContentProfile, string Coverage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WorldFingerprint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SystemCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? BodyCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HyperlaneCount = null,
    long LobbyRevision = 0, bool StartPending = false);
