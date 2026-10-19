import { PROFILE_PATHS, buildM3Profile } from './m3-profile.mjs';
import { validateM3Manifest, sha256 } from './m3-manifest.mjs';
import { GENERATED_DEFINITION_PATHS, GENERATED_OBJECT_DEFINITION_PATHS, generatedObjectDefinitionPaths, discoverGeneratedMeshPaths, resolveGeneratedWorldVisuals, resolveGeneratedWorldObjects, GeneratedVisualError } from './generated-world-visual-profile.mjs';

export function collectTransferBuffers(value)
{
    const buffers = new Set();
    const seen = new Set();
    function visit(item)
    {
        if (!item || typeof item !== 'object' || seen.has(item)) { return; }
        seen.add(item);
        if (item instanceof ArrayBuffer) { buffers.add(item); return; }
        if (ArrayBuffer.isView(item)) { if (!(item.buffer instanceof ArrayBuffer)) { throw new Error('Неподдержанный shared buffer.'); } buffers.add(item.buffer); return; }
        if (item instanceof Map) { for (const [key, entry] of item) { visit(key); visit(entry); } return; }
        if (item instanceof Set) { for (const entry of item) { visit(entry); } return; }
        for (const entry of Object.values(item)) { visit(entry); }
    }
    visit(value);
    return [...buffers];
}

