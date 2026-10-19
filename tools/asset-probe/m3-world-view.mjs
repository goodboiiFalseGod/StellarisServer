import { computeConvertedBytes } from './m3-memory.mjs';
import { validateBelts } from './m3-belts.mjs';
import { generatedRequirementsCanonicalText, generatedVisualId } from './generated-world-visual-profile.mjs';

// Чистая проекция разрешённого снапшота; здесь нет расчёта игрового движения.
export const VISUAL_IDS = Object.freeze(['ship.corvette.mammalian', 'station.outpost.mammalian', 'planet.continental', 'star.g']);
export const CORVETTE_OFFSETS = Object.freeze([Object.freeze([-6, -4]), Object.freeze([0, 0]), Object.freeze([6, -4])]);
const PHASES = new Set(['idle', 'movingLocal', 'approachingExit', 'charging', 'inTransit']);
const finite = value => typeof value === 'number' && Number.isFinite(value);
const point = value => value && finite(value.x) && finite(value.z);
const fail = () => { throw new Error('M3: некорректная пространственная проекция.'); };
const spatialNameFallbacks = new WeakMap();

// Единая подпись для карты, выбранного объекта, колонии и пунктов назначения.
export function resolveSpatialNames(state, resolve)
{
    for (const item of [...(state?.spatial?.systems ?? []), ...(state?.spatial?.bodies ?? [])])
    {
        if (!item.nameKey) { continue; }
        const suffix = item.nameSuffix ?? '';
        if (!spatialNameFallbacks.has(item))
        {
            const original = item.name ?? item.nameKey.replaceAll('_', ' ');
            spatialNameFallbacks.set(item, suffix && original.endsWith(suffix) ? original.slice(0, -suffix.length) : original);
        }
        item.name = resolve(item.nameKey, spatialNameFallbacks.get(item)) + suffix;
    }
    const bodies = new Map((state?.spatial?.bodies ?? []).map(body => [body.id, body]));
    const colonies = new Map((state?.spatial?.colonies ?? []).filter(colony => colony.economyPlanetId != null)
        .map(colony => [colony.economyPlanetId, bodies.get(colony.bodyId)]));
    for (const planet of state?.planets ?? [])
    {
        const body = colonies.get(planet.id);
        if (body?.name) { planet.name = body.name; }
    }
    return state;
}

