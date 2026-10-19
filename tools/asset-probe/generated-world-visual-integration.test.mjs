import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, statSync } from 'node:fs';
import * as THREE from 'three';
import { createLocalM3Manifest, validateM3Manifest, canonicalManifestText, sha256, generatedProfileId } from './m3-manifest.mjs';
import { buildM3Profile, PROFILE_PATHS } from './m3-profile.mjs';
import { generatedRequirementsCanonicalText, GENERATED_DEFINITION_PATHS, resolveGeneratedWorldVisuals } from './generated-world-visual-profile.mjs';
import { profileTexturePlan, validateSnapshot, validateGeneratedSnapshotBindings } from './m3-world-view.mjs';
import { runM3Import } from './m3-worker-pipeline.mjs';
import { M3WorldRenderer } from './m3-world-renderer.mjs';
import { requireGeneratedMemory } from './generated-world-resource-plan.mjs';
import { gpuResourceReservation } from './generated-world-resource-plan.mjs';
import { collectTransferBuffers } from './m3-worker-pipeline.mjs';
import { templateDisplayFactor, updateGalaxyGlyphScale } from './m3-presentation.mjs';
import { sourceHsvRgb } from './generated-world-material.mjs';
import { parseVisualDefinitions, requireBlock, requireScalar } from './visual-definitions.mjs';

test('Generated requirements: canonical identity/vector и strict schema', async()=>
{
    const r={version:1,policy:'SourceFirstNumberedEntityV1',classIds:['pc_continental','pc_toxic']};
    assert.equal(generatedRequirementsCanonicalText(r),'generated-world-visuals-v1\nSourceFirstNumberedEntityV1\npc_continental\npc_toxic\n');
    assert.equal(await generatedProfileId(r),'m3-static-452-v12-gmu1-gw1-'+await sha256(new TextEncoder().encode(generatedRequirementsCanonicalText(r))));
    for(const bad of [{...r,extra:true},{...r,version:2},{...r,classIds:[...r.classIds].reverse()},{...r,classIds:['pc_toxic','pc_toxic']}]) { assert.throws(()=>generatedRequirementsCanonicalText(bad)); }
    assert.throws(()=>requireGeneratedMemory({decodedBytes:300*1048576,convertedBytes:220*1048576}));
    assert.throws(()=>requireGeneratedMemory({decodedBytes:241*1048576,convertedBytes:83*1048576},{decodedBytes:241*1048576,convertedBytes:83*1048576}));
});

