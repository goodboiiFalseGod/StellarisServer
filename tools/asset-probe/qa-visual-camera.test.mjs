import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { orbitAboutPivot } from './m3-camera.mjs';

// CPU-only surfaces: these tests do not exercise browser input or WebGL.
function fixture()
{
    const captures = new Set();
    const document = { addEventListener() {}, removeEventListener() {} };
    const canvas = { style: {}, clientWidth: 800, clientHeight: 600, ownerDocument: document, getRootNode: () => document, addEventListener() {}, removeEventListener() {}, setPointerCapture: id => captures.add(id), hasPointerCapture: id => captures.has(id), releasePointerCapture: id => captures.delete(id), getBoundingClientRect: () => ({ width: 800, height: 600 }) };
    const camera = new THREE.PerspectiveCamera(45, 4 / 3, .01, 10000);
    camera.position.set(140, 100, 180);
    const controls = new OrbitControls(camera, canvas);
    controls.target.set(30, 0, 40);
    controls.enableRotate = false;
    controls.update();
    const value = Object.create(M3WorldRenderer.prototype);
    Object.assign(value, { camera, controls, canvas, disposed: false, selection: { kind: 'body', id: 'B' }, viewResources: { objects: new Map() } });
    return value;
}

function event(x = 0, y = 0)
{
    return { pointerId: 1, button: 0, altKey: true, clientX: x, clientY: y, preventDefault() {}, stopImmediatePropagation() {} };
}

function near(actual, expected)
{
    assert.ok(actual.distanceTo(expected) < 1e-8, `${actual.toArray()} != ${expected.toArray()}`);
}

test('QA: pivot uses the displayed world position through parent transforms', t =>
{
    const value = fixture(); t.after(() => value.controls.dispose());
    const parent = new THREE.Group(), child = new THREE.Group();
    parent.position.set(10, 3, -20); parent.scale.setScalar(2); parent.rotation.y = Math.PI / 2;
    child.position.set(4, 5, 6); parent.add(child);
    value.viewResources.objects.set('body:B', child);
    near(value.resolvePivot(), new THREE.Vector3(22, 13, -28));
    value.pointerDown(event());
    parent.position.set(200, 0, 200);
    near(value.orbit.pivot, new THREE.Vector3(22, 13, -28));
    value.cancelPointer();
    value.selection = null;
    near(value.resolvePivot(), new THREE.Vector3());
});

test('QA: repeated yaw gestures are reversible and do not drift the pan offset', t =>
{
    const value = fixture(); t.after(() => value.controls.dispose());
    const position = value.camera.position.clone(), target = value.controls.target.clone();
    const pivot = value.resolvePivot();
    const deltas = [...Array(50).fill(13), ...Array(50).fill(-13)];
    for (const [index, delta] of deltas.entries())
    {
        value.pointerDown(event()); value.pointerMove(event(delta)); value.pointerUp(event(delta));
        assert.ok(Math.abs(value.camera.position.distanceTo(value.controls.target) - position.distanceTo(target)) < 1e-8);
        assert.ok(Math.abs(value.camera.position.distanceTo(pivot) - position.distanceTo(pivot)) < 1e-8);
        assert.ok(Math.abs(value.controls.target.distanceTo(pivot) - target.distanceTo(pivot)) < 1e-8);
        if (index === 0)
        {
            assert.ok(value.camera.position.distanceTo(position) > 1, 'A nonzero gesture must move the camera');
            assert.ok(value.controls.target.distanceTo(target) > 1, 'A nonzero gesture must move the panned target');
        }
    }
    near(value.camera.position, position); near(value.controls.target, target);
});

test('QA: resize preserves pose and the active pivot; completion preserves disabled controls', t =>
{
    const value = fixture(); t.after(() => value.controls.dispose());
    value.controls.enabled = false;
    value.pointerDown(event());
    const position = value.camera.position.clone(), target = value.controls.target.clone(), pivot = value.orbit.pivot.clone();
    let measured;
    value.renderer = { setSize: (...args) => { measured = args; } };
    value.canvas.getBoundingClientRect = () => ({ width: 1200, height: 400 });
    value.resize();
    assert.deepEqual(measured, [1200, 400, false]); assert.equal(value.camera.aspect, 3);
    near(value.camera.position, position); near(value.controls.target, target); near(value.orbit.pivot, pivot);
    value.pointerUp(event()); assert.equal(value.controls.enabled, false); assert.equal(value.canvas.hasPointerCapture(1), false);
});

test('QA: invalid rotation deltas fail before mutating camera or target', t =>
{
    const value = fixture(); t.after(() => value.controls.dispose());
    const position = value.camera.position.clone(), target = value.controls.target.clone();
    for (const [dx, dy] of [[NaN, 0], [0, Infinity], [-Infinity, 2]])
    {
        assert.throws(() => orbitAboutPivot(value.camera, value.controls.target, new THREE.Vector3(), dx, dy));
        near(value.camera.position, position); near(value.controls.target, target);
    }
});
