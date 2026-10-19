import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
import * as THREE from 'three';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { VISUAL_IDS } from './m3-world-view.mjs';
import { PROFILE_ID, PROFILE_PATHS, buildM3Profile } from './m3-profile.mjs';
import { createLocalM3Manifest, validateM3Manifest } from './m3-manifest.mjs';
import { MAX_PLANET_DECODED_BYTES } from './m3-planet-assets.mjs';
import { ORDINARY_PLANET_IMAGES } from './m3-ordinary-planet-assets.mjs';
import { inspectDds, decodeDds } from './formats.mjs';

const awaitingCamera = typeof M3WorldRenderer.prototype.setCameraPreset !== 'function';
const gameRoot = process.env.STELLARIS_GAME_ROOT;

// CPU-only contract checks. TODO/SKIP entries are not visual or implementation PASS.
function pivotFixture(positions)
{
    const value = Object.create(M3WorldRenderer.prototype);
    const group = new THREE.Group();
    group.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1)));
    group.children[0].position.set(100000, 100000, -100000);
    Object.assign(value, { selection: null, view: { kind: 'galaxy' }, viewResources: { group, objects: new Map() }, current: { state: { spatial: { systems: positions.map((p, i) => ({ id: `S${i}`, galaxyPosition: { x: p[0], y: p[1], z: p[2] } })), fleets: [{ galaxyPosition: { x: -900000, y: 0, z: 900000 } }] } } } });
    return value;
}

test('QA reference: galaxy pivot uses authorized system AABB, not average, fleet or decoration', { todo: awaitingCamera && 'CAM-001 implementation pending; current origin fallback is known wrong' }, t =>
{
    const value = pivotFixture([[100, 0, -60], [300, 0, 140], [110, 0, -50]]);
    t.after(() => value.viewResources.group.children[0].geometry.dispose());
    assert.deepEqual(value.resolvePivot().toArray(), [200, 0, 40]);
    value.current.state.spatial.systems.forEach(s => { s.galaxyPosition.x += 700; s.galaxyPosition.z -= 200; });
    assert.deepEqual(value.resolvePivot().toArray(), [900, 0, -160]);
});

test('QA reference: empty galaxy and unselected system retain origin fallback', t =>
{
    const value = pivotFixture([]);
    t.after(() => value.viewResources.group.children[0].geometry.dispose());
    assert.deepEqual(value.resolvePivot().toArray(), [0, 0, 0]);
    value.view = { kind: 'system', systemId: 'S' };
    value.current.state.spatial.systems = [{ id: 'S', galaxyPosition: { x: 700, y: 0, z: -400 } }];
    assert.deepEqual(value.resolvePivot().toArray(), [0, 0, 0]);
});