const root=process.env.STELLARIS_GAME_ROOT, manifestPath=process.env.GENERATED_VISUAL_SERVER_MANIFEST;
test('VISUAL-02: actual generated source profile drives map glyphs and projection', {skip:!root}, async()=>
{
    const classIds=['pc_a_star','pc_black_hole','pc_continental','pc_g_star','pc_m_giant_star','pc_m_star','pc_neutron_star','pc_pulsar'];
    const requirements={version:1,policy:'SourceFirstNumberedEntityV1',classIds};
    const read=relative=>{const bytes=readFileSync(root+'/'+relative);return bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength);};
    const closure=resolveGeneratedWorldVisuals(classIds,read,{exists:relative=>statSync(root+'/'+relative,{throwIfNoEntry:false})?.isFile()??false});
    const paths=[...new Set([...PROFILE_PATHS,...GENERATED_DEFINITION_PATHS,...closure.bindings.flatMap(binding=>binding.dependencies)])].sort();
    const buffers=new Map(paths.map(relative=>[relative,read(relative)]));
    const localManifest=await createLocalM3Manifest(buffers,{generatedRequirements:requirements});
    assert.equal(localManifest.profileId,await generatedProfileId(requirements));assert.equal(localManifest.materialProfileVersion,7);
    assert.ok(localManifest.visuals.filter(item=>item.visualId.startsWith('world.')).every(item=>item.materialProfile.endsWith('/GeneratedMapUnitSurfaceV1')));
    const profile=buildM3Profile(buffers,{generatedRequirements:requirements}),value=renderer();value.setProfile(profile);value.addNavigation=()=>{};
    const factor=templateDisplayFactor(value.resources.templates.get('planet.continental'),'planet.continental')*.5;
    assert.equal(value.resources.templates.get('world.pc_continental').userData.generatedDisplayFactor,factor);
    assert.equal(value.resources.templates.get('world.pc_continental').userData.generatedDisplayPolicy,'GeneratedMapUnitSurfaceV1');
    const classes={one:['pc_g_star'],binary:['pc_g_star','pc_a_star'],triple:['pc_g_star','pc_a_star','pc_m_star'],giant:['pc_m_giant_star'],hole:['pc_black_hole'],neutron:['pc_neutron_star'],pulsar:['pc_pulsar']};
    const starClassId=profile.systemLighting.keys().next().value;
    const systems=Object.keys(classes).map(id=>({id,starClassId,radius:100,galaxyPosition:{x:0,y:0,z:0}}));
    const bodies=Object.entries(classes).flatMap(([systemId,ids])=>ids.map((worldClassId,index)=>({id:`${systemId}.${index}`,systemId,kind:'star',worldClassId,visualId:`world.${worldClassId}`,scale:1,localPosition:{x:index*10,z:0}})));
    const state={protocol:2,sessionId:'visual02-source',sequence:1,participants:[],spatial:{galaxyRadius:100,systems,bodies,fleets:[],hyperlanes:[],colonies:[],nebulae:[],belts:[]}};
    validateSnapshot(state);validateGeneratedSnapshotBindings(value.resources,state);
    const view=value.buildView(value.resources,state,{kind:'galaxy'}),glyph=id=>view.galaxyGlyphs.find(item=>item.name===`stellar-glyph:${id}`);
    assert.equal(view.galaxyGlyphs.length,7);
    const dots=id=>{const found=[];glyph(id).traverse(child=>{if(child.isMesh&&child.geometry.type==='SphereGeometry'&&child.scale.x<1)found.push(child);});return found;};
    assert.deepEqual(['one','binary','triple'].map(id=>dots(id).length),[1,2,3]);
    const ornament=(id,type)=>{let found;glyph(id).traverse(child=>{if(child.geometry?.type===type)found=child;});return found;};
    assert.ok(ornament('hole','RingGeometry').geometry.parameters.innerRadius>dots('hole')[0].scale.x*3);
    assert.ok(ornament('neutron','TorusGeometry'));assert.ok(ornament('pulsar','BufferGeometry'));
    assert.equal(dots('hole')[0].material.color.getHexString(),'05070b');
    const sample=profile.samples.get('world.pc_m_giant_star'),hsv=sample.sourceAtmosphereColor??sample.starSurface?.atmosphereColor;
    assert.ok(Array.isArray(hsv)&&hsv.length===3);
    const expected=new THREE.Color().setRGB(...sourceHsvRgb([hsv[0],hsv[1],Math.min(1,hsv[2])]));
    assert.ok(dots('giant')[0].material.color.toArray().every((channel,index)=>Math.abs(channel-expected.toArray()[index])<.005),'giant color comes from pc_m_giant_star metadata.');
    let glow;glyph('triple').traverse(child=>{if(child.isPoints)glow=child;});assert.equal(glow.renderOrder,1);assert.equal(glow.geometry.attributes.color.count,3);
    assert.equal(view.objects.get('system:triple').userData.pick.id,'triple');
    const camera=new THREE.PerspectiveCamera(45,1,.1,20000);
    const projectedDiameter=glyph=>
    {
        glyph.updateMatrixWorld(true);const bounds=new THREE.Box3().setFromObject(glyph),corners=[];
        for(const x of [bounds.min.x,bounds.max.x])for(const y of [bounds.min.y,bounds.max.y])for(const z of [bounds.min.z,bounds.max.z])corners.push(new THREE.Vector3(x,y,z).project(camera));
        return Math.max((Math.max(...corners.map(p=>p.x))-Math.min(...corners.map(p=>p.x)))*300,(Math.max(...corners.map(p=>p.y))-Math.min(...corners.map(p=>p.y)))*300);
    };
    camera.position.set(0,4000,1400);camera.lookAt(0,0,0);camera.updateMatrixWorld();
    for(const id of Object.keys(classes))
    {
        const item=glyph(id);item.parent.updateMatrixWorld(true);assert.ok(updateGalaxyGlyphScale(item,camera,600)>1);
        assert.ok(projectedDiameter(item)>=12,`${id} projected diameter ${projectedDiameter(item).toFixed(2)} CSS px`);
    }
    camera.position.set(0,100,35);camera.lookAt(0,0,0);camera.updateMatrixWorld();
    for(const id of Object.keys(classes)) { const item=glyph(id);assert.ok(updateGalaxyGlyphScale(item,camera,600)>=1);assert.ok(projectedDiameter(item)>=12); }
    value.releaseView(view);value.releaseView(value.viewResources);value.releaseProfile(value.resources);
});
test('VISUAL-02: optional saved-world source mesh support checks every body pair', {skip:!root||!process.env.GENERATED_VISUAL_WORLD_FIXTURE}, async()=>
{
    const world=JSON.parse(readFileSync(process.env.GENERATED_VISUAL_WORLD_FIXTURE,'utf8'));
    const classIds=[...new Set(world.bodies.map(body=>body.ClassId))].sort(),requirements={version:1,policy:'SourceFirstNumberedEntityV1',classIds};
    const read=relative=>{const bytes=readFileSync(root+'/'+relative);return bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength);};
    const closure=resolveGeneratedWorldVisuals(classIds,read,{exists:relative=>statSync(root+'/'+relative,{throwIfNoEntry:false})?.isFile()??false});
    const paths=[...new Set([...PROFILE_PATHS,...GENERATED_DEFINITION_PATHS,...closure.bindings.flatMap(binding=>binding.dependencies)])].sort(),buffers=new Map(paths.map(relative=>[relative,read(relative)]));
    const profile=buildM3Profile(buffers,{generatedRequirements:requirements}),value=renderer();value.setProfile(profile);
    const classAst=parseVisualDefinitions(readFileSync(root+'/common/planet_classes/00_planet_classes.txt','utf8'));
    const variables=new Map([...readFileSync(root+'/common/scripted_variables/00_scripted_variables.txt','utf8').matchAll(/^@([a-z0-9_]+)\s*=\s*([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)/gm)].map(match=>[match[1],Number(match[2])]));
    const entityScale=classId=>{const raw=requireScalar(requireBlock(classAst,classId),'entity_scale'),value=typeof raw==='number'?raw:raw.startsWith('@')?variables.get(raw.slice(1)):Number(raw);assert.ok(Number.isFinite(value)&&value>0,`source entity_scale missing for ${classId}: ${raw}`);return value;};
    const planetBaseline=entityScale('pc_continental');
    const bodies=world.bodies.map(body=>
    {
        const template=value.resources.templates.get(`world.${body.ClassId}`),surface=[],meshSurfaces=[];template.updateMatrixWorld(true);
        template.traverse(mesh=>
        {
            if(!mesh.isMesh||!['PdxMeshStar','PdxMeshPlanet','PdxMeshPlanetEmissive','PdxMeshGasGiant','PdxMeshMoltenPlanet','PdxMeshBlackHole'].includes(mesh.userData.sourceShader))return;
            const positions=mesh.geometry.attributes.position,vertices=[];
            for(let index=0;index<positions.count;index++)vertices.push(new THREE.Vector3().fromBufferAttribute(positions,index).applyMatrix4(mesh.matrixWorld));
            if(vertices.length){surface.push(...vertices);meshSurfaces.push({name:mesh.name,shader:mesh.userData.sourceShader,radius:Math.max(...vertices.map(point=>point.length()))});}
        });
        assert.ok(surface.length,`source surface mesh missing for ${body.ClassId}`);
        const scale=template.userData.generatedDisplayFactor*Math.min(5,Math.max(.1,body.Size/20))*entityScale(body.ClassId)/planetBaseline;
        const radius=Math.max(...surface.map(point=>point.length()))*scale;
        return {body,vertices:surface.map(point=>point.multiplyScalar(scale)),radius,meshSurfaces:meshSurfaces.map(item=>({...item,displayRadius:item.radius*scale}))};
    });
    const systems=new Map();for(const item of bodies){if(!systems.has(item.body.SystemId))systems.set(item.body.SystemId,[]);systems.get(item.body.SystemId).push(item);}
    let pairs=0,minGap=Infinity;const sphereOverlaps=[];
    for(const [systemId,items]of systems)for(let i=0;i<items.length;i++)for(let j=i+1;j<items.length;j++)
    {
        const a=items[i],b=items[j],dx=b.body.X-a.body.X,dz=b.body.Y-a.body.Y,distance=Math.hypot(dx,dz);assert.ok(distance>0,`coincident source centers in ${systemId}`);
        const ux=dx/distance,uz=dz/distance,support=(vertices,x,z)=>Math.max(...vertices.map(point=>point.x*x+point.z*z));
        const gap=distance-support(a.vertices,ux,uz)-support(b.vertices,-ux,-uz);pairs++;minGap=Math.min(minGap,gap);
        if(distance<a.radius+b.radius)sphereOverlaps.push({systemId,a:{id:a.body.Id,classId:a.body.ClassId,surfaceMeshes:a.meshSurfaces},b:{id:b.body.Id,classId:b.body.ClassId,surfaceMeshes:b.meshSurfaces},gap});
        assert.ok(gap>0,`source surface overlap ${systemId}: ${a.body.Id}/${b.body.Id}, points=${a.body.X}/${a.body.Y};${b.body.X}/${b.body.Y}, sizes=${a.body.Size}/${b.body.Size}, distance=${distance}, radii=${a.radius}/${b.radius}, gap=${gap}`);
    }
    console.log(JSON.stringify({sourceFixture:process.env.GENERATED_VISUAL_WORLD_FIXTURE,bodies:bodies.length,pairs,minDirectionalGap:minGap,sphereBoundOverlaps:sphereOverlaps}));
    value.releaseView(value.viewResources);value.releaseProfile(value.resources);
});
let cached;
async function real()
{
    if(!cached) cached=(async()=>
    {
        const manifest=await validateM3Manifest(JSON.parse(readFileSync(manifestPath,'utf8')));
        const buffers=new Map(manifest.files.map(f=>{const b=readFileSync(root+'/'+f.relativePath);return[f.relativePath,b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength)];}));
        const localManifest=await createLocalM3Manifest(buffers,{generatedRequirements:manifest.generatedRequirements});
        const profile=buildM3Profile(buffers,{generatedRequirements:manifest.generatedRequirements});
        return{manifest,buffers,profile,localManifest};
    })();
    return cached;
}
function renderer()
{
    const value=Object.create(M3WorldRenderer.prototype);
    Object.assign(value,{disposed:false,resources:null,profile:null,profileReady:false,current:null,view:{kind:'galaxy'},viewResources:null,scene:new THREE.Scene(),renderer:{renderLists:{dispose(){}}}});
    // CPU wiring tests. Отдельная browser QA проверяет настоящий WebGL staging.
    value.stageGeneratedGpu=()=>{};
    return value;
}
function directory(opened,prefix='')
{
    return{async getDirectoryHandle(name){return directory(opened,prefix+name+'/');},async getFileHandle(name)
    {
        const path=prefix+name; opened.push(path);
        const size=statSync(root+'/'+path).size;
        return{async getFile(){return{name,size,async arrayBuffer(){const b=readFileSync(root+'/'+path);return b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength);}};}};
    }};
}

