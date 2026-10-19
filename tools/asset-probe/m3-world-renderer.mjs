import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { orbitAboutPivot, radiusAboutOrigin, viewCenter, createCameraPreset, createSystemOverviewPreset, CAMERA_PRESET_IDS } from './m3-camera.mjs';
import { displayedSurfaceRadius, templateDisplayFactor, orbitPositions, createStarCoronaParameters, createGalaxyGlow, updateGalaxyGlyphScale, createObjectLabel, validateLabelInsets, labelFitsViewport, selectVisibleLabelKeys } from './m3-presentation.mjs';
import { createBackgroundParameters, selectSourceBackgroundBand } from './m3-background.mjs';
import { createGalaxyLayerTextures, createGalaxyLayerParameters, createGalaxyLayerGroup } from './m3-galaxy-layers.mjs';
import { createNebulaGroup } from './m3-nebulae.mjs';
import { createBeltGroup, beltOuterRadius } from './m3-belts.mjs';
import { normalRrxg, specularApprox, createDdsCubeTexture } from './adapters.mjs';
import { createStaticGStarParameters } from './m3-star-material.mjs';
import { createGeneratedMaterial, findAtmosphereStarLayers, sourceHsvRgb } from './generated-world-material.mjs';
import { gpuResourceReservation, requireGeneratedMemory, GENERATED_MEMORY_LIMIT } from './generated-world-resource-plan.mjs';
import { requireConvertedBudget, MAX_CONVERTED_BYTES } from './m3-memory.mjs';
import { VISUAL_IDS, CORVETTE_OFFSETS, profileTexturePlan, validateSnapshot, validateGeneratedSnapshotBindings, reconcileSelection, ownFleet, orderGate, collectViewObjects, interpolateFleet, bodyDisplayName } from './m3-world-view.mjs';
import { SYSTEM_HIT_RADIUS, nearestProjectedTarget, zoomDetail, navigationScale, validateNavigationSnapshot, receivedRoute, routeGeometry, withinManualRadius } from './m3-navigation.mjs';

// Отдельные внешние bounds кольца не участвуют в surface radius подписей.
export function displayedRingRadius(ringTemplate, bodyTemplate, body)
{
    if(!body.hasRing||!ringTemplate) { return 0; }
    const bounds=new THREE.Box3().setFromObject(ringTemplate);
    return Math.hypot(Math.max(Math.abs(bounds.min.x),Math.abs(bounds.max.x)),Math.max(Math.abs(bounds.min.z),Math.abs(bounds.max.z))) * body.scale * templateDisplayFactor(bodyTemplate,body.visualId);
}

// Единственный владелец GPU-профиля; экземпляры видов только заимствуют его ресурсы.
export class M3WorldRenderer
{
    constructor(canvas, callbacks = {})
    {
        this.canvas = canvas;
        this.callbacks = callbacks;
        this.disposed = false;
        this.contextLost = false;
        this.needsFreshSnapshot = false;
        this.fresh = false;
        this.profileReady = false;
        this.profile = null;
        this.resources = null;
        this.current = null;
        this.retiredSessions = new Set();
        this.previous = null;
        this.selection = null;
        this.commandFleetId = null;
        this.hoverSystemId = null;
        this.detailLevel = 'far';
        this.view = { kind: 'galaxy' };
        this.viewResources = null;
        this.frameMilliseconds = 0;
        this.handlers = [];
        this.nativePointers = new Map();
        this.nativeZoomGeneration = 0;
        this.nativeZoomEvents = new WeakMap();
        this.labelInsets = { top: 16, right: 16, bottom: 72, left: 16 };
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
        if (!this.renderer.capabilities.isWebGL2) { this.renderer.dispose(); throw new Error('Для M3 требуется WebGL2.'); }
        this.renderer.setPixelRatio(1);
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.NoToneMapping;
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color('#050c16');
        this.ambientLight = new THREE.AmbientLight(0xffffff, 0.18);
        this.scene.add(this.ambientLight);
        const light = new THREE.DirectionalLight(0xffffff, 2);
        light.position.set(10, 12, 8);
        this.scene.add(light);
        this.fillLight = light;
        this.camera = new THREE.PerspectiveCamera(45, 1, 0.01, 10000);
        this.camera.position.set(240, 240, 240);
        // Capture не допускает начала native rotate для собственной орбиты.
        this.listen('pointerdown', event => this.pointerDown(event), true);
        this.controls = new OrbitControls(this.camera, canvas);
        this.controls.mouseButtons.LEFT = THREE.MOUSE.PAN;
        this.controls.mouseButtons.RIGHT = null;
        this.controls.mouseButtons.MIDDLE = THREE.MOUSE.DOLLY;
        this.controls.enableRotate = false;
        this.listen('wheel', event => this.captureNativeZoomInput('wheel', event), true);
        this.listen('wheel', event => this.finishNativeZoomInput(event));
        this.listen('pointermove', event => this.pointerMove(event), true);
        this.listen('pointerup', event => this.pointerUp(event));
        this.listen('pointercancel', event => this.cancelPointer(event));
        this.listen('lostpointercapture', event => this.cancelPointer(event));
        this.listen('pointerleave', () => { this.hoverSystemId = null; });
        const window = canvas.ownerDocument?.defaultView;
        if (window)
        {
            // OrbitControls processes captured pointermove on ownerDocument; window bubble runs after it.
            this.listen('pointermove', event => this.finishNativeZoomInput(event), false, window);
            this.listen('keyup', event => { if (event.key === 'Alt') { this.finishOrbit(); } }, false, window);
            this.listen('blur', () => this.cancelPointer(), false, window);
            this.listen('pagehide', () => this.cancelPointer(), false, window);
        }
        this.listen('contextmenu', event => event.preventDefault());
        this.listen('dblclick', event =>
        {
            if (event.altKey || event.button !== 0 || this.view.kind !== 'galaxy' || this.rejectDoubleClick || performance.now() < (this.suppressDoubleClickUntil ?? 0)) { return; }
            const hit = this.pick(event, 'system');
            if (hit) { this.callbacks.onOpenSystem?.(hit.id); }
        });
        this.listen('webglcontextlost', event =>
        {
            event.preventDefault();
            this.cancelPointer();
            this.contextLost = true;
            this.needsFreshSnapshot = true;
            this.fresh = false;
            this.previous = null;
            this.callbacks.onContextState?.('lost');
        });
        this.listen('webglcontextrestored', () =>
        {
            this.contextLost = false;
            this.needsFreshSnapshot = true;
            this.fresh = false;
            this.previous = null;
            // Three восстанавливает buffers/textures из удерживаемых CPU-массивов.
            // Пересоздание вида не читает файлы и не повторяет конвертацию каналов.
            try { this.rebuild(); this.callbacks.onContextState?.('restored'); }
            catch (error) { this.contextLost = true; this.callbacks.onHint?.('Не удалось восстановить графический вид. Повторите импорт.'); }
        });
        this.resize();
    }

