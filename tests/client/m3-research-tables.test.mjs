import test from 'node:test';
import assert from 'node:assert/strict';
import { hydrateResearchTables } from '../../src/Stellaris.Server/wwwroot/m3-research-tables.mjs';

const compact = () => ({
    outerUnknown: { keep: true },
    research: {
        revision: 17,
        encoding: 'shared-unavailable-v1',
        unavailableTable: [
            { technologyId: 'tech_same', name: 'Same technology', gates: ['gate_a'], nameKey: 'TECH_SAME', dtoExtra: 1 },
            { technologyId: 'tech_same', name: 'Same technology', gates: ['gate_b', 'gate_a'], nameKey: null, dtoExtra: 2 },
        ],
        unavailableIndices: [null, [], [1, 0, 1]],
        empires: [
            { participantId: 1, unknownEmpireField: 'keep-1' },
            { participantId: 2, unknownEmpireField: 'keep-2' },
            { participantId: 3, unknownEmpireField: 'keep-3' },
        ],
        unknownResearchField: 'keep-research',
    },
});

test('Hydrates shared unavailable DTOs losslessly, preserving null, empty, order, duplicates, and unknown fields', () =>
{
    const wire = compact(); const before = structuredClone(wire);
    const result = hydrateResearchTables(wire);
    assert.deepEqual(result.research.empires.map(empire => empire.unavailable), [
        null,
        [],
        [wire.research.unavailableTable[1], wire.research.unavailableTable[0], wire.research.unavailableTable[1]],
    ]);
    assert.notEqual(result.research.empires[2].unavailable[0], result.research.empires[2].unavailable[1]);
    assert.equal(result.research.empires[2].unavailable[0], result.research.empires[2].unavailable[2]);
    assert.deepEqual(result.research.empires[2].unavailable.map(item => item.gates), [['gate_b', 'gate_a'], ['gate_a'], ['gate_b', 'gate_a']]);
    assert.equal(result.outerUnknown.keep, true);
    assert.equal(result.research.unknownResearchField, 'keep-research');
    assert.equal(result.research.empires[0].unknownEmpireField, 'keep-1');
    assert.equal(result.research.empires[2].unavailable[1].dtoExtra, 1);
    for (const key of ['encoding', 'unavailableTable', 'unavailableIndices']) { assert.equal(Object.hasOwn(result.research, key), false); }
    assert.deepEqual(wire, before, 'Hydration must not mutate its input.');
    assert.notEqual(result, wire);
    assert.notEqual(result.research, wire.research);
    assert.notEqual(result.research.empires[0], wire.research.empires[0]);
});

test('Leaves legacy and missing research snapshots untouched', () =>
{
    for (const value of [
        { research: { empires: [{ unavailable: null }] } },
        { research: { legacyField: true } },
        { sequence: 4 },
        null,
    ]) { assert.equal(hydrateResearchTables(value), value); }
});

test('Rejects unknown encodings, malformed DTO tables, misaligned arrays, and invalid references', () =>
{
    const changes = [
        wire => { wire.research.encoding = 'other-v2'; },
        wire => { wire.research.encoding = undefined; },
        wire => { wire.research.empires = {}; },
        wire => { wire.research.empires[1] = null; },
        wire => { wire.research.unavailableTable[0] = null; },
        wire => { wire.research.unavailableTable[0].gates = 'gate_a'; },
        wire => { wire.research.unavailableTable[0].gates = [null]; },
        wire => { wire.research.unavailableIndices.pop(); },
        wire => { wire.research.unavailableIndices[0] = 0; },
        wire => { wire.research.unavailableIndices[0] = [2]; },
        wire => { wire.research.unavailableIndices[0] = [-1]; },
        wire => { wire.research.unavailableIndices[0] = [1.5]; },
        wire => { wire.research.unavailableIndices[0] = [Number.MAX_SAFE_INTEGER + 1]; },
    ];
    for (const change of changes)
    {
        const wire = compact(); change(wire);
        assert.throws(() => hydrateResearchTables(wire), /Некорректная таблица исследований/);
    }
});
