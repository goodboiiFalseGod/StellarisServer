import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { CAMERA_PRESET_IDS, createCameraPreset, createSystemOverviewPreset, orbitAboutPivot, viewCenter } from './m3-camera.mjs';

function state()
{
    return { spatial: { systems: [{ galaxyPosition: { x: 100, y: 0, z: 200 } }, { galaxyPosition: { x: 300, y: 0, z: 500 } }, { galaxyPosition: { x: 110, y: 0, z: 220 } }], fleets: [{ galaxyPosition: { x: 9000, y: 0, z: 9000 } }] } };
}

test('Galaxy center uses authorized system extent; system and empty views retain origin', () =>
{
    assert.deepEqual(viewCenter(state(), { kind: 'galaxy' }).toArray(), [200, 0, 350]);
    assert.deepEqual(viewCenter(state(), { kind: 'system', systemId: 'visible' }).toArray(), [0, 0, 0]);
    assert.deepEqual(viewCenter({ spatial: { systems: [] } }, { kind: 'galaxy' }).toArray(), [0, 0, 0]);
    assert.deepEqual(viewCenter(null, { kind: 'galaxy' }).toArray(), [0, 0, 0]);
    assert.throws(() => viewCenter({ spatial: { systems: [{ galaxyPosition: { x: NaN, y: 0, z: 0 } }] } }, { kind: 'galaxy' }));
});

test('After pan and selection clearing, orbit retains radii about the offset galaxy center', () =>
{
    const center = viewCenter(state(), { kind: 'galaxy' });
    const camera = new THREE.PerspectiveCamera(35, 16 / 9, 0.1, 10000);
    camera.position.set(700, 400, 900);
    const target = new THREE.Vector3(430, 0, 510);
    const originalPosition = camera.position.clone();
    const originalTarget = target.clone();
    orbitAboutPivot(camera, target, new THREE.Vector3(100, 0, 200), 0, 0);
    assert.ok(camera.position.distanceTo(originalPosition) < 1e-10);
    assert.ok(target.distanceTo(originalTarget) < 1e-10);
    const positionRadius = camera.position.distanceTo(center);
    const targetRadius = target.distanceTo(center);
    const separation = camera.position.distanceTo(target);
    orbitAboutPivot(camera, target, center, 75, 20);
    assert.ok(Math.abs(camera.position.distanceTo(center) - positionRadius) < 1e-9);
    assert.ok(Math.abs(target.distanceTo(center) - targetRadius) < 1e-9);
    assert.ok(Math.abs(camera.position.distanceTo(target) - separation) < 1e-9);
});

test('Explicit presets fit translated bounds in portrait and landscape perspective frusta', () =>
{
    const bounds = new THREE.Box3(new THREE.Vector3(100, -20, 200), new THREE.Vector3(300, 60, 500));
    const original = bounds.clone();
    const direction = new THREE.Vector3(1, 1.2, 1);
    for (const id of CAMERA_PRESET_IDS)
    {
        for (const aspect of [16 / 9, 9 / 16])
        {
            const result = createCameraPreset(id, { bounds, aspect, fov: 35, direction, padding: 1.1, distanceScale: 1 });
            assert.deepEqual(result.target.toArray(), [200, 20, 350]);
            const camera = new THREE.PerspectiveCamera(35, aspect, 0.01, result.distance * 10);
            camera.position.copy(result.position);
            camera.lookAt(result.target);
            camera.updateMatrixWorld();
            for (const x of [bounds.min.x, bounds.max.x])
            {
                for (const y of [bounds.min.y, bounds.max.y])
                {
                    for (const z of [bounds.min.z, bounds.max.z])
                    {
                        const projected = new THREE.Vector3(x, y, z).project(camera);
                        assert.ok(Math.abs(projected.x) <= 1 && Math.abs(projected.y) <= 1 && projected.z > -1 && projected.z < 1);
                    }
                }
            }
        }
    }
    assert.ok(bounds.equals(original));
    assert.deepEqual(direction.toArray(), [1, 1.2, 1]);
});

test('Framing responds to FOV/aspect and explicit calibration; invalid input fails', () =>
{
    const options = { bounds: new THREE.Box3(new THREE.Vector3(-10, -5, -3), new THREE.Vector3(10, 5, 3)), aspect: 1, fov: 35, direction: new THREE.Vector3(0, 0, 1), padding: 1, distanceScale: 1 };
    const baseline = createCameraPreset('galaxy-far', options);
    assert.ok(createCameraPreset('galaxy-near', { ...options, fov: 60 }).distance < baseline.distance);
    assert.ok(createCameraPreset('system-overview', { ...options, aspect: 0.5 }).distance > baseline.distance);
    assert.equal(createCameraPreset('system-close', { ...options, distanceScale: 1.2 }).distance, baseline.distance * 1.2);
    for (const change of [{ aspect: 0 }, { fov: 180 }, { padding: NaN }, { distanceScale: 0.001 }, { bounds: new THREE.Box3() }, { direction: new THREE.Vector3(0, 1, 0) }])
    {
        assert.throws(() => createCameraPreset('galaxy-far', { ...options, ...change }), /M3:/);
    }
    assert.throws(() => createCameraPreset('unknown', options), /M3:/);
});

test('System overview helper shares framing calibration and rejects missing radius/aspect', () =>
{
    const radius = 150, aspect = 1280 / 720;
    const preset = createSystemOverviewPreset(radius, aspect);
    const bounds = new THREE.Box3(new THREE.Vector3(-radius, -8, -radius), new THREE.Vector3(radius, 8, radius));
    const expected = createCameraPreset('system-overview', { bounds, aspect, fov: 45, direction: new THREE.Vector3(0, 0.8, 1), padding: 1.15, distanceScale: 0.8 });
    assert.equal(preset.distance, expected.distance);
    assert.throws(() => createSystemOverviewPreset(undefined, aspect), /радиуса/);
    assert.throws(() => createSystemOverviewPreset(radius, NaN), /пропорций/);
});
