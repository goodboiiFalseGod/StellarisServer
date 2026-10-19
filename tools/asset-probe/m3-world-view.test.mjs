import test from 'node:test';
import assert from 'node:assert/strict';
import { VISUAL_IDS, CORVETTE_OFFSETS, profileTexturePlan, validateSnapshot, reconcileSelection, ownFleet, orderGate, collectViewObjects, interpolateFleet } from './m3-world-view.mjs';
import { requireConvertedBudget } from './m3-memory.mjs';

function fixture()
{
    return {
        protocol: 2, sessionId: 'session', sequence: 1, phase: 'running', paused: false, you: 1,
        participants: [{ id: 1, role: 'player' }, { id: 2, role: 'player' }, { id: 3, role: 'observer' }],
        spatial: {
            systems: [{ id: 'S1', galaxyPosition: { x: 0, y: 0, z: 0 }, radius: 150, starBodyId: 'B1' }, { id: 'S2', galaxyPosition: { x: 100, y: 10, z: 50 }, radius: 150 }],
            hyperlanes: [{ id: 'L1', fromSystemId: 'S1', toSystemId: 'S2' }],
            bodies: [{ id: 'B1', systemId: 'S1', kind: 'star', localPosition: { x: 0, z: 0 }, visualId: 'star.g', scale: 1.5 }, { id: 'B2', systemId: 'S1', kind: 'planet', localPosition: { x: 20, z: 30 }, visualId: 'planet.continental', scale: 2 }],
            colonies: [{ id: 'C1', bodyId: 'B2', ownerParticipantId: 1 }],
            fleets: [{ id: 'F1', ownerParticipantId: 1, visualId: 'ship.corvette.mammalian', shipCount: 3, phase: 'movingLocal', location: { kind: 'system', systemId: 'S1', position: { x: 10, z: 20 } }, movement: { orderVersion: 4 } }, { id: 'F2', ownerParticipantId: 2, visualId: 'ship.corvette.mammalian', shipCount: 3, phase: 'inTransit', location: { kind: 'transit', fromSystemId: 'S1', toSystemId: 'S2', progress: 0.25 } }]
        }
    };
}

test('точные unions, foreign без movement и согласованные offsets', () =>
{
    const state = fixture();
    assert.equal(validateSnapshot(state), state);
    assert.deepEqual(CORVETTE_OFFSETS, [[-6, -4], [0, 0], [6, -4]]);
    for (const mutate of [s => { s.protocol = 1; }, s => { s.spatial.fleets[0].location.kind = 'local'; }, s => { s.spatial.fleets[0].phase = 'transit'; }, s => { s.spatial.bodies[0].visualId = 'fallback'; }, s => { s.spatial.fleets[0].location.position.x = NaN; }, s => { s.spatial.fleets[1].location.progress = 1.01; }, s => { s.spatial.systems.push(s.spatial.systems[0]); }])
    {
        const broken = fixture(); mutate(broken); assert.throws(() => validateSnapshot(broken));
    }
});

test('optional gateway details validate, render as system object, and reconcile selection', () =>
{
    const state=fixture();
    state.spatial.gateways=[{id:'gateway/0001',systemId:'S1',localPosition:{x:115,z:0},visualId:'object.gateway_ruined',name:'Заброшенные врата',status:'Неактивны'}];
    assert.equal(validateSnapshot(state),state);
    const objects=collectViewObjects(state,{kind:'system',systemId:'S1'});
    assert.equal(objects[0].kind,'gateway');
    assert.deepEqual(objects[0].position,{x:115,y:0,z:0});
    assert.deepEqual(reconcileSelection(state,{kind:'gateway',id:'gateway/0001'},null).selection,{kind:'gateway',id:'gateway/0001'});
    const legacy=fixture();
    assert.equal(validateSnapshot(legacy),legacy);
    assert.throws(()=>{state.spatial.gateways[0].systemId='hidden';validateSnapshot(state);});
});