test('QA reference: actual four-preset API is deterministic after pan and ignores decoration', { skip: awaitingCamera && 'Camera preset API not integrated' }, t =>
{
    const value = pivotFixture([[0, 0, 0], [300, 0, 300]]);
    t.after(() => value.viewResources.group.children[0].geometry.dispose());
    value.disposed = false;
    value.camera = new THREE.PerspectiveCamera(45, 16 / 9, .01, 10000);
    value.controls = { target: new THREE.Vector3(), update() {} };
    value.current.state.spatial.bodies = [];
    value.current.state.spatial.systems[0].radius = 150;
    for (const [id, visualId, x, radius] of [['star', 'star.g', 0, 20], ['planet', 'planet.continental', 80, 10]])
    {
        const object = new THREE.Mesh(new THREE.SphereGeometry(radius, 8, 4));
        object.position.x = x;
        value.viewResources.objects.set(`body:${id}`, object);
        value.current.state.spatial.bodies.push({ id, kind: id === 'star' ? 'star' : 'planet', visualId, systemId: 'S0' });
        t.after(() => object.geometry.dispose());
    }
    const snapshot = JSON.stringify(value.current.state);
    const distances = new Map();
    for (const id of ['galaxy-far', 'galaxy-near', 'system-overview', 'system-close'])
    {
        value.view = id.startsWith('galaxy') ? { kind: 'galaxy' } : { kind: 'system', systemId: 'S0' };
        value.setCameraPreset(id);
        const position = value.camera.position.clone(), target = value.controls.target.clone();
        assert.equal(value.cameraPreset, id);
        assert.ok(position.toArray().every(Number.isFinite));
        assert.ok(position.distanceTo(target) > 0);
        distances.set(id, position.distanceTo(target));
        value.camera.position.addScalar(500);
        value.controls.target.addScalar(-200);
        value.viewResources.group.children[0].position.addScalar(90000);
        value.setCameraPreset(id);
        assert.ok(value.camera.position.distanceTo(position) < 1e-9);
        assert.ok(value.controls.target.distanceTo(target) < 1e-9);
        assert.equal(JSON.stringify(value.current.state), snapshot);
        for (const invalid of ['unknown', id.startsWith('galaxy') ? 'system-close' : 'galaxy-far'])
        {
            assert.throws(() => value.setCameraPreset(invalid));
            assert.ok(value.camera.position.distanceTo(position) < 1e-9);
            assert.ok(value.controls.target.distanceTo(target) < 1e-9);
        }
    }
    assert.ok(distances.get('galaxy-near') < distances.get('galaxy-far'));
    assert.ok(distances.get('system-close') < distances.get('system-overview'));
});

test('QA reference: failed template staging disposes candidate and preserves active profile/view', () =>
{
    const value = Object.create(M3WorldRenderer.prototype);
    const oldProfile = {}, oldView = {}, oldResources = { convertedBytes: 0 };
    Object.assign(value, { disposed: false, profile: oldProfile, profileReady: true, resources: oldResources, viewResources: oldView, view: { kind: 'galaxy' } });
    const source = { samples: new Map(VISUAL_IDS.map(id => [id, { images: new Map(), meshes: [{ material: { shader: 'PdxMeshStar' } }] }])) };
    let disposed = 0;
    value.prepareTemplate = (sample, candidate) =>
    {
        const geometry = new THREE.BufferGeometry();
        geometry.addEventListener('dispose', () => disposed++);
        candidate.geometries.push(geometry);
        throw new Error('QA staged failure');
    };
    assert.throws(() => value.setProfile(source), /QA staged failure/);
    assert.equal(disposed, 1);
    assert.equal(value.profile, oldProfile);
    assert.equal(value.resources, oldResources);
    assert.equal(value.viewResources, oldView);
    assert.equal(value.profileReady, true);
});

function cube()
{
    // Distinct faces/mips and spatial selectors detect face reuse and cropping.
    const buffer = new ArrayBuffer(128 + 6 * 40), view = new DataView(buffer);
    for (const [offset, value] of [[0, 0x20534444], [4, 124], [12, 8], [16, 8], [28, 2], [76, 32], [80, 4], [112, 0xfe00]])
    {
        view.setUint32(offset, value, true);
    }
    [...'DXT1'].forEach((char, i) => view.setUint8(84 + i, char.charCodeAt(0)));
    const colors = [0xf800, 0x07e0, 0x001f, 0xffe0, 0x07ff, 0xf81f];
    for (let face = 0; face < 6; face++)
    {
        const base = 128 + face * 40;
        for (let block = 0; block < 4; block++)
        {
            const offset = base + block * 8;
            view.setUint16(offset + 2, colors[face], true);
            let selectors = 0;
            for (let y = 0; y < 4; y++)
            {
                for (let x = 0; x < 4; x++) { selectors |= ((x + 2 * y) % 4) << (2 * (4 * y + x)); }
            }
            view.setUint32(offset + 4, selectors >>> 0, true);
        }
        view.setUint16(base + 32, colors[(face + 1) % 6], true);
    }
    return buffer;
}

