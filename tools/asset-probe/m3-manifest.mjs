import { PROFILE_ID, PROFILE_PATHS } from './m3-profile.mjs';
import { generatedRequirementsCanonicalText, normalizedGeneratedClasses, normalizedGeneratedObjects, generatedVisualId, GENERATED_DEFINITION_PATHS, GENERATED_OBJECT_DEFINITION_PATHS, generatedObjectDefinitionPaths, resolveGeneratedWorldVisuals } from './generated-world-visual-profile.mjs';

export const VISUAL_IDS = Object.freeze(['planet.continental', 'ship.corvette.mammalian', 'star.g', 'station.outpost.mammalian']);
export const MATERIAL_PROFILE = 'm3-static-galaxy-v4';
export const GENERATED_MAP_UNIT_POLICY = 'GeneratedMapUnitSurfaceV1';
const PATHS = [...PROFILE_PATHS].sort();
const MAX_FILE = 64 * 1024 * 1024;
const HASH = /^[0-9a-f]{64}$/;
const fail = message => { throw new Error(`M3 manifest: ${message}`); };
const keys = (object, expected) =>
{
    if (!object || typeof object !== 'object' || Array.isArray(object) || Object.keys(object).sort().join(',') !== [...expected].sort().join(',')) { fail('неверная структура metadata.'); }
};
export function manifestKind(path) { return path.slice(path.lastIndexOf('.') + 1); }
export async function sha256(buffer)
{
    return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', buffer)), byte => byte.toString(16).padStart(2, '0')).join('');
}
export function canonicalManifestText(manifest)
{
    return [manifest.profileId, manifest.gameVersion, manifest.schemaVersion, manifest.importerVersion, manifest.materialProfileVersion].join('\n') + '\n' + [...manifest.files].sort((a, b) => a.relativePath < b.relativePath ? -1 : a.relativePath > b.relativePath ? 1 : 0).map(file => `${file.relativePath}\t${file.byteLength}\t${file.sha256}\n`).join('');
}
export function expectedVisuals()
{
    return VISUAL_IDS.map(visualId => ({ visualId, dependencies: [...PATHS], materialProfile: MATERIAL_PROFILE }));
}
export async function validateM3Manifest(input)
{
    if (input?.generatedRequirements !== undefined) { return validateGeneratedManifestMetadata(input); }
    if (new TextEncoder().encode(JSON.stringify(input)).byteLength > 1024 * 1024) { fail('metadata превышает 1 MiB.'); }
    keys(input, ['schemaVersion', 'profileId', 'profileDigest', 'gameVersion', 'importerVersion', 'materialProfileVersion', 'files', 'visuals']);
    if (input.schemaVersion !== 1 || input.profileId !== PROFILE_ID || input.gameVersion !== '4.5.2' || input.importerVersion !== 9 || input.materialProfileVersion !== 5 || typeof input.profileDigest !== 'string' || !HASH.test(input.profileDigest)) { fail('несовместимые версии или digest.'); }
    if (!Array.isArray(input.files) || input.files.length !== PATHS.length) { fail(`Expected exactly ${PATHS.length} files.`); }
    const seen = new Set();
    const files = input.files.map(file =>
    {
        keys(file, ['relativePath', 'byteLength', 'sha256', 'kind']);
        const path = file.relativePath;
        if (typeof path !== 'string' || !PATHS.includes(path) || seen.has(path)) { fail('неизвестный, дублированный или ненормализованный путь.'); }
        seen.add(path);
        if (!Number.isSafeInteger(file.byteLength) || file.byteLength < 0 || file.byteLength > MAX_FILE || typeof file.sha256 !== 'string' || !HASH.test(file.sha256) || file.kind !== manifestKind(path)) { fail(`неверные length/hash/kind: ${path}.`); }
        return { relativePath: path, byteLength: file.byteLength, sha256: file.sha256, kind: file.kind };
    }).sort((a, b) => a.relativePath < b.relativePath ? -1 : 1);
    if (!Array.isArray(input.visuals) || input.visuals.length !== VISUAL_IDS.length) { fail('нужны четыре visual definitions.'); }
    const visualSeen = new Set();
    for (const visual of input.visuals)
    {
        keys(visual, ['visualId', 'dependencies', 'materialProfile']);
        if (!VISUAL_IDS.includes(visual.visualId) || visualSeen.has(visual.visualId) || visual.materialProfile !== MATERIAL_PROFILE || !Array.isArray(visual.dependencies) || visual.dependencies.length !== PATHS.length || [...visual.dependencies].sort().some((path, index) => path !== PATHS[index])) { fail('visual mapping не соответствует собственному шаблону.'); }
        visualSeen.add(visual.visualId);
    }
    // Собственная копия до await: внешняя мутация metadata не меняет проверенный результат.
    const result = { schemaVersion: 1, profileId: PROFILE_ID, profileDigest: input.profileDigest, gameVersion: '4.5.2', importerVersion: 9, materialProfileVersion: 5, files, visuals: expectedVisuals() };
    if (await sha256(new TextEncoder().encode(canonicalManifestText(result))) !== result.profileDigest) { fail('profileDigest не совпадает.'); }
    return result;
}

