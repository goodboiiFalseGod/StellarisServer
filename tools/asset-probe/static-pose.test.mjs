import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { parsePdxMesh } from './formats.mjs';
import { validateStaticBindPose } from './static-pose.mjs';
import { Matrix4, Quaternion, Vector3 } from './node_modules/three/build/three.module.js';

const i32 = values => Int32Array.from(values);
const f32 = values => Float32Array.from(values);
const node = (name, properties = {}, children = []) => ({ name, properties, children });
const identity = () => f32([1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]);

function fixture()
{
    const positions = f32([2, 3, 4]);
    const normals = f32([0, 1, 0]);
    const tangents = f32([1, 0, 0, 1]);
    const uv = f32([0.3, 0.7]);
    const bone = node('root', { ix: i32([0]), tx: f32([0, 1, 0, -1, 0, 0, 0, 0, 2, 7, 8, 9]) });
    const skeleton = node('skeleton', {}, [bone]);
    const skin = node('skin', { bones: i32([1]), ix: i32([0, -1, -1, -1]), w: f32([1, 0, 0, 0]) });
    const meshNode = node('mesh', { p: positions }, [skin]);
    const shape = node('shape', {}, [meshNode, skeleton]);
    const mesh = { name: 'shape', positions, normals, tangents, uv, skinned: true };
    return { parsed: { nodes: [node('object', {}, [shape])], meshes: [mesh] }, bone, skeleton, skin, shape, meshNode, mesh };
}

test('Static pose: nonidentity inverse bind сохраняет p/n/ta/UV и общий origin', () =>
{
    const { parsed, mesh } = fixture();
    const before = structuredClone(mesh);
    const result = validateStaticBindPose(parsed);
    assert.strictEqual(result.meshes, parsed.meshes);
    for (const key of ['positions', 'normals', 'tangents', 'uv'])
    {
        assert.strictEqual(result.meshes[0][key], mesh[key]);
        assert.deepEqual(result.meshes[0][key], before[key]);
    }
    assert.equal(result.skinnedLayerCount, 1);
    assert.deepEqual(result.layers, [{ name: 'shape', staticBindPose: true, nonIdentityInverseBind: true }]);
});

test('Static pose: identity и nonskinned layer проходят без фиктивного skin', () =>
{
    const value = fixture(); value.bone.properties.tx = identity();
    assert.equal(validateStaticBindPose(value.parsed).layers[0].nonIdentityInverseBind, false);
    value.meshNode.children = []; value.shape.children = [value.meshNode]; value.mesh.skinned = false;
    assert.equal(validateStaticBindPose(value.parsed).skinnedLayerCount, 0);
    assert.equal(validateStaticBindPose(value.parsed).skeletonCount, 0);
});

test('Static pose: skeleton структура, unknown attributes, singular/NaN matrices', () =>
{
    const bad = mutate => { const value = fixture(); mutate(value); assert.throws(() => validateStaticBindPose(value.parsed), /Static pose:/); };
    bad(v => { v.bone.properties.extra = i32([0]); });
    bad(v => { v.skeleton.properties.extra = i32([0]); });
    bad(v => { v.bone.children.push(node('extra')); });
    bad(v => { v.bone.properties.tx = f32([1]); });
    bad(v => { v.bone.properties.tx[0] = NaN; });
    bad(v => { v.bone.properties.tx[0] = Infinity; });
    bad(v => { v.bone.properties.tx = f32(Array(12).fill(0)); });
    bad(v => { v.bone.properties.tx = identity(); v.bone.properties.tx[8] = 1e-13; });
    bad(v => { v.bone.properties.ix = i32([-1]); });
    bad(v => { v.bone.properties.ix = i32([0, 1]); });
    bad(v => { v.bone.properties.pa = i32([99]); });
    bad(v => { v.skeleton.children = []; });
    bad(v => { v.skeleton.children = Array.from({ length: 257 }, (_, ix) => node('bone' + ix, { ix: i32([ix]), tx: identity() })); });
    bad(v => { v.skeleton.children.push(node('root', { ix: i32([1]), pa: i32([0]), tx: identity() })); });
    bad(v => { v.skeleton.children.push(node('second', { ix: i32([0]), pa: i32([0]), tx: identity() })); });
    bad(v => { v.skeleton.children.push(node('second', { ix: i32([1]), tx: identity() })); });
    bad(v => { v.skeleton.children.push(node('a', { ix: i32([1]), pa: i32([2]), tx: identity() }), node('b', { ix: i32([2]), pa: i32([1]), tx: identity() })); });
});