test('QA reference: synthetic cube default decode preserves spatial alpha and distinct faces', () =>
{
    const decoded = decodeDds(cube());
    assert.equal(decoded.width, 8);
    assert.equal(decoded.faces.length, 6);
    const colors = [[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255], [255, 255, 0, 255], [0, 255, 255, 255], [255, 0, 255, 255]];
    for (const [index, face] of decoded.faces.entries())
    {
        assert.deepEqual([...face.slice(4 * 9, 4 * 10)], [0, 0, 0, 0]);
        assert.deepEqual([...face.slice(4 * 11, 4 * 12)], colors[index]);
    }
});

test('QA reference: exact v12 path count, required additions and spatial six-face LOD samples', () =>
{
    assert.equal(PROFILE_ID, 'm3-static-452-v12');
    assert.equal(PROFILE_PATHS.length, 390);
    assert.equal(new Set(PROFILE_PATHS).size, 390);
    assert.equal(MAX_PLANET_DECODED_BYTES, 16 * 1024 * 1024, 'Пять слоёв окружения входят в бюджет планетарных изображений');
    for (const path of ['gfx/worldgfx/default.txt', 'gfx/worldgfx/stars.dds', 'gfx/worldgfx/star_a_class.txt', 'gfx/worldgfx/star_black_hole.txt', 'gfx/map/sky_core.dds', 'gfx/map/sky_mid.dds', 'gfx/map/sky_rim.dds', 'interface/main.gfx', 'interface/outliner.gfx', 'interface/government_view.gfx', 'gfx/interface/main/topbar_background.dds', 'gfx/interface/tiles/outliner_tile.dds', 'gfx/interface/tiles/green_bg_tile.dds', 'gfx/interface/buttons/outliner_entry.dds', 'gfx/interface/main/starsystem_panel.dds', 'gfx/map/center.dds', 'gfx/map/dust.dds', 'gfx/map/galaxycolor.dds'])
    {
        assert.ok(PROFILE_PATHS.includes(path), `Missing required presentation source: ${path}`);
    }
    for (const path of ['common/buildings/03_resource_buildings.txt', 'common/deposits/01_blocker_deposits.txt', 'common/deposits/01_planetary_deposits.txt', 'common/pop_jobs/04_gestalt_jobs.txt', 'common/zones/00_zones.txt', 'gfx/interface/icons/buildings/building_hydroponics_farm.dds', 'gfx/interface/icons/deposits/d_metal_boneyard.dds', 'gfx/interface/icons/deposits/d_slums.dds', 'interface/planet_view.gfx'])
    {
        assert.ok(PROFILE_PATHS.includes(path), `Missing required planet dependency: ${path}`);
    }
    for (const path of [
        'common/districts/07_ark_districts.txt', 'common/zones/05_arkship_zones.txt',
        ...['ark_harvest', 'research_physics', 'research_society', 'research_engineering'].map(id => `common/inline_scripts/zones/shared_${id}_zone.txt`),
        ...['ark_city', 'ark_generator', 'ark_basic', 'ark_military', 'hive', 'nexus'].map(id => `gfx/interface/icons/districts/district_${id}.dds`),
        ...['culture_worker', 'physicist', 'biology', 'engineer'].map(id => `gfx/interface/icons/districts/district_specialization_icons/district_specialization_${id}.dds`)
    ])
    {
        assert.ok(PROFILE_PATHS.includes(path), `Missing exact accepted v8 dependency: ${path}`);
    }
    const colors = [[255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255], [255, 255, 0, 255], [0, 255, 255, 255], [255, 0, 255, 255]];
    const input = cube();
    const inspected = inspectDds(input, { mipLevel: 1, downsampleFactor: 1 });
    assert.equal(inspected.decodedBytes, 6 * 4 * 4 * 4);
    const decoded = decodeDds(input, { mipLevel: 1, downsampleFactor: 1 });
    assert.equal(decoded.width, 4);
    assert.equal(decoded.height, 4);
    assert.equal(decoded.decodedMip, 1);
    assert.equal(decoded.faces.length, 6);
    for (const [index, face] of decoded.faces.entries()) { assert.deepEqual([...face.slice(0, 4)], colors[(index + 1) % 6]); }
    const sampled = decodeDds(input, { mipLevel: 0, downsampleFactor: 2 });
    assert.equal(sampled.width, 4);
    for (const [index, face] of sampled.faces.entries())
    {
        for (let y = 0; y < 4; y++)
        {
            for (let x = 0; x < 4; x++)
            {
                const offset = 4 * (4 * y + x);
                assert.deepEqual([...face.slice(offset, offset + 4)], x % 2 === 0 ? [0, 0, 0, 0] : colors[index]);
            }
        }
    }
});

