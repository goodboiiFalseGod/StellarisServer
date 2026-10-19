import { parseVisualDefinitions, requireBlock, requireScalar, optionalScalar, selectNamedDefinition } from './visual-definitions.mjs';
import { parsePdxMesh, inspectDds, decodeDds } from './formats.mjs';
import { validateStaticBindPose, validateStaticRestGeometry } from './static-pose.mjs';

// Версионированная политика прототипа; выбор движка среди вариантов не восстановлен.
export const GENERATED_VISUAL_POLICY = 'SourceFirstNumberedEntityV1';
export const GENERATED_RESOURCE_POLICY = 'SourceMip512V1';
export const GENERATED_VISUAL_ERROR_CODES = Object.freeze(['requirements','class-id','class-kind','class-not-requested','source-definition','definition-limit','missing-source','missing-texture','path','texture','scale','override','shader','overlay','entity','mesh','attachment','attachment-transform','attachment-shader','skin-policy','star-world','star-texture','star-color','star-intensity','star-atmosphere','atmosphere-color','lod-policy','lod-unavailable','memory','system-lighting']);
export const GENERATED_DEFINITION_PATHS = Object.freeze([
    'common/planet_classes/00_planet_classes.txt', 'common/star_classes/00_star_classes.txt',
    'gfx/models/planets/_planetary_entities.asset', 'gfx/models/planets/_planetary_meshes.gfx',
    'gfx/models/planets/_star_entities.asset', 'gfx/models/planets/_star_meshes.gfx',
    'gfx/models/planets/distant_stars_planets/_distant_stars_planetary_entities.asset',
    'gfx/models/planets/distant_stars_planets/_distant_stars_planetary_meshes.gfx',
    'gfx/models/planets/distant_stars_planets/_distant_stars_star_entities.asset',
    'gfx/models/planets/distant_stars_planets/_ice_asteroids.gfx',
    'gfx/models/planets/black_hole_new.asset', 'gfx/models/planets/black_hole_new.gfx',
    ...['star_a_class.txt','star_b_class.txt','star_f_class.txt','star_g_class.txt','star_k_class.txt','star_m_class.txt','star_t_class.txt','star_neutron.txt','star_pulsar.txt','star_black_hole.txt'].map(p=>'gfx/worldgfx/'+p),
    'gfx/lights/star_lights.asset'
]);
export const GENERATED_OBJECT_DEFINITION_PATHS = Object.freeze(['common/megastructures/05_gateways.txt','gfx/models/ships/megastructures/gateway/gateway.asset','gfx/models/ships/megastructures/gateway/gateway.gfx']);
export const GENERATED_WORMHOLE_DEFINITION_PATHS = Object.freeze(['gfx/models/add_ons/_add_ons_entities.asset','gfx/models/add_ons/_add_ons_meshes.gfx']);
export function generatedObjectDefinitionPaths(objectIds)
{
    const ids=normalizedGeneratedObjects(objectIds);
    return [...new Set(ids.flatMap(id=>id==='gateway_ruined'?GENERATED_OBJECT_DEFINITION_PATHS:GENERATED_WORMHOLE_DEFINITION_PATHS))].sort();
}
const SHADERS = new Set(['PdxMeshPlanet', 'PdxMeshClouds', 'PdxMeshAtmosphere', 'PdxMeshStar', 'PdxMeshAtmosphereStar', 'PdxMeshPlanetEmissive', 'PdxMeshGasGiant', 'PdxMeshMoltenPlanet', 'PdxMeshAlphaAdditiveAnimateUV', 'PdxMeshAlphaAdditiveAnimateUVNoDissolve', 'PdxMeshBlackHole', 'PdxMeshBlackHoleBillboard', 'PdxMeshAlphaAdditive', 'PdxMeshTerra', 'PdxMeshShip']);
const MAX_BYTES = 512 * 1024 * 1024;
export class GeneratedVisualError extends Error
{
    constructor(code, classId, detail) { super(`Generated visual ${classId}: ${code}: ${detail}`); this.code = code; this.classId = classId; }
}
function reject(code, id, detail) { throw new GeneratedVisualError(code, id, detail); }
export function generatedVisualId(classId)
{
    if (typeof classId !== 'string' || !/^pc_[a-z0-9_]{1,96}$/.test(classId)) { reject('class-id', '', 'Недопустимый source class ID.'); }
    return 'world.' + classId;
}
export function normalizedGeneratedClasses(classIds)
{
    if (!Array.isArray(classIds) || classIds.length < 1 || classIds.length > 128) { reject('requirements', '', 'Нужны 1..128 source classes.'); }
    classIds.forEach(generatedVisualId);
    return Object.freeze([...new Set(classIds)].sort());
}
export function generatedRequirementsCanonicalText(requirements)
{
    if (!requirements || !['classIds,policy,version','classIds,objectIds,policy,version'].includes(Object.keys(requirements).sort().join(',')) || requirements.version !== 1 || requirements.policy !== GENERATED_VISUAL_POLICY) { reject('requirements', '', 'Несовместимая схема.'); }
    const classes = normalizedGeneratedClasses(requirements.classIds);
    if (classes.join('\n') !== requirements.classIds.join('\n')) { reject('requirements', '', 'Нужны sorted unique source classes.'); }
    const base='generated-world-visuals-v1\n' + GENERATED_VISUAL_POLICY + '\n' + classes.map(id=>id+'\n').join('');
    if(requirements.objectIds===undefined) { return base; }
    const objects=normalizedGeneratedObjects(requirements.objectIds);
    return base+'objects-v1\n'+objects.map(id=>id+'\n').join('');
}
export function normalizedGeneratedObjects(objectIds)
{
    if(!Array.isArray(objectIds)||objectIds.length<1||objectIds.length>16||objectIds.some(id=>!['gateway_ruined','natural_wormhole'].includes(id))) { reject('object-requirements','','Only bounded source gateway/wormhole objects are supported.'); }
    const values=[...new Set(objectIds)].sort();
    if(values.length!==objectIds.length) { reject('object-requirements','','Object IDs must be unique.'); }
    return Object.freeze(values);
}

