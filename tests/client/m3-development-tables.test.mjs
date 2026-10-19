import test from 'node:test';
import assert from 'node:assert/strict';
import { hydrateDevelopmentTables } from '../../src/Stellaris.Server/wwwroot/m3-development-tables.mjs';

const compact = () => ({ development: {
    sourceRecipeTerms: [
        ['production', 'minerals', 2, null, 'Included', 'Planet'],
        ['upkeep', 'energy', null, 'base = 1', 'Conditional', 'Building'],
    ],
    catalogSlotSets: [['slot-b', 'slot-a', 'slot-b']],
    colonies: [{ colonyId: 'C1', catalog: [{ definitionId: 'building_lab', sourceRecipe: null, compatibleSlotIds: null, sourceRecipeIndices: [1, 0, 1], compatibleSlotSetIndex: 0 }] }],
} });

test('Development tables restore exact recipe and slot order with duplicates before presentation', () =>
{
    const wire = compact(); const decoded = hydrateDevelopmentTables(wire);
    const entry = decoded.development.colonies[0].catalog[0];
    assert.deepEqual(entry.sourceRecipe, [
        { kind: 'upkeep', targetId: 'energy', value: null, expression: 'base = 1', conditionDisposition: 'Conditional', applicationScope: 'Building' },
        { kind: 'production', targetId: 'minerals', value: 2, expression: null, conditionDisposition: 'Included', applicationScope: 'Planet' },
        { kind: 'upkeep', targetId: 'energy', value: null, expression: 'base = 1', conditionDisposition: 'Conditional', applicationScope: 'Building' },
    ]);
    assert.deepEqual(entry.compatibleSlotIds, ['slot-b', 'slot-a', 'slot-b']);
    assert.equal(wire.development.colonies[0].catalog[0].sourceRecipe, null);
    assert.notEqual(entry.sourceRecipe[0], entry.sourceRecipe[2]);
    const omitted = compact(); delete omitted.development.colonies[0].catalog[0].sourceRecipe; delete omitted.development.colonies[0].catalog[0].compatibleSlotIds;
    assert.deepEqual(hydrateDevelopmentTables(omitted).development.colonies[0].catalog[0].compatibleSlotIds, ['slot-b', 'slot-a', 'slot-b']);
});

test('Nullable references preserve unresolved gated catalog metadata as null or absent', () =>
{
    const wire = compact(); wire.development.colonies[0].catalog.push({ definitionId: 'unresolved', sourceRecipe: null, compatibleSlotIds: null, sourceRecipeIndices: null, compatibleSlotSetIndex: null });
    wire.development.colonies[0].catalog.push({ definitionId: 'omitted' });
    const [, gated, omitted] = hydrateDevelopmentTables(wire).development.colonies[0].catalog;
    assert.equal(gated.sourceRecipe, null); assert.equal(gated.compatibleSlotIds, null);
    assert.equal(Object.hasOwn(omitted, 'sourceRecipe'), false); assert.equal(Object.hasOwn(omitted, 'compatibleSlotIds'), false);
});

test('Interned strings decode the same six term fields and reject unknown or invalid string indices', () =>
{
    const wire = compact();
    wire.development.sourceRecipeEncoding = 'interned-strings-v1';
    wire.development.sourceRecipeStrings = ['production', 'minerals', 'Included', 'Planet', 'upkeep', 'energy', 'base = 1', 'Conditional', 'Building'];
    wire.development.sourceRecipeTerms = [[0, 1, 2, null, 2, 3], [4, 5, null, 6, 7, 8]];
    const entry = hydrateDevelopmentTables(wire).development.colonies[0].catalog[0];
    assert.deepEqual(entry.sourceRecipe.map(term => term.kind), ['upkeep', 'production', 'upkeep']);
    assert.equal(entry.sourceRecipe[0].expression, 'base = 1');
    assert.equal(entry.sourceRecipe[1].value, 2);
    for (const change of [
        value => { value.development.sourceRecipeEncoding = 'unknown-v2'; },
        value => { value.development.sourceRecipeTerms[0][0] = 99; },
        value => { value.development.sourceRecipeTerms[1][3] = 'base = 1'; },
        value => { value.development.sourceRecipeStrings[0] = 7; },
        value => { delete value.development.sourceRecipeStrings; },
    ]) { const invalid = structuredClone(wire); change(invalid); assert.throws(() => hydrateDevelopmentTables(invalid), /Некорректные таблицы развития/); }
});

test('Malformed table rows and references fail closed rather than turning into empty recipe or slots', () =>
{
    const changes = [
        wire => { wire.development.sourceRecipeTerms[0].pop(); },
        wire => { wire.development.sourceRecipeTerms[0][2] = '2'; },
        wire => { wire.development.sourceRecipeTerms[0][3] = 4; },
        wire => { wire.development.catalogSlotSets[0][0] = null; },
        wire => { wire.development.colonies[0].catalog[0].sourceRecipeIndices = [2]; },
        wire => { wire.development.colonies[0].catalog[0].sourceRecipeIndices = [-1]; },
        wire => { wire.development.colonies[0].catalog[0].compatibleSlotSetIndex = 1; },
        wire => { wire.development.colonies[0].catalog[0].sourceRecipe = []; },
        wire => { wire.development.colonies[0].catalog[0].compatibleSlotIds = []; },
        wire => { delete wire.development.catalogSlotSets; },
    ];
    for (const change of changes) { const wire = compact(); change(wire); assert.throws(() => hydrateDevelopmentTables(wire), /Некорректные таблицы развития/); }
});

test('Legacy fixture without tables retains its original inline catalog', () =>
{
    const legacy = { development: { colonies: [{ catalog: [{ sourceRecipe: [{ kind: 'legacy' }], compatibleSlotIds: ['slot'] }] }] } };
    assert.equal(hydrateDevelopmentTables(legacy), legacy);
    const malformed = structuredClone(legacy); malformed.development.colonies[0].catalog[0].sourceRecipeIndices = [0];
    assert.throws(() => hydrateDevelopmentTables(malformed), /индексы без таблиц/);
});
