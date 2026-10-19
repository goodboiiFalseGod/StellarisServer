using System.Globalization;
using System.Text;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Assets;

public sealed record GeneratedWorldVisualBinding(string ClassId, string VisualId, string Kind,
    string EntityName, string EntityPath, string MeshName, string MeshPath, string GfxPath,
    double Scale, IReadOnlyList<string> Dependencies, string? AttachmentPolicy = null);
public sealed record GeneratedWorldObjectBinding(string ObjectId, string VisualId, string EntityName,
    string EntityPath, string MeshName, string MeshPath, string GfxPath, IReadOnlyList<string> Dependencies,
    string ApproximationPolicy);

/// <summary>Ограниченный source graph resolver. Не исполняет scripts и не выбирает generic fallback.</summary>
public static class GeneratedWorldVisualSourceCatalog
{
    public static IReadOnlyList<string> GatewayDefinitionPaths { get; } = Array.AsReadOnly(new[] {
        "common/megastructures/05_gateways.txt",
        "gfx/models/ships/megastructures/gateway/gateway.asset",
        "gfx/models/ships/megastructures/gateway/gateway.gfx" });
    public static IReadOnlyList<string> WormholeDefinitionPaths { get; } = Array.AsReadOnly(new[] {
        "gfx/models/add_ons/_add_ons_entities.asset", "gfx/models/add_ons/_add_ons_meshes.gfx" });
    public static IReadOnlyList<string> ObjectDefinitionPathsFor(IEnumerable<string> objectIds) => objectIds
        .SelectMany(id => id switch { "gateway_ruined" => GatewayDefinitionPaths, "natural_wormhole" => WormholeDefinitionPaths, _ => throw new GeneratedWorldVisualException("object-requirements", id, "Unsupported generated object.") })
        .Distinct(StringComparer.Ordinal).ToArray();
    public static IReadOnlyList<string> DefinitionPaths { get; } = Array.AsReadOnly(new[] {
        "common/planet_classes/00_planet_classes.txt", "common/star_classes/00_star_classes.txt",
        "gfx/models/planets/_planetary_entities.asset", "gfx/models/planets/_planetary_meshes.gfx",
        "gfx/models/planets/_star_entities.asset", "gfx/models/planets/_star_meshes.gfx",
        "gfx/models/planets/distant_stars_planets/_distant_stars_planetary_entities.asset",
        "gfx/models/planets/distant_stars_planets/_distant_stars_planetary_meshes.gfx",
        "gfx/models/planets/distant_stars_planets/_distant_stars_star_entities.asset",
        "gfx/models/planets/distant_stars_planets/_ice_asteroids.gfx",
        "gfx/models/planets/black_hole_new.asset", "gfx/models/planets/black_hole_new.gfx" }.Concat(GeneratedWorldSystemLighting.Paths).Distinct(StringComparer.Ordinal).ToArray());
    private static readonly HashSet<string> Shaders = new(StringComparer.Ordinal) {
        "PdxMeshPlanet", "PdxMeshClouds", "PdxMeshAtmosphere", "PdxMeshStar", "PdxMeshAtmosphereStar",
        "PdxMeshPlanetEmissive", "PdxMeshGasGiant", "PdxMeshMoltenPlanet", "PdxMeshAlphaAdditiveAnimateUV", "PdxMeshAlphaAdditiveAnimateUVNoDissolve",
        "PdxMeshBlackHole", "PdxMeshBlackHoleBillboard", "PdxMeshAlphaAdditive", "PdxMeshTerra" };
    private sealed record Definition(string Path, string Name, ScriptBlock Fields, ScriptBlock Root);