export function discoverGeneratedMeshPaths(classIds, read, objectIds = undefined)
{
    if(GENERATED_DEFINITION_PATHS.reduce((sum,p)=>sum+(read(p)?.byteLength??Infinity),0)>16*1024*1024) { reject('definition-limit','','Source definitions суммарно >16MiB.'); }
    const asts = new Map(GENERATED_DEFINITION_PATHS.map(path=>[path,parseVisualDefinitions(new TextDecoder('utf-8',{fatal:true}).decode(read(path)))]));
    const entities = [...asts].filter(([p])=>p.endsWith('.asset')).flatMap(([path,ast])=>ast.filter(e=>e.key==='entity').map(e=>({path,fields:e.value,name:requireScalar(e.value,'name')})));
    const gfxs = [...asts].filter(([p])=>p.endsWith('.gfx')).flatMap(([path,ast])=>requireBlock(ast,'objectTypes').filter(e=>e.key==='pdxmesh').map(e=>({path,fields:e.value,name:requireScalar(e.value,'name')})));
    const paths = new Set();
    for(const id of normalizedGeneratedClasses(classIds))
    {
        try
        {
        const pc = requireBlock(asts.get(GENERATED_DEFINITION_PATHS[0]),id); const base = requireScalar(pc,'entity');
        const exact = entities.filter(e=>e.name===base); const selected = exact.length?exact:entities.filter(e=>e.name===base+'_01_entity');
        if(selected.length!==1) { reject('entity',id,'Отсутствующий/неоднозначный source entity.'); }
        const names = [selected[0].name];
        if(id==='pc_black_hole')
        {
            const root=selected[0];
            const locators=root.fields.filter(entry=>entry.key==='locator');
            const expected=['black_hole_rings_entity','black_hole_grav_lens_entity','black_hole_grav_animation_entity'];
            const attachments=root.fields.filter(entry=>entry.key==='attach');
            if(root.name!=='black_hole_entity'||requireScalar(root.fields,'pdxmesh')!=='black_hole_new_mesh'||locators.length!==1||locators[0].operator!=='='||!Array.isArray(locators[0].value)||locators[0].value.length!==1||locators[0].value[0].key!=='name'||locators[0].value[0].value!=='planet'||attachments.length!==expected.length)
            { reject('attachment',id,'Неизвестная source-форма корня или locator чёрной дыры.'); }
            attachments.forEach((entry,index)=>
            {
                if(entry.operator!=='='||!Array.isArray(entry.value)||entry.value.length!==1||entry.value[0].key!=='planet'||entry.value[0].value!==expected[index])
                { reject('attachment',id,'Изменился порядок или locator attachment чёрной дыры.'); }
                names.push(expected[index]);
            });
        }
        for(const attach of selected[0].fields.filter(e=>e.key==='attach'))
        {
            if(id==='pc_black_hole') { continue; }
            if(!['pc_neutron_star','pc_pulsar'].includes(id)||!Array.isArray(attach.value)||attach.value.length!==1||attach.value[0].key!=='planet') { reject('attachment',id,'Неподдержанный source attachment.'); }
            names.push(attach.value[0].value);
        }
        for(const name of names)
        {
            const entity=entities.filter(e=>e.name===name); if(entity.length!==1) { reject('entity',id,name); }
            const meshName=requireScalar(entity[0].fields,'pdxmesh'); const gfx=gfxs.filter(g=>g.name===meshName); if(gfx.length!==1) { reject('mesh',id,meshName); }
            const path=relativePath(requireScalar(gfx[0].fields,'file'),id); if(!path.endsWith('.mesh')) { reject('mesh',id,path); } paths.add(path);
            if(id==='pc_black_hole'&&name!=='black_hole_entity')
            {
                const expected={black_hole_rings_entity:['black_hole_rings_mesh','gfx/models/planets/black_hole_rings.mesh'],black_hole_grav_lens_entity:['black_hole_grav_lens_mesh','gfx/models/planets/black_hole_grav_lens.mesh'],black_hole_grav_animation_entity:['black_hole_grav_animation_mesh','gfx/models/planets/black_hole_grav_animation.mesh']}[name];
                if(!expected||meshName!==expected[0]||path!==expected[1]) { reject('attachment',id,'Изменилась source mesh-связь attachment чёрной дыры.'); }
            }
        }
        }
        catch(error) { if(error instanceof GeneratedVisualError)throw error; reject('source-definition',id,error.message); }
    }
    paths.add(resolveRingDefinitions(asts).meshPath);
    if(objectIds!==undefined)
    {
        for(const id of normalizedGeneratedObjects(objectIds)) for(const path of id==='gateway_ruined'?['gfx/models/ships/megastructures/gateway/gateway.mesh']:['gfx/models/add_ons/wormhole_rings.mesh','gfx/models/add_ons/wormhole_stretched_ring.mesh']) paths.add(path);
    }
    return [...paths].sort();
}

