import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { PROFILE_ID, PROFILE_PATHS, buildM3Profile, parseStarSurface, parsePresentationBackgrounds, parseUiSpritesMetadata } from './m3-profile.mjs';
import { parsePdxMesh, inspectDds, decodeDds } from './formats.mjs';
import { PLANET_ASSET_PATHS, PLANET_IMAGE_DEFINITIONS, parsePlanetImageMetadata } from './m3-planet-assets.mjs';
import { collectTransferBuffers } from './m3-worker-pipeline.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';

const root = process.env.STELLARIS_GAME_ROOT;
const arrayBuffer = bytes => bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
let realBuffers;
async function loadRealBuffers()
{
    if (!realBuffers)
    {
        realBuffers = new Map(await Promise.all(PROFILE_PATHS.map(async path => [path, arrayBuffer(await readFile(resolve(root, path)))])));
    }
    return realBuffers;
}

test('M3 profile: явная уникальная allowlist без animation/исполняемых shaders', () =>
{
    assert.equal(PROFILE_ID, 'm3-static-452-v12');
    assert.equal(PROFILE_PATHS.length, 390);
    assert.equal(PLANET_ASSET_PATHS.length, 322);
    assert.equal(new Set(PROFILE_PATHS).size, PROFILE_PATHS.length);
    assert.ok(Object.isFrozen(PROFILE_PATHS));
    assert.ok(PROFILE_PATHS.every(path => /\.(asset|gfx|mesh|dds|txt)$/.test(path) && !path.includes('..')));
    assert.ok(!PROFILE_PATHS.some(path => path.includes('military_station_small')));
});

test('M3 profile: missing/extra/path traversal/type/лимит источника отклоняются до декодирования', () =>
{
    assert.throws(() => buildM3Profile({}), /buffers\/budget/);
    assert.throws(() => buildM3Profile(new Map()), /отсутствует/);
    for (const path of ['../outside.dds', '/absolute.dds', 'C:/private.dds', 'gfx\\models\\nospec.dds', 'gfx/models/not-allowed.dds', 'gfx/models/\n.dds'])
    {
        assert.throws(() => buildM3Profile(new Map([[path, new ArrayBuffer(0)]])), /allowlist/);
    }
    assert.throws(() => buildM3Profile(new Map([[PROFILE_PATHS[0], new Uint8Array(0)]])), /ArrayBuffer/);
    assert.throws(() => buildM3Profile(new Map([[PROFILE_PATHS[0], new ArrayBuffer(64 * 1024 * 1024 + 1)]])), /64 MiB/);
    for (const maxDecodedBytes of [-1, Infinity, 0.5, 512 * 1024 * 1024 + 1]) { assert.throws(() => buildM3Profile(new Map(), { maxDecodedBytes }), /budget/); }
});

test('M3 profile: DDS preflight и общий budget проверяются до parse/decode', () =>
{
    function buffers()
    {
        return new Map(PROFILE_PATHS.map(path =>
        {
            const buffer = path.endsWith('.dds') ? syntheticDds(path) : new TextEncoder().encode(syntheticPlanetText(path)).buffer;
            return [path, buffer];
        }));
    }
    assert.throws(() => buildM3Profile(buffers(), { maxDecodedBytes: 0 }), /memory budget/);
    const invalid = buffers();
    new DataView(invalid.get('gfx/models/nospec.dds')).setUint32(16, 8193, true);
    assert.throws(() => buildM3Profile(invalid), /DDS:.*размеры/);
    const truncated = buffers(); truncated.set('gfx/models/nospec.dds', new ArrayBuffer(127));
    assert.throws(() => buildM3Profile(truncated), /DDS:.*заголовок/);
    const cube = buffers(); cube.set('gfx/worldgfx/lava_noise.dds', syntheticDds('gfx/worldgfx/lava_noise.dds', { faces: 1 }));
    assert.throws(() => buildM3Profile(cube), /\.faces/);
    const wrongMips = buffers(); wrongMips.set('gfx/worldgfx/lava_d.dds', syntheticDds('gfx/worldgfx/lava_d.dds', { mips: 1 }));
    assert.throws(() => buildM3Profile(wrongMips), /\.mips/);
    const wrongCodec = buffers(); wrongCodec.set('gfx/worldgfx/stone_d.dds', syntheticDds('gfx/worldgfx/stone_d.dds', { codec: 'DXT5' }));
    assert.throws(() => buildM3Profile(wrongCodec), /\.codec/);
    // Noise requires 24 MiB across six faces, not 4 MiB for one face.
    const total = PROFILE_PATHS.filter(path => path.endsWith('.dds')).reduce((sum, path) => sum + inspectDds(syntheticDds(path), path.endsWith('/stars.dds') ? { mipLevel: 1 } : /\/sky_(core|mid|rim)\.dds$/.test(path) ? { downsampleFactor: 4 } : {}).decodedBytes, 0);
    assert.throws(() => buildM3Profile(buffers(), { maxDecodedBytes: total - 1 }), /memory budget before decoding.*Reload the page, then reimport/);
    for (const [path, overrides, reason] of [
        ['gfx/worldgfx/stars.dds', { width: 1024, height: 1024, mips: 11 }, /\.width/],
        ['gfx/worldgfx/stars.dds', { mips: 1 }, /\.mips/],
        ['gfx/map/sky_core.dds', { codec: 'DXT1' }, /\.codec/],
        ['gfx/map/sky_core.dds', { faces: 1 }, /\.faces/],
        ['gfx/map/center.dds', { width: 512, height: 512, mips: 10 }, /\.width/],
        ['gfx/map/dust.dds', { height: 512 }, /\.height/],
        ['gfx/map/galaxycolor.dds', { width: 1024, height: 1024, mips: 11 }, /\.width/],
        ['gfx/map/center.dds', { codec: 'DXT1' }, /\.codec/],
        ['gfx/map/dust.dds', { faces: 6 }, /\.faces/],
        ['gfx/map/galaxycolor.dds', { mips: 1 }, /\.mips/],
        ['gfx/interface/buttons/outliner_entry.dds', { width: 764 }, /\.width/],
        ['gfx/interface/main/topbar_background.dds', { codec: 'DXT1' }, /\.codec/]
    ])
    {
        const changed = buffers(); changed.set(path, syntheticDds(path, overrides));
        assert.throws(() => buildM3Profile(changed), reason);
    }
    for (const path of ['gfx/map/center.dds', 'gfx/map/dust.dds', 'gfx/map/galaxycolor.dds'])
    {
        const valid = syntheticDds(path);
        for (const malformed of [valid.slice(0, -1), new Uint8Array(valid.byteLength + 1).buffer])
        {
            if (malformed.byteLength > valid.byteLength) { new Uint8Array(malformed).set(new Uint8Array(valid)); }
            const changed = buffers(); changed.set(path, malformed);
            assert.throws(() => buildM3Profile(changed, { maxDecodedBytes: 0 }), /DDS:/);
        }
        const volume = valid.slice(0); new DataView(volume).setUint32(112, 0x200000, true);
        const changed = buffers(); changed.set(path, volume);
        assert.throws(() => buildM3Profile(changed, { maxDecodedBytes: 0 }), /DDS:/);
    }
    const invalidUniform = buffers(); invalidUniform.set('gfx/worldgfx/star_g_class.txt', new TextEncoder().encode(starSettings().replace('lava_bright_intensity = 1', 'lava_bright_intensity = -1')).buffer);
    assert.throws(() => buildM3Profile(invalidUniform), /nonnegative/);
});

