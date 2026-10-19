import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { generatedVisualId, normalizedGeneratedClasses, resolveGeneratedWorldVisuals, generatedTexturePlan, decodeGeneratedTextures, discoverGeneratedMeshPaths, GeneratedVisualError } from './generated-world-visual-profile.mjs';
import { decodeDds, inspectDds } from './formats.mjs';
import { computeConvertedBytes } from './m3-memory.mjs';
import * as THREE from 'three';
import { createGeneratedMaterial, findAtmosphereStarLayers } from './generated-world-material.mjs';
import { generatedBindingApproximationPolicy } from './m3-profile.mjs';

test('Чёрная дыра и pc_broken используют явные статические материалы source', () =>
{
    const materialFor=(shader)=>createGeneratedMaterial({shader,diff:'diff.dds',n:'normal.dds',spec:'spec.dds'},()=>new THREE.Texture());
    assert.ok(materialFor('PdxMeshBlackHole') instanceof THREE.MeshPhongMaterial);
    assert.ok(materialFor('PdxMeshTerra') instanceof THREE.MeshPhongMaterial);
    assert.throws(()=>materialFor('PdxMeshUnknown'),/неподдержанный source shader/);
    const surface={material:{shader:'PdxMeshBlackHole'}},atmosphere={material:{shader:'PdxMeshAtmosphereStar'}};
    assert.deepEqual(findAtmosphereStarLayers({meshes:[surface,atmosphere]}),{surface,atmosphere});
    assert.equal(findAtmosphereStarLayers({meshes:[atmosphere]}),null);
    assert.equal(generatedBindingApproximationPolicy({approximationPolicy:'StaticBlackHoleAttachmentsV1',starSurface:{approximationPolicy:'star'}}),'StaticBlackHoleAttachmentsV1');
    assert.equal(generatedBindingApproximationPolicy({starSurface:{approximationPolicy:'StaticBrokenPlanetRestGeometryV1'}}),'StaticBrokenPlanetRestGeometryV1');
});

test('RGB24 accepts only the exact bounded source layout and expands pixels to opaque RGBA', () =>
{
    const bytes=Buffer.alloc(131); bytes.write('DDS ',0,'ascii'); bytes.writeUInt32LE(124,4); bytes.writeUInt32LE(0x81007,8);
    bytes.writeUInt32LE(1,12); bytes.writeUInt32LE(1,16); bytes.writeUInt32LE(3,20); bytes.writeUInt32LE(32,76);
    bytes.writeUInt32LE(0x40,80); bytes.writeUInt32LE(24,88); bytes.writeUInt32LE(0x00ff0000,92); bytes.writeUInt32LE(0x0000ff00,96); bytes.writeUInt32LE(0x000000ff,100);
    bytes[128]=0x12; bytes[129]=0x34; bytes[130]=0x56;
    const view=()=>bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength);
    assert.equal(inspectDds(view()).codec,'RGB24');
    assert.deepEqual([...decodeDds(view()).rgba],[0x56,0x34,0x12,255]);
    bytes.writeUInt32LE(0x000000f0,100);
    assert.throws(()=>inspectDds(view()),/неизвестный codec/);
});

test('Точный class ID, immutable class set; URL и неизвестный ID не маскируются', () =>
{
    assert.equal(generatedVisualId('pc_g_star'), 'world.pc_g_star');
    assert.deepEqual(normalizedGeneratedClasses(['pc_toxic', 'pc_continental', 'pc_toxic']), ['pc_continental', 'pc_toxic']);
    assert.ok(Object.isFrozen(normalizedGeneratedClasses(['pc_toxic'])));
    for (const id of ['star.g', 'pc_a/../x', 'pc_X', 'pc_x\n', '', null]) { assert.throws(() => generatedVisualId(id), GeneratedVisualError); }
    assert.throws(() => normalizedGeneratedClasses([]), GeneratedVisualError);
    assert.throws(() => normalizedGeneratedClasses(Array(129).fill('pc_a')), GeneratedVisualError);
});

test('Отсутствующий local source — typed gate до создания template', () =>
{
    assert.throws(() => resolveGeneratedWorldVisuals(['pc_continental'], () => undefined), e => e.code === 'missing-source');
});