    setProfile(profile, { maxConvertedBytes = MAX_CONVERTED_BYTES } = {})
    {
        this.requireLive();
        if (profile === this.profile && this.resources)
        {
            requireConvertedBudget(0, this.resources.convertedBytes, maxConvertedBytes);
            this.profileReady = true;
            return;
        }
        const { images, convertedBytes } = profileTexturePlan(profile);
        requireConvertedBudget(convertedBytes, this.resources?.convertedBytes ?? 0, maxConvertedBytes);
        if(profile.generatedRequirements) { requireGeneratedMemory({decodedBytes:profile.decodedBytes,convertedBytes},{decodedBytes:this.profile?.decodedBytes??0,convertedBytes:this.resources?.convertedBytes??0}); }
        const candidate = { templates: new Map(), backgrounds: new Map(), galaxyLayers: new Map(), geometries: [], materials: [], textures: [], textureMap: new Map(), images, convertedBytes,
            systemLighting:profile.systemLighting?new Map([...profile.systemLighting].map(([id,l])=>[id,structuredClone(l)])):null,
            generatedBindings:profile.generatedBindings?new Map([...profile.generatedBindings].map(([id,b])=>[id,{...b}])):new Map(),
            generatedObjects:profile.generatedObjects?new Map([...profile.generatedObjects].map(([id,b])=>[id,{...b}])):new Map() };
        let candidateView;
        try
        {
            for (const [id,sample] of profile.samples) { candidate.templates.set(id, this.prepareTemplate(sample, candidate)); }
            if(profile.ringSample) { candidate.ringTemplate=this.prepareTemplate(profile.ringSample,candidate); }
            if(candidate.generatedBindings.size)
            {
                const commonWorldBaseline=templateDisplayFactor(candidate.templates.get('planet.continental'),'planet.continental')*0.5;
                for(const [id] of candidate.generatedBindings)
                {
                    const template=candidate.templates.get(id);
                    template.userData.generatedDisplayFactor=commonWorldBaseline;
                    template.userData.generatedDisplayPolicy='GeneratedMapUnitSurfaceV1';
                }
            }
            if (profile.backgrounds !== undefined)
            {
                if (!(profile.backgrounds instanceof Map) || !profile.backgrounds.has('galaxy.stars') || profile.backgrounds.size < 2) { throw new Error('M3: incomplete presentation backgrounds.'); }
                const backgroundTextures=new Map();
                for (const [id,background] of profile.backgrounds)
                {
                    const textureFor=layer=>{let texture=backgroundTextures.get(layer.sourcePath);if(!texture){texture=createDdsCubeTexture(layer.image);backgroundTextures.set(layer.sourcePath,texture);candidate.textures.push(...texture.images,texture);}return texture;};
                    if(background.layers)
                    {
                        for(const [index,layer] of background.layers.entries()){const texture=textureFor(layer);const material=new THREE.ShaderMaterial(createBackgroundParameters(layer,texture));candidate.materials.push(material);candidate.backgrounds.set(`${id}:${['core','mid','rim'][index]}`,material);}
                    }
                    else
                    {
                        const texture=textureFor(background);const material=new THREE.ShaderMaterial(createBackgroundParameters(background,texture));candidate.materials.push(material);candidate.backgrounds.set(id,material);
                    }
                }
            }
            if (profile.galaxyLayers !== undefined)
            {
                const textures = createGalaxyLayerTextures(profile.galaxyLayers);
                candidate.textures.push(...textures.values());
                for (const [id, parameters] of Object.entries(createGalaxyLayerParameters(textures)))
                {
                    const material = new THREE.ShaderMaterial(parameters);
                    candidate.materials.push(material);
                    candidate.galaxyLayers.set(id, material);
                }
            }
            candidateView = this.buildView(candidate, this.current?.state, this.view);
            candidate.gpuReservationBytes=gpuResourceReservation(candidate);
            const retainedGpu=this.resources?.gpuReservationBytes??(this.resources?gpuResourceReservation(this.resources):0);
            if(candidate.gpuReservationBytes+retainedGpu>GENERATED_MEMORY_LIMIT) { throw new Error('M3: GPU reservation old+staged >512MiB.'); }
            if(profile.generatedRequirements) { this.stageGeneratedGpu(candidate,candidateView); }
        }
        catch (error)
        {
            this.releaseView(candidateView);
            this.releaseProfile(candidate);
            throw error;
        }
        const previous = this.resources;
        this.replaceView(candidateView);
        this.resources = candidate;
        this.profile = profile;
        this.profileReady = true;
        this.releaseProfile(previous);
    }

    stageGeneratedGpu(candidate,candidateView)
    {
        if(!this.renderer?.isWebGLRenderer||typeof this.renderer.getContext!=='function') { throw new Error('Generated profile: WebGL2 staging недоступен.'); }
        const gl=this.renderer.getContext();
        if(gl.isContextLost()) { throw new Error('Generated profile: GPU context lost.'); }
        const previousTarget=this.renderer.getRenderTarget(); const previousError=this.renderer.debug.onShaderError;
        const previousCheck=this.renderer.debug.checkShaderErrors;
        const target=new THREE.WebGLRenderTarget(1,1,{depthBuffer:true});
        const scene=new THREE.Scene(); const camera=new THREE.PerspectiveCamera(45,1,0.01,100000);
        camera.position.set(0,0,1000); camera.lookAt(0,0,0);
        scene.add(new THREE.AmbientLight(0xffffff,0.2),new THREE.PointLight(0xffffff,2.5,0,0));
        for(const template of [...candidate.templates.values(),...(candidate.ringTemplate?[candidate.ringTemplate]:[])])
        {
            const clone=template.clone(true); clone.traverse(child=>{if(child.isMesh)child.frustumCulled=false;}); scene.add(clone);
        }
        if(candidateView?.group) { scene.add(candidateView.group.clone(true)); }
        try
        {
            this.renderer.debug.checkShaderErrors=true;
            this.renderer.debug.onShaderError=()=>{throw new Error('Generated profile: GPU shader compilation failed.');};
            const faces=new Set(candidate.textures.filter(t=>t.isCubeTexture).flatMap(t=>t.images));
            for(const texture of candidate.textures) { if(!faces.has(texture)) { this.renderer.initTexture(texture); } }
            this.renderer.setRenderTarget(target); this.renderer.clear(); this.renderer.render(scene,camera);
            if(gl.getError()!==gl.NO_ERROR) { throw new Error('Generated profile: GPU allocation/draw failed.'); }
        }
        finally
        {
            this.renderer.setRenderTarget(previousTarget); this.renderer.debug.onShaderError=previousError; this.renderer.debug.checkShaderErrors=previousCheck;
            target.dispose(); scene.clear();
        }
    }

    applySnapshot(state, { receivedAt = performance.now(), resetInterpolation = false } = {})
    {
        this.requireLive();
        this.invalidateNativeZoomGesture();
        validateSnapshot(state);
        validateNavigationSnapshot(state);
        if (!Number.isFinite(receivedAt)) { throw new Error('M3: неверное время получения состояния.'); }
        const old = this.current;
        const newSession = old && old.state.sessionId !== state.sessionId;
        if (this.retiredSessions.has(state.sessionId)) { return false; }
        const refreshEqual = old && !newSession && state.sequence === old.state.sequence && (this.needsFreshSnapshot || (!this.fresh && resetInterpolation));
        if (old && !newSession && (state.sequence < old.state.sequence || (state.sequence === old.state.sequence && !refreshEqual))) { return false; }
        // Снапшот принадлежит renderer: внешняя мутация не меняет picking/видимость.
        const ownedState = structuredClone(state);
        const nextView = this.view.kind === 'system' && !state.spatial.systems.some(item => item.id === this.view.systemId) ? { kind: 'galaxy' } : this.view;
        const viewChanged = nextView !== this.view;
        const candidate = this.buildView(this.resources, ownedState, nextView);
        this.previous = !resetInterpolation && !newSession && this.fresh ? old : null;
        this.current = { state: ownedState, receivedAt };
        if (!this.contextLost) { this.needsFreshSnapshot = false; }
        this.view = nextView;
        if (newSession)
        {
            this.retiredSessions.add(old.state.sessionId);
            this.profileReady = false;
            this.selection = null;
            this.commandFleetId = null;
            this.callbacks.onSelect?.(null);
        }
        else
        {
            const next = reconcileSelection(ownedState, this.selection, this.commandFleetId);
            if (this.selection && !next.selection) { this.callbacks.onSelect?.(null); }
            if (this.commandFleetId && !next.commandFleetId) { this.callbacks.onHint?.('Командный флот больше недоступен.'); }
            this.selection = next.selection;
            this.commandFleetId = next.commandFleetId;
        }
        this.replaceView(candidate);
        if (!old || newSession || viewChanged) { this.resetCamera(); }
        return true;
    }

    // Обновляет только локальные подписи уже принятого снапшота, сохраняя сетевой guard.
    refreshSpatialNames(state)
    {
        this.requireLive();
        if (!this.current || state?.sessionId !== this.current.state.sessionId || state.sequence !== this.current.state.sequence) { return false; }
        const ownedState = structuredClone(this.current.state);
        for (const kind of ['systems', 'bodies'])
        {
            const names = new Map((state.spatial?.[kind] ?? []).map(item => [item.id, item.name]));
            for (const item of ownedState.spatial[kind])
            {
                const name = names.get(item.id);
                if (typeof name === 'string') { item.name = name; }
            }
        }
        const candidate = this.buildView(this.resources, ownedState, this.view);
        this.current = { ...this.current, state: ownedState };
        this.replaceView(candidate);
        return true;
    }

    showGalaxy() { this.switchView({ kind: 'galaxy' }); }
    showSystem(systemId)
    {
        if (!this.current?.state.spatial.systems.some(item => item.id === systemId)) { throw new Error('M3: система отсутствует в разрешённой проекции.'); }
        this.switchView({ kind: 'system', systemId });
    }
    showStress200() { this.switchView({ kind: 'stress' }); }

    select(selection)
    {
        this.requireLive();
        const next = reconcileSelection(this.current?.state, selection, this.commandFleetId);
        if (selection && !next.selection) { throw new Error('M3: выбранный объект недоступен.'); }
        this.selection = next.selection;
        this.logicalPivot = this.resolvePivot();
        if (selection?.kind === 'fleet' && ownFleet(this.current?.state, selection.id)) { this.commandFleetId = selection.id; }
        this.callbacks.onSelect?.(this.selection ? { ...this.selection } : null);
    }

    setCommandFleet(fleetId)
    {
        this.requireLive();
        if (fleetId !== null && !ownFleet(this.current?.state, fleetId)) { throw new Error('M3: командный флот не принадлежит текущему игроку.'); }
        this.commandFleetId = fleetId;
    }

    setConnectionFresh(fresh)
    {
        this.requireLive();
        if (typeof fresh !== 'boolean') { throw new Error('M3: неверный статус соединения.'); }
        if (!fresh || !this.fresh) { this.previous = null; }
        this.fresh = fresh && !this.needsFreshSnapshot;
    }

    setLabelInsets(insets)
    {
        this.requireLive();
        this.labelInsets = validateLabelInsets(insets);
    }