test('Real 31-class profile: C#/JS canonical parity, native source mips, CPU/GPU reservations и reload rollback', {skip:!root||!manifestPath}, async()=>
{
    const {manifest,profile,localManifest:local}=await real();
    assert.deepEqual(local,manifest);
    assert.ok(manifest.profileId.includes('-gmu1-gw1-'));
    assert.equal(manifest.materialProfileVersion,7);
    assert.ok(manifest.visuals.filter(visual=>visual.visualId.startsWith('world.')).every(visual=>visual.materialProfile.endsWith('/GeneratedMapUnitSurfaceV1')));
    assert.equal(profile.generatedBindings.size,31);
    assert.equal(profile.generatedResourcePolicy,'SourceMip512V1');
    const lighting=JSON.parse(readFileSync(manifestPath.replace(/\.json$/,'.lighting.json'),'utf8')); assert.deepEqual(Object.fromEntries(profile.systemLighting),lighting);
    const plan=profileTexturePlan(profile);
    requireGeneratedMemory({decodedBytes:profile.decodedBytes,convertedBytes:plan.convertedBytes});
    const value=renderer(); value.setProfile(profile);
    assert.equal(value.resources.templates.size,35);
    assert.ok(value.resources.gpuReservationBytes<512*1048576);
    for(const [id,sample] of profile.samples)
    {
        const template=value.resources.templates.get(id);
        for(let i=0;i<sample.meshes.length;i++) { assert.equal(template.children[i].geometry.attributes.position.array,sample.meshes[i].positions); }
    }
    const old=value.resources,view=value.viewResources;
    assert.throws(()=>value.setProfile({...profile}),/old\+staged/);
    assert.equal(value.resources,old); assert.equal(value.viewResources,view); assert.equal(value.profile,profile);
    assert.equal(profile.generatedBindings.get('world.pc_neutron_star').approximationPolicy,'NeutronNoSurfaceAtmosphereMixV1');
    console.log(JSON.stringify({decodedBytes:profile.decodedBytes,convertedBytes:plan.convertedBytes,gpuReservationBytes:old.gpuReservationBytes,files:manifest.files.length,digest:manifest.profileDigest}));
    value.releaseView(view); value.releaseProfile(old);
});