    public static async Task<IReadOnlyList<GeneratedWorldVisualBinding>> ResolveAsync(GeneratedWorldVisualRequirements requirements,
        Func<string, CancellationToken, Task<byte[]>> read, Func<string, bool> exists, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements); ArgumentNullException.ThrowIfNull(read); ArgumentNullException.ThrowIfNull(exists);
        var roots = new Dictionary<string, ScriptBlock>(StringComparer.Ordinal); long definitionBytes=0;
        foreach (var path in DefinitionPaths)
        {
            var bytes = await read(path, cancellationToken);
            definitionBytes+=bytes.Length;
            if (definitionBytes > 16 * 1024 * 1024) throw new GeneratedWorldVisualException("definition-limit", null, "Source definitions суммарно >16MiB.");
            roots.Add(path, GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(bytes), path));
        }
        _ = GeneratedWorldSystemLighting.FromDefinitions(roots);
        var entities = new List<Definition>(); var gfxs = new List<Definition>();
        foreach (var (path, root) in roots)
        {
            if (path.EndsWith(".asset", StringComparison.Ordinal))
                entities.AddRange(root.Named("entity").Select(e => new Definition(path, Scalar(DefinitionBlock(e), "name"), DefinitionBlock(e), root)));
            if (path.EndsWith(".gfx", StringComparison.Ordinal))
                gfxs.AddRange(Block(root,"objectTypes").Named("pdxmesh").Select(e => new Definition(path, Scalar(DefinitionBlock(e), "name"), DefinitionBlock(e), root)));
        }
        var nativeMeshes = new Dictionary<string, IReadOnlyList<GeneratedWorldVisualMeshMetadata.Layer>>(StringComparer.Ordinal);
        var bindings = new List<GeneratedWorldVisualBinding>();
        foreach (var id in requirements.ClassIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var pc = Block(roots[DefinitionPaths[0]],id);
                if (pc.Optional("overlay_entity") is not null) Fail("overlay", id, "Нужен проверенный adapter.");
                var baseName = Scalar(pc, "entity");
                var candidates = entities.Where(e => e.Name == baseName).ToArray();
                if (candidates.Length == 0) candidates = entities.Where(e => e.Name == baseName + "_01_entity").ToArray();
                if (candidates.Length != 1) Fail("entity", id, "Неоднозначный или отсутствующий exact/01 entity.");
                var entity = candidates[0];
                var attachments = entity.Fields.Named("attach").ToArray();
                var expectedAttachments = id switch { "pc_neutron_star" => new[] { "neutron_outbursts_entity" }, "pc_pulsar" => new[] { "pulsar_outbursts_entity" },
                    "pc_black_hole" => new[] { "black_hole_rings_entity", "black_hole_grav_lens_entity", "black_hole_grav_animation_entity" }, _ => Array.Empty<string>() };
                string? attachmentPolicy = null;
                if (id == "pc_black_hole")
                {
                    var locators=entity.Fields.Named("locator").ToArray();
                    if(entity.Name!="black_hole_entity"||baseName!="black_hole_entity"||locators.Length!=1||locators[0].Operator!="="||Scalar(DefinitionBlock(locators[0]),"name")!="planet"||DefinitionBlock(locators[0]).Entries.Count!=1||attachments.Length!=3)
                        Fail("attachment",id,"Неизвестный locator или набор attachments чёрной дыры.");
                    attachmentPolicy="StaticBlackHoleAttachmentsV1";
                }
                else if (entity.Fields.Named("locator").Any() || attachments.Length > 0 && (expectedAttachments.Length == 0 || attachments.Length != 1)) Fail("attachment", id, entity.Name);
                var meshName = Scalar(entity.Fields, "pdxmesh"); var selectedGfx = gfxs.Where(g => g.Name == meshName).ToArray();
                if (selectedGfx.Length != 1) Fail("mesh", id, "Неоднозначный или отсутствующий mesh definition.");
                var gfx = selectedGfx[0]; var meshPath = Scalar(gfx.Fields, "file"); ValidatePath(meshPath);
                if (!meshPath.EndsWith(".mesh", StringComparison.Ordinal)) Fail("mesh", id, meshPath);
                if (!nativeMeshes.TryGetValue(meshPath, out var layers))
                {
                    layers = GeneratedWorldVisualMeshMetadata.Read(await read(meshPath, cancellationToken)); nativeMeshes.Add(meshPath, layers);
                }
                var materials = layers.Select(l => (l.Name, Material: new Dictionary<string, string>(l.Material, StringComparer.Ordinal))).ToArray();
                var folder = Folder(meshPath);
                foreach (var layer in materials) foreach (var channel in new[] { "diff", "n", "spec" })
                    if (layer.Material.TryGetValue(channel, out var value)) layer.Material[channel] = TexturePath(value, folder);
                foreach (var definition in new[] { gfx, entity })
                {
                    var used = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in definition.Fields.Named("meshsettings"))
                    {
                        var fields = entry.AsBlock();
                        if (entry.Operator != "=" || fields.Entries.Any(e => e.Key is not ("name" or "index" or "shader" or "texture_diffuse" or "texture_normal" or "texture_specular"))) Fail("override", id, "Неподдержанный meshsettings.");
                        var name = OptionalScalar(fields,"name"); var indexText = OptionalScalar(fields,"index");
                        int? index = indexText is null ? null : int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : throw new InvalidDataException("Invalid override index.");
                        if (!used.Add(name + ":" + index)) Fail("override", id, "Повторный override.");
                        var selected = materials.Where(m => name is null || m.Name == name || m.Name.EndsWith("|" + name, StringComparison.Ordinal)).ToArray();
                        if (index is not null) selected = selected.Skip(index.Value).Take(1).ToArray();
                        if (selected.Length == 0) Fail("override", id, "Неизвестный shape.");
                        foreach (var (field, channel) in new[] { ("shader", "shader"), ("texture_diffuse", "diff"), ("texture_normal", "n"), ("texture_specular", "spec") })
                        {
                            var value = OptionalScalar(fields,field);
                            if (value is not null) foreach (var layer in selected) layer.Material[channel] = channel == "shader" ? value : TexturePath(value, Folder(definition.Path));
                        }
                    }
                }
                var dependencies = new HashSet<string>(new[] { DefinitionPaths[0], entity.Path, gfx.Path, meshPath }, StringComparer.Ordinal);
                var star = OptionalScalar(pc,"star") == "yes";
                foreach (var layer in materials)
                {
                    if (!layer.Material.TryGetValue("shader", out var shader) || !Shaders.Contains(shader!)) Fail("shader", id, shader ?? "missing");
                    if (!star && shader == "PdxMeshStar") Fail("class-kind", id, "Star shader у planet class.");
                    foreach (var channel in new[] { "diff", "n", "spec" })
                    {
                        if (!layer.Material.TryGetValue(channel, out var path)) { if (!shader!.StartsWith("PdxMeshAtmosphere", StringComparison.Ordinal) && shader != "PdxMeshStar") Fail("texture", id, channel); continue; }
                        if (!exists(path))
                        {
                            var shared = "gfx/models/planets/" + path.Split('/')[^1];
                            if (path.StartsWith("gfx/models/planets/distant_stars_planets/", StringComparison.Ordinal) && exists(shared)) path = shared;
                            else Fail("missing-texture", id, path);
                        }
                        dependencies.Add(path);
                    }
                }
                if (materials.Any(m => m.Material["shader"] == "PdxMeshStar"))
                {
                    var worlds = roots[DefinitionPaths[1]].Entries.Where(e => e.Key.StartsWith("sc_", StringComparison.Ordinal))
                        .Select(DefinitionBlock).Where(b => b.Named("planet").Count() == 1 && Scalar(Block(b,"planet"), "key") == id)
                        .Select(b => Scalar(b, "class")).Distinct(StringComparer.Ordinal).ToArray();
                    if (worlds.Length != 1) Fail("star-world", id, "Неоднозначная lighting family.");
                    var worldFile = worlds[0] switch { "a_star" => "star_a_class.txt", "b_star" => "star_b_class.txt", "f_star" => "star_f_class.txt",
                        "g_star" => "star_g_class.txt", "k_star" => "star_k_class.txt", "m_star" => "star_m_class.txt", "neutron_star" => "star_neutron.txt", "pulsar" => "star_pulsar.txt", _ => null };
                    if (worldFile is null) Fail("star-world", id, worlds[0]);
                    var worldPath = "gfx/worldgfx/" + worldFile;
                    if (!roots.TryGetValue(worldPath, out var worldRoot))
                    {
                        worldRoot = GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(await read(worldPath, cancellationToken)), worldPath); roots.Add(worldPath, worldRoot);
                    }
                    var worldFields = Block(worldRoot,"gfx_settings");
                    if (Scalar(worldFields, "world") != worlds[0]) Fail("star-world", id, "worldgfx tag mismatch.");
                    dependencies.Add(DefinitionPaths[1]); dependencies.Add(worldPath);
                    foreach (var (field, expected) in new[] { ("tex_lava_noise", "gfx/worldgfx/lava_noise.dds"), ("tex_lava_diffuse", "gfx/worldgfx/lava_d.dds"), ("tex_stone_diffuse", "gfx/worldgfx/stone_d.dds") })
                    {
                        if (Scalar(worldFields, field) != expected) Fail("star-texture", id, field); dependencies.Add(expected);
                    }
                    foreach(var prefix in new[] { "lava_bright", "lava_hot_stone", "lava_cold_stone" }) { Hsv(worldFields,prefix+"_color"); Nonnegative(worldFields,prefix+"_intensity",false); }
                    if(id!="pc_neutron_star"||new[]{"atmosphere_color","atmosphere_intensity","atmosphere_width"}.Any(k=>pc.Optional(k) is not null))
                    {
                        Hsv(pc,"atmosphere_color"); Nonnegative(pc,"atmosphere_intensity",false); Nonnegative(pc,"atmosphere_width",true);
                    }
                }
                foreach(var (attach,index) in attachments.Select((value,index)=>(value,index)))
                {
                    var attachFields = attach.AsBlock();
                    if (attach.Operator != "=" || attachFields.Entries.Count != 1 || Scalar(attachFields, "planet") != expectedAttachments[index]) Fail("attachment", id, "Неизвестная binding.");
                    var childName=expectedAttachments[index];
                    var childCandidates = entities.Where(e => e.Name == childName).ToArray();
                    if (childCandidates.Length != 1) Fail("attachment", id, "Неоднозначный child entity.");
                    var child = childCandidates[0];
                    if (child.Fields.Named("attach").Any() || child.Fields.Named("locator").Any() || child.Fields.Named("meshsettings").Any()) Fail("attachment", id, "Неподдержанный child override/attach.");
                    var childMesh = Scalar(child.Fields, "pdxmesh"); var childGfxs = gfxs.Where(g => g.Name == childMesh).ToArray();
                    if (childGfxs.Length != 1) Fail("attachment", id, "Неоднозначный child mesh.");
                    var childGfx = childGfxs[0]; var childPath = Scalar(childGfx.Fields, "file"); ValidatePath(childPath);
                    if(id=="pc_black_hole")
                    {
                        var expectedMesh=childName switch{"black_hole_rings_entity"=>"black_hole_rings_mesh","black_hole_grav_lens_entity"=>"black_hole_grav_lens_mesh",_=>"black_hole_grav_animation_mesh"};
                        var expectedPath=childName switch{"black_hole_rings_entity"=>"gfx/models/planets/black_hole_rings.mesh","black_hole_grav_lens_entity"=>"gfx/models/planets/black_hole_grav_lens.mesh",_=>"gfx/models/planets/black_hole_grav_animation.mesh"};
                        var expectedShader=childName=="black_hole_rings_entity"?"PdxMeshAlphaAdditiveAnimateUVNoDissolve":"PdxMeshBlackHoleBillboard";
                        var meshOverrides=childGfx.Fields.Named("meshsettings").ToArray();
                        if(childMesh!=expectedMesh||childPath!=expectedPath||Scale(child)!=2||meshOverrides.Length!=1)
                            Fail("attachment",id,"Изменилась исходная форма attachment чёрной дыры.");
                        var overrideFields=DefinitionBlock(meshOverrides[0]);
                        if(meshOverrides[0].Operator!="="||overrideFields.Entries.Count!=1||Scalar(overrideFields,"shader")!=expectedShader)
                            Fail("attachment-shader",id,"Изменился статический shader-adapter чёрной дыры.");
                    }
                    if (!nativeMeshes.TryGetValue(childPath, out var childLayers)) { childLayers = GeneratedWorldVisualMeshMetadata.Read(await read(childPath, cancellationToken)); nativeMeshes.Add(childPath, childLayers); }
                    dependencies.Add(childPath); dependencies.Add(child.Path); dependencies.Add(childGfx.Path);
                    foreach (var layer in childLayers)
                    {
                        if (id!="pc_black_hole"&&layer.Material["shader"] != "PdxMeshAlphaAdditiveAnimateUV") Fail("attachment-shader", id, layer.Material["shader"]);
                        foreach (var channel in new[] { "diff", "n", "spec" }) if (layer.Material.TryGetValue(channel, out var value)) dependencies.Add(TexturePath(value, Folder(childPath)));
                    }
                    foreach (var entry in id=="pc_black_hole"?Array.Empty<ScriptEntry>():childGfx.Fields.Named("meshsettings"))
                    {
                        var fields = entry.AsBlock();
                        if (fields.Entries.Count != 1 || fields.Required("shader").AsScalar() != "PdxMeshAlphaAdditiveAnimateUVNoDissolve") Fail("attachment-shader", id, "Неподдержанный child shader override.");
                    }
                    _ = Scale(child); _ = Scale(childGfx);
                }
                if(id=="pc_black_hole")
                    foreach(var path in new[]{"common/star_classes/00_star_classes.txt","gfx/worldgfx/star_black_hole.txt","gfx/lights/star_lights.asset"}) dependencies.Add(path);
                bindings.Add(new(id, requirements.VisualIdFor(id), star ? "star" : "planet", entity.Name, entity.Path, meshName,
                    meshPath, gfx.Path, Scale(entity) * Scale(gfx), Array.AsReadOnly(dependencies.Order(StringComparer.Ordinal).ToArray()),attachmentPolicy));
            }
            catch (Exception e) when (e is FormatException or InvalidDataException or DecoderFallbackException)
            {
                throw new GeneratedWorldVisualException("source-definition", id, e.Message);
            }
        }
        // Общая closure хранится в manifest; class-only requirements остаются прежними.
        var ringEntities = entities.Where(e => e.Name == "gas_giant_rings_01_entity").ToArray();
        var ringGfxs = gfxs.Where(g => g.Name == "gas_giant_rings_01_mesh").ToArray();
        if (ringEntities.Length != 1 || ringGfxs.Length != 1) Fail("ring-binding", "rings", "Неоднозначный source ring entity/mesh.");
        var ringEntity = ringEntities[0]; var ringGfx = ringGfxs[0];
        if (Scalar(ringEntity.Fields, "pdxmesh") != ringGfx.Name || ringEntity.Fields.Entries.Any(e => e.Key is "attach" or "locator" or "meshsettings") || ringGfx.Fields.Named("meshsettings").Any())
            Fail("ring-binding", "rings", "Неподдержанный ring override/attachment.");
        const string ringPath = "gfx/models/planets/gas_giant_rings_01.mesh";
        if (Scalar(ringGfx.Fields, "file") != ringPath) Fail("ring-binding", "rings", "Неизвестный ring mesh path.");
        var ringLayers = GeneratedWorldVisualMeshMetadata.Read(await read(ringPath, cancellationToken));
        var ringDependencies = new HashSet<string>([ringEntity.Path, ringGfx.Path, ringPath], StringComparer.Ordinal);
        if (ringLayers.Count == 0) Fail("ring-binding", "rings", "Пустой ring mesh.");
        foreach (var layer in ringLayers)
        {
            if (layer.Material.GetValueOrDefault("shader") != "PdxMeshPlanetRings") Fail("ring-binding", "rings", "Неизвестный ring shader.");
            foreach (var (channel, basename) in new[] { ("diff", "ring_tiling_diffuse.dds"), ("n", "ring_tiling_normal.dds"), ("spec", "ring_tiling_specular.dds") })
            {
                if (!layer.Material.TryGetValue(channel, out var value)) Fail("ring-binding", "rings", "Нет ring texture: " + channel);
                var path = TexturePath(value!, Folder(ringPath));
                if (path != Folder(ringPath) + basename || !exists(path)) Fail("ring-binding", "rings", "Неизвестная/отсутствующая ring texture.");
                ringDependencies.Add(path);
            }
        }
        bindings.Add(new("rings", "decoration.planet-rings", "decoration", ringEntity.Name, ringEntity.Path,
            ringGfx.Name, ringPath, ringGfx.Path, Scale(ringEntity) * Scale(ringGfx), Array.AsReadOnly(ringDependencies.Order(StringComparer.Ordinal).ToArray())));
        return bindings.AsReadOnly();
    }

    public static async Task<IReadOnlyList<GeneratedWorldObjectBinding>> ResolveObjectsAsync(GeneratedWorldVisualRequirements requirements,
        Func<string, CancellationToken, Task<byte[]>> read, Func<string, bool> exists, CancellationToken cancellationToken = default)
    {
        if (requirements.ObjectIds is null) return Array.Empty<GeneratedWorldObjectBinding>();
        if (requirements.ObjectIds.Any(id => id is not ("gateway_ruined" or "natural_wormhole")) || requirements.ObjectIds.Distinct(StringComparer.Ordinal).Count() != requirements.ObjectIds.Count)
            throw new GeneratedWorldVisualException("object-requirements", null, "Unsupported generated object requirements.");
        var result = new List<GeneratedWorldObjectBinding>();
        foreach (var id in requirements.ObjectIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (id == "gateway_ruined") result.AddRange(await ResolveGatewayObjectAsync(read, exists, cancellationToken));
            else result.Add(await ResolveWormholeObjectAsync(read, exists, cancellationToken));
        }
        return result.AsReadOnly();
    }

    private static async Task<IReadOnlyList<GeneratedWorldObjectBinding>> ResolveGatewayObjectAsync(Func<string, CancellationToken, Task<byte[]>> read,
        Func<string, bool> exists, CancellationToken cancellationToken)
    {
        const string assetPath = "gfx/models/ships/megastructures/gateway/gateway.asset";
        const string gfxPath = "gfx/models/ships/megastructures/gateway/gateway.gfx";
        const string definitionPath = "common/megastructures/05_gateways.txt";
        const string meshPath = "gfx/models/ships/megastructures/gateway/gateway.mesh";
        try
        {
            var gatewayDefinitions = GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(await read(definitionPath, cancellationToken)), definitionPath);
            var gateway = gatewayDefinitions.Named("gateway_ruined").ToArray();
            if (gateway.Length != 1 || Scalar(DefinitionBlock(gateway[0]), "entity") != "deactivated_gateway_entity" ||
                Scalar(DefinitionBlock(gateway[0]), "build_type") != "outside_gravity_well" ||
                Scalar(DefinitionBlock(gateway[0]), "place_entity_on_planet_plane") != "no" ||
                Scalar(DefinitionBlock(gateway[0]), "show_galactic_map_icon") != "no" || Scalar(DefinitionBlock(gateway[0]), "bypass_type") != "gateway")
                Fail("gateway-definition", "gateway_ruined", "Source megastructure definition is outside the supported disabled gateway form.");
            var potential = Block(DefinitionBlock(gateway[0]), "potential");
            if (Scalar(potential, "always") != "no" || potential.Entries.Count != 1) Fail("gateway-definition", "gateway_ruined", "gateway_ruined must remain unavailable.");
            var asset = GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(await read(assetPath, cancellationToken)), assetPath);
            var gfxRoot = GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(await read(gfxPath, cancellationToken)), gfxPath);
            var expectedAnimations = new Dictionary<string, string>(StringComparer.Ordinal) { ["gateway_idle_animation"] = "gateway_idle.anim", ["gateway_death_animation"] = "gateway_death.anim" };
            var animations = asset.Named("animation").ToArray();
            var animationNames = animations.Select(e => Scalar(DefinitionBlock(e), "name")).ToArray();
            if (animations.Length != expectedAnimations.Count || animationNames.Distinct(StringComparer.Ordinal).Count() != expectedAnimations.Count ||
                !animationNames.ToHashSet(StringComparer.Ordinal).SetEquals(expectedAnimations.Keys) || animations.Any(e => e.Operator != "=" ||
                !expectedAnimations.TryGetValue(Scalar(DefinitionBlock(e), "name"), out var file) || Scalar(DefinitionBlock(e), "file") != file))
                Fail("gateway-animation", "gateway_ruined", "Animation metadata references differ from the known static gateway files.");
            var entities = asset.Named("entity").Where(e => Scalar(DefinitionBlock(e), "name") == "deactivated_gateway_entity").ToArray();
            if (entities.Length != 1) Fail("gateway-entity", "gateway_ruined", "deactivated gateway entity missing or ambiguous.");
            var entityFields = DefinitionBlock(entities[0]);
            if (Scalar(entityFields, "pdxmesh") != "gateway_mesh" || Scalar(entityFields, "default_state") != "idle" ||
                entityFields.Named("attach").Any() || entityFields.Named("locator").Any() || HasUnsupportedTransform(entityFields))
                Fail("gateway-entity", "gateway_ruined", "Unsupported deactivated entity binding or attachment.");
            var idle = entityFields.Named("state").Select(DefinitionBlock).SingleOrDefault(b => Scalar(b, "name") == "idle");
            if (idle is null || Scalar(idle, "animation") != "idle" || Scalar(idle, "animation_speed") != "0")
                Fail("gateway-animation", "gateway_ruined", "Idle must be static with speed 0.");
            var death = entityFields.Named("state").Select(DefinitionBlock).SingleOrDefault(b => Scalar(b, "name") == "death");
            if (death is null || Scalar(death, "animation") != "death") Fail("gateway-animation", "gateway_ruined", "Expected known disabled death animation reference.");
            var meshSettings = entityFields.Named("meshsettings").ToArray();
            if (meshSettings.Length != 1) Fail("gateway-material", "gateway_ruined", "Expected one deactivated gateway material binding.");
            var material = DefinitionBlock(meshSettings[0]);
            if (Scalar(material, "name") != "gateway_bottomShapeShape" || Scalar(material, "index") != "0" || Scalar(material, "shader") != "PdxMeshShip")
                Fail("gateway-material", "gateway_ruined", "Unexpected static mesh layer binding.");
            foreach (var (key, file) in new[] { ("texture_diffuse", "fungoid_01_gateway_diffuse.dds"), ("texture_normal", "fungoid_01_gateway_normal.dds"), ("texture_specular", "fungoid_01_gateway_specular.dds") })
                if (Scalar(material, key) != file || !exists("gfx/models/ships/megastructures/gateway/" + file)) Fail("gateway-texture", "gateway_ruined", key);
            var definitions = Block(gfxRoot, "objectTypes").Named("pdxmesh").ToArray();
            var mesh = definitions.Where(e => Scalar(DefinitionBlock(e), "name") == "gateway_mesh").ToArray();
            if (mesh.Length != 1 || Scalar(DefinitionBlock(mesh[0]), "file") != meshPath || HasUnsupportedTransform(DefinitionBlock(mesh[0]))) Fail("gateway-mesh", "gateway_ruined", "Unexpected gateway mesh path or transform.");
            var references = DefinitionBlock(mesh[0]).Named("animation").Select(DefinitionBlock).ToArray();
            if (references.Length != 2 || !references.Any(fields => Scalar(fields, "id") == "idle" && Scalar(fields, "type") == "gateway_idle_animation") ||
                !references.Any(fields => Scalar(fields, "id") == "death" && Scalar(fields, "type") == "gateway_death_animation"))
                Fail("gateway-animation", "gateway_ruined", "Mesh animation references differ from the known idle/death metadata.");
            var layers = GeneratedWorldVisualMeshMetadata.Read(await read(meshPath, cancellationToken));
            if (layers.Count != 1 || layers[0].Name != "gateway_bottomShapeShape" || layers[0].Material.GetValueOrDefault("shader") != "PdxMeshShip")
                Fail("gateway-mesh", "gateway_ruined", "Unexpected mesh layer or shader.");
            var texturePaths = new[] { "gfx/models/ships/megastructures/gateway/fungoid_01_gateway_diffuse.dds", "gfx/models/ships/megastructures/gateway/fungoid_01_gateway_normal.dds", "gfx/models/ships/megastructures/gateway/fungoid_01_gateway_specular.dds" };
            if (texturePaths.Any(path => !exists(path))) Fail("gateway-texture", "gateway_ruined", "Source texture missing.");
            return [new("gateway_ruined", "object.gateway_ruined", "deactivated_gateway_entity", assetPath, "gateway_mesh", meshPath, gfxPath,
                Array.AsReadOnly(new[] { definitionPath, assetPath, gfxPath, meshPath }.Concat(texturePaths).Order(StringComparer.Ordinal).ToArray()), "StaticGatewayBindPoseV1")];
        }
        catch (GeneratedWorldVisualException) { throw; }
        catch (Exception e) when (e is FormatException or InvalidDataException or DecoderFallbackException or InvalidOperationException)
        { throw new GeneratedWorldVisualException("gateway-source-definition", "gateway_ruined", e.Message); }
    }

    private static async Task<GeneratedWorldObjectBinding> ResolveWormholeObjectAsync(Func<string, CancellationToken, Task<byte[]>> read,
        Func<string, bool> exists, CancellationToken cancellationToken)
    {
        const string assetPath = "gfx/models/add_ons/_add_ons_entities.asset";
        const string gfxPath = "gfx/models/add_ons/_add_ons_meshes.gfx";
        const string rootPath = "gfx/models/add_ons/wormhole_rings.mesh";
        const string childPath = "gfx/models/add_ons/wormhole_stretched_ring.mesh";
        try
        {
            var asset = GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(await read(assetPath, cancellationToken)), assetPath);
            var gfxRoot = GeneratedWorldVisualDefinitions.Parse(new UTF8Encoding(false, true).GetString(await read(gfxPath, cancellationToken)), gfxPath);
            var entities = asset.Named("entity").ToArray();
            var rootEntity = entities.Where(e => OptionalScalar(DefinitionBlock(e), "name") == "natural_wormhole_entity").ToArray();
            var childEntity = entities.Where(e => OptionalScalar(DefinitionBlock(e), "name") == "natural_wormhole_stretched_ring_entity").ToArray();
            if (rootEntity.Length != 1 || childEntity.Length != 1) Fail("wormhole-entity", "natural_wormhole", "Expected unique root/child source entities.");
            var root = DefinitionBlock(rootEntity[0]); var child = DefinitionBlock(childEntity[0]);
            if (Scalar(root, "pdxmesh") != "wormhole_rings_mesh" || OptionalScalar(root, "default_state") != "start" || ScaleValue(root) != 12 ||
                Scalar(child, "pdxmesh") != "wormhole_stretched_ring_mesh" || OptionalScalar(child, "default_state") != "start" || ScaleValue(child) != 1 ||
                HasTransformExceptScale(root) || HasTransformExceptScale(child) || root.Named("locator").Any() || child.Named("locator").Any())
                Fail("wormhole-entity", "natural_wormhole", "Source entity names, mesh bindings, default states, or scales changed.");
            var attachments = root.Named("attach").ToArray();
            if (attachments.Length != 1 || attachments[0].Operator != "=" || attachments[0].AsBlock().Entries.Count != 1 || OptionalScalar(attachments[0].AsBlock(), "root") != "natural_wormhole_stretched_ring_entity" || child.Named("attach").Any())
                Fail("wormhole-attachment", "natural_wormhole", "Expected the exact root child attachment.");
            var meshDefs = Block(gfxRoot, "objectTypes").Named("pdxmesh").ToArray();
            var rootGfx = meshDefs.Where(e => OptionalScalar(DefinitionBlock(e), "name") == "wormhole_rings_mesh").ToArray();
            var childGfx = meshDefs.Where(e => OptionalScalar(DefinitionBlock(e), "name") == "wormhole_stretched_ring_mesh").ToArray();
            if (rootGfx.Length != 1 || childGfx.Length != 1 || Scalar(DefinitionBlock(rootGfx[0]), "file") != rootPath || Scalar(DefinitionBlock(childGfx[0]), "file") != childPath)
                Fail("wormhole-mesh", "natural_wormhole", "Expected exact source mesh paths.");
            foreach (var gfx in new[] { DefinitionBlock(rootGfx[0]), DefinitionBlock(childGfx[0]) })
            {
                var settings = gfx.Named("meshsettings").ToArray();
                if (Scalar(gfx, "scale") != "1" || HasUnsupportedTransform(gfx) || gfx.Named("locator").Any() || gfx.Named("attach").Any() || settings.Length != 1 || settings[0].Operator != "=" ||
                    settings[0].AsBlock().Entries.Count != 1 || Scalar(settings[0].AsBlock(), "shader") != "PdxMeshAlphaAdditiveAnimateUVNoDissolve") Fail("wormhole-material", "natural_wormhole", "Expected source unit transform and static NoDissolve shader override.");
            }
            var rootLayers = GeneratedWorldVisualMeshMetadata.Read(await read(rootPath, cancellationToken));
            var childLayers = GeneratedWorldVisualMeshMetadata.Read(await read(childPath, cancellationToken));
            if (!rootLayers.Select(l => l.Name).Order(StringComparer.Ordinal).SequenceEqual(new[] { "above1Shape", "above2Shape" }) ||
                !childLayers.Select(l => l.Name).SequenceEqual(new[] { "above3Shape" }) ||
                !TexturesMatch(rootLayers, "wormhole_rings.dds") || !TexturesMatch(childLayers, "wormhole_stretch.dds"))
                Fail("wormhole-mesh", "natural_wormhole", "Expected source rings/stretched visible layers and shader.");
            var dependencies = new[] { assetPath, gfxPath, rootPath, childPath, "gfx/models/add_ons/wormhole_rings.dds", "gfx/models/add_ons/wormhole_stretch.dds", "gfx/models/nospec.dds" };
            if (dependencies.Any(path => !exists(path))) Fail("wormhole-texture", "natural_wormhole", "Wormhole source dependency missing.");
            return new("natural_wormhole", "object.natural_wormhole", "natural_wormhole_entity", assetPath, "wormhole_rings_mesh", rootPath, gfxPath,
                Array.AsReadOnly(dependencies.Order(StringComparer.Ordinal).ToArray()), "StaticWormholeRingsV1");
        }
        catch (GeneratedWorldVisualException) { throw; }
        catch (Exception e) when (e is FormatException or InvalidDataException or DecoderFallbackException or InvalidOperationException)
        { throw new GeneratedWorldVisualException("wormhole-source-definition", "natural_wormhole", e.Message); }
    }

    private static double ScaleValue(ScriptBlock fields)
    {
        if (!double.TryParse(Scalar(fields, "scale"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value <= 0) throw new InvalidDataException("Invalid source entity scale.");
        return value;
    }
    private static bool HasTransformExceptScale(ScriptBlock fields) => fields.Entries.Any(e => e.Key is "position" or "rotation" or "orientation" or "quaternion");
    private static bool TexturesMatch(IReadOnlyList<GeneratedWorldVisualMeshMetadata.Layer> layers, string diffuse)
    {
        foreach (var layer in layers)
        {
            var material = layer.Material;
            var diffuseValue = material.GetValueOrDefault("diff");
            if (material.GetValueOrDefault("shader") != "PdxMeshAlphaAdditiveAnimateUV" ||
                diffuseValue != diffuse && diffuseValue != "gfx/models/add_ons/" + diffuse ||
                material.GetValueOrDefault("spec") is not ("nospec.dds" or "gfx/models/nospec.dds") || material.ContainsKey("n") ||
                material.Keys.Any(key => key is not ("shader" or "diff" or "spec"))) return false;
        }
        return layers.Count > 0;
    }

    private static string Scalar(ScriptBlock fields, string key)
    {
        var entry = fields.Required(key); if (entry.Operator != "=") throw new InvalidDataException("Требуется оператор =."); return entry.AsScalar();
    }
    private static bool HasUnsupportedTransform(ScriptBlock fields)
    {
        if (fields.Entries.Any(e => e.Key is "position" or "rotation" or "orientation" or "quaternion")) return true;
        var scales = fields.Named("scale").ToArray();
        return scales.Length > 1 || scales.Any(entry => entry.Operator != "=" ||
            !double.TryParse(entry.AsScalar(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value != 1);
    }
    private static double Scale(Definition definition)
    {
        var value = OptionalScalar(definition.Fields,"scale") ?? "1";
        if (value.StartsWith('@')) value = Scalar(definition.Root, value);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) || !double.IsFinite(scale) || scale <= 0) throw new InvalidDataException("Entity/mesh scale.");
        return scale;
    }
    private static void ValidatePath(string path)
    {
        AssetManifestCanonical.ValidateRelativePath(path);
        if (!path.StartsWith("gfx/", StringComparison.Ordinal) || path.Any(char.IsWhiteSpace)) throw new InvalidDataException("Dependency outside gfx.");
    }
    private static string Folder(string path) => path[..(path.LastIndexOf('/') + 1)];
    private static string TexturePath(string value, string folder)
    {
        var path = value.Contains('/') ? value : value is "nospec.dds" or "nonormal.dds" ? "gfx/models/" + value : folder + value;
        ValidatePath(path); if(!path.EndsWith(".dds",StringComparison.Ordinal)) throw new InvalidDataException("Texture extension."); return path;
    }
    private static ScriptBlock DefinitionBlock(ScriptEntry entry) { if(entry.Operator!="=") throw new InvalidDataException("Требуется оператор =."); return entry.AsBlock(); }
    private static ScriptBlock Block(ScriptBlock fields,string key)=>DefinitionBlock(fields.Required(key));
    private static string? OptionalScalar(ScriptBlock fields,string key) { var entry=fields.Optional(key); if(entry is null)return null; if(entry.Operator!="=")throw new InvalidDataException("Требуется оператор =."); return entry.AsScalar(); }
    private static void Nonnegative(ScriptBlock fields,string key,bool positive)
    {
        if(!double.TryParse(Scalar(fields,key),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value)||value<0||positive&&value==0)throw new InvalidDataException("Invalid source intensity/width.");
    }
    private static void Hsv(ScriptBlock fields,string key)
    {
        if(Scalar(fields,key)!="hsv") throw new InvalidDataException("Invalid source HSV.");
        var index=fields.Entries.ToList().FindIndex(e=>e.Key==key);
        if(index+1>=fields.Entries.Count)throw new InvalidDataException("Missing source HSV triple.");
        var triple=fields.Entries[index+1];
        if(triple.Key!=""||triple.Operator!=""||triple.Block?.Entries.Count!=3||triple.Block.Entries.Any(e=>e.Key!=""||e.Operator!=""||!double.TryParse(e.Scalar,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)||!double.IsFinite(v)))throw new InvalidDataException("Invalid source HSV triple.");
    }
    private static void Fail(string code, string id, string detail) => throw new GeneratedWorldVisualException(code, id, detail);
}
