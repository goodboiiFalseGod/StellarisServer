import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { templateDisplayFactor } from './m3-presentation.mjs';
import { VISUAL_IDS } from './m3-world-view.mjs';
import { GALAXY_LAYER_PATHS } from './m3-galaxy-layers.mjs';

function renderer()
{
    const value = Object.create(M3WorldRenderer.prototype);
    Object.assign(value, { disposed: false, resources: null, profile: null, profileReady: false, current: null, view: { kind: 'galaxy' }, viewResources: null, scene: new THREE.Scene(), renderer: { renderLists: { dispose() {} } } });
    return value;
}

function profile()
{
    const image = { width: 1, height: 1, rgba: new Uint8Array([128, 128, 255, 255]) };
    const cube = { width: 1, height: 1, faces: Array.from({ length: 6 }, () => new Uint8Array([128, 128, 128, 255])), codec: 'DXT1', mipCount: 1, decodedMip: 0, isCubemap: true };
    const images = new Map([['d', image], ['n', image], ['s', image], ['noise', cube], ['lava', image], ['stone', image]]);
    const mesh = { name: 'surface', positions: new Float32Array([-1, 0, 0, 1, 0, 0, 0, 1, 0]), normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]), uv: new Float32Array([0, 0, 1, 0, 0, 1]), indices: new Uint32Array([0, 1, 2]), material: { shader: 'PdxMeshShip', diff: 'd', n: 'n', spec: 's' } };
    const samples = new Map(VISUAL_IDS.map(id => [id, { scale: 1, images, meshes: [mesh] }]));
    samples.set('star.g', { scale: 1.3, images, starSurface: { noisePath: 'noise', lavaPath: 'lava', stonePath: 'stone', lavaBrightColor: [0, 1, 1], lavaBrightIntensity: 1, lavaHotStoneColor: [0.2, 1, 1], lavaHotStoneIntensity: 1, lavaColdStoneColor: [0.1, 1, 1], lavaColdStoneIntensity: 1, atmosphereColor: [0.1, 0.7, 0.7], atmosphereIntensity: 0.5, atmosphereWidth: 1.9 }, meshes: [{ ...mesh, material: { shader: 'PdxMeshStar' } }, { ...mesh, positions: Float32Array.from(mesh.positions, value => value * 2), name: 'atmosphere', material: { shader: 'PdxMeshAtmosphereStar' } }] });
    return { samples };
}

test('Presentation cube textures are shared across views and failed staging preserves the profile', () =>
{
    const value = renderer();
    const source = profile();
    const image = source.samples.get('star.g').images.get('noise');
    source.backgrounds = new Map([['galaxy.stars', { image, encoding: 'linear-rgb', hsvShift: [0, 0, 0] }], ['system.sky', { image, encoding: 'ycocg', hsvShift: [-0.45, -0.1, -0.3] }]]);
    value.setProfile(source);
    const resources = value.resources;
    assert.equal(resources.textures.length, 19);
    const oldView = value.viewResources;
    let releases = 0;
    for (const texture of resources.textures) { texture.addEventListener('dispose', () => releases++); }
    const scene = value.buildView(resources, null, { kind: 'system' });
    assert.equal(scene.group.children[0].material, resources.backgrounds.get('system.sky'));
    assert.equal(scene.bounds.isEmpty(), true);
    value.releaseView(scene);
    assert.equal(releases, 0);
    const invalid = profile();
    invalid.backgrounds = new Map([['galaxy.stars', { image, encoding: 'bad', hsvShift: [0, 0, 0] }], ['system.sky', { image, encoding: 'ycocg', hsvShift: [0, 0, 0] }]]);
    assert.throws(() => value.setProfile(invalid));
    assert.equal(value.resources, resources);
    assert.equal(value.viewResources, oldView);
    invalid.backgrounds.get('galaxy.stars').encoding = 'linear-rgb';
    invalid.backgrounds.get('galaxy.stars').hsvShift = new Array(3);
    assert.throws(() => value.setProfile(invalid));
    assert.equal(value.resources, resources);
    value.releaseView(oldView); value.releaseProfile(resources);
    assert.equal(releases, 19);
});