export function profileTexturePlan(profile)
{
    if (!(profile?.samples instanceof Map)) { throw new Error('M3: отсутствует проверенный профиль ассетов.'); }
    const meshes = [];
    const images = new Map();
    let ids=VISUAL_IDS;
    if(!profile.generatedRequirements&&profile.samples.size!==VISUAL_IDS.length) { throw new Error('M3: неполный визуальный профиль или неизвестные templates вне generated contract.'); }
    if(profile.generatedRequirements)
    {
        generatedRequirementsCanonicalText(profile.generatedRequirements);
        ids=[...VISUAL_IDS,...profile.generatedRequirements.classIds.map(generatedVisualId),...(profile.generatedRequirements.objectIds??[]).map(id=>'object.'+id)];
        if(!(profile.generatedBindings instanceof Map)||profile.generatedBindings.size!==profile.generatedRequirements.classIds.length||!(profile.generatedObjects instanceof Map)||profile.generatedObjects.size!==(profile.generatedRequirements.objectIds??[]).length||profile.samples.size!==ids.length||!(profile.systemLighting instanceof Map)||profile.systemLighting.size===0) { throw new Error('M3: неполный generated visual contract.'); }
        const ring=profile.ringSample;
        if(!ring||ring.sourceEntityName!=='gas_giant_rings_01_entity'||ring.bodyKind!=='decoration'||!Number.isFinite(ring.scale)||ring.scale<=0||!Array.isArray(ring.meshes)||!ring.meshes.length||ring.meshes.some(m=>m.material.shader!=='PdxMeshPlanetRings')||!(ring.images instanceof Map)) { throw new Error('M3: неполный ring contract.'); }
        meshes.push(...ring.meshes);
        for(const [path,image] of ring.images) { images.set(path,image); }
    }
    for (const id of ids)
    {
        const sample = profile.samples.get(id);
        if (!sample || !Array.isArray(sample.meshes) || !sample.meshes.length || !(sample.images instanceof Map)) { throw new Error('M3: неполный визуальный профиль.'); }
        if(id.startsWith('world.') && !VISUAL_IDS.includes(id))
        {
            const binding=profile.generatedBindings.get(id);
            if(!binding||id!==generatedVisualId(binding.classId)||sample.sourceClassId!==binding.classId||sample.sourceEntityName!==binding.entityName||sample.bodyKind!==binding.kind||!['star','planet'].includes(binding.kind)) { throw new Error('M3: generated source binding mismatch.'); }
            for(const attachment of sample.attachments??[]) { if(!Array.isArray(attachment.meshes)||!Array.isArray(attachment.matrix)||attachment.matrix.length!==16||!attachment.matrix.every(Number.isFinite)||!Number.isFinite(attachment.scale)||attachment.scale<=0) { throw new Error('M3: generated attachment contract.'); } meshes.push(...attachment.meshes); }
        }
        else if(id.startsWith('object.'))
        {
            const binding=profile.generatedObjects.get(id);
            const gateway=id==='object.gateway_ruined';
            if(!binding||id!=='object.'+binding.objectId||sample.bodyKind!=='object'||gateway&&(sample.sourceEntityName!=='deactivated_gateway_entity'||binding.approximationPolicy!=='StaticGatewayBindPoseV1'||binding.scalePolicy!=='GatewaySurfaceRadius6V1'||(sample.attachments??[]).length)||!gateway&&(id!=='object.natural_wormhole'||sample.sourceEntityName!=='natural_wormhole_entity'||binding.approximationPolicy!=='StaticWormholeRingsV1'||binding.scalePolicy!=='WormholeSurfaceRadius6V1'||(sample.attachments??[]).length!==1)) { throw new Error('M3: generated object source binding mismatch.'); }
            for(const attachment of sample.attachments??[]) { if(!Array.isArray(attachment.meshes)||!Array.isArray(attachment.matrix)||attachment.matrix.length!==16||!attachment.matrix.every(Number.isFinite)||!Number.isFinite(attachment.scale)||attachment.scale<=0||!(attachment.images instanceof Map)) { throw new Error('M3: generated object attachment contract.'); } meshes.push(...attachment.meshes); }
        }
        meshes.push(...sample.meshes);
        for (const [path, image] of sample.images)
        {
            if (images.has(path) && images.get(path) !== image) { throw new Error('M3: неоднозначная общая текстура профиля.'); }
            images.set(path, image);
        }
    }
    return { images, convertedBytes: computeConvertedBytes({ meshes, images }) };
}

export function validateGeneratedSnapshotBindings(resources,state)
{
    if(state.spatial.bodies.some(b=>b.hasRing)&&!resources.ringTemplate) { throw new Error('M3: отсутствует импортированный ring binding.'); }
    for (const belt of state.spatial.belts ?? [])
    {
        const binding = resources.generatedBindings?.get(belt.visualId);
        if (!binding || belt.visualId !== `world.${binding.classId}` || binding.kind !== 'planet' || !resources.templates?.has(belt.visualId))
        { throw new Error('M3: отсутствует импортированный belt binding.'); }
    }
    const generatedSystems=new Set();
    for(const body of state.spatial.bodies)
    {
        if(!body.visualId.startsWith('world.')) { continue; }
        const binding=resources.generatedBindings?.get(body.visualId);
        if(!binding||binding.classId!==body.worldClassId||(body.kind==='star'?binding.kind!=='star':!['planet','moon'].includes(body.kind)||binding.kind!=='planet')) { throw new Error('M3: snapshot source class/kind не соответствует импортированному native binding.'); }
        generatedSystems.add(body.systemId);
    }
    for(const gateway of state.spatial.gateways??[])
    {
        const binding=resources.generatedObjects?.get(gateway.visualId);
        if(!binding||binding.objectId!=='gateway_ruined'||gateway.visualId!=='object.gateway_ruined'||!resources.templates?.has(gateway.visualId)) { throw new Error('M3: snapshot gateway не соответствует импортированному source binding.'); }
    }
    for(const wormhole of state.spatial.wormholes??[])
    {
        const binding=resources.generatedObjects?.get(wormhole.visualId);
        if(!binding||binding.objectId!=='natural_wormhole'||wormhole.visualId!=='object.natural_wormhole'||!resources.templates?.has(wormhole.visualId)) { throw new Error('M3: snapshot wormhole не соответствует импортированному source binding.'); }
    }
    for(const system of state.spatial.systems.filter(s=>generatedSystems.has(s.id))) { if(!resources.systemLighting?.has(system.starClassId)) { throw new Error('M3: unsupported source system starClassId lighting.'); } }
}