function syntheticDds(path, overrides = {})
{
    const world = path.startsWith('gfx/worldgfx/');
    const background = path.endsWith('/stars.dds') || /\/sky_(core|mid|rim)\.dds$/.test(path);
    const galaxy = ['gfx/map/center.dds', 'gfx/map/dust.dds', 'gfx/map/galaxycolor.dds'].includes(path);
    const ui = uiDefinitions.find(sprite => sprite.path === path);
    const planetImage = PLANET_IMAGE_DEFINITIONS.find(image => image.sourcePath === path);
    const width = overrides.width ?? (planetImage ? planetImage.width : ui ? ui.width : background || path.endsWith('/galaxycolor.dds') ? 2048 : world || galaxy ? 1024 : 1), height = overrides.height ?? (planetImage ? planetImage.height : ui ? ui.height : width);
    const codec = overrides.codec ?? (planetImage ? planetImage.codec : ui ? 'BGRA8' : galaxy || path.endsWith('lava_d.dds') || /\/sky_(core|mid|rim)\.dds$/.test(path) ? 'DXT5' : 'DXT1');
    const faces = overrides.faces ?? (path.endsWith('lava_noise.dds') || background ? 6 : 1);
    const mips = overrides.mips ?? (path.endsWith('/stars.dds') || path.endsWith('/galaxycolor.dds') ? 12 : galaxy || world && faces === 1 ? 11 : 1);
    let payload = 0;
    for (let index = 0; index < mips; index++) { payload += codec === 'BGRA8' ? Math.max(1, width >> index) * Math.max(1, height >> index) * 4 : Math.ceil(Math.max(1, width >> index) / 4) * Math.ceil(Math.max(1, height >> index) / 4) * (codec === 'DXT1' ? 8 : 16); }
    const buffer = new ArrayBuffer(128 + payload * faces), view = new DataView(buffer);
    for (const [offset, value] of [[0, 0x20534444], [4, 124], [12, height], [16, width], [28, mips], [76, 32], [80, 4], [112, faces === 6 ? 0xfe00 : 0]]) { view.setUint32(offset, value, true); }
    for (let index = 0; index < 4; index++) { view.setUint8(84 + index, codec.charCodeAt(index)); }
    if (codec === 'BGRA8')
    {
        for (const [offset, value] of [[8, 8], [20, width * 4], [80, 0x41], [84, 0], [88, 32], [92, 0x00ff0000], [96, 0x0000ff00], [100, 0x000000ff], [104, 0xff000000]]) { view.setUint32(offset, value, true); }
    }
    return buffer;
}
function starSettings()
{
    return `gfx_settings = { world = g_star
    tex_lava_noise = "gfx/worldgfx/lava_noise.dds" tex_lava_diffuse = "gfx/worldgfx/lava_d.dds" tex_stone_diffuse = "gfx/worldgfx/stone_d.dds"
    lava_bright_color = hsv { .45 .3 .5 } lava_bright_intensity = 1
    lava_hot_stone_color = hsv { .15 .7 1 } lava_hot_stone_intensity = 1
    lava_cold_stone_color = hsv { .09 .85 .9 } lava_cold_stone_intensity = 1
    galaxy_background = { texture = "gfx/map/sky_core.dds" trigger = { distance_to_core_percent < .5 } ycocg = yes }
    galaxy_background = { texture = "gfx/map/sky_mid.dds" trigger = { distance_to_core_percent < .75 } ycocg = yes }
    galaxy_background = { texture = "gfx/map/sky_rim.dds" trigger = { always = yes } ycocg = yes }
    galaxy_background_hsv_shift = { -.45 -.1 -.3 } }`;
}
const atmosphereSettings = () => 'pc_g_star = { atmosphere_color = hsv { .09 .7 .7 } atmosphere_intensity = .5 atmosphere_width = 1.9 }';
const defaultBackgroundSettings = () => 'gfx_settings = { world = default galaxy_background = { texture = "gfx/worldgfx/stars.dds" trigger = { always = yes } ycocg = no } galaxy_background_hsv_shift = { 0 0 0 } }';
const starBackgroundSettings = (world='g_star',shift='-.45 -.1 -.3') => `gfx_settings = { world = ${world}
galaxy_background = { texture = "gfx/map/sky_core.dds" trigger = { distance_to_core_percent < .5 } ycocg = yes }
galaxy_background = { texture = "gfx/map/sky_mid.dds" trigger = { distance_to_core_percent < .75 } ycocg = yes }
galaxy_background = { texture = "gfx/map/sky_rim.dds" trigger = { always = yes } ycocg = yes }
galaxy_background_hsv_shift = { ${shift} } }`;
const uiDefinitions = [
    { id: 'GFX_topbar_background', source: 'interface/main.gfx', path: 'gfx/interface/main/topbar_background.dds', type: 'corneredTileSpriteType', width: 36, height: 36, border: [8, 8], effect: 'gfx/FX/buttonstate_onlydisable.shader' },
    { id: 'GFX_tile_outliner_bg', source: 'interface/outliner.gfx', path: 'gfx/interface/tiles/outliner_tile.dds', type: 'corneredTileSpriteType', width: 180, height: 180, border: [80, 30], effect: 'gfx/FX/buttonstate_onlydisable.shader' },
    { id: 'GFX_outliner_header_bg', source: 'interface/government_view.gfx', path: 'gfx/interface/tiles/green_bg_tile.dds', type: 'corneredTileSpriteType', width: 20, height: 20, border: [8, 8], effect: 'gfx/FX/buttonstate.shader' },
    { id: 'GFX_outliner_member_bg', source: 'interface/outliner.gfx', path: 'gfx/interface/buttons/outliner_entry.dds', type: 'spriteType', width: 765, height: 50, frames: 3, effect: 'gfx/FX/buttonstate_onlydisable.shader' },
    { id: 'GFX_starsystem_panel', source: 'interface/main.gfx', path: 'gfx/interface/main/starsystem_panel.dds', type: 'spriteType', width: 440, height: 49 }
];
function syntheticPlanetText(path)
{
    if(path==='common/planet_classes/00_planet_classes.txt') return 'pc_continental = { entity = continental_planet } pc_g_star = { atmosphere_color = hsv { .09 .7 .7 } atmosphere_intensity = .5 atmosphere_width = 1.9 }';
    if(path==='gfx/worldgfx/default.txt') return defaultBackgroundSettings();
    if(['interface/main.gfx','interface/outliner.gfx','interface/government_view.gfx'].includes(path)) return uiTexts().get(path);
    if(path==='gfx/worldgfx/star_g_class.txt') return starSettings();
    const skyProfile=Object.entries({a_star:'star_a_class.txt',b_star:'star_b_class.txt',f_star:'star_f_class.txt',k_star:'star_k_class.txt',m_star:'star_m_class.txt',t_star:'star_t_class.txt',neutron_star:'star_neutron.txt',pulsar:'star_pulsar.txt',black_hole:'star_black_hole.txt'}).find(([,file])=>path==='gfx/worldgfx/'+file);
    if(skyProfile) return starBackgroundSettings(skyProfile[0],'-.'+String(Object.keys({a_star:1,b_star:1,f_star:1,k_star:1,m_star:1,t_star:1,neutron_star:1,pulsar:1,black_hole:1}).indexOf(skyProfile[0])+1)+' 0 .1');
    if (path === 'gfx/portraits/portraits/00_portraits_main.txt')
        return 'planet = { size = { x = 800 y = 400 } sky = { orientation = lower_left position = { x = 0 y = 0 } } '
            + 'environment = { orientation = lower_left position = { x = 0 y = 0 } } '.repeat(4) + '}';
    const definitions = PLANET_IMAGE_DEFINITIONS.filter(record => record.definitionPath === path).map(record =>
    {
        let body = '';
        if (record.category === 'job' && record.sourceStratum) { body = `category = ${record.sourceStratum} icon = "${record.sourceIcon}"`; }
        else if (record.category === 'zone')
        {
            body = record.inlinePath
                ? `inline_script = { script = "${record.inlinePath.slice('common/inline_scripts/'.length, -4)}" }`
                : `icon = "${record.spriteId}"`;
        }
        else if (record.category === 'district') { body = `icon = "${record.id}"`; }
        else if (record.category === 'building' || record.category === 'job') { body = `icon = "${record.sourceIcon}"`; }
        else if (record.category === 'blocker' || record.category === 'feature') { body = `icon = "${record.sourcePath.split('/').at(-1).slice(0, -4)}"`; }
        return `${record.id} = { ${body} }`;
    });
    const sprites = new Map();
    for (const record of PLANET_IMAGE_DEFINITIONS.filter(record => record.spriteSource === path))
    {
        const border = record.type === 'corneredTileSpriteType' ? ` borderSize = { x = ${record.border.x} y = ${record.border.y} } effectFile = "gfx/FX/buttonstate_onlydisable.shader"` : '';
        sprites.set(record.spriteId, `${record.type} = { name = "${record.spriteId}" textureFile = "${record.sourcePath}"${border} }`);
    }
    definitions.push(...sprites.values());
    const inline = [...new Set(PLANET_IMAGE_DEFINITIONS.filter(record => record.inlinePath === path).map(record => record.spriteId))].map(spriteId => `icon = "${spriteId}"`);
    if (path === 'common/game_concepts/01_job_concepts.txt')
    {
        definitions.push(...PLANET_IMAGE_DEFINITIONS.filter(record => record.conceptId).map(record => `${record.conceptId} = { ${record.conceptAlias ? `alias = { ${record.conceptAlias} } ` : ''}icon = "${record.sourcePath}" }`));
    }
    if (path === 'interface/planet_view.gfx' || path.endsWith('.gfx')) { return `spriteTypes = { ${definitions.join(' ')} }`; }
    return [...definitions, ...inline].join('\n');
}