test('Galaxy layers survive 100 view rebuilds without leaking view resources or changing framing', () =>
{
    const value = renderer();
    const source = profile();
    source.galaxyLayers = new Map(Object.entries(GALAXY_LAYER_PATHS).map(([id, sourcePath]) => [id, { sourcePath, encoding: 'raw-rgba', image: { width: 1, height: 1, rgba: new Uint8Array([255, 255, 255, 128]) } }]));
    value.setProfile(source);
    const state = { spatial: { systems: [{ id: 'S', galaxyPosition: { x: 250, y: 0, z: -150 } }], hyperlanes: [], bodies: [], fleets: [] } };
    let sharedReleases = 0;
    for (const resource of [...value.resources.textures, ...value.resources.materials]) { resource.addEventListener('dispose', () => sharedReleases++); }
    for (let index = 0; index < 100; index++)
    {
        const view = value.buildView(value.resources, state, { kind: 'galaxy' });
        assert.equal(view.picks.length, 1);
        assert.ok(view.bounds.max.x - view.bounds.min.x <= 6);
        let releases = 0;
        for (const resource of view.owned) { resource.addEventListener('dispose', () => releases++); }
        value.releaseView(view);
        assert.equal(releases, view.owned.size);
    }
    assert.equal(sharedReleases, 0);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('Star wiring retains geometry, layers and scale while sharing imported GPU textures', () =>
{
    const value = renderer();
    const source = profile();
    value.setProfile(source);
    const resources = value.resources;
    const star = resources.templates.get('star.g');
    assert.equal(star.scale.x, 1.3);
    assert.equal(star.children.length, 2);
    assert.equal(star.children[0].geometry.attributes.position.array, source.samples.get('star.g').meshes[0].positions);
    assert.deepEqual(new THREE.Box3().setFromObject(star).min.toArray(), [-2.6, 0, 0]);
    const material = star.children[0].material;
    assert.equal(material.isShaderMaterial, true);
    assert.equal(material.uniforms.lavaNoise.value.isCubeTexture, true);
    assert.equal(material.uniforms.lavaDiffuse.value.colorSpace, THREE.NoColorSpace);
    assert.equal(material.uniforms.lavaDiffuse.value.wrapS, THREE.RepeatWrapping);
    assert.equal(material.uniforms.stoneDiffuse.value.wrapT, THREE.RepeatWrapping);
    assert.equal(resources.textures.length, 12);
    value.setProfile(source);
    assert.equal(value.resources, resources);
    let releases = 0;
    for (const texture of resources.textures) { texture.addEventListener('dispose', () => releases++); }
    value.releaseView(value.viewResources);
    assert.equal(releases, 0);
    value.releaseProfile(resources);
    assert.equal(releases, 12);
});

test('Missing star metadata or texture rolls staging back without replacing the valid view', () =>
{
    const value = renderer();
    value.setProfile(profile());
    const resources = value.resources;
    const view = value.viewResources;
    const texture = value.texture;
    const staged = new Set();
    let releases = 0;
    value.texture = function(...args)
    {
        const result = texture.apply(this, args);
        for (const resource of args[2].textures)
        {
            if (staged.has(resource)) { continue; }
            staged.add(resource);
            resource.addEventListener('dispose', () => releases++);
        }
        return result;
    };
    for (const breakSample of [sample => { delete sample.starSurface; }, sample => { sample.starSurface.stonePath = 'missing'; }, sample => { delete sample.starSurface.atmosphereWidth; }])
    {
        const invalid = profile();
        breakSample(invalid.samples.get('star.g'));
        assert.throws(() => value.setProfile(invalid));
        assert.equal(value.resources, resources);
        assert.equal(value.viewResources, view);
        assert.equal(value.profileReady, true);
        assert.equal(releases, staged.size);
    }
    value.releaseView(view);
    value.releaseProfile(resources);
});

test('Generated templates share the continental source-unit calibration', () =>
{
    const value = renderer(), source = profile();
    value.stageGeneratedGpu = () => {};
    const mesh = { ...source.samples.get('planet.continental').meshes[0], material: { shader: 'PdxMeshPlanet', diff: 'd', n: 'n', spec: 's' } };
    source.samples.set('planet.continental', { ...source.samples.get('planet.continental'), meshes: [mesh] });
    const generated = new Map();
    const classIds = ['pc_black_hole', 'pc_continental', 'pc_gas_giant'];
    for (const [classId, sourceScale, kind] of [['pc_black_hole', .62, 'star'], ['pc_continental', 1, 'planet'], ['pc_gas_giant', 2.4, 'planet']])
    {
        const id = `world.${classId}`;
        const binding = { classId, entityName: `${classId}_entity`, kind };
        generated.set(id, binding);
        source.samples.set(id, { scale: sourceScale, sourceClassId: classId, sourceEntityName: binding.entityName, bodyKind: kind, images: source.samples.get('planet.continental').images, meshes: [mesh] });
    }
    source.generatedBindings = generated; source.generatedRequirements = { version: 1, policy: 'SourceFirstNumberedEntityV1', classIds };
    source.systemLighting = new Map([['sc_fixture', { color: [1, .8, .5], world: 'g_star', position: [0, 0, 0], intensity: 1, lightName: 'fixture' }]]); source.decodedBytes = 1;
    const cube = { width: 1, height: 1, faces: Array.from({ length: 6 }, () => new Uint8Array([128, 128, 128, 255])), isCubemap: true };
    source.backgrounds = new Map([
        ['galaxy.stars', { image: cube, encoding: 'linear-rgb', hsvShift: [0, 0, 0] }],
        ['system.sky:g_star', { layers: ['core', 'mid', 'rim'].map(band => ({ sourcePath: `${band}.dds`, image: cube, encoding: 'ycocg', hsvShift: [0, 0, 0] })) }]
    ]);
    source.ringSample = { sourceEntityName: 'gas_giant_rings_01_entity', bodyKind: 'decoration', scale: 1, images: source.samples.get('planet.continental').images,
        meshes: [{ ...mesh, material: { shader: 'PdxMeshPlanetRings', diff: 'd', n: 'n', spec: 's' } }] };
    value.addNavigation = () => {};
    value.setProfile(source);
    const factors = [...generated.keys()].map(id => value.resources.templates.get(id).userData.generatedDisplayFactor);
    assert.deepEqual(factors, [factors[0], factors[0], factors[0]]);
    assert.equal(factors[0], templateDisplayFactor(value.resources.templates.get('planet.continental'), 'planet.continental') * 0.5);
    assert.ok([...generated.keys()].every(id => value.resources.templates.get(id).userData.generatedDisplayPolicy === 'GeneratedMapUnitSurfaceV1'));
    assert.equal(value.resources.templates.get('star.g').scale.x, 1.3);
    for (const [id, x, band] of [['core', 40, 'core'], ['mid', 60, 'mid'], ['rim', 80, 'rim']])
    {
        const state = { spatial: { galaxyRadius: 100, systems: [{ id, starClassId: 'sc_fixture', galaxyPosition: { x, y: 0, z: 0 } }], bodies: [], fleets: [], hyperlanes: [], colonies: [], nebulae: [], belts: [] } };
        const view = value.buildView(value.resources, state, { kind: 'system', systemId: id });
        const sky = view.group.children.find(child => child.isMesh && child.renderOrder === -1000);
        assert.strictEqual(sky.material, value.resources.backgrounds.get(`system.sky:g_star:${band}`));
        value.releaseView(view);
    }
    const missingRadius = { spatial: { systems: [{ id: 'bad', starClassId: 'sc_fixture', galaxyPosition: { x: 10, y: 0, z: 0 } }], bodies: [], fleets: [], hyperlanes: [], colonies: [], nebulae: [], belts: [] } };
    assert.throws(() => value.buildView(value.resources, missingRadius, { kind: 'system', systemId: 'bad' }), /missing permitted-system distance/);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('Planet atmosphere material uses the outward source mesh with an opaque-depth silhouette falloff', () =>
{
    const value = renderer(), mesh = new THREE.SphereGeometry(2, 12, 8);
    const position = mesh.attributes.position, normal = mesh.attributes.normal;
    for (let index = 0; index < position.count; index++)
    {
        const p = new THREE.Vector3().fromBufferAttribute(position, index), n = new THREE.Vector3().fromBufferAttribute(normal, index);
        assert.ok(p.dot(n) > 0, 'source sphere normals point outward');
    }
    const resources = { textures: [], materials: [], geometries: [], textureMap: new Map() };
    const material = value.makeMaterial({ shader: 'PdxMeshAtmosphere' }, resources, { sourceAtmosphereColor: [0, .2, .3] });
    assert.equal(material.side, THREE.BackSide);
    assert.equal(material.depthTest, true); assert.equal(material.depthWrite, false);
    assert.match(material.fragmentShader, /pow\(ndotl\*1\.2,4\.0\)/);
    const falloff = cosine => Math.pow(Math.max(cosine * 1.2, 0), 4);
    assert.ok(falloff(1) > 0); assert.equal(falloff(0), 0); assert.equal(falloff(-1), 0);
    material.dispose(); mesh.dispose();
});

test('Galaxy glyphs lay out permitted one, two, three and compact stellar components', () =>
{
    const value = renderer(), source = profile();
    source.systemLighting = new Map([['g', { color: [1, .8, .5], world: 'g_star' }], ['a', { color: [.4, .6, 1], world: 'a_star' }]]);
    value.setProfile(source); value.addNavigation = () => {};
    const classes = { one: ['pc_g_star'], binary: ['pc_g_star', 'pc_a_star'], triple: ['pc_g_star', 'pc_a_star', 'pc_m_star'], hole: ['pc_black_hole'], neutron: ['pc_neutron_star'] };
    const systems = Object.keys(classes).map(id => ({ id, starClassId: 'g', galaxyPosition: { x: 10 * Object.keys(classes).indexOf(id), y: 0, z: 0 } }));
    const bodies = Object.entries(classes).flatMap(([systemId, worldClasses]) => worldClasses.map((worldClassId, index) => ({ id: `${systemId}-${index}`, kind: 'star', systemId, worldClassId, visualId: 'star.g' })));
    const state = { spatial: { systems, bodies, fleets: [], hyperlanes: [], colonies: [], nebulae: [], belts: [] } };
    const view = value.buildView(value.resources, state, { kind: 'galaxy' });
    const group = id => view.objects.get(`system:${id}`);
    const glyph = id => group(id).children.find(child => child.name === `stellar-glyph:${id}`);
    const dots = id => { const values=[]; glyph(id).traverse(child=>{if(child.isMesh&&child.geometry.type==='SphereGeometry'&&child.scale.x<1) values.push(child);}); return values; };
    assert.equal(dots('one').length, 1); assert.equal(dots('binary').length, 2); assert.equal(dots('triple').length, 3);
    assert.equal(dots('hole').length, 1); assert.equal(dots('neutron').length, 1);
    const separation = items => Math.min(...items.flatMap((left, index) => items.slice(index + 1).map(right => left.position.distanceTo(right.position))));
    assert.ok(separation(dots('binary')) > 2 * 3 * .28);
    assert.ok(separation(dots('triple')) > 2 * 3 * .28);
    const holeDot = dots('hole')[0], ring = (()=>{let found;glyph('hole').traverse(child=>{if(child.geometry?.type==='RingGeometry')found=child;});return found;})();
    assert.ok(ring.geometry.parameters.innerRadius > holeDot.scale.x * 3);
    const neutronDot = dots('neutron')[0], halo = (()=>{let found;glyph('neutron').traverse(child=>{if(child.geometry?.type==='TorusGeometry')found=child;});return found;})();
    assert.ok(halo.geometry.parameters.radius - halo.geometry.parameters.tube > neutronDot.scale.x * 3);
    for (const id of Object.keys(classes)) assert.equal(group(id).userData.pick.id, id);
    value.releaseView(view); value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});