export function validateSnapshot(state)
{
    if (state?.protocol !== 2 || typeof state.sessionId !== 'string' || !state.sessionId || !Number.isSafeInteger(state.sequence) || state.sequence < 0 || !Array.isArray(state.participants) || !state.spatial) { fail(); }
    const spatial = state.spatial;
    for (const key of ['systems', 'hyperlanes', 'bodies', 'colonies', 'fleets'])
    {
        if (!Array.isArray(spatial[key])) { fail(); }
        const ids = new Set();
        for (const item of spatial[key])
        {
            if (typeof item?.id !== 'string' || !item.id || ids.has(item.id)) { fail(); }
            ids.add(item.id);
        }
    }
    if(spatial.gateways!=null&&!Array.isArray(spatial.gateways)) { fail(); }
    if(spatial.wormholes!=null&&!Array.isArray(spatial.wormholes)) { fail(); }
    const systems = new Set(spatial.systems.map(item => item.id));
    const systemById=new Map(spatial.systems.map(item=>[item.id,item]));
    const gatewayIds=new Set();
    for(const gateway of spatial.gateways??[])
    {
        if(typeof gateway.id!=='string'||!gateway.id||gatewayIds.has(gateway.id)||!systems.has(gateway.systemId)||!point(gateway.localPosition)||gateway.visualId!=='object.gateway_ruined'||gateway.name!=='Заброшенные врата'||gateway.status!=='Неактивны') { fail(); }
        const system=systemById.get(gateway.systemId);
        if(Math.hypot(gateway.localPosition.x,gateway.localPosition.z)+6>system.radius+1e-6) { fail(); }
        gatewayIds.add(gateway.id);
    }
    const wormholeIds=new Set(),wormholeSystems=new Set();
    for(const wormhole of spatial.wormholes??[])
    {
        if(typeof wormhole.id!=='string'||!wormhole.id||wormholeIds.has(wormhole.id)||wormholeSystems.has(wormhole.systemId)||!systems.has(wormhole.systemId)||!point(wormhole.localPosition)||wormhole.visualId!=='object.natural_wormhole'||wormhole.name!=='Червоточина'||wormhole.status!=='Не исследована'||Object.hasOwn(wormhole,'pairId')||Object.hasOwn(wormhole,'partnerId')) { fail(); }
        const system=systemById.get(wormhole.systemId);
        if(Math.hypot(wormhole.localPosition.x,wormhole.localPosition.z)+6>system.radius+1e-6) { fail(); }
        wormholeIds.add(wormhole.id);wormholeSystems.add(wormhole.systemId);
    }
    validateBelts(spatial.belts, spatial.systems);
    if (spatial.nebulae != null && !Array.isArray(spatial.nebulae)) { fail(); }
    const nebulaIds = new Set(); const members = new Set();
    for (const nebula of spatial.nebulae ?? [])
    {
        if (typeof nebula?.id !== 'string' || !nebula.id || nebulaIds.has(nebula.id) || typeof nebula.nameKey !== 'string' ||
            ![nebula.center?.x, nebula.center?.y, nebula.center?.z].every(finite) || !finite(nebula.radius) || nebula.radius <= 0 ||
            !Array.isArray(nebula.systemIds) || !nebula.systemIds.length) { fail(); }
        nebulaIds.add(nebula.id);
        for (const id of nebula.systemIds) { if (!systems.has(id) || members.has(id)) { fail(); } members.add(id); }
    }
    const bodies = new Set(spatial.bodies.map(item => item.id));
    for (const item of [...spatial.systems, ...spatial.bodies])
    {
        if (item.nameKey != null && (typeof item.nameKey !== 'string' || !item.nameKey.trim()) ||
            item.nameSuffix != null && (typeof item.nameSuffix !== 'string' || item.nameKey == null)) { fail(); }
    }
    for (const system of spatial.systems)
    {
        if (![system.galaxyPosition?.x, system.galaxyPosition?.y, system.galaxyPosition?.z].every(finite) || !finite(system.radius) || system.radius <= 0 || (system.starBodyId != null && !bodies.has(system.starBodyId))) { fail(); }
    }
    for (const lane of spatial.hyperlanes) { if (!systems.has(lane.fromSystemId) || !systems.has(lane.toSystemId)) { fail(); } }
    for (const body of spatial.bodies)
    {
        const generated=typeof body.visualId==='string'&&body.visualId.startsWith('world.');
        if (!systems.has(body.systemId) || !['star', 'planet', 'moon', 'station'].includes(body.kind) || !point(body.localPosition) || !finite(body.scale) || body.scale <= 0 || !generated&&!VISUAL_IDS.includes(body.visualId)) { fail(); }
        if(generated&&(generatedVisualId(body.worldClassId)!==body.visualId||!['star','planet','moon'].includes(body.kind))) { fail(); }
        if(body.planetSize!=null&&(!['planet','moon'].includes(body.kind)||!Number.isSafeInteger(body.planetSize)||body.planetSize<=0)) { fail(); }
        if(body.hasRing!=null&&(typeof body.hasRing!=='boolean'||!['planet','moon'].includes(body.kind))) { fail(); }
        if(body.parentBodyId!=null&&(!bodies.has(body.parentBodyId)||body.parentBodyId===body.id)) { fail(); }
    }
    const byBody=new Map(spatial.bodies.map(b=>[b.id,b]));
    for(const body of spatial.bodies)
    {
        const seen=new Set([body.id]); let parent=body.parentBodyId;
        while(parent!=null) { const value=byBody.get(parent); if(!value||value.systemId!==body.systemId||seen.has(parent)) { fail(); } seen.add(parent); parent=value.parentBodyId; }
    }
    const generatedSystems=new Set(spatial.bodies.filter(b=>b.visualId.startsWith('world.')).map(b=>b.systemId));
    for(const system of spatial.systems.filter(s=>generatedSystems.has(s.id))) { if(typeof system.starClassId!=='string'||!/^sc_[a-z0-9_]{1,96}$/.test(system.starClassId)) { fail(); } }
    for (const colony of spatial.colonies) { if (!bodies.has(colony.bodyId)) { fail(); } }
    for (const fleet of spatial.fleets)
    {
        if (!PHASES.has(fleet.phase) || !VISUAL_IDS.includes(fleet.visualId) || !Number.isSafeInteger(fleet.shipCount) || fleet.shipCount < 1) { fail(); }
        const location = fleet.location;
        if (location?.kind === 'system')
        {
            if (fleet.phase === 'inTransit' || !systems.has(location.systemId) || !point(location.position)) { fail(); }
        }
        else if (location?.kind === 'transit')
        {
            if (fleet.phase !== 'inTransit' || !systems.has(location.fromSystemId) || !systems.has(location.toSystemId) || !finite(location.progress) || location.progress < 0 || location.progress > 1) { fail(); }
        }
        else { fail(); }
    }
    return state;
}

