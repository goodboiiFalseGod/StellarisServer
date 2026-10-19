import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { VISUAL_IDS } from './m3-world-view.mjs';
import { createSystemOverviewPreset } from './m3-camera.mjs';
import { updateGalaxyGlyphScale } from './m3-presentation.mjs';
import { selectionCard } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

// Реальные Three-объекты без WebGL/DOM: это проверка владения, не браузерный gate.
function renderer()
{
    const value = Object.create(M3WorldRenderer.prototype);
    Object.assign(value, { disposed: false, resources: null, profile: null, profileReady: false, current: null, previous: null, retiredSessions: new Set(), fresh: false, needsFreshSnapshot: false, contextLost: false, selection: null, commandFleetId: null, view: { kind: 'galaxy' }, viewResources: null, callbacks: {}, scene: new THREE.Scene(), renderer: { renderLists: { dispose() {} } }, nativePointers: new Map(), nativeZoomGeneration: 0, nativeZoomEvents: new WeakMap(), resetCamera() {}, stageGeneratedGpu() {} });
    return value;
}
function profile()
{
    const image = { width: 1, height: 1, rgba: new Uint8Array([128, 128, 255, 255]) };
    const cube = { width: 1, height: 1, faces: Array.from({ length: 6 }, () => new Uint8Array([128, 128, 128, 255])), codec: 'DXT1', mipCount: 1, decodedMip: 0, isCubemap: true };
    const images = new Map([['d', image], ['n', image], ['s', image], ['noise', cube], ['lava', image], ['stone', image]]);
    const samples = new Map(VISUAL_IDS.map(id => [id, { scale: 2, position: [1, 0, 2], images, meshes: [{ name: 'mesh', positions: new Float32Array([0, 0, 0, 2, 0, 0, 0, 2, 0]), normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]), uv: new Float32Array([0, 0, 1, 0, 0, 1]), indices: new Uint32Array([0, 1, 2]), material: { shader: 'PdxMeshShip', diff: 'd', n: 'n', spec: 's' } }] }]));
    samples.get('planet.continental').meshes[0].material.shader = 'PdxMeshPlanet';
    const star = samples.get('star.g');
    const surface = star.meshes[0];
    surface.material = { shader: 'PdxMeshStar' };
    star.starSurface = { noisePath: 'noise', lavaPath: 'lava', stonePath: 'stone', lavaBrightColor: [0, 1, 1], lavaBrightIntensity: 1, lavaHotStoneColor: [0.2, 1, 1], lavaHotStoneIntensity: 1, lavaColdStoneColor: [0.1, 1, 1], lavaColdStoneIntensity: 1, atmosphereColor: [0.1, 0.7, 0.7], atmosphereIntensity: 0.5, atmosphereWidth: 1.9 };
    star.meshes.push({ ...surface, name: 'atmosphere', positions: Float32Array.from(surface.positions, value => value * 2), material: { shader: 'PdxMeshAtmosphereStar' } });
    return { samples };
}
function state(sequence = 1)
{
    return { protocol: 2, sessionId: 'A', sequence, phase: 'running', paused: false, you: 1, participants: [{ id: 1, role: 'player' }], spatial: { systems: [{ id: 'S', radius: 150, galaxyPosition: { x: 0, y: 0, z: 0 } }], hyperlanes: [], colonies: [], bodies: [{ id: 'B', kind: 'planet', systemId: 'S', localPosition: { x: 50, z: 0 }, scale: 3, visualId: 'planet.continental' }], fleets: [{ id: 'F', ownerParticipantId: 1, phase: 'idle', visualId: 'ship.corvette.mammalian', shipCount: 3, location: { kind: 'system', systemId: 'S', position: { x: 0, z: -100 } }, movement: { orderVersion: 1 } }] } };
}

function navigationState(sequence = 1)
{
    const snapshot = state(sequence);
    snapshot.spatial.systems.push({ id: 'T', name: 'Destination', radius: 150, galaxyPosition: { x: 200, y: 0, z: 0 } });
    snapshot.spatial.hyperlanes.push({ id: 'ST', fromSystemId: 'S', toSystemId: 'T' });
    snapshot.spatial.exits = [{ systemId: 'S', destinationSystemId: 'T', position: { x: 80, z: 60 } }];
    const fleet = snapshot.spatial.fleets[0]; fleet.phase = 'approachingExit';
    fleet.movement = { orderVersion: sequence, target: { kind: 'system', systemId: 'T' }, routeSystemIds: ['S', 'T'] };
    return snapshot;
}

