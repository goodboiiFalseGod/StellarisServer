import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { beltDecorations, beltOuterRadius, createBeltGroup, validateBelts } from './m3-belts.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { validateSnapshot } from './m3-world-view.mjs';

const belt = {id:'s/belt/0000', systemId:'s', type:'rocky_asteroid_belt', visualId:'world.pc_asteroid', radius:120, width:1, density:1};
test('геометрия: deterministic annulus, ограниченная плотность и binding validation', () => {
    const values = beltDecorations(belt);
    assert.deepEqual(values, beltDecorations(belt)); assert.equal(values.length, 60);
    assert.notDeepEqual(values, beltDecorations({...belt,id:'s/belt/0001'}));
    assert.equal(beltDecorations({...belt,density:.01}).length,32);
    assert.equal(beltDecorations({...belt,density:100}).length,160);
    for (const item of values) { assert.ok(Math.hypot(item.x,item.z)>=114-1e-10 && Math.hypot(item.x,item.z)<=126+1e-10); assert.ok(item.radius>=.3&&item.radius<=1.1); }
    assert.equal(beltOuterRadius(belt),127.1);
    validateBelts([belt],[{id:'s'}]); validateBelts(undefined,[]);
    for (const patch of [{radius:0},{width:NaN},{density:-1},{systemId:'hidden'},{visualId:'world.pc_ice_asteroid'},{type:'unknown'}]) { assert.throws(()=>validateBelts([{...belt,...patch}],[{id:'s'}])); }
    assert.throws(()=>validateBelts([belt,belt],[{id:'s'}]));
    const state={protocol:2,sessionId:'belts',sequence:1,participants:[],spatial:{systems:[{id:'s',galaxyPosition:{x:0,y:0,z:0},radius:150}],bodies:[],fleets:[],colonies:[],hyperlanes:[],belts:[belt]}};
    assert.equal(validateSnapshot(state),state);
});

test('renderer: непикаемые shared templates, system-only и lifecycle без shared disposal', () => {
    const template=new THREE.Group(); template.userData={sourceClassId:'pc_asteroid',generatedDisplayFactor:1,generatedDisplayPolicy:'GeneratedMapUnitSurfaceV1'};
    const geometry=new THREE.SphereGeometry(2); const material=new THREE.MeshBasicMaterial();
    const mesh=new THREE.Mesh(geometry,material); mesh.userData.sourceShader='PdxMeshPlanet'; template.add(mesh);
    let disposed=0; geometry.addEventListener('dispose',()=>disposed++); material.addEventListener('dispose',()=>disposed++);
    const group=createBeltGroup(belt,template); assert.equal(group.children.length,60);
    group.traverse(child=>{assert.equal(child.userData.pick,undefined); if(child.isMesh){assert.equal(child.geometry,geometry);assert.equal(child.material,material);assert.equal(child.raycast(),undefined);} });
    const value=Object.create(M3WorldRenderer.prototype);
    value.callbacks={};
    value.releaseView({group,owned:new Set()}); assert.equal(group.children.length,0); assert.equal(disposed,0);
    const state={you:1,spatial:{systems:[{id:'s',radius:150,galaxyPosition:{x:0,y:0,z:0}}],bodies:[],fleets:[],colonies:[],hyperlanes:[],belts:[belt]}};
    const resources={templates:new Map([[belt.visualId,template]]),generatedBindings:new Map([[belt.visualId,{classId:'pc_asteroid',kind:'planet'}]])};
    for(let repeat=0;repeat<2;repeat++){
        const view=value.buildView(resources,state,{kind:'system',systemId:'s'});
        assert.equal(view.group.children.filter(child=>child.name===belt.id).length,1); assert.equal(view.picks.length,0); assert.ok(view.bounds.max.x>114);
        value.releaseView(view); assert.equal(disposed,0);
    }
    const galaxy=value.buildView(resources,state,{kind:'galaxy'}); assert.equal(galaxy.group.children.filter(child=>child.name===belt.id).length,0); value.releaseView(galaxy);
    geometry.dispose(); material.dispose();
});
