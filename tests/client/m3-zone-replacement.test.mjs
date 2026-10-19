import test from 'node:test';
import assert from 'node:assert/strict';
import { constructionOperations } from '../../src/Stellaris.Server/wwwroot/m3-construction-controls.mjs';

const quote = () => ({ policyId: 'ordinary-v1', durationDays: 12, costs: { minerals: 50 }, infrastructureUpkeep: {}, jobCapacity: {} });

function model()
{
    const replacement = {
        sourceDefinitionId: 'zone_industrial', definitionId: 'zone_foundry', slotId: 'zone-slot-a', zoneInstanceId: 'zone-a',
        expectedColonyRevision: 8, previewId: 'preview-zone-a', canEnqueue: true, canAfford: true, gates: [], quote: quote(),
        removedBuildings: [{ slotId: 'building-factory-a', definitionId: 'building_factory', localizationKey: 'building_factory_name' }]
    };
    return {
        id: 'colony-a', gates: [],
        development: {
            revision: 8, gates: [], catalog: [], queue: [],
            slots: [
                { slotId: 'zone-slot-a', kind: 'zone', zoneInstanceId: 'zone-a', installedDefinitionId: 'zone_industrial', gates: [] },
                { slotId: 'building-factory-a', kind: 'building', zoneInstanceId: 'zone-a', installedDefinitionId: 'building_factory', gates: [] },
                { slotId: 'building-foundry-a', kind: 'building', zoneInstanceId: 'zone-a', installedDefinitionId: 'building_foundry', gates: [] },
                { slotId: 'zone-slot-b', kind: 'zone', zoneInstanceId: 'zone-b', installedDefinitionId: 'zone_city', gates: [] },
                { slotId: 'building-other-b', kind: 'building', zoneInstanceId: 'zone-b', installedDefinitionId: 'building_other', gates: [] }
            ],
            installed: [
                { slotId: 'zone-slot-a', kind: 'zone', definitionId: 'zone_industrial', zoneReplacements: [replacement] },
                { slotId: 'building-factory-a', kind: 'building', definitionId: 'building_factory' },
                { slotId: 'building-foundry-a', kind: 'building', definitionId: 'building_foundry' },
                { slotId: 'zone-slot-b', kind: 'zone', definitionId: 'zone_city' },
                { slotId: 'building-other-b', kind: 'building', definitionId: 'building_other' }
            ]
        }
    };
}

const replacements = state => constructionOperations(state).filter(operation => operation.type === 'enqueueZoneReplacement');

test('offers a valid exact zone replacement and clones its removed-building DTO', () => {
    const state = model();
    const preview = state.development.installed[0].zoneReplacements[0];
    const [operation] = replacements(state);
    assert.ok(operation);
    assert.deepEqual(operation, {
        type: 'enqueueZoneReplacement', sourceDefinitionId: 'zone_industrial', definitionId: 'zone_foundry',
        slotId: 'zone-slot-a', zoneInstanceId: 'zone-a', zoneReplacementPreviewId: 'preview-zone-a',
        removedBuildings: [{ slotId: 'building-factory-a', definitionId: 'building_factory', localizationKey: 'building_factory_name' }],
        quote: preview.quote
    });
    assert.notEqual(operation.removedBuildings, preview.removedBuildings);
    assert.notEqual(operation.removedBuildings[0], preview.removedBuildings[0]);
});

test('an empty exact removal list is a valid replacement', () => {
    const state = model();
    state.development.installed[0].zoneReplacements[0].removedBuildings = [];
    assert.deepEqual(replacements(state)[0]?.removedBuildings, []);
});

test('does not expose unavailable, stale, gated or malformed previews', () => {
    const mutations = [
        preview => { preview.canEnqueue = false; },
        preview => { preview.canAfford = false; },
        preview => { preview.gates = [{ code: 'dependency-unresolved', dependencyId: 'unknown' }]; },
        preview => { preview.expectedColonyRevision++; },
        preview => { preview.previewId = null; },
        preview => { preview.quote = null; },
        preview => { preview.quote.durationDays = 0; },
        preview => { preview.quote.costs.minerals = -1; },
        preview => { preview.removedBuildings = null; },
        preview => { preview.removedBuildings = [{}]; },
        preview => { preview.removedBuildings = [preview.removedBuildings[0], { ...preview.removedBuildings[0] }]; },
        preview => { preview.removedBuildings[0].definitionId = 'building_foundry'; },
        preview => { preview.removedBuildings[0].slotId = 'building-other-b'; },
        preview => { preview.removedBuildings[0].localizationKey = 12; },
        preview => { preview.slotId = 'zone-slot-b'; },
        preview => { preview.zoneInstanceId = 'zone-b'; },
        preview => { preview.sourceDefinitionId = 'zone_city'; },
        preview => { preview.definitionId = 'zone_industrial'; }
    ];
    for (const mutate of mutations)
    {
        const state = model();
        mutate(state.development.installed[0].zoneReplacements[0]);
        assert.deepEqual(replacements(state), [], 'malformed preview must remain unavailable');
    }
});

test('rejects ambiguous installed zone, slot, target and removed-building identities', () => {
    const duplicateInstalled = model();
    duplicateInstalled.development.installed.push({ ...duplicateInstalled.development.installed[0] });
    assert.deepEqual(replacements(duplicateInstalled), []);

    const duplicateSlot = model();
    duplicateSlot.development.slots.push({ ...duplicateSlot.development.slots[0] });
    assert.deepEqual(replacements(duplicateSlot), []);

    const duplicateTarget = model();
    duplicateTarget.development.installed[0].zoneReplacements.push({ ...duplicateTarget.development.installed[0].zoneReplacements[0] });
    assert.deepEqual(replacements(duplicateTarget), []);

    const duplicateRemoved = model();
    const target = duplicateRemoved.development.installed[0].zoneReplacements[0];
    target.removedBuildings.push({ ...target.removedBuildings[0] });
    assert.deepEqual(replacements(duplicateRemoved), []);
});

test('blocks replacement when the parent or any child is pending or reserved', () => {
    const pendingParent = model();
    pendingParent.development.queue.push({ orderId: 'parent', slotId: 'other-slot', status: 'pending', gates: [], zoneReplacement: { zoneInstanceId: 'zone-a' } });
    assert.deepEqual(replacements(pendingParent), []);

    const pendingParentSlot = model();
    pendingParentSlot.development.queue.push({ orderId: 'parent', slotId: 'zone-slot-a', status: 'pending', gates: [] });
    assert.deepEqual(replacements(pendingParentSlot), []);

    const pendingChild = model();
    pendingChild.development.queue.push({ orderId: 'child', slotId: 'building-foundry-a', status: 'pending', gates: [] });
    assert.deepEqual(replacements(pendingChild), []);

    const reservedParent = model();
    reservedParent.development.slots[0].reservedByOrderId = 'reserved';
    assert.deepEqual(replacements(reservedParent), []);

    const reservedChild = model();
    reservedChild.development.slots[2].reservedByOrderId = 'reserved';
    assert.deepEqual(replacements(reservedChild), []);
});

test('does not trust a removed building from another occurrence', () => {
    const state = model();
    state.development.installed[0].zoneReplacements[0].removedBuildings[0].slotId = 'building-other-b';
    assert.deepEqual(replacements(state), []);
});