export function resolveGeneratedWorldObjects(objectIds, read, { exists, maxDefinitionBytes = 16*1024*1024, parsedMeshes = new Map() } = {})
{
    const ids=normalizedGeneratedObjects(objectIds); const bindings=[]; let definitionBytes=0;
    const buffer=path=>{const value=read(path);if(!(value instanceof ArrayBuffer)||value.byteLength>64*1024*1024){reject('missing-source','gateway_ruined',path);}return value;};
    const parse=(path)=>{const source=buffer(path);definitionBytes+=source.byteLength;if(definitionBytes>maxDefinitionBytes){reject('definition-limit','gateway_ruined','Generated source definitions exceed16MiB.');}return parseVisualDefinitions(new TextDecoder('utf-8',{fatal:true}).decode(source));};
    for(const objectId of ids)
    {
        try
        {
            if(objectId==='natural_wormhole')
            {
                const [assetPath,gfxPath]=GENERATED_WORMHOLE_DEFINITION_PATHS;
                const rootPath='gfx/models/add_ons/wormhole_rings.mesh',childPath='gfx/models/add_ons/wormhole_stretched_ring.mesh';
                const asset=parse(assetPath),gfxAst=parse(gfxPath);
                const rootEntity=selectNamedDefinition(asset,'entity','natural_wormhole_entity');
                const childEntity=selectNamedDefinition(asset,'entity','natural_wormhole_stretched_ring_entity');
                const scale=fields=>{const values=fields.filter(e=>e.key==='scale');if(values.length!==1||values[0].operator!=='=') {reject('wormhole-entity',objectId,'Expected one source scale.');}const value=Number(requireScalar([values[0]],'scale'));if(!Number.isFinite(value)||value<=0)return reject('wormhole-entity',objectId,'Invalid source scale.');return value;};
                const attachments=rootEntity.filter(e=>e.key==='attach');
                const forbiddenTransform=fields=>fields.some(e=>['position','rotation','orientation','quaternion','locator'].includes(e.key));
                if(requireScalar(rootEntity,'pdxmesh')!=='wormhole_rings_mesh'||requireScalar(rootEntity,'default_state')!=='start'||scale(rootEntity)!==12||
                    requireScalar(childEntity,'pdxmesh')!=='wormhole_stretched_ring_mesh'||requireScalar(childEntity,'default_state')!=='start'||scale(childEntity)!==1||
                    forbiddenTransform(rootEntity)||forbiddenTransform(childEntity)||childEntity.some(e=>e.key==='attach')||
                    attachments.length!==1||attachments[0].operator!=='='||!Array.isArray(attachments[0].value)||attachments[0].value.length!==1||attachments[0].value[0].key!=='root'||attachments[0].value[0].operator!=='='||attachments[0].value[0].value!=='natural_wormhole_stretched_ring_entity')
                    {reject('wormhole-entity',objectId,'Expected exact root and attached child entities/transforms.');}
                const meshes=requireBlock(gfxAst,'objectTypes').filter(e=>e.key==='pdxmesh');
                const gfxFor=(name,path)=>{const matches=meshes.filter(e=>requireScalar(e.value,'name')===name);if(matches.length!==1) {reject('wormhole-mesh',objectId,name);}const fields=matches[0].value;if(requireScalar(fields,'file')!==path||Number(optionalScalar(fields,'scale',1))!==1||forbiddenTransform(fields)||fields.some(e=>['attach','locator'].includes(e.key))) {reject('wormhole-mesh',objectId,'Source mesh path/transform changed.');}const settings=fields.filter(e=>e.key==='meshsettings');if(settings.length!==1||settings[0].operator!=='='||!Array.isArray(settings[0].value)||settings[0].value.length!==1||requireScalar(settings[0].value,'shader')!=='PdxMeshAlphaAdditiveAnimateUVNoDissolve') {reject('wormhole-material',objectId,'NoDissolve override changed.');}return fields;};
                const rootGfx=gfxFor('wormhole_rings_mesh',rootPath),childGfx=gfxFor('wormhole_stretched_ring_mesh',childPath);
                if(!parsedMeshes.has(rootPath)) { parsedMeshes.set(rootPath,parsePdxMesh(buffer(rootPath))); }
                if(!parsedMeshes.has(childPath)) { parsedMeshes.set(childPath,parsePdxMesh(buffer(childPath))); }
                const rootParsed=parsedMeshes.get(rootPath),childParsed=parsedMeshes.get(childPath);
                try{validateStaticBindPose(rootParsed);validateStaticBindPose(childParsed);}catch(error){reject('wormhole-pose',objectId,error.message);}
                const rootAnchors=[];
                const findRootAnchor=node=>
                {
                    if(node.name==='root') rootAnchors.push(node);
                    node.children.forEach(findRootAnchor);
                };
                rootParsed.nodes.forEach(findRootAnchor);
                if(rootAnchors.length!==1||Object.keys(rootAnchors[0]?.properties??{}).sort().join(',')!=='p,q'||rootAnchors[0].children.length!==0||
                    !(rootAnchors[0].properties.p instanceof Float32Array)||!(rootAnchors[0].properties.q instanceof Float32Array)||
                    Array.from(rootAnchors[0].properties.p).join(',')!=='0,0,0'||Array.from(rootAnchors[0].properties.q).join(',')!=='0,0,0,-1')
                    { reject('wormhole-attachment',objectId,'Expected one source root locator at identity transform.'); }
                if(rootParsed.meshes.map(m=>m.name).sort().join(',')!=='above1Shape,above2Shape'||childParsed.meshes.map(m=>m.name).join(',')!=='above3Shape') {reject('wormhole-mesh',objectId,'Expected source ring surface layers.');}
                const rootLayers=materialMeshes(rootParsed,[{fields:rootGfx,path:gfxPath}],rootPath.slice(0,rootPath.lastIndexOf('/')+1),objectId);
                const childLayers=materialMeshes(childParsed,[{fields:childGfx,path:gfxPath}],childPath.slice(0,childPath.lastIndexOf('/')+1),objectId);
                if(rootLayers.some(m=>m.material.shader!=='PdxMeshAlphaAdditiveAnimateUVNoDissolve'||m.material.diff!=='gfx/models/add_ons/wormhole_rings.dds'||m.material.spec!=='gfx/models/nospec.dds'||m.material.n!==undefined||Object.keys(m.material).some(key=>!['shader','diff','spec'].includes(key)))||
                    childLayers.some(m=>m.material.shader!=='PdxMeshAlphaAdditiveAnimateUVNoDissolve'||m.material.diff!=='gfx/models/add_ons/wormhole_stretch.dds'||m.material.spec!=='gfx/models/nospec.dds'||m.material.n!==undefined||Object.keys(m.material).some(key=>!['shader','diff','spec'].includes(key)))) {reject('wormhole-material',objectId,'Source shader/texture mapping changed.');}
                const dependencies=[...new Set([assetPath,gfxPath,rootPath,childPath,'gfx/models/add_ons/wormhole_rings.dds','gfx/models/add_ons/wormhole_stretch.dds','gfx/models/nospec.dds',...rootLayers.flatMap(m=>[m.material.diff,m.material.n,m.material.spec]),...childLayers.flatMap(m=>[m.material.diff,m.material.n,m.material.spec])].filter(Boolean))].sort();
                for(const path of dependencies.filter(p=>p.endsWith('.dds'))) {if(exists&&!exists(path)) {reject('missing-texture',objectId,path);}}
                bindings.push({objectId,visualId:'object.'+objectId,kind:'object',entityName:'natural_wormhole_entity',entityPath:assetPath,meshName:'wormhole_rings_mesh',meshPath:rootPath,gfxPath,scale:12,meshes:rootLayers,
                    attachments:[{entityName:'natural_wormhole_stretched_ring_entity',meshName:'wormhole_stretched_ring_mesh',meshPath:childPath,meshes:childLayers,matrix:[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],scale:1,policy:'StaticWormholeRingsV1'}],dependencies,staticPose:true,approximationPolicy:'StaticWormholeRingsV1'});
                continue;
            }
            const sourcePath=GENERATED_OBJECT_DEFINITION_PATHS[0],assetPath=GENERATED_OBJECT_DEFINITION_PATHS[1],gfxPath=GENERATED_OBJECT_DEFINITION_PATHS[2],meshPath='gfx/models/ships/megastructures/gateway/gateway.mesh';
            const source=parse(sourcePath),asset=parse(assetPath),gfxAst=parse(gfxPath);
            const definition=requireBlock(source,'gateway_ruined');
            if(requireScalar(definition,'entity')!=='deactivated_gateway_entity'||requireScalar(definition,'build_type')!=='outside_gravity_well'||requireScalar(definition,'place_entity_on_planet_plane')!=='no'||requireScalar(definition,'show_galactic_map_icon')!=='no'||requireScalar(definition,'bypass_type')!=='gateway') { reject('gateway-definition',objectId,'Source megastructure definition changed.'); }
            const potential=requireBlock(definition,'potential');
            if(potential.length!==1||requireScalar(potential,'always')!=='no') { reject('gateway-definition',objectId,'gateway_ruined must remain unavailable.'); }
            const expectedAnimations={gateway_idle_animation:'gateway_idle.anim',gateway_death_animation:'gateway_death.anim'};
            const animationDefinitions=asset.filter(e=>e.key==='animation');
            const animationNames=animationDefinitions.map(e=>requireScalar(e.value,'name'));
            if(animationDefinitions.length!==2||new Set(animationNames).size!==2||[...animationNames].sort().join(',')!==Object.keys(expectedAnimations).sort().join(',')||animationDefinitions.some(e=>e.operator!=='='||!Array.isArray(e.value)||!Object.hasOwn(expectedAnimations,requireScalar(e.value,'name'))||expectedAnimations[requireScalar(e.value,'name')]!==requireScalar(e.value,'file'))) { reject('gateway-animation',objectId,'Animation file metadata changed.'); }
            const entity=selectNamedDefinition(asset,'entity','deactivated_gateway_entity');
            const hasUnsupportedTransform=fields=>{const scales=fields.filter(e=>e.key==='scale');return fields.some(e=>['position','rotation','orientation','quaternion'].includes(e.key))||scales.length>1||scales.some(e=>e.operator!=='='||Number(requireScalar([e],'scale'))!==1);};
            if(requireScalar(entity,'pdxmesh')!=='gateway_mesh'||requireScalar(entity,'default_state')!=='idle'||entity.some(e=>['attach','locator'].includes(e.key))||hasUnsupportedTransform(entity)) { reject('gateway-entity',objectId,'Unsupported entity binding, transform, or attachment.'); }
            const states=entity.filter(e=>e.key==='state').map(e=>e.value).filter(fields=>requireScalar(fields,'name')==='idle');
            if(states.length!==1||requireScalar(states[0],'animation')!=='idle'||requireScalar(states[0],'animation_speed')!==0) { reject('gateway-animation',objectId,'Idle state must be static at speed 0.'); }
            const deaths=entity.filter(e=>e.key==='state').map(e=>e.value).filter(fields=>requireScalar(fields,'name')==='death');
            if(deaths.length!==1||requireScalar(deaths[0],'animation')!=='death') { reject('gateway-animation',objectId,'Expected disabled death animation reference.'); }
            const entityMeshes=entity.filter(e=>e.key==='meshsettings');
            if(entityMeshes.length!==1||requireScalar(entityMeshes[0].value,'name')!=='gateway_bottomShapeShape'||requireScalar(entityMeshes[0].value,'index')!==0||requireScalar(entityMeshes[0].value,'shader')!=='PdxMeshShip') { reject('gateway-material',objectId,'Unexpected deactivated mesh binding.'); }
            const values=Object.fromEntries(['diffuse','normal','specular'].map(channel=>[channel,requireScalar(entityMeshes[0].value,'texture_'+channel)]));
            const expected={diffuse:'fungoid_01_gateway_diffuse.dds',normal:'fungoid_01_gateway_normal.dds',specular:'fungoid_01_gateway_specular.dds'};
            for(const [channel,file] of Object.entries(expected)) { if(values[channel]!==file) { reject('gateway-texture',objectId,channel); } }
            const objectTypes=requireBlock(gfxAst,'objectTypes');
            const gfx=selectNamedDefinition(objectTypes,'pdxmesh','gateway_mesh');
            const animationRefs=gfx.filter(e=>e.key==='animation').map(e=>e.value);
            if(requireScalar(gfx,'file')!==meshPath||hasUnsupportedTransform(gfx)||animationRefs.length!==2||!animationRefs.some(a=>requireScalar(a,'id')==='idle'&&requireScalar(a,'type')==='gateway_idle_animation')||!animationRefs.some(a=>requireScalar(a,'id')==='death'&&requireScalar(a,'type')==='gateway_death_animation')) { reject('gateway-mesh',objectId,'Unexpected source mesh, transform or animation references.'); }
            const parsed=parsePdxMesh(buffer(meshPath));
            if(parsed.meshes.length!==1||parsed.meshes[0].name!=='gateway_bottomShapeShape'||parsed.meshes[0].material.shader!=='PdxMeshShip') { reject('gateway-mesh',objectId,'Expected exact static gateway surface layer.'); }
            try { validateStaticBindPose(parsed); } catch(error) { reject('gateway-pose',objectId,error.message); }
            const meshDefinitions=[{fields:gfx,path:gfxPath},{fields:entity,path:assetPath}];
            const meshes=materialMeshes(parsed,meshDefinitions,'gfx/models/ships/megastructures/gateway/',objectId);
            if(meshes.length!==1||meshes[0].material.shader!=='PdxMeshShip') { reject('gateway-material',objectId,'Material binding changed.'); }
            const texturePaths=Object.values(expected).map(file=>'gfx/models/ships/megastructures/gateway/'+file);
            for(const path of texturePaths) { if(exists&&!exists(path)) { reject('missing-texture',objectId,path); } }
            const dependencies=[...new Set([sourcePath,assetPath,gfxPath,meshPath,...texturePaths])].sort();
            bindings.push({objectId,visualId:'object.'+objectId,kind:'object',entityName:'deactivated_gateway_entity',entityPath:assetPath,meshName:'gateway_mesh',meshPath,gfxPath,scale:1,meshes,attachments:[],dependencies,staticPose:true,approximationPolicy:'StaticGatewayBindPoseV1'});
        }
        catch(error) { if(error instanceof GeneratedVisualError)throw error; reject('gateway-source-definition',objectId,error.message); }
    }
    return bindings;
}

