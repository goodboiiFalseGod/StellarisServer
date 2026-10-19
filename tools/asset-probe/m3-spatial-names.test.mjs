import assert from 'node:assert/strict';
import { resolveSpatialNames, bodyDisplayName } from './m3-world-view.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';

const state = { sessionId: 'names', sequence: 4, planets: [{ id: 7, name: 'Планета Host' }, { id: 8, name: 'Other' }], spatial: { colonies: [{ economyPlanetId: 7, bodyId: 'p' }], systems: [{ id: 's', name: 'Alpha', nameKey: 'Alpha', nameSuffix: '' }], bodies: [
    { id: 'p', name: 'Alpha II', nameKey: 'Alpha', nameSuffix: ' II' },
    { id: 'm', name: 'Alpha II b', nameKey: 'Alpha', nameSuffix: ' II b' },
    { id: 'literal', name: 'Literal_override', nameKey: 'Literal_override', nameSuffix: '' },
    { id: 'station', name: 'Станция' }
] } };
const resolve = (key, fallback) => key === 'Alpha' ? 'Альфа' : fallback;
const renderer = Object.create(M3WorldRenderer.prototype);
renderer.disposed = false; renderer.current = { state: structuredClone(state), receivedAt: 123 };
renderer.previous = { retained: true }; renderer.view = { kind: 'system', systemId: 's' };
renderer.buildView = (_resources, snapshot, view) => ({ names: snapshot.spatial.bodies.map(body => body.name), view });
renderer.replaceView = view => { renderer.viewResources = view; };
renderer.resetCamera = () => assert.fail('Локализация не должна сбрасывать камеру');
resolveSpatialNames(state, resolve); resolveSpatialNames(state, resolve);
assert.equal(state.spatial.systems[0].name, 'Альфа');
assert.deepEqual(state.spatial.bodies.map(b => b.name), ['Альфа II', 'Альфа II b', 'Literal_override', 'Станция']);
assert.equal(bodyDisplayName(state.spatial.bodies[0]), 'Альфа II');
assert.equal(state.planets[0].name, 'Альфа II'); assert.equal(state.planets[0].id, 7); assert.equal(state.planets[1].name, 'Other');
assert.equal(renderer.refreshSpatialNames(state), true);
assert.equal(renderer.viewResources.names[0], 'Альфа II');
assert.equal(renderer.current.state.sequence, 4); assert.equal(renderer.current.receivedAt, 123);
assert.equal(renderer.previous.retained, true); assert.equal(renderer.view.systemId, 's');
state.spatial.bodies[0].name = 'External mutation';
assert.equal(renderer.current.state.spatial.bodies[0].name, 'Альфа II');
assert.equal(renderer.refreshSpatialNames({ ...state, sequence: 3 }), false);
resolveSpatialNames(state, (_key, fallback) => fallback);
assert.equal(state.spatial.bodies[1].name, 'Alpha II b');
assert.equal(state.spatial.bodies[0].id, 'p');
console.log('PASS spatial naming localization/reload/literal/targets');