test('Planet image metadata: synthetic source resolves all 296 exact keys and descriptors', () =>
{
    const texts = new Map(PLANET_ASSET_PATHS.filter(path => !path.endsWith('.dds')).map(path => [path, syntheticPlanetText(path)]));
    const parsed = parsePlanetImageMetadata(texts);
    assert.equal(parsed.size, 296);
    for (const record of PLANET_IMAGE_DEFINITIONS)
    {
        assert.deepEqual(parsed.get(record.key), { sourcePath: record.sourcePath, spriteId: record.spriteId, frames: 1, frame: 1, border: record.border });
    }
    const ddsRecords = PLANET_IMAGE_DEFINITIONS.map(record => syntheticDds(record.sourcePath));
    assert.ok(ddsRecords.every((buffer, index) =>
    {
        const info = inspectDds(buffer);
        const record = PLANET_IMAGE_DEFINITIONS[index];
        return info.width === record.width && info.height === record.height;
    }));
});
function uiTexts()
{
    return new Map(['interface/main.gfx', 'interface/outliner.gfx', 'interface/government_view.gfx'].map(source => [source, 'spriteTypes = { ' + uiDefinitions.filter(sprite => sprite.source === source).map(sprite => `${sprite.type} = { name = "${sprite.id}" textureFile = "${sprite.path}" ${sprite.frames ? 'noOfFrames = ' + sprite.frames : ''} ${sprite.border ? `borderSize = { x = ${sprite.border[0]} y = ${sprite.border[1]} }` : ''} ${sprite.effect ? `effectFile = "${sprite.effect}"` : ''} }`).join(' ') + ' }']));
}