export function reconcileSelection(state, selection, commandFleetId)
{
    const arrays = { system: 'systems', body: 'bodies', colony: 'colonies', fleet: 'fleets', gateway: 'gateways', wormhole: 'wormholes' };
    const validSelection = selection && arrays[selection.kind] && (state?.spatial?.[arrays[selection.kind]]??[]).some(item => item.id === selection.id);
    const own = ownFleet(state, commandFleetId);
    return { selection: validSelection ? { kind: selection.kind, id: selection.id } : null, commandFleetId: own?.id ?? null };
}

export function ownFleet(state, id)
{
    if (!state || state.participants.find(item => item.id === state.you)?.role !== 'player') { return null; }
    return state.spatial.fleets.find(item => item.id === id && item.ownerParticipantId === state.you) ?? null;
}

export function orderGate(state, commandFleetId, { fresh, profileReady, contextLost, viewKind, pointOrder = false, systemId } = {})
{
    if (!fresh || !profileReady || contextLost) { return 'Дождитесь свежего состояния и проверки ассетов.'; }
    if (state?.phase !== 'running' || viewKind === 'stress') { return 'В этом режиме игровые приказы недоступны.'; }
    const fleet = ownFleet(state, commandFleetId);
    if (!fleet) { return 'Выберите собственный флот для приказа.'; }
    if (pointOrder && (fleet.location.kind !== 'system' || fleet.location.systemId !== systemId)) { return 'Локальный приказ доступен только в текущей системе флота.'; }
    return null;
}

