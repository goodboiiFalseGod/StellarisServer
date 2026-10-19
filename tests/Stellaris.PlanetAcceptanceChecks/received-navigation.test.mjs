import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { receivedRoute, validateNavigationSnapshot } from '../../tools/asset-probe/m3-navigation.mjs';
import { outlinerGroups, commandFleet } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

// Actual public-session snapshots with synthetic economy/assets; no parallel route simulation.
const capture = spawnSync('dotnet', [fileURLToPath(new URL('./bin/Release/net8.0/Stellaris.PlanetAcceptanceChecks.dll', import.meta.url)), '--navigation-capture'], { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 });
assert.equal(capture.status, 0, capture.stderr || capture.error?.message || capture.stdout);
const evidence = JSON.parse(capture.stdout);
const fleet = state => state.spatial.fleets.find(item => item.id === evidence.fleetId);
const points = route => route?.points.map(point => point.toArray()) ?? null;
const named = label => { const value = evidence.captures.find(item => item.label === label); assert.ok(value, `Missing actual capture: ${label}`); return value; };

test('Independent received exits and movement validate through the browser adapter for both players and observer POV', () =>
{
    assert.ok(evidence.captures.length >= 9);
    for (const { owner, foreign, observer } of evidence.captures)
    {
        for (const state of [owner, foreign, observer])
        {
            assert.ok(state.spatial.exits.length > 0); validateNavigationSnapshot(state);
            for (const exit of state.spatial.exits)
            {
                assert.ok(state.spatial.systems.some(system => system.id === exit.destinationSystemId));
                assert.ok(Math.abs(Math.hypot(exit.position.x, exit.position.z) - 140) < 1e-6, 'Accepted M3 fixture exit ring changed');
            }
        }
        assert.equal(observer.you, 3); assert.equal(observer.viewContext.participantId, owner.you);
        assert.deepEqual(fleet(observer).movement, fleet(owner).movement); assert.equal(fleet(foreign).movement, undefined);
        assert.equal(commandFleet(observer, evidence.fleetId), null);
        for (const view of [{ kind: 'galaxy' }, { kind: 'system', systemId: evidence.startSystemId }])
        {
            assert.deepEqual(points(receivedRoute(observer, fleet(observer), view)), points(receivedRoute(owner, fleet(owner), view)));
            assert.equal(receivedRoute(foreign, fleet(foreign), view), null);
        }
    }
    assert.deepEqual(outlinerGroups(evidence.general), []);
});

test('Independent accepted local replacement and stop replace geometry, not just acknowledgements', () =>
{
    const first = named('local').owner, replacement = named('localReplacement').owner, stopped = named('localStopped').owner;
    const view = { kind: 'system', systemId: evidence.startSystemId };
    const a = points(receivedRoute(first, fleet(first), view)), b = points(receivedRoute(replacement, fleet(replacement), view));
    assert.ok(a && b); assert.notDeepEqual(a, b);
    for (const state of [first, replacement])
    {
        const target = fleet(state).movement.target.position;
        assert.deepEqual(points(receivedRoute(state, fleet(state), view)).at(-1), [target.x, 0, target.z]);
    }
    assert.ok(fleet(replacement).movement.orderVersion > fleet(first).movement.orderVersion);
    assert.equal(fleet(stopped).phase, 'idle'); assert.equal(receivedRoute(stopped, fleet(stopped), view), null);
});

test('Independent system replacement and charge use the received next-hop exit rather than old destination geometry', () =>
{
    const first = named('system').owner, replacement = named('systemReplacement').owner, charging = named('charging').owner;
    assert.notEqual(fleet(first).movement.target.systemId, fleet(replacement).movement.target.systemId);
    assert.ok(fleet(replacement).movement.orderVersion > fleet(first).movement.orderVersion);
    assert.notDeepEqual(points(receivedRoute(first, fleet(first), { kind: 'galaxy' })), points(receivedRoute(replacement, fleet(replacement), { kind: 'galaxy' })));
    for (const state of [first, replacement, charging])
    {
        const current = fleet(state), next = current.movement.routeSystemIds[1];
        const exit = state.spatial.exits.find(item => item.systemId === current.location.systemId && item.destinationSystemId === next);
        assert.ok(exit);
        assert.deepEqual(points(receivedRoute(state, current, { kind: 'system', systemId: current.location.systemId })).at(-1), [exit.position.x, 0, exit.position.z]);
    }
});

test('Independent actual transit has no local line and arrival clears accepted geometry', () =>
{
    const transit = named('midTransit').owner, moving = fleet(transit);
    assert.equal(moving.location.kind, 'transit');
    assert.ok(moving.location.progress > 0 && moving.location.progress < 1);
    assert.equal(receivedRoute(transit, moving, { kind: 'system', systemId: moving.location.fromSystemId }), null);
    const route = points(receivedRoute(transit, moving, { kind: 'galaxy' })); assert.ok(route);
    const from = transit.spatial.systems.find(item => item.id === moving.location.fromSystemId).galaxyPosition;
    const to = transit.spatial.systems.find(item => item.id === moving.location.toSystemId).galaxyPosition;
    assert.deepEqual(route[0], ['x', 'y', 'z'].map(axis => from[axis] + (to[axis] - from[axis]) * moving.location.progress));
    const arrived = evidence.captures.at(-1).owner, current = fleet(arrived);
    assert.equal(current.phase, 'idle'); assert.equal(current.location.systemId, fleet(named('systemReplacement').owner).movement.target.systemId);
    assert.equal(receivedRoute(arrived, current, { kind: 'galaxy' }), null);
    assert.equal(receivedRoute(arrived, current, { kind: 'system', systemId: current.location.systemId }), null);
});