function resolveRingDefinitions(asts)
{
    const id='rings';
    const select=(suffix,key,name)=>[...asts].filter(([p])=>p.endsWith(suffix)).flatMap(([path,root])=>(key==='entity'?root:requireBlock(root,'objectTypes')).filter(e=>e.key===key&&requireScalar(e.value,'name')===name).map(e=>({path,fields:e.value})));
    const entities=select('.asset','entity','gas_giant_rings_01_entity'),gfxs=select('.gfx','pdxmesh','gas_giant_rings_01_mesh');
    if(entities.length!==1||gfxs.length!==1) { reject('attachment',id,'Неоднозначный source ring entity/mesh.'); }
    const entity=entities[0],gfx=gfxs[0],meshPath='gfx/models/planets/gas_giant_rings_01.mesh';
    if(requireScalar(entity.fields,'pdxmesh')!=='gas_giant_rings_01_mesh'||requireScalar(gfx.fields,'file')!==meshPath||entity.fields.some(e=>['attach','locator','meshsettings'].includes(e.key))||gfx.fields.some(e=>e.key==='meshsettings')) { reject('attachment',id,'Неподдержанный source ring binding.'); }
    return {entity,gfx,meshPath};
}

export function parseGeneratedSystemLighting(read)
{
    try
    {
    const parse=path=>parseVisualDefinitions(new TextDecoder('utf-8',{fatal:true}).decode(read(path)));
    const lights=parse('gfx/lights/star_lights.asset'); const worlds=new Map();
    const expected={a_star:'star_a_class.txt',b_star:'star_b_class.txt',f_star:'star_f_class.txt',g_star:'star_g_class.txt',k_star:'star_k_class.txt',m_star:'star_m_class.txt',t_star:'star_t_class.txt',neutron_star:'star_neutron.txt',pulsar:'star_pulsar.txt',black_hole:'star_black_hole.txt'};
    for(const [world,file] of Object.entries(expected))
    {
        const fields=requireBlock(parse('gfx/worldgfx/'+file),'gfx_settings');
        if(requireScalar(fields,'world')!==world) { reject('system-lighting',world,'worldgfx tag mismatch.'); }
        const lightName=requireScalar(fields,'system_light'); const light=selectNamedDefinition(lights,'light',lightName);
        const color=requireBlock(light,'color').filter(e=>['r','g','b'].includes(e.key));
        const rgb=['r','g','b'].map(key=>{const v=requireBlock(color,key);if(v.length!==1||v[0].key!==null||!Number.isFinite(v[0].value)||v[0].value<0) { reject('system-lighting',world,'RGB.'); }return v[0].value;});
        const position=requireBlock(light,'position'); const xyz=['x','y','z'].map(k=>requireScalar(position,k));
        const intensity=requireScalar(light,'intensity'),radius=requireScalar(light,'radius'),falloff=requireScalar(light,'falloff');
        if(!xyz.every(Number.isFinite)||![intensity,radius,falloff].every(v=>Number.isFinite(v)&&v>=0)) { reject('system-lighting',world,'Intensity/position/radius/falloff.'); }
        worlds.set(world,{world,lightName,color:rgb,intensity,position:xyz,radius,falloff,conversionPolicy:'SourceLightThreeCalibrationV1'});
    }
    const result=new Map();
    for(const entry of parse('common/star_classes/00_star_classes.txt').filter(e=>e.key?.startsWith('sc_')))
    {
        if(entry.operator!=='='||!Array.isArray(entry.value)) { reject('system-lighting',entry.key,'Source definition.'); }
        const world=requireScalar(entry.value,'class');
        if(worlds.has(world)) { if(result.has(entry.key)) { reject('system-lighting',entry.key,'Duplicate star class.'); } result.set(entry.key,{...worlds.get(world),starClassId:entry.key}); }
    }
    return result;
    }
    catch(error) { if(error instanceof GeneratedVisualError)throw error; reject('system-lighting','',error.message); }
}
function relativePath(path, id)
{
    if (typeof path !== 'string' || !path.startsWith('gfx/') || /[\\\x00-\x20\x7f:]/.test(path) || path.split('/').some(p => !p || p === '.' || p === '..')) { reject('path', id, 'Недопустимая source dependency.'); }
    return path;
}
function texturePath(value, folder, id)
{
    const path=relativePath(value.includes('/') ? value : ['nospec.dds', 'nonormal.dds'].includes(value) ? 'gfx/models/' + value : folder + value, id);
    if(!path.endsWith('.dds')) { reject('texture',id,'Texture extension.'); } return path;
}
function positiveScale(value, id)
{
    if (!Number.isFinite(value) || value <= 0) { reject('scale', id, 'Неподдержанный entity/mesh scale.'); }
    return value;
}
function materialMeshes(parsed, definitions, folder, id)
{
    const meshes = parsed.meshes.map((mesh, meshIndex) => ({ ...mesh, meshIndex, material: { ...mesh.material } }));
    for (const mesh of meshes) { for (const c of ['diff', 'n', 'spec']) { if (mesh.material[c]) { mesh.material[c] = texturePath(mesh.material[c], folder, id); } } }
    for (const { fields: definition, path } of definitions)
    {
        const used = new Set();
        for (const entry of definition.filter(e => e.key === 'meshsettings'))
        {
            if (entry.operator !== '=' || !Array.isArray(entry.value) || entry.value.some(e => !['name', 'index', 'shader', 'texture_diffuse', 'texture_normal', 'texture_specular'].includes(e.key))) { reject('override', id, 'Неподдержанный meshsettings.'); }
            const name = optionalScalar(entry.value, 'name');
            const index = optionalScalar(entry.value, 'index');
            if (index !== undefined && (!Number.isSafeInteger(index) || index < 0)) { reject('override', id, 'Некорректный mesh index.'); }
            const key = name + ':' + index;
            if (used.has(key)) { reject('override', id, 'Повторный override.'); }
            used.add(key);
            const named = name === undefined ? meshes : meshes.filter(m => m.name === name || m.name.endsWith('|' + name));
            const selected = index === undefined ? named : named.slice(index, index + 1);
            if (!selected.length) { reject('override', id, `Неизвестный shape ${name}.`); }
            for (const [field, property] of [['shader', 'shader'], ['texture_diffuse', 'diff'], ['texture_normal', 'n'], ['texture_specular', 'spec']])
            {
                const value = optionalScalar(entry.value, field);
                if (value !== undefined) { selected.forEach(m => { m.material[property] = property === 'shader' ? value : texturePath(value, path.slice(0, path.lastIndexOf('/') + 1), id); }); }
            }
        }
    }
    for (const mesh of meshes)
    {
        if (!SHADERS.has(mesh.material.shader)) { reject('shader', id, mesh.material.shader); }
        for (const channel of ['diff', 'n', 'spec'])
        {
            const value = mesh.material[channel];
            if (value !== undefined) { if (typeof value !== 'string') { reject('texture', id, channel); } mesh.material[channel] = texturePath(value, folder, id); }
            else if (!mesh.material.shader.startsWith('PdxMeshAtmosphere') && mesh.material.shader !== 'PdxMeshStar' && !(mesh.material.shader.startsWith('PdxMeshAlphaAdditive') && channel !== 'diff') && !(mesh.material.shader==='PdxMeshBlackHoleBillboard'&&channel!=='diff')) { reject('texture', id, channel); }
        }
    }
    return meshes;
}

