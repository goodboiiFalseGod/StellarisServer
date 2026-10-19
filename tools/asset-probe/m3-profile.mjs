import { parsePdxMesh, decodeDds, inspectDds } from './formats.mjs';
import { validateStaticBindPose } from './static-pose.mjs';
import { parseVisualDefinitions, selectNamedDefinition, requireScalar, requireBlock, optionalScalar } from './visual-definitions.mjs';
import { PLANET_ASSET_PATHS, PLANET_IMAGE_DEFINITIONS, MAX_PLANET_DECODED_BYTES, parsePlanetImageMetadata, validatePlanetImageInfo } from './m3-planet-assets.mjs';
import { resolveGeneratedWorldVisuals, generatedTexturePlan, GENERATED_DEFINITION_PATHS, GENERATED_OBJECT_DEFINITION_PATHS, generatedObjectDefinitionPaths, GENERATED_RESOURCE_POLICY, generatedRequirementsCanonicalText, discoverGeneratedMeshPaths, parseGeneratedSystemLighting } from './generated-world-visual-profile.mjs';

export const PROFILE_ID = 'm3-static-452-v12';
export function generatedBindingApproximationPolicy(binding) { return binding.approximationPolicy ?? binding.starSurface?.approximationPolicy ?? null; }
const WORLDGFX = 'gfx/worldgfx/';
const PLANET_CLASSES = 'common/planet_classes/00_planet_classes.txt';
const STAR_SURFACE_PATHS = [WORLDGFX + 'lava_noise.dds', WORLDGFX + 'lava_d.dds', WORLDGFX + 'stone_d.dds'];
const SKY_PROFILES = Object.freeze({a_star:'star_a_class.txt',b_star:'star_b_class.txt',f_star:'star_f_class.txt',g_star:'star_g_class.txt',k_star:'star_k_class.txt',m_star:'star_m_class.txt',t_star:'star_t_class.txt',neutron_star:'star_neutron.txt',pulsar:'star_pulsar.txt',black_hole:'star_black_hole.txt'});
const SKY_PATHS = ['gfx/map/sky_core.dds','gfx/map/sky_mid.dds','gfx/map/sky_rim.dds'];
const BACKGROUND_PATHS = ['gfx/worldgfx/stars.dds', ...SKY_PATHS];
const BACKGROUND_OPTIONS = [{ mipLevel: 1, downsampleFactor: 1 }, ...SKY_PATHS.map(() => ({ mipLevel: 0, downsampleFactor: 4 }))];
const GALAXY_LAYERS = [
    ['core', 'gfx/map/center.dds', 1024, 11],
    ['dust', 'gfx/map/dust.dds', 1024, 11],
    ['color', 'gfx/map/galaxycolor.dds', 2048, 12]
];
const UI_SPRITES = [
    ['GFX_topbar_background', 'interface/main.gfx', 'gfx/interface/main/topbar_background.dds', 'corneredTileSpriteType', 36, 36, 1, 8, 8, 'gfx/FX/buttonstate_onlydisable.shader'],
    ['GFX_tile_outliner_bg', 'interface/outliner.gfx', 'gfx/interface/tiles/outliner_tile.dds', 'corneredTileSpriteType', 180, 180, 1, 80, 30, 'gfx/FX/buttonstate_onlydisable.shader'],
    ['GFX_outliner_header_bg', 'interface/government_view.gfx', 'gfx/interface/tiles/green_bg_tile.dds', 'corneredTileSpriteType', 20, 20, 1, 8, 8, 'gfx/FX/buttonstate.shader'],
    ['GFX_outliner_member_bg', 'interface/outliner.gfx', 'gfx/interface/buttons/outliner_entry.dds', 'spriteType', 765, 50, 3, 0, 0, 'gfx/FX/buttonstate_onlydisable.shader'],
    ['GFX_starsystem_panel', 'interface/main.gfx', 'gfx/interface/main/starsystem_panel.dds', 'spriteType', 440, 49, 1, 0, 0, undefined]
];
const MAMMALIAN = 'gfx/models/ships/mammalian_01/';
const STARBASE = 'gfx/models/ships/starbases/';
const PLANETS = 'gfx/models/planets/';
const CORVETTE_ENTITY = MAMMALIAN + '_mammalian_01_ships_entities.asset';
const CORVETTE_GFX = MAMMALIAN + '_mammalian_01_ships_meshes.gfx';
const STATION_ENTITY = STARBASE + '_starbase_entities.asset';
const STATION_GFX = STARBASE + '_starbase_meshes.gfx';
const PLANET_ENTITY = PLANETS + '_planetary_entities.asset';
const PLANET_GFX = PLANETS + '_planetary_meshes.gfx';
const STAR_ENTITY = PLANETS + '_star_entities.asset';
const STAR_GFX = PLANETS + '_star_meshes.gfx';
const ICONS = [
    ['gfx/interface/icons/resources/energy.dds', 'interface/resources.gfx', 'GFX_resource_energy'],
    ['gfx/interface/icons/resources/minerals.dds', 'interface/resources.gfx', 'GFX_resource_minerals'],
    ['gfx/interface/icons/resources/food.dds', 'interface/resources.gfx', 'GFX_resource_food'],
    ['gfx/interface/icons/fleet_size_icon.dds', 'interface/icons.gfx', 'GFX_fleet_size_icon'],
    ['gfx/interface/icons/planet.dds', 'interface/galaxy_view.gfx', 'GFX_colonies']
];
export const PROFILE_PATHS = Object.freeze([...new Set([
    CORVETTE_ENTITY, CORVETTE_GFX, STATION_ENTITY, STATION_GFX, PLANET_ENTITY, PLANET_GFX, STAR_ENTITY, STAR_GFX,
    'common/ship_sizes/00_starbases.txt', 'common/section_templates/starbase.txt',
    'interface/resources.gfx', 'interface/icons.gfx', 'interface/galaxy_view.gfx',
    MAMMALIAN + 'mammalian_01_corvette_S1.mesh', STARBASE + 'mammalian_01_outpost_frame.mesh', STARBASE + 'mammalian_01_outpost.mesh', PLANETS + 'planet_clouded.mesh', PLANETS + 'base_star.mesh',
    MAMMALIAN + 'mammalian_01_corvette_diffuse.dds', MAMMALIAN + 'mammalian_01_corvette_normal.dds', MAMMALIAN + 'mammalian_01_corvette_specular.dds',
    MAMMALIAN + 'mammalian_01_stations_diffuse.dds', MAMMALIAN + 'mammalian_01_stations_normal.dds', MAMMALIAN + 'mammalian_01_stations_specular.dds',
    MAMMALIAN + 'mammalian_01_battleship_diffuse.dds', MAMMALIAN + 'mammalian_01_battleship_normal.dds', MAMMALIAN + 'mammalian_01_battleship_specular.dds',
    PLANETS + 'continental_01_diffuse.dds', PLANETS + 'continental_01_normals.dds', PLANETS + 'continental_01_spec.dds',
    PLANETS + 'poles_1_diffuse.dds', PLANETS + 'poles_1_normal.dds', PLANETS + 'poles_1_specular.dds',
    PLANETS + 'clouds_medium_diffuse.dds', PLANETS + 'clouds_medium_normal.dds', PLANETS + 'clouds_medium_specular.dds', PLANETS + 'clouds_medium_shadow_diffuse.dds',
    'gfx/models/nonormal.dds', 'gfx/models/nospec.dds', ...ICONS.map(([path]) => path),
    WORLDGFX + 'star_g_class.txt', ...STAR_SURFACE_PATHS, PLANET_CLASSES,
    WORLDGFX + 'default.txt', ...Object.values(SKY_PROFILES).map(path=>WORLDGFX+path), ...BACKGROUND_PATHS, 'interface/main.gfx', 'interface/outliner.gfx', 'interface/government_view.gfx', ...UI_SPRITES.map(sprite => sprite[2]),
    ...GALAXY_LAYERS.map(layer => layer[1]), ...PLANET_ASSET_PATHS
])]);
const ALLOWED = new Set(PROFILE_PATHS);
const MAX_DECODED = 512 * 1024 * 1024;
const SHADERS = new Set(['PdxMeshShip', 'PdxMeshPlanet', 'PdxMeshClouds', 'PdxMeshAtmosphere', 'PdxMeshStar', 'PdxMeshAtmosphereStar']);