export function fleetGalaxyPosition(state, fleet)
{
    const systems = state.spatial.systems;
    if (fleet.location.kind === 'system') { return { ...systems.find(item => item.id === fleet.location.systemId).galaxyPosition }; }
    const from = systems.find(item => item.id === fleet.location.fromSystemId).galaxyPosition;
    const to = systems.find(item => item.id === fleet.location.toSystemId).galaxyPosition;
    const t = fleet.location.progress;
    return { x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t, z: from.z + (to.z - from.z) * t };
}

// Это подпись полученного объекта, а не придуманное игровое имя.
export function bodyDisplayName(body, bodies = [])
{
    if (body?.name && !/^pc_[a-z0-9_]+$/.test(body.name)) { return body.name; }
    const type = { star: 'Звезда', planet: 'Планета', moon: 'Спутник', station: 'Станция' }[body?.kind] ?? 'Объект';
    const siblings = bodies.filter(item => item.systemId === body?.systemId && item.kind === body?.kind).sort((a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
    const index = siblings.findIndex(item => item.id === body?.id);
    return `${type}${index >= 0 ? ` ${index + 1}` : ''}`;
}

export function collectViewObjects(state, view)
{
    if (!state || view.kind === 'stress') { return []; }
    if (view.kind === 'galaxy')
    {
        return [
            ...state.spatial.systems.map(item => ({ kind: 'system', id: item.id, position: { ...item.galaxyPosition } })),
            ...state.spatial.fleets.map(item => ({ kind: 'fleet', id: item.id, position: fleetGalaxyPosition(state, item), ownerParticipantId: item.ownerParticipantId }))
        ];
    }
    if (view.kind !== 'system' || !state.spatial.systems.some(item => item.id === view.systemId)) { throw new Error('M3: система отсутствует в разрешённой проекции.'); }
    return [
        ...(state.spatial.gateways??[]).filter(item=>item.systemId===view.systemId).map(item=>({kind:'gateway',id:item.id,position:{x:item.localPosition.x,y:0,z:item.localPosition.z},visualId:item.visualId,scale:1})),
        ...(state.spatial.wormholes??[]).filter(item=>item.systemId===view.systemId).map(item=>({kind:'wormhole',id:item.id,position:{x:item.localPosition.x,y:0,z:item.localPosition.z},visualId:item.visualId,scale:1})),
        ...state.spatial.bodies.filter(item => item.systemId === view.systemId).map(item => ({ kind: 'body', id: item.id, position: { x: item.localPosition.x, y: 0, z: item.localPosition.z }, visualId: item.visualId, scale: item.scale, ownerParticipantId: item.ownerParticipantId,
            ...(item.visualId.startsWith('world.')?{worldClassId:item.worldClassId,bodyKind:item.kind,parentBodyId:item.parentBodyId}: {}) })),
        ...state.spatial.fleets.filter(item => item.location.kind === 'system' && item.location.systemId === view.systemId).map(item => ({ kind: 'fleet', id: item.id, position: { x: item.location.position.x, y: 0, z: item.location.position.z }, visualId: item.visualId, scale: 1, shipCount: item.shipCount, ownerParticipantId: item.ownerParticipantId }))
    ];
}

export function interpolateFleet(previous, current, fleetId, now)
{
    const fleet = current?.state.spatial.fleets.find(item => item.id === fleetId);
    if (!fleet || fleet.location.kind !== 'system') { return null; }
    const actual = { ...fleet.location.position };
    const old = previous?.state.spatial.fleets.find(item => item.id === fleetId);
    if (!old || old.location.kind !== 'system' || previous.state.sessionId !== current.state.sessionId || current.state.sequence !== previous.state.sequence + 1 || previous.state.paused || current.state.paused || previous.state.phase !== current.state.phase || old.phase !== fleet.phase || old.location.systemId !== fleet.location.systemId || old.movement?.orderVersion == null || old.movement.orderVersion !== fleet.movement?.orderVersion || current.receivedAt <= previous.receivedAt) { return actual; }
    const alpha = Math.max(0, Math.min(1, (now - 100 - previous.receivedAt) / (current.receivedAt - previous.receivedAt)));
    return { x: old.location.position.x + (actual.x - old.location.position.x) * alpha, z: old.location.position.z + (actual.z - old.location.position.z) * alpha };
}