// Читатель предоставляется локальным import owner. Серверные URL/bytes здесь отсутствуют.
export function resolveGeneratedWorldVisuals(classIds, read, { exists, objectIds } = {})
{
    const classes = normalizedGeneratedClasses(classIds);
    const asts = new Map(); const parsedMeshes = new Map(); let definitionBytes=0;
    const buffer = path =>
    {
        const value = read(path);
        if (!(value instanceof ArrayBuffer) || value.byteLength > 64 * 1024 * 1024) { reject('missing-source', '', path); }
        return value;
    };
    const ast = path =>
    {
        if (!asts.has(path))
        {
            const source=buffer(path); definitionBytes+=source.byteLength;
            if(definitionBytes>16*1024*1024) { reject('definition-limit','','Source definitions суммарно >16MiB.'); }
            asts.set(path, parseVisualDefinitions(new TextDecoder('utf-8', { fatal: true }).decode(source)));
        }
        return asts.get(path);
    };
    const planetClasses = ast(GENERATED_DEFINITION_PATHS[0]);
    const entityPaths = GENERATED_DEFINITION_PATHS.filter(p => p.endsWith('.asset'));
    const gfxPaths = GENERATED_DEFINITION_PATHS.filter(p => p.endsWith('.gfx'));
    const bindings = [];
    for (const classId of classes)
    {
        try
        {
            const pc = requireBlock(planetClasses, classId);
            if (optionalScalar(pc, 'overlay_entity') !== undefined) { reject('overlay', classId, 'Нужен отдельный проверенный adapter.'); }
            const base = requireScalar(pc, 'entity');
            const star = optionalScalar(pc, 'star') === 'yes';
            const exactEntities = []; const numbered = [];
            for (const path of entityPaths)
            {
                for (const entry of ast(path).filter(e => e.key === 'entity'))
                {
                    const name = requireScalar(entry.value, 'name');
                    if (name === base) { exactEntities.push({ path, name, fields: entry.value }); }
                    if (name === base + '_01_entity') { numbered.push({ path, name, fields: entry.value }); }
                }
            }
            const candidates = exactEntities.length ? exactEntities : numbered;
            if (candidates.length !== 1) { reject('entity', classId, `Найдено ${candidates.length} exact/01 definitions.`); }
            const entity = candidates[0];
            const nativeAttachments = entity.fields.filter(e => e.key === 'attach');
            const expectedAttachment = { pc_neutron_star: ['neutron_outbursts_entity'], pc_pulsar: ['pulsar_outbursts_entity'],
                pc_black_hole: ['black_hole_rings_entity','black_hole_grav_lens_entity','black_hole_grav_animation_entity'] }[classId] ?? [];
            if (classId === 'pc_black_hole')
            {
                const locators=entity.fields.filter(e=>e.key==='locator');
                if(entity.name!=='black_hole_entity'||requireScalar(entity.fields,'pdxmesh')!=='black_hole_new_mesh'||locators.length!==1||locators[0].operator!=='='||!Array.isArray(locators[0].value)||locators[0].value.length!==1||locators[0].value[0].key!=='name'||locators[0].value[0].value!=='planet'||nativeAttachments.length!==3) { reject('attachment',classId,'Неизвестный locator или набор attachments чёрной дыры.'); }
            }
            else if (entity.fields.some(e => e.key === 'locator') || nativeAttachments.length && (!expectedAttachment.length || nativeAttachments.length !== 1)) { reject('attachment', classId, entity.name); }
            const meshName = requireScalar(entity.fields, 'pdxmesh');
            const meshes = [];
            for (const path of gfxPaths)
            {
                const fields = requireBlock(ast(path), 'objectTypes');
                for (const entry of fields.filter(e => e.key === 'pdxmesh'))
                {
                    if (requireScalar(entry.value, 'name') === meshName) { meshes.push({ path, fields: entry.value }); }
                }
            }
            if (meshes.length !== 1) { reject('mesh', classId, `Найдено ${meshes.length} mesh definitions.`); }
            const gfx = meshes[0]; const meshPath = relativePath(requireScalar(gfx.fields, 'file'), classId);
            if (!meshPath.endsWith('.mesh')) { reject('mesh', classId, meshPath); }
            if (!parsedMeshes.has(meshPath)) { parsedMeshes.set(meshPath, parsePdxMesh(buffer(meshPath))); }
            const parsed = parsedMeshes.get(meshPath);
            if (!parsed.meshes.length) { reject('mesh', classId, 'Нет render layers.'); }
            let poseReport;
            let approximationPolicy;
            if(classId==='pc_broken')
            {
                if(entity.name!=='broken_planet_01_entity'||meshName!=='barren_planet_01_destroyed_mesh'||meshPath!=='gfx/models/planets/barren_planet_01_destroyed.mesh'||parsed.meshes.length!==4||parsed.unsupportedFeatures.length!==2||!parsed.unsupportedFeatures.includes('skin')||!parsed.unsupportedFeatures.includes('skeleton')||parsed.meshes.some(mesh=>!mesh.skinned)) { reject('skin-policy',classId,'Изменился source rest-geometry разрушенной планеты.'); }
                const expectedShaders=['PdxMeshPlanet','PdxMeshClouds','PdxMeshTerra','PdxMeshTerra'];
                if(parsed.meshes.some((mesh,index)=>mesh.material.shader!==expectedShaders[index])) { reject('skin-policy',classId,'Изменился source layer order разрушенной планеты.'); }
                let rest;
                try { rest=validateStaticRestGeometry(parsed,{expectedBonesPerVertex:2,expectedSkins:4}); }
                catch(error) { reject('skin-policy',classId,error.message); }
                poseReport={layers:parsed.meshes.map(mesh=>({name:mesh.name,staticRestGeometry:true})),skinnedLayerCount:rest.skinnedLayerCount,skeletonCount:rest.skeletonCount};
                approximationPolicy='StaticBrokenPlanetRestGeometryV1: используется исходная rest geometry; анимация разрушения и particle events не исполняются.';
            }
            else
            {
                const pose=validateStaticBindPose(parsed);
                poseReport={layers:pose.layers,skinnedLayerCount:pose.skinnedLayerCount,skeletonCount:pose.skeletonCount};
            }
            const folder = meshPath.slice(0, meshPath.lastIndexOf('/') + 1);
            const materialLayers = materialMeshes(parsed, [gfx, entity], folder, classId);
            let sourceAtmosphereColor;
            if(pc.some(e=>e.key==='atmosphere_color'))
            {
                const value=requireScalar(pc,'atmosphere_color'); const triple=pc[pc.findIndex(e=>e.key==='atmosphere_color')+1];
                if(value!=='hsv'||triple?.key!==null||!Array.isArray(triple.value)||triple.value.length!==3||triple.value.some(e=>e.key!==null||!Number.isFinite(e.value))) { reject('atmosphere-color',classId,'Source HSV.'); }
                sourceAtmosphereColor=triple.value.map(e=>e.value);
            }
            if (exists)
            {
                for (const mesh of materialLayers) { for (const channel of ['diff', 'n', 'spec'])
                {
                    const path = mesh.material[channel];
                    if (!path || exists(path)) { continue; }
                    // Варианты distant_stars ссылаются на общие planetary textures basename.
                    const shared = 'gfx/models/planets/' + path.split('/').at(-1);
                    if (path.startsWith('gfx/models/planets/distant_stars_planets/') && exists(shared)) { mesh.material[channel] = shared; }
                    else { reject('missing-texture', classId, path); }
                } }
            }
            if (!star && materialLayers.some(m => m.material.shader === 'PdxMeshStar')) { reject('class-kind', classId, 'Star shader у planet class.'); }
            const dependencies = new Set([GENERATED_DEFINITION_PATHS[0], entity.path, gfx.path, meshPath]);
            if(classId==='pc_black_hole')
            {
                ['common/star_classes/00_star_classes.txt','gfx/worldgfx/star_black_hole.txt','gfx/lights/star_lights.asset'].forEach(path=>dependencies.add(path));
                approximationPolicy='StaticBlackHoleAttachmentsV1: source mesh и attachments статичны; distortion/lensing, UV motion и анимационные события не воспроизводятся.';
            }
            materialLayers.forEach(m => ['diff', 'n', 'spec'].forEach(c => { if (m.material[c]) { dependencies.add(m.material[c]); } }));
            let starSurface;
            if (materialLayers.some(m => m.material.shader === 'PdxMeshStar'))
            {
                const worlds = new Set();
                for (const entry of ast(GENERATED_DEFINITION_PATHS[1]).filter(e => e.key?.startsWith('sc_')))
                {
                    const planets = entry.value.filter(e => e.key === 'planet');
                    if (planets.length === 1 && requireScalar(planets[0].value, 'key') === classId) { worlds.add(requireScalar(entry.value, 'class')); }
                }
                if (worlds.size !== 1) { reject('star-world', classId, 'Неоднозначная source lighting family.'); }
                const world = [...worlds][0];
                const file = { a_star:'star_a_class.txt', b_star:'star_b_class.txt', f_star:'star_f_class.txt', g_star:'star_g_class.txt', k_star:'star_k_class.txt', m_star:'star_m_class.txt', neutron_star:'star_neutron.txt', pulsar:'star_pulsar.txt' }[world];
                if (!file) { reject('star-world', classId, world); }
                const path = 'gfx/worldgfx/' + file;
                starSurface = generatedStarSurface(requireBlock(ast(path), 'gfx_settings'), pc, world, classId);
                [GENERATED_DEFINITION_PATHS[1], path, starSurface.noisePath, starSurface.lavaPath, starSurface.stonePath].forEach(p => dependencies.add(p));
            }
            const attachments = [];
            if (nativeAttachments.length)
            {
                for(let index=0;index<nativeAttachments.length;index++)
                {
                    const attach = nativeAttachments[index];
                    if (attach.operator !== '=' || !Array.isArray(attach.value) || attach.value.length !== 1 || attach.value[0].key !== 'planet' || attach.value[0].operator !== '=' || attach.value[0].value !== expectedAttachment[index]) { reject('attachment', classId, 'Неизвестная attachment binding.'); }
                    const fields = selectNamedDefinition(ast(entity.path), 'entity', expectedAttachment[index]);
                    if (fields.some(e => ['attach', 'locator'].includes(e.key))) { reject('attachment', classId, 'Вложенный attachment.'); }
                    const childMeshName = requireScalar(fields, 'pdxmesh');
                    const childGfx = selectNamedDefinition(requireBlock(ast(gfx.path), 'objectTypes'), 'pdxmesh', childMeshName);
                    const childPath = relativePath(requireScalar(childGfx, 'file'), classId);
                    const blackHoleChild={black_hole_rings_entity:['black_hole_rings_mesh','gfx/models/planets/black_hole_rings.mesh'],black_hole_grav_lens_entity:['black_hole_grav_lens_mesh','gfx/models/planets/black_hole_grav_lens.mesh'],black_hole_grav_animation_entity:['black_hole_grav_animation_mesh','gfx/models/planets/black_hole_grav_animation.mesh']}[expectedAttachment[index]];
                    if(classId==='pc_black_hole'&&(!blackHoleChild||blackHoleChild[0]!==childMeshName||blackHoleChild[1]!==childPath)) { reject('attachment',classId,'Изменилась исходная связь дочернего объекта чёрной дыры.'); }
                    if (!parsedMeshes.has(childPath)) { parsedMeshes.set(childPath, parsePdxMesh(buffer(childPath))); }
                    const child = parsedMeshes.get(childPath);
                    validateStaticBindPose(child);
                    const childLayers = materialMeshes(child, [{ fields: childGfx, path: gfx.path }, { fields, path: entity.path }], childPath.slice(0, childPath.lastIndexOf('/') + 1), classId);
                    if(classId==='pc_black_hole')
                    {
                        const policyShaders={black_hole_rings_entity:['PdxMeshAlphaAdditiveAnimateUVNoDissolve'],black_hole_grav_lens_entity:['PdxMeshBlackHoleBillboard'],black_hole_grav_animation_entity:['PdxMeshBlackHoleBillboard']}[expectedAttachment[index]];
                        if(!policyShaders||childLayers.length===0||childLayers.some(layer=>!policyShaders.includes(layer.material.shader))) { reject('attachment-shader',classId,expectedAttachment[index]); }
                        if(optionalScalar(fields,'scale',1)!==2) { reject('attachment',classId,'Неожиданный масштаб дочерней чёрной дыры.'); }
                    }
                    const matrix = classId==='pc_black_hole'?[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]:sourceAttachmentMatrix(parsed, 'planet', classId);
                    dependencies.add(childPath); dependencies.add(entity.path); dependencies.add(gfx.path);
                    childLayers.forEach(m => ['diff', 'n', 'spec'].forEach(c => { if (m.material[c]) { dependencies.add(m.material[c]); } }));
                    attachments.push({ entityName: expectedAttachment[index], meshName: childMeshName, meshPath: childPath, meshes: childLayers, matrix,
                        scale: positiveScale(optionalScalar(fields, 'scale', 1), classId) * positiveScale(optionalScalar(childGfx, 'scale', 1), classId),
                        ...(classId==='pc_black_hole'?{policy:'StaticBlackHoleAttachmentsV1',staticApproximation:'Гравитационное искажение и UV-анимация отсутствуют; source geometry отображается статично.'}:{}) });
                }
            }
            bindings.push({ classId, visualId: generatedVisualId(classId), kind: star ? 'star' : 'planet', policy: GENERATED_VISUAL_POLICY,
                entityName: entity.name, entityPath: entity.path, meshName, meshPath, gfxPath: gfx.path,
                scale: positiveScale(resolveScale(entity), classId) * positiveScale(resolveScale(gfx), classId),
                dependencies: [...dependencies].sort(), meshes: materialLayers, attachments, poseReport, starSurface, sourceAtmosphereColor, approximationPolicy });
        }
        catch (error)
        {
            if (error instanceof GeneratedVisualError) { throw error; }
            reject('source-definition', classId, error.message);
        }
    }
    // Одна общая ring closure; она не изменяет class-only requirements.
    GENERATED_DEFINITION_PATHS.forEach(ast);
    const ring=resolveRingDefinitions(asts);
    const parsed=parsePdxMesh(buffer(ring.meshPath)); parsedMeshes.set(ring.meshPath,parsed);
    const poseReport=validateStaticBindPose(parsed);
    const meshes=parsed.meshes.map(mesh=>({...mesh,material:{...mesh.material}}));
    if(!meshes.length) { reject('attachment','rings','Пустой ring mesh.'); }
    const dependencies=new Set([ring.entity.path,ring.gfx.path,ring.meshPath]);
    for(const mesh of meshes)
    {
        if(mesh.material.shader!=='PdxMeshPlanetRings') { reject('shader','rings',mesh.material.shader); }
        for(const [channel,name] of [['diff','ring_tiling_diffuse.dds'],['n','ring_tiling_normal.dds'],['spec','ring_tiling_specular.dds']])
        {
            const value=mesh.material[channel];
            const path=typeof value==='string'?texturePath(value,'gfx/models/planets/','rings'):null;
            if(path!=='gfx/models/planets/'+name||exists&&!exists(path)) { reject('texture','rings','Неизвестная/отсутствующая ring texture.'); }
            mesh.material[channel]=path; dependencies.add(path);
        }
    }
    bindings.push({classId:'rings',visualId:'decoration.planet-rings',kind:'decoration',entityName:'gas_giant_rings_01_entity',
        entityPath:ring.entity.path,meshName:'gas_giant_rings_01_mesh',meshPath:ring.meshPath,gfxPath:ring.gfx.path,
        scale:positiveScale(resolveScale(ring.entity),'rings')*positiveScale(resolveScale(ring.gfx),'rings'),
        dependencies:[...dependencies].sort(),meshes,attachments:[],poseReport});
    const objects=objectIds===undefined?[]:resolveGeneratedWorldObjects(objectIds,read,{exists,maxDefinitionBytes:16*1024*1024-definitionBytes,parsedMeshes});
    return { classes, bindings, objects, parsedMeshes };

    function resolveScale(definition)
    {
        const value = optionalScalar(definition.fields, 'scale', 1);
        return typeof value === 'string' && value.startsWith('@') ? requireScalar(ast(definition.path), value) : value;
    }
}