function fail(message)
{
    throw new Error(`M3 profile: ${message}`);
}

function requirePath(path)
{
    if (typeof path !== 'string' || path.includes('\\') || /[\x00-\x1f\x7f:]/.test(path) || path.startsWith('/') || path.split('/').some(part => part === '..' || part === '.' || !part) || !ALLOWED.has(path)) { fail(`путь вне allowlist: ${String(path)}.`); }
    return path;
}

function exact(value, expected, label)
{
    if (value !== expected) { fail(`${label}: ожидалось ${expected}, получено ${String(value)}.`); }
}

function texturePath(value, folder)
{
    if (typeof value !== 'string') { fail('texture reference не является строкой.'); }
    if (value.includes('/')) { return requirePath(value); }
    if (value === 'nospec.dds' || value === 'nonormal.dds') { return requirePath('gfx/models/' + value); }
    return requirePath(folder + value);
}

function applyOverrides(meshes, definitions, folder)
{
    const result = meshes.map((mesh, meshIndex) => ({ ...mesh, meshIndex, material: { ...mesh.material } }));
    for (const definition of definitions)
    {
        const used = new Set();
        for (const entry of definition.filter(field => field.key === 'meshsettings'))
        {
            if (entry.operator !== '=' || !Array.isArray(entry.value)) { fail('meshsettings должен быть блоком.'); }
            const block = entry.value;
            if (block.some(field => !['name', 'shader', 'texture_diffuse', 'texture_normal', 'texture_specular'].includes(field.key))) { fail('неподдерживаемый meshsettings attribute.'); }
            const name = optionalScalar(block, 'name');
            const target = name === undefined ? '*' : name;
            if (used.has(target)) { fail('повторный active meshsettings override.'); }
            used.add(target);
            const selected = name === undefined ? result : result.filter(mesh => mesh.name === name);
            if (selected.length === 0) { fail(`override неизвестного shape ${String(name)}.`); }
            for (const [field, property] of [['shader', 'shader'], ['texture_diffuse', 'diff'], ['texture_normal', 'n'], ['texture_specular', 'spec']])
            {
                const value = optionalScalar(block, field);
                if (value !== undefined) { for (const mesh of selected) { mesh.material[property] = value; } }
            }
        }
    }
    for (const mesh of result)
    {
        if (!SHADERS.has(mesh.material.shader)) { fail(`неподдерживаемый shader ${mesh.material.shader}.`); }
        for (const channel of ['diff', 'n', 'spec'])
        {
            if (mesh.material[channel] !== undefined) { mesh.material[channel] = texturePath(mesh.material[channel], folder); }
        }
        if (!mesh.material.shader.startsWith('PdxMeshAtmosphere') && ['diff', 'n', 'spec'].some(channel => !mesh.material[channel])) { fail('обязательный texture channel отсутствует.'); }
    }
    return result;
}

