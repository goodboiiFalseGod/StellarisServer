import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { orbitAboutPivot, radiusAboutOrigin } from './m3-camera.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { readFile } from 'node:fs/promises';

function canvas()
{
    const handlers = new Map(), capture = new Set();
    const element = { style: {}, clientWidth: 800, clientHeight: 600, ownerDocument: { addEventListener() {}, removeEventListener() {} }, getRootNode() { return this.ownerDocument; }, addEventListener(type, callback) { handlers.set(type, callback); }, removeEventListener(type) { handlers.delete(type); }, setPointerCapture(id) { capture.add(id); }, hasPointerCapture(id) { return capture.has(id); }, releasePointerCapture(id) { capture.delete(id); } };
    return element;
}
function fixture()
{
    const value = Object.create(M3WorldRenderer.prototype);
    const camera = new THREE.PerspectiveCamera(45, 1, .01, 10000);
    camera.position.set(140, 100, 180);
    const element = canvas(), controls = new OrbitControls(camera, element);
    controls.target.set(30, 0, 40);
    controls.mouseButtons.LEFT = THREE.MOUSE.PAN;
    controls.enableRotate = false;
    controls.update();
    const body = new THREE.Group(); body.position.set(50, 0, -10);
    const fleet = new THREE.Group(); fleet.position.set(25, 0, 30);
    Object.assign(value, { camera, controls, canvas: element, disposed: false, selection: { kind: 'body', id: 'B' }, current: { state: { spatial: { colonies: [{ id: 'C', bodyId: 'B' }] } } }, viewResources: { objects: new Map([['body:B', body], ['fleet:F', fleet]]), bounds: new THREE.Box3(new THREE.Vector3(100, 0, 0), new THREE.Vector3(200, 50, 20)) }, callbacks: {}, view: { kind: 'system', systemId: 'S' }, handlers: [] });
    value.select = () => { throw new Error('Alt не должен выбирать.'); };
    value.pick = () => { throw new Error('Alt не должен pick.'); };
    return value;
}
function event(x = 10, y = 20, extra = {}) { return { pointerId: 1, button: 0, altKey: true, clientX: x, clientY: y, preventDefault() {}, stopImmediatePropagation() {}, ...extra }; }
function near(actual, expected) { assert.ok(actual.distanceTo(expected) < 1e-9, `${actual.toArray()} != ${expected.toArray()}`); }

test('actual handler: Alt-down/up не меняет camera/target/zoom и не выбирает', () =>
{
    const value = fixture(), position = value.camera.position.clone(), target = value.controls.target.clone(), zoom = value.camera.zoom;
    value.pointerDown(event());
    near(value.camera.position, position); near(value.controls.target, target); assert.equal(value.camera.zoom, zoom);
    assert.equal(value.controls.enabled, false);
    value.pointerUp(event());
    near(value.camera.position, position); near(value.controls.target, target); assert.equal(value.controls.enabled, true);
    assert.equal(value.controls.mouseButtons.LEFT, THREE.MOUSE.PAN);
    value.controls.dispose();
});

test('quaternion вокруг pivot сохраняет все расстояния и pan-offset; .004 rad/px', () =>
{
    const value = fixture(), pivot = value.resolvePivot(), position = value.camera.position.clone(), target = value.controls.target.clone();
    value.pointerDown(event()); value.pointerMove(event(35, 20));
    const rotation = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), -.1);
    near(value.camera.position, position.clone().sub(pivot).applyQuaternion(rotation).add(pivot));
    near(value.controls.target, target.clone().sub(pivot).applyQuaternion(rotation).add(pivot));
    assert.ok(Math.abs(value.camera.position.distanceTo(pivot) - position.distanceTo(pivot)) < 1e-9);
    assert.ok(Math.abs(value.camera.position.distanceTo(value.controls.target) - position.distanceTo(target)) < 1e-9);
    const after = value.camera.position.clone(); value.pointerUp(event(35, 20)); value.controls.update(); near(value.camera.position, after);
    assert.equal(value.controls.enableDamping, false);
    // Реальные OrbitControls pan/dolly после собственной орбиты не откатывают pose.
    value.controls._pan(12, -8); value.controls.update();
    assert.ok(value.camera.position.distanceTo(after) > 0);
    const radius = value.camera.position.distanceTo(value.controls.target);
    value.controls._dollyIn(.9); value.controls.update();
    assert.ok(Math.abs(value.camera.position.distanceTo(value.controls.target) - radius) > .1);
    value.controls.dispose();
});

