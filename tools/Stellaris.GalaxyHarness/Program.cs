using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stellaris.Core.Galaxy;

var json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
try
{
    if (args is ["--options", var optionsRoot])
    {
        var catalog = GalaxyLobbyCatalog.Import(optionsRoot, new([], []));
        Console.WriteLine(JsonSerializer.Serialize(new { catalog.ContentFingerprint, catalog.Scenarios,
            supportedProfile = GalaxyContentProfile.PrototypeOrdinaryV1,
            nebulaCountRange = catalog.Scenarios.Select(s => new { s.Id, minimum = 0, maximum = s.NebulaeDefault }),
            otherSpecialContentSupported = false, catalog.NebulaNamesFile,
            ordinaryPool = GalaxyLobbyCatalog.OrdinaryInitializers, homePool = GalaxyLobbyCatalog.HomeInitializers,
            seedVersion = GalaxySeedBundle.Version }, json));
        return 0;
    }
    if (args is not ["--generate", var root, var worker, var workerSha, var requestPath, var outputPath])
    {
        Console.Error.WriteLine("--options game-root\n--generate game-root triangle-worker worker-sha256 request.json output.json");
        return 2;
    }
    var file = JsonSerializer.Deserialize<RequestFile>(File.ReadAllText(requestPath), json) ?? throw new InvalidDataException("Пустой request.");
    var source = GalaxyLobbyCatalog.Import(root, new(file.HostDlcs ?? [], file.LocalDlcs ?? []));
    var request = new GalaxyLobbyRequest(file.Seed, file.ScenarioId, file.ShapeId, file.HyperlaneDensity,
        file.Players, file.HabitableMultiplier, file.Profile, file.SpecialContent);
    var resolved = source.Resolve(request);
    if (!resolved.Success) { Console.Error.WriteLine(JsonSerializer.Serialize(resolved.Failure, json)); return 1; }
    var backend = new NativeTriangleBackend(Path.GetFullPath(worker), workerSha,
        Path.Combine(Path.GetTempPath(), "galaxy-harness-" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(30));
    var generated = new GalaxyWorldGenerator(source, backend).Generate(resolved.Request!);
    if (!generated.Success) { Console.Error.WriteLine(JsonSerializer.Serialize(generated.Failure, json)); return 1; }
    var world = generated.World!;
    File.WriteAllText(Path.GetFullPath(outputPath), world.ToMetadataJson(), new UTF8Encoding(false));
    Console.WriteLine(JsonSerializer.Serialize(new { world.Readiness, world.WorldFingerprint, resolved.Request!.SettingsFingerprint,
        source.ContentFingerprint, seed = resolved.Request.Seeds.UserSeed, systems = world.Systems.Count,
        bodies = world.Bodies.Count, lanes = world.Lanes.Count, homes = world.Homes.Count, deferred = world.Deferred.Count,
        nebulae = world.Nebulae.Nebulae.Count, nebulaDiagnostics = world.Nebulae.Diagnostics }, json));
    return 0;
}
catch (Exception error) when (error is IOException or ArgumentException or JsonException or InvalidOperationException)
{ Console.Error.WriteLine(error.Message); return 1; }

internal sealed record RequestFile(string? Seed, string ScenarioId, string ShapeId, decimal HyperlaneDensity,
    GalaxyPlayerSlot[] Players, decimal HabitableMultiplier = 1, GalaxyContentProfile Profile = GalaxyContentProfile.PrototypeOrdinaryV1,
    GalaxySpecialContent? SpecialContent = null, string[]? HostDlcs = null, string[]? LocalDlcs = null);