test('Static pose: rigid weights/indices и привязка skeleton проверяются явно', () =>
{
    const bad = mutate => { const value = fixture(); mutate(value); assert.throws(() => validateStaticBindPose(value.parsed), /Static pose:/); };
    bad(v => { v.skin.properties.extra = i32([0]); });
    bad(v => { v.skin.children.push(node('extra')); });
    bad(v => { v.skin.properties.bones[0] = 2; });
    bad(v => { v.skin.properties.ix = i32([0]); });
    bad(v => { v.skin.properties.w = f32([1]); });
    bad(v => { v.skin.properties.ix[0] = -1; });
    bad(v => { v.skin.properties.ix[0] = 100; });
    bad(v => { v.skin.properties.ix[1] = 100; });
    bad(v => { v.skin.properties.ix[1] = -2; });
    bad(v => { v.skin.properties.w[0] = 0.5; });
    bad(v => { v.skin.properties.w[0] = NaN; });
    bad(v => { v.skin.properties.w[1] = -1e-8; });
    bad(v => { v.skin.properties.w[0] = 0; });
    bad(v => { v.skin.properties.w[1] = 1; v.skin.properties.ix[1] = 0; });
    bad(v => { v.meshNode.children.push(v.skin); });
    bad(v => { v.mesh.skinned = false; });
    bad(v => { v.shape.children = [v.meshNode]; });
    bad(v => { v.shape.children.push(v.skeleton); });
    bad(v => { v.parsed.nodes.push(node('unmatched', {}, [v.skin])); v.meshNode.children = []; v.mesh.skinned = false; });
});

test('Static pose: допуск rigid weights и неактивные существующие индексы', () =>
{
    const value = fixture();
    value.skin.properties.w[0] = 1 - 5e-7;
    value.skin.properties.w[1] = 5e-7;
    value.skin.properties.ix[1] = 0;
    const before = structuredClone(value.skin.properties);
    assert.equal(validateStaticBindPose(value.parsed).skinnedLayerCount, 1);
    assert.deepEqual(value.skin.properties, before);
});

test('Static pose: skeleton copies сравниваются с допуском, без смешения поз', () =>
{
    const { parsed, skeleton } = fixture();
    const copy = structuredClone(skeleton);
    parsed.nodes.push(node('other', {}, [copy]));
    copy.children[0].properties.tx[9] += 5e-7;
    assert.equal(validateStaticBindPose(parsed).skeletonCount, 2);
    copy.children[0].properties.tx[9] += 1e-4;
    assert.throws(() => validateStaticBindPose(parsed), /не совпадают/);
});

test('Реальная static pose: planet 5/4skin, star 2/1skin; массивы не изменены', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    for (const [file, layers, skins] of [['planet_clouded.mesh', 5, 4], ['base_star.mesh', 2, 1]])
    {
        const bytes = await readFile(resolve(process.env.STELLARIS_GAME_ROOT, 'gfx/models/planets', file));
        const parsed = parsePdxMesh(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
        const before = parsed.meshes.map(mesh => structuredClone(mesh));
        const result = validateStaticBindPose(parsed);
        assert.equal(result.meshes.length, layers);
        assert.equal(result.skinnedLayerCount, skins);
        assert.equal(result.skeletonCount, skins);
        assert.equal(result.layers.filter(layer => layer.nonIdentityInverseBind).length, skins);
        for (let index = 0; index < layers; index++)
        {
            for (const key of ['positions', 'normals', 'tangents', 'uv']) { assert.deepEqual(result.meshes[index][key], before[index][key]); }
        }
    }
});

