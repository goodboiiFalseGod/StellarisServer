import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { nearestProjectedTarget, zoomDetail, validateNavigationSnapshot, receivedRoute, routeGeometry, withinManualRadius, ZOOM_THRESHOLDS } from './m3-navigation.mjs';

function cameraAndRect()
{
    const rect = { left: 40, top: 25, width: 300, height: 200 };
    const camera = new THREE.PerspectiveCamera(60, rect.width / rect.height, 0.1, 100);
    camera.position.set(0, 0, 10);
    camera.lookAt(0, 0, 0);
    camera.updateMatrixWorld(true);
    return { camera, rect };
}

function clientPoint(position, camera, rect)
{
    const projected = new THREE.Vector3(position.x, position.y ?? 0, position.z).project(camera);
    return { x: rect.left + (projected.x + 1) * rect.width / 2, y: rect.top + (1 - projected.y) * rect.height / 2 };
}

function navigationState({ exits = [], fleets = [] } = {})
{
    const systems = [
        { id: 'A', galaxyPosition: { x: 0, y: 0, z: 0 }, radius: 150 },
        { id: 'B', galaxyPosition: { x: 100, y: 0, z: 0 }, radius: 150 },
        { id: 'C', galaxyPosition: { x: 100, y: 0, z: 100 }, radius: 150 }
    ];
    return {
        spatial: {
            systems,
            hyperlanes: [{ fromSystemId: 'A', toSystemId: 'B' }, { fromSystemId: 'B', toSystemId: 'C' }],
            ...(exits === undefined ? {} : { exits }),
            fleets
        }
    };
}

test('projected picking chooses the nearest overlap within CSS radius and resolves exact ties by ID', () =>
{
    const { camera, rect } = cameraAndRect();
    const center = { x: 0, y: 0, z: 0 };
    const right5 = { x: 0.2886751346, y: 0, z: 0 };
    const query = clientPoint(right5, camera, rect);
    const targets = [
        { id: 'center', position: center },
        { id: 'neighbor', position: right5 }
    ];
    assert.equal(nearestProjectedTarget(targets, camera, rect, query.x, query.y)?.id, 'neighbor');

    const radiusPoint = clientPoint(center, camera, rect);
    assert.equal(nearestProjectedTarget([{ id: 'edge', position: center }], camera, rect, radiusPoint.x + 18, radiusPoint.y)?.id, 'edge');
    assert.equal(nearestProjectedTarget([{ id: 'edge', position: center }], camera, rect, radiusPoint.x + 18.001, radiusPoint.y), null);
    const tied = [{ id: 'zeta', position: center }, { id: 'alpha', position: center }];
    assert.equal(nearestProjectedTarget(tied, camera, rect, radiusPoint.x, radiusPoint.y)?.id, 'alpha');
});

test('projected picking excludes behind-camera and clipped targets and follows a resized CSS rectangle', () =>
{
    const { camera, rect } = cameraAndRect();
    const visible = { id: 'visible', position: { x: 0, y: 0, z: 0 } };
    const behind = { id: 'behind', position: { x: 0, y: 0, z: 20 } };
    const clipped = { id: 'clipped', position: { x: 100, y: 0, z: 0 } };
    const center = clientPoint(visible.position, camera, rect);
    assert.equal(nearestProjectedTarget([behind, clipped, visible], camera, rect, center.x, center.y), visible);

    const resized = { left: 115, top: 80, width: 640, height: 360 };
    camera.aspect = resized.width / resized.height;
    camera.updateProjectionMatrix();
    camera.updateMatrixWorld(true);
    const newCenter = clientPoint(visible.position, camera, resized);
    assert.equal(nearestProjectedTarget([visible], camera, resized, newCenter.x, newCenter.y), visible);
    assert.equal(nearestProjectedTarget([visible], camera, resized, center.x, center.y), null);
});

test('zoom detail applies enter/exit hysteresis in both galaxy and system views', () =>
{
    for (const [kind, { near, far }] of Object.entries(ZOOM_THRESHOLDS))
    {
        assert.equal(zoomDetail(kind, near, 'far'), 'near');
        assert.equal(zoomDetail(kind, (near + far) / 2, 'near'), 'near');
        assert.equal(zoomDetail(kind, far, 'near'), 'far');
        assert.equal(zoomDetail(kind, (near + far) / 2, 'far'), 'far');
    }
});