export async function runM3Import({ manifest, source, maxDecodedBytes }, { onProgress = () => {}, buildProfile = buildM3Profile } = {})
{
    const start = performance.now();
    let paths = PROFILE_PATHS;
    const progress = (phase, checked, relativePath = null) => onProgress({ phase, checked, total: paths.length, relativePath });
    progress('metadata', 0);
    const checkedManifest = await validateM3Manifest(manifest);
    paths = checkedManifest.files.map(f=>f.relativePath);
    if (!Number.isSafeInteger(maxDecodedBytes) || maxDecodedBytes < 0 || maxDecodedBytes > 512 * 1024 * 1024) { throw new Error('Неверный decoded budget.'); }
    if(checkedManifest.generatedRequirements)
    {
        const encoded=checkedManifest.files.reduce((sum,f)=>sum+f.byteLength,0);
        const geometry=checkedManifest.files.filter(f=>f.kind==='mesh').reduce((sum,f)=>sum+f.byteLength*4,0);
        if(encoded>maxDecodedBytes) { throw new GeneratedVisualError('memory',null,'Generated encoded staging превышает доступный old+staged budget до чтения source.'); }
        if(encoded+geometry>maxDecodedBytes) { throw new GeneratedVisualError('memory',null,'Generated combined source+geometry preparse budget превышен до чтения source.'); }
    }
    const objectPaths=checkedManifest.generatedRequirements?.objectIds?generatedObjectDefinitionPaths(checkedManifest.generatedRequirements.objectIds):[];
    if(checkedManifest.generatedRequirements&&checkedManifest.files.filter(f=>GENERATED_DEFINITION_PATHS.includes(f.relativePath)||objectPaths.includes(f.relativePath)).reduce((sum,f)=>sum+f.byteLength,0)>16*1024*1024) { throw new Error('Generated source definitions суммарно >16MiB.'); }
    let snapshotFiles;
    if (source?.mode === 'files')
    {
        if (!Array.isArray(source.files) || source.files.length !== paths.length) { throw new Error(`Нужны ровно ${paths.length} File snapshots.`); }
        const basenames = new Set(paths.map(path => path.split('/').at(-1)));
        if(basenames.size!==paths.length) { throw new Error('Generated flat snapshots имеют неоднозначные basename; используйте directory handle.'); }
        snapshotFiles = new Map();
        for (const file of source.files)
        {
            if (!file || !basenames.has(file.name) || snapshotFiles.has(file.name) || typeof file.arrayBuffer !== 'function') { throw new Error('Лишний, дублированный или неверный File snapshot.'); }
            snapshotFiles.set(file.name, file);
        }
    }
    else if (source?.mode !== 'directory' || typeof source.handle?.getDirectoryHandle !== 'function') { throw new Error('Неверный read-only источник.'); }
    async function resolveFile(path)
    {
        // Уникальный basename связывает выбранный File только с точным manifest path,
        // а не со всеми source alias-кандидатами с таким же именем.
        if (snapshotFiles) { return paths.includes(path) ? snapshotFiles.get(path.split('/').at(-1)) : undefined; }
        let directory = source.handle;
        const parts = path.split('/');
        for (const part of parts.slice(0, -1)) { directory = await directory.getDirectoryHandle(part); }
        return (await directory.getFileHandle(parts.at(-1))).getFile();
    }
    async function read(file)
    {
        try
        {
            const selected = await resolveFile(file.relativePath);
            if (!selected || selected.size !== file.byteLength) { throw new Error('Размер не совпадает.'); }
            const bytes = await selected.arrayBuffer();
            if (!(bytes instanceof ArrayBuffer) || bytes.byteLength !== file.byteLength || await sha256(bytes) !== file.sha256) { throw new Error('SHA-256/длина не совпадают.'); }
            return bytes;
        }
        catch (error) { throw new Error(`Не удалось проверить локальный файл ${file.relativePath} (${typeof error?.name === 'string' && /^[A-Za-z]+$/.test(error.name) ? error.name : 'ReadError'}).`); }
    }
    const buffers = new Map();
    let count = 0;
    const filesByPath=new Map(checkedManifest.files.map(f=>[f.relativePath,f]));
    async function readPath(path)
    {
        if(buffers.has(path)) { return; }
        const file=filesByPath.get(path); if(!file) { throw new Error(`Source dependency отсутствует в manifest: ${path}.`); }
        buffers.set(path,await read(file)); progress('read',++count,path);
    }
    if(checkedManifest.generatedRequirements)
    {
        for(const path of [...GENERATED_DEFINITION_PATHS,...objectPaths]) { await readPath(path); }
        const meshes=discoverGeneratedMeshPaths(checkedManifest.generatedRequirements.classIds,p=>buffers.get(p),checkedManifest.generatedRequirements.objectIds);
        if(meshes.reduce((sum,p)=>sum+(filesByPath.get(p)?.byteLength??Infinity)*2,0)>maxDecodedBytes) { throw new Error('Generated geometry reserve превышает decoded budget.'); }
        for(const path of meshes) { await readPath(path); }
        const worldFiles=['star_a_class.txt','star_b_class.txt','star_f_class.txt','star_g_class.txt','star_k_class.txt','star_m_class.txt','star_neutron.txt','star_pulsar.txt'].map(p=>'gfx/worldgfx/'+p);
        for(const path of worldFiles.filter(p=>filesByPath.has(p))) { await readPath(path); }
        const preliminary=resolveGeneratedWorldVisuals(checkedManifest.generatedRequirements.classIds,p=>buffers.get(p),{objectIds:checkedManifest.generatedRequirements.objectIds});
        const existing=new Set();
        for(const path of new Set([...preliminary.bindings,...preliminary.objects].flatMap(b=>b.dependencies).filter(p=>p.endsWith('.dds'))))
        {
            const candidates=[path];
            if(path.startsWith('gfx/models/planets/distant_stars_planets/')) { candidates.push('gfx/models/planets/'+path.split('/').at(-1)); }
            for(const candidate of candidates) { try { if(await resolveFile(candidate)) { existing.add(candidate); } } catch { /* Отсутствующая source candidate остаётся явной missing dependency. */ } }
        }
        const graph=resolveGeneratedWorldVisuals(checkedManifest.generatedRequirements.classIds,p=>buffers.get(p),{exists:p=>existing.has(p),objectIds:checkedManifest.generatedRequirements.objectIds});
        const closure=[...new Set([...PROFILE_PATHS,...GENERATED_DEFINITION_PATHS,...objectPaths,...graph.bindings.flatMap(b=>b.dependencies),...graph.objects.flatMap(b=>b.dependencies)])].sort();
        if(closure.join('\n')!==[...paths].sort().join('\n')) { throw new Error('Generated manifest paths не совпадают с независимо разрешённым local source graph.'); }
    }
    for (const file of checkedManifest.files) { await readPath(file.relativePath); }
    progress('decode', count);
    const profile = buildProfile(buffers, { maxDecodedBytes, ...(checkedManifest.generatedRequirements?{generatedRequirements:checkedManifest.generatedRequirements,releaseSourceBuffers:true}:{}) });
    buffers.clear();
    count = 0;
    for (const file of checkedManifest.files) { await read(file); progress('recheck', ++count, file.relativePath); }
    progress('ready', count);
    return { profile, profileDigest: checkedManifest.profileDigest, mode: source.mode === 'files' ? 'immutable-file-snapshots' : 'read-only-directory-rechecked', decodedBytes: profile.decodedBytes, milliseconds: performance.now() - start };
}
