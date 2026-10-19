import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { PROFILE_PATHS } from './m3-profile.mjs';
import { createLocalM3Manifest } from './m3-manifest.mjs';
import { runM3Import, collectTransferBuffers } from './m3-worker-pipeline.mjs';
import { GENERATED_DEFINITION_PATHS, GENERATED_OBJECT_DEFINITION_PATHS, generatedObjectDefinitionPaths, generatedTexturePlan, resolveGeneratedWorldVisuals } from './generated-world-visual-profile.mjs';

const fakeProfile = () => { const buffer = new ArrayBuffer(16); return { decodedBytes: 16, samples: new Map([['test', { vertices: new Float32Array(buffer), other: new Uint8Array(buffer) }]]), icons: new Map() }; };
async function fixture()
{
    const buffers = new Map(PROFILE_PATHS.map(path => [path, new Uint8Array([1, 2, 3]).buffer]));
    const manifest = await createLocalM3Manifest(buffers);
    let reads = 0;
    const files = PROFILE_PATHS.map(path => ({ name: path.split('/').at(-1), size: 3, arrayBuffer: async () => { reads++; return buffers.get(path).slice(0); } }));
    return { buffers, manifest, files, reads: () => reads, maxDecodedBytes: 1024 };
}
test('Pipeline: snapshots дважды хешируются, progress и unique transfer сохраняют typed arrays', async () =>
{
    const f = await fixture(); const progress = [];
    const result = await runM3Import({ ...f, source: { mode: 'files', files: f.files } }, { buildProfile: fakeProfile, onProgress: value => progress.push(value) });
    assert.equal(f.reads(), PROFILE_PATHS.length * 2); assert.equal(result.mode, 'immutable-file-snapshots');
    assert.equal(result.profileDigest, f.manifest.profileDigest);
    assert.equal(progress.at(-1).phase, 'ready');
    assert.equal(progress.filter(value => value.phase === 'read').length, PROFILE_PATHS.length);
    const transfers = collectTransferBuffers(result.profile); assert.equal(transfers.length, 1);
    const received = structuredClone(result.profile, { transfer: transfers });
    assert.equal(transfers[0].byteLength, 0);
    assert.equal(received.samples.get('test').vertices.byteLength, 16);
    assert.equal(received.samples.get('test').vertices.buffer, received.samples.get('test').other.buffer);
});
test('Pipeline: лишние/missing/duplicate snapshots, mismatches, ошибки decode/budget отказ до ready', async () =>
{
    const f = await fixture();
    for (const files of [f.files.slice(1), [...f.files, f.files[0]], [f.files[0], ...f.files.slice(0, -1)], [{ ...f.files[0], name: 'extra.dds' }, ...f.files.slice(1)]])
    {
        await assert.rejects(runM3Import({ ...f, source: { mode: 'files', files } }, { buildProfile: fakeProfile }));
    }
    assert.equal(f.reads(), 0);
    for (const file of [{ ...f.files[0], size: 4 }, { ...f.files[0], arrayBuffer: async () => new Uint8Array([3, 2, 1]).buffer }])
    {
        await assert.rejects(runM3Import({ ...f, source: { mode: 'files', files: [file, ...f.files.slice(1)] } }, { buildProfile: fakeProfile }), /локальный файл/);
    }
    await assert.rejects(runM3Import({ ...f, maxDecodedBytes: -1, source: { mode: 'files', files: f.files } }, { buildProfile: fakeProfile }), /budget/);
    await assert.rejects(runM3Import({ ...f, source: { mode: 'files', files: f.files } }, { buildProfile: () => { throw new Error('unsupported format'); } }), /unsupported/);
});
test('Pipeline: directory читает только fixed paths, changed reread и permission denial не публикуются', async () =>
{
    for (const failure of [null, 'changed', 'denied'])
    {
        const f = await fixture(); let passes = 0; const requested = [];
        const directory = prefix => ({
            getDirectoryHandle: async part => directory(prefix + part + '/'),
            getFileHandle: async name => ({ getFile: async () =>
            {
                const path = prefix + name; requested.push(path);
                if (path === f.manifest.files[0].relativePath) { passes++; }
                if (passes === 2 && failure === 'denied') { throw new DOMException('denied', 'NotAllowedError'); }
                const bytes = passes === 2 && failure === 'changed' ? new Uint8Array([7, 8, 9]).buffer : f.buffers.get(path);
                return { size: bytes.byteLength, arrayBuffer: async () => bytes.slice(0) };
            } })
        });
        const request = { ...f, source: { mode: 'directory', handle: directory('') } };
        if (failure) { await assert.rejects(runM3Import(request, { buildProfile: fakeProfile }), /локальный файл/); }
        else { assert.equal((await runM3Import(request, { buildProfile: fakeProfile })).mode, 'read-only-directory-rechecked'); assert.equal(requested.length, PROFILE_PATHS.length * 2); }
        assert.ok(requested.every(path => PROFILE_PATHS.includes(path)));
    }
});
test('Pipeline: неверная metadata не читает источник; ошибка File snapshot при recheck отклоняет staged профиль', async () =>
{
    const f = await fixture();
    await assert.rejects(runM3Import({ ...f, manifest: { ...f.manifest, gameVersion: '4.5.3' }, source: { mode: 'files', files: f.files } }, { buildProfile: fakeProfile }));
    assert.equal(f.reads(), 0);
    let decoded = false;
    const first = f.files[0];
    f.files[0] = { ...first, arrayBuffer: async () => { if (decoded) { throw new DOMException('read failed', 'NotReadableError'); } return first.arrayBuffer(); } };
    await assert.rejects(runM3Import({ ...f, source: { mode: 'files', files: f.files } }, { buildProfile: () => { decoded = true; return fakeProfile(); } }), /NotReadableError/);
});
test('Pipeline: real profile snapshots декодируются, слои и transfer уникальны', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const buffers = new Map();
    for (const path of PROFILE_PATHS) { const bytes = await readFile(resolve(process.env.STELLARIS_GAME_ROOT, path)); buffers.set(path, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)); }
    const manifest = await createLocalM3Manifest(buffers);
    const files = [...buffers].map(([path, bytes]) => ({ name: path.split('/').at(-1), size: bytes.byteLength, arrayBuffer: async () => bytes.slice(0) }));
    const result = await runM3Import({ manifest, source: { mode: 'files', files }, maxDecodedBytes: 512 * 1024 * 1024 });
    assert.deepEqual([...result.profile.samples.values()].map(sample => sample.meshes.length), [1, 3, 5, 2]);
    assert.deepEqual([...result.profile.galaxyLayers.keys()], ['core', 'dust', 'color']);
    const galaxyBuffers = [...result.profile.galaxyLayers.values()].map(layer => layer.image.rgba.buffer);
    assert.equal(new Set(galaxyBuffers).size, 3);
    assert.equal(galaxyBuffers.reduce((total, buffer) => total + buffer.byteLength, 0), 25165824);
    const transfers = collectTransferBuffers(result.profile); assert.ok(transfers.length > 20);
    assert.equal(new Set(transfers).size, transfers.length);
    assert.ok(galaxyBuffers.every(buffer => transfers.includes(buffer)));
});

