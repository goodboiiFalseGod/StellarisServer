import test from 'node:test';
import assert from 'node:assert/strict';
import { outlinerGroups, commandFleet } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';
import { outlinerNavigation } from '../../src/Stellaris.Server/wwwroot/m3-outliner-navigation.mjs';
import { planetPanelModel } from '../../src/Stellaris.Server/wwwroot/m3-planet-panel.mjs';

// Deliberately adversarial synthetic DTOs, not evidence of native startup, transport or physical input.
function snapshot(owner, observer = false)
{
    return {
        you: observer ? 3 : owner, viewContext: { mode: 'player', participantId: owner },
        participants: [{ id: 1, role: 'player' }, { id: 2, role: 'player' }, { id: 3, role: 'observer' }],
        spatial: {
            systems: [{ id: 's1', name: 'One' }, { id: 's2', name: 'Two' }],
            bodies: [1, 2].map(id => ({ id: `b${id}`, name: `Body ${id}`, systemId: `s${id}`, ownerParticipantId: id })),
            colonies: [1, 2].map(id => ({ id: `c${id}`, bodyId: `b${id}`, ownerParticipantId: id, economyPlanetId: id * 10 })),
            fleets: [1, 2].map(id => ({ id: `f${id}`, name: `Fleet ${id}`, ownerParticipantId: id, shipCount: 1, phase: 'idle', location: { kind: 'system', systemId: `s${id}` } }))
        },
        planets: [1, 2].map(id => ({ id: id * 10, ownerId: id, population: id * 100, workforce: id * 80, jobs: {} })),
        development: { contentProfileId: 'qa-synthetic', gates: [], colonies: [1, 2].map(id => ({ colonyId: `c${id}`, ownerParticipantId: id, economyPlanetId: id * 10, catalog: [{ definitionId: `qa-owner-${id}` }] })) }
    };
}

test('Independent cross-module personal rows navigate and bind the same player colony without granting observer commands', () =>
{
    for (const owner of [1, 2])
    {
        for (const observer of [false, true])
        {
            const state = snapshot(owner, observer); const before = structuredClone(state);
            const rows = outlinerGroups(state).flatMap(group => group.rows);
            assert.deepEqual(rows.map(row => row.id).sort(), [`c${owner}`, `f${owner}`]);
            for (const row of rows) { assert.deepEqual(outlinerNavigation(state, row), { systemId: `s${owner}`, reason: null }); }
            const model = planetPanelModel(state, rows.find(row => row.kind === 'colony'));
            assert.equal(model.ownerId, owner); assert.equal(model.economy.population, owner * 100);
            assert.equal(model.development.catalog[0].definitionId, `qa-owner-${owner}`);
            assert.equal(commandFleet(state, `f${owner}`)?.id ?? null, observer ? null : `f${owner}`);
            assert.equal(commandFleet(state, `f${owner === 1 ? 2 : 1}`), null);
            assert.deepEqual(state, before);
        }
    }
});

test('Independent delayed row resolves latest location and rejects transit, removed systems and ownership changes', () =>
{
    const state = snapshot(1); const row = outlinerGroups(state).flatMap(group => group.rows).find(item => item.kind === 'fleet');
    const fleet = state.spatial.fleets[0];
    fleet.location.systemId = 's2'; assert.equal(outlinerNavigation(state, row).systemId, 's2');
    state.spatial.systems = state.spatial.systems.filter(item => item.id !== 's2');
    assert.equal(outlinerNavigation(state, row).systemId, null); assert.ok(outlinerNavigation(state, row).reason);
    fleet.location = { kind: 'transit', fromSystemId: 's1', toSystemId: 's2' };
    assert.equal(outlinerNavigation(state, row).systemId, null); assert.ok(outlinerNavigation(state, row).reason);
    fleet.location = { kind: 'system', systemId: 's1' };
    assert.equal(outlinerNavigation(state, row).systemId, 's1');
    fleet.ownerParticipantId = 2; assert.equal(commandFleet(state, row.id), null);
    assert.equal(outlinerNavigation(state, row).systemId, null); assert.ok(outlinerNavigation(state, row).reason);
    assert.ok(!outlinerGroups(state).flatMap(group => group.rows).some(item => item.id === row.id));
    state.spatial.fleets = []; assert.equal(outlinerNavigation(state, row).systemId, null);
});

test('Independent stale private links cannot cross-bind development or economy after a POV switch', () =>
{
    const state = snapshot(1, true); const selection = { kind: 'colony', id: 'c1' };
    state.development.colonies[0].economyPlanetId = 20;
    assert.equal(planetPanelModel(state, selection).development, null);
    state.spatial.colonies[0].economyPlanetId = 20;
    assert.equal(planetPanelModel(state, selection).economy, null);
    state.viewContext.participantId = 2;
    assert.equal(planetPanelModel(state, selection), null); assert.equal(outlinerNavigation(state, selection).systemId, null);
    for (const mode of ['general', 'pending'])
    {
        state.viewContext = { mode, participantId: null };
        assert.deepEqual(outlinerGroups(state), []); assert.equal(planetPanelModel(state, { kind: 'colony', id: 'c2' }), null);
        assert.equal(outlinerNavigation(state, { kind: 'fleet', id: 'f2' }).systemId, null); assert.equal(commandFleet(state, 'f2'), null);
    }
});