    resetCamera()
    {
        this.requireLive();
        this.invalidateNativeZoomGesture();
        if (this.view.kind !== 'stress') { this.setCameraPreset(this.view.kind === 'galaxy' ? 'galaxy-far' : 'system-overview'); return; }
        this.cancelPointer();
        const center = new THREE.Vector3();
        const radius = radiusAboutOrigin(this.viewResources?.bounds);
        this.controls.target.copy(center);
        this.camera.position.copy(center).add(new THREE.Vector3(1, 1.2, 1).normalize().multiplyScalar(radius * 3));
        this.camera.near = Math.max(0.01, radius / 1000);
        this.camera.far = Math.max(1000, radius * 100);
        this.camera.updateProjectionMatrix();
        this.controls.update();
    }

    setCameraPreset(id)
    {
        this.requireLive();
        this.invalidateNativeZoomGesture();
        if (!CAMERA_PRESET_IDS.includes(id) || !id.startsWith(`${this.view.kind}-`)) { throw new Error('M3: camera preset does not match the current view.'); }
        this.cancelPointer();
        const center = viewCenter(this.current?.state, this.view);
        let bounds;
        if (this.view.kind === 'galaxy')
        {
            bounds = new THREE.Box3();
            for (const system of this.current?.state.spatial.systems ?? []) { bounds.expandByPoint(new THREE.Vector3(system.galaxyPosition.x, system.galaxyPosition.y, system.galaxyPosition.z)); }
            bounds.expandByScalar(12);
        }
        else if (id === 'system-close')
        {
            bounds = new THREE.Box3();
            for (const body of this.current?.state.spatial.bodies ?? [])
            {
                if (body.systemId !== this.view.systemId || !['star','planet','moon'].includes(body.kind)) { continue; }
                const object = this.viewResources?.objects.get(`body:${body.id}`);
                if (object) { bounds.union(new THREE.Box3().setFromObject(object)); }
            }
        }
        else
        {
            const radius = this.current?.state.spatial.systems?.find(item => item.id === this.view.systemId)?.radius ?? 150;
            bounds = new THREE.Box3(new THREE.Vector3(-radius, -8, -radius), new THREE.Vector3(radius, 8, radius));
        }
        if (bounds.isEmpty()) { bounds = new THREE.Box3(center.clone().addScalar(-150), center.clone().addScalar(150)); }
        const direction = this.view.kind === 'galaxy' ? new THREE.Vector3(0, 1, 0.35) : new THREE.Vector3(0, 0.8, 1);
        // Overview radius охватывает источники объектов, в том числе внешние мегаструктуры.
        const bodies = (this.current?.state.spatial.bodies ?? []).filter(item => item.systemId === this.view.systemId && ['star', 'planet', 'moon'].includes(item.kind));
        const gateways=(this.current?.state.spatial.gateways??[]).filter(item=>item.systemId===this.view.systemId);
        const wormholes=(this.current?.state.spatial.wormholes??[]).filter(item=>item.systemId===this.view.systemId);
        const contentRadius = id === 'system-overview' && (gateways.length || wormholes.length || bodies.length && this.resources) ? Math.max(40,
            ...(this.resources ? bodies.map(body => Math.hypot(body.localPosition.x, body.localPosition.z) + Math.max(displayedSurfaceRadius(this.resources.templates.get(body.visualId), body.visualId, body.scale),displayedRingRadius(this.resources.ringTemplate,this.resources.templates.get(body.visualId),body)) + 12) : []),
            ...gateways.map(gateway=>Math.hypot(gateway.localPosition.x,gateway.localPosition.z)+6+12),
            ...wormholes.map(wormhole=>Math.hypot(wormhole.localPosition.x,wormhole.localPosition.z)+6+12)) : this.current?.state.spatial.systems?.find(item => item.id === this.view.systemId)?.radius ?? 150;
        const beltRadius = Math.max(0, ...(this.current?.state.spatial.belts ?? []).filter(b => b.systemId === this.view.systemId).map(beltOuterRadius));
        const preset = id === 'system-overview' ? createSystemOverviewPreset(Math.max(contentRadius, beltRadius + 12), this.camera.aspect) : createCameraPreset(id, { bounds, aspect: this.camera.aspect, fov: 45, direction, padding: 1.15, distanceScale: id === 'galaxy-near' ? 0.6 : 1 });
        this.controls.target.copy(preset.target);
        this.camera.position.copy(preset.position);
        this.camera.fov = 45;
        this.camera.near = 0.01;
        this.camera.far = Math.max(10000, preset.distance * 10);
        this.camera.updateProjectionMatrix();
        this.controls.update();
        this.cameraPreset = id;
    }

    focusSelection()
    {
        this.requireLive();
        this.invalidateNativeZoomGesture();
        const object = this.selectedDisplayObject();
        if (!object) { this.callbacks.onHint?.('Выбранный объект отсутствует в этом виде.'); return; }
        const target = object.getWorldPosition(new THREE.Vector3());
        const offset = this.camera.position.clone().sub(this.controls.target);
        this.controls.target.copy(target);
        this.camera.position.copy(target).add(offset);
        this.controls.update();
    }

    resize()
    {
        this.requireLive();
        this.invalidateNativeZoomGesture();
        const rect = this.canvas.getBoundingClientRect();
        const width = Math.max(1, Math.round(rect.width));
        const height = Math.max(1, Math.round(rect.height));
        this.renderer.setSize(width, height, false);
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
    }

    render(now = performance.now())
    {
        this.requireLive();
        if (this.contextLost) { return; }
        const started = performance.now();
        if (this.current && this.view.kind === 'system')
        {
            for (const [id, object] of this.viewResources?.fleets ?? [])
            {
                const position = interpolateFleet(this.previous, this.current, id, now);
                if (position) { object.position.set(position.x, 0, position.z); }
            }
        }
        this.controls.update();
        this.camera.updateMatrixWorld();
        this.scene.updateMatrixWorld(true);
        if (this.current && this.view.kind !== 'stress')
        {
            this.detailLevel = zoomDetail(this.view.kind, this.camera.position.distanceTo(this.controls.target) / navigationScale(this.current.state, this.view), this.detailLevel);
        }
        for (const [id, group] of this.viewResources?.routes ?? []) { group.visible = id === this.commandFleetId || this.selection?.kind === 'fleet' && this.selection.id === id; }
        const rect = this.canvas.getBoundingClientRect();
        if(this.view.kind==='galaxy')
        {
            for(const glyph of this.viewResources?.galaxyGlyphs??[]) { updateGalaxyGlyphScale(glyph,this.camera,rect.height); }
        }
        const insets = this.labelInsets;
        this.labelAreaEmpty = rect.width <= insets.left + insets.right || rect.height <= insets.top + insets.bottom;
        const pixelsPerUnit = rect.height / (2 * Math.tan(THREE.MathUtils.degToRad(this.camera.fov) * 0.5));
        const selectedObject = this.selectedDisplayObject();
        for (const highlight of this.viewResources?.highlights ?? [])
        {
            const selected = highlight.object === selectedObject;
            const command = highlight.pick.kind === 'fleet' && highlight.pick.id === this.commandFleetId;
            const hover = highlight.pick.kind === 'system' && highlight.pick.id === this.hoverSystemId;
            highlight.mesh.visible = selected || command || hover;
            if (!highlight.mesh.visible) { continue; }
            const point = highlight.object.getWorldPosition(new THREE.Vector3());
            highlight.mesh.position.copy(point); highlight.mesh.quaternion.copy(this.camera.quaternion);
            const depth = -point.clone().applyMatrix4(this.camera.matrixWorldInverse).z;
            highlight.mesh.visible = depth > 0;
            highlight.mesh.scale.setScalar(Math.max(0.01, depth * SYSTEM_HIT_RADIUS / pixelsPerUnit));
            highlight.mesh.material.color.set(selected ? '#ffe39c' : command ? '#70f4bc' : '#d5f3ef');
        }
        const galaxyLabels = [];
        const state = this.current?.state;
        const perspectiveOwner = state?.viewContext?.mode === 'player' ? state.viewContext.participantId : state?.you;
        const importantSystems = new Set((state?.spatial?.colonies ?? []).filter(colony => colony.ownerParticipantId === perspectiveOwner).map(colony => state.spatial.bodies.find(body => body.id === colony.bodyId)?.systemId));
        for (const fleet of state?.spatial?.fleets ?? [])
        {
            if (fleet.id === this.commandFleetId || this.selection?.kind === 'fleet' && fleet.id === this.selection.id)
            { for (const id of fleet.movement?.routeSystemIds ?? []) { importantSystems.add(id); } }
        }
        for (const label of this.viewResources?.labels ?? [])
        {
            const point = label.getWorldPosition(new THREE.Vector3());
            const selected = selectedObject && this.viewResources.objects.get(`${label.userData.kind}:${label.userData.objectId}`) === selectedObject;
            const hovered = this.view.kind === 'galaxy' && label.userData.kind === 'system' && this.hoverSystemId === label.userData.objectId;
            const command = label.userData.kind === 'fleet' && this.commandFleetId === label.userData.objectId;
            const inspected = selected || this.selection?.kind === label.userData.kind && this.selection.id === label.userData.objectId;
            const important = label.userData.kind === 'system' && importantSystems.has(label.userData.objectId);
            const priority = this.detailLevel === 'near' || inspected || hovered || ['exit', 'nebula'].includes(label.userData.kind) || command || important;
            const halfWidth = label.scale.x * pixelsPerUnit * label.userData.textWidth * 0.5 + 3;
            const halfHeight = label.scale.y * pixelsPerUnit * 0.5;
            label.visible = priority && !this.labelAreaEmpty && labelFitsViewport(point.project(this.camera), halfWidth, halfHeight, rect.width, rect.height, insets);
            if (label.visible)
            {
                const x = (point.x + 1) * rect.width * 0.5, y = (1 - point.y) * rect.height * 0.5;
                galaxyLabels.push({ key: `${label.userData.kind}:${label.userData.objectId}`, priority: inspected ? 5 : hovered ? 4 : command ? 3 : important ? 2 : label.userData.kind === 'exit' ? 1 : 0, left: x - halfWidth, top: y - halfHeight, right: x + halfWidth, bottom: y + halfHeight, label });
            }
        }
        const visibleGalaxyLabels = selectVisibleLabelKeys(galaxyLabels);
        for (const candidate of galaxyLabels) { candidate.label.visible = visibleGalaxyLabels.has(candidate.key); }
        this.renderer.render(this.scene, this.camera);
        this.frameMilliseconds = performance.now() - started;
    }