test('pitch guard предотвращает полюс, сохраняя расстояния и конечную pose', () =>
{
    const value = fixture(), pivot = value.resolvePivot(), radius = value.camera.position.distanceTo(pivot);
    orbitAboutPivot(value.camera, value.controls.target, pivot, 12, 100000);
    assert.ok(value.camera.position.toArray().every(Number.isFinite));
    assert.ok(Math.abs(value.camera.position.distanceTo(pivot) - radius) < 1e-8);
    const direction = value.camera.position.clone().sub(value.controls.target).normalize();
    assert.ok(Math.abs(direction.y) < 1);
    value.controls.dispose();
});

test('selection/colony/fallback и moving fleet: gesture фиксирует актуальную отображённую точку', () =>
{
    const value = fixture();
    value.selection = { kind: 'colony', id: 'C' }; near(value.resolvePivot(), new THREE.Vector3(50, 0, -10));
    value.selection = null; near(value.resolvePivot(), new THREE.Vector3());
    value.selection = { kind: 'system', id: 'elsewhere' }; near(value.resolvePivot(), new THREE.Vector3());
    value.selection = { kind: 'fleet', id: 'F' }; value.pointerDown(event()); near(value.orbit.pivot, new THREE.Vector3(25, 0, 30));
    value.viewResources.objects.get('fleet:F').position.set(60, 0, 80);
    near(value.orbit.pivot, new THREE.Vector3(25, 0, 30));
    value.cancelPointer(); value.pointerDown(event()); near(value.orbit.pivot, new THREE.Vector3(60, 0, 80));
    value.cancelPointer(); value.viewResources.objects.delete('fleet:F'); near(value.resolvePivot(), new THREE.Vector3());
    value.controls.dispose();
});

test('cancel/lostcapture/Alt release restores native controls, unrelated pointer не завершает жест', () =>
{
    for (const ending of ['cancel', 'lostcapture', 'altrelease', 'pagehide'])
    {
        const value = fixture(); value.pointerDown(event());
        value.cancelPointer(event(10, 20, { pointerId: 2 })); assert.ok(value.orbit);
        if (ending === 'altrelease') { value.pointerMove(event(15, 20, { altKey: false })); }
        else if (ending === 'pagehide') { value.cancelPointer(); }
        else { value.cancelPointer(event()); }
        assert.equal(value.orbit, null); assert.equal(value.controls.enabled, true); assert.equal(value.canvas.hasPointerCapture(1), false);
        value.pointerUp(event(15, 20, { altKey: false }));
        value.controls.dispose();
    }
});

test('System reset retains origin and a finite preset; explicit focus preserves offset and zoom', () =>
{
    const value = fixture(); assert.equal(radiusAboutOrigin(value.viewResources.bounds), Math.hypot(200, 50, 20));
    value.resetCamera(); near(value.controls.target, new THREE.Vector3());
    assert.equal(value.cameraPreset, 'system-overview');
    assert.ok(value.camera.position.toArray().every(Number.isFinite));
    value.controls.target.set(22, 0, 33); value.controls.update();
    const offset = value.camera.position.clone().sub(value.controls.target), zoom = value.camera.zoom;
    value.focusSelection(); near(value.controls.target, new THREE.Vector3(50, 0, -10)); near(value.camera.position.clone().sub(value.controls.target), offset); assert.equal(value.camera.zoom, zoom);
    value.controls.dispose();
});

