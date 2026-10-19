import test from 'node:test';
import assert from 'node:assert/strict';
import { selectedObject, commandFleet, selectionCard, movementRows, fleetOrderRows, outlinerGroups, resourceItems, identityPresentation, cameraOptions, labelInsets, viewParticipantId, observerViews, decimalPrecisionWithin } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

test('gateway frequency decimal precision accepts ordinary five-place values without binary rounding errors', () =>
{
    assert.equal(decimalPrecisionWithin(0.29, 5), true);
    assert.equal(decimalPrecisionWithin(0.07, 5), true);
    assert.equal(decimalPrecisionWithin(1.23456, 5), true);
    assert.equal(decimalPrecisionWithin(0.000001, 5), false);
});

function snapshot()
{
    return {
        you: 1, phase: 'running', paused: true, viewContext: { mode: 'player', participantId: 1 },
        participants: [{ id: 1, name: 'Player', role: 'player', isHost: true }, { id: 2, name: 'Other', role: 'player' }],
        planets: [{ ownerId: 2, balances: { energy: 999 } }, { ownerId: 1, balances: { energy: 40 }, report: { net: { energy: 2 } } }],
        spatial: {
            systems: [{ id: 's1', name: 'Visible' }], bodies: [{ id: 'b1', systemId: 's1', name: 'Home', kind: 'planet', ownerParticipantId: 1 }],
            colonies: [{ id: 'c1', bodyId: 'b1', ownerParticipantId: 1 }],
            fleets: [{ id: 'f1', name: 'Own', ownerParticipantId: 1, shipCount: 3, phase: 'idle', location: { kind: 'system', systemId: 's1' }, movement: { routeSystemIds: ['s1', 'secret-system'], target: { systemId: 'secret-system' } } }, { id: 'f2', name: 'Foreign', ownerParticipantId: 2, shipCount: 2, phase: 'idle', location: { kind: 'system', systemId: 's1' }, secretDebugValue: 'NEVER_RENDER' }]
        }
    };
}

test('HUD only displays the current player economy and never falls back to a foreign planet', () =>
{
    const state = snapshot();
    assert.deepEqual(resourceItems(state), [{ id: 'energy', value: '40', net: '+2' }]);
    state.planets = state.planets.filter(item => item.ownerId !== state.you);
    assert.deepEqual(resourceItems(state), []);
    state.participants[0].role = 'observer'; state.viewContext = { mode: 'general', participantId: null };
    assert.deepEqual(resourceItems(state), []);
});

test('Foreign selection cannot become a command fleet; observer commands remain unavailable', () =>
{
    const state = snapshot(); const before = structuredClone(state);
    assert.equal(commandFleet(state, 'f2'), null);
    assert.equal(commandFleet(state, 'f1').id, 'f1');
    assert.equal(selectedObject(state, { kind: 'fleet', id: 'f2' }).id, 'f2');
    assert.doesNotMatch(JSON.stringify(selectionCard(state, { kind: 'fleet', id: 'f2' })), /NEVER_RENDER|secretDebugValue/);
    assert.deepEqual(state, before);
    state.participants[0].role = 'observer';
    assert.equal(commandFleet(state, 'f1'), null);
});

test('Missing objects and hidden system identifiers do not persist in contextual presentation', () =>
{
    const state = snapshot();
    assert.equal(selectionCard(state, { kind: 'fleet', id: 'removed' }), null);
    assert.equal(selectedObject(state, { kind: 'unsupported', id: 'f1' }), null);
    const movement = JSON.stringify(movementRows(state, state.spatial.fleets[0]));
    assert.doesNotMatch(movement, /secret-system/);
    assert.match(movement, /Недоступная система/);
});

test('Detailed Trappist body card localizes the persistent modifier', () =>
{
    const state = snapshot();
    state.spatial.bodies[0] = { id: 'b1', systemId: 's1', name: 'Trappist b', kind: 'planet', worldClassId: 'pc_continental',
        modifiers: [{ modifierId: 'terraforming_candidate', days: -1, nameKey: 'terraforming_candidate' }] };
    const card = selectionCard(state, { kind: 'body', id: 'b1' }, (key, fallback) => key === 'terraforming_candidate' ? 'Кандидат на терраформирование' : fallback);
    assert.ok(card.rows.some(([label, value]) => label === 'Модификатор' && value === 'Кандидат на терраформирование'));
});