    stats()
    {
        return { ...this.renderer.info.memory, ...this.renderer.info.render, convertedBytes: this.resources?.convertedBytes ?? 0, gpuReservationBytes:this.resources?.gpuReservationBytes??0, frameMilliseconds: this.frameMilliseconds, viewport: [this.canvas.width, this.canvas.height], view: this.view.kind, cameraPreset: this.cameraPreset ?? null, detailLevel: this.detailLevel ?? 'far', labelAreaEmpty: this.labelAreaEmpty ?? false, contextLost: this.contextLost };
    }

    dispose()
    {
        if (this.disposed) { return; }
        this.invalidateNativeZoomGesture();
        this.cancelPointer();
        this.disposed = true;
        for (const [type, handler, capture, target] of this.handlers) { target.removeEventListener(type, handler, capture); }
        this.handlers.length = 0;
        this.controls.dispose();
        // Logout/pageshow создают renderer на том же canvas и WebGL-контексте.
        // Вернуть pixel-store и GL bindings к defaults до освобождения ресурсов.
        this.renderer.resetState();
        this.releaseView(this.viewResources);
        this.releaseProfile(this.resources);
        this.scene.clear();
        this.renderer.renderLists.dispose();
        this.renderer.dispose();
        this.resources = null;
        this.profile = null;
        this.current = null;
        this.previous = null;
        this.viewResources = null;
        this.callbacks = {};
        this.retiredSessions.clear();
    }