test('Pipeline: actual gateway and wormhole objects close local source, import and bind together', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const root=process.env.STELLARIS_GAME_ROOT,cache=new Map();
    const objectIds=['gateway_ruined','natural_wormhole'];
    const read=async path=>
    {
        if(!cache.has(path))
        {
            const bytes=await readFile(resolve(root,path));
            cache.set(path,bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength));
        }
        return cache.get(path);
    };
    for(const path of [...PROFILE_PATHS,...GENERATED_DEFINITION_PATHS,...generatedObjectDefinitionPaths(objectIds),'gfx/models/planets/gas_giant_rings_01.mesh','gfx/models/ships/megastructures/gateway/gateway.mesh','gfx/models/add_ons/wormhole_rings.mesh','gfx/models/add_ons/wormhole_stretched_ring.mesh']) { await read(path); }
    const graph=resolveGeneratedWorldVisuals(['pc_continental'],path=>cache.get(path),{exists:path=>existsSync(resolve(root,path)),objectIds});
    for(const path of new Set(graph.objects.flatMap(binding=>binding.dependencies.filter(path=>path.endsWith('.dds'))))) { await read(path); }
    const wormholeTexturePlan=generatedTexturePlan(graph.objects,path=>cache.get(path)).plan;
    assert.deepEqual([wormholeTexturePlan.get('gfx/models/add_ons/wormhole_rings.dds').policy,wormholeTexturePlan.get('gfx/models/add_ons/wormhole_rings.dds').width,wormholeTexturePlan.get('gfx/models/add_ons/wormhole_rings.dds').height],['WormholeNativeMip0V1',1024,1024]);
    assert.deepEqual([wormholeTexturePlan.get('gfx/models/add_ons/wormhole_stretch.dds').policy,wormholeTexturePlan.get('gfx/models/add_ons/wormhole_stretch.dds').width,wormholeTexturePlan.get('gfx/models/add_ons/wormhole_stretch.dds').height],['WormholeNativeMip0V1',2048,1024]);
    const originalRings=new Uint8Array(cache.get('gfx/models/add_ons/wormhole_rings.dds'));
    const extraMip=new Uint8Array(originalRings.length+262144);extraMip.set(originalRings);new DataView(extraMip.buffer).setUint32(28,2,true);
    assert.throws(()=>generatedTexturePlan(graph.objects,path=>path==='gfx/models/add_ons/wormhole_rings.dds'?extraMip.buffer:cache.get(path)),error=>error.code==='wormhole-native-mip');
    const wrongSize=originalRings.slice();new DataView(wrongSize.buffer).setUint32(12,256,true);new DataView(wrongSize.buffer).setUint32(16,256,true);
    assert.throws(()=>generatedTexturePlan(graph.objects,path=>path==='gfx/models/add_ons/wormhole_rings.dds'?wrongSize.buffer:cache.get(path)));
    assert.throws(()=>generatedTexturePlan([{objectId:'gateway_ruined',dependencies:['gfx/models/add_ons/wormhole_rings.dds']}],path=>cache.get(path)),error=>error.code==='wormhole-native-mip');
    const paths=[...new Set([...PROFILE_PATHS,...GENERATED_DEFINITION_PATHS,...generatedObjectDefinitionPaths(objectIds),...graph.bindings.flatMap(b=>b.dependencies),...graph.objects.flatMap(b=>b.dependencies)])].sort();
    for(const path of paths) { await read(path); }
    const requirements={version:1,policy:'SourceFirstNumberedEntityV1',classIds:['pc_continental'],objectIds};
    const manifest=await createLocalM3Manifest(cache,{generatedRequirements:requirements});
    const files=paths.map(path=>{const bytes=cache.get(path);return{name:path.split('/').at(-1),size:bytes.byteLength,arrayBuffer:async()=>bytes.slice(0)};});
    const result=await runM3Import({manifest,source:{mode:'files',files},maxDecodedBytes:512*1024*1024});
    assert.equal(result.profile.generatedObjects.get('object.gateway_ruined').objectId,'gateway_ruined');
    const sample=result.profile.samples.get('object.gateway_ruined');
    assert.equal(sample.sourceEntityName,'deactivated_gateway_entity');
    assert.equal(sample.meshes.length,1);
    assert.equal(sample.meshes[0].material.shader,'PdxMeshShip');
    assert.ok(sample.images.size===3);
    assert.equal(result.profile.generatedRequirements.objectIds[0],'gateway_ruined');
    const wormhole=result.profile.samples.get('object.natural_wormhole');
    assert.equal(result.profile.generatedObjects.get('object.natural_wormhole').objectId,'natural_wormhole');
    assert.equal(wormhole.sourceEntityName,'natural_wormhole_entity');
    assert.equal(wormhole.meshes.length,2);assert.equal(wormhole.attachments.length,1);
    assert.equal(wormhole.attachments[0].meshes.length,1);
    assert.deepEqual([...new Set(result.profile.generatedRequirements.objectIds)].sort(),objectIds.slice().sort());
    assert.equal(result.profile.generatedResourcePolicy,'SourceMip512V1+WormholeNativeMip0V1');
    assert.equal(manifest.visuals.find(visual=>visual.visualId==='object.natural_wormhole').materialProfile,'generated-object-static-v1/WormholeNativeMip0V1/StaticWormholeRingsV1/WormholeSurfaceRadius6V1/WormholeOutsideOrbitalEnvelopeV1');
    assert.ok(result.profile.memoryReport.decodePeakLiveBytes<512*1024*1024,'native mip0 stays within the unchanged import peak budget');
});

