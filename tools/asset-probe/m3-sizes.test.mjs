import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { displayedSurfaceRadius } from './m3-presentation.mjs';
import { validateSnapshot } from './m3-world-view.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { createSystemOverviewPreset } from './m3-camera.mjs';
import { selectionCard } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

test('размер optional DTO и карточки планеты/луны', () => {
    const state = { protocol:2, sessionId:'sizes', sequence:1, participants:[], spatial:{
        systems:[{id:'s',name:'Система',starClassId:'sc_g',galaxyPosition:{x:0,y:0,z:0},radius:150}], hyperlanes:[], colonies:[], fleets:[],
        bodies:[{id:'b',systemId:'s',name:'Мир',kind:'planet',worldClassId:'pc_gas_giant',visualId:'world.pc_gas_giant',scale:2,planetSize:30,localPosition:{x:100,z:0}}] } };
    for (const kind of ['planet','moon']) {
        state.spatial.bodies[0].kind=kind;
        assert.equal(validateSnapshot(state),state);
        assert.ok(selectionCard(state,{kind:'body',id:'b'}).rows.some(([key,value])=>key==='Размер'&&value==='30'));
        if(kind==='moon') { assert.ok(selectionCard(state,{kind:'body',id:'b'}).subtitle.includes('Спутник')); }
    }
    for (const size of [0,-1,1.5,NaN,'20']) {
        const broken=structuredClone(state); broken.spatial.bodies[0].planetSize=size;
        assert.throws(()=>validateSnapshot(broken));
    }
    const star=structuredClone(state); star.spatial.bodies[0].kind='star'; assert.throws(()=>validateSnapshot(star));
    delete state.spatial.bodies[0].planetSize;
    assert.equal(validateSnapshot(state),state);
    assert.ok(!selectionCard(state,{kind:'body',id:'b'}).rows.some(([key])=>key==='Размер'));
});

test('surface radius исключает FX и управляет подписью/обзором giant', () => {
    const template=new THREE.Group(); template.scale.setScalar(2);
    template.userData={sourceClassId:'pc_gas_giant',generatedDisplayFactor:3,generatedDisplayPolicy:'GeneratedMapUnitSurfaceV1'};
    const surface=new THREE.Mesh(new THREE.SphereGeometry(5,16,8),new THREE.MeshBasicMaterial()); surface.userData.sourceShader='PdxMeshGasGiant';
    const atmosphere=new THREE.Mesh(new THREE.SphereGeometry(50,16,8),new THREE.MeshBasicMaterial()); atmosphere.userData.sourceShader='PdxMeshAtmosphere';
    template.add(surface,atmosphere);
    const radius=displayedSurfaceRadius(template,'world.pc_gas_giant',2);
    assert.ok(Math.abs(radius-60)<1e-5);
    assert.equal(template.scale.x,2);
    const body={id:'b',systemId:'s',name:'Гигант',kind:'planet',worldClassId:'pc_gas_giant',visualId:'world.pc_gas_giant',scale:2,localPosition:{x:100,z:0}};
    const state={you:1,spatial:{systems:[{id:'s',starClassId:'sc_g',radius:150}],bodies:[body],fleets:[],hyperlanes:[],colonies:[]}};
    const resources={templates:new Map([[body.visualId,template]]),generatedBindings:new Map([[body.visualId,{classId:body.worldClassId,kind:'planet'}]]),systemLighting:new Map([['sc_g',{color:[1,1,1],intensity:1,position:[0,0,0]}]])};
    const value=Object.create(M3WorldRenderer.prototype);
    Object.assign(value,{disposed:false,resources,current:{state},view:{kind:'system',systemId:'s'},camera:new THREE.PerspectiveCamera(45,16/9,.01,10000),controls:{target:new THREE.Vector3(),update(){}},cancelPointer(){},
        canvas:{ownerDocument:{createElement:()=>({getContext:()=>({strokeText(){},fillText(){},measureText:()=>({width:100})})})}}});
    const view=value.buildView(resources,state,value.view);
    assert.ok(Math.abs(view.labels[0].position.y-(radius+4))<1e-5);
    assert.equal(view.picks.length,2);
    value.setCameraPreset('system-overview');
    const expected=createSystemOverviewPreset(100+radius+12,16/9);
    assert.ok(value.camera.position.distanceTo(expected.position)<1e-5);
    value.releaseView(view);
    for(const mesh of [surface,atmosphere]) { mesh.geometry.dispose(); mesh.material.dispose(); }
});
