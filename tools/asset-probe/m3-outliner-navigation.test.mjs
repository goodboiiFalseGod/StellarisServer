import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { outlinerNavigation } from '../../src/Stellaris.Server/wwwroot/m3-outliner-navigation.mjs';

function snapshot({ you = 3, role = 'observer', mode = 'player', participantId = 1 } = {})
{
    return {
        you,
        viewContext: { mode, participantId },
        participants: [{ id: 1, role: 'player' }, { id: 2, role: 'player' }, { id: 3, role }],
        spatial: {
            systems: [{ id: 's1' }, { id: 's2' }],
            bodies: [{ id: 'b1', ownerParticipantId: 1, systemId: 's1' }, { id: 'b2', ownerParticipantId: 2, systemId: 's2' }],
            colonies: [{ id: 'c1', ownerParticipantId: 1, bodyId: 'b1' }, { id: 'c2', ownerParticipantId: 2, bodyId: 'b2' }],
            fleets: [
                { id: 'f1', ownerParticipantId: 1, location: { kind: 'system', systemId: 's2' } },
                { id: 'f2', ownerParticipantId: 2, location: { kind: 'system', systemId: 's1' } },
                { id: 'transit', ownerParticipantId: 1, location: { kind: 'transit', fromSystemId: 's1', toSystemId: 's2' } },
                { id: 'foreignFleet', ownerParticipantId: 2, location: { kind: 'system', systemId: 's1' } }
            ]
        }
    };
}

test('Owned colonies, bodies and system-located fleets resolve received systems for players and observer POV', () =>
{
    const cases = [
        { state: snapshot({ you: 1, role: 'player', participantId: 1 }), colony: 'c1', body: 'b1', fleet: 'f1', expectedSystem: 's1', expectedFleetSystem: 's2' },
        { state: snapshot({ you: 2, role: 'player', participantId: 2 }), colony: 'c2', body: 'b2', fleet: 'f2', expectedSystem: 's2', expectedFleetSystem: 's1' },
        { state: snapshot(), colony: 'c1', body: 'b1', fleet: 'f1', expectedSystem: 's1', expectedFleetSystem: 's2' },
        { state: snapshot({ participantId: 2 }), colony: 'c2', body: 'b2', fleet: 'f2', expectedSystem: 's2', expectedFleetSystem: 's1' }
    ];
    for (const { state, colony, body, fleet, expectedSystem, expectedFleetSystem } of cases)
    {
        assert.deepEqual(outlinerNavigation(state, { kind: 'colony', id: colony }), { systemId: expectedSystem, reason: null });
        assert.deepEqual(outlinerNavigation(state, { kind: 'body', id: body }), { systemId: expectedSystem, reason: null });
        assert.deepEqual(outlinerNavigation(state, { kind: 'fleet', id: fleet }), { systemId: expectedFleetSystem, reason: null });
    }
});

test('Transit, foreign and removed objects fail visibly', () =>
{
    const state = snapshot();
    assert.match(outlinerNavigation(state, { kind: 'fleet', id: 'transit' }).reason, /в пути/);
    assert.match(outlinerNavigation(state, { kind: 'fleet', id: 'foreignFleet' }).reason, /не принадлежит/);
    assert.match(outlinerNavigation(state, { kind: 'colony', id: 'removed' }).reason, /отсутствует/);
    assert.match(outlinerNavigation(state, { kind: 'unsupported', id: 'f1' }).reason, /отсутствует/);
});

test('Missing colony body or received system fails visibly', () =>
{
    const noBody = snapshot(); noBody.spatial.bodies = [];
    assert.match(outlinerNavigation(noBody, { kind: 'colony', id: 'c1' }).reason, /Тело колонии отсутствует/);
    const noSystem = snapshot(); noSystem.spatial.systems = [];
    assert.match(outlinerNavigation(noSystem, { kind: 'body', id: 'b1' }).reason, /Система объекта отсутствует/);
    assert.match(outlinerNavigation(noSystem, { kind: 'fleet', id: 'f1' }).reason, /Система объекта отсутствует/);
});

test('General, pending and unconfirmed views fail visibly', () =>
{
    assert.match(outlinerNavigation(snapshot({ mode: 'general', participantId: null }), { kind: 'body', id: 'b1' }).reason, /общем обзоре/);
    assert.match(outlinerNavigation(snapshot({ mode: 'pending', participantId: null }), { kind: 'body', id: 'b1' }).reason, /Ожидайте/);
    assert.match(outlinerNavigation(snapshot({ participantId: 99 }), { kind: 'body', id: 'b1' }).reason, /не подтверждён/);
});

test('Observer navigation is read-only and does not mutate the received snapshot', () =>
{
    const state = snapshot();
    const before = structuredClone(state);
    const result = outlinerNavigation(state, { kind: 'colony', id: 'c1' });
    assert.deepEqual(result, { systemId: 's1', reason: null });
    assert.deepEqual(state, before);
});

test('Production double-click opens the current system and focuses the exact object, with visible failed paths', () =>
{
    const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
    const source = client.slice(client.indexOf("$('outliner').ondblclick ="), client.indexOf('function renderControls()'));
    const outliner = {}; const calls = [];
    const context = { $: () => outliner, state: snapshot(), outlinerNavigation, error: reason => calls.push(['error', reason]), openSystem: id => { calls.push(['open', id]); return true; }, select: value => calls.push(['inspect', value.id]), renderer: { select: value => calls.push(['select', value.id]), focusSelection: () => calls.push(['focus']) } };
    vm.runInNewContext(source, context);
    const event = (id, disabled = false) => ({ target: { closest: () => ({ disabled, dataset: { kind: 'fleet', id } }) } });
    outliner.ondblclick(event('f1'));
    assert.deepEqual(calls, [['open', 's2'], ['select', 'f1'], ['inspect', 'f1'], ['focus']]);
    calls.length = 0; outliner.ondblclick(event('transit'));
    assert.equal(calls.length, 1); assert.equal(calls[0][0], 'error');
    calls.length = 0; outliner.ondblclick(event('f1', true)); assert.deepEqual(calls, []);
    context.openSystem = () => false; outliner.ondblclick(event('f1')); assert.deepEqual(calls, []);
    context.openSystem = () => true; context.renderer.focusSelection = () => { throw new Error('Focus unavailable'); };
    outliner.ondblclick(event('f1')); assert.deepEqual(calls.at(-1), ['error', 'Focus unavailable']);
});