function generatedStarSurface(fields, pc, world, id)
{
    if (requireScalar(fields, 'world') !== world) { reject('star-world', id, 'worldgfx tag mismatch.'); }
    const surface = {};
    for (const [field, property, expected] of [['tex_lava_noise','noisePath','gfx/worldgfx/lava_noise.dds'],['tex_lava_diffuse','lavaPath','gfx/worldgfx/lava_d.dds'],['tex_stone_diffuse','stonePath','gfx/worldgfx/stone_d.dds']])
    {
        const path = requireScalar(fields, field); if (path !== expected) { reject('star-texture', id, field); } surface[property] = path;
    }
    function hsv(block, key)
    {
        if (requireScalar(block,key) !== 'hsv') { reject('star-color', id, key); }
        const triple = block[block.findIndex(e=>e.key===key)+1];
        if (triple?.key !== null || !Array.isArray(triple.value) || triple.value.length !== 3 || triple.value.some(e=>e.key!==null || !Number.isFinite(e.value))) { reject('star-color',id,key); }
        return triple.value.map(e=>e.value);
    }
    for (const [prefix, property] of [['lava_bright','lavaBright'],['lava_hot_stone','lavaHotStone'],['lava_cold_stone','lavaColdStone']])
    {
        surface[property+'Color'] = hsv(fields,prefix+'_color'); surface[property+'Intensity'] = requireScalar(fields,prefix+'_intensity');
        if (!Number.isFinite(surface[property+'Intensity']) || surface[property+'Intensity'] < 0) { reject('star-intensity',id,prefix); }
    }
    if(id==='pc_neutron_star'&&['atmosphere_color','atmosphere_intensity','atmosphere_width'].every(key=>!pc.some(e=>e.key===key)))
    {
        // Явная RC approximation: 0 отключает неизвестный CPU mix, не объявляется native default.
        surface.atmosphereColor=[...surface.lavaBrightColor]; surface.atmosphereIntensity=0; surface.atmosphereWidth=0;
        surface.approximationPolicy='NeutronNoSurfaceAtmosphereMixV1';
    }
    else
    {
        surface.atmosphereColor = hsv(pc,'atmosphere_color'); surface.atmosphereIntensity = requireScalar(pc,'atmosphere_intensity'); surface.atmosphereWidth = requireScalar(pc,'atmosphere_width');
        if (!Number.isFinite(surface.atmosphereIntensity) || surface.atmosphereIntensity < 0 || !Number.isFinite(surface.atmosphereWidth) || surface.atmosphereWidth <= 0) { reject('star-atmosphere',id,'width/intensity'); }
    }
    return surface;
}