test('ruined gateway has a non-command selection card and no owner/travel actions', () =>
{
    const state=snapshot();
    state.spatial.gateways=[{id:'gateway/0001',systemId:'s1',visualId:'object.gateway_ruined',name:'Заброшенные врата',status:'Неактивны'}];
    const selection={kind:'gateway',id:'gateway/0001'};
    assert.equal(selectedObject(state,selection).status,'Неактивны');
    const card=selectionCard(state,selection);
    assert.equal(card.title,'Заброшенные врата');
    assert.equal(card.subtitle,'Мегаструктура');
    assert.ok(card.rows.some(([label,value])=>label==='Состояние'&&value==='Неактивны'));
    assert.ok(card.rows.some(([label,value])=>label==='Система'&&value==='Visible'));
    assert.equal(card.rows.some(([label])=>['Владелец','Маршрут','Прибытие'].includes(label)),false);
});

test('Fleet pending intents remain separate from received routes and phase time never becomes ETA', () =>
{
    const state = snapshot(); const fleet = state.spatial.fleets[0];
    fleet.phase = 'inTransit'; fleet.location = { kind: 'transit', fromSystemId: 's1', toSystemId: 'secret-system' };
    fleet.movement.phaseEndsDay = 123; fleet.movement.pendingAction = { systemId: 's1' };
    const pending = [{ fleetId: 'f1', type: 'moveFleetToSystem', targetSystemId: 'UNCONFIRMED_TARGET' }, { fleetId: 'f2', type: 'stopFleet' }, { fleetId: 'f1', type: 'configure' }];
    const before = structuredClone(state);
    const rows = fleetOrderRows(state, 'f1', pending);
    assert.match(JSON.stringify(rows), /1 · К системе/);
    assert.match(JSON.stringify(rows), /Прибытие.*Недоступно/);
    assert.match(JSON.stringify(rows), /Гиперпереход/);
    assert.match(JSON.stringify(rows), /После прибытия/);
    assert.doesNotMatch(JSON.stringify(rows), /UNCONFIRMED_TARGET|123|Стоп|secret-system/);
    assert.equal(rows.find(([key]) => key === 'Корабли')[1], '3');
    assert.equal(fleetOrderRows(state, 'f1', [])[rows.length - 1][1], 'Нет');
    assert.deepEqual(fleetOrderRows(state, 'f2', pending), []);
    assert.deepEqual(fleetOrderRows(state, 'removed', pending), []);
    assert.deepEqual(state, before);
    state.participants[0].role = 'observer'; state.viewContext.participantId = 2;
    assert.deepEqual(fleetOrderRows(state, 'f1', pending), []);
    assert.match(JSON.stringify(selectionCard(state, { kind: 'fleet', id: 'f1' })), /Прибытие.*Недоступно/);
});

test('Personal outliner includes owned assets only and never treats public systems as personal', () =>
{
    const state = snapshot();
    state.spatial.bodies.push({ id: 'station', ownerParticipantId: 1, kind: 'station', systemId: 's1', name: 'Own station' });
    assert.deepEqual(outlinerGroups(state).map(group => [group.title, group.rows.length]), [['Колонии', 1], ['Флоты', 1], ['Станции', 1]]);
    assert.doesNotMatch(JSON.stringify(outlinerGroups(state)), /Foreign|f2/);
    state.you = 2; state.viewContext.participantId = 2;
    assert.deepEqual(outlinerGroups(state).flatMap(group => group.rows.map(row => row.id)), ['f2']);
    state.you = 1; state.viewContext.participantId = 1;
    state.spatial.colonies = []; state.spatial.fleets = [];
    assert.deepEqual(outlinerGroups(state).map(group => group.title), ['Станции', 'Планеты']);
    assert.deepEqual(outlinerGroups(null), []);
});