test('snapshot validation rejects hidden targets, broken routes, disconnected exits and duplicate exits', () =>
{
    const fleet = movement => ({ location: { kind: 'system', systemId: 'A', position: { x: 0, z: 0 } }, movement });
    const validMovement = { target: { kind: 'system', systemId: 'B' }, routeSystemIds: ['A', 'B'] };
    const valid = navigationState({ exits: [{ systemId: 'A', destinationSystemId: 'B', position: { x: 10, z: 0 } }], fleets: [fleet(validMovement)] });
    assert.equal(validateNavigationSnapshot(valid), undefined);

    const hidden = navigationState({ fleets: [fleet({ target: { kind: 'system', systemId: 'hidden' }, routeSystemIds: ['A', 'hidden'] })] });
    assert.throws(() => validateNavigationSnapshot(hidden), /Target system is unavailable/);
    const broken = navigationState({ fleets: [fleet({ target: { kind: 'system', systemId: 'C' }, routeSystemIds: ['A', 'C'] })] });
    assert.throws(() => validateNavigationSnapshot(broken), /Invalid received system route/);
    const duplicateExit = navigationState({ exits: [
        { systemId: 'A', destinationSystemId: 'B', position: { x: 1, z: 0 } },
        { systemId: 'A', destinationSystemId: 'B', position: { x: 2, z: 0 } }
    ] });
    assert.throws(() => validateNavigationSnapshot(duplicateExit), /duplicate received exit/);
    const missingExitArray = navigationState({ fleets: [fleet(validMovement)] }); delete missingExitArray.spatial.exits;
    assert.equal(validateNavigationSnapshot(missingExitArray), undefined);
});

test('received routes follow local points, exit approach/charge, transit progress, and clear idle or absent movement', () =>
{
    const exit = { systemId: 'A', destinationSystemId: 'B', position: { x: 40, z: 5 } };
    const state = navigationState({ exits: [exit] });
    const local = {
        phase: 'movingLocal',
        location: { kind: 'system', systemId: 'A', position: { x: 3, z: 4 } },
        movement: { target: { kind: 'point', systemId: 'A', position: { x: 20, z: -8 } }, routeSystemIds: [], pendingAction: { kind: 'destination', systemId: 'C' } }
    };
    const localRoute = receivedRoute(state, local, { kind: 'system', systemId: 'A' });
    assert.deepEqual(localRoute.points.map(point => [point.x, point.y, point.z]), [[3, 0, 4], [20, 0, -8]]);
    for (const phase of ['approachingExit', 'charging'])
    {
        const fleet = { phase, location: local.location, movement: { target: { kind: 'system', systemId: 'B' }, routeSystemIds: ['A', 'B'] } };
        const route = receivedRoute(state, fleet, { kind: 'system', systemId: 'A' });
        assert.deepEqual(route.points.map(point => [point.x, point.y, point.z]), [[3, 0, 4], [40, 0, 5]]);
    }
    const withoutExits = navigationState(); delete withoutExits.spatial.exits;
    assert.equal(receivedRoute(withoutExits, { ...local, phase: 'approachingExit', movement: { target: { kind: 'system', systemId: 'B' }, routeSystemIds: ['A', 'B'] } }, { kind: 'system', systemId: 'A' }), null);

    const transit = {
        phase: 'inTransit',
        location: { kind: 'transit', fromSystemId: 'A', toSystemId: 'B', progress: 0.25 },
        movement: { target: { kind: 'system', systemId: 'C' }, routeSystemIds: ['A', 'B', 'C'] }
    };
    const galaxyRoute = receivedRoute(state, transit, { kind: 'galaxy' });
    assert.deepEqual(galaxyRoute.points.map(point => [point.x, point.y, point.z]), [[25, 0, 0], [100, 0, 0], [100, 0, 100]]);
    assert.equal(receivedRoute(state, { ...local, phase: 'idle' }, { kind: 'system', systemId: 'A' }), null);
    assert.equal(receivedRoute(state, { phase: 'movingLocal', location: local.location }, { kind: 'system', systemId: 'A' }), null);
    assert.equal(receivedRoute(state, { phase: 'idle', location: local.location, movement: { target: null, pendingAction: local.movement.pendingAction } }, { kind: 'galaxy' }), null);
});

test('route geometry stays finite and bounded and points its terminal arrow along travel direction', () =>
{
    const start = new THREE.Vector3(0, 0, 0), end = new THREE.Vector3(10000, 0, 0);
    const geometry = routeGeometry([start, end]);
    assert.ok(geometry.length > 0 && geometry.length <= 4620);
    assert.ok(geometry.every(Number.isFinite));
    assert.deepEqual(geometry.slice(-3), [end.x, end.y, end.z]);
    assert.ok(geometry.at(-12) < end.x);
    const reverse = routeGeometry([end, start]);
    assert.deepEqual(reverse.slice(-3), [start.x, start.y, start.z]);
    assert.ok(reverse.at(-12) > start.x);
    assert.deepEqual(routeGeometry([start, start]), []);
});

test('manual target radius includes 150, excludes outside and allows crossing the 140 inner ring', () =>
{
    assert.equal(withinManualRadius({ x: 150, z: 0 }, 150), true);
    assert.equal(withinManualRadius({ x: 150.001, z: 0 }, 150), false);
    assert.equal(withinManualRadius({ x: 145, z: 0 }, 150), true);
    assert.equal(withinManualRadius({ x: 140, z: 0 }, 150), true);
});