// Только исследовательский тест: idle.anim не является runtime-зависимостью адаптера.
function readResearchAnimation(bytes)
{
    assert.equal(bytes.toString('ascii', 0, 4), '@@b@');
    assert.ok(bytes.length < 1024 * 1024);
    let offset = 4;
    let path = [];
    const records = new Map();
    while (offset < bytes.length)
    {
        const token = bytes[offset++];
        if (token === 91)
        {
            let depth = 1;
            while (bytes[offset] === 91) { offset++; depth++; }
            const end = bytes.indexOf(0, offset);
            assert.ok(end >= offset && depth <= 4);
            path = path.slice(0, depth - 1);
            path.push(bytes.toString('ascii', offset, end));
            offset = end + 1;
            continue;
        }
        assert.equal(token, 33);
        const length = bytes[offset++];
        const name = bytes.toString('ascii', offset, offset + length);
        offset += length;
        const kind = String.fromCharCode(bytes[offset++]);
        const count = bytes.readUInt32LE(offset);
        offset += 4;
        assert.ok(count <= 100000 && ['i', 'f', 's'].includes(kind));
        const values = [];
        for (let index = 0; index < count; index++)
        {
            if (kind === 's')
            {
                const size = bytes.readUInt32LE(offset);
                offset += 4;
                assert.ok(size > 0 && offset + size <= bytes.length);
                values.push(bytes.toString('ascii', offset, offset + size - 1));
                offset += size;
            }
            else
            {
                values.push(kind === 'i' ? bytes.readInt32LE(offset) : bytes.readFloatLE(offset));
                offset += 4;
            }
        }
        const key = path.join('/');
        if (!records.has(key)) { records.set(key, {}); }
        assert.ok(!Object.hasOwn(records.get(key), name));
        records.get(key)[name] = values;
    }
    return records;
}

test('Исследовательская регрессия: оригинальная idle TRS × tx = Identity', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    for (const [meshFile, animationFile] of [['planet_clouded.mesh', 'clouded_planet_idle.anim'], ['base_star.mesh', 'base_star_idle.anim']])
    {
        const directory = resolve(process.env.STELLARIS_GAME_ROOT, 'gfx/models/planets');
        const bytes = await readFile(resolve(directory, meshFile));
        const parsed = parsePdxMesh(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
        const animation = readResearchAnimation(await readFile(resolve(directory, animationFile)));
        const skeleton = parsed.nodes[0].children.flatMap(shape => shape.children).find(child => child.name === 'skeleton');
        const bones = new Map(skeleton.children.map(bone => [bone.properties.ix[0], bone]));
        const globals = new Map();
        function globalPose(id)
        {
            if (globals.has(id)) { return globals.get(id); }
            const bone = bones.get(id);
            const source = animation.get('info/' + bone.name) ?? animation.get('info/_' + bone.name);
            assert.ok(source);
            const local = new Matrix4().compose(new Vector3(...source.t), new Quaternion(...source.q).normalize(), new Vector3(source.s[0], source.s[0], source.s[0]));
            const global = bone.properties.pa ? globalPose(bone.properties.pa[0]).clone().multiply(local) : local;
            globals.set(id, global);
            return global;
        }
        for (const [id, bone] of bones)
        {
            const tx = bone.properties.tx;
            const inverseBind = new Matrix4().set(tx[0], tx[3], tx[6], tx[9], tx[1], tx[4], tx[7], tx[10], tx[2], tx[5], tx[8], tx[11], 0, 0, 0, 1);
            const product = globalPose(id).clone().multiply(inverseBind);
            const identityElements = new Matrix4().elements;
            assert.ok(product.elements.every((value, index) => Math.abs(value - identityElements[index]) <= 1e-6), meshFile + ':' + bone.name);
        }
    }
});