test('QA reference: LOD rejects invalid options and truncated skipped mip data', () =>
{
    const input = cube();
    for (const options of [{ mipLevel: -1 }, { mipLevel: 2 }, { mipLevel: .5 }, { downsampleFactor: 0 }, { downsampleFactor: 3 }, { downsampleFactor: 16 }])
    {
        assert.throws(() => inspectDds(input, options));
        assert.throws(() => decodeDds(input, options));
    }
    assert.throws(() => decodeDds(input.slice(0, -1), { mipLevel: 1 }));
    assert.throws(() => inspectDds(input.slice(0, -1), { mipLevel: 1 }));
});

test('QA reference: real 4.5.2 v12 manifest, audited planet imagery and retained galaxy layers', { skip: !gameRoot && 'STELLARIS_GAME_ROOT unavailable; real-source coverage NOT RUN' }, async () =>
{
    assert.equal(PROFILE_ID, 'm3-static-452-v12');
    const buffers = new Map(await Promise.all(PROFILE_PATHS.map(async path =>
    {
        const bytes = await readFile(resolve(gameRoot, path));
        return [path, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)];
    })));
    const manifest = await createLocalM3Manifest(buffers);
    assert.equal(manifest.profileDigest, 'c1a2234ecd9fd1390bd02cb37a3f64547612d28894c17865aa5f5de7ffb4340c');
    assert.deepEqual([manifest.schemaVersion, manifest.importerVersion, manifest.materialProfileVersion, manifest.files.length], [1, 9, 5, 390]);
    assert.equal(manifest.files.reduce((sum, file) => sum + file.byteLength, 0), 191249125);
    assert.deepEqual(await validateM3Manifest(manifest), manifest);
    for (const stale of [{ profileId: 'm3-static-452-v8' }, { profileId: 'm3-static-452-v9' }, { profileId: 'm3-static-452-v10' }, { importerVersion: 7 }])
    {
        await assert.rejects(validateM3Manifest({ ...manifest, ...stale }), /несовместимые версии/, 'Prior profile/importer versions must reject at the compatibility guard');
    }
    assert.throws(() => buildM3Profile(buffers, { maxDecodedBytes: 342668399 }), /Exceeded decoded memory budget before decoding/, 'A one-byte-short total budget must fail before publishing a profile');
    const profile = buildM3Profile(buffers);
    assert.equal(profile.decodedBytes, 342668400);
    const expectedPlanetKeys = [
        ...['city', 'generator', 'mining', 'farming'].map(id => `district.district_${id}`),
        ...['ark_city', 'ark_generator', 'ark_basic', 'ark_military', 'hive', 'nexus'].map(id => `district.district_${id}`),
        ...['default', 'urban', 'research_unity', 'industrial', 'research', 'unity', 'foundry', 'factory', 'energy', 'minerals', 'food', 'trade'].map(id => `zone.zone_${id}`),
        ...['research_unity', 'industrial', 'harvest', 'food', 'reactor_physics', 'farm_society', 'military_engineering'].map(id => `zone.zone_ark_${id}`),
        ...['colony_shelter', 'capital', 'factory_1', 'precinct_house', 'research_lab_1', 'stronghold', 'foundry_1', 'holo_theatres', 'bureaucratic_1', 'hydroponics_farm', 'medical_1', 'commercial_zone'].map(id => `building.building_${id}`),
        ...['farmer', 'agri_drone', 'technician', 'miner', 'physicist', 'biologist', 'engineer', 'foundry', 'artisan', 'bureaucrat', 'politician', 'enforcer', 'replicator', 'coordinator', 'patrol_drone', 'spawning_drone', 'synapse_drone', 'logistics_drone', 'clerk', 'entertainer', 'healthcare', 'soldier'].map(id => `job.${id}`),
        ...['luxury_residence', 'temple', 'hive_capital', 'spawning_pool', 'offspring_nest', 'hive_warren', 'machine_capital', 'machine_assembly_plant', 'drone_storage'].map(id => `building.building_${id}`),
        ...['alloys', 'consumer_goods', 'unity', 'physics_research', 'society_research', 'engineering_research', 'trade', 'energy', 'minerals', 'food'].map(id => `resource.${id}`),
        'ui.GFX_planetview_middle', 'ui.GFX_planet_building_bg',
        ...['sky', 'l01', 'l02', 'l03', 'l04'].map(layer => `environment.pc_continental.${layer}`),
        ...ORDINARY_PLANET_IMAGES.map(record => record.key),
        'blocker.d_failing_infrastructure', 'blocker.d_decrepit_dwellings',
        ...['hot_springs', 'rushing_waterfalls', 'tempestous_mountain', 'veiny_cliffs', 'prosperous_mesa', 'rich_mountain', 'rugged_woods', 'fertile_lands', 'black_soil'].map(id => `feature.d_${id}`)
    ];
    assert.equal(expectedPlanetKeys.length, 296);
    assert.deepEqual([...profile.planetImages.keys()].sort(), expectedPlanetKeys.sort());
    let descriptorBytes = 0;
    const uniqueImages = new Set();
    for (const [key, sprite] of profile.planetImages)
    {
        const info = inspectDds(buffers.get(sprite.sourcePath));
        const expectedCodec = ORDINARY_PLANET_IMAGES.find(record => record.key === key)?.codec ?? (key === 'job.miner' ? 'DXT5' : 'BGRA8');
        assert.deepEqual([info.codec, info.faceCount, info.mipCount, sprite.frames, sprite.frame], [expectedCodec, 1, 1, 1, 1], key);
        assert.ok(sprite.image.rgba instanceof Uint8Array, key);
        assert.equal(sprite.image.rgba.byteLength, sprite.image.width * sprite.image.height * 4, key);
        descriptorBytes += sprite.image.rgba.byteLength;
        uniqueImages.add(sprite.image);
        if (key.startsWith('blocker.') || key.startsWith('feature.')) { assert.deepEqual([info.width, info.height], [98, 75], key); }
    }
    const uniquePlanetBytes = [...uniqueImages].reduce((sum, image) => sum + image.rgba.byteLength, 0);
    assert.equal(uniqueImages.size, 268);
    assert.equal(new Set([...profile.planetImages.values()].map(sprite => sprite.sourcePath)).size, 268);
    assert.equal(descriptorBytes, 11960524);
    assert.equal(uniquePlanetBytes, 11412892);
    assert.ok(uniquePlanetBytes < 16 * 1024 * 1024);
    const districtHashes = new Set();
    for (const id of ['city', 'generator', 'mining', 'farming', 'ark_city', 'ark_generator', 'ark_basic', 'ark_military', 'hive', 'nexus'])
    {
        const sprite = profile.planetImages.get(`district.district_${id}`);
        assert.equal(sprite.sourcePath, `gfx/interface/icons/districts/district_${id}.dds`);
        assert.deepEqual([sprite.image.width, sprite.image.height], [77, 77]);
        if (['ark_city', 'ark_generator', 'ark_basic', 'ark_military', 'hive', 'nexus'].includes(id)) { assert.equal(sprite.spriteId, `local-district:district_${id}`); }
        districtHashes.add(createHash('sha256').update(sprite.image.rgba).digest('hex'));
    }
    assert.equal(districtHashes.size, 10, 'Distinct district keys must use distinct decoded pixels, not city aliases');
    for (const [id, icon, texture, shared] of [
        ['research_unity', 'ark_research_unity_consumer_goods', 'culture_worker', null],
        ['industrial', 'ark_research_alloys_support', 'industrial', 'industrial'],
        ['harvest', 'minerals', 'minerals', 'minerals'], ['food', 'food', 'food_market', 'food'],
        ['reactor_physics', 'physics', 'physicist', null], ['farm_society', 'society', 'biology', null],
        ['military_engineering', 'engineering', 'engineer', null]
    ])
    {
        const sprite = profile.planetImages.get(`zone.zone_ark_${id}`);
        assert.equal(sprite.spriteId, `GFX_district_specialization_${icon}`);
        assert.equal(sprite.sourcePath, `gfx/interface/icons/districts/district_specialization_icons/district_specialization_${texture}.dds`);
        assert.deepEqual([sprite.image.width, sprite.image.height], [64, 64]);
        if (shared) { assert.equal(sprite.image, profile.planetImages.get(`zone.zone_${shared}`).image, 'Source-sharing must reuse decoded image identity'); }
    }
    assert.equal(profile.planetImages.get('blocker.d_failing_infrastructure').sourcePath, 'gfx/interface/icons/deposits/d_metal_boneyard.dds');
    assert.equal(profile.planetImages.get('blocker.d_decrepit_dwellings').sourcePath, 'gfx/interface/icons/deposits/d_slums.dds');
    assert.deepEqual([...profile.galaxyLayers.keys()].sort(), ['color', 'core', 'dust']);
    for (const [key, path, width, mips] of [['core', 'gfx/map/center.dds', 1024, 11], ['dust', 'gfx/map/dust.dds', 1024, 11], ['color', 'gfx/map/galaxycolor.dds', 2048, 12]])
    {
        const input = buffers.get(path), info = inspectDds(input), layer = profile.galaxyLayers.get(key);
        assert.deepEqual([info.codec, info.width, info.height, info.faceCount, info.mipCount], ['DXT5', width, width, 1, mips]);
        assert.equal(layer.sourcePath, path);
        assert.equal(layer.encoding, 'raw-rgba');
        assert.equal(layer.image.decodedMip, 0);
        assert.equal(layer.image.rgba.byteLength, width * width * 4);
        assert.throws(() => inspectDds(input.slice(0, -1)), /./);
        assert.throws(() => decodeDds(input.slice(0, -1)), /./);
    }
    const galaxyBackground=profile.backgrounds.get('galaxy.stars');
    assert.deepEqual([galaxyBackground.image.width,galaxyBackground.image.height,galaxyBackground.image.faces.length,galaxyBackground.image.decodedMip,galaxyBackground.lod.downsampleFactor],[1024,1024,6,1,1]);
    const skyProfiles=[...profile.backgrounds].filter(([key])=>key.startsWith('system.sky:')).map(([,value])=>value);
    assert.equal(skyProfiles.length,10);
    const uniqueSkyImages=new Map();
    for(const background of skyProfiles)
    {
        assert.equal(background.approximation,'HSV-only/static LUT omission'); assert.equal(background.layers.length,3);
        for(const layer of background.layers)
        {
            assert.deepEqual([layer.image.width,layer.image.height,layer.image.faces.length,layer.image.downsampleFactor],[512,512,6,4]);
            assert.ok(layer.image.faces.every(face=>face.byteLength===512*512*4));
            if(uniqueSkyImages.has(layer.sourcePath)) assert.strictEqual(uniqueSkyImages.get(layer.sourcePath),layer.image);
            else uniqueSkyImages.set(layer.sourcePath,layer.image);
        }
    }
    assert.equal(uniqueSkyImages.size,3);
});
