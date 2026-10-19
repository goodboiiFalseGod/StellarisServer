import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { M3WorldRenderer } from './m3-world-renderer.mjs';

function fixture(kind = 'galaxy')
{
    const width = 1280, height = 720;
    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    camera.position.set(0, 0, 100);
    camera.lookAt(0, 0, 0);
    const scene = new THREE.Scene();
    const pixelsPerUnit = height / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov) * 0.5));
    const labels = [], objects = new Map();
    function add(id, x, y = 355, labelKind = 'system')
    {
        const label = new THREE.Object3D();
        label.position.set((x - width / 2) * 100 / pixelsPerUnit, (height / 2 - y) * 100 / pixelsPerUnit, 0);
        label.scale.set(0.25, 0.02, 1);
        label.userData = { kind: labelKind, objectId: id, textWidth: 64 / (0.25 * pixelsPerUnit) };
        labels.push(label); scene.add(label);
        objects.set(`${labelKind}:${id}`, new THREE.Object3D());
        return label;
    }
    const renderer = {
        requireLive() {}, contextLost: false, current: null, view: { kind }, detailLevel: 'far',
        controls: { update() {} }, camera, scene,
        canvas: { getBoundingClientRect: () => ({ left: 0, top: 0, width, height }) },
        labelInsets: { top: 16, right: 16, bottom: 72, left: 16 },
        viewResources: { labels, objects }, selection: null, commandFleetId: null, hoverSystemId: null,
        selectedDisplayObject() { return this.selection ? objects.get(`${this.selection.kind}:${this.selection.id}`) : null; },
        renderer: { render() {} }
    };
    const render = () => M3WorldRenderer.prototype.render.call(renderer, 0);
    const visible = () => labels.filter(label => label.visible).map(label => `${label.userData.kind}:${label.userData.objectId}`).sort();
    return { renderer, add, render, visible, labels, objects };
}

test('galaxy crowded labels preserve anchors and objects while selection, hover and zoom expose names', () =>
{
    const f = fixture();
    for (const [index, x] of [628, 659, 691, 723].entries()) { f.add(`S${index + 1}`, x); }
    const positions = f.labels.map(label => label.position.clone());
    const objects = [...f.objects.entries()];
    f.renderer.selection = { kind: 'system', id: 'S3' };
    f.render();
    assert.deepEqual(f.visible(), ['system:S3']);
    f.renderer.hoverSystemId = 'S2';
    f.render();
    assert.ok(f.visible().includes('system:S2'));
    assert.ok(!f.visible().includes('system:S3'));
    f.renderer.hoverSystemId = null;
    f.render();
    assert.deepEqual(f.visible(), ['system:S3']);
    f.renderer.detailLevel = 'near';
    f.render();
    assert.deepEqual(f.visible(), ['system:S3']);
    f.renderer.camera.position.z = 25;
    f.render();
    assert.deepEqual(f.visible(), ['system:S1', 'system:S2', 'system:S3', 'system:S4']);
    assert.deepEqual(f.labels.map(label => label.position), positions);
    assert.deepEqual([...f.objects.entries()], objects);
    assert.equal(f.renderer.camera.position.z, 25);
});

test('galaxy command and inspected fleet labels precede ordinary systems, with viewport clipping first', () =>
{
    const f = fixture();
    f.add('S1', 640); f.add('fleet', 640, 355, 'fleet');
    f.add('outside', 10); f.add('hud', 900, 690);
    f.renderer.commandFleetId = 'fleet';
    f.render();
    assert.deepEqual(f.visible(), ['fleet:fleet']);
    f.renderer.hoverSystemId = 'S1';
    f.render();
    assert.deepEqual(f.visible(), ['system:S1']);
    f.renderer.hoverSystemId = 'outside';
    f.render();
    assert.deepEqual(f.visible(), ['fleet:fleet']);
    f.renderer.hoverSystemId = null;
    f.renderer.commandFleetId = null;
    f.renderer.selection = { kind: 'fleet', id: 'fleet' };
    f.render();
    assert.deepEqual(f.visible(), ['fleet:fleet']);
    f.renderer.selection = null;
    f.render();
    assert.deepEqual(f.visible(), ['system:S1']);
    f.renderer.labelInsets.left = 1300;
    f.render();
    assert.deepEqual(f.visible(), []);
    assert.equal(f.renderer.labelAreaEmpty, true);
});

test('system route groups and exit/body labels retain their existing visibility behavior', () =>
{
    const f = fixture('system');
    f.add('body', 640, 355, 'body'); f.add('destination', 640, 355, 'exit');
    const commandedRoute = { visible: false }, otherRoute = { visible: false };
    f.renderer.viewResources.routes = new Map([['fleet', commandedRoute], ['other', otherRoute]]);
    f.renderer.commandFleetId = 'fleet';
    f.render();
    assert.deepEqual(f.visible(), ['exit:destination']);
    assert.equal(commandedRoute.visible, true); assert.equal(otherRoute.visible, false);
    f.renderer.detailLevel = 'near';
    f.render();
    assert.deepEqual(f.visible(), ['body:body', 'exit:destination']);
    assert.equal(commandedRoute.visible, true);
});
