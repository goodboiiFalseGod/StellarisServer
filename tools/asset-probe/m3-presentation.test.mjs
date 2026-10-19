import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { DISPLAY_RADII, templateDisplayFactor, orbitPositions, createStarCoronaParameters, validateLabelInsets, labelFitsViewport } from './m3-presentation.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';

test('HUD insets clip the complete label rectangle and remain explicit for exhausted areas', () =>
{
    const input = { top: 16, right: 16, bottom: 72, left: 16 };
    const insets = validateLabelInsets(input);
    input.bottom = 0;
    assert.equal(insets.bottom, 72);
    assert.equal(labelFitsViewport(new THREE.Vector3(0, 0, 0), 40, 10, 1280, 720, insets), true);
    assert.equal(labelFitsViewport(new THREE.Vector3(0, -0.85, 0), 40, 10, 1280, 720, insets), false);
    assert.equal(labelFitsViewport(new THREE.Vector3(0.95, 0, 0), 40, 10, 1280, 720, insets), false);
    assert.equal(labelFitsViewport(new THREE.Vector3(0, 0, 0), 1, 1, 20, 20, insets), false);
    assert.equal(labelFitsViewport(new THREE.Vector3(0, 0, 2), 40, 10, 1280, 720, insets), false);
    assert.throws(() => validateLabelInsets({ ...insets, bottom: -1 }));
    assert.throws(() => validateLabelInsets({ top: 0 }));
    assert.throws(() => validateLabelInsets({ ...insets, bottom: NaN }));
});

test('Surface calibration ignores the atmosphere extent and preserves borrowed geometry', () =>
{
    const group = new THREE.Group();
    group.scale.setScalar(1.3);
    const surface = new THREE.Mesh(new THREE.SphereGeometry(4, 32, 16));
    surface.geometry.computeBoundingSphere();
    surface.userData.sourceShader = 'PdxMeshStar';
    const atmosphere = new THREE.Mesh(new THREE.SphereGeometry(8, 32, 16));
    atmosphere.geometry.computeBoundingSphere();
    atmosphere.userData.sourceShader = 'PdxMeshAtmosphereStar';
    group.add(surface, atmosphere);
    const factor = templateDisplayFactor(group, 'star.g');
    assert.ok(Math.abs(factor * 1.3 * surface.geometry.boundingSphere.radius - DISPLAY_RADII['star.g']) < 1e-8);
    assert.equal(group.scale.x, 1.3);
    assert.equal(surface.geometry.attributes.position.count, 561);
    surface.geometry.dispose(); atmosphere.geometry.dispose();
});

test('Orbit context follows the authorized planet radius and closes without invented bodies', () =>
{
    const values = orbitPositions({ x: 30, z: 40 });
    assert.equal(values.length, 129 * 3);
    for (let index = 0; index < values.length; index += 3) { assert.ok(Math.abs(Math.hypot(values[index], values[index + 2]) - 50) < 1e-8); assert.equal(values[index + 1], 0); }
    assert.throws(() => orbitPositions({ x: NaN, z: 0 }));
    assert.throws(() => createStarCoronaParameters(2, 2));
});

test('Renderer presets use authorized bounds, reject mismatch and preserve world coordinates', () =>
{
    const value = Object.create(M3WorldRenderer.prototype);
    const state = { spatial: { systems: [{ id: 'A', galaxyPosition: { x: 200, y: 0, z: -500 } }, { id: 'B', galaxyPosition: { x: 500, y: 0, z: -200 } }] } };
    const copy = structuredClone(state);
    Object.assign(value, { disposed: false, current: { state }, view: { kind: 'galaxy' }, camera: new THREE.PerspectiveCamera(45, 16 / 9, 0.01, 10000), controls: { target: new THREE.Vector3(), update() {} }, cancelPointer() {} });
    value.setCameraPreset('galaxy-far');
    assert.deepEqual(value.controls.target.toArray(), [350, 0, -350]);
    const far = value.camera.position.distanceTo(value.controls.target);
    value.setCameraPreset('galaxy-near');
    assert.ok(value.camera.position.distanceTo(value.controls.target) < far);
    assert.throws(() => value.setCameraPreset('system-close'));
    assert.throws(() => value.setCameraPreset('other'));
    assert.deepEqual(state, copy);
});
