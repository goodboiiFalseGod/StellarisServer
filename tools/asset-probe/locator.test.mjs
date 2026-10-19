import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { parsePdxMesh } from './formats.mjs';

const arrayBuffer = value => value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength);
const node = (depth, name) => Buffer.from('['.repeat(depth) + name + '\0');
function property(name, kind, values)
{
    const prefix = Buffer.from([33, name.length, ...Buffer.from(name), kind.charCodeAt(0), values.length, 0, 0, 0]);
    const data = Buffer.alloc(values.length * 4);
    values.forEach((value, i) => kind === 'f' ? data.writeFloatLE(value, i * 4) : data.writeInt32LE(value, i * 4));
    return Buffer.concat([prefix, data]);
}
function frame({ position = [1, 2, 3], quaternion = [0, 0, 0, -2], duplicate = false, extra = null, name = 'part1' } = {})
{
    const parts = [Buffer.from('@@b@'), property('pdxasset', 'i', [1, 0]), node(1, 'locator'), node(2, name), property('p', 'f', position), property('q', 'f', quaternion)];
    if (duplicate) { parts.push(node(2, name), property('p', 'f', position), property('q', 'f', quaternion)); }
    if (extra) { parts.push(node(3, extra)); }
    return arrayBuffer(Buffer.concat(parts));
}

test('PDX locator-only: явный режим, исходная позиция и нормализованный quaternion без геометрии', () =>
{
    const input = frame();
    assert.throws(() => parsePdxMesh(input), /нет отображаемых meshes/);
    const result = parsePdxMesh(input, { locatorOnly: true });
    assert.deepEqual(result.version, [1, 0]);
    assert.deepEqual(result.locators, [{ name: 'part1', position: [1, 2, 3], quaternion: [0, 0, 0, -1] }]);
    assert.deepEqual(result.meshes, []);
    assert.deepEqual(result.unsupportedFeatures, []);
    assert.equal(result.nodes[0].name, 'locator');
    assert.equal(result.nodes[0].children[0].properties.q[3], -2);
});

test('PDX locator-only: geometry/skin, дубли, неизвестные поля и неверные transforms отвергаются', () =>
{
    for (const extra of ['mesh', 'skin', 'skeleton', 'unknown']) { assert.throws(() => parsePdxMesh(frame({ extra }), { locatorOnly: true }), /geometry\/skin/); }
    assert.throws(() => parsePdxMesh(frame({ duplicate: true }), { locatorOnly: true }), /единственный/);
    assert.throws(() => parsePdxMesh(frame({ name: 'part2' }), { locatorOnly: true }), /единственный/);
    for (const position of [[1, 2], [NaN, 0, 0], [Infinity, 0, 0]]) { assert.throws(() => parsePdxMesh(frame({ position }), { locatorOnly: true }), /float3|NaN\/Infinity/); }
    for (const quaternion of [[0, 0, 0], [0, 0, 0, 0], [NaN, 0, 0, 1]]) { assert.throws(() => parsePdxMesh(frame({ quaternion }), { locatorOnly: true }), /float4|ненулевым|NaN\/Infinity/); }
    const rootGeometry = Buffer.concat([Buffer.from(frame()), node(1, 'object')]);
    assert.throws(() => parsePdxMesh(arrayBuffer(rootGeometry), { locatorOnly: true }), /geometry\/skin/);
    const unknownProperty = Buffer.concat([Buffer.from(frame()), property('s', 'f', [1])]);
    assert.throws(() => parsePdxMesh(arrayBuffer(unknownProperty), { locatorOnly: true }), /float3/);
    assert.throws(() => parsePdxMesh(frame().slice(0, -1), { locatorOnly: true }), /усечённые/);
});

test('Реальный outpost: locator-only frame 88B и три самостоятельных submeshes', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const folder = resolve(process.env.STELLARIS_GAME_ROOT, 'gfx/models/ships/starbases');
    const data = await readFile(resolve(folder, 'mammalian_01_outpost_frame.mesh'));
    assert.equal(data.length, 88);
    assert.throws(() => parsePdxMesh(arrayBuffer(data)), /нет отображаемых meshes/);
    const frameResult = parsePdxMesh(arrayBuffer(data), { locatorOnly: true });
    assert.equal(frameResult.locators.length, 1);
    assert.equal(frameResult.locators[0].name, 'part1');
    assert.ok(frameResult.locators[0].position.every(value => value === 0));
    assert.deepEqual(frameResult.locators[0].quaternion.slice(0, 2), [0, 0]);
    assert.equal(frameResult.locators[0].quaternion[2], -0);
    assert.equal(frameResult.locators[0].quaternion[3], -1);
    const sectionData = await readFile(resolve(folder, 'mammalian_01_outpost.mesh'));
    assert.throws(() => parsePdxMesh(arrayBuffer(sectionData), { locatorOnly: true }), /geometry\/skin/);
    const section = parsePdxMesh(arrayBuffer(sectionData));
    assert.equal(section.meshes.length, 3);
    assert.equal(section.skippedCollision, 0);
    assert.deepEqual(section.unsupportedFeatures, []);
    const expected = [['stations', 6605, 4424], ['corvette', 372, 504], ['battleship', 102, 110]];
    section.meshes.forEach((mesh, meshIndex) =>
    {
        const [kind, vertices, triangles] = expected[meshIndex];
        assert.equal(mesh.name, 'mammalian_01_outpost_meshShape');
        assert.equal(mesh.positions.length / 3, vertices);
        assert.equal(mesh.indices.length / 3, triangles);
        assert.equal(mesh.skinned, false);
        assert.deepEqual(mesh.material, { shader: 'PdxMeshShip', diff: `mammalian_01_${kind}_diffuse.dds`, n: `mammalian_01_${kind}_normal.dds`, spec: `mammalian_01_${kind}_specular.dds` });
        assert.ok(mesh.indices.every(index => index < vertices));
    });
});