function expectedMaterial(mesh, shader, folder, channels)
{
    exact(mesh.material.shader, shader, mesh.name + '.shader');
    for (const [index, channel] of ['diff', 'n', 'spec'].entries())
    {
        exact(mesh.material[channel], channels ? folder + channels[index] : undefined, mesh.name + '.' + channel);
    }
}

function hsvTriple(fields, key)
{
    exact(requireScalar(fields, key), 'hsv', key);
    const index = fields.findIndex(entry => entry.key === key);
    const triple = fields[index + 1];
    if (triple?.key !== null || triple.operator !== null || !Array.isArray(triple.value) || triple.value.length !== 3 || triple.value.some(entry => entry.key !== null || entry.operator !== null || typeof entry.value !== 'number' || !Number.isFinite(entry.value))) { fail(`${key}: expected a finite numeric HSV triple.`); }
    return triple.value.map(entry => entry.value);
}

export function parseStarSurface(text, planetClassesText)
{
    const fields = requireBlock(parseVisualDefinitions(text), 'gfx_settings');
    exact(requireScalar(fields, 'world'), 'g_star', 'gfx_settings.world');
    const surface = {};
    for (const [field, property, path] of [['tex_lava_noise', 'noisePath', STAR_SURFACE_PATHS[0]], ['tex_lava_diffuse', 'lavaPath', STAR_SURFACE_PATHS[1]], ['tex_stone_diffuse', 'stonePath', STAR_SURFACE_PATHS[2]]])
    {
        exact(requireScalar(fields, field), path, field);
        surface[property] = requirePath(path);
    }
    for (const [prefix, property] of [['lava_bright', 'lavaBright'], ['lava_hot_stone', 'lavaHotStone'], ['lava_cold_stone', 'lavaColdStone']])
    {
        surface[property + 'Color'] = hsvTriple(fields, prefix + '_color');
        const intensity = requireScalar(fields, prefix + '_intensity');
        if (typeof intensity !== 'number' || !Number.isFinite(intensity) || intensity < 0) { fail(`${prefix}_intensity: expected a finite nonnegative number.`); }
        surface[property + 'Intensity'] = intensity;
    }
    const atmosphere = requireBlock(parseVisualDefinitions(planetClassesText), 'pc_g_star');
    surface.atmosphereColor = hsvTriple(atmosphere, 'atmosphere_color');
    surface.atmosphereIntensity = requireScalar(atmosphere, 'atmosphere_intensity');
    surface.atmosphereWidth = requireScalar(atmosphere, 'atmosphere_width');
    if (typeof surface.atmosphereIntensity !== 'number' || !Number.isFinite(surface.atmosphereIntensity) || surface.atmosphereIntensity < 0) { fail('atmosphere_intensity: expected a finite nonnegative number.'); }
    if (typeof surface.atmosphereWidth !== 'number' || !Number.isFinite(surface.atmosphereWidth) || surface.atmosphereWidth <= 0) { fail('atmosphere_width: expected a finite positive number.'); }
    return surface;
}

function numericTriple(fields, key)
{
    const entries = requireBlock(fields, key);
    if (entries.length !== 3 || entries.some(entry => entry.key !== null || entry.operator !== null || typeof entry.value !== 'number' || !Number.isFinite(entry.value))) { fail(`${key}: expected a finite numeric triple.`); }
    return entries.map(entry => entry.value);
}