const root = process.env.STELLARIS_GAME_ROOT;
test('Реальные source classes разрешаются в native buffers и material bindings', { skip: !root }, () =>
{
    const read = path => { const value = readFileSync(root + '/' + path); return value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength); };
    const exists = path => existsSync(root + '/' + path);
    const ids = ['pc_continental', 'pc_desert', 'pc_arid', 'pc_savannah', 'pc_tropical', 'pc_ocean', 'pc_tundra', 'pc_arctic', 'pc_alpine', 'pc_frozen', 'pc_toxic',
        'pc_barren', 'pc_barren_cold', 'pc_gas_giant', 'pc_molten', 'pc_asteroid', 'pc_ice_asteroid', 'pc_rare_crystal_asteroid', 'pc_gaia', 'pc_nuked', 'pc_volcanic', 'pc_broken', 'pc_a_star', 'pc_b_star', 'pc_f_star', 'pc_g_star', 'pc_k_star', 'pc_m_star', 'pc_m_giant_star', 'pc_t_star', 'pc_pulsar', 'pc_neutron_star', 'pc_black_hole'];
    const discovered=discoverGeneratedMeshPaths(['pc_black_hole','pc_broken'],read);
    assert.ok(['gfx/models/planets/black_hole_new.mesh','gfx/models/planets/black_hole_rings.mesh','gfx/models/planets/black_hole_grav_lens.mesh','gfx/models/planets/black_hole_grav_animation.mesh','gfx/models/planets/barren_planet_01_destroyed.mesh'].every(path=>discovered.includes(path)));
    const result = resolveGeneratedWorldVisuals(ids, read, { exists });
    assert.equal(result.bindings.length, ids.length + 1);
    for (const binding of result.bindings.filter(b=>b.kind!=='decoration'))
    {
        assert.equal(binding.visualId, 'world.' + binding.classId);
        assert.ok(binding.meshes.length > 0);
        assert.ok(binding.meshes.every(m => m.positions instanceof Float32Array && m.positions.length > 0 && m.indices instanceof Uint32Array));
        assert.ok(binding.dependencies.every(exists));
        const surface = binding.meshes.find(m => !m.material.shader.startsWith('PdxMeshAtmosphere'));
        assert.ok(surface.material.diff.endsWith('.dds'));
    }
    const a = result.bindings.find(b => b.classId === 'pc_a_star');
    const g = result.bindings.find(b => b.classId === 'pc_g_star');
    assert.notEqual(a.entityName, g.entityName);
    assert.notEqual(a.scale, g.scale);
    assert.equal(a.meshes[1].positions, g.meshes[1].positions, 'Общая native geometry не дублируется на каждый класс.');
    assert.notDeepEqual(a.starSurface.lavaBrightColor,g.starSurface.lavaBrightColor,'Классы получают собственные source worldgfx colors.');
    const pulsar=result.bindings.find(b=>b.classId==='pc_pulsar'); assert.equal(pulsar.attachments.length,1); assert.equal(pulsar.attachments[0].meshes.length,3);
    const blackHole=result.bindings.find(b=>b.classId==='pc_black_hole');
    assert.deepEqual(blackHole.attachments.map(a=>a.entityName),['black_hole_rings_entity','black_hole_grav_lens_entity','black_hole_grav_animation_entity']);
    assert.ok(blackHole.attachments.every(a=>a.policy==='StaticBlackHoleAttachmentsV1'&&a.matrix.length===16&&a.meshes.length>0));
    assert.ok(blackHole.meshes.some(m=>m.material.shader==='PdxMeshBlackHole'));
    assert.ok(blackHole.dependencies.includes('gfx/worldgfx/star_black_hole.txt'));
    const brown=result.bindings.find(b=>b.classId==='pc_t_star');
    assert.equal(brown.entityName,'t_star_class_star_entity'); assert.equal(brown.kind,'star');
    assert.ok(brown.meshes.some(m=>m.material.shader==='PdxMeshPlanetEmissive')); assert.equal(brown.starSurface,undefined,'Native T-star не подменяется lava/G-star material.');
    const plan=generatedTexturePlan(result.bindings,read); assert.ok(plan.decodedBytes<256*1024*1024);
    assert.ok([...plan.plan.values()].every(p=>p.width<=1024&&p.height<=1024));
    const convertedBindings=result.bindings.filter(binding=>['pc_black_hole','pc_broken'].includes(binding.classId));
    const decoded=decodeGeneratedTextures(plan.plan,read);
    const convertedBytes=computeConvertedBytes({meshes:convertedBindings.flatMap(binding=>[...binding.meshes,...binding.attachments.flatMap(attachment=>attachment.meshes)]),images:decoded});
    assert.ok(convertedBytes>0&&convertedBytes<512*1024*1024,'Все BH/Terra Phong channels и Basic omissions учитываются в пределах текущего бюджета.');
    assert.throws(() => resolveGeneratedWorldVisuals(['pc_absent_test'], read), e => e.code === 'source-definition' && e.classId === 'pc_absent_test');
    assert.throws(() => discoverGeneratedMeshPaths(['pc_absent_test'],read),e=>e.code==='source-definition'&&e.classId==='pc_absent_test');
});