test('Presentation backgrounds: exact ordered dependencies, triggers, encoding and finite HSV', () =>
{
    const defaults = defaultBackgroundSettings(), profileFiles={a_star:'star_a_class.txt',b_star:'star_b_class.txt',f_star:'star_f_class.txt',g_star:'star_g_class.txt',k_star:'star_k_class.txt',m_star:'star_m_class.txt',t_star:'star_t_class.txt',neutron_star:'star_neutron.txt',pulsar:'star_pulsar.txt',black_hole:'star_black_hole.txt'},profiles=new Map(Object.entries(profileFiles).map(([world],index)=>[world,world==='g_star'?starSettings():starBackgroundSettings(world,`${-((index+1)/100)} 0 .1`)]));
    const backgrounds = parsePresentationBackgrounds(defaults, profiles);
    assert.equal(backgrounds.size,11);
    assert.deepEqual(backgrounds.get('system.sky:g_star').layers.map(layer=>layer.sourcePath), ['gfx/map/sky_core.dds','gfx/map/sky_mid.dds','gfx/map/sky_rim.dds']);
    assert.deepEqual(backgrounds.get('system.sky:g_star').layers[0].lod, { version: 1, sourceWidth: 2048, sourceHeight: 2048, mipLevel: 0, downsampleFactor: 4 });
    assert.deepEqual(backgrounds.get('system.sky:g_star').layers[0].hsvShift, [-.45, -.1, -.3]);
    assert.deepEqual(backgrounds.get('galaxy.stars').hsvShift, [0, 0, 0]);
    assert.notDeepEqual(backgrounds.get('system.sky:a_star').layers[0].hsvShift, backgrounds.get('system.sky:g_star').layers[0].hsvShift);
    for (const bad of [defaults + defaults, defaults.replace('world = default', 'world = g_star'), defaults.replace('0 0 0', '0 .1 0'), defaults.replace('ycocg = no', 'ycocg = yes'), defaults.replace('always = yes', 'always = no'), defaults.replace('always = yes', 'always < yes'), defaults.replace('stars.dds', '../outside.dds'), defaults.replace('trigger =', 'extra = 1 trigger =')]) { assert.throws(() => parsePresentationBackgrounds(bad, profiles)); }
    for (const bad of [starSettings().replace('sky_core.dds', 'sky_mid.dds'), starSettings().replace('< .5', '= .5'), starSettings().replace('< .75', '< .8'), starSettings().replace('ycocg = yes', 'ycocg = no'), starSettings().replace('-.45 -.1 -.3', '-.45 -.1'), starSettings().replace('-.45 -.1 -.3', '1e999 -.1 -.3'), starSettings().replace('trigger = { always = yes }', 'trigger = { always = yes always = yes }')]) { const altered=new Map(profiles);altered.set('g_star',bad);assert.throws(() => parsePresentationBackgrounds(defaults, altered)); }
    assert.ok(PROFILE_PATHS.includes('gfx/map/sky_mid.dds')); assert.ok(PROFILE_PATHS.includes('gfx/map/sky_rim.dds'));
});

