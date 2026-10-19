import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { perspectiveKey, viewParticipantId, observerViews, commandFleet } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
const stateSource = client.slice(client.indexOf('onState: (snapshot, options) =>'), client.indexOf('\n    onResult:')).replace('onState:', 'handler =').replace(/,\s*$/, '');
const selectorSource = client.slice(client.indexOf("$('observer-view').onchange ="), client.indexOf("$('galaxy').onclick"));
const resultSource = client.slice(client.indexOf('onResult: result =>'), client.indexOf('\n});', client.indexOf('onResult: result =>'))).replace('onResult:', 'resultHandler =');

function snapshot(target = 1)
{
    return { you: 3, phase: 'running', viewContext: { mode: target ? 'player' : 'general', participantId: target || null }, participants: [{ id: 1, role: 'player', name: 'A' }, { id: 2, role: 'player', name: 'B', connected: false }, { id: 3, role: 'observer', name: 'Observer' }], spatial: { systems: [], bodies: [], colonies: [], fleets: [] }, planets: [{ ownerId: target, balances: { energy: target * 10 } }] };
}

function harness()
{
    const calls = []; const nodes = new Map();
    const context = {
        state: snapshot(), profile: {}, selected: { kind: 'fleet', id: 'old' }, commandFleetId: 'old', currentSystem: 'old-system', stressView: true, outlinerCollapsed: true,
        economyKey: 'old economy', contextLost: false, contextAwaitingSnapshot: true, perspectiveKey,
        $: id => { if (!nodes.has(id)) { nodes.set(id, { replaceChildren() { calls.push(['clear', id]); }, close() {} }); } return nodes.get(id); },
        renderer: { select(value) { calls.push(['select', value]); }, setCommandFleet(value) { calls.push(['commandFleet', value]); }, applySnapshot(state, options) { calls.push(['snapshot', state, options]); }, showGalaxy() { calls.push(['galaxy']); } },
        findSelected() { return true; }, ownFleet() { return commandFleet(context.state, context.commandFleetId); },
        renderState() { calls.push(['render', context.state]); }
    };
    vm.runInNewContext(stateSource, context);
    return { context, calls, nodes };
}

test('POV changes retain each authorized view type and reset interpolation', () =>
{
    const views = [
        { name: 'galaxy', currentSystem: null, stressView: false, systems: [] },
        { name: 'system', currentSystem: 'old-system', stressView: false, systems: [{ id: 'old-system' }] },
        { name: 'stress', currentSystem: null, stressView: true, systems: [] }
    ];
    for (const view of views)
    {
        const { context, calls } = harness(); context.currentSystem = view.currentSystem; context.stressView = view.stressView;
        for (const target of [2, 0])
        {
            const next = snapshot(target); next.spatial.systems = view.systems;
            context.handler(next, { receivedAt: 100, resetInterpolation: false });
            assert.equal(context.currentSystem, view.currentSystem, `${view.name} current system`);
            assert.equal(context.stressView, view.stressView, `${view.name} stress state`);
            assert.equal(context.selected, null); assert.equal(context.commandFleetId, null);
            assert.equal(context.outlinerCollapsed, false); assert.equal(context.economyKey, '');
            assert.ok(calls.some(call => call[0] === 'clear' && call[1] === 'economy-content'));
            assert.ok(calls.some(call => call[0] === 'clear' && call[1] === 'destination'));
            assert.equal(calls.find(call => call[0] === 'snapshot')[2].resetInterpolation, true);
            assert.equal(calls.find(call => call[0] === 'snapshot')[2].receivedAt, 100);
            assert.equal(calls.filter(call => call[0] === 'galaxy').length, 0);
            assert.equal(context.state.you, 3); assert.equal(context.contextAwaitingSnapshot, false);
            assert.equal(commandFleet(context.state, 'old'), null);
        }
    }
});

test('Ordinary updates retain local state and missing systems reconcile', () =>
{
    const { context, calls } = harness();
    context.handler(snapshot(1), { resetInterpolation: false });
    assert.equal(context.currentSystem, null); // A removed system is reconciled even without a POV change.
    assert.equal(context.selected.id, 'old'); assert.equal(context.outlinerCollapsed, true);
    assert.equal(context.economyKey, 'old economy'); assert.ok(!calls.some(call => call[0] === 'galaxy'));
    assert.equal(calls.find(call => call[0] === 'snapshot')[2].resetInterpolation, false);
    assert.equal(calls.filter(call => call[0] === 'galaxy').length, 0);
});

test('Chooser sends the target but retains confirmed context through accepted or rejected ACKs', () =>
{
    const { context, nodes } = harness(); const sent = []; const feedback = [];
    Object.assign(context, { viewParticipantId, observerViews, send: (type, fields) => sent.push([type, fields]), error: message => feedback.push(message), persist() {}, commandRow: (...args) => feedback.push(args) });
    vm.runInNewContext(selectorSource, context); vm.runInNewContext(resultSource, context);
    const chooser = nodes.get('observer-view'); chooser.value = '2'; chooser.disabled = false; chooser.onchange();
    assert.equal(sent[0][0], 'observerView'); assert.equal(sent[0][1].targetParticipantId, 2);
    assert.equal(chooser.value, '1'); assert.equal(context.state.viewContext.participantId, 1);
    for (const accepted of [true, false]) { context.resultHandler({ commandId: 'switch', accepted, code: 'fixture', message: 'Visible feedback' }); }
    assert.equal(context.state.viewContext.participantId, 1); assert.equal(chooser.value, '1'); assert.ok(feedback.length >= 2);
    chooser.value = '0'; chooser.onchange(); assert.equal(sent[1][1].targetParticipantId, 0);
});

test('Disabled, pending and player selectors cannot submit a perspective request', () =>
{
    const { context, nodes } = harness(); let sent = 0; let errors = 0;
    Object.assign(context, { viewParticipantId, observerViews, send() { sent++; }, error() { errors++; } });
    vm.runInNewContext(selectorSource, context); const chooser = nodes.get('observer-view');
    chooser.value = '2'; chooser.disabled = true; chooser.onchange();
    chooser.disabled = false;
    for (const role of ['pendingObserver', 'player'])
    {
        context.state.participants[2].role = role; chooser.value = '2'; chooser.onchange();
    }
    assert.equal(sent, 0); assert.equal(errors, 3);
});