export async function createLocalM3Manifest(buffers, { generatedRequirements } = {})
{
    if (generatedRequirements !== undefined)
    {
        const requirements = checkedRequirements(generatedRequirements);
        const graph = resolveGeneratedWorldVisuals(requirements.classIds, p=>buffers.get(p), {exists:p=>buffers.has(p),objectIds:requirements.objectIds});
        const objectDefinitions=requirements.objectIds?generatedObjectDefinitionPaths(requirements.objectIds):[];
        const paths = [...new Set([...PATHS,...GENERATED_DEFINITION_PATHS,...objectDefinitions,...graph.bindings.flatMap(b=>b.dependencies),...graph.objects.flatMap(b=>b.dependencies)])].sort();
        if (!(buffers instanceof Map) || paths.length!==buffers.size || paths.some(p=>!buffers.has(p))) { fail('Local generated buffers не совпадают с source closure.'); }
        const files = [];
        for(const relativePath of paths)
        {
            const data=buffers.get(relativePath); if(!(data instanceof ArrayBuffer)||data.byteLength>MAX_FILE) { fail('Generated source buffer/limit.'); }
            files.push({relativePath,byteLength:data.byteLength,sha256:await sha256(data),kind:manifestKind(relativePath)});
        }
        const profileId=await generatedProfileId(requirements);
        const result={schemaVersion:1,profileId,profileDigest:'',gameVersion:'4.5.2',importerVersion:requirements.objectIds?11:10,materialProfileVersion:7,files,visuals:generatedVisuals(requirements,paths),generatedRequirements:requirements};
        result.profileDigest=await sha256(new TextEncoder().encode(canonicalManifestText(result))); return result;
    }
    if (!(buffers instanceof Map) || buffers.size !== PATHS.length || [...buffers.keys()].some(path => !PATHS.includes(path))) { fail(`Local buffers must match all ${PATHS.length} paths.`); }
    const files = [];
    for (const relativePath of PATHS)
    {
        const data = buffers.get(relativePath);
        if (!(data instanceof ArrayBuffer) || data.byteLength > MAX_FILE) { fail('неверный локальный ArrayBuffer.'); }
        files.push({ relativePath, byteLength: data.byteLength, sha256: await sha256(data), kind: manifestKind(relativePath) });
    }
    const result = { schemaVersion: 1, profileId: PROFILE_ID, profileDigest: '', gameVersion: '4.5.2', importerVersion: 9, materialProfileVersion: 5, files, visuals: expectedVisuals() };
    result.profileDigest = await sha256(new TextEncoder().encode(canonicalManifestText(result)));
    return result;
}