test('wormhole view exposes only visible endpoint objects and never its pair relation', () =>
{
    const state=fixture();
    state.spatial.wormholes=[{id:'wormhole/0001',systemId:'S1',localPosition:{x:45,z:-10},visualId:'object.natural_wormhole',name:'Червоточина',status:'Не исследована'}];
    assert.equal(validateSnapshot(state),state);
    const system=collectViewObjects(state,{kind:'system',systemId:'S1'});
    assert.deepEqual(system[0],{kind:'wormhole',id:'wormhole/0001',position:{x:45,y:0,z:-10},visualId:'object.natural_wormhole',scale:1});
    assert.equal(collectViewObjects(state,{kind:'galaxy'}).some(item=>item.kind==='wormhole'),false);
    assert.deepEqual(reconcileSelection(state,{kind:'wormhole',id:'wormhole/0001'},null).selection,{kind:'wormhole',id:'wormhole/0001'});
    for(const key of ['pairId','partnerId'])
    {
        const leaked=fixture();leaked.spatial.wormholes=[{...state.spatial.wormholes[0],[key]:'secret'}];
        assert.throws(()=>validateSnapshot(leaked));
    }
});

test('отсутствующие ссылки не восстанавливаются по ID', () =>
{
    for (const mutate of [s => { s.spatial.hyperlanes[0].toSystemId = 'missing'; }, s => { s.spatial.bodies[0].systemId = 'missing'; }, s => { s.spatial.colonies[0].bodyId = 'missing'; }, s => { s.spatial.fleets[0].location.systemId = 'missing'; }, s => { s.spatial.fleets[1].location.fromSystemId = 'missing'; }, s => { s.spatial.systems[0].starBodyId = 'missing'; }])
    {
        const broken = fixture(); mutate(broken); assert.throws(() => validateSnapshot(broken));
    }
});

test('galaxy только маркеры; transit из server endpoints/progress, system без transit', () =>
{
    const state = fixture();
    const galaxy = collectViewObjects(state, { kind: 'galaxy' });
    assert.equal(galaxy.length, 4);
    assert.deepEqual(galaxy.find(item => item.id === 'F2').position, { x: 25, y: 2.5, z: 12.5 });
    assert.equal(galaxy.some(item => item.visualId), false);
    const system = collectViewObjects(state, { kind: 'system', systemId: 'S1' });
    assert.deepEqual(system.map(item => item.id), ['B1', 'B2', 'F1']);
    assert.equal(system[1].scale, 2);
    assert.equal(collectViewObjects(state, { kind: 'system', systemId: 'S2' }).length, 0);
    assert.throws(() => collectViewObjects(state, { kind: 'system', systemId: 'hidden' }));
    assert.deepEqual(collectViewObjects(state, { kind: 'stress' }), []);
});

test('выбор и командный флот независимы; удаление/observer/foreign сбрасывает командный', () =>
{
    const state = fixture();
    assert.deepEqual(reconcileSelection(state, { kind: 'system', id: 'S2' }, 'F1'), { selection: { kind: 'system', id: 'S2' }, commandFleetId: 'F1' });
    assert.equal(reconcileSelection(state, { kind: 'colony', id: 'C1' }, 'F2').commandFleetId, null);
    assert.equal(reconcileSelection(state, { kind: 'fleet', id: 'hidden' }, 'F1').selection, null);
    assert.equal(ownFleet(state, 'F1').id, 'F1');
    state.you = 3;
    assert.equal(ownFleet(state, 'F1'), null);
    assert.equal(reconcileSelection(null, null, null).selection, null);
});