test('Presentation UI: exact selected types, paths, frame strips, borders and effect references', () =>
{
    const texts = uiTexts(), metadata = parseUiSpritesMetadata(texts);
    assert.equal(metadata.size, 5);
    assert.deepEqual(metadata.get('gfx/interface/buttons/outliner_entry.dds'), { frames: 3, border: { x: 0, y: 0 } });
    assert.deepEqual(metadata.get('gfx/interface/tiles/outliner_tile.dds'), { frames: 1, border: { x: 80, y: 30 } });
    for (const [source, replace] of [
        ['interface/main.gfx', text => text.replace('corneredTileSpriteType', 'spriteType')],
        ['interface/main.gfx', text => text.replace('textureFile =', 'texturefile = "other.dds" textureFile =')],
        ['interface/main.gfx', text => text.replace('topbar_background.dds', '../outside.dds')],
        ['interface/main.gfx', text => text.replace('x = 8', 'x = 100')],
        ['interface/main.gfx', text => text.replace('x = 8', 'x = 8 x = 8')],
        ['interface/main.gfx', text => text.replace('name = "GFX_topbar_background"', 'name = "GFX_topbar_background" noOfFrames = 1')],
        ['interface/main.gfx', text => text.replace('name = "GFX_starsystem_panel"', 'name = "GFX_starsystem_panel" effectFile = "gfx/FX/buttonstate.shader"')],
        ['interface/main.gfx', text => text.replace('name = "GFX_starsystem_panel"', 'name = "GFX_starsystem_panel" borderSize = { x = 0 y = 0 }')],
        ['interface/outliner.gfx', text => text.replace('noOfFrames = 3', 'noOfFrames = 2')],
        ['interface/outliner.gfx', text => text.replace('noOfFrames = 3', 'noOfFrames = 3 noOfFrames = 3')],
        ['interface/government_view.gfx', text => text.replace('buttonstate.shader', 'buttonstate_onlydisable.shader')],
        ['interface/main.gfx', text => text.replace('spriteTypes = {', 'spriteTypes = { spriteType = { name = "GFX_topbar_background" texturefile = "other.dds" }')],
        ['interface/main.gfx', text => text.replace('name = "GFX_topbar_background"', 'name = "GFX_topbar_background" animation = { file = "other" }')]
    ])
    {
        const changed = new Map(texts); changed.set(source, replace(texts.get(source)));
        assert.notEqual(changed.get(source), texts.get(source)); assert.throws(() => parseUiSpritesMetadata(changed));
    }
});
test('Star worldgfx AST: strict paths/HSV/intensity, unknown fields не исполняются', () =>
{
    const good = starSettings(), parsed = parseStarSurface(good, atmosphereSettings());
    assert.deepEqual(parsed.lavaBrightColor, [.45, .3, .5]);
    assert.deepEqual(parsed.lavaHotStoneColor, [.15, .7, 1]);
    assert.deepEqual(parsed.lavaColdStoneColor, [.09, .85, .9]);
    assert.equal(parsed.lavaBrightIntensity, 1);
    assert.equal(parsed.noisePath, 'gfx/worldgfx/lava_noise.dds');
    assert.deepEqual(parseStarSurface(good.replace('world = g_star', 'world = g_star unsupported = { path = "../do-not-load" }'), atmosphereSettings()), parsed);
    for (const modified of [
        good.replace('lava_bright_color = hsv { .45 .3 .5 }', ''),
        good.replace('lava_bright_color = hsv', 'lava_bright_color = hsv { 0 0 0 } lava_bright_color = hsv'),
        good.replace('hsv { .45 .3 .5 }', '{ .45 .3 .5 }'), good.replace('hsv { .45 .3 .5 }', 'rgb { .45 .3 .5 }'),
        good.replace('hsv { .45 .3 .5 }', 'hsv { .45 .3 }'), good.replace('hsv { .45 .3 .5 }', 'hsv { .45 .3 .5 1 }'),
        good.replace('hsv { .45 .3 .5 }', 'hsv { .45 unknown .5 }'), good.replace('hsv { .45 .3 .5 }', 'hsv { .45 x = .3 .5 }'),
        good.replace('lava_bright_intensity = 1', 'lava_bright_intensity = -1'), good.replace('lava_bright_intensity = 1', 'lava_bright_intensity = "1"'),
        good.replace('lava_bright_intensity = 1', 'lava_bright_intensity = 1e999'), good.replace('lava_bright_intensity = 1', ''),
        good.replace('lava_bright_intensity = 1', 'lava_bright_intensity = 1 lava_bright_intensity = 1'),
        good.replace('gfx/worldgfx/lava_noise.dds', '../outside.dds'), good.replace('tex_lava_noise =', 'tex_lava_noise = "gfx/worldgfx/lava_noise.dds" tex_lava_noise ='),
        good.replace('world = g_star', 'world = other'), good + good
    ]) { assert.throws(() => parseStarSurface(modified, atmosphereSettings())); }
    assert.deepEqual(parsed.atmosphereColor, [.09, .7, .7]); assert.equal(parsed.atmosphereIntensity, .5); assert.equal(parsed.atmosphereWidth, 1.9);
    const atmosphere = atmosphereSettings();
    for (const modified of [atmosphere + atmosphere, '', atmosphere.replace('atmosphere_width = 1.9', ''), atmosphere.replace('atmosphere_width = 1.9', 'atmosphere_width = 0'), atmosphere.replace('atmosphere_width = 1.9', 'atmosphere_width = "1.9"'), atmosphere.replace('atmosphere_intensity = .5', 'atmosphere_intensity = -1'), atmosphere.replace('atmosphere_intensity = .5', 'atmosphere_intensity = 1e999'), atmosphere.replace('atmosphere_intensity = .5', 'atmosphere_intensity = .5 atmosphere_intensity = .5'), atmosphere.replace('hsv { .09 .7 .7 }', 'rgb { .09 .7 .7 }'), atmosphere.replace('hsv { .09 .7 .7 }', 'hsv { .09 .7 }')]) { assert.throws(() => parseStarSurface(good, modified)); }
});

