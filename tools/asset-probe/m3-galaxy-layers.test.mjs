import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { GALAXY_LAYER_PATHS, createGalaxyLayerTextures, createGalaxyLayerParameters, createGalaxyLayerGroup } from './m3-galaxy-layers.mjs';

function input()
{
    return new Map(Object.entries(GALAXY_LAYER_PATHS).map(([id, sourcePath]) => [id, { sourcePath, encoding: 'raw-rgba', image: { width: 1, height: 1, rgba: new Uint8Array([255, 128, 64, 128]) } }]));
}

test('Galaxy channels stay raw, source arrays are borrowed and incomplete layers fail', () =>
{
    const source = input();
    const textures = createGalaxyLayerTextures(source);
    for (const [id, texture] of textures)
    {
        assert.equal(texture.image.data, source.get(id).image.rgba);
        assert.equal(texture.colorSpace, THREE.NoColorSpace);
        assert.equal(texture.generateMipmaps, false);
        assert.equal(texture.wrapS, id === 'color' ? THREE.ClampToEdgeWrapping : THREE.RepeatWrapping);
        texture.dispose();
    }
    source.delete('color'); assert.throws(() => createGalaxyLayerTextures(source));
    source.set('color', { ...input().get('color'), encoding: 'ycocg' }); assert.throws(() => createGalaxyLayerTextures(source));
});

test('Static decorative patches use authorized extent, borrow textures and own only view resources', () =>
{
    const textures = createGalaxyLayerTextures(input());
    const parameters = createGalaxyLayerParameters(textures);
    const materials = new Map(Object.entries(parameters).map(([id, value]) => [id, new THREE.ShaderMaterial(value)]));
    const systems = [{ galaxyPosition: { x: 500, y: 0, z: -400 } }, { galaxyPosition: { x: 800, y: 0, z: -100 } }];
    const original = structuredClone(systems);
    const first = createGalaxyLayerGroup(materials, systems);
    const second = createGalaxyLayerGroup(materials, [{ galaxyPosition: { x: 0, y: 0, z: 0 } }]);
    assert.equal(first.group.children.length, 25);
    assert.deepEqual(first.group.children[0].position.toArray(), [650, -1.5, -250]);
    assert.deepEqual(first.group.children[1].material.uniforms.galaxyCenter.value.toArray(), [650, -250]);
    assert.equal(first.group.children[1].material.uniforms.source.value, textures.get('dust'));
    assert.notEqual(first.group.children[1].material, second.group.children[1].material);
    assert.deepEqual(systems, original);
    let textureDisposals = 0;
    for (const texture of textures.values()) { texture.addEventListener('dispose', () => textureDisposals++); }
    for (const resource of [...first.owned, ...second.owned]) { resource.dispose(); }
    assert.equal(textureDisposals, 0);
    assert.equal(createGalaxyLayerGroup(materials, []).group.children.length, 0);
    for (const material of materials.values()) { material.dispose(); }
    for (const texture of textures.values()) { texture.dispose(); }
    assert.equal(textureDisposals, 3);
});