test('Identity and pause label reflect actual role and current view', () =>
{
    const state = snapshot();
    assert.deepEqual(identityPresentation(state, 's1', false), { view: 'Visible', name: 'Player', detail: 'Игрок · хост', paused: true });
    state.participants[0].role = 'observer'; state.paused = false;
    assert.equal(identityPresentation(state, null, false).detail, 'Наблюдатель · Общий обзор · только просмотр · хост');
    assert.equal(identityPresentation(state, null, false).paused, false);
    assert.equal(identityPresentation(state, null, true).view, 'Тест · 200 корветов');
});

test('Технический класс не выдаётся за имя; собственные спутники остаются в обзоре', () =>
{
    const state = snapshot(); state.spatial.bodies[0].name = 'pc_continental';
    state.spatial.bodies[0].worldClassId = 'pc_continental';
    state.spatial.bodies.push({ id: 'm1', name: 'pc_barren', kind: 'moon', ownerParticipantId: 1, systemId: 's1' });
    const card = selectionCard(state, { kind: 'colony', id: 'c1' }, (_key, fallback) => fallback);
    assert.equal(card.title, 'Планета 1');
    assert.doesNotMatch(card.title, /pc_continental/);
    assert.match(card.diagnostic, /b1.*pc_continental/);
    assert.equal(outlinerGroups(state).find(group => group.title === 'Спутники').rows[0].name, 'Спутник 1');
    state.spatial.bodies.push({ ...state.spatial.bodies[0], id: 'b2' });
    const before = selectionCard(state, { kind: 'colony', id: 'c1' }).title;
    state.spatial.bodies.reverse();
    assert.equal(selectionCard(state, { kind: 'colony', id: 'c1' }).title, before);
});

test('Observer player POV renders target assets and resources while retaining actual identity and read-only commands', () =>
{
    const state = snapshot();
    state.participants.push({ id: 3, name: 'Watcher', role: 'observer' }); state.you = 3;
    state.participants[1].connected = false;
    assert.deepEqual(observerViews(state), [[0, 'Общий обзор'], [1, 'Player'], [2, 'Other · offline']]);
    for (const [owner, fleet, energy] of [[1, 'f1', '40'], [2, 'f2', '999']])
    {
        state.viewContext = { mode: 'player', participantId: owner };
        assert.equal(viewParticipantId(state), owner);
        assert.deepEqual(outlinerGroups(state).find(group => group.title === 'Флоты').rows.map(row => row.id), [fleet]);
        assert.equal(resourceItems(state)[0].value, energy);
        assert.equal(commandFleet(state, fleet), null);
        const identity = identityPresentation(state, null, false);
        assert.equal(identity.name, 'Watcher'); assert.match(identity.detail, /Вид игрока: .* · только просмотр/);
    }
    state.viewContext = { mode: 'general', participantId: null };
    assert.deepEqual(outlinerGroups(state), []); assert.deepEqual(resourceItems(state), []);
});

test('Missing, pending and unauthorized player contexts fail closed without a foreign chooser or HUD', () =>
{
    const state = snapshot();
    assert.deepEqual(observerViews(state), []);
    for (const context of [undefined, { mode: 'pending', participantId: null }, { mode: 'player', participantId: 2 }, { mode: 'player', participantId: 99 }])
    {
        state.viewContext = context;
        assert.equal(viewParticipantId(state), null); assert.deepEqual(resourceItems(state), []); assert.deepEqual(outlinerGroups(state), []);
    }
    state.participants[0].role = 'pendingObserver'; state.viewContext = { mode: 'player', participantId: 2 };
    assert.deepEqual(observerViews(state), []); assert.equal(viewParticipantId(state), null);
});

test('Camera choices match the active view and never expose gameplay presets in stress mode', () =>
{
    assert.deepEqual(cameraOptions(null, false).map(([id]) => id), ['galaxy-far', 'galaxy-near']);
    assert.deepEqual(cameraOptions('s1', false).map(([id]) => id), ['system-overview', 'system-close']);
    assert.deepEqual(cameraOptions(null, true), []);
});

test('Measured HUD and plaque exclusion keeps a usable label center on short screens', () =>
{
    assert.deepEqual(labelInsets(720, 36, 610), { top: 44, right: 16, bottom: 122, left: 16 });
    const short = labelInsets(100, 66, -30);
    assert.equal(short.top, 20); assert.equal(short.bottom, 35);
    assert.ok(short.top + short.bottom < 100);
});