test('actual constructor wiring: capture подавляет native rotate; window/cancel/dispose освобождают handlers', async () =>
{
    function surface()
    {
        const listeners = new Map();
        return {
            listeners,
            addEventListener(type, callback, capture = false) { const values = listeners.get(type) ?? []; values.push({ callback, capture }); listeners.set(type, values); },
            removeEventListener(type, callback) { listeners.set(type, (listeners.get(type) ?? []).filter(value => value.callback !== callback)); },
            dispatch(type, source = {})
            {
                let stopped = false;
                const value = { ...event(), ...source, preventDefault() {}, stopImmediatePropagation() { stopped = true; } };
                for (const listener of [...(listeners.get(type) ?? [])].sort((left, right) => Number(right.capture) - Number(left.capture)))
                {
                    listener.callback(value);
                    if (stopped) { break; }
                }
            }
        };
    }
    class CpuRenderer
    {
        capabilities = { isWebGL2: true };
        renderLists = { dispose() {} };
        setPixelRatio() {}
        setSize() {}
        dispose() {}
    }
    const key = '__m3CameraConstructorTest';
    globalThis[key] = { THREE: { ...THREE, WebGLRenderer: CpuRenderer }, OrbitControls };
    let source = await readFile(new URL('./m3-world-renderer.mjs', import.meta.url), 'utf8');
    source = source.replace("import * as THREE from 'three';", `const THREE = globalThis.${key}.THREE;`).replace("import { OrbitControls } from 'three/addons/controls/OrbitControls.js';", `const OrbitControls = globalThis.${key}.OrbitControls;`).replace(/from '(\.\/[^']+)'/g, (_, path) => `from '${new URL(path, import.meta.url).href}'`);
    let Renderer;
    try { Renderer = (await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'))).M3WorldRenderer; }
    finally { delete globalThis[key]; }
    const window = surface(), document = surface(), element = Object.assign(canvas(), surface());
    document.defaultView = window; element.ownerDocument = document;
    element.getBoundingClientRect = () => ({ width: 800, height: 600 });
    const value = new Renderer(element);
    const original = value.camera.position.clone(), target = value.controls.target.clone();
    element.dispatch('pointerdown');
    assert.equal(value.controls._pointers.length, 0); assert.equal(value.controls.enabled, false);
    near(value.camera.position, original); near(value.controls.target, target);
    element.dispatch('pointerup'); assert.equal(value.controls.enabled, true);
    for (const ending of ['pointercancel', 'lostpointercapture', 'keyup', 'blur', 'pagehide'])
    {
        element.dispatch('pointerdown');
        if (ending === 'keyup') { window.dispatch(ending, { key: 'Alt' }); }
        else if (ending === 'blur' || ending === 'pagehide') { window.dispatch(ending); }
        else { element.dispatch(ending); }
        assert.equal(value.controls.enabled, true); assert.equal(value.orbit, null);
        element.dispatch('pointerup');
    }
    for (const ending of ['lostpointercapture', 'blur', 'pagehide', 'pointercancel'])
    {
        element.dispatch('pointerdown', { altKey: false, pointerType: 'mouse', pageX: 10, pageY: 20 });
        assert.deepEqual(value.controls._pointers, [1]);
        if (ending === 'blur' || ending === 'pagehide') { window.dispatch(ending); }
        else { element.dispatch(ending, { altKey: false }); }
        assert.equal(value.pointer, null); assert.equal(value.controls._pointers.length, 0); assert.equal(value.controls.state, -1);
        assert.equal((document.listeners.get('pointermove') ?? []).length, 0);
        element.dispatch('pointerdown', { altKey: false, pointerType: 'mouse', pageX: 10, pageY: 20 });
        assert.deepEqual(value.controls._pointers, [1]);
        window.dispatch('blur');
    }
    let opened = 0; value.callbacks.onOpenSystem = () => opened++; value.pick = () => ({ kind: 'system', id: 'S' });
    element.dispatch('dblclick'); assert.equal(opened, 0);
    element.dispatch('pointerdown'); value.dispose(); assert.equal(value.controls.enabled, true);
    assert.equal([...window.listeners.values()].flat().length, 0);
    assert.equal([...element.listeners.values()].flat().length, 0);
});
