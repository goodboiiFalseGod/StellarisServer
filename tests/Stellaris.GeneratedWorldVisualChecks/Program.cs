using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Assets;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string code)
{
    try { action(); throw new InvalidOperationException("Expected rejection: " + code); }
    catch (GeneratedWorldVisualException e) { Check(e.Code == code, "Typed rejection " + code); }
}
var input = new[] { "pc_toxic", "pc_continental", "pc_toxic" };
var requirements = new GeneratedWorldVisualRequirements(input); input[0] = "pc_g_star";
Check(requirements.ClassIds.SequenceEqual(new[] { "pc_continental", "pc_toxic" }), "Immutable normalized source class set");
Check(requirements.VisualIdFor("pc_toxic") == "world.pc_toxic", "Exact visual ID mapping");
Reject(() => requirements.VisualIdFor("pc_g_star"), "class-not-requested");
foreach (var id in new[] { "pc_A", "pc_x/../a", "star.g", "pc_x\n" }) Reject(() => new GeneratedWorldVisualRequirements([id]), "requirements");
Reject(() => new GeneratedWorldVisualRequirements([]), "requirements");
Reject(() => new GeneratedWorldVisualRequirements(Enumerable.Repeat("pc_x", 129)), "requirements");
Check(requirements.CanonicalText() == "generated-world-visuals-v1\nSourceFirstNumberedEntityV1\npc_continental\npc_toxic\n", "UTF8 LF canonical requirements vector");
Check(requirements.Fingerprint == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requirements.CanonicalText()))).ToLowerInvariant(), "Requirements digest vector");
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
Check(JsonSerializer.Serialize(requirements, json) == "{\"version\":1,\"policy\":\"SourceFirstNumberedEntityV1\",\"classIds\":[\"pc_continental\",\"pc_toxic\"]}", "Strict requirements wire shape");

if (args.Length != 0)
{
    if (args.Length != 4 || args[0] != "--game-root" || args[2] != "--output") throw new ArgumentException("--game-root PATH --output metadata.json");
    var root = args[1];
    var ids = new[] { "pc_a_star", "pc_alpine", "pc_arctic", "pc_arid", "pc_asteroid", "pc_b_star", "pc_barren", "pc_barren_cold",
        "pc_continental", "pc_desert", "pc_f_star", "pc_frozen", "pc_g_star", "pc_gaia", "pc_gas_giant", "pc_ice_asteroid", "pc_k_star",
        "pc_m_giant_star", "pc_m_star", "pc_molten", "pc_neutron_star", "pc_nuked", "pc_ocean", "pc_pulsar", "pc_rare_crystal_asteroid",
        "pc_savannah", "pc_t_star", "pc_toxic", "pc_tropical", "pc_tundra", "pc_volcanic", "pc_black_hole", "pc_broken" };
    var required = new GeneratedWorldVisualRequirements(ids);
    async Task RejectSource(string changedPath,Func<byte[],byte[]> mutate,string code,string classId)
    {
        try
        {
            await GeneratedWorldVisualSourceCatalog.ResolveAsync(new([classId]),async(p,ct)=>
            {
                var bytes=await File.ReadAllBytesAsync(Path.Combine(root,p),ct); return p==changedPath?mutate(bytes):bytes;
            },p=>File.Exists(Path.Combine(root,p)));
            throw new InvalidOperationException("Expected source rejection "+code);
        }
        catch(GeneratedWorldVisualException error) { Check(error.Code==code,"Source typed gate "+code); }
    }
    await RejectSource("common/planet_classes/00_planet_classes.txt",bytes=>Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("entity = \"toxic_planet\"","entity = \"absent_test_entity\"",StringComparison.Ordinal)),"entity","pc_toxic");
    await RejectSource("gfx/models/planets/planet_clouded.mesh",bytes=>
    {
        var pattern=Encoding.ASCII.GetBytes("PdxMeshPlanet"); var index=bytes.AsSpan().IndexOf(pattern);
        Check(index>=0,"Native material shader marker found"); Encoding.ASCII.GetBytes("UnknownShader").CopyTo(bytes,index); return bytes;
    },"shader","pc_continental");
    await RejectSource("gfx/worldgfx/star_g_class.txt",bytes=>Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("g_class_star","absent_test_light",StringComparison.Ordinal)),"system-lighting","pc_g_star");
    var bindings = await GeneratedWorldVisualSourceCatalog.ResolveAsync(required, (p, ct) => File.ReadAllBytesAsync(Path.Combine(root, p), ct), p => File.Exists(Path.Combine(root, p)));
    Check(bindings.Count == ids.Length + 1 && bindings.Any(binding => binding.ClassId == "pc_black_hole") && bindings.Any(binding => binding.ClassId == "pc_broken"), "All ordinary-pool and MISC native source graphs");
    foreach (var binding in bindings.Where(binding => binding.ClassId != "rings"))
    {
        Check(binding.VisualId == "world." + binding.ClassId && binding.Dependencies.All(p => File.Exists(Path.Combine(root, p))) && binding.Scale > 0, "Source graph " + binding.ClassId);
    }
    var manifest = await new M3AssetManifestBuilder().BuildAsync(root, required);
    var lighting=await GeneratedWorldSystemLighting.ResolveAsync((p,ct)=>File.ReadAllBytesAsync(Path.Combine(root,p),ct));
    Check(lighting.ContainsKey("sc_g")&&lighting.ContainsKey("sc_trinary_1")&&lighting.ContainsKey("sc_black_hole")&&lighting["sc_black_hole"].LightName=="black_hole_light","Source system lighting includes multiple-star and black-hole classes");
    Check(manifest.GeneratedRequirements == required && manifest.Visuals.Count == ids.Length + 4 && manifest.ImporterVersion == 9, "Dynamic manifest requirements/visuals");
    Check(manifest.Files.Select(f => f.RelativePath).Distinct(StringComparer.Ordinal).Count() == manifest.Files.Count, "Deduplicated manifest closure");
    Check(manifest.ProfileDigest == AssetManifestCanonical.ComputeDigest(manifest.ProfileId, manifest.GameVersion, manifest.SchemaVersion, manifest.ImporterVersion, manifest.MaterialProfileVersion, manifest.Files), "Generated canonical digest");
    var old = await new M3AssetManifestBuilder().BuildAsync(root);
    Check(old.GeneratedRequirements is null && old.ProfileId == M3AssetManifestBuilder.PROFILE_ID && old.ImporterVersion == 8 && !JsonSerializer.Serialize(old, json).Contains("generatedRequirements", StringComparison.Ordinal), "Old v10 wire behavior preserved");
    var output = Path.GetFullPath(args[3]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(manifest, json), new UTF8Encoding(false));
    await File.WriteAllTextAsync(Path.ChangeExtension(output,"lighting.json"),JsonSerializer.Serialize(lighting,json),new UTF8Encoding(false));
    Console.WriteLine($"Generated manifest files={manifest.Files.Count} digest={manifest.ProfileDigest}");
}
Console.WriteLine($"{checks} PASS");