    requireLive() { if (this.disposed) { throw new Error('M3: renderer уже освобождён.'); } }
    listen(type, handler, capture = false, target = this.canvas) { target.addEventListener(type, handler, capture); this.handlers.push([type, handler, capture, target]); }
    selectedDisplayObject()
    {
        let selection = this.selection;
        if (selection?.kind === 'colony')
        {
            const colony = this.current?.state.spatial.colonies.find(item => item.id === selection.id);
            selection = colony ? { kind: 'body', id: colony.bodyId } : null;
        }
        return this.viewResources?.objects.get(selection ? `${selection.kind}:${selection.id}` : '');
    }
    resolvePivot() { return this.selectedDisplayObject()?.getWorldPosition(new THREE.Vector3()) ?? (this.view?.kind === 'stress' ? new THREE.Vector3() : viewCenter(this.current?.state, this.view)); }
    pointerDown(event)
    {
        this.nativePointers ??= new Map();
        this.nativePointers.set(event.pointerId, { type: event.pointerType, button: event.button });
        this.rejectDoubleClick = false;
        if (this.pointer)
        {
            if (this.orbit) { event.preventDefault(); event.stopImmediatePropagation(); }
            return;
        }
        this.pointer = { id: event.pointerId, button: event.button, x: event.clientX, y: event.clientY, distance: 0, alt: event.altKey };
        if (event.altKey && event.button === 0)
        {
            event.preventDefault();
            event.stopImmediatePropagation();
            this.logicalPivot = this.resolvePivot();
            this.orbit = { id: event.pointerId, pivot: this.logicalPivot.clone(), x: event.clientX, y: event.clientY, enabled: this.controls.enabled };
            this.controls.enabled = false;
            this.canvas.setPointerCapture(event.pointerId);
        }
    }
    pointerMove(event)
    {
        this.captureNativeZoomInput('pointermove', event);
        if (!this.pointer && this.view.kind === 'galaxy') { this.hoverSystemId = event.altKey ? null : this.pick(event, 'system')?.id ?? null; }
        if (this.pointer?.id !== event.pointerId) { return; }
        this.pointer.distance = Math.max(this.pointer.distance, Math.hypot(event.clientX - this.pointer.x, event.clientY - this.pointer.y));
        if (!this.orbit || this.orbit.id !== event.pointerId) { return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        if (!event.altKey) { this.finishOrbit(); return; }
        orbitAboutPivot(this.camera, this.controls.target, this.orbit.pivot, event.clientX - this.orbit.x, event.clientY - this.orbit.y);
        this.orbit.x = event.clientX;
        this.orbit.y = event.clientY;
        this.controls.update();
    }
    finishOrbit()
    {
        const orbit = this.orbit;
        if (!orbit) { return; }
        this.orbit = null;
        this.controls.enabled = orbit.enabled;
        if (this.canvas.hasPointerCapture?.(orbit.id)) { this.canvas.releasePointerCapture(orbit.id); }
    }
    cancelPointer(event)
    {
        this.invalidateNativeZoomGesture();
        this.nativePointers ??= new Map();
        if (event) { this.nativePointers.delete(event.pointerId); }
        else { this.nativePointers.clear(); }
        if (event && this.pointer?.id !== event.pointerId) { return; }
        const pointer = this.pointer;
        const nativeGesture = pointer && !this.orbit;
        this.pointer = null;
        this.rejectDoubleClick = true;
        this.suppressDoubleClickUntil = performance.now() + 500;
        this.finishOrbit();
        if (nativeGesture)
        {
            // Public disconnect сбрасывает native pointers/state и document handlers.
            // В отличие от update он не изменяет camera/target и не применяет damping.
            this.controls.disconnect();
            this.controls.connect(this.canvas);
            if (this.canvas.hasPointerCapture?.(pointer.id)) { this.canvas.releasePointerCapture(pointer.id); }
        }
    }
    switchView(view)
    {
        this.requireLive();
        this.invalidateNativeZoomGesture();
        const candidate = this.buildView(this.resources, this.current?.state, view);
        this.view = view;
        this.detailLevel = 'far';
        this.hoverSystemId = null;
        this.replaceView(candidate);
        this.resetCamera();
    }

    captureNativeZoomInput(kind, event)
    {
        if (this.view.kind !== 'system' || !this.controls?.enabled || !this.controls.enableZoom) { return; }
        this.nativePointers ??= new Map();
        const types = [...this.nativePointers.values()];
        const isTouchDolly = types.length >= 2 && types.every(pointer => pointer.type === 'touch');
        const isMiddleDolly = types.some(pointer => pointer.type === 'mouse' && pointer.button === 1);
        if (kind === 'wheel' ? event?.deltaY === 0 : !isTouchDolly && !isMiddleDolly) { return; }
        const distance = this.camera.position.distanceTo(this.controls.target);
        if (!Number.isFinite(distance)) { return; }
        if (!this.nativeZoomEvents) { this.nativeZoomEvents = new WeakMap(); }
        this.nativeZoomEvents.set(event, { view: this.view, state: this.current?.state, before: distance, generation: this.nativeZoomGeneration });
    }

    finishNativeZoomInput(event)
    {
        const candidate = this.nativeZoomEvents?.get(event);
        this.nativeZoomEvents?.delete(event);
        if (!candidate) { return; }
        if (candidate.generation !== this.nativeZoomGeneration || candidate.view !== this.view || candidate.state !== this.current?.state || this.view.kind !== 'system') { return; }
        const distance = this.camera.position.distanceTo(this.controls.target);
        if (!Number.isFinite(distance) || distance <= candidate.before) { return; }
        const system = this.current?.state?.spatial?.systems?.find(item => item.id === this.view.systemId);
        let threshold;
        try { threshold = createSystemOverviewPreset(system?.radius, this.camera.aspect).distance * 1.5; }
        catch { this.callbacks.onHint?.('Возврат к галактике недоступен: не определены масштаб системы или пропорции окна.'); return; }
        if (distance >= threshold) { this.switchToGalaxyAfterZoomOut(); }
    }

    invalidateNativeZoomGesture() { this.nativeZoomGeneration = (this.nativeZoomGeneration ?? 0) + 1; }

    switchToGalaxyAfterZoomOut()
    {
        this.switchView({ kind: 'galaxy' });
        this.callbacks.onSystemZoomOut?.();
    }
    rebuild() { this.replaceView(this.buildView(this.resources, this.current?.state, this.view)); }
    replaceView(candidate)
    {
        this.releaseView(this.viewResources);
        this.viewResources = candidate;
        this.logicalPivot = this.resolvePivot();
        this.scene.add(candidate.group);
        if (this.fillLight) { this.fillLight.intensity = this.view.kind === 'system' ? 0 : 2; }
        this.renderer.renderLists.dispose();
    }
    releaseView(view)
    {
        if (!view) { return; }
        view.group.removeFromParent();
        for (const resource of view.owned) { resource.dispose(); }
        view.group.clear();
    }
    releaseProfile(profile)
    {
        if (!profile) { return; }
        for (const resource of [...profile.geometries, ...profile.materials, ...profile.textures]) { resource.dispose(); }
    }

    prepareTemplate(sample, resources)
    {
        const group = new THREE.Group();
        group.scale.setScalar(sample.scale ?? 1);
        if(sample.sourceClassId)
        {
            group.userData.sourceClassId=sample.sourceClassId; group.userData.sourceKind=sample.bodyKind; group.userData.sourceEntityName=sample.sourceEntityName;
            if(sample.sourceAtmosphereColor) { group.userData.sourceAtmosphereColor=[...sample.sourceAtmosphereColor]; }
            if(sample.starSurface?.atmosphereColor) { group.userData.starSurfaceAtmosphereColor=[...sample.starSurface.atmosphereColor]; }
        }
        if (sample.position) { group.position.fromArray(sample.position); }
        if (sample.quaternion) { group.quaternion.fromArray(sample.quaternion).normalize(); }
        for (const [index, mesh] of sample.meshes.entries())
        {
            const geometry = new THREE.BufferGeometry();
            resources.geometries.push(geometry);
            geometry.setAttribute('position', new THREE.BufferAttribute(mesh.positions, 3));
            if (mesh.normals) { geometry.setAttribute('normal', new THREE.BufferAttribute(mesh.normals, 3)); }
            if (mesh.uv) { geometry.setAttribute('uv', new THREE.BufferAttribute(mesh.uv, 2)); }
            geometry.setIndex(new THREE.BufferAttribute(mesh.indices, 1));
            geometry.computeBoundingSphere();
            const material = this.makeMaterial(mesh.material, resources, sample);
            resources.materials.push(material);
            const drawable = new THREE.Mesh(geometry, material);
            drawable.name = `${mesh.name}:${index}`;
            drawable.userData.sourceShader = mesh.material.shader;
            drawable.renderOrder = mesh.name.includes('shadow') ? 1 : mesh.name.includes('poles') ? 2 : mesh.name.includes('clouds') ? 3 : mesh.name.includes('atmosphere') ? 4 : 0;
            group.add(drawable);
        }
        for(const attachment of sample.attachments??[])
        {
            const child=this.prepareTemplate({...attachment,attachments:[]},resources);
            const anchor=new THREE.Group(); anchor.matrix.fromArray(attachment.matrix); anchor.matrixAutoUpdate=false; anchor.add(child); group.add(anchor);
        }
        if (new THREE.Box3().setFromObject(group).isEmpty()) { throw new Error('M3: пустая геометрия профиля.'); }
        return group;
    }

    texture(path, mode, resources)
    {
        const key = `${path}:${mode}`;
        if (resources.textureMap.has(key)) { return resources.textureMap.get(key); }
        const image = resources.images.get(path);
        if (!image) { throw new Error('M3: отсутствует декодированная текстура.'); }
        if (mode === 'starNoise')
        {
            const texture = createDdsCubeTexture(image);
            resources.textures.push(...texture.images, texture);
            resources.textureMap.set(key, texture);
            return texture;
        }
        if (image.isCubemap) { throw new Error('M3: a cubemap cannot be used as a 2D material channel.'); }
        const converted = mode === 'normal' ? normalRrxg(image) : mode === 'specular' ? specularApprox(image) : image;
        const texture = new THREE.DataTexture(converted.rgba, converted.width, converted.height, THREE.RGBAFormat);
        resources.textures.push(texture);
        texture.colorSpace = mode === 'diffuse' ? THREE.SRGBColorSpace : THREE.NoColorSpace;
        texture.flipY = false;
        texture.generateMipmaps = true;
        texture.minFilter = THREE.LinearMipmapLinearFilter;
        texture.magFilter = THREE.LinearFilter;
        if (mode === 'starSurface') { texture.wrapS = texture.wrapT = THREE.RepeatWrapping; }
        texture.needsUpdate = true;
        resources.textureMap.set(key, texture);
        return texture;
    }

    makeMaterial(material, resources, sample)
    {
        const shader = material.shader;
        if (shader === 'PdxMeshAtmosphereStar')
        {
            const radius = mesh =>
            {
                let value = 0;
                for (let index = 0; index < mesh.positions.length; index += 3) { value = Math.max(value, Math.hypot(mesh.positions[index], mesh.positions[index + 1], mesh.positions[index + 2])); }
                return value;
            };
            const layers=findAtmosphereStarLayers(sample);
            if (!layers) { throw new Error('M3: star corona requires both source layers.'); }
            const {surface,atmosphere}=layers;
            return new THREE.ShaderMaterial(createStarCoronaParameters(radius(surface), radius(atmosphere), sample.sourceClassId&&sample.starSurface?{tint:sourceHsvRgb(sample.starSurface.atmosphereColor)}:sample.sourceClassId&&sample.sourceAtmosphereColor?{tint:sourceHsvRgb(sample.sourceAtmosphereColor)}:{}));
        }
        if (shader === 'PdxMeshAtmosphere')
        {
            return new THREE.ShaderMaterial({
                uniforms: { tint: { value: sample.sourceAtmosphereColor?new THREE.Color().setRGB(...sourceHsvRgb(sample.sourceAtmosphereColor)):new THREE.Color('#549ed5') } },
                vertexShader: 'varying vec3 viewNormal; varying vec3 viewPosition; void main(){vec4 p=modelViewMatrix*vec4(position,1.0);viewNormal=normalize(normalMatrix*normal);viewPosition=p.xyz;gl_Position=projectionMatrix*p;}',
                fragmentShader: 'uniform vec3 tint; varying vec3 viewNormal; varying vec3 viewPosition; void main(){float ndotl=max(dot(normalize(viewNormal),normalize(viewPosition)),0.0);float intensity=pow(ndotl*1.2,4.0);gl_FragColor=vec4(tint,intensity);}',
                transparent: true, blending: THREE.AdditiveBlending, depthTest: true, depthWrite: false, side: THREE.BackSide
            });
        }
        if (shader === 'PdxMeshStar')
        {
            if (!sample?.starSurface || !Number.isFinite(sample.scale) || sample.scale <= 0) { throw new Error('M3: star surface requires validated metadata and a positive uniform scale.'); }
            const surface = sample.starSurface;
            const parameters = createStaticGStarParameters(surface, {
                lavaNoise: this.texture(surface.noisePath, 'starNoise', resources),
                lavaDiffuse: this.texture(surface.lavaPath, 'starSurface', resources),
                stoneDiffuse: this.texture(surface.stonePath, 'starSurface', resources)
            });
            return new THREE.ShaderMaterial({ ...parameters, side: THREE.DoubleSide });
        }
        if(['PdxMeshPlanetRings','PdxMeshPlanetEmissive','PdxMeshGasGiant','PdxMeshMoltenPlanet','PdxMeshAlphaAdditiveAnimateUV','PdxMeshAlphaAdditiveAnimateUVNoDissolve','PdxMeshBlackHole','PdxMeshBlackHoleBillboard','PdxMeshAlphaAdditive','PdxMeshTerra'].includes(shader)) { return createGeneratedMaterial(material,(path,mode)=>this.texture(path,mode,resources)); }
        if (!['PdxMeshShip', 'PdxMeshPlanet', 'PdxMeshClouds'].includes(shader)) { throw new Error('M3: нет статического адаптера shader.'); }
        const clouds = shader === 'PdxMeshClouds';
        return new THREE.MeshPhongMaterial({ map: this.texture(material.diff, 'diffuse', resources), normalMap: this.texture(material.n, 'normal', resources), specularMap: this.texture(material.spec, 'specular', resources), specular: 0xffffff, shininess: 30, side: THREE.DoubleSide, transparent: clouds, depthWrite: !clouds });
    }

    buildView(profile, state, view)
    {
        const result = { group: new THREE.Group(), owned: new Set(), picks: [], objects: new Map(), fleets: new Map(), labels: [], routes: new Map(), highlights: [], galaxyGlyphs: [], bounds: new THREE.Box3() };
        try
        {
            if(profile&&state) { validateGeneratedSnapshotBindings(profile,state); }
            if (view.kind === 'stress')
            {
                if (!profile) { throw new Error('M3: сначала импортируйте профиль ассетов.'); }
                const template = profile.templates.get('ship.corvette.mammalian');
                template.updateMatrixWorld(true);
                for (const child of template.children)
                {
                    const instances = new THREE.InstancedMesh(child.geometry, child.material, 200);
                    result.owned.add(instances);
                    instances.renderOrder = child.renderOrder;
                    const matrix = new THREE.Matrix4();
                    for (let index = 0; index < 200; index++)
                    {
                        matrix.makeTranslation((index % 20 - 9.5) * 18, 0, (Math.floor(index / 20) - 4.5) * 18).multiply(child.matrixWorld);
                        instances.setMatrixAt(index, matrix);
                    }
                    instances.instanceMatrix.needsUpdate = true;
                    instances.computeBoundingSphere();
                    result.group.add(instances);
                }
            }
            else if (state)
            {
                const descriptors = collectViewObjects(state, view);
                const sphere = new THREE.SphereGeometry(view.kind === 'galaxy' ? 3 : 2, 12, 8);
                result.owned.add(sphere);
                const markerMaterials = new Map();
                const markerMaterial = color =>
                {
                    if (!markerMaterials.has(color)) { const material = new THREE.MeshBasicMaterial({ color, transparent: true }); markerMaterials.set(color, material); result.owned.add(material); }
                    return markerMaterials.get(color);
                };
                const ownershipColor = owner => owner == null ? '#b8cbe1' : owner === state.you ? '#69e6c0' : '#ffad75';
                for (const descriptor of descriptors)
                {
                    const object = new THREE.Group();
                    object.position.set(descriptor.position.x, descriptor.position.y, descriptor.position.z);
                    object.userData.pick = { kind: descriptor.kind, id: descriptor.id };
                    result.objects.set(`${descriptor.kind}:${descriptor.id}`, object);
                    if (view.kind === 'galaxy')
                    {
                        const system = state.spatial.systems.find(item => item.id === descriptor.id);
                        const sourceLight = descriptor.kind === 'system' ? profile?.systemLighting?.get(system?.starClassId) : null;
                        const color = sourceLight ? new THREE.Color().setRGB(...sourceLight.color).getHexString() : descriptor.kind === 'system' ? 'ffe5ae' : ownershipColor(descriptor.ownerParticipantId).slice(1);
                        if (descriptor.kind === 'system')
                        {
                            const components=state.spatial.bodies.filter(body=>body.systemId===descriptor.id&&body.kind==='star');
                            if(!components.length){const marker=new THREE.Mesh(sphere,markerMaterial(`#${color}`));marker.renderOrder=2;object.add(marker);result.picks.push(marker);}
                            else
                            {
                                const glyph=new THREE.Group();glyph.name=`stellar-glyph:${descriptor.id}`;glyph.userData.pick=object.userData.pick;
                                const radii=components.map(component=>['pc_neutron_star','pc_pulsar'].includes(component.worldClassId) ? .22 : .28);
                                const count=components.length,orbit=count===1?0:count===2?1.2:Math.max(1.2,.96/Math.sin(Math.PI/count));
                                const glowComponents=[];let screenBoundRadius=0;
                                const componentColor=body=>
                                {
                                    if(profile?.generatedBindings?.size)
                                    {
                                        const classId=body.worldClassId,visualId=`world.${classId}`,binding=profile.generatedBindings.get(visualId),template=profile.templates.get(visualId);
                                        if(!binding||binding.classId!==classId||template?.userData.sourceClassId!==classId) { throw new Error('M3: generated stellar glyph source binding mismatch.'); }
                                        const hsv=template.userData.sourceAtmosphereColor??template.userData.starSurfaceAtmosphereColor;
                                        if(!Array.isArray(hsv)||hsv.length!==3||hsv.some(value=>!Number.isFinite(value))||hsv[1]<0||hsv[1]>1||hsv[2]<0) { throw new Error('M3: generated stellar glyph requires source HSV.'); }
                                        const rgb=sourceHsvRgb([hsv[0],hsv[1],Math.min(1,hsv[2])]),converted=new THREE.Color().setRGB(...rgb);
                                        return {hex:converted.getHexString(),linear:converted.toArray()};
                                    }
                                    const world=String(body.worldClassId??'').replace(/^pc_/,''),legacy=profile?.systemLighting&&[...profile.systemLighting.values()].find(item=>item.world===world);
                                    const converted=legacy?new THREE.Color().setRGB(...legacy.color):new THREE.Color(`#${color}`);
                                    return {hex:converted.getHexString(),linear:converted.toArray()};
                                };
                                for(let index=0;index<components.length;index++)
                                {
                                    const component=components[index],world=String(component.worldClassId??'').replace(/^pc_/,''),hole=world==='black_hole',neutron=world==='neutron_star',pulsar=world==='pulsar';
                                    const angle=count===1?0:count===2?index*Math.PI:Math.PI/2+index*Math.PI*2/count,x=Math.cos(angle)*orbit,z=Math.sin(angle)*orbit;
                                    const radius=radii[index],sourceColor=componentColor(component),material=markerMaterial(`#${sourceColor.hex}`);
                                    glowComponents.push({x,y:0,z,color:sourceColor.linear});screenBoundRadius=Math.max(screenBoundRadius,Math.hypot(x,z)+3*radius);
                                    const dot=new THREE.Mesh(sphere,hole?markerMaterial('#05070b'):material);dot.scale.setScalar(radius);dot.position.set(x,0,z);dot.renderOrder=2;glyph.add(dot);result.picks.push(dot);
                                    if(hole)
                                    {
                                        const ringGeometry=new THREE.RingGeometry(.95,1.1,24);result.owned.add(ringGeometry);const ring=new THREE.Mesh(ringGeometry,material);ring.rotation.x=-Math.PI/2;ring.position.set(x,0,z);ring.renderOrder=2;glyph.add(ring);result.picks.push(ring);screenBoundRadius=Math.max(screenBoundRadius,Math.hypot(x,z)+1.1);
                                    }
                                    else if(neutron)
                                    {
                                        const haloGeometry=new THREE.TorusGeometry(.9,.1,6,20);result.owned.add(haloGeometry);const halo=new THREE.Mesh(haloGeometry,markerMaterial('#d8f2ff'));halo.rotation.x=Math.PI/2;halo.position.set(x,0,z);halo.renderOrder=2;glyph.add(halo);result.picks.push(halo);screenBoundRadius=Math.max(screenBoundRadius,Math.hypot(x,z)+1);
                                    }
                                    else if(pulsar)
                                    {
                                        const points=[];for(let tick=0;tick<4;tick++){const a=tick*Math.PI/2;points.push(x+Math.cos(a)*.94,0,z+Math.sin(a)*.94,x+Math.cos(a)*1.28,0,z+Math.sin(a)*1.28);}
                                        const geometry=new THREE.BufferGeometry();geometry.setAttribute('position',new THREE.Float32BufferAttribute(points,3));const ticks=new THREE.LineSegments(geometry,new THREE.LineBasicMaterial({color:'#e7f7ff',transparent:true,opacity:.9,depthTest:false}));ticks.renderOrder=2;glyph.add(ticks);result.owned.add(geometry);result.owned.add(ticks.material);result.picks.push(ticks);screenBoundRadius=Math.max(screenBoundRadius,Math.hypot(x,z)+1.28);
                                    }
                                }
                                glyph.userData.screenBoundRadius=screenBoundRadius;
                                const glow=createGalaxyGlow(glowComponents);glyph.add(glow);result.owned.add(glow.geometry);result.owned.add(glow.material);
                                object.add(glyph);result.galaxyGlyphs.push(glyph);
                            }
                            for(const wormhole of (state.spatial.wormholes??[]).filter(item=>item.systemId===descriptor.id))
                            {
                                const glyph=new THREE.Group();glyph.name=`wormhole-glyph:${wormhole.id}`;
                                glyph.userData.pick={kind:'wormhole',id:wormhole.id};glyph.userData.screenBoundRadius=1.2;glyph.userData.isWormholeGlyph=true;glyph.userData.separationClearance=1.5;
                                glyph.userData.separateFromGlyph=object.children.find(child=>child.name===`stellar-glyph:${descriptor.id}`)??null;
                                for(const offset of [-.38,.38])
                                {
                                    const ringGeometry=new THREE.RingGeometry(.34,.46,20);result.owned.add(ringGeometry);
                                    const ring=new THREE.Mesh(ringGeometry,markerMaterial('#75e8f2'));ring.rotation.x=-Math.PI/2;ring.position.set(offset,1.7,0);ring.renderOrder=3;
                                    ring.userData.pick={kind:'wormhole',id:wormhole.id};glyph.add(ring);result.picks.push(ring);
                                }
                                object.add(glyph);result.galaxyGlyphs.push(glyph);result.objects.set(`wormhole:${wormhole.id}`,glyph);
                            }
                        }
                        else
                        {
                            const marker = new THREE.Mesh(sphere, markerMaterial(`#${color}`)); marker.renderOrder=2;
                            if (descriptor.kind === 'fleet') { marker.scale.setScalar(0.55); marker.position.y = 5; }
                            object.add(marker); result.picks.push(marker);
                        }
                    }
                    else if (profile)
                    {
                        const template = profile.templates.get(descriptor.visualId);
                        if (!template) { throw new Error('M3: visualId отсутствует в проверенном наборе.'); }
                        const count = descriptor.kind === 'fleet' ? descriptor.shipCount : 1;
                        if(descriptor.kind==='gateway'||descriptor.kind==='wormhole')
                        {
                            const clone=template.clone(true);
                            clone.updateMatrixWorld(true);
                            const bounds=new THREE.Box3().setFromObject(clone),size=bounds.getSize(new THREE.Vector3());
                            const surfaceRadius=Math.max(size.x,size.y,size.z)/2;
                            if(!Number.isFinite(surfaceRadius)||surfaceRadius<=0) { throw new Error('M3: gateway source bounds отсутствуют.'); }
                            clone.scale.multiplyScalar(6/surfaceRadius);
                            if(descriptor.kind==='gateway') { clone.position.y+=4; }
                            object.add(clone);
                            clone.traverse(child=>{if(child.isMesh)result.picks.push(child);});
                            result.group.add(object);
                            continue;
                        }
                        for (let index = 0; index < count; index++)
                        {
                            const clone = template.clone(true);
                            const displayFactor = templateDisplayFactor(template, descriptor.visualId);
                            clone.scale.multiplyScalar(descriptor.scale * displayFactor);
                            clone.position.multiplyScalar(displayFactor);
                            if (descriptor.kind === 'fleet')
                            {
                                if (!CORVETTE_OFFSETS[index]) { throw new Error('M3: нет согласованного декоративного строя для этого флота.'); }
                                clone.position.x += CORVETTE_OFFSETS[index][0];
                                clone.position.z += CORVETTE_OFFSETS[index][1];
                            }
                            object.add(clone);
                            clone.traverse(child => { if (child.isMesh) { result.picks.push(child); } });
                            const body=descriptor.kind==='body'?state.spatial.bodies.find(b=>b.id===descriptor.id):null;
                            if(body?.hasRing)
                            {
                                if(!profile.ringTemplate) { throw new Error('M3: отсутствует shared ring template.'); }
                                // SourceMeshRingDisplayV1: исходные координаты, body/display scale ровно один раз.
                                const ring=profile.ringTemplate.clone(true);
                                ring.name='planet-rings:'+body.id;
                                ring.scale.multiplyScalar(descriptor.scale*displayFactor);
                                object.add(ring);
                            }
                        }
                        if (descriptor.kind === 'fleet') { result.fleets.set(descriptor.id, object); }
                    }
                    result.group.add(object);
                }
                if (view.kind === 'system')
                {
                    if (profile) for (const belt of (state.spatial.belts ?? []).filter(b => b.systemId === view.systemId))
                    { result.group.add(createBeltGroup(belt, profile.templates.get(belt.visualId))); }
                    const system=state.spatial.systems.find(s=>s.id===view.systemId);
                    const sourceLight=profile?.systemLighting?.get(system?.starClassId);
                    if(sourceLight)
                    {
                        // Source RGB/intensity/position; Three units/attenuation остаются явной calibration.
                        const light=new THREE.PointLight(new THREE.Color().setRGB(...sourceLight.color),sourceLight.intensity,0,0);
                        light.position.fromArray(sourceLight.position); light.userData.sourceStarClassId=system.starClassId; light.userData.sourceLightName=sourceLight.lightName;
                        result.group.add(light);
                    }
                    else for(const star of descriptors.filter(item=>item.visualId==='star.g'))
                    {
                        // Browser light calibration: native intensity conversion is unknown.
                        const light = new THREE.PointLight(new THREE.Color(0.9, 0.9, 0.8), 2.5, 0, 0);
                        light.position.set(star.position.x, star.position.y, star.position.z);
                        result.group.add(light);
                    }
                    for (const colony of state.spatial.colonies)
                    {
                        const body = result.objects.get(`body:${colony.bodyId}`);
                        if (body) { result.objects.set(`colony:${colony.id}`, body); }
                    }
                }
                else
                {
                    const systems = new Map(state.spatial.systems.map(item => [item.id, item.galaxyPosition]));
                    const positions = [];
                    for (const lane of state.spatial.hyperlanes)
                    {
                        for (const id of [lane.fromSystemId, lane.toSystemId]) { const p = systems.get(id); positions.push(p.x, p.y, p.z); }
                    }
                    const geometry = new THREE.BufferGeometry();
                    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
                    const material = new THREE.LineBasicMaterial({ color: '#65c5d5', transparent: true, opacity: 0.46 });
                    result.owned.add(geometry); result.owned.add(material);
                    result.group.add(new THREE.LineSegments(geometry, material));
                }
            }
            result.bounds.setFromObject(result.group);
            if (state && view.kind !== 'stress') { this.addNavigation(result, state, view); }
            let backgroundId=view.kind==='galaxy'?'galaxy.stars':view.kind==='system'?'system.sky':'';
            if(view.kind==='system'&&state)
            {
                const system=state.spatial.systems.find(item=>item.id===view.systemId),source=profile?.systemLighting?.get(system?.starClassId);
                if(profile?.backgrounds?.has('system.sky')) { backgroundId='system.sky'; }
                else if(profile?.backgrounds?.size)
                {
                    const position=system?.galaxyPosition,radius=state.spatial.galaxyRadius;
                    if(source&&position&&Number.isFinite(position.x)&&Number.isFinite(position.z)&&Number.isFinite(radius)&&radius>0)
                    {
                        const distance=Math.min(1,Math.hypot(position.x,position.z)/radius);
                        const band=selectSourceBackgroundBand(distance);backgroundId=`system.sky:${source.world}:${band}`;
                    }
                    // Статический профиль не передаёт разрешённые system-lighting/distance; оставляем его проверенный G/core фон.
                    else if(!profile.generatedBindings?.size&&profile.backgrounds.has('system.sky:g_star:core')) { backgroundId='system.sky:g_star:core'; }
                    else { throw new Error('M3: unsupported source background profile or missing permitted-system distance.'); }
                }
                else { backgroundId=''; }
            }
            const background = profile?.backgrounds?.get(backgroundId);
            if (background)
            {
                const geometry = new THREE.SphereGeometry(1, 24, 16);
                result.owned.add(geometry);
                const sky = new THREE.Mesh(geometry, background);
                sky.frustumCulled = false;
                sky.renderOrder = -1000;
                result.group.add(sky);
            }
            if (state && view.kind === 'galaxy')
            {
                const nebulae = createNebulaGroup(state.spatial.nebulae, this.canvas?.ownerDocument, this.callbacks?.resolveName);
                for (const resource of nebulae.owned) { result.owned.add(resource); }
                result.labels.push(...nebulae.labels); result.group.add(nebulae.group);
                if (profile?.galaxyLayers?.size)
                {
                    const layers = createGalaxyLayerGroup(profile.galaxyLayers, state.spatial.systems);
                    for (const resource of layers.owned) { result.owned.add(resource); }
                    result.group.add(layers.group);
                }
            }
            if (state && view.kind === 'system')
            {
                for (const body of state.spatial.bodies)
                {
                    if (body.systemId !== view.systemId || !['planet','moon'].includes(body.kind)) { continue; }
                    const parent=body.parentBodyId?state.spatial.bodies.find(b=>b.id===body.parentBodyId):null;
                    const origin=parent?.localPosition??{x:0,z:0}; const relative={x:body.localPosition.x-origin.x,z:body.localPosition.z-origin.z};
                    if(Math.hypot(relative.x,relative.z)===0) { continue; }
                    const geometry = new THREE.BufferGeometry();
                    geometry.setAttribute('position', new THREE.Float32BufferAttribute(orbitPositions(relative), 3));
                    const material = new THREE.LineBasicMaterial({ color: '#9aa9a6', transparent: true, opacity: 0.2 });
                    result.owned.add(geometry); result.owned.add(material);
                    const orbit=new THREE.Line(geometry,material); orbit.name='orbit:'+body.id; orbit.position.set(origin.x,0,origin.z); result.group.add(orbit);
                }
            }
            const document = this.canvas?.ownerDocument;
            if (state && document && view.kind !== 'stress')
            {
                for (const descriptor of collectViewObjects(state, view))
                {
                    const object = result.objects.get(`${descriptor.kind}:${descriptor.id}`);
                    const collection=descriptor.kind==='gateway'?state.spatial.gateways:descriptor.kind==='wormhole'?state.spatial.wormholes:descriptor.kind==='system'?state.spatial.systems:descriptor.kind==='body'?state.spatial.bodies:state.spatial.fleets;
                    const source=collection.find(item=>item.id===descriptor.id);
                    const text=descriptor.kind==='fleet'?`${source?.name??descriptor.id} · ${source.shipCount}`:descriptor.kind==='body'?bodyDisplayName(source,state.spatial.bodies):source?.name??descriptor.id;
                    const label = createObjectLabel(document, text);
                    result.owned.add(label.material.map); result.owned.add(label.material);
                    label.position.copy(object.position);
                    label.position.y += view.kind === 'galaxy' ? descriptor.kind === 'fleet' ? 16 : 7 : descriptor.kind === 'fleet' || !profile ? 4 : (['gateway','wormhole'].includes(descriptor.kind)?6:displayedSurfaceRadius(profile.templates.get(descriptor.visualId), descriptor.visualId, descriptor.scale)) + 4;
                    label.userData.objectId = descriptor.id;
                    label.userData.kind = descriptor.kind;
                    if (descriptor.kind === 'fleet') { object.add(label); label.position.set(0, 4, 0); } else { result.group.add(label); }
                    result.labels.push(label);
                }
            }
            return result;
        }
        catch (error) { this.releaseView(result); throw error; }
    }

    addNavigation(result, state, view)
    {
        const line = (positions, color, opacity = 1, parent = result.group) =>
        {
            const geometry = new THREE.BufferGeometry();
            geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
            const material = new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthWrite: false });
            result.owned.add(geometry); result.owned.add(material);
            const object = new THREE.LineSegments(geometry, material); object.renderOrder = 5; parent.add(object);
            return object;
        };
        const circle = (radius, center = new THREE.Vector3()) =>
        {
            const points = [];
            for (let i = 0; i < 128; i++)
            {
                for (const angle of [i * Math.PI / 64, (i + 1) * Math.PI / 64]) { points.push(center.x + Math.cos(angle) * radius, center.y + 0.4, center.z + Math.sin(angle) * radius); }
            }
            return points;
        };
        if (view.kind === 'system')
        {
            const exits = (state.spatial.exits ?? []).filter(exit => exit.systemId === view.systemId);
            if (exits.length)
            {
                const firstExit = exits[0].position;
                const exitRadius = Math.hypot(firstExit.x, firstExit.z);
                if (Number.isFinite(exitRadius) && exitRadius > 0) { line(circle(exitRadius), '#63b29c', 0.36).name = 'exit-ring'; }
            }
            const system = state.spatial.systems.find(system => system.id === view.systemId);
            line(circle(system.radius), '#dab46a', 0.55).name = 'manual-limit';
            for (const exit of exits)
            {
                const marker = new THREE.Group(); marker.position.set(exit.position.x, 0, exit.position.z);
                const angle = Math.atan2(exit.position.z, exit.position.x), direction = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle)), side = new THREE.Vector3(-direction.z, 0, direction.x);
                const tip = direction.clone().multiplyScalar(4), a = direction.clone().multiplyScalar(-3).addScaledVector(side, 3), b = direction.clone().multiplyScalar(-3).addScaledVector(side, -3);
                const triangle = line([...tip.toArray(), ...a.toArray(), ...a.toArray(), ...b.toArray(), ...b.toArray(), ...tip.toArray()], '#79ebad', 1, marker);
                triangle.userData.pick = { kind: 'exit', id: exit.destinationSystemId };
                result.picks.push(triangle); result.group.add(marker);
                const document = this.canvas?.ownerDocument;
                if (document)
                {
                    const label = createObjectLabel(document, state.spatial.systems.find(system => system.id === exit.destinationSystemId).name ?? exit.destinationSystemId);
                    result.owned.add(label.material.map); result.owned.add(label.material);
                    label.position.copy(marker.position); label.position.y = 7;
                    label.userData.kind = 'exit'; label.userData.objectId = exit.destinationSystemId;
                    result.labels.push(label); result.group.add(label);
                }
            }
        }
        for (const fleet of state.spatial.fleets)
        {
            const route = receivedRoute(state, fleet, view);
            if (!route) { continue; }
            const group = new THREE.Group(); group.name = `route:${fleet.id}`; group.visible = false;
            const points = route.points.map(point => point.clone().add(new THREE.Vector3(0, 0.8, 0)));
            line(routeGeometry(points, view.kind === 'galaxy' ? 1.1 : 0.65), '#84f5b8', 1, group);
            line(circle(view.kind === 'galaxy' ? 4.5 : 3.5, points.at(-1)), '#84f5b8', 1, group);
            result.routes.set(fleet.id, group); result.group.add(group);
        }
        const ring = new THREE.RingGeometry(0.88, 1, 48); result.owned.add(ring);
        for (const [key, object] of result.objects)
        {
            if (key.startsWith('colony:')) { continue; }
            const material = new THREE.MeshBasicMaterial({ color: '#f4df8d', transparent: true, opacity: 0.9, depthTest: false, depthWrite: false, side: THREE.DoubleSide });
            result.owned.add(material);
            const mesh = new THREE.Mesh(ring, material); mesh.renderOrder = 20; mesh.visible = false;
            result.highlights.push({ mesh, object, pick: object.userData.pick }); result.group.add(mesh);
        }
    }

    ray(event)
    {
        const rect = this.canvas.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) { return null; }
        const pointer = new THREE.Vector2((event.clientX - rect.left) / rect.width * 2 - 1, -(event.clientY - rect.top) / rect.height * 2 + 1);
        this.camera.updateMatrixWorld();
        if(this.view.kind==='galaxy')
        {
            const rect=this.canvas.getBoundingClientRect();
            for(const glyph of this.viewResources?.galaxyGlyphs??[]) { updateGalaxyGlyphScale(glyph,this.camera,rect.height); }
        }
        this.scene.updateMatrixWorld(true);
        const ray = new THREE.Raycaster();
        ray.setFromCamera(pointer, this.camera);
        return ray;
    }
    pick(event, kind)
    {
        const ray = this.ray(event);
        if (!ray || this.contextLost) { return null; }
        if (this.view.kind === 'galaxy' && kind === 'system')
        {
            const hit = nearestProjectedTarget((this.current?.state.spatial.systems ?? []).map(system => ({ id: system.id, position: system.galaxyPosition })), this.camera, this.canvas.getBoundingClientRect(), event.clientX, event.clientY);
            return hit ? { kind: 'system', id: hit.id } : null;
        }
        if (this.view.kind === 'galaxy' && !kind)
        {
            const wormhole = this.pick(event, 'wormhole');
            if (wormhole) { return wormhole; }
            const fleet = this.pick(event, 'fleet');
            if (fleet) { return fleet; }
            const hit = nearestProjectedTarget((this.current?.state.spatial.systems ?? []).map(system => ({ id: system.id, position: system.galaxyPosition })), this.camera, this.canvas.getBoundingClientRect(), event.clientX, event.clientY);
            return hit ? { kind: 'system', id: hit.id } : null;
        }
        if (this.view.kind === 'system' && kind === 'exit')
        {
            const hit = nearestProjectedTarget((this.current?.state.spatial.exits ?? []).filter(exit => exit.systemId === this.view.systemId).map(exit => ({ id: exit.destinationSystemId, position: exit.position })), this.camera, this.canvas.getBoundingClientRect(), event.clientX, event.clientY);
            return hit ? { kind: 'exit', id: hit.id } : null;
        }
        for (const hit of ray.intersectObjects(this.viewResources?.picks ?? [], false))
        {
            let object = hit.object;
            while (object && !object.userData.pick) { object = object.parent; }
            if (object && (!kind ? object.userData.pick.kind !== 'exit' : object.userData.pick.kind === kind)) { return object.userData.pick; }
        }
        return null;
    }
    pointerUp(event)
    {
        this.nativePointers?.delete(event.pointerId);
        const pointer = this.pointer;
        if (!pointer || pointer.id !== event.pointerId) { return; }
        this.pointer = null;
        this.finishOrbit();
        this.rejectDoubleClick = pointer.alt || event.altKey || pointer.distance > 5 || Math.hypot(event.clientX - pointer.x, event.clientY - pointer.y) > 5;
        if (this.rejectDoubleClick) { this.suppressDoubleClickUntil = performance.now() + 500; return; }
        if (pointer.button === 0)
        {
            if (this.view.kind !== 'stress') { this.select(this.pick(event)); }
            return;
        }
        if (pointer.button !== 2) { return; }
        const exit = this.view.kind === 'system' ? this.pick(event, 'exit') : null;
        const pointOrder = this.view.kind === 'system' && !exit;
        const reason = orderGate(this.current?.state, this.commandFleetId, { fresh: this.fresh, profileReady: this.profileReady, contextLost: this.contextLost, viewKind: this.view.kind, pointOrder, systemId: this.view.systemId });
        if (reason) { this.callbacks.onHint?.(reason); return; }
        if (exit) { this.callbacks.onSystemOrder?.(exit.id); return; }
        if (pointOrder)
        {
            const point = this.ray(event)?.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 1, 0), 0), new THREE.Vector3());
            if (point && withinManualRadius(point, this.current.state.spatial.systems.find(system => system.id === this.view.systemId).radius)) { this.callbacks.onPointOrder?.({ systemId: this.view.systemId, x: point.x, z: point.z }); }
            else if (point) { this.callbacks.onHint?.('Точка находится за внешней границей ручного движения.'); }
            else { this.callbacks.onHint?.('Укажите точку на плоскости системы.'); }
        }
        else
        {
            const hit = this.pick(event, 'system');
            if (hit) { this.callbacks.onSystemOrder?.(hit.id); }
            else { this.callbacks.onHint?.('Укажите видимую систему назначения.'); }
        }
    }
}