function sourceAttachmentMatrix(parsed, name, id)
{
    const found = [];
    function walk(node) { if (node.name === 'skeleton') { found.push(...node.children.filter(c => c.name === name).map(c => c.properties.tx)); } node.children.forEach(walk); }
    parsed.nodes.forEach(walk);
    if (found.length !== 1 || !(found[0] instanceof Float32Array) || found[0].length !== 12) { reject('attachment-transform', id, name); }
    const t = found[0]; const [a,b,c,d,e,f,g,h,i] = [t[0],t[3],t[6],t[1],t[4],t[7],t[2],t[5],t[8]];
    const det = a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g);
    if (!Number.isFinite(det) || Math.abs(det) < 1e-12) { reject('attachment-transform', id, 'singular'); }
    const m = [(e*i-f*h)/det,(f*g-d*i)/det,(d*h-e*g)/det,0,(c*h-b*i)/det,(a*i-c*g)/det,(b*g-a*h)/det,0,(b*f-c*e)/det,(c*d-a*f)/det,(a*e-b*d)/det,0,0,0,0,1];
    for (let r=0;r<3;r++) { m[12+r]=-(m[r]*t[9]+m[4+r]*t[10]+m[8+r]*t[11]); }
    if (m.some(v=>!Number.isFinite(v))) { reject('attachment-transform', id, 'nonfinite'); }
    return m;
}