test('Реальный M3 profile: 4 модели, 5 icons, все layers и shared texture bindings', { skip: !root }, async () =>
{
    const buffers = await loadRealBuffers();
    const profile = buildM3Profile(buffers);
    assert.equal(profile.samples.size, 4);
    assert.equal(profile.icons.size, 5);
    assert.ok(profile.decodedBytes > 200 * 1024 * 1024 && profile.decodedBytes < 512 * 1024 * 1024);
    for (const [id, count, skins, scale] of [['ship.corvette.mammalian', 1, 0, 1], ['station.outpost.mammalian', 3, 0, 1], ['planet.continental', 5, 4, 1], ['star.g', 2, 1, 1.3]])
    {
        const sample = profile.samples.get(id);
        assert.equal(sample.meshes.length, count);
        assert.equal(sample.poseReport.skinnedLayerCount, skins);
        assert.equal(sample.scale, scale);
        assert.ok(!Object.hasOwn(sample.poseReport, 'meshes'));
        for (const mesh of sample.meshes)
        {
            for (const channel of ['diff', 'n', 'spec'])
            {
                if (mesh.material[channel]) { assert.ok(sample.images.has(mesh.material[channel])); }
            }
        }
    }
    const station = profile.samples.get('station.outpost.mammalian');
    const star = profile.samples.get('star.g');
    assert.deepEqual(star.starSurface.lavaBrightColor, [.45, .3, .5]);
    assert.equal(star.starSurface.lavaColdStoneIntensity, 1);
    const noise = star.images.get(star.starSurface.noisePath);
    assert.equal(noise.isCubemap, true); assert.equal(noise.faces.length, 6); assert.equal(noise.rgba, undefined);
    assert.ok(noise.faces.every(face => face instanceof Uint8Array && face.byteLength === 1024 * 1024 * 4));
    assert.equal(star.images.get(star.starSurface.lavaPath).codec, 'DXT5');
    assert.equal(star.images.get(star.starSurface.stonePath).codec, 'DXT1');
    assert.deepEqual(star.starSurface.atmosphereColor, [.09, .7, .7]);
    assert.equal(star.starSurface.atmosphereIntensity, .5); assert.equal(star.starSurface.atmosphereWidth, 1.9);
    const cubeBytes = inspectDds(buffers.get(star.starSurface.noisePath)).decodedBytes;
    assert.equal(cubeBytes, 6 * 1024 * 1024 * 4);
    // The caller subtracts retained CPU memory before building an atomic candidate.
    const ceiling = 512 * 1024 * 1024;
    const malformedGeometry = new Map(buffers); malformedGeometry.set('gfx/models/planets/base_star.mesh', new ArrayBuffer(0));
    // A malformed-geometry sentinel proves budget refusal precedes geometry parsing and image decoding.
    assert.throws(() => buildM3Profile(malformedGeometry, { maxDecodedBytes: ceiling - profile.decodedBytes }), /memory budget before decoding.*Reload the page, then reimport/);
    assert.throws(() => buildM3Profile(buffers, { maxDecodedBytes: ceiling - profile.decodedBytes - 117473408 }), /memory budget before decoding/);
    assert.equal(profile.backgrounds.get('system.sky:g_star').layers[0].sourcePath, 'gfx/map/sky_core.dds');
    assert.equal(profile.decodedBytes, 342668400);
    assert.equal(profile.planetImages.size, 296);
    assert.equal([...profile.planetImages.values()].reduce((total, sprite) => total + sprite.image.rgba.byteLength, 0), 11960524);
    for (const id of ['energy', 'minerals', 'food'])
    {
        const path = `gfx/interface/icons/resources/${id}.dds`;
        assert.strictEqual(profile.planetImages.get(`resource.${id}`).image, profile.icons.get(path), `Resource image ${id} must reuse its existing M3 decode.`);
    }
    const uniquePlanetPixels = new Map();
    for (const sprite of profile.planetImages.values()) { uniquePlanetPixels.set(sprite.image.rgba.buffer, sprite.image.rgba.byteLength); }
    assert.equal([...uniquePlanetPixels.values()].reduce((total, bytes) => total + bytes, 0), 11412892);
    assert.strictEqual(profile.planetImages.get('zone.zone_ark_industrial').image, profile.planetImages.get('zone.zone_industrial').image);
    assert.strictEqual(profile.planetImages.get('zone.zone_ark_harvest').image, profile.planetImages.get('zone.zone_minerals').image);
    assert.strictEqual(profile.planetImages.get('zone.zone_ark_food').image, profile.planetImages.get('zone.zone_food').image);
    for (const record of PLANET_IMAGE_DEFINITIONS)
    {
        const sprite = profile.planetImages.get(record.key);
        assert.ok(sprite, `Missing planet image ${record.key}.`);
        assert.deepEqual({ sourcePath: sprite.sourcePath, spriteId: sprite.spriteId, frames: sprite.frames, frame: sprite.frame }, { sourcePath: record.sourcePath, spriteId: record.spriteId, frames: 1, frame: 1 });
        assert.equal(sprite.image.width, record.width);
        assert.equal(sprite.image.height, record.height);
        assert.equal(sprite.image.codec, record.codec);
        assert.equal(sprite.image.rgba.byteLength, record.width * record.height * 4);
    }
    assert.equal(profile.galaxyLayers.size, 3);
    const galaxyArrays = new Set();
    for (const [role, sourcePath, dimension, mips] of [
        ['core', 'gfx/map/center.dds', 1024, 11], ['dust', 'gfx/map/dust.dds', 1024, 11], ['color', 'gfx/map/galaxycolor.dds', 2048, 12]
    ])
    {
        const layer = profile.galaxyLayers.get(role);
        assert.deepEqual(Object.keys(layer).sort(), ['encoding', 'image', 'sourcePath']);
        assert.equal(layer.sourcePath, sourcePath); assert.equal(layer.encoding, 'raw-rgba');
        assert.equal(layer.image.codec, 'DXT5'); assert.equal(layer.image.width, dimension); assert.equal(layer.image.height, dimension);
        assert.equal(layer.image.mipCount, mips); assert.equal(layer.image.decodedMip, 0);
        assert.equal(layer.image.isCubemap, undefined); assert.equal(layer.image.rgba.byteLength, dimension * dimension * 4);
        assert.deepEqual(layer.image.rgba, decodeDds(buffers.get(sourcePath)).rgba);
        assert.ok(!profile.icons.has(sourcePath)); assert.ok(!profile.uiSprites.has(sourcePath));
        assert.ok([...profile.samples.values()].every(sample => !sample.images.has(sourcePath)));
        galaxyArrays.add(layer.image.rgba.buffer);
    }
    assert.equal(galaxyArrays.size, 3);
    assert.equal([...galaxyArrays].reduce((sum, buffer) => sum + buffer.byteLength, 0), 25165824);
    assert.equal(profile.backgrounds.size, 11); assert.equal(profile.uiSprites.size, 5);
    for (const background of profile.backgrounds.values())
    {
        if(background.layers) for(const layer of background.layers)
        {
            assert.equal(layer.image.width, 512); assert.equal(layer.image.height, 512);
            assert.equal(layer.image.faces.length, 6); assert.equal(layer.image.sourceWidth, 2048);
            assert.equal(layer.image.faces.reduce((sum, face) => sum + face.byteLength, 0), 6291456);
        }
        if(background.layers) assert.equal(background.approximation,'HSV-only/static LUT omission');
    }
    assert.equal(profile.backgrounds.get('galaxy.stars').image.decodedMip, 1);
    assert.equal(profile.backgrounds.get('system.sky:g_star').layers[0].image.downsampleFactor, 4);
    const uniqueSkyImages=new Set([...profile.backgrounds.values()].flatMap(bg=>(bg.layers??[]).map(layer=>layer.image)));
    assert.equal(uniqueSkyImages.size,3);
    assert.equal([...uniqueSkyImages].reduce((sum,image)=>sum+image.faces.reduce((n,face)=>n+face.byteLength,0),0),18874368);
    for (const [path, sprite] of profile.uiSprites)
    {
        assert.ok(!profile.icons.has(path)); assert.equal(sprite.image.codec, 'BGRA8');
        assert.equal(sprite.image.width % sprite.frames, 0);
    }
    assert.ok(station.position.every(value => value === 0));
    assert.ok(station.quaternion.every((value, index) => index === 3 ? Math.abs(value) === 1 : value === 0));
    assert.deepEqual(station.meshes.map(mesh => mesh.meshIndex), [0, 1, 2]);
    assert.equal(new Set(station.meshes.map(mesh => mesh.name)).size, 1);
    const path = 'gfx/models/ships/mammalian_01/mammalian_01_corvette_diffuse.dds';
    assert.strictEqual(station.images.get(path), profile.samples.get('ship.corvette.mammalian').images.get(path));
    for (const icon of profile.icons.values()) { assert.equal(icon.codec, 'BGRA8'); }
    const original = parsePdxMesh(buffers.get('gfx/models/planets/planet_clouded.mesh'));
    const planet = profile.samples.get('planet.continental');
    for (let index = 0; index < 5; index++)
    {
        for (const field of ['positions', 'normals', 'tangents', 'uv']) { assert.deepEqual(planet.meshes[index][field], original.meshes[index][field]); }
    }
    assert.deepEqual([...planet.meshes[1].positions.slice(0, 3)], [-0.01628800667822361, 4.454536437988281, -0.025597358122467995]);
    assert.throws(() => buildM3Profile(buffers, { maxDecodedBytes: profile.decodedBytes - 1 }), /memory budget/);
    const renderer = Object.create(M3WorldRenderer.prototype);
    Object.assign(renderer, { disposed: false, resources: null, profile: null, profileReady: false, current: null, view: { kind: 'galaxy' }, viewResources: null,
        scene: new THREE.Scene(), renderer: { renderLists: { dispose() {} } }, addNavigation() {} });
    renderer.setProfile(profile);
    const state = { spatial: { systems: [{ id: 'S', starClassId: null, radius: 10, galaxyPosition: { x: 0, y: 0, z: 0 } }], bodies: [], fleets: [], hyperlanes: [], colonies: [], nebulae: [], belts: [] } };
    const view = renderer.buildView(renderer.resources, state, { kind: 'system', systemId: 'S' });
    const sky = view.group.children.find(child => child.isMesh && child.renderOrder === -1000);
    assert.strictEqual(sky.material, renderer.resources.backgrounds.get('system.sky:g_star:core'));
    renderer.releaseView(view); renderer.releaseView(renderer.viewResources); renderer.releaseProfile(renderer.resources);
});