test('actual deactivated gateway entity, mesh and material closure resolve from local source', { skip: !root }, () =>
{
    const read=path=>{const value=readFileSync(root+'/'+path);return value.buffer.slice(value.byteOffset,value.byteOffset+value.byteLength);};
    const exists=path=>existsSync(root+'/'+path);
    const graph=resolveGeneratedWorldVisuals(['pc_continental'],read,{exists,objectIds:['gateway_ruined']});
    const gateway=graph.objects.find(binding=>binding.objectId==='gateway_ruined');
    assert.ok(gateway);
    assert.equal(gateway.entityName,'deactivated_gateway_entity');
    assert.equal(gateway.meshPath,'gfx/models/ships/megastructures/gateway/gateway.mesh');
    assert.equal(gateway.approximationPolicy,'StaticGatewayBindPoseV1');
    assert.equal(gateway.meshes.length,1);
    assert.equal(gateway.meshes[0].material.shader,'PdxMeshShip');
    assert.deepEqual(gateway.attachments,[]);
    assert.ok(gateway.dependencies.every(exists));
    assert.ok(gateway.dependencies.includes('common/megastructures/05_gateways.txt'));
    const textures=generatedTexturePlan(graph.objects,read);
    assert.ok(textures.plan.size>0&&[...textures.plan.values()].every(item=>item.width<=512&&item.height<=512));
});

test('gateway source closure rejects availability and scale changes instead of applying them implicitly', { skip: !root }, () =>
{
    const read=path=>{const value=readFileSync(root+'/'+path);return value.buffer.slice(value.byteOffset,value.byteOffset+value.byteLength);};
    const exists=path=>existsSync(root+'/'+path);
    const sourcePath='common/megastructures/05_gateways.txt';
    const original=new TextDecoder().decode(read(sourcePath));
    const changed=original.replace(/(gateway_ruined\s*=\s*\{[\s\S]*?potential\s*=\s*\{\s*always\s*=\s*)no/, '$1yes');
    assert.notEqual(changed,original);
    assert.throws(() => resolveGeneratedWorldVisuals(['pc_continental'],path=>{
        if(path!==sourcePath) return read(path);
        const bytes=new TextEncoder().encode(changed); return bytes.buffer;
    },{exists,objectIds:['gateway_ruined']}), error=>error.code==='gateway-definition');
    const assetPath='gfx/models/ships/megastructures/gateway/gateway.asset';
    const originalAsset=new TextDecoder().decode(read(assetPath));
    const changedAsset=originalAsset.replace('name = "deactivated_gateway_entity"','name = "deactivated_gateway_entity"\n\tscale = 2');
    assert.notEqual(changedAsset,originalAsset);
    assert.throws(() => resolveGeneratedWorldVisuals(['pc_continental'],path=>{
        if(path!==assetPath) return read(path);
        const bytes=new TextEncoder().encode(changedAsset); return bytes.buffer;
    },{exists,objectIds:['gateway_ruined']}), error=>error.code==='gateway-entity');
    const duplicatedDeath=originalAsset.replace(/name = "gateway_death_animation"\s+file = "gateway_death\.anim"/, 'name = "gateway_idle_animation"\n\tfile = "gateway_idle.anim"');
    assert.notEqual(duplicatedDeath,originalAsset);
    assert.throws(() => resolveGeneratedWorldVisuals(['pc_continental'],path=>{
        if(path!==assetPath) return read(path);
        const bytes=new TextEncoder().encode(duplicatedDeath); return bytes.buffer;
    },{exists,objectIds:['gateway_ruined']}), error=>error.code==='gateway-animation');
});