test('Received routes/exits/rings replace atomically and every view resource is owned', () =>
{
    const value = renderer(); value.setProfile(profile()); value.applySnapshot(navigationState()); value.showSystem('S');
    const view = value.viewResources;
    assert.equal(view.routes.size, 1);
    const exitRing=view.group.children.find(child=>child.name==='exit-ring');
    const manualLimit=view.group.children.find(child=>child.name==='manual-limit');
    assert.ok(exitRing);
    assert.ok(manualLimit);
    const firstPoint=ring=>ring.geometry.attributes.position;
    assert.ok(Math.abs(Math.hypot(firstPoint(exitRing).getX(0),firstPoint(exitRing).getZ(0))-100)<1e-6,'exit ring must use the received exit position radius');
    assert.ok(Math.abs(Math.hypot(firstPoint(manualLimit).getX(0),firstPoint(manualLimit).getZ(0))-150)<1e-6,'manual boundary remains the received system radius');
    assert.deepEqual(view.picks.find(pick => pick.userData.pick?.kind === 'exit').userData.pick, { kind: 'exit', id: 'T' });
    const marker=view.group.children.find(child=>child.isGroup&&child.children.some(item=>item.userData.pick?.kind==='exit'));
    assert.deepEqual([marker.position.x,marker.position.z],[80,60]);
    const routePositions=view.routes.get('F').children[0].geometry.attributes.position;
    assert.ok(Array.from({length:routePositions.count},(_,index)=>[routePositions.getX(index),routePositions.getZ(index)]).some(([x,z])=>Math.abs(x-80)<1e-6&&Math.abs(z-60)<1e-6),'route must end at received exit coordinates');
    let disposed = 0;
    for (const resource of view.owned) { resource.addEventListener('dispose', () => disposed++); }
    const stopped = navigationState(2); stopped.spatial.fleets[0].phase = 'idle'; stopped.spatial.fleets[0].movement.target = null;
    value.applySnapshot(stopped);
    assert.equal(disposed, view.owned.size); assert.equal(value.viewResources.routes.size, 0);
    const hidden = navigationState(3); delete hidden.spatial.fleets[0].movement; delete hidden.spatial.exits;
    value.applySnapshot(hidden); assert.equal(value.viewResources.routes.size, 0);
    assert.ok(!value.viewResources.picks.some(pick => pick.userData.pick?.kind === 'exit'));
    assert.ok(!value.viewResources.group.children.some(child=>child.name==='exit-ring'),'systems without permitted exits have no exit ring');
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('Exit orders, inclusive manual radius and rejected gestures use actual pointer handler', () =>
{
    const value = renderer(); value.applySnapshot(navigationState()); value.fresh = true; value.profileReady = true; value.commandFleetId = 'F'; value.view = { kind: 'system', systemId: 'S' };
    const points = [], systems = [], hints = [];
    value.callbacks = { onPointOrder: point => points.push(point), onSystemOrder: id => systems.push(id), onHint: hint => hints.push(hint) };
    let destination = null, point = new THREE.Vector3(150, 0, 0);
    value.pick = (_, kind) => kind === 'exit' ? destination : null;
    value.ray = () => ({ ray: { intersectPlane: () => point } });
    const click = (extra = {}, distance = 0) => { value.pointer = { id: 1, button: 2, x: 10, y: 10, distance, alt: false }; value.pointerUp({ pointerId: 1, clientX: 10, clientY: 10, altKey: false, ...extra }); };
    click(); assert.equal(points.length, 1); assert.equal(points[0].x, 150);
    point = new THREE.Vector3(150.0001, 0, 0); click(); assert.equal(points.length, 1); assert.match(hints.at(-1), /границ/);
    destination = { kind: 'exit', id: 'T' }; click(); assert.deepEqual(systems, ['T']); assert.equal(points.length, 1);
    click({ altKey: true }); click({}, 6); assert.equal(systems.length, 1);
    assert.ok(value.suppressDoubleClickUntil > performance.now());
    value.current.state.participants[0].role = 'observer'; click(); assert.equal(systems.length, 1); assert.ok(hints.length >= 2);
    value.releaseView(value.viewResources);
});

test('Wormhole glyph remains distinct and pickable at galaxy far zoom', () =>
{
    const value=renderer(),snapshot=state();
    snapshot.spatial.bodies.push({id:'star/S',systemId:'S',kind:'star',localPosition:{x:0,z:0},visualId:'star.g',scale:1,worldClassId:'pc_g_star'});
    snapshot.spatial.wormholes=[{id:'wormhole/system/0001',systemId:'S',localPosition:{x:60,z:0},visualId:'object.natural_wormhole',name:'Червоточина',status:'Не исследована'}];
    value.applySnapshot(snapshot);
    const glyph=value.viewResources.objects.get('wormhole:wormhole/system/0001');
    assert.ok(glyph);assert.notEqual(glyph,value.viewResources.objects.get('system:S'));
    assert.equal(glyph.children.length,2);assert.ok(glyph.children.every(child=>child.userData.pick?.kind==='wormhole'));
    value.camera=new THREE.PerspectiveCamera(45,1000/600,.01,50000);value.camera.up.set(0,0,-1);value.camera.position.set(0,10000,0);value.camera.lookAt(0,0,0);value.camera.updateMatrixWorld(true);
    value.controls={target:new THREE.Vector3(),update(){}};value.canvas={getBoundingClientRect:()=>({left:0,top:0,width:1000,height:600})};
    value.renderer.render=()=>{};value.labelInsets={top:0,right:0,bottom:0,left:0};
    for(const item of value.viewResources.galaxyGlyphs) updateGalaxyGlyphScale(item,value.camera,600);
    value.viewResources.group.updateMatrixWorld(true);
    const ring=glyph.children[0];const radius=(ring.geometry.parameters.innerRadius+ring.geometry.parameters.outerRadius)*.5;
    const target=ring.localToWorld(new THREE.Vector3(radius,0,0));target.project(value.camera);
    const event={clientX:(target.x+1)*500,clientY:(1-target.y)*300};
    assert.deepEqual(value.pick(event),{kind:'wormhole',id:'wormhole/system/0001'});
    assert.ok(glyph.scale.x>=1,'far zoom keeps the glyph at its minimum screen size');
    const starGlyph=value.viewResources.galaxyGlyphs.find(item=>item.name==='stellar-glyph:S');
    assert.ok(starGlyph);assert.ok(Math.hypot(glyph.position.x,glyph.position.z)>starGlyph.userData.screenBoundRadius*starGlyph.scale.x+glyph.userData.screenBoundRadius*glyph.scale.x,'far zoom separates wormhole and stellar glyph bounds');
    value.select({kind:'wormhole',id:'wormhole/system/0001'});
    assert.equal(value.selection.kind,'wormhole');
    value.render();assert.equal(value.viewResources.highlights.find(item=>item.pick.kind==='wormhole')?.mesh.visible,true);
    value.releaseView(value.viewResources);
});

test('Automatic detail, selected route/highlight and POV camera continuity use render/applySnapshot', () =>
{
    const value = renderer(); value.applySnapshot(navigationState());
    value.camera = new THREE.PerspectiveCamera(45, 4 / 3, 0.01, 10000);
    value.camera.position.set(0, 240, 0); value.camera.lookAt(0, 0, 0);
    value.controls = { target: new THREE.Vector3(), update() {} };
    value.canvas = { getBoundingClientRect: () => ({ left: 0, top: 0, width: 800, height: 600 }) };
    value.renderer.render = () => {};
    value.labelInsets = { top: 0, bottom: 0, left: 0, right: 0 };
    value.commandFleetId = 'F'; value.selection = { kind: 'system', id: 'S' };
    value.render(); assert.equal(value.detailLevel, 'far'); assert.equal(value.viewResources.routes.get('F').visible, true);
    value.camera.position.set(0, 160, 0); value.camera.lookAt(0, 0, 0); value.render(); assert.equal(value.detailLevel, 'near');
    const position = value.camera.position.clone(), target = value.controls.target.clone();
    const pov = navigationState(2); pov.participants[0].role = 'observer'; pov.viewContext = { mode: 'participant', participantId: 2 }; delete pov.spatial.fleets[0].movement;
    value.applySnapshot(pov); value.render();
    assert.deepEqual(value.camera.position, position); assert.deepEqual(value.controls.target, target);
    assert.equal(value.commandFleetId, null); assert.equal(value.viewResources.routes.size, 0);
    assert.equal(value.viewResources.highlights.find(item => item.pick.kind === 'system' && item.pick.id === 'S').mesh.visible, true);
    value.showSystem('S'); value.camera.position.set(0, 600, 600); value.camera.lookAt(value.controls.target);
    value.current.state.spatial.colonies.push({ id: 'C', bodyId: 'B' }); value.selection = { kind: 'colony', id: 'C' };
    const label = new THREE.Sprite(new THREE.SpriteMaterial()); label.position.set(50, 0, 0); label.scale.set(0.01, 0.01, 1);
    label.userData = { kind: 'body', objectId: 'B', textWidth: 1 }; value.viewResources.group.add(label); value.viewResources.labels.push(label);
    value.render(); assert.equal(value.detailLevel, 'far'); assert.equal(label.visible, true);
    value.selection = null; value.render(); assert.equal(label.visible, false); label.material.dispose();
    value.releaseView(value.viewResources);
});

test('System zoom-out checks each native input after dispatch with exact overview threshold', async () =>
{
    const threshold = createSystemOverviewPreset(150, 16 / 9).distance * 1.5;
    const run = async (distance, kind = 'wheel', extra = {}) =>
    {
        const value = renderer();
        value.view = { kind: 'system', systemId: 'S' };
        value.current = { state: state() };
        value.camera = new THREE.PerspectiveCamera(45, 16 / 9, 0.01, 10000);
        value.camera.position.set(0, 0, 100);
        value.controls = { target: new THREE.Vector3(), enabled: true, enableZoom: true };
        value.nativePointers = new Map();
        value.callbacks = { onSystemZoomOut() { value.returns++; }, onHint(message) { value.hint = message; } };
        value.returns = 0;
        let dispatching = true;
        value.switchToGalaxyAfterZoomOut = () => { assert.equal(dispatching, false, 'view switch must wait until native event dispatch completes'); value.returns++; };
        if (kind === 'middle') { value.nativePointers.set(1, { type: 'mouse', button: 1 }); }
        if (kind === 'touch') { value.nativePointers.set(1, { type: 'touch', button: 0 }); value.nativePointers.set(2, { type: 'touch', button: 0 }); }
        Object.assign(value, extra);
        const event = { deltaY: 1 };
        value.captureNativeZoomInput(kind === 'wheel' ? 'wheel' : 'pointermove', event);
        await Promise.resolve(); // Capture checkpoint occurs before native target/document handlers.
        assert.equal(value.returns, 0, 'capture phase cannot switch views before OrbitControls runs');
        // Simulated native OrbitControls handler updates the camera between capture and bubble.
        value.camera.position.set(0, 0, distance);
        dispatching = false;
        value.finishNativeZoomInput(event);
        return value;
    };
    assert.equal((await run(threshold - 0.01)).returns, 0);
    assert.equal((await run(threshold)).returns, 1);
    assert.equal((await run(threshold + 0.01)).returns, 1);
    assert.equal((await run(200)).returns, 0, 'outward movement below threshold stays in system');
    assert.equal((await run(50)).returns, 0, 'inward movement cannot return');
    assert.equal((await run(100)).returns, 0, 'zero movement cannot return');
    assert.equal((await run(threshold, 'middle')).returns, 1);
    assert.equal((await run(threshold, 'touch')).returns, 1);
    assert.equal((await run(threshold, 'ignored')).returns, 0);
    assert.equal((await run(threshold, 'touch', { nativePointers: new Map([[1, { type: 'touch', button: 0 }]]) })).returns, 0);
    assert.equal((await run(threshold, 'middle', { controls: { target: new THREE.Vector3(), enabled: false, enableZoom: true } })).returns, 0);
});

test('Дальние метки сохраняют домашнюю систему текущего POV, hover и выбор без раскрытия остальных', () =>
{
    const snapshot = navigationState(); snapshot.you = 3;
    snapshot.participants.push({ id: 2, role: 'player' }, { id: 3, role: 'observer' });
    snapshot.viewContext = { mode: 'player', participantId: 2 };
    snapshot.spatial.colonies.push({ id: 'C', bodyId: 'B', ownerParticipantId: 2 });
    const value = renderer(); value.applySnapshot(snapshot);
    value.camera = new THREE.PerspectiveCamera(45, 16 / 9, .01, 10000);
    value.camera.position.set(100, 700, 100); value.camera.lookAt(100, 0, 0);
    value.controls = { target: new THREE.Vector3(100, 0, 0), update() {} };
    value.canvas = { getBoundingClientRect: () => ({ width: 1280, height: 720 }) };
    value.renderer.render = () => {}; value.labelInsets = { top: 0, bottom: 0, left: 0, right: 0 };
    const labels = ['S', 'T'].map(id =>
    {
        const label = new THREE.Sprite(new THREE.SpriteMaterial()); label.position.copy(value.viewResources.objects.get(`system:${id}`).position);
        label.scale.set(.01, .01, 1); label.userData = { kind: 'system', objectId: id, textWidth: 1 };
        value.viewResources.group.add(label); value.viewResources.labels.push(label); return label;
    });
    value.render(); assert.equal(value.detailLevel, 'far'); assert.equal(labels[0].visible, true); assert.equal(labels[1].visible, false);
    value.hoverSystemId = 'T'; value.render(); assert.equal(labels[1].visible, true);
    value.hoverSystemId = null; value.selection = { kind: 'system', id: 'T' }; value.render(); assert.equal(labels[1].visible, true);
    value.releaseView(value.viewResources); for (const label of labels) { label.material.dispose(); }
});

test('System zoom-out defers until after input dispatch, survives snapshots between events and rejects stale/invalid data', async () =>
{
    const value = renderer();
    value.view = { kind: 'system', systemId: 'S' };
    value.current = { state: state() };
    value.camera = new THREE.PerspectiveCamera(45, 16 / 9, 0.01, 10000);
    value.camera.position.set(0, 0, 100);
    value.controls = { target: new THREE.Vector3(), enabled: true, enableZoom: true };
    value.nativePointers = new Map();
    value.callbacks = { onHint(message) { value.hint = message; } };
    let returns = 0; value.switchToGalaxyAfterZoomOut = () => returns++;
    let event = { deltaY: 1 };
    value.captureNativeZoomInput('wheel', event); value.camera.position.z = 1000; value.invalidateNativeZoomGesture(); value.finishNativeZoomInput(event);
    assert.equal(returns, 0);
    event = { deltaY: 1 }; value.captureNativeZoomInput('wheel', event); value.camera.position.z = 1000;
    value.finishNativeZoomInput({ deltaY: 1 }); // A later unrelated window event cannot consume a suppressed input's candidate.
    assert.equal(returns, 0);
    value.current = { state: state(2) };
    value.invalidateNativeZoomGesture(); // A received snapshot/resize between input events invalidates only stale work.
    value.camera.position.z = 100; event = { deltaY: 1 }; value.captureNativeZoomInput('wheel', event); value.camera.position.z = 200; value.finishNativeZoomInput(event);
    assert.equal(returns, 0, 'below-threshold event does not accumulate toward the next event');
    value.current = { state: state(3) };
    event = { deltaY: 1 }; value.captureNativeZoomInput('wheel', event); value.camera.position.z = 1000; value.finishNativeZoomInput(event);
    assert.equal(returns, 1, 'a fresh event after a received snapshot can still return');
    value.view = { kind: 'system', systemId: 'S' }; returns = 0; value.current = { state: state(4) }; value.camera.position.z = 100;
    event = { deltaY: 1 }; value.captureNativeZoomInput('wheel', event); value.current.state.spatial.systems[0].radius = undefined; value.camera.position.z = 1000; value.finishNativeZoomInput(event);
    assert.equal(returns, 0); assert.match(value.hint, /недоступен/);
    value.current.state.spatial.systems[0].radius = 150; value.camera.aspect = NaN;
    value.camera.position.z = 100; event = { deltaY: 1 }; value.captureNativeZoomInput('wheel', event); value.camera.position.z = 1000; value.finishNativeZoomInput(event);
    assert.equal(returns, 0); assert.match(value.hint, /недоступен/);
});

test('System zoom-out switches view once and reports local callback without clearing selection or command fleet', () =>
{
    const value = renderer(); value.view = { kind: 'system', systemId: 'S' }; value.selection = { kind: 'fleet', id: 'F' }; value.commandFleetId = 'F';
    let switched = 0, notified = 0;
    value.switchView = view => { switched++; value.view = view; };
    value.callbacks = { onSystemZoomOut() { notified++; } };
    value.switchToGalaxyAfterZoomOut();
    assert.equal(switched, 1); assert.equal(notified, 1); assert.deepEqual(value.view, { kind: 'galaxy' });
    assert.deepEqual(value.selection, { kind: 'fleet', id: 'F' }); assert.equal(value.commandFleetId, 'F');
});

test('четыре templates, global textures, повтор профиля без conversion', () =>
{
    const value = renderer(), source = profile();
    value.setProfile(source);
    assert.equal(value.resources.templates.size, 4);
    assert.equal(value.resources.textures.length, 12);
    assert.equal(value.resources.convertedBytes, 8);
    const resources = value.resources;
    value.setProfile(source, { maxConvertedBytes: 8 });
    assert.equal(value.resources, resources);
    assert.throws(() => value.setProfile(source, { maxConvertedBytes: 7 }));
    assert.equal(value.resources, resources);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('old+new guard и staged failure атомарно сохраняют профиль и вид', () =>
{
    const value = renderer(); value.setProfile(profile()); value.applySnapshot(state()); value.showSystem('S');
    const resources = value.resources, oldView = value.viewResources;
    assert.throws(() => value.setProfile(profile(), { maxConvertedBytes: 15 }));
    assert.equal(value.resources, resources); assert.equal(value.viewResources, oldView);
    const broken = profile(); broken.samples.get('planet.continental').meshes[0].material.diff = 'missing';
    assert.throws(() => value.setProfile(broken));
    assert.equal(value.resources, resources); assert.equal(value.viewResources, oldView);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('общий origin/entity×DTO, три корвета и instancing200 без копий geometry/material', () =>
{
    const value = renderer(); value.setProfile(profile()); value.applySnapshot(state()); value.showSystem('S');
    const body = value.viewResources.objects.get('body:B');
    const template = value.resources.templates.get('planet.continental');
    // Source triangle sphere: center (1,1,0), radius sqrt(2); entity scale 2, translation (1,0,2).
    const factor = 7 / (Math.sqrt(17) + Math.sqrt(8));
    assert.equal(body.position.x, 50); assert.deepEqual(body.children[0].position.toArray(), [factor, 0, factor * 2]);
    assert.equal(body.children[0].scale.x, 6 * factor);
    assert.equal(body.children[0].children[0].geometry, template.children[0].geometry);
    assert.equal(body.children[0].children[0].material, template.children[0].material);
    const fleet = value.viewResources.fleets.get('F');
    const shipFactor = 1.5 / (Math.sqrt(17) + Math.sqrt(8));
    assert.deepEqual(fleet.children.map(item => [item.position.x, item.position.z]), [[shipFactor - 6, shipFactor * 2 - 4], [shipFactor, shipFactor * 2], [shipFactor + 6, shipFactor * 2 - 4]]);
    value.showStress200();
    const instances = value.viewResources.group.children[0];
    assert.equal(instances.count, 200);
    assert.equal(instances.geometry, value.resources.templates.get('ship.corvette.mammalian').children[0].geometry);
    assert.equal(value.viewResources.picks.length, 0);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('gateway source template is placed in system view, selectable, and disposed with the view', () =>
{
    const value=renderer(),source=profile(),images=source.samples.get('ship.corvette.mammalian').images;
    const mesh={name:'gateway',positions:new Float32Array([-2,0,-2,2,0,-2,0,0,2]),normals:new Float32Array([0,1,0,0,1,0,0,1,0]),uv:new Float32Array([0,0,1,0,0,1]),indices:new Uint32Array([0,1,2]),material:{shader:'PdxMeshShip',diff:'d',n:'n',spec:'s'}};
    source.samples.set('object.gateway_ruined',{scale:1,position:[0,0,0],images,meshes:[mesh],sourceEntityName:'deactivated_gateway_entity',bodyKind:'object',attachments:[],position:[0,0,0]});
    source.generatedRequirements={version:1,policy:'SourceFirstNumberedEntityV1',classIds:['pc_continental'],objectIds:['gateway_ruined']};
    source.decodedBytes=1;
    source.generatedBindings=new Map([['world.pc_continental',{classId:'pc_continental',entityName:'test',kind:'planet'}]]);
    source.samples.set('world.pc_continental',{...source.samples.get('planet.continental'),sourceClassId:'pc_continental',sourceEntityName:'test',bodyKind:'planet',attachments:[]});
    source.generatedObjects=new Map([['object.gateway_ruined',{objectId:'gateway_ruined',scalePolicy:'GatewaySurfaceRadius6V1',approximationPolicy:'StaticGatewayBindPoseV1'}]]);
    source.systemLighting=new Map([['sc_g',{world:'g_star'}]]);
    source.ringSample={sourceEntityName:'gas_giant_rings_01_entity',bodyKind:'decoration',scale:1,meshes:[{name:'ring',positions:new Float32Array([0,0,0,1,0,0,0,1,0]),normals:new Float32Array([0,0,1,0,0,1,0,0,1]),uv:new Float32Array([0,0,1,0,0,1]),indices:new Uint32Array([0,1,2]),material:{shader:'PdxMeshPlanetRings',diff:'d',n:'n',spec:'s'}}],images,attachments:[]};
    value.setProfile(source);const snapshot=state();snapshot.spatial.gateways=[{id:'gateway/0001',systemId:'S',localPosition:{x:20,z:10},visualId:'object.gateway_ruined',name:'Заброшенные врата',status:'Неактивны'}];
    value.applySnapshot(snapshot);value.showSystem('S');
    const object=value.viewResources.objects.get('gateway:gateway/0001');
    assert.ok(object);assert.equal(object.position.x,20);assert.equal(object.position.z,10);
    const surfaceRadius=new THREE.Box3().setFromObject(object).getSize(new THREE.Vector3()).length()/2;
    assert.ok(surfaceRadius>0);
    assert.ok(value.viewResources.picks.some(item=>object.children[0].getObjectById(item.id)===item));
    value.camera=new THREE.PerspectiveCamera(45,1,.01,1000);value.camera.position.set(20,30,10);value.camera.lookAt(20,4,10);value.camera.updateMatrixWorld(true);value.viewResources.group.updateMatrixWorld(true);value.canvas={getBoundingClientRect:()=>({left:0,top:0,width:100,height:100})};
    assert.equal(value.pick({clientX:50,clientY:50},'gateway')?.id,'gateway/0001');
    value.releaseView(value.viewResources);value.releaseProfile(value.resources);
});

test('natural wormhole root and attachment render, select, report a card, fit bounds and reconcile away', () =>
{
    const value=renderer(),source=profile(),images=source.samples.get('ship.corvette.mammalian').images;
    const layer=(name)=>({name,positions:new Float32Array([-2,0,-2,2,0,-2,0,0,2]),normals:new Float32Array([0,1,0,0,1,0,0,1,0]),uv:new Float32Array([0,0,1,0,0,1]),indices:new Uint32Array([0,1,2]),material:{shader:'PdxMeshAlphaAdditiveAnimateUV',diff:'d',spec:'s'}});
    const identity=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];
    source.samples.set('object.gateway_ruined',{scale:1,position:[0,0,0],images,meshes:[{...layer('gateway'),material:{shader:'PdxMeshShip',diff:'d',n:'n',spec:'s'}}],sourceEntityName:'deactivated_gateway_entity',bodyKind:'object',attachments:[]});
    source.samples.set('object.natural_wormhole',{scale:1,position:[0,0,0],images,meshes:[layer('above1Shape'),layer('above2Shape')],sourceEntityName:'natural_wormhole_entity',bodyKind:'object',attachments:[{scale:1,position:[0,0,0],images,meshes:[layer('above3Shape')],matrix:identity}]});
    source.generatedRequirements={version:1,policy:'SourceFirstNumberedEntityV1',classIds:['pc_continental'],objectIds:['gateway_ruined','natural_wormhole']};
    source.decodedBytes=1;
    source.generatedBindings=new Map([['world.pc_continental',{classId:'pc_continental',entityName:'test',kind:'planet'}]]);
    source.samples.set('world.pc_continental',{...source.samples.get('planet.continental'),sourceClassId:'pc_continental',sourceEntityName:'test',bodyKind:'planet',attachments:[]});
    source.generatedObjects=new Map([
        ['object.gateway_ruined',{objectId:'gateway_ruined',scalePolicy:'GatewaySurfaceRadius6V1',approximationPolicy:'StaticGatewayBindPoseV1'}],
        ['object.natural_wormhole',{objectId:'natural_wormhole',scalePolicy:'WormholeSurfaceRadius6V1',approximationPolicy:'StaticWormholeRingsV1'}]
    ]);
    source.systemLighting=new Map([['sc_g',{world:'g_star'}]]);
    source.ringSample={sourceEntityName:'gas_giant_rings_01_entity',bodyKind:'decoration',scale:1,meshes:[{...layer('ring'),material:{shader:'PdxMeshPlanetRings',diff:'d',n:'n',spec:'s'}}],images,attachments:[]};
    value.setProfile(source);
    const snapshot=state();snapshot.spatial.systems[0].radius=350;snapshot.spatial.gateways=[{id:'gateway/0001',systemId:'S',localPosition:{x:20,z:10},visualId:'object.gateway_ruined',name:'Заброшенные врата',status:'Неактивны'}];
    snapshot.spatial.wormholes=[{id:'wormhole/0001',systemId:'S',localPosition:{x:320,z:0},visualId:'object.natural_wormhole',name:'Червоточина',status:'Не исследована'}];
    const selections=[];value.callbacks={onSelect:item=>selections.push(item)};value.applySnapshot(snapshot);
    value.camera=new THREE.PerspectiveCamera(45,1,.01,1000);value.controls={target:new THREE.Vector3(),update(){}};
    value.canvas={getBoundingClientRect:()=>({left:0,top:0,width:100,height:100})};value.resetCamera=M3WorldRenderer.prototype.resetCamera.bind(value);value.showSystem('S');
    assert.ok(value.camera.position.distanceTo(value.controls.target)>createSystemOverviewPreset(150,1).distance,'System overview includes the external wormhole envelope.');
    const object=value.viewResources.objects.get('wormhole:wormhole/0001');assert.ok(object);
    const renderedLayers=[];object.traverse(item=>{if(item.isMesh)renderedLayers.push(item.userData.sourceShader);});
    assert.deepEqual(renderedLayers,['PdxMeshAlphaAdditiveAnimateUV','PdxMeshAlphaAdditiveAnimateUV','PdxMeshAlphaAdditiveAnimateUV']);
    const bounds=new THREE.Box3().setFromObject(object);assert.ok(!bounds.isEmpty());assert.ok(bounds.getSize(new THREE.Vector3()).length()>0);
    value.camera=new THREE.PerspectiveCamera(45,1,.01,1000);value.camera.position.set(320,30,0);value.camera.lookAt(320,4,0);value.camera.updateMatrixWorld(true);value.viewResources.group.updateMatrixWorld(true);value.canvas={getBoundingClientRect:()=>({left:0,top:0,width:100,height:100})};
    assert.equal(value.pick({clientX:50,clientY:50},'wormhole')?.id,'wormhole/0001');
    value.select({kind:'wormhole',id:'wormhole/0001'});assert.deepEqual(selections.at(-1),{kind:'wormhole',id:'wormhole/0001'});assert.equal(value.selectedDisplayObject(),object);
    const card=selectionCard(value.current.state,{kind:'wormhole',id:'wormhole/0001'});
    assert.equal(card.title,'Червоточина');assert.ok(card.rows.some(row=>row[0]==='Состояние'&&row[1]==='Не исследована'));
    const before=selectionCard(value.current.state,value.selection);assert.ok(before);
    let disposed=0;for(const resource of value.viewResources.owned)resource.addEventListener('dispose',()=>disposed++);
    const next=structuredClone(value.current.state);next.sequence++;next.spatial.wormholes=[];value.applySnapshot(next);
    assert.equal(value.selection,null);assert.equal(selections.at(-1),null);assert.ok(disposed>0);
    assert.equal(selectionCard(value.current.state,{kind:'wormhole',id:'wormhole/0001'}),null);
    value.releaseView(value.viewResources);value.releaseProfile(value.resources);
});

test('100 switches освобождают view-only, не shared assets', () =>
{
    const value = renderer(); value.setProfile(profile()); value.applySnapshot(state());
    let sharedDisposals = 0;
    for (const resource of [...value.resources.geometries, ...value.resources.materials, ...value.resources.textures]) { resource.addEventListener('dispose', () => sharedDisposals++); }
    let expected = 0, disposed = 0;
    for (let index = 0; index < 100; index++)
    {
        const old = value.viewResources;
        for (const resource of old.owned) { expected++; resource.addEventListener('dispose', () => disposed++); }
        if (index % 3 === 0) { value.showSystem('S'); } else if (index % 3 === 1) { value.showStress200(); } else { value.showGalaxy(); }
        assert.equal(old.group.children.length, 0);
        assert.equal(old.group.parent, null);
    }
    assert.equal(disposed, expected); assert.equal(sharedDisposals, 0);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
    assert.equal(sharedDisposals, 22);
});

test('A valid planet at the system origin renders without a fabricated zero-radius orbit', () =>
{
    const value = renderer();
    value.setProfile(profile());
    const snapshot = state();
    snapshot.spatial.bodies[0].localPosition = { x: 0, z: 0 };
    value.applySnapshot(snapshot);
    value.showSystem('S');
    assert.deepEqual(value.viewResources.objects.get('body:B').position.toArray(), [0, 0, 0]);
    assert.equal(value.viewResources.group.children.filter(child => child.isLine && !['exit-ring', 'manual-limit'].includes(child.name)).length, 0);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('session/sequence/reconcile/reset, initial импорт сохраняет readiness', () =>
{
    const value = renderer(); value.setProfile(profile()); value.applySnapshot(state());
    assert.equal(value.profileReady, true);
    value.setConnectionFresh(true); value.select({ kind: 'fleet', id: 'F' });
    value.select({ kind: 'system', id: 'S' }); assert.equal(value.commandFleetId, 'F');
    assert.equal(value.applySnapshot(state()), false);
    const next = state(2); next.spatial.fleets = []; value.applySnapshot(next);
    assert.equal(value.commandFleetId, null); assert.equal(value.selection.kind, 'system');
    const newSession = state(); newSession.sessionId = 'B'; value.applySnapshot(newSession);
    assert.equal(value.profileReady, false); assert.equal(value.selection, null);
    assert.equal(value.applySnapshot(state(100)), false);
    value.setProfile(value.profile); assert.equal(value.profileReady, true);
    value.needsFreshSnapshot = true; value.setConnectionFresh(true); assert.equal(value.fresh, false);
    assert.equal(value.applySnapshot(newSession), true); value.setConnectionFresh(true); assert.equal(value.fresh, true);
    assert.equal(value.applySnapshot(newSession), false);
    const input = state(3); input.sessionId = 'B'; value.applySnapshot(input); input.spatial.fleets.length = 0;
    assert.equal(value.current.state.spatial.fleets.length, 1);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});
