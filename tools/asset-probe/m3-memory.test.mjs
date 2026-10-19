import test from 'node:test';
import assert from 'node:assert/strict';
import { computeConvertedBytes, requireConvertedBudget, MAX_CONVERTED_BYTES } from './m3-memory.mjs';

const image = () => ({ width: 2, height: 3, rgba: new Uint8Array(24) });
const mesh = (shader = 'PdxMeshShip', n = 'normal.dds', spec = 'spec.dds') => ({ material: { shader, n, spec } });
const sample = (meshes = [mesh()]) => ({ meshes, images: new Map([['normal.dds', image()], ['spec.dds', image()]]) });

test('M3 memory: уникальные path:mode, shared каналы и Phong layers', () =>
{
    const value = sample([mesh(), mesh('PdxMeshPlanet'), mesh('PdxMeshClouds')]);
    assert.equal(computeConvertedBytes(value), 48);
    assert.equal(computeConvertedBytes(sample([mesh('PdxMeshShip', 'normal.dds', 'normal.dds')])), 48);
    value.images.set('other.dds', image());
    value.meshes.push(mesh('PdxMeshClouds', 'other.dds', 'spec.dds'));
    assert.equal(computeConvertedBytes(value), 72);
});

test('M3 memory: star/atmosphere не создают converted arrays, данные не изменяются', () =>
{
    const value = sample([mesh('PdxMeshStar'), mesh('PdxMeshAtmosphere'), mesh('PdxMeshAtmosphereStar')]);
    const before = structuredClone(value);
    assert.equal(computeConvertedBytes(value), 0);
    assert.deepEqual(value, before);
    assert.equal(computeConvertedBytes(sample([])), 0);
});

test('M3 memory: missing/invalid inputs и изображения отклоняются до конвертации', () =>
{
    for (const value of [null, {}, { meshes: [], images: {} }]) { assert.throws(() => computeConvertedBytes(value), /meshes/); }
    assert.throws(() => computeConvertedBytes(sample([mesh('Unknown')])), /shader/);
    assert.throws(() => computeConvertedBytes(sample([mesh('PdxMeshShip', '')])), /канал/);
    assert.throws(() => computeConvertedBytes(sample([mesh('PdxMeshShip', 'missing.dds')])), /текстура/);
    for (const changed of [null, { width: 0 }, { width: 1.5 }, { height: Infinity }, { width: 8193 }, { rgba: new Uint8Array(23) }, { rgba: [] }])
    {
        const value = sample();
        value.images.set('normal.dds', changed === null ? null : { ...image(), ...changed });
        assert.throws(() => computeConvertedBytes(value), /текстура/);
    }
});

test('M3 memory: старый+staged budget, равенство границе и неверные лимиты', () =>
{
    assert.doesNotThrow(() => requireConvertedBudget(24, 24, 48));
    assert.throws(() => requireConvertedBudget(24, 25, 48), /converted memory budget/);
    assert.throws(() => requireConvertedBudget(49, 0, 48), /converted memory budget/);
    assert.doesNotThrow(() => requireConvertedBudget(0, 0, 0));
    assert.doesNotThrow(() => requireConvertedBudget(MAX_CONVERTED_BYTES, 0));
    assert.throws(() => requireConvertedBudget(MAX_CONVERTED_BYTES, 1), /converted memory budget/);
    for (const invalid of [-1, 0.5, Infinity, NaN, Number.MAX_SAFE_INTEGER + 1])
    {
        assert.throws(() => requireConvertedBudget(invalid, 0), /неверные/);
        assert.throws(() => requireConvertedBudget(0, invalid), /неверные/);
        assert.throws(() => requireConvertedBudget(0, 0, invalid), /неверные/);
    }
    assert.throws(() => requireConvertedBudget(0, 0, MAX_CONVERTED_BYTES + 1), /неверные/);
});