export function parsePresentationBackgrounds(defaultText, starTexts)
{
    const defaultFields = requireBlock(parseVisualDefinitions(defaultText), 'gfx_settings');
    exact(requireScalar(defaultFields, 'world'), 'default', 'default.world');
    const profiles=starTexts instanceof Map?starTexts:new Map([['g_star',starTexts]]);
    function validate(fields, expected)
    {
        const backgrounds = fields.filter(entry => entry.key === 'galaxy_background');
        exact(backgrounds.length, expected.length, 'galaxy_background count');
        for (const [index, [path, ycocg, key, operator, value]] of expected.entries())
        {
            const entry = backgrounds[index];
            if (entry.operator !== '=' || !Array.isArray(entry.value) || entry.value.length !== 3) { fail('Malformed galaxy_background.'); }
            exact(requireScalar(entry.value, 'texture'), path, 'background.texture');
            exact(requireScalar(entry.value, 'ycocg'), ycocg, 'background.ycocg');
            const trigger = requireBlock(entry.value, 'trigger');
            if (trigger.length !== 1 || trigger[0].key !== key || trigger[0].operator !== operator || trigger[0].value !== value) { fail('Unexpected background trigger.'); }
        }
    }
    validate(defaultFields, [[BACKGROUND_PATHS[0], 'no', 'always', '=', 'yes']]);
    const galaxyShift = numericTriple(defaultFields, 'galaxy_background_hsv_shift');
    if (galaxyShift.some(value => value !== 0)) { fail('Default background HSV shift must be zero.'); }
    const result=new Map([['galaxy.stars', { sourcePath: BACKGROUND_PATHS[0], encoding: 'linear-rgb', lod: { version: 1, sourceWidth: 2048, sourceHeight: 2048, ...BACKGROUND_OPTIONS[0] }, hsvShift: galaxyShift }]]);
    for(const [world,file] of Object.entries(SKY_PROFILES))
    {
        const text=profiles.get(world); if(typeof text!=='string') { fail(`Unknown active stellar background profile: ${world}/${file}.`); }
        const fields=requireBlock(parseVisualDefinitions(text),'gfx_settings'); exact(requireScalar(fields,'world'),world,world+'.world');
        validate(fields, [[SKY_PATHS[0], 'yes', 'distance_to_core_percent', '<', .5], [SKY_PATHS[1], 'yes', 'distance_to_core_percent', '<', .75], [SKY_PATHS[2], 'yes', 'always', '=', 'yes']]);
        const hsvShift=numericTriple(fields,'galaxy_background_hsv_shift');
        result.set('system.sky:'+world,{layers:SKY_PATHS.map((sourcePath,index)=>({sourcePath,encoding:'ycocg',lod:{version:1,sourceWidth:2048,sourceHeight:2048,...BACKGROUND_OPTIONS[index+1]},hsvShift})),approximation:'HSV-only/static LUT omission'});
    }
    return result;
}

export function parseUiSpritesMetadata(texts)
{
    if (!(texts instanceof Map)) { fail('UI metadata requires a source-text Map.'); }
    const parsed = new Map();
    const result = new Map();
    for (const [id, source, path, type, width, height, frames, x, y, effect] of UI_SPRITES)
    {
        if (!parsed.has(source)) { parsed.set(source, requireBlock(parseVisualDefinitions(texts.get(source)), 'spriteTypes')); }
        const records = parsed.get(source);
        const matches = records.filter(entry => Array.isArray(entry.value) && entry.value.some(field => field.key === 'name' && field.value === id));
        if (matches.length !== 1 || matches[0].key !== type || matches[0].operator !== '=') { fail(`Ambiguous or invalid sprite type: ${id}.`); }
        const fields = selectNamedDefinition(records, type, id);
        if (fields.some(field => typeof field.key !== 'string' || !['name', 'texturefile', 'noOfFrames', 'borderSize', 'effectFile'].includes(field.key.toLowerCase() === 'texturefile' ? 'texturefile' : field.key))) { fail(`Unsupported selected sprite field: ${id}.`); }
        const textures = fields.filter(field => typeof field.key === 'string' && field.key.toLowerCase() === 'texturefile');
        if (textures.length !== 1 || textures[0].operator !== '=') { fail(`Ambiguous textureFile: ${id}.`); }
        exact(textures[0].value, path, id + '.textureFile');
        const actualFrames = optionalScalar(fields, 'noOfFrames', 1);
        exact(actualFrames, frames, id + '.frames');
        if (!Number.isSafeInteger(actualFrames) || actualFrames < 1 || width % actualFrames !== 0) { fail(`Invalid frame strip: ${id}.`); }
        if (frames === 1 && fields.some(field => field.key === 'noOfFrames')) { fail(`Unexpected explicit frames: ${id}.`); }
        let border = { x: 0, y: 0 };
        if (type === 'corneredTileSpriteType')
        {
            const block = requireBlock(fields, 'borderSize');
            if (block.length !== 2) { fail(`Invalid borderSize: ${id}.`); }
            border = { x: requireScalar(block, 'x'), y: requireScalar(block, 'y') };
        }
        else if (fields.some(field => field.key === 'borderSize')) { fail(`Unexpected borderSize: ${id}.`); }
        exact(border.x, x, id + '.border.x'); exact(border.y, y, id + '.border.y');
        if (!Number.isSafeInteger(border.x) || !Number.isSafeInteger(border.y) || border.x < 0 || border.y < 0 || border.x * 2 > width / frames || border.y * 2 > height) { fail(`Border exceeds frame: ${id}.`); }
        exact(optionalScalar(fields, 'effectFile'), effect, id + '.effectFile');
        result.set(path, { frames: actualFrames, border });
    }
    return result;
}