test('Planet image transfer keeps borrowed shared pixels on one unique transferable buffer', () =>
{
    const image = { width: 1, height: 1, rgba: new Uint8Array([4, 5, 6, 255]) };
    const profile = { planetImages: new Map([['district.one', { image }], ['zone.two', { image }]]) };
    const transfers = collectTransferBuffers(profile);
    assert.deepEqual(transfers, [image.rgba.buffer]);
    const received = structuredClone(profile, { transfer: transfers });
    assert.equal(image.rgba.byteLength, 0);
    assert.strictEqual(received.planetImages.get('district.one').image, received.planetImages.get('zone.two').image);
    assert.deepEqual([...received.planetImages.get('district.one').image.rgba], [4, 5, 6, 255]);
});

test('Реальный M3 profile: изменённые named definitions/attachments/paths не проходят', { skip: !root }, async () =>
{
    const original = await loadRealBuffers();
    function changed(path, replace)
    {
        const buffers = new Map(original);
        const text = new TextDecoder().decode(buffers.get(path));
        const modified = replace(text);
        assert.notEqual(modified, text);
        buffers.set(path, new TextEncoder().encode(modified).buffer);
        return buffers;
    }
    const mammalian = 'gfx/models/ships/mammalian_01/';
    const starbase = 'gfx/models/ships/starbases/';
    const planet = 'gfx/models/planets/';
    const cases = [
        [mammalian + '_mammalian_01_ships_entities.asset', text => text.replace('name = "mammalian_01_corvette_S1_entity"', 'name = "mammalian_01_corvette_S1_entity" scale = 2')],
        [mammalian + '_mammalian_01_ships_entities.asset', text => text + '\nentity={name="mammalian_01_corvette_S1_entity" pdxmesh="mammalian_01_corvette_S1_mesh"}'],
        [mammalian + '_mammalian_01_ships_meshes.gfx', text => text.replace('file = "gfx/models/ships/mammalian_01/mammalian_01_corvette_S1.mesh"', 'file = "../outside.mesh"')],
        [planet + '_planetary_entities.asset', text => text.replaceAll('name = "polesShape"', 'name = "unknownShape"')],
        [planet + '_planetary_entities.asset', text => text.replaceAll('texture_diffuse = "continental_01_diffuse.dds"', 'texture_diffuse = "poles_1_diffuse.dds"')],
        [planet + '_planetary_entities.asset', text => text.replace('name = "continental_planet_01_entity"', 'name = "continental_planet_01_entity" meshsettings={shader=UnknownShader}')],
        [planet + '_planetary_entities.asset', text => text.replace('name = "continental_planet_01_entity"', 'name = "continental_planet_01_entity" meshsettings={name=polesShape shader=PdxMeshClouds}')],
        [starbase + '_starbase_entities.asset', text => text.replace('name = "mammalian_01_starbase_outpost_entity"', 'name = "mammalian_01_starbase_outpost_entity" attach={slot=other}')],
        [starbase + '_starbase_entities.asset', text => text.replace('name = "mammalian_01_starbase_outpost_section_entity"', 'name = "mammalian_01_starbase_outpost_section_entity" locator={name=extra}')],
        ['common/ship_sizes/00_starbases.txt', text => text.replace('locator = "part1"', 'locator = "wrong"')],
        ['common/ship_sizes/00_starbases.txt', text => text.replace('section_slots = {', 'section_slots = { extra={locator=part2}')],
        ['common/section_templates/starbase.txt', text => text.replace('fits_on_slot = "core"', 'fits_on_slot = "other"')],
        ['interface/resources.gfx', text => text.replace('gfx/interface/icons/resources/energy.dds', 'gfx/interface/icons/resources/minerals.dds')],
        ['interface/resources.gfx', text => text + '\nspriteTypes = {}']
    ];
    for (const [path, replace] of cases) { assert.throws(() => buildM3Profile(changed(path, replace)), /M3 profile:|Planet assets:|Поле |Определение /, path); }
    const missing = new Map(original); missing.delete('gfx/models/nonormal.dds');
    assert.throws(() => buildM3Profile(missing), /отсутствует/);
    const extra = new Map(original); extra.set('secret.dds', new ArrayBuffer(0));
    assert.throws(() => buildM3Profile(extra), /allowlist/);
});