test('гейтинг приказов: свежесть/profile/running/observer/stress/transit/не та система', () =>
{
    const state = fixture();
    const options = { fresh: true, profileReady: true, contextLost: false, viewKind: 'galaxy' };
    assert.equal(orderGate(state, 'F1', options), null);
    for (const patch of [{ fresh: false }, { profileReady: false }, { contextLost: true }, { viewKind: 'stress' }, { pointOrder: true, systemId: 'S2' }]) { assert.equal(typeof orderGate(state, 'F1', { ...options, ...patch }), 'string'); }
    assert.equal(orderGate(state, 'F1', { ...options, pointOrder: true, systemId: 'S1' }), null);
    assert.equal(typeof orderGate(state, 'F2', options), 'string');
    state.spatial.fleets[0].location = { kind: 'transit', fromSystemId: 'S1', toSystemId: 'S2', progress: 0.5 };
    state.spatial.fleets[0].phase = 'inTransit';
    assert.equal(orderGate(state, 'F1', options), null);
    assert.equal(typeof orderGate(state, 'F1', { ...options, pointOrder: true, systemId: 'S1' }), 'string');
    state.phase = 'lobby'; assert.equal(typeof orderGate(state, 'F1', options), 'string');
    state.you = 3; assert.equal(typeof orderGate(state, 'F1', options), 'string');
});

test('100ms interpolation, clamp без экстраполяции', () =>
{
    const old = fixture();
    const next = fixture(); next.sequence = 2; next.spatial.fleets[0].location.position = { x: 20, z: 40 };
    const a = { state: old, receivedAt: 1000 }, b = { state: next, receivedAt: 1200 };
    assert.deepEqual(interpolateFleet(a, b, 'F1', 900), { x: 10, z: 20 });
    assert.deepEqual(interpolateFleet(a, b, 'F1', 1200), { x: 15, z: 30 });
    assert.deepEqual(interpolateFleet(a, b, 'F1', 5000), { x: 20, z: 40 });
    assert.equal(interpolateFleet(a, b, 'F2', 1200), null);
    assert.equal(interpolateFleet(a, b, 'missing', 1200), null);
});

test('пауза/step/phase/system/version/session/gap/reconnect сбрасывают interpolation', () =>
{
    const mutations = [s => { s.paused = true; }, s => { s.phase = 'finished'; }, s => { s.sessionId = 'new'; }, s => { s.sequence = 3; }, s => { s.spatial.fleets[0].phase = 'charging'; }, s => { s.spatial.fleets[0].location.systemId = 'S2'; }, s => { s.spatial.fleets[0].movement.orderVersion++; }, s => { delete s.spatial.fleets[0].movement; }];
    for (const mutate of mutations)
    {
        const next = fixture(); next.sequence = 2; next.spatial.fleets[0].location.position.x = 50; mutate(next);
        assert.equal(interpolateFleet({ state: fixture(), receivedAt: 1000 }, { state: next, receivedAt: 1200 }, 'F1', 1200).x, 50);
    }
    const next = fixture(); next.spatial.fleets[0].location.position.x = 50;
    assert.equal(interpolateFleet(null, { state: next, receivedAt: 1200 }, 'F1', 1200).x, 50);
});

test('все четыре templates, глобальный texture(path,mode), old+new memory до alloc', () =>
{
    const image = { width: 2, height: 2, rgba: new Uint8Array(16) };
    const samples = new Map(VISUAL_IDS.map(id => [id, { meshes: [{ material: { shader: 'PdxMeshShip', diff: 'd', n: 'shared', spec: 'shared' } }], images: new Map([['shared', image], ['d', image]]) }]));
    const plan = profileTexturePlan({ samples });
    assert.equal(plan.convertedBytes, 32);
    assert.equal(plan.images.size, 2);
    assert.doesNotThrow(() => requireConvertedBudget(plan.convertedBytes, 32, 64));
    assert.throws(() => requireConvertedBudget(plan.convertedBytes, 33, 64));
    samples.get(VISUAL_IDS[0]).images.set('shared', { ...image });
    assert.throws(() => profileTexturePlan({ samples }), /неоднозначная/);
    samples.delete(VISUAL_IDS[0]); assert.throws(() => profileTexturePlan({ samples }), /неполный/);
});