export function buildM3Profile(buffers, { maxDecodedBytes = MAX_DECODED, generatedRequirements, releaseSourceBuffers = generatedRequirements !== undefined } = {})
{
    if (!(buffers instanceof Map) || !Number.isSafeInteger(maxDecodedBytes) || maxDecodedBytes < 0 || maxDecodedBytes > MAX_DECODED) { fail('неверные buffers/budget.'); }
    let generated;
    if (generatedRequirements !== undefined)
    {
        let encodedBytes=0,geometryBytesReserve=0;
        for(const [path,buffer] of buffers)
        {
            if(typeof path!=='string'||!(buffer instanceof ArrayBuffer)||buffer.byteLength>64*1024*1024) { fail('Generated buffer/type/limit.'); }
            encodedBytes+=buffer.byteLength;
            // Два parse одного mesh (baseline/generated), каждый ≤source+копия индексов.
            if(path.endsWith('.mesh')) { geometryBytesReserve+=buffer.byteLength*4; }
        }
        if(encodedBytes+geometryBytesReserve>maxDecodedBytes) { fail('Generated combined source+geometry preparse budget превышен до allocations.'); }
        generatedRequirementsCanonicalText(generatedRequirements);
        const meshes=discoverGeneratedMeshPaths(generatedRequirements.classIds,p=>buffers.get(p),generatedRequirements.objectIds);
        if(meshes.reduce((sum,p)=>sum+(buffers.get(p)?.byteLength??Infinity)*2,0)>maxDecodedBytes) { fail('Generated geometry reserve превышает budget до parse.'); }
        generated = resolveGeneratedWorldVisuals(generatedRequirements.classIds, path => buffers.get(path), {exists:path=>buffers.has(path),objectIds:generatedRequirements.objectIds});
    }
    const paths = generated ? [...new Set([...PROFILE_PATHS,...GENERATED_DEFINITION_PATHS,...(generatedRequirements.objectIds?generatedObjectDefinitionPaths(generatedRequirements.objectIds):[]),...generated.bindings.flatMap(b=>b.dependencies),...generated.objects.flatMap(b=>b.dependencies)])] : PROFILE_PATHS;
    const allowed = new Set(paths);
    for (const [path, buffer] of buffers)
    {
        if (!allowed.has(path)) { fail('путь вне source-validated allowlist.'); }
        if (!(buffer instanceof ArrayBuffer) || buffer.byteLength > 64 * 1024 * 1024) { fail(`неверный ArrayBuffer/64 MiB: ${path}.`); }
    }
    for (const path of paths) { if (!buffers.has(path)) { fail(`отсутствует ${path}.`); } }
    const lod = generated ? generatedTexturePlan([...generated.bindings,...generated.objects], p=>buffers.get(p)).plan : new Map();
    if (generated)
    {
        for (const path of PROFILE_PATHS.filter(p=>p.startsWith(PLANETS)&&p.endsWith('.dds')))
        {
            if (lod.has(path)) { continue; }
            const info=inspectDds(buffers.get(path),{allowBgraMipChain:true}); let mipLevel=0;
            while(Math.max(info.width>>mipLevel,info.height>>mipLevel)>512&&mipLevel+1<info.mipCount) { mipLevel++; }
            const options={mipLevel,allowBgraMipChain:true}; const selected=inspectDds(buffers.get(path),options);
            if(Math.max(selected.width,selected.height)>512) { fail('Native source mip512 отсутствует.'); }
            lod.set(path,{options});
        }
    }
    let rgbaBytes = 0;
    let planetRgbaBytes = 0;
    const meshPaths = PROFILE_PATHS.filter(path => path.endsWith('.mesh'));
    // До распаковки проверяем ВСЕ DDS и консервативный резерв typed arrays геометрии.
    for (const path of paths.filter(path => path.endsWith('.dds')))
    {
        const info = inspectDds(buffers.get(path), generated ? {allowBgraMipChain:true} : {});
        const index = STAR_SURFACE_PATHS.indexOf(path);
        if (index >= 0)
        {
            exact(info.width, 1024, path + '.width'); exact(info.height, 1024, path + '.height');
            exact(info.codec, index === 1 ? 'DXT5' : 'DXT1', path + '.codec');
            exact(info.faceCount, index === 0 ? 6 : 1, path + '.faces');
            exact(info.mipCount, index === 0 ? 1 : 11, path + '.mips');
        }
        else if (BACKGROUND_PATHS.includes(path))
        {
            const backgroundIndex = BACKGROUND_PATHS.indexOf(path);
            exact(info.width, 2048, path + '.width'); exact(info.height, 2048, path + '.height');
            exact(info.codec, backgroundIndex === 0 ? 'DXT1' : 'DXT5', path + '.codec');
            exact(info.faceCount, 6, path + '.faces'); exact(info.mipCount, backgroundIndex === 0 ? 12 : 1, path + '.mips');
        }
        else { exact(info.faceCount, 1, path + '.faces'); }
        const galaxyLayer = GALAXY_LAYERS.find(layer => layer[1] === path);
        if (galaxyLayer)
        {
            exact(info.width, galaxyLayer[2], path + '.width'); exact(info.height, galaxyLayer[2], path + '.height');
            exact(info.codec, 'DXT5', path + '.codec'); exact(info.mipCount, galaxyLayer[3], path + '.mips');
        }
        const ui = UI_SPRITES.find(sprite => sprite[2] === path);
        if (ui) { exact(info.width, ui[4], path + '.width'); exact(info.height, ui[5], path + '.height'); exact(info.codec, 'BGRA8', path + '.codec'); exact(info.mipCount, 1, path + '.mips'); }
        if (PLANET_IMAGE_DEFINITIONS.some(record => record.sourcePath === path))
        {
            validatePlanetImageInfo(path, info);
            planetRgbaBytes += info.decodedBytes;
            if (planetRgbaBytes > MAX_PLANET_DECODED_BYTES) { fail('Planet images exceed the 16 MiB decoded budget.'); }
        }
        const backgroundIndex = BACKGROUND_PATHS.indexOf(path);
        rgbaBytes += backgroundIndex < 0 ? lod.has(path) ? inspectDds(buffers.get(path),lod.get(path).options).decodedBytes : info.decodedBytes : inspectDds(buffers.get(path), BACKGROUND_OPTIONS[backgroundIndex]).decodedBytes;
    }
    const geometryReserve = paths.filter(p=>p.endsWith('.mesh')).reduce((total, path) => total + buffers.get(path).byteLength * 2, 0);
    if (rgbaBytes + geometryReserve > maxDecodedBytes) { fail('Exceeded decoded memory budget before decoding. Reload the page, then reimport.'); }
    const decoder = new TextDecoder('utf-8', { fatal: true });
    const starSurface = parseStarSurface(decoder.decode(buffers.get(WORLDGFX + 'star_g_class.txt')), decoder.decode(buffers.get(PLANET_CLASSES)));
    const planetImages = parsePlanetImageMetadata(new Map(PLANET_ASSET_PATHS.filter(path => !path.endsWith('.dds')).map(path => [path, decoder.decode(buffers.get(path))])));
    const backgrounds = parsePresentationBackgrounds(decoder.decode(buffers.get(WORLDGFX + 'default.txt')), new Map(Object.entries(SKY_PROFILES).map(([world,file])=>[world,decoder.decode(buffers.get(WORLDGFX+file))])));
    const uiSprites = parseUiSpritesMetadata(new Map(['interface/main.gfx', 'interface/outliner.gfx', 'interface/government_view.gfx'].map(path => [path, decoder.decode(buffers.get(path))])));
    const asts = new Map();
    function ast(path)
    {
        if (!asts.has(path)) { asts.set(path, parseVisualDefinitions(new TextDecoder('utf-8', { fatal: true }).decode(buffers.get(path)))); }
        return asts.get(path);
    }
    function entity(path, name)
    {
        const definition = selectNamedDefinition(ast(path), 'entity', name);
        if (definition.some(entry => entry.key === 'attach')) { fail(`неожиданный attach ${name}.`); }
        return definition;
    }
    function meshDefinition(path, name, expectedFile)
    {
        const definition = selectNamedDefinition(requireBlock(ast(path), 'objectTypes'), 'pdxmesh', name);
        exact(requirePath(requireScalar(definition, 'file')), expectedFile, name + '.file');
        exact(requireScalar(definition, 'scale'), 1, name + '.scale');
        return definition;
    }
    function pair(entityPath, entityName, gfxPath, meshName, file, expectedScale)
    {
        const selected = entity(entityPath, entityName);
        exact(requireScalar(selected, 'pdxmesh'), meshName, entityName + '.pdxmesh');
        exact(optionalScalar(selected, 'scale', 1), expectedScale, entityName + '.scale');
        if (entityName !== 'mammalian_01_starbase_outpost_entity' && selected.some(entry => entry.key === 'locator')) { fail(`неожиданный дополнительный locator ${entityName}.`); }
        return [meshDefinition(gfxPath, meshName, file), selected];
    }
    const corvetteFile = MAMMALIAN + 'mammalian_01_corvette_S1.mesh';
    const planetFile = PLANETS + 'planet_clouded.mesh';
    const starFile = PLANETS + 'base_star.mesh';
    const frameFile = STARBASE + 'mammalian_01_outpost_frame.mesh';
    const stationFile = STARBASE + 'mammalian_01_outpost.mesh';
    const definitions = new Map([
        ['ship.corvette.mammalian', pair(CORVETTE_ENTITY, 'mammalian_01_corvette_S1_entity', CORVETTE_GFX, 'mammalian_01_corvette_S1_mesh', corvetteFile, 1)],
        ['planet.continental', pair(PLANET_ENTITY, 'continental_planet_01_entity', PLANET_GFX, 'planet_clouded_mesh', planetFile, 1)],
        ['star.g', pair(STAR_ENTITY, 'g_star_class_star_entity', STAR_GFX, 'star_mesh', starFile, 1.3)],
        ['station.outpost.mammalian', pair(STATION_ENTITY, 'mammalian_01_starbase_outpost_section_entity', STATION_GFX, 'mammalian_01_outpost_mesh', stationFile, 1)]
    ]);
    const rootDefinitions = pair(STATION_ENTITY, 'mammalian_01_starbase_outpost_entity', STATION_GFX, 'mammalian_01_outpost_frame_mesh', frameFile, 1);
    if (rootDefinitions.some(definition => definition.some(entry => entry.key === 'meshsettings'))) { fail('неожиданный frame material override.'); }
    const rootLocators = rootDefinitions[1].filter(entry => entry.key === 'locator');
    if (rootLocators.length !== 1 || rootLocators[0].operator !== '=' || !Array.isArray(rootLocators[0].value) || rootLocators[0].value.some(entry => !['name', 'position'].includes(entry.key))) { fail('root entity требует единственный root locator.'); }
    exact(requireScalar(rootLocators[0].value, 'name'), 'root', 'root.locator');
    const rootPosition = requireBlock(rootLocators[0].value, 'position');
    if (rootPosition.length !== 3 || rootPosition.some(entry => entry.key !== null || entry.value !== 0)) { fail('неожиданное положение root locator.'); }
    const size = requireBlock(ast('common/ship_sizes/00_starbases.txt'), 'starbase_outpost');
    const slots = requireBlock(size, 'section_slots');
    if (slots.length !== 1 || slots[0].key !== 'core') { fail('ожидался единственный core slot.'); }
    exact(requireScalar(requireBlock(slots, 'core'), 'locator'), 'part1', 'core.locator');
    const section = selectNamedDefinition(ast('common/section_templates/starbase.txt'), 'ship_section_template', 'OUTPOST_STARBASE_SECTION', 'key');
    exact(requireScalar(section, 'ship_size'), 'starbase_outpost', 'section.ship_size');
    exact(requireScalar(section, 'fits_on_slot'), 'core', 'section.slot');
    exact(requireScalar(section, 'entity'), 'starbase_outpost_section_entity', 'section.entity');
    for (const [path, metadata, name] of ICONS)
    {
        const selected = selectNamedDefinition(requireBlock(ast(metadata), 'spriteTypes'), 'spriteType', name);
        const fields = selected.filter(entry => typeof entry.key === 'string' && entry.key.toLowerCase() === 'texturefile');
        if (fields.length !== 1 || fields[0].operator !== '=') { fail(`неоднозначный textureFile ${name}.`); }
        exact(requirePath(fields[0].value), path, name + '.textureFile');
        exact(optionalScalar(selected, 'noOfFrames', 1), 1, name + '.frames');
    }
    const parsedModels = new Map(meshPaths.map(path => [path, parsePdxMesh(buffers.get(path), { locatorOnly: path === frameFile })]));
    const frame = parsedModels.get(frameFile);
    const samples = new Map();
    let ringSample;
    for (const [visualId, file, count, folder, scale] of [
        ['ship.corvette.mammalian', corvetteFile, 1, MAMMALIAN, 1], ['station.outpost.mammalian', stationFile, 3, MAMMALIAN, 1],
        ['planet.continental', planetFile, 5, PLANETS, 1], ['star.g', starFile, 2, PLANETS, 1.3]
    ])
    {
        const parsed = parsedModels.get(file);
        exact(parsed.meshes.length, count, visualId + '.layers');
        const { layers, skinnedLayerCount, skeletonCount } = validateStaticBindPose(parsed);
        const poseReport = { layers, skinnedLayerCount, skeletonCount };
        const meshes = applyOverrides(parsed.meshes, definitions.get(visualId), folder);
        samples.set(visualId, { meshes, images: new Map(), scale, poseReport });
    }
    const corvette = samples.get('ship.corvette.mammalian').meshes[0];
    expectedMaterial(corvette, 'PdxMeshShip', MAMMALIAN, ['mammalian_01_corvette_diffuse.dds', 'mammalian_01_corvette_normal.dds', 'mammalian_01_corvette_specular.dds']);
    const station = samples.get('station.outpost.mammalian');
    for (const [index, prefix] of ['stations', 'corvette', 'battleship'].entries())
    {
        expectedMaterial(station.meshes[index], 'PdxMeshShip', MAMMALIAN, [`mammalian_01_${prefix}_diffuse.dds`, `mammalian_01_${prefix}_normal.dds`, `mammalian_01_${prefix}_specular.dds`]);
    }
    station.position = frame.locators[0].position;
    station.quaternion = frame.locators[0].quaternion;
    const planetMaterials = [
        ['atmosphere_geosphereShape', 'PdxMeshAtmosphere', undefined],
        ['planet_geosphereShape', 'PdxMeshPlanet', ['continental_01_diffuse.dds', 'continental_01_normals.dds', 'continental_01_spec.dds']],
        ['polesShape', 'PdxMeshClouds', ['poles_1_diffuse.dds', 'poles_1_normal.dds', 'poles_1_specular.dds']],
        ['clouds_shadow_geosphereShape', 'PdxMeshClouds', ['clouds_medium_shadow_diffuse.dds', 'gfx/models/nonormal.dds', 'gfx/models/nospec.dds']],
        ['clouds_geosphereShape', 'PdxMeshClouds', ['clouds_medium_diffuse.dds', 'clouds_medium_normal.dds', 'clouds_medium_specular.dds']]
    ];
    for (const [index, [name, shader, channels]] of planetMaterials.entries())
    {
        const mesh = samples.get('planet.continental').meshes[index];
        exact(mesh.name, name, 'planet layer');
        if (index === 3)
        {
            exact(mesh.material.shader, shader, name + '.shader');
            exact(mesh.material.diff, PLANETS + channels[0], name + '.diff');
            exact(mesh.material.n, channels[1], name + '.n');
            exact(mesh.material.spec, channels[2], name + '.spec');
        }
        else { expectedMaterial(mesh, shader, PLANETS, channels); }
    }
    const star = samples.get('star.g');
    star.starSurface = starSurface;
    expectedMaterial(star.meshes[0], 'PdxMeshAtmosphereStar', '', undefined);
    expectedMaterial(star.meshes[1], 'PdxMeshStar', 'gfx/models/', ['nospec.dds', 'nospec.dds', 'nospec.dds']);
    const allocations = new Set();
    function countArrays(value)
    {
        if (ArrayBuffer.isView(value)) { allocations.add(value.buffer); }
        else if (Array.isArray(value)) { value.forEach(countArrays); }
        else if (value && typeof value === 'object') { Object.values(value).forEach(countArrays); }
    }
    for (const parsed of parsedModels.values()) { countArrays(parsed); }
    if (generated) { for (const parsed of generated.parsedMeshes.values()) { countArrays(parsed); } }
    const geometryBytes = [...allocations].reduce((total, buffer) => total + buffer.byteLength, 0);
    if (geometryBytes + rgbaBytes > maxDecodedBytes) { fail('превышен decoded memory budget.'); }
    const sourceBytes=[...buffers.values()].reduce((sum,b)=>sum+b.byteLength,0);
    const ddsPaths=paths.filter(path=>path.endsWith('.dds'));
    let remainingSource=sourceBytes,allocatedRgba=0,decodePeakLiveBytes=sourceBytes+geometryBytes;
    for(const path of ddsPaths)
    {
        const options=lod.get(path)?.options??BACKGROUND_OPTIONS[BACKGROUND_PATHS.indexOf(path)];
        allocatedRgba+=inspectDds(buffers.get(path),options).decodedBytes;
        decodePeakLiveBytes=Math.max(decodePeakLiveBytes,remainingSource+allocatedRgba+geometryBytes);
        if(releaseSourceBuffers) { remainingSource-=buffers.get(path).byteLength; }
    }
    if(generated&&decodePeakLiveBytes>maxDecodedBytes) { fail('Generated source+decoded live staging peak превышает budget до allocations.'); }
    const images=new Map();
    for(const path of ddsPaths)
    {
        images.set(path,decodeDds(buffers.get(path),lod.get(path)?.options??BACKGROUND_OPTIONS[BACKGROUND_PATHS.indexOf(path)]));
        // Worker уже проверил hash; второй проход перечитывает source независимо от этой Map.
        if(releaseSourceBuffers) { buffers.delete(path); }
    }
    for (const background of backgrounds.values()) { if(background.layers) { for(const layer of background.layers) layer.image=images.get(layer.sourcePath); } else { background.image = images.get(background.sourcePath); } }
    for (const [path, sprite] of uiSprites) { sprite.image = images.get(path); }
    for (const sprite of planetImages.values()) { sprite.image = images.get(sprite.sourcePath); }
    const galaxyLayers = new Map(GALAXY_LAYERS.map(([role, sourcePath]) => [role, { image: images.get(sourcePath), sourcePath, encoding: 'raw-rgba' }]));
    for (const path of STAR_SURFACE_PATHS) { star.images.set(path, images.get(path)); }
    for (const sample of samples.values())
    {
        for (const mesh of sample.meshes)
        {
            for (const channel of ['diff', 'n', 'spec'])
            {
                if (mesh.material[channel]) { sample.images.set(mesh.material[channel], images.get(mesh.material[channel])); }
            }
        }
    }
    if (generated)
    {
        for (const binding of generated.bindings)
        {
            const sampleImages=new Map(binding.dependencies.filter(p=>p.endsWith('.dds')).map(p=>[p,images.get(p)]));
            const sample={meshes:binding.meshes,images:sampleImages,scale:binding.scale,poseReport:binding.poseReport,
                sourceClassId:binding.classId,sourceEntityName:binding.entityName,bodyKind:binding.kind,starSurface:binding.starSurface,sourceAtmosphereColor:binding.sourceAtmosphereColor,
                attachments:binding.attachments.map(child=>({...child,images:sampleImages}))};
            if(binding.kind==='decoration') { ringSample=sample; } else { samples.set(binding.visualId,sample); }
        }
        for(const binding of generated.objects)
        {
            const sampleImages=new Map(binding.dependencies.filter(p=>p.endsWith('.dds')).map(p=>[p,images.get(p)]));
            samples.set(binding.visualId,{meshes:binding.meshes,images:sampleImages,scale:binding.scale,poseReport:{staticPose:true},sourceClassId:null,sourceEntityName:binding.entityName,bodyKind:'object',attachments:(binding.attachments??[]).map(child=>({...child,images:sampleImages}))});
        }
    }
    return { samples, icons: new Map(ICONS.map(([path]) => [path, images.get(path)])), backgrounds, uiSprites, galaxyLayers, planetImages,
        ...(generated ? {ringSample,generatedRequirements:structuredClone(generatedRequirements),generatedResourcePolicy:generated.objects.some(binding=>binding.objectId==='natural_wormhole')?GENERATED_RESOURCE_POLICY+'+WormholeNativeMip0V1':GENERATED_RESOURCE_POLICY,systemLighting:parseGeneratedSystemLighting(p=>buffers.get(p)),
            memoryReport:{sourceBytes,decodePeakLiveBytes,sourceReleasePolicy:releaseSourceBuffers?'ReleaseVerifiedDdsAfterDecodeV1':'CallerRetainsSourceBuffers'},
            generatedBindings:new Map(generated.bindings.filter(b=>b.kind!=='decoration').map(b=>[b.visualId,{classId:b.classId,entityName:b.entityName,kind:b.kind,approximationPolicy:generatedBindingApproximationPolicy(b)}])),
            generatedObjects:new Map(generated.objects.map(b=>[b.visualId,{objectId:b.objectId,entityName:b.entityName,kind:b.kind,scalePolicy:b.objectId==='gateway_ruined'?'GatewaySurfaceRadius6V1':'WormholeSurfaceRadius6V1',approximationPolicy:b.approximationPolicy}]))} : {}), decodedBytes: geometryBytes + rgbaBytes };
}