test('Pipeline: cube faces transfer без копий и дублей, shared image сохраняется', () =>
{
    const faces = Array.from({ length: 6 }, (_, index) => new Uint8Array([index, 0, 0, 255]));
    const image = { width: 1, height: 1, isCubemap: true, faces };
    const profile = { samples: new Map([['star.g', { images: new Map([['noise', image]]), borrowed: image }]]) };
    const transfers = collectTransferBuffers(profile);
    assert.equal(transfers.length, 6);
    const received = structuredClone(profile, { transfer: transfers });
    assert.ok(faces.every(face => face.byteLength === 0));
    const sample = received.samples.get('star.g');
    assert.equal(sample.images.get('noise'), sample.borrowed);
    assert.deepEqual(sample.borrowed.faces.map(face => face[0]), [0, 1, 2, 3, 4, 5]);
});

test('Pipeline: presentation maps transfer unique buffers and preserve shared image identity', () =>
{
    const cube = { width: 1, height: 1, faces: Array.from({ length: 6 }, (_, index) => new Uint8Array([index, 1, 2, 255])), isCubemap: true };
    const sprite = { width: 1, height: 1, rgba: new Uint8Array([8, 9, 10, 255]) };
    const galaxyImages = Array.from({ length: 3 }, (_, index) => ({ width: 1, height: 1, rgba: new Uint8Array([index, 20, 30, 255]) }));
    const profile = {
        backgrounds: new Map([['galaxy.stars', { image: cube }], ['system.sky:a', { layers: [{ sourcePath: 'sky-core.dds', image: cube }, { sourcePath: 'sky-mid.dds', image: cube }, { sourcePath: 'sky-rim.dds', image: cube }] }]]),
        uiSprites: new Map([['frame.dds', { image: sprite, frames: 1, border: { x: 0, y: 0 } }]]),
        icons: new Map([['shared', sprite]]),
        galaxyLayers: new Map(['core', 'dust', 'color'].map((key, index) => [key, { image: galaxyImages[index], encoding: 'raw-rgba' }])),
        borrowedGalaxyImage: galaxyImages[0]
    };
    const transfers = collectTransferBuffers(profile);
    assert.equal(transfers.length, 10);
    assert.equal(new Set(transfers).size, 10);
    const received = structuredClone(profile, { transfer: transfers });
    assert.ok(cube.faces.every(face => face.byteLength === 0));
    assert.equal(sprite.rgba.byteLength, 0);
    assert.ok(galaxyImages.every(image => image.rgba.byteLength === 0));
    assert.equal(received.galaxyLayers.get('core').image, received.borrowedGalaxyImage);
    assert.deepEqual([...received.galaxyLayers.values()].map(layer => [...layer.image.rgba]), [[0, 20, 30, 255], [1, 20, 30, 255], [2, 20, 30, 255]]);
    assert.equal(received.backgrounds.get('galaxy.stars').image, received.backgrounds.get('system.sky:a').layers[0].image);
    assert.equal(received.backgrounds.get('system.sky:a').layers[0].image, received.backgrounds.get('system.sky:a').layers[2].image);
    assert.equal(received.uiSprites.get('frame.dds').image, received.icons.get('shared'));
    assert.deepEqual([...received.uiSprites.get('frame.dds').image.rgba], [8, 9, 10, 255]);
});