function checkedRequirements(input)
{
    generatedRequirementsCanonicalText(input);
    return Object.freeze({version:1,policy:input.policy,classIds:normalizedGeneratedClasses(input.classIds),...(input.objectIds===undefined?{}:{objectIds:normalizedGeneratedObjects(input.objectIds)})});
}
export async function generatedProfileId(requirements)
{
    return PROFILE_ID+(requirements.objectIds?'-gmo1-':'-gmu1-gw1-')+await sha256(new TextEncoder().encode(generatedRequirementsCanonicalText(requirements)));
}
function generatedVisuals(requirements,paths)
{
    return [...VISUAL_IDS,...requirements.classIds.map(generatedVisualId),...(requirements.objectIds??[]).map(id=>'object.'+id)].sort().map(visualId=>({visualId,dependencies:[...paths],materialProfile:visualId==='object.gateway_ruined'?'generated-object-static-v1/SourceMip512V1/StaticGatewayBindPoseV1/GatewaySurfaceRadius6V1/GatewayOutsideOrbitalEnvelopeV1':visualId==='object.natural_wormhole'?'generated-object-static-v1/WormholeNativeMip0V1/StaticWormholeRingsV1/WormholeSurfaceRadius6V1/WormholeOutsideOrbitalEnvelopeV1':visualId==='world.pc_neutron_star'?`generated-world-static-v1/SourceMip512V1/NeutronNoSurfaceAtmosphereMixV1/SourceClassCommonScaleV2/${GENERATED_MAP_UNIT_POLICY}`:visualId.startsWith('world.')?`generated-world-static-v1/SourceMip512V1/SourceClassCommonScaleV2/${GENERATED_MAP_UNIT_POLICY}`:MATERIAL_PROFILE+'/SourceMip512V1'}));
}
async function validateGeneratedManifestMetadata(input)
{
    if(new TextEncoder().encode(JSON.stringify(input)).byteLength>1024*1024) { fail('metadata превышает 1 MiB.'); }
    keys(input,['schemaVersion','profileId','profileDigest','gameVersion','importerVersion','materialProfileVersion','files','visuals','generatedRequirements']);
    const requirements=checkedRequirements(input.generatedRequirements); const profileId=await generatedProfileId(requirements);
    if(input.schemaVersion!==1||input.profileId!==profileId||input.gameVersion!=='4.5.2'||input.importerVersion!==(requirements.objectIds?11:10)||input.materialProfileVersion!==7||!HASH.test(input.profileDigest)) { fail('Generated version/identity mismatch.'); }
    if(!Array.isArray(input.files)||input.files.length<PATHS.length||input.files.length>1024) { fail('Generated files count.'); }
    const seen=new Set();
    const files=input.files.map(file=>
    {
        keys(file,['relativePath','byteLength','sha256','kind']); const p=file.relativePath;
        const objectDefinitions=requirements.objectIds?generatedObjectDefinitionPaths(requirements.objectIds):[];
        if(typeof p!=='string'||seen.has(p)||/[\\\x00-\x20\x7f:]/.test(p)||p.split('/').some(s=>!s||s==='.'||s==='..')||!PATHS.includes(p)&&!GENERATED_DEFINITION_PATHS.includes(p)&&!objectDefinitions.includes(p)&&!/^gfx\/(models\/planets\/|models\/(nonormal|nospec)\.dds$|worldgfx\/|models\/(ships\/megastructures\/gateway\/|add_ons\/))/.test(p)) { fail('Generated path/duplicate.'); }
        seen.add(p);
        if(!Number.isSafeInteger(file.byteLength)||file.byteLength<0||file.byteLength>MAX_FILE||!HASH.test(file.sha256)||!['dds','mesh','asset','gfx','txt'].includes(file.kind)||file.kind!==manifestKind(p)) { fail('Generated length/hash/kind.'); }
        return {...file};
    }).sort((a,b)=>a.relativePath<b.relativePath?-1:1);
    const paths=files.map(f=>f.relativePath);
    const requiredObjectDefinitions=requirements.objectIds?generatedObjectDefinitionPaths(requirements.objectIds):[];
    if(PATHS.some(p=>!seen.has(p))||GENERATED_DEFINITION_PATHS.some(p=>!seen.has(p))||requiredObjectDefinitions.some(p=>!seen.has(p))) { fail('Generated base/source definitions missing.'); }
    const visuals=generatedVisuals(requirements,paths);
    if(!Array.isArray(input.visuals)||input.visuals.length!==visuals.length) { fail('Generated visual count.'); }
    const actual=[...input.visuals].sort((a,b)=>a.visualId<b.visualId?-1:1);
    for(let i=0;i<visuals.length;i++)
    {
        keys(actual[i],['visualId','dependencies','materialProfile']);
        if(actual[i].visualId!==visuals[i].visualId||actual[i].materialProfile!==visuals[i].materialProfile||!Array.isArray(actual[i].dependencies)||[...actual[i].dependencies].sort().join('\n')!==paths.join('\n')) { fail('Generated visual mapping.'); }
    }
    const result={schemaVersion:1,profileId,profileDigest:input.profileDigest,gameVersion:'4.5.2',importerVersion:requirements.objectIds?11:10,materialProfileVersion:7,files,visuals,generatedRequirements:requirements};
    if(await sha256(new TextEncoder().encode(canonicalManifestText(result)))!==result.profileDigest) { fail('Generated digest mismatch.'); }
    // Paths остаются только metadata до независимого local-source graph resolve в worker.
    return result;
}