test('Real templates: binary/triple stars, moon semantic class, mismatch fail closed, geometry borrowed per instance', {skip:!root||!manifestPath}, async()=>
{
    const {profile}=await real(); const value=renderer(); value.setProfile(profile);
    const state={protocol:2,sessionId:'generated',sequence:1,participants:[],spatial:{systems:[{id:'S',starClassId:'sc_trinary_1',radius:500,galaxyPosition:{x:0,y:0,z:0}}],hyperlanes:[],colonies:[],fleets:[],bodies:[
        ...['pc_a_star','pc_g_star','pc_neutron_star'].map((worldClassId,i)=>({id:'star'+i,systemId:'S',kind:'star',worldClassId,visualId:'world.'+worldClassId,scale:1,localPosition:{x:i*50,z:0}})),
        {id:'planet',systemId:'S',kind:'planet',worldClassId:'pc_toxic',visualId:'world.pc_toxic',scale:1,localPosition:{x:160,z:0}},
        {id:'moon',systemId:'S',kind:'moon',parentBodyId:'planet',worldClassId:'pc_frozen',visualId:'world.pc_frozen',scale:0.3,localPosition:{x:180,z:0}}
    ]}};
    validateSnapshot(state); validateGeneratedSnapshotBindings(value.resources,state);
    const view=value.buildView(value.resources,state,{kind:'system',systemId:'S'});
    assert.equal(view.objects.size,5);
    const first=view.objects.get('body:star0').children[0];
    assert.equal(first.userData.sourceClassId,'pc_a_star');
    assert.equal(first.children[0].geometry,value.resources.templates.get('world.pc_a_star').children[0].geometry);
    assert.equal(view.objects.get('body:star2').children[0].children.length,3,'Neutron source atmosphere/surface/attachment сохранены.');
    assert.equal(view.group.children.filter(x=>x.isPointLight).length,1);
    assert.equal(view.group.children.find(x=>x.isPointLight).userData.sourceStarClassId,'sc_trinary_1');
    assert.equal(view.group.getObjectByName('orbit:moon').position.x,160,'Moon orbit использует actual parent origin.');
    const continental=value.resources.templates.get('world.pc_continental');
    assert.equal(continental.userData.generatedDisplayPolicy,'GeneratedMapUnitSurfaceV1');
    assert.equal(continental.userData.generatedDisplayFactor,templateDisplayFactor(value.resources.templates.get('planet.continental'),'planet.continental')*.5);
    const classes={single:['pc_g_star'],binary:['pc_g_star','pc_a_star'],triple:['pc_g_star','pc_a_star','pc_m_star'],giant:['pc_m_giant_star'],hole:['pc_black_hole'],neutron:['pc_neutron_star'],pulsar:['pc_pulsar']};
    const starClassId=profile.systemLighting.keys().next().value;
    const systems=Object.keys(classes).map(id=>({id,starClassId,radius:200,galaxyPosition:{x:0,y:0,z:0}}));
    const galaxyBodies=Object.entries(classes).flatMap(([systemId,values])=>values.map((worldClassId,index)=>({id:`${systemId}.${index}`,systemId,kind:'star',worldClassId,visualId:`world.${worldClassId}`,scale:1,localPosition:{x:index*10,z:0}})));
    const galaxyState={protocol:2,sessionId:'generated-glyphs',sequence:1,participants:[],spatial:{galaxyRadius:200,systems,bodies:galaxyBodies,fleets:[],hyperlanes:[],colonies:[],nebulae:[],belts:[]}};
    validateSnapshot(galaxyState); validateGeneratedSnapshotBindings(value.resources,galaxyState); value.addNavigation=()=>{};
    const galaxy=value.buildView(value.resources,galaxyState,{kind:'galaxy'}),glyph=id=>galaxy.galaxyGlyphs.find(item=>item.name===`stellar-glyph:${id}`);
    assert.equal(galaxy.galaxyGlyphs.length,7);
    const dots=id=>{const found=[];glyph(id).traverse(child=>{if(child.isMesh&&child.geometry.type==='SphereGeometry'&&child.scale.x<1)found.push(child);});return found;};
    assert.deepEqual(['single','binary','triple'].map(id=>dots(id).length),[1,2,3]);
    const get=id=>{let found;glyph(id).traverse(child=>{if(child.geometry?.type===id)found=child;});return found;};
    assert.ok(get('RingGeometry').geometry.parameters.innerRadius>dots('hole')[0].scale.x*3);
    assert.ok(get('TorusGeometry')); assert.ok(dots('pulsar').length===1);
    assert.equal(dots('hole')[0].material.color.getHexString(),'05070b');
    const giantProfile=profile.samples.get('world.pc_m_giant_star'),giantHsv=giantProfile.sourceAtmosphereColor??giantProfile.starSurface?.atmosphereColor;
    assert.ok(Array.isArray(giantHsv)&&giantHsv.length===3);
    const giantExpected=new THREE.Color().setRGB(...sourceHsvRgb([giantHsv[0],giantHsv[1],Math.min(1,giantHsv[2])]));
    assert.ok(dots('giant')[0].material.color.equals(giantExpected),'m_giant visual uses its bound pc_m_giant_star color.');
    let glow;glyph('triple').traverse(child=>{if(child.isPoints)glow=child;});assert.equal(glow.renderOrder,1);assert.equal(glow.geometry.attributes.color.count,3);
    assert.equal(galaxy.objects.get('system:triple').userData.pick.id,'triple');
    const camera=new THREE.PerspectiveCamera(45,1,.1,20000);camera.position.set(0,0,4000);camera.lookAt(0,0,0);camera.updateMatrixWorld();
    const triple=glyph('triple');triple.parent.updateMatrixWorld(true);assert.ok(updateGalaxyGlyphScale(triple,camera,600)>1&&triple.scale.x<64);triple.updateMatrixWorld(true);
    const bounds=new THREE.Box3().setFromObject(triple),corners=[];
    for(const x of [bounds.min.x,bounds.max.x])for(const y of [bounds.min.y,bounds.max.y])for(const z of [bounds.min.z,bounds.max.z])corners.push(new THREE.Vector3(x,y,z).project(camera));
    const projectedWidth=(Math.max(...corners.map(p=>p.x))-Math.min(...corners.map(p=>p.x)))*300;
    const projectedHeight=(Math.max(...corners.map(p=>p.y))-Math.min(...corners.map(p=>p.y)))*300;
    assert.ok(Math.max(projectedWidth,projectedHeight)>=11.5,`projected actual glyph bounds ${projectedWidth.toFixed(2)}x${projectedHeight.toFixed(2)} CSS px`);
    const cycle=structuredClone(state); cycle.spatial.bodies.find(b=>b.id==='planet').parentBodyId='moon'; assert.throws(()=>validateSnapshot(cycle));
    const wrong=structuredClone(state); wrong.spatial.bodies[0].worldClassId='pc_g_star'; assert.throws(()=>validateSnapshot(wrong));
    const wrongKind=structuredClone(state); wrongKind.spatial.bodies[0].kind='planet'; assert.throws(()=>validateGeneratedSnapshotBindings(value.resources,wrongKind));
    const wrongLight=structuredClone(state); wrongLight.spatial.systems[0].starClassId='sc_unknown_test'; assert.throws(()=>validateGeneratedSnapshotBindings(value.resources,wrongLight),/starClassId lighting/);
    value.releaseView(galaxy); value.releaseView(view); value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('Generated worker: arbitrary server path is rejected before any read; local source classes are authority', {skip:!root||!manifestPath}, async()=>
{
    const {manifest}=await real(); const extra='gfx/models/planets/unused-test.dds'; const bad=structuredClone(manifest);
    bad.files.push({relativePath:extra,byteLength:1,sha256:'0'.repeat(64),kind:'dds'});
    bad.visuals.forEach(v=>v.dependencies.push(extra)); bad.profileDigest=await sha256(new TextEncoder().encode(canonicalManifestText(bad)));
    const opened=[];
    await assert.rejects(runM3Import({manifest:bad,source:{mode:'directory',handle:directory(opened)},maxDecodedBytes:512*1048576}),/local source graph/);
    assert.ok(!opened.includes(extra));
    const wrong=structuredClone(manifest); wrong.files.find(f=>f.relativePath==='common/planet_classes/00_planet_classes.txt').sha256='0'.repeat(64);
    wrong.profileDigest=await sha256(new TextEncoder().encode(canonicalManifestText(wrong)));
    await assert.rejects(runM3Import({manifest:wrong,source:{mode:'directory',handle:directory([])},maxDecodedBytes:512*1048576}),/локальный файл/);
});

test('Actual eight generated worlds: 200 systems, every body class/source template, binary/triple/mοon coverage и per-case resource plan', {skip:!root||!manifestPath||!process.env.GENERATED_VISUAL_WORLD_INDEX}, async()=>
{
    const {profile}=await real(); const value=renderer(); value.setProfile(profile);
    const index=JSON.parse(readFileSync(process.env.GENERATED_VISUAL_WORLD_INDEX,'utf8')); assert.equal(index.length,8);
    let bodies=0,moons=0,multiple=0; const union=new Set();
    for(const entry of index)
    {
        const bytes=readFileSync(entry.worldPath); assert.equal((await sha256(bytes)).toUpperCase(),entry.worldSha);
        const world=JSON.parse(bytes); assert.equal(world.systems.length,200); assert.ok(world.bodies.length>200);
        const classIds=[...new Set(world.bodies.map(b=>b.ClassId))].sort(); assert.deepEqual(classIds,entry.classIds);
        const spatial={systems:world.systems.map(s=>({id:s.Id,starClassId:s.StarClassId,radius:2000,galaxyPosition:{x:s.Position.X,y:0,z:s.Position.Y}})),
            hyperlanes:[],colonies:[],fleets:[],bodies:world.bodies.map(b=>({id:b.Id,systemId:b.SystemId,parentBodyId:b.ParentId,kind:['planet','moon','star'][b.Kind],
                worldClassId:b.ClassId,visualId:'world.'+b.ClassId,scale:1,localPosition:{x:b.X,z:b.Y}}))};
        const state={protocol:2,sessionId:entry.case,sequence:1,participants:[],spatial}; validateSnapshot(state); validateGeneratedSnapshotBindings(value.resources,state);
        for(const body of spatial.bodies) { union.add(body.worldClassId); assert.equal(value.resources.templates.get(body.visualId).userData.sourceClassId,body.worldClassId); }
        const selected=world.systems.find(s=>world.bodies.filter(b=>b.SystemId===s.Id&&b.Kind===2).length>=3)??world.systems[0];
        const view=value.buildView(value.resources,state,{kind:'system',systemId:selected.Id});
        assert.equal(view.objects.size,world.bodies.filter(b=>b.SystemId===selected.Id).length);
        value.releaseView(view);
        const requirements={...profile.generatedRequirements,classIds}; const ids=new Set([...profile.samples.keys()].filter(id=>!id.startsWith('world.')||classIds.includes(id.slice(6))));
        const subset={...profile,generatedRequirements:requirements,samples:new Map([...profile.samples].filter(([id])=>ids.has(id))),generatedBindings:new Map([...profile.generatedBindings].filter(([id])=>ids.has(id)))};
        const texturePlan=profileTexturePlan(subset); const retainedDecoded=collectTransferBuffers(subset).reduce((n,b)=>n+b.byteLength,0);
        requireGeneratedMemory({decodedBytes:retainedDecoded,convertedBytes:texturePlan.convertedBytes});
        const mapped=new Set(value.resources.textureMap.values()); const cubeFaces=new Set(value.resources.textures.filter(t=>t.isCubeTexture).flatMap(t=>t.images));
        const textures=[...new Set([...value.resources.textures.filter(t=>!mapped.has(t)&&!cubeFaces.has(t)),...[...value.resources.textureMap].filter(([key])=>texturePlan.images.has(key.split(':')[0])).map(([,t])=>t)])];
        const geometries=new Set(); for(const id of ids) { value.resources.templates.get(id).traverse(child=>{if(child.isMesh)geometries.add(child.geometry);}); }
        const gpu=gpuResourceReservation({textures,geometries:[...geometries]}); assert.ok(gpu<512*1048576);
        bodies+=world.bodies.length; moons+=world.bodies.filter(b=>b.Kind===1).length;
        multiple+=world.systems.filter(s=>world.bodies.filter(b=>b.SystemId===s.Id&&b.Kind===2).length>1).length;
        console.log(JSON.stringify({case:entry.case,classes:classIds.length,bodies:world.bodies.length,decodedRetainedBytes:retainedDecoded,convertedBytes:texturePlan.convertedBytes,gpuReservationBytes:gpu,worldSha:entry.worldSha}));
    }
    assert.equal(union.size,29); assert.ok(bodies>16000); assert.ok(moons>0); assert.ok(multiple>0);
    value.releaseView(value.viewResources); value.releaseProfile(value.resources);
});

test('Production generated worker path: full hash/recheck, source DDS release и bounded live staging peak', {skip:!root||!manifestPath}, async()=>
{
    const {manifest}=await real(); const opened=[];
    const result=await runM3Import({manifest,source:{mode:'directory',handle:directory(opened)},maxDecodedBytes:512*1048576});
    assert.equal(result.profileDigest,manifest.profileDigest); assert.equal(result.profile.generatedBindings.size,31);
    assert.equal(result.profile.memoryReport.sourceReleasePolicy,'ReleaseVerifiedDdsAfterDecodeV1');
    assert.ok(result.profile.memoryReport.decodePeakLiveBytes<=512*1048576);
    assert.equal(opened.filter(p=>p==='common/planet_classes/00_planet_classes.txt').length,2);
    const before=[];
    await assert.rejects(runM3Import({manifest,source:{mode:'directory',handle:directory(before)},maxDecodedBytes:180*1048576}),/encoded staging/);
    assert.equal(before.length,0,'Budget rejection не открывает ни одного local source.');
    console.log(JSON.stringify({productionWorkerMemory:result.profile.memoryReport,decodedBytes:result.decodedBytes}));
});

test('Production File snapshots: shared DDS basename не создаёт ложный distant-stars alias', {skip:!root||!manifestPath}, async()=>
{
    const manifest=await validateM3Manifest(JSON.parse(readFileSync(manifestPath,'utf8')));
    const opened=[];
    const files=manifest.files.map(file=>({name:file.relativePath.split('/').at(-1),size:file.byteLength,
        async arrayBuffer() { opened.push(file.relativePath); const bytes=readFileSync(root+'/'+file.relativePath); return bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength); }}));
    const result=await runM3Import({manifest,source:{mode:'files',files},maxDecodedBytes:512*1048576});
    assert.equal(result.mode,'immutable-file-snapshots');
    assert.equal(result.profileDigest,manifest.profileDigest);
    assert.equal(result.profile.generatedBindings.size,31);
    assert.equal(opened.length,manifest.files.length*2,'Каждый точный источник проверен до и после decode.');
    assert.ok(result.profile.memoryReport.decodePeakLiveBytes<=512*1048576);
});

test('Combined boundary rejects before source reads/mesh allocations, even when individual reservations fit',{skip:!root||!manifestPath},async()=>
{
    const {manifest,profile}=await real();
    const encoded=manifest.files.reduce((n,f)=>n+f.byteLength,0);
    const geometry=manifest.files.filter(f=>f.kind==='mesh').reduce((n,f)=>n+f.byteLength*4,0);
    const available=encoded+1; assert.ok(encoded<=available&&geometry<=available&&encoded+geometry>available);
    const opened=[];
    await assert.rejects(runM3Import({manifest,source:{mode:'directory',handle:directory(opened)},maxDecodedBytes:available}),error=>error.code==='memory'&&/combined.*preparse/.test(error.message));
    assert.equal(opened.length,0);
    const buffers=new Map(manifest.files.map(f=>{const b=readFileSync(root+'/'+f.relativePath);return[f.relativePath,b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength)];}));
    const native=globalThis.Float32Array; let allocations=0;
    globalThis.Float32Array=new Proxy(native,{construct(Target,args){allocations++;return new Target(...args);}});
    try { assert.throws(()=>buildM3Profile(buffers,{generatedRequirements:manifest.generatedRequirements,maxDecodedBytes:available}),/combined.*preparse/); }
    finally { globalThis.Float32Array=native; }
    assert.equal(allocations,0,'Ни одного mesh Float32Array до отказа.');
    assert.equal(profile.generatedBindings.size,31,'Прежний валидный profile сохранён.');
});