export function generatedTexturePlan(bindings, read, { maxDimension = 512 } = {})
{
    if (maxDimension !== 512) { reject('lod-policy', '', 'Поддерживается только SourceMip512V1.'); }
    const plan = new Map(); let decodedBytes = 0;
    for (const binding of bindings)
    {
        for (const path of binding.dependencies.filter(p => p.endsWith('.dds')))
        {
            const wormholePath = path==='gfx/models/add_ons/wormhole_rings.dds' || path==='gfx/models/add_ons/wormhole_stretch.dds';
            if (wormholePath && binding.objectId!=='natural_wormhole') { reject('wormhole-native-mip','natural_wormhole',path); }
            if (plan.has(path)) { continue; }
            const source = read(path); const info = inspectDds(source, { allowBgraMipChain: true });
            const limit=path==='gfx/worldgfx/lava_noise.dds'?1024:maxDimension; let mipLevel = 0;
            while (Math.max(info.width >> mipLevel, info.height >> mipLevel) > limit && mipLevel + 1 < info.mipCount) { mipLevel++; }
            let nativeWormholeMip0=false;
            if(path==='gfx/models/add_ons/wormhole_rings.dds')
            {
                if(info.width!==1024||info.height!==1024||info.mipCount!==1||info.faceCount!==1) { reject('wormhole-native-mip','natural_wormhole',path); }
                nativeWormholeMip0=true;
            }
            else if(path==='gfx/models/add_ons/wormhole_stretch.dds')
            {
                if(info.width!==2048||info.height!==1024||info.mipCount!==1||info.faceCount!==1) { reject('wormhole-native-mip','natural_wormhole',path); }
                nativeWormholeMip0=true;
            }
            const options = { mipLevel, allowBgraMipChain: true }; const selected = inspectDds(source, options);
            if (!nativeWormholeMip0&&Math.max(selected.width, selected.height) > limit || selected.faceCount !== 1 && path !== 'gfx/worldgfx/lava_noise.dds') { reject('lod-unavailable', binding.classId, path); }
            decodedBytes += selected.decodedBytes;
            if (decodedBytes > MAX_BYTES) { reject('memory', binding.classId, 'DDS closure >512MiB.'); }
            plan.set(path, { options, decodedBytes: selected.decodedBytes, width: selected.width, height: selected.height, ...(nativeWormholeMip0?{policy:'WormholeNativeMip0V1'}:{}) });
        }
    }
    return { plan, decodedBytes };
}

export function decodeGeneratedTextures(plan, read)
{
    return new Map([...plan].map(([path, item]) => [path, decodeDds(read(path), item.options)]));
}
