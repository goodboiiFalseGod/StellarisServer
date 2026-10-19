import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { parseLatency, createLatencySocketFactory } from '../../src/Stellaris.Server/wwwroot/m3-latency.mjs';
import * as presentation from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';
import { outlinerNavigation } from '../../src/Stellaris.Server/wwwroot/m3-outliner-navigation.mjs';
import { planetPanelModel, renderPlanetPanel, resolvePlanetInspection, appendPlanetWorkforceControls } from '../../src/Stellaris.Server/wwwroot/m3-planet-panel.mjs';
import { renderPlanetWindowView } from '../../src/Stellaris.Server/wwwroot/m3-planet-window-view.mjs';
import { createPlanetNotifications } from '../../src/Stellaris.Server/wwwroot/m3-planet-notifications.mjs';
import { renderPlanetNotifications } from '../../src/Stellaris.Server/wwwroot/m3-planet-notification-view.mjs';
import { renderConstructionRefund, renderConstructionRefundLosses } from '../../src/Stellaris.Server/wwwroot/m3-construction-refund-view.mjs';
import { appendJobControls } from '../../src/Stellaris.Server/wwwroot/m3-job-controls.mjs';
import { constructionIdentifier, constructionOperations } from '../../src/Stellaris.Server/wwwroot/m3-construction-controls.mjs';
import { hydrateDevelopmentTables } from '../../src/Stellaris.Server/wwwroot/m3-development-tables.mjs';
import { researchEmpire, researchPanelState, renderResearchPanel } from '../../src/Stellaris.Server/wwwroot/m3-research-panel.mjs';

async function refundHarness()
{
    const h = await constructionHarness();
    h.development.queue[0].refundQuote = { policyId: 'test-refund', previewId: 'loss-preview-1', amounts: { minerals: 30 }, losses: { minerals: 10 } };
    h.update(); h.cancel().click();
    h.refundButtons = () => h.el('planet-refund-content').lastChild.children;
    return h;
}
test('Refund confirmation sends only after explicit consent, ACK shows actual loss once', async () =>
{
    const h = await refundHarness(); const owner = h.owners[0];
    assert.equal(owner.commands.length, 0); assert.equal(h.el('planet-refund-dialog').open, true);
    h.refundButtons()[1].click(); assert.equal(h.el('planet-refund-dialog').open, false); assert.equal(owner.commands.length, 0);
    h.cancel().click(); h.refundButtons()[0].click();
    assert.deepEqual(owner.commands[0], { commandId: 'id-0', type: 'cancelConstruction', colonyId: 'C1', expectedColonyRevision: 4, orderId: 'existing-order', refundPreviewId: 'loss-preview-1', confirmRefundLoss: true });
    assert.equal(h.development.queue[0].status, 'pending'); assert.equal(h.el('planet-refund-dialog').open, false);
    owner.pending.clear(); owner.callbacks.onResult({ commandId: 'id-0', accepted: true, code: 'OK', message: 'Принято', refundLosses: { minerals: 9 } });
    assert.equal(h.el('planet-refund-losses').hidden, false);
    const text = node => node.textContent + node.children.map(text).join(' ');
    assert.match(text(h.el('planet-refund-losses')), /9/); assert.doesNotMatch(text(h.el('planet-refund-losses')), /10/);
    const notice = h.el('planet-refund-losses').firstChild; h.update();
    assert.equal(h.el('planet-refund-losses').firstChild, notice, 'Обычный snapshot не повторяет aria-live сообщение');
    owner.callbacks.onResult({ commandId: 'id-0', accepted: true, code: 'OK', message: 'Принято', refundLosses: { minerals: 500 } });
    assert.doesNotMatch(text(h.el('planet-refund-losses')), /500/);
    h.state.viewContext = { mode: 'general', participantId: null }; h.update(); assert.equal(h.el('planet-refund-losses').hidden, true);
});
test('Refund updated stocks refresh consent and reject the old callback', async () =>
{
    const h = await refundHarness(); const old = h.refundButtons()[0];
    h.development.queue[0].refundQuote.previewId = 'loss-preview-2'; h.development.queue[0].refundQuote.losses.minerals = 20; h.update();
    old.onclick(); assert.equal(h.owners[0].commands.length, 0);
    h.refundButtons()[0].click(); assert.equal(h.owners[0].commands[0].refundPreviewId, 'loss-preview-2');
    h.owners[0].pending.clear(); h.owners[0].callbacks.onResult({ commandId: 'id-0', accepted: false, code: 'REFUND_PREVIEW_CHANGED', message: 'Preview изменён' });
    assert.equal(h.el('planet-refund-dialog').open, false);
    h.development.revision++; h.development.queue[0].refundQuote.previewId = 'revision-preview-3'; h.update();
    assert.equal(h.el('planet-refund-dialog').open, true); assert.equal(h.development.queue[0].status, 'pending');
});
for (const change of ['ownership', 'perspective', 'connection', 'tab'])
{
    test(`Refund consent cannot act after ${change}`, async () =>
    {
        const h = await refundHarness(); const old = h.refundButtons()[0];
        if (change === 'ownership') h.state.spatial.colonies[0].ownerParticipantId = 2;
        if (change === 'perspective') h.state.viewContext = { mode: 'general', participantId: null };
        if (change === 'connection') h.owners[0].connected = false;
        if (change === 'tab') h.tabs.jobs.onclick();
        h.update(); old.onclick(); assert.equal(h.owners[0].commands.length, 0); assert.equal(h.el('planet-refund-dialog').open, false);
    });
}

function createResearchImageStore({ source })
{
    return { source, decodedBytes: 0, async get() { throw new Error('Локальный DDS не настроен в тестовом harness.'); }, dispose() { this.disposed = true; } };
}

// Execute the production UI with real presentation helpers and explicit browser/transport/GPU boundaries.
const source = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
const executable = source.replace(/^import .*;\r?$/gm, '');
function harness(configuration = {})
{
    const elements = new Map(); const renderers = []; const owners = []; const imports = []; const imageStages = []; const imageRequests = []; const uiStages = []; const localizationLoads = [];
    const timers = new Map(); let nextTimerId = 0;
    const setTimer = callback => { const id = ++nextTimerId; timers.set(id, callback); return id; };
    const clearTimer = id => timers.delete(id);
    const flushTimers = () => { const callbacks = [...timers.values()]; timers.clear(); for (const callback of callbacks) { callback(); } };
    const options = { gpuError: false, noCanvas: false, deferred: null, localizationDeferred: null, localizationFailure: null, localizationValues: {}, cancelCount: 0, ...configuration };
    const profile = { decodedBytes: 100, icons: new Map([['gfx/interface/icons/resources/energy.dds', { width: 1, height: 1, rgba: new Uint8Array([1, 2, 3, 255]) }]]) };
    class Element
    {
        constructor(tag = 'div') { this.tagName = tag; this.children = []; this.parentNode = null; this.handlers = new Map(); this.value = ''; this.files = []; this.style = { setProperty() {} }; this.dataset = {}; this.textContent = ''; this.open = false; this.selectionStart = null; this.selectionEnd = null; }
        addEventListener(type, callback) { this.handlers.set(type, callback); }
        async emit(type, extra = {}) { return this.handlers.get(type)?.({ preventDefault() {}, ...extra }); }
        append(...nodes) { this.insert(nodes, false); }
        prepend(...nodes) { this.insert(nodes, true); }
        replaceChildren(...nodes) { for (const child of this.children) { child.parentNode = null; } this.children = []; this.insert(nodes, false); }
        insert(nodes, before) { for (const node of nodes) { node.parentNode?.remove(node); node.parentNode = this; } this.children = before ? [...nodes, ...this.children] : [...this.children, ...nodes]; }
        remove(node) { this.children = this.children.filter(child => child !== node); node.parentNode = null; }
        contains(node) { return this === node || this.children.some(child => child.contains(node)); }
        querySelectorAll(selector) { return this.children.flatMap(node => { const matches = selector === 'input' && node.tagName === 'input' || selector === '[data-kind]' && node.dataset.kind || selector === '[data-file-count]' && node.dataset.fileCount !== undefined || selector === '[data-planet-tab]' && node.dataset.planetTab || selector === '[data-panel]' && node.dataset.panel || selector === 'dialog' && node.tagName === 'dialog' || selector === 'dialog[open]' && node.tagName === 'dialog' && node.open || selector === '[data-close]' && node.dataset.close !== undefined || selector.split(', ').some(value => value.startsWith('.') && node.className?.split(' ').includes(value.slice(1))); return [...(matches ? [node] : []), ...node.querySelectorAll(selector)]; }); }
        querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
        setAttribute(name, value) { this[name] = value; }
        getAttribute(name) { return Object.hasOwn(this, name) ? String(this[name]) : null; }
        closest(selector) { for (let node = this; node; node = node.parentNode) { if (selector === 'details' && node.tagName === 'details') { return node; } if (selector.split(',').some(part => part.trim() === 'input' && node.tagName === 'input' || part.trim() === 'textarea' && node.tagName === 'textarea' || part.trim() === 'select' && node.tagName === 'select' || part.trim() === '[data-kind]' && node.dataset.kind !== undefined || part.trim() === '[contenteditable="true"]' && node.contentEditable === 'true')) { return node; } } return null; }
        getBoundingClientRect() { return { left: 0, top: 0, right: 1280, bottom: 720, width: 1280, height: 720 }; }
        show() { this.open = true; this.modal = false; } showModal() { this.open = true; this.modal = true; } close() { this.open = false; }
        get options() { return this.children; }
        get firstChild() { return this.children[0]; }
        get lastChild() { return this.children.at(-1); }
        getContext() { return options.noCanvas ? null : { putImageData() {}, drawImage() {} }; }
        focus() { document.activeElement = this; }
        setSelectionRange(start, end) { this.selectionStart = start; this.selectionEnd = end; }
        click() { if (!this.disabled) { this.onclick?.(); this.handlers.get('click')?.({ target: this }); } }
    }
    const document = new Element(); document.activeElement = null; const window = new Element();
    const tabs = Object.fromEntries(['infrastructure', 'features', 'jobs', 'armies', 'holdings'].map(tab => { const node = new Element('button'); node.dataset.planetTab = tab; return [tab, node]; }));
    const economyPanelButton = new Element('button'); economyPanelButton.dataset.panel = 'economy'; document.append(...Object.values(tabs), economyPanelButton);
    const absentIds = new Set(['x', 'z', 'move-point']);
    document.getElementById = id => { if (absentIds.has(id)) { return null; } if (!elements.has(id)) { elements.set(id, new Element()); } return elements.get(id); };
    document.createElement = tag => new Element(tag);
    document.querySelector = selector => selector.startsWith('[data-panel=') ? economyPanelButton : document.getElementById(selector);
    document.documentElement = new Element(); document.body = new Element();
    class Renderer
    {
        constructor(canvas, callbacks) { this.callbacks = callbacks; this.profiles = []; this.converted = 40; const vector = value => ({ value, clone() { return { value: this.value }; }, copy(other) { this.value = other.value; } }); this.camera = { position: vector(7), fov: 45, near: .01, far: 1000, updateProjectionMatrix() {} }; this.controls = { target: vector(8), update() {} }; renderers.push(this); }
        resize() {} render() {} setCommandFleet(value) { this.commandFleetId = value; } setConnectionFresh() {} resetCamera() {} showGalaxy() { this.openedSystem = null; this.camera.position.value = 70; } showStress200() { this.openedSystem = 'stress'; this.camera.position.value = 80; }
        showSystem(id) { if (options.systemError) { throw new Error('Система временно недоступна.'); } this.openedSystem = id; this.camera.position.value = 90; }
        focusSelection() { this.camera.position.value = 123; if (options.focusError) { throw new Error('Фокус недоступен.'); } this.focusCount = (this.focusCount ?? 0) + 1; }
        select(value) { if (value && !this.current) { throw new Error('Выбор без renderer snapshot.'); } if (options.selectError && value?.kind === 'colony') { throw new Error('Выбор недоступен.'); } this.selectedValue = value; }
        stats() { return { convertedBytes: this.profiles.length ? this.converted : 0 }; }
        setProfile(value, limits) { if (options.gpuError) { throw new Error('GPU failure'); } this.profiles.push({ value, limits }); }
        refreshSpatialNames() {}
        applySnapshot(state) { this.current = state; } dispose() { this.disposed = true; }
    }
    class Connection
    {
        constructor(callbacks) { this.callbacks = callbacks; this.pending = new Map(); this.commands = []; this.profiles = []; this.connected = false; this.stopped = true; this.socket = null; this.retryAt = null; this.candidateVersion = 0; this.candidate = null; owners.push(this); }
        ready() { return this.connected; } lobbyReady() { return this.connected; } command(type, fields) { const commandId = `id-${this.commands.length}`; const command = { type, ...fields, commandId }; this.commands.push(command); this.pending.set(commandId, command); return commandId; }
        setProfile(...args) { this.profiles.push(args); } setGalaxyCandidate(profileId, profileDigest, galaxyWorldFingerprint, expectedVersion) { if (expectedVersion !== this.candidateVersion) { return false; } this.candidate = { profileId, profileDigest, galaxyWorldFingerprint }; return true; } connect(login) { this.login = login; this.stopped = false; this.socket = { readyState: 0 }; this.callbacks.onStatus('Подключение…', false); } disconnect() { this.connected = false; this.stopped = true; this.socket = null; this.retryAt = null; this.callbacks.onStatus('Отключено.', false); } dispose() { this.disconnect(); options.diagnostic = { value: null, stale: false }; }
        requestSnapshotRefresh() { this.refresh = true; }
        simulationDiagnostic() { return options.diagnostic ?? { value: null, stale: false }; }
    }
    class Importer
    {
        cancel() { options.cancelCount++; }
        async import(request) { imports.push(request); if (options.deferred) { await options.deferred; } if (options.importFailure) { throw options.importFailure; } return { profile, decodedBytes: profile.decodedBytes, milliseconds: 4, mode: 'test' }; }
    }
    const metadata = { profileId: 'profile', profileDigest: 'digest', files: options.manifestFiles ?? [] };
    const fetchManifest = async () =>
    {
        const snapshot = options.manifestFetch ? await options.manifestFetch() : { ...metadata, files: options.manifestFiles ?? [] };
        let done = false;
        return { ok: true, body: { getReader: () => ({ read: async () => done ? { done: true } : (done = true, { value: new TextEncoder().encode(JSON.stringify(snapshot)), done: false }), cancel: async () => {} }) } };
    };
    const localizationSnapshot = () =>
    {
        const values = Object.freeze({ ...options.localizationValues });
        const diagnosticValues = structuredClone(options.localizationDiagnostics ?? {});
        const raw = Object.freeze({ ...values });
        return Object.freeze({
            raw,
            diagnostics: Object.freeze([]),
            stats: Object.freeze({ language: 'russian', files: 2, entries: Object.keys(values).length }),
            resolve: (key, request) =>
            {
                (options.localizationResolveRequests ??= []).push({ key, request });
                return { text: Object.hasOwn(values, key) ? values[key] : key, diagnostics: diagnosticValues[key] ?? (Object.hasOwn(values, key) ? [] : [{ code: 'missing-key' }]) };
            },
            text: (key, fallback) => Object.hasOwn(values, key) ? values[key] : (fallback ?? String(key)),
        });
    };
    const loadLocalLocalization = async (source, request) =>
    {
        localizationLoads.push({ source, request });
        const snapshot = localizationSnapshot();
        if (options.localizationDeferred) { await (typeof options.localizationDeferred === 'function' ? options.localizationDeferred(source, request, localizationLoads.length - 1) : options.localizationDeferred); }
        if (options.localizationFailure) { throw options.localizationFailure; }
        return snapshot;
    };
    const prepareUiSprites = async () => { const handle = { decodedBytes: 0, disposed: false, apply() {}, dispose() { this.disposed = true; } }; uiStages.push(handle); return handle; };
    const preparePlanetImages = async (candidate, request) =>
    {
        imageRequests.push(request);
        if (options.imageDeferred) { await options.imageDeferred; }
        if (options.imageFailure) { throw new Error('Planet PNG failure'); }
        const index = imageStages.length;
        const handle = { decodedBytes: 16, disposed: false, get() { assert.equal(this.disposed, false); return { url: `blob:planet-${index}`, width: 2, height: 2 }; }, dispose() { this.disposed = true; } };
        imageStages.push(handle); return handle;
    };
    let orderSequence = 0; let frameCallback;
const context = vm.createContext({ ...presentation, createPlanetNotifications, renderPlanetNotifications, renderConstructionRefund, renderConstructionRefundLosses, outlinerNavigation, planetPanelModel, renderPlanetPanel: configuration.renderPlanetPanel ?? renderPlanetPanel, appendJobControls, constructionIdentifier, constructionOperations, hydrateDevelopmentTables, researchEmpire, researchPanelState, renderResearchPanel, createResearchImageStore, loadLocalLocalization, resolveSpatialNames: value => value, LOCALIZATION_FILES_452: ['core_l_{language}.yml', 'main_l_{language}.yml'], crypto: { randomUUID: () => `test-order-${++orderSequence}` }, structuredClone, AbortController, prepareUiSprites, preparePlanetImages, document, window, console, Map, Set, Uint8Array, Uint8ClampedArray, TextEncoder, TextDecoder, Intl, JSON, Number, String, setTimeout: setTimer, clearTimeout: clearTimer, performance: { now: () => 100 }, location: { protocol: 'http:', host: 'localhost', origin: 'http://localhost' }, sessionStorage: { getItem: () => null, setItem() {} }, M3Connection: Connection, M3Importer: Importer, M3WorldRenderer: Renderer, PROFILE_PATHS: ['file'], validateM3Manifest: async value => value, fetch: fetchManifest, ImageData: class {}, requestAnimationFrame: callback => { frameCallback = callback; return 1; }, cancelAnimationFrame() {} });
    context.resolvePlanetInspection = resolvePlanetInspection;
    context.createPlanetWindow = dialog => ({ open() { dialog.show(); }, layout() {} });
    context.appendPlanetWorkforceControls = appendPlanetWorkforceControls;
    Object.assign(context, { parseLatency, createLatencySocketFactory, URL });
    Object.assign(context.location, { search: options.search ?? '', href: `http://localhost/m3.html${options.search ?? ''}`, assign: value => { options.navigatedTo = value; } });
    vm.runInContext(executable, context, { filename: 'm3-client.mjs' });
    const el = id => document.getElementById(id);
    async function settle() { for (let i = 0; i < 40; i++) { await Promise.resolve(); } }
    async function importFiles(files = [{}]) { el('files').files = files; el('files').onchange(); await settle(); }
    const state = { protocol: 2, sessionId: 'session', sequence: 1, you: 1, viewContext: { mode: 'player', participantId: 1 }, phase: 'running', date: { year: 2200, month: 1, day: 1 }, month: 0, paused: true, speedDaysPerSecond: 1, participants: [{ id: 1, role: 'player', isHost: true, assetReady: true }], catalog: { jobs: [], traits: [], buildings: [] }, planets: [], spatial: { systems: [{ id: 'S01', name: 'Система' }], fleets: [{ id: 'F1', name: 'Флот', shipCount: 3, phase: 'idle', ownerParticipantId: 1, location: { kind: 'system', systemId: 'S01' } }], bodies: [], colonies: [] } };
    function ready() { owners[0].connected = true; owners[0].stopped = false; owners[0].socket = { readyState: 1 }; owners[0].callbacks.onState(state, {}); owners[0].callbacks.onStatus('Подключено', true); renderers.at(-1).callbacks.onSelect({ kind: 'fleet', id: 'F1' }); }
    return { el, document, window, renderers, owners, options, imports, localizationLoads, importFiles, settle, flushTimers, ready, state, profile, imageStages, imageRequests, uiStages, tabs, frame: now => frameCallback(now) };
}
test('Режим задержки виден, применяется reload, неверный URL блокирует вход', () =>
{
    const h = harness({ search: '?latency=500' });
    assert.match(h.el('latency-status').textContent, /RTT 500/);
    h.el('latency-mode').value = '1500';
    h.el('latency-settings').onsubmit({ preventDefault() {} });
    assert.equal(h.options.navigatedTo, 'http://localhost/m3.html?latency=1500');
    const invalid = harness({ search: '?latency=bad' });
    invalid.el('join').handlers.get('submit')({ preventDefault() {} });
    assert.match(invalid.el('error').textContent, /latency/);
    assert.equal(invalid.owners[0].connected, false);
});

async function jobsHarness(actor = 1, panelRenderer = renderPlanetPanel)
{
    const h = harness({ renderPlanetPanel: panelRenderer }); h.state.you = actor; h.state.viewContext.participantId = actor;
    h.state.participants = [{ id: 1, role: 'player', assetReady: true }, { id: 2, role: 'player', assetReady: true }];
    h.state.catalog.jobs = [{ id: 'farmer', name: 'Фермер' }, { id: 'miner', name: 'Шахтёр' }];
    h.state.planets = [{ id: 11, ownerId: actor, population: 600, workforce: 600, jobs: { farmer: 2, miner: 1, unknown: 0 }, balance: {} }];
    h.state.spatial.bodies.push({ id: 'B1', kind: 'planet', name: 'Colony', systemId: 'S01', ownerParticipantId: actor });
    h.state.spatial.colonies.push({ id: 'C1', bodyId: 'B1', ownerParticipantId: actor, economyPlanetId: 11 });
    await h.importFiles(); h.ready();
    h.openJobs = () => { h.renderers[0].callbacks.onSelect({ kind: 'colony', id: 'C1' }); h.el('open-planet').onclick(); h.tabs.jobs.onclick(); };
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    h.job = id =>
    {
        const input = descendants(h.el('planet-content')).find(node => node.tagName === 'input' && node.dataset.jobId === id);
        const row = input?.parentNode?.parentNode;
        return row ? { input, button: row.children.find(child => child.tagName === 'button') } : null;
    };
    h.openJobs(); return h;
}

function addSecondReadableColony(h, actor = h.state.you)
{
    h.state.spatial.bodies.push({ id: 'B2', kind: 'planet', name: 'Second Colony', systemId: 'S01', ownerParticipantId: actor });
    h.state.spatial.colonies.push({ id: 'C2', bodyId: 'B2', ownerParticipantId: actor, economyPlanetId: 12 });
    h.state.planets.push({ id: 12, ownerId: actor, population: 20, workforce: 20, jobs: {}, balance: {} });
    assert.equal(planetPanelModel(h.state, { kind: 'colony', id: 'C1' })?.id, 'C1');
    assert.equal(planetPanelModel(h.state, { kind: 'colony', id: 'C2' })?.id, 'C2');
}

test('Навигация циклически меняет только читаемую колонию, сохраняя вкладку и геометрию', async () =>
{
    const h = await jobsHarness(); addSecondReadableColony(h); h.owners[0].callbacks.onState(h.state, {});
    const dialog = h.el('planet-dialog'); dialog.style.left = '41px'; dialog.style.top = '73px';
    const camera = h.renderers[0].camera.position.value; const commands = h.owners[0].commands.length;
    assert.equal(h.el('planet-previous').disabled, false); assert.equal(h.el('planet-next').disabled, false);
    h.el('planet-next').click(); assert.equal(h.el('planet-heading').textContent, 'Second Colony');
    assert.equal(h.tabs.jobs['aria-selected'], 'true'); assert.equal(dialog.style.left, '41px'); assert.equal(dialog.style.top, '73px');
    h.el('planet-next').click(); assert.equal(h.el('planet-heading').textContent, 'Colony');
    h.el('planet-previous').click(); assert.equal(h.el('planet-heading').textContent, 'Second Colony');
    assert.equal(h.renderers[0].camera.position.value, camera); assert.equal(h.owners[0].commands.length, commands);
});

test('Выбор body той же читаемой колонии сохраняет инспекцию и привязанные обработчики', async () =>
{
    const h = await constructionHarness();
    const dialog = h.el('planet-dialog');
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' });
    h.el('open-planet').click();
    const inspected = h.inspect('slot'); assert.ok(inspected); inspected.click();
    const before = h.buttons().find(button => button.dataset.constructionType === 'enqueueConstruction');
    assert.ok(before);
    const commands = h.owners[0].commands.length;
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' });
    assert.equal(dialog.open, true);
    assert.ok(h.inspect('catalog', 'free', 'test-building'));
    assert.equal(before.parentNode != null, true, 'обработчик прежней колонии остаётся привязанным');
    assert.equal(h.owners[0].commands.length, commands);
    assert.equal(dialog.open, true);
});

test('Выбор другой собственной колонии сохраняет вкладку и геометрию, сбрасывает инспекцию и инвалидирует callback', async () =>
{
    const h = await constructionHarness(); addSecondReadableColony(h);
    const secondDevelopment = structuredClone(h.development);
    secondDevelopment.colonyId = 'C2'; secondDevelopment.economyPlanetId = 12;
    h.state.development.colonies.push(secondDevelopment); h.update();
    const dialog = h.el('planet-dialog'); dialog.style.left = '48px'; dialog.style.top = '66px';
    h.inspect('slot').click();
    const stale = h.buttons().find(button => button.dataset.constructionType === 'enqueueConstruction'); assert.ok(stale);
    const commandsBeforeSwitch = h.owners[0].commands.length;
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B2' });
    stale.onclick();
    assert.equal(h.owners[0].commands.length, commandsBeforeSwitch, 'смена колонии сама инвалидирует старый обработчик');
    h.tabs.jobs.click();
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' });
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B2' });
    assert.equal(dialog.open, true); assert.equal(h.el('planet-heading').textContent, 'Second Colony');
    assert.equal(h.tabs.jobs['aria-selected'], 'true'); assert.equal(dialog.style.left, '48px'); assert.equal(dialog.style.top, '66px');
    assert.equal(h.buttons().some(button => button.dataset.constructionType === 'enqueueConstruction'), false);
    const commands = h.owners[0].commands.length; stale.onclick();
    assert.equal(h.owners[0].commands.length, commands);
});

test('Недоступное body и не-colony selection закрывают окно, пустой выбор оставляет его открытым', async () =>
{
    const h = await constructionHarness();
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' }); h.el('open-planet').click();
    h.renderers[0].callbacks.onSelect(null); assert.equal(h.el('planet-dialog').open, true);
    h.state.spatial.bodies.push({ id: 'FOREIGN', kind: 'planet', name: 'Чужое', systemId: 'S01', ownerParticipantId: 2 });
    h.state.spatial.colonies.push({ id: 'FOREIGN-C', bodyId: 'FOREIGN', ownerParticipantId: 2, economyPlanetId: 99 });
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'FOREIGN' }); assert.equal(h.el('planet-dialog').open, false);

    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' }); h.el('open-planet').click();
    h.state.spatial.bodies.push({ id: 'EMPTY', kind: 'planet', name: 'Без колонии', systemId: 'S01', ownerParticipantId: 1 });
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'EMPTY' }); assert.equal(h.el('planet-dialog').open, false);

    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' }); h.el('open-planet').click();
    h.renderers[0].callbacks.onSelect({ kind: 'fleet', id: 'F1' }); assert.equal(h.el('planet-dialog').open, false);
});

test('Навигация отключена для одной доступной колонии и отвергает устаревший/чужой callback', async () =>
{
    const h = await jobsHarness();
    assert.equal(planetPanelModel(h.state, { kind: 'colony', id: 'C1' })?.id, 'C1');
    assert.equal(planetPanelModel(h.state, { kind: 'colony', id: 'missing' }), null);
    assert.equal(h.el('planet-previous').disabled, true); assert.equal(h.el('planet-next').disabled, true);
    addSecondReadableColony(h); h.owners[0].callbacks.onState(h.state, {});
    const old = h.el('planet-next').onclick; const commands = h.owners[0].commands.length;
    h.state.spatial.colonies.find(item => item.id === 'C2').ownerParticipantId = 2;
    h.owners[0].callbacks.onState(h.state, {}); old();
    assert.equal(h.el('planet-heading').textContent, 'Colony'); assert.equal(h.el('planet-next').disabled, true);
    assert.equal(h.owners[0].commands.length, commands); assert.equal(h.renderers[0].focusCount ?? 0, 0);

    h.state.spatial.colonies.find(item => item.id === 'C2').ownerParticipantId = 1; h.owners[0].callbacks.onState(h.state, {});
    const stale = h.el('planet-next').onclick; h.state.viewContext.participantId = 2; h.owners[0].callbacks.onState(h.state, {}); stale();
    assert.equal(h.el('planet-dialog').open, false); assert.equal(h.owners[0].commands.length, commands);
});

test('Навигация остаётся актуальной после исчезновения осматриваемого места', async () =>
{
    const h = await constructionHarness(); h.inspect('slot').click(); addSecondReadableColony(h);
    h.development.slots = []; h.update();
    assert.equal(h.el('planet-next').disabled, false);
    h.el('planet-next').click(); assert.equal(h.el('planet-heading').textContent, 'Second Colony');
    assert.equal(h.owners[0].commands.length, 0);
});

test('Legacy setJobs callback после перехода той же колонии на live economy не отправляет команду', async () =>
{
    const h = await jobsHarness(); const old = h.job('miner').button.handlers.get('click');
    h.state.development = { contentProfileId: 'basic', gates: [], colonies: [{ colonyId: 'C1', economyPlanetId: 11, ownerParticipantId: 1, revision: 7, slots: [], catalog: [], installed: [], queue: [], features: [], jobs: [], gates: [], economy: { profileId: 'basic', population: 60, baseWorkforce: 60, reportMonth: 0, resources: [], jobs: [{ jobId: 'miner', categoryId: 'worker', capacity: 10, limit: 5, assigned: 4, bonusWorkforce: 0, effectiveWorkforce: 4 }] } }] };
    h.owners[0].callbacks.onState(h.state, {});
    old();
    assert.equal(h.owners[0].commands.length, 0);
    assert.equal(h.job('miner'), null, 'В новом профиле нет legacy-контрола');
});

for (const change of ['revision', 'profile'])
{
    test(`Legacy setJobs callback отвергает устаревший ${change} при той же колонии`, async () =>
    {
        const h = await jobsHarness();
        h.state.development = { contentProfileId: 'old', gates: [], colonies: [{ colonyId: 'C1', economyPlanetId: 11, ownerParticipantId: 1, revision: 4, slots: [], catalog: [], installed: [], queue: [], features: [], jobs: [], gates: [] }] };
        h.owners[0].callbacks.onState(h.state, {});
        const old = h.job('miner').button.handlers.get('click');
        if (change === 'revision') { h.state.development.colonies[0].revision = 5; }
        else { h.state.development.contentProfileId = 'new'; }
        h.owners[0].callbacks.onState(h.state, {});
        old(); assert.equal(h.owners[0].commands.length, 0);
        h.job('miner').button.click(); assert.equal(h.owners[0].commands.at(-1).type, 'setJobs');
    });
}

async function workforceHarness()
{
    const h = await jobsHarness();
    const colony = { colonyId: 'C1', economyPlanetId: 11, ownerParticipantId: 1, revision: 7, slots: [], catalog: [], installed: [], queue: [], features: [], jobs: [], gates: [], economy: { profileId: 'basic', population: 60, baseWorkforce: 60, reportMonth: 0, resources: [{ resourceId: 'energy', storagePolicy: 'source-capped', sourceMaximum: 50000, stock: 123 }], jobs: [{ jobId: 'miner', categoryId: 'worker', capacity: 240, limit: 107.25, assigned: 40, bonusWorkforce: 2, effectiveWorkforce: 42 }] } };
    h.state.development = { gates: [], colonies: [colony] }; h.development = colony;
    h.update = () => h.owners[0].callbacks.onState(h.state, {});
    h.update();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    h.wf = () => descendants(h.el('planet-content')).find(node => node.className === 'planet-workforce-controls');
    return h;
}

test('Клиент показывает научные job icons только по уже импортированным planet image keys', async () =>
{
    const h = await workforceHarness();
    h.profile.planetImages = new Map(['physicist', 'biologist', 'engineer'].map(id => [`job.${id}`, {}]));
    h.development.economy.jobs = ['physicist', 'biologist', 'engineer', 'unknown_job'].map(jobId => ({ jobId, categoryId: 'specialist', imageDefinitionKey: null, capacity: 10, limit: 10, assigned: 5, bonusWorkforce: 0, effectiveWorkforce: 5 }));
    await h.importFiles(); h.update();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    const icons = descendants(h.el('planet-content')).filter(node => node.tagName === 'img').map(node => node.alt);
    assert.deepEqual([...new Set(icons)], ['job.physicist', 'job.biologist', 'job.engineer']);
    assert.equal(h.owners[0].commands.length, 0);
});

for (const [request, expected] of [[{}, { type: 'adjustWorkforceLimit', count: 1 }], [{ shiftKey: true }, { type: 'setWorkforceLimitMax' }]])
{
    test(`Live WF ${expected.type} посылает точную colony/revision/job identity без расчёта 5% на клиенте`, async () =>
    {
        const h = await workforceHarness(); const before = structuredClone(h.development); const plus = h.wf().children[1];
        plus.onclick(request);
        const command = h.owners[0].commands.at(-1);
        assert.equal(command.type, expected.type); assert.equal(command.count, expected.count);
        assert.equal(command.colonyId, 'C1'); assert.equal(command.expectedColonyRevision, 7); assert.equal(command.job, 'miner'); assert.ok(command.commandId);
        assert.deepEqual(h.development, before); assert.equal(h.wf().children[2].disabled, true);
        plus.onclick({}); assert.equal(h.owners[0].commands.length, 1);
    });
}

test('Live WF Shift минимум, raw absolute и Ctrl refusal без 1/100', async () =>
{
    const h = await workforceHarness(); const owner = h.owners[0];
    h.wf().children[0].onclick({ ctrlKey: true }); assert.equal(owner.commands.length, 0); assert.match(h.el('error').textContent, /единица Workforce/);
    h.wf().children[0].onclick({ shiftKey: true }); assert.equal(owner.commands.at(-1).type, 'setWorkforceLimitMin'); assert.equal(Object.hasOwn(owner.commands.at(-1), 'count'), false);
    owner.pending.clear(); h.update();
    h.wf().children[2].value = '173.125'; h.wf().children[3].onclick();
    assert.equal(owner.commands.at(-1).type, 'setWorkforceLimit'); assert.equal(owner.commands.at(-1).count, 173.125);
});

test('Live WF draft/focus переживает новый отчёт и revision; старый handler отвергается', async () =>
{
    const h = await workforceHarness(); const input = h.wf().children[2]; const old = h.wf().children[1].onclick;
    input.value = '133.5'; input.focus(); h.development.revision++; h.development.economy.reportMonth = 1; h.development.economy.jobs[0].limit = 110; h.update();
    assert.equal(h.wf().children[2].value, '133.5'); assert.equal(h.document.activeElement, h.wf().children[2]);
    old({}); assert.equal(h.owners[0].commands.length, 0);
    h.wf().children[3].onclick(); assert.equal(h.owners[0].commands.at(-1).expectedColonyRevision, 8); assert.equal(h.owners[0].commands.at(-1).count, 133.5);
});

for (const invalidation of ['observer', 'stale', 'removed-job', 'removed-colony', 'construction-pending', 'close'])
{
    test(`Live WF не отправляет старый callback: ${invalidation}`, async () =>
    {
        const h = await workforceHarness(); const old = h.wf().children[1].onclick;
        if (invalidation === 'observer') { h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true }); }
        if (invalidation === 'stale') { h.owners[0].callbacks.onStatus('Устарело', false); }
        if (invalidation === 'removed-job') { h.development.economy.jobs = []; }
        if (invalidation === 'removed-colony') { h.state.spatial.colonies = []; }
        if (invalidation === 'construction-pending') { h.owners[0].pending.set('other', { type: 'enqueueConstruction', colonyId: 'C1' }); }
        if (invalidation === 'close') { h.el('planet-dialog').close(); }
        if (invalidation !== 'stale') { h.update(); }
        old({}); assert.equal(h.owners[0].commands.length, 0);
    });
}

test('Live resources запрашивают resource image key и не угадывают DDS', async () =>
{
    const h = await workforceHarness(); h.profile.planetImages = new Map([['resource.energy', {}]]); await h.importFiles(); h.update();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    assert.equal(descendants(h.el('planet-content')).some(node => node.tagName === 'img' && node.alt === 'resource.energy'), true);
    assert.equal(descendants(h.el('planet-content')).some(node => node.tagName === 'canvas'), false);
});

test('Верхняя панель компактно показывает полученный запас/поток и доступную подпись при отсутствии значка', async () =>
{
    const h = harness();
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { unknown_resource: 2500, food: 5, minerals: 0, energy: 1_234_567 }, reportMonth: 1, report: { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { energy: 12_000, minerals: 0, food: 12 }, unemployedWorkforce: 0 } }];
    await h.importFiles(); h.ready();
    h.owners[0].callbacks.onState(h.state, {});
    const [energy, minerals, food, rare] = h.el('resources').children;
    assert.equal(rare.className, 'rare-resources');
    const unknown = rare.children[1].children[0];
    rare.open = true;
    const values = item => item.children.find(child => child.className === 'resource-values');
    assert.equal(energy.role, 'group'); assert.match(energy['aria-label'], /Энергия\. Запас: 1/); assert.match(energy.title, /1.?234.?567/);
    assert.equal(energy.dataset.iconMissing, 'false'); assert.equal(energy.children[0].tagName, 'canvas');
    assert.equal(values(energy).children[0].textContent, '1,2млн'); assert.equal(values(energy).children[1].textContent, '+12тыс');
    assert.equal(values(minerals).children[0].textContent, '0'); assert.equal(values(minerals).children[1].textContent, '0');
    assert.equal(values(food).children[1].textContent, '+12');
    assert.equal(unknown.dataset.iconMissing, 'true'); assert.equal(unknown.children[0].textContent, 'unknown_resource');
    assert.equal(values(unknown).children[1].textContent, '—'); assert.equal(values(unknown).children[1].dataset.flow, 'unavailable');
    assert.match(unknown['aria-label'], /Месячный поток недоступен/);
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.el('resources').children[3].open, true);
});

test('M3 locale: локализованный alloys остаётся читаемым без DDS', async () =>
{
    const h = harness({ localizationValues: { alloys: 'Сплавы' } });
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { alloys: 0 }, reportMonth: 1, report: { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { alloys: null }, unemployedWorkforce: 0 } }];
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    const resource = h.el('resources').children[0];
    assert.equal(resource.dataset.iconMissing, 'true');
    assert.equal(resource.children[0].textContent, 'Сплавы');
    assert.match(resource['aria-label'], /^Сплавы\./);
    assert.equal(resource.title.startsWith('Сплавы:'), true);
    assert.equal(resource.children[1].children[0].textContent, '0');
    assert.equal(resource.children[1].children[1].textContent, '—');
});

test('Ресурсный HUD использует original key только текущего экономического POV', async () =>
{
    const h = harness({ localizationValues: { own_resource_key: 'Полученная подпись', foreign_resource_key: 'Чужая подпись' } });
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { energy: 7 }, reportMonth: 0 }];
    h.state.development = { colonies: [
        { ownerParticipantId: 1, economy: { resources: [{ resourceId: 'energy', localizationKey: 'own_resource_key' }] } },
        { ownerParticipantId: 2, economy: { resources: [{ resourceId: 'energy', localizationKey: 'foreign_resource_key' }] } }
    ] };
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    assert.match(h.el('resources').children[0]['aria-label'], /^Полученная подпись\. Запас: 7/);
    assert.doesNotMatch(h.el('resources').children[0].title, /Чужая подпись/);
});

test('Игровые вкладки, focus и close используют подтверждённые original keys после импорта', async () =>
{
    const h = harness({ localizationValues: { PLANET_TAB: 'Оригинальная инфраструктура', PLANET_MANAGEMENT: 'Оригинальное управление', ECONOMY: 'Оригинальная экономика', ZOOM_TO_PLANET: 'Оригинальный переход к планете', CLOSE_TITLE: 'Оригинальное закрытие' } });
    const close = h.document.createElement('button'); close.dataset.close = ''; h.el('planet-dialog').append(close);
    await h.importFiles(); h.ready();
    assert.equal(h.tabs.infrastructure.textContent, 'Оригинальная инфраструктура');
    assert.equal(h.tabs.features.textContent, 'Оригинальное управление'); assert.equal(h.tabs.jobs.textContent, 'Оригинальная экономика');
    assert.equal(h.el('show-planet-on-map')['aria-label'], 'Оригинальный переход к планете');
    assert.equal(close['aria-label'], 'Оригинальное закрытие колонию');
    h.el('clear-login').onclick();
    assert.equal(h.tabs.infrastructure.textContent, 'Инфраструктура'); assert.equal(h.el('show-planet-on-map')['aria-label'], 'Показать колонию на карте');
});

test('M3 locale: reader получает полный file source, graphics importer — только свои файлы и неизвестные extras', async () =>
{
    const h = harness();
    const files = [
        { name: 'core_l_russian.yml' }, { name: 'main_l_russian.yml' },
        { name: 'energy.dds' }, { name: 'unexpected.txt' },
    ];
    await h.importFiles(files);
    assert.equal(h.localizationLoads.length, 1);
    assert.deepEqual(Array.from(h.localizationLoads[0].source.files, file => file.name), files.map(file => file.name));
    assert.deepEqual(Array.from(h.imports[0].source.files, file => file.name), ['energy.dds', 'unexpected.txt']);
});

test('M3 locale: ошибка локализации сохраняет предыдущую партию, подписи и готовность', async () =>
{
    const h = harness({ localizationValues: { energy: 'Старая энергия' } });
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { energy: 1 }, reportMonth: 1, report: { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { energy: 0 }, unemployedWorkforce: 0 } }];
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    const oldProfileCount = h.owners[0].profiles.length;
    const previousResource = h.el('resources').children[0];
    h.options.localizationValues = { energy: 'Повреждённая энергия' };
    h.options.localizationFailure = new Error('Некорректная локализация');
    await h.importFiles();
    assert.equal(h.owners[0].profiles.length, oldProfileCount);
    assert.equal(h.owners[0].ready(), true);
    assert.equal(h.el('resources').children[0], previousResource);
    assert.match(previousResource['aria-label'], /^Старая энергия\./);
    assert.match(h.el('error').textContent, /Некорректная локализация/);
    assert.match(h.el('import-status').textContent, /^Импорт не выполнен\. Прежний набор сохранён\.$/);
});

test('Localization failure without a prior profile leaves a terminal import status and claims no rollback', async () =>
{
    const h = harness({ localizationFailure: new Error('Нет обязательных EN/RU файлов') });
    await h.importFiles();
    assert.equal(h.el('import-status').textContent, 'Импорт не выполнен.');
    assert.match(h.el('error').textContent, /Нет обязательных EN\/RU файлов/);
    assert.doesNotMatch(h.el('error').textContent, /Прежний набор сохранён/);
    assert.equal(h.el('cancel-import').disabled, true);
    assert.equal(h.owners[0].profiles.length, 0);
});

test('M3 locale: устаревшая загрузка не заменяет подписи после более нового импорта', async () =>
{
    const h = harness({ localizationValues: { energy: 'Первая подпись' } });
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { energy: 1 }, reportMonth: 1, report: { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { energy: 0 }, unemployedWorkforce: 0 } }];
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    let releaseStale; let loaderCall = 0;
    h.options.localizationValues = { energy: 'Устаревшая подпись' };
    h.options.localizationDeferred = () => ++loaderCall === 1 ? new Promise(resolve => { releaseStale = resolve; }) : Promise.resolve();
    h.el('files').files = [{ name: 'energy.dds' }]; h.el('files').onchange(); await h.settle();
    h.options.localizationValues = { energy: 'Новая подпись' };
    h.el('files').files = [{ name: 'energy.dds' }]; h.el('files').onchange(); await h.settle();
    releaseStale(); await h.settle();
    assert.equal(loaderCall, 2);
    assert.equal(h.owners[0].profiles.length, 2);
    assert.match(h.el('resources').children[0]['aria-label'], /^Новая подпись\./);
});

test('M3 locale: новый snapshot обновляет открытые ресурсы колонии и верхнюю панель', async () =>
{
    const h = await jobsHarness();
    h.state.planets[0].balances = { alloys: 10 };
    h.state.planets[0].reportMonth = 1;
    h.state.planets[0].report = { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { alloys: 2 }, unemployedWorkforce: 0 };
    h.state.development = { gates: [], colonies: [{ colonyId: 'C1', economyPlanetId: 11, ownerParticipantId: 1, revision: 1, slots: [], catalog: [], installed: [], queue: [], features: [], jobs: [], gates: [], economy: { profileId: 'basic', population: 60, baseWorkforce: 60, reportMonth: 1, resources: [{ resourceId: 'alloys', storagePolicy: 'source-capped', sourceMaximum: 100, stock: 10, net: 2 }], jobs: [] } }] };
    h.owners[0].callbacks.onState(h.state, {}); h.tabs.infrastructure.onclick();
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    assert.match(text(h.el('planet-content')), /Сплавы/);
    h.options.localizationValues = { alloys: 'Локальные сплавы' };
    await h.importFiles([{ name: 'energy.dds' }]);
    assert.match(text(h.el('planet-content')), /Локальные сплавы/);
    assert.match(h.el('resources').children[0]['aria-label'], /^Локальные сплавы\./);
});

test('M3 locale: clear-login очищает прежние локализованные подписи', async () =>
{
    const h = harness({ localizationValues: { energy: 'Только старая партия' } });
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { energy: 1 }, reportMonth: 1, report: { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { energy: 0 }, unemployedWorkforce: 0 } }];
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    assert.match(h.el('resources').children[0]['aria-label'], /^Только старая партия\./);
    h.el('clear-login').onclick();
    assert.equal(h.el('resources').children.length, 0);
    h.options.localizationValues = {};
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    assert.match(h.el('resources').children[0]['aria-label'], /^Энергия\./);
    assert.doesNotMatch(h.el('resources').children[0]['aria-label'], /Только старая партия/);
});

function setEnergyTopbar(h)
{
    h.state.planets = [{ id: 11, ownerId: 1, population: 0, workforce: 0, jobs: {}, balances: { energy: 1 }, reportMonth: 1, report: { allocated: {}, produced: {}, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { energy: 0 }, unemployedWorkforce: 0 } }];
}
const energyTopbarLabel = h => h.el('resources').children[0]?.['aria-label'] ?? '';

test('M3 locale: cancel-import во время чтения не публикует staged locale или графику', async () =>
{
    const h = harness({ localizationValues: { energy: 'Подтверждённая энергия' } }); setEnergyTopbar(h);
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    const renderer = h.renderers[0]; let release;
    h.options.localizationValues = { energy: 'Отменённая энергия' };
    h.options.localizationDeferred = new Promise(resolve => { release = resolve; });
    h.el('files').files = [{ name: 'energy.dds' }]; h.el('files').onchange(); await h.settle();
    assert.equal(h.localizationLoads[1].request.signal.aborted, false);
    h.el('cancel-import').onclick(); assert.equal(h.localizationLoads[1].request.signal.aborted, true);
    release(); await h.settle();
    assert.equal(h.owners[0].profiles.length, 1);
    assert.equal(renderer.profiles.length, 1);
    h.state.planets[0].balances.energy = 123;
    h.owners[0].callbacks.onState(h.state, {});
    assert.match(energyTopbarLabel(h), /^Подтверждённая энергия\./);
    assert.match(energyTopbarLabel(h), /123/);
});

for (const termination of ['clear-login', 'pagehide'])
{
    test(`M3 locale: ${termination} во время чтения не публикует поздний snapshot`, async () =>
    {
        const h = harness({ localizationValues: { energy: 'Сохранённая энергия' } }); setEnergyTopbar(h);
        await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
        const renderer = h.renderers[0]; let release;
        h.options.localizationValues = { energy: 'Поздняя энергия' };
        h.options.localizationDeferred = new Promise(resolve => { release = resolve; });
        h.el('files').files = [{ name: 'energy.dds' }]; h.el('files').onchange(); await h.settle();
        if (termination === 'clear-login') { h.el('clear-login').onclick(); }
        else { await h.window.emit('pagehide'); }
        assert.equal(h.localizationLoads[1].request.signal.aborted, true);
        release(); await h.settle();
        assert.equal(h.owners[0].profiles.length, 1);
        assert.equal(renderer.profiles.length, 1);
        assert.equal(renderer.disposed, true);

        if (termination === 'clear-login')
        {
            h.options.localizationDeferred = null; h.options.localizationValues = { energy: 'Новая партия' };
            await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
            assert.equal(h.owners[0].profiles.length, 2);
            assert.match(energyTopbarLabel(h), /^Новая партия\./);
            assert.doesNotMatch(energyTopbarLabel(h), /Поздняя энергия/);
        }
        else
        {
            // BFCache восстанавливает ранее принятые профиль и словарь.
            h.options.localizationDeferred = null;
            await h.window.emit('pageshow', { persisted: true });
            h.owners[0].callbacks.onState(h.state, {});
            assert.equal(h.owners[0].profiles.length, 2, 'Только штатное восстановление профиля повторно сообщает readiness.');
            assert.match(energyTopbarLabel(h), /^Сохранённая энергия\./);
            assert.doesNotMatch(energyTopbarLabel(h), /Поздняя энергия/);
        }
    });
}

test('M3 locale: GPU failure после чтения сохраняет прежние подписи и readiness', async () =>
{
    const h = harness({ localizationValues: { energy: 'Подтверждённая энергия' } }); setEnergyTopbar(h);
    await h.importFiles(); h.ready(); h.owners[0].callbacks.onState(h.state, {});
    const renderer = h.renderers[0]; const priorResource = h.el('resources').children[0];
    h.options.localizationValues = { energy: 'Неопубликованная энергия' }; h.options.gpuError = true;
    await h.importFiles([{ name: 'energy.dds' }]);
    assert.equal(h.localizationLoads.length, 2, 'Новый snapshot был прочитан до GPU staging.');
    assert.equal(h.owners[0].profiles.length, 1);
    assert.equal(renderer.profiles.length, 1);
    assert.equal(h.el('resources').children[0], priorResource);
    h.state.planets[0].balances.energy = 456;
    h.owners[0].callbacks.onState(h.state, {});
    assert.match(energyTopbarLabel(h), /^Подтверждённая энергия\./);
    assert.match(energyTopbarLabel(h), /456/);
    assert.match(h.el('error').textContent, /GPU failure/);
});

test('Кнопка окна ведёт к открытой колонии в полученной системе без сетевой команды и смены флота', async () =>
{
    const h = await jobsHarness();
    const renderer = h.renderers[0];
    h.state.spatial.systems.push({ id: 'S02', name: 'Другая система' });
    h.state.spatial.bodies[0].systemId = 'S02';
    h.owners[0].callbacks.onState(h.state, {});
    const click = h.el('show-planet-on-map').onclick;
    click();
    assert.equal(renderer.openedSystem, 'S02');
    assert.equal(renderer.selectedValue.id, 'C1');
    assert.equal(renderer.focusCount, 1);
    assert.equal(renderer.commandFleetId, 'F1');
    assert.equal(h.el('planet-dialog').open, true);
    assert.equal(h.owners[0].commands.length, 0);
    h.el('show-planet-on-map').click();
    assert.equal(renderer.focusCount, 2);
    assert.equal(h.owners[0].commands.length, 0);
});

test('Второй player POV переходит только к собственной открытой колонии', async () =>
{
    const h = await jobsHarness(2);
    h.el('show-planet-on-map').click();
    assert.equal(h.renderers[0].openedSystem, 'S01');
    assert.equal(h.renderers[0].selectedValue.id, 'C1');
    assert.equal(h.owners[0].commands.length, 0);
});

for (const failure of ['select', 'focus'])
{
    test(`Неудачный ${failure} возвращает прежнюю систему и выбор`, async () =>
    {
        const h = await jobsHarness();
        const renderer = h.renderers[0];
        h.state.spatial.systems.push({ id: 'S02', name: 'Другая система' });
        h.state.spatial.bodies[0].systemId = 'S02';
        h.owners[0].callbacks.onState(h.state, {});
        renderer.callbacks.onOpenSystem('S01');
        renderer.select({ kind: 'colony', id: 'C1' });
        renderer.camera.position.value = 77; renderer.controls.target.value = 66;
        h.options[`${failure}Error`] = true;
        h.el('show-planet-on-map').click();
        assert.equal(renderer.openedSystem, 'S01');
        assert.equal(renderer.camera.position.value, 77);
        assert.equal(renderer.controls.target.value, 66);
        assert.equal(renderer.selectedValue.id, 'C1');
        assert.equal(renderer.commandFleetId, 'F1');
        assert.equal(h.owners[0].commands.length, 0);
        assert.match(h.el('error').textContent, /недоступен/);
        if (failure === 'focus')
        {
            h.state.spatial.bodies[0].systemId = 'S01';
            h.owners[0].callbacks.onState(h.state, {});
            renderer.camera.position.value = 55;
            h.el('show-planet-on-map').click();
            assert.equal(renderer.openedSystem, 'S01');
            assert.equal(renderer.camera.position.value, 55);
            assert.equal(renderer.selectedValue.id, 'C1');
        }
    });
}

test('Переход из окна запрещает старую кнопку и сохраняет выбор при недоступной системе', async () =>
{
    const h = await jobsHarness();
    const renderer = h.renderers[0];
    const oldClick = h.el('show-planet-on-map').onclick;
    h.owners[0].callbacks.onState(h.state, {});
    oldClick();
    assert.match(h.el('error').textContent, /актуального обзора/);
    assert.equal(renderer.openedSystem, undefined);
    h.state.spatial.bodies[0].systemId = 'missing';
    h.owners[0].callbacks.onState(h.state, {});
    h.el('show-planet-on-map').click();
    assert.match(h.el('error').textContent, /Система объекта отсутствует/);
    assert.equal(renderer.openedSystem, undefined);
    assert.equal(renderer.selectedValue, null);
    h.state.spatial.bodies[0].systemId = 'S01';
    h.options.systemError = true;
    h.owners[0].callbacks.onState(h.state, {});
    h.el('show-planet-on-map').click();
    assert.match(h.el('error').textContent, /временно недоступна/);
    assert.equal(renderer.openedSystem, undefined);
    assert.equal(renderer.focusCount, undefined);
    h.options.systemError = false;
    h.el('show-planet-on-map').click();
    assert.equal(renderer.openedSystem, 'S01');
});

for (const invalidation of ['observer', 'general', 'pending', 'foreign', 'removed', 'missing-body', 'disconnect', 'assets', 'close'])
{
    test(`Переход из окна проверяет текущий допуск: ${invalidation}`, async () =>
    {
        const h = await jobsHarness();
        const renderer = h.renderers[0];
        const oldClick = h.el('show-planet-on-map').onclick;
        if (invalidation === 'observer')
        {
            h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true });
            h.state.viewContext = { mode: 'player', participantId: 1 };
        }
        if (invalidation === 'general') { h.state.viewContext = { mode: 'general', participantId: null }; }
        if (invalidation === 'pending') { h.state.viewContext = { mode: 'pending', participantId: null }; }
        if (invalidation === 'foreign') { h.state.viewContext = { mode: 'player', participantId: 2 }; }
        if (invalidation === 'removed') { h.state.spatial.colonies = []; }
        if (invalidation === 'missing-body') { h.state.spatial.bodies = []; }
        if (invalidation === 'disconnect') { h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Разрыв', false); }
        if (invalidation === 'assets') { h.state.participants[0].assetReady = false; }
        if (invalidation === 'close') { h.el('planet-dialog').close(); await h.el('planet-dialog').emit('close'); }
        if (!['disconnect', 'close'].includes(invalidation)) { h.owners[0].callbacks.onState(h.state, {}); }
        oldClick();
        assert.equal(renderer.openedSystem, undefined);
        assert.equal(h.owners[0].commands.length, 0);
        if (invalidation === 'observer')
        {
            renderer.callbacks.onSelect({ kind: 'colony', id: 'C1' });
            h.el('open-planet').onclick();
            h.el('show-planet-on-map').click();
            assert.equal(renderer.openedSystem, 'S01', h.el('error').textContent);
            assert.equal(h.owners[0].commands.length, 0);
        }
    });
}

test('PERF existing metrics field separates configured speed, automatic window, tick and FPS', async () =>
{
    const h = harness(); await h.importFiles(); h.state.speedDaysPerSecond = 4; h.state.lastTickMilliseconds = 12.5; h.ready();
    assert.match(h.el('metrics').textContent, /Задано: 4 дн\/с/);
    assert.match(h.el('metrics').textContent, /Автоматически: нет данных/);
    assert.match(h.el('metrics').textContent, /Последний тик: 12,5 мс/);
    const world = h.renderers[0].current;
    h.options.diagnostic = { value: { status: 'measured', automaticDaysPerRealSecond: 1.25, completedAutomaticDays: 5, windowSeconds: 4 }, stale: false };
    h.frame(1100); h.frame(1200);
    assert.match(h.el('metrics').textContent, /Автоматически: 1,25 дн\.\/с · завершено: 5 · окно: 4 с/);
    assert.match(h.el('metrics').textContent, /FPS · p95/);
    assert.equal(h.renderers[0].current, world); assert.equal(h.owners[0].commands.length, 0);
    h.options.diagnostic.stale = true; h.frame(1201);
    assert.match(h.el('metrics').textContent, /Автоматически: устарело/);
    assert.doesNotMatch(h.el('metrics').textContent, /Автоматически: 1,25/);
    assert.match(h.el('metrics').textContent, /Задано: 4 дн\/с/);
    assert.match(h.el('metrics').textContent, /Последний тик: 12,5 мс/);
    h.el('clear-login').onclick(); h.frame(1300);
    assert.match(h.el('metrics').textContent, /Автоматически: нет данных/);
    assert.match(h.el('metrics').textContent, /Задано: недоступно/);
    assert.doesNotMatch(h.el('metrics').textContent, /FPS|12,5|устарело/);
});

test('Сводка закрытого месяца обновляет открытое окно и сохраняет черновик вакансии', async () =>
{
    const h = await jobsHarness();
    const planet = h.state.planets[0];
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    const settlement = () => descendants(h.el('planet-content')).find(node => node.className === 'planet-settlement');
    assert.match(text(settlement()), /Отчёт закрытого месяца недоступен/);
    const input = h.job('farmer').input;
    input.value = '3.5'; input.selectionStart = 1; input.selectionEnd = 3; input.focus();
    planet.reportMonth = 1;
    planet.balances = {};
    planet.report = { allocated: {}, produced: { energy: 12 }, jobUpkeep: { energy: 1 }, buildingUpkeep: { energy: 2 }, upkeep: { energy: 3 }, net: { energy: 9, unknown_resource: -2, minerals: 0 } };
    h.owners[0].callbacks.onState(h.state, {});
    assert.match(text(settlement()), /Закрытый месяц: 1/);
    assert.match(text(settlement()), /\+9/);
    assert.match(text(settlement()), /unknown_resource/);
    assert.equal(descendants(settlement()).filter(node => node.tagName === 'canvas').length, 0, 'Нет подбора DDS-пути для отсутствующего resource key');
    assert.equal(h.job('farmer').input.value, '3.5');
    assert.equal(h.document.activeElement, h.job('farmer').input);
    assert.equal(h.job('farmer').input.selectionStart, 1);
    assert.equal(h.job('farmer').input.selectionEnd, 3);
    planet.reportMonth = 2; planet.report.net.energy = -7;
    h.owners[0].callbacks.onState(h.state, {});
    assert.match(text(settlement()), /Закрытый месяц: 2/);
    assert.match(text(settlement()), /-7/);
    assert.doesNotMatch(text(settlement()), /\+9/);
    assert.equal(h.job('farmer').input.value, '3.5');
    assert.equal(h.owners[0].commands.length, 0);
});

for (const invalidation of ['general', 'pending', 'foreign', 'removed', 'logout'])
{
    test(`Сводка закрытого месяца очищает частные данные: ${invalidation}`, async () =>
    {
        const h = await jobsHarness();
        const text = node => [node.textContent, ...node.children.map(text)].join(' ');
        h.state.planets[0].reportMonth = 123;
        h.state.planets[0].balances = {};
        h.state.planets[0].report = { allocated: {}, net: { private_resource: 987 } };
        h.owners[0].callbacks.onState(h.state, {});
        assert.match(text(h.el('planet-content')), /private_resource/);
        if (invalidation === 'logout') { h.el('clear-login').onclick(); }
        else
        {
            if (invalidation === 'general' || invalidation === 'pending') { h.state.viewContext = { mode: invalidation, participantId: null }; }
            if (invalidation === 'foreign') { h.state.planets[0].ownerId = 2; }
            if (invalidation === 'removed') { h.state.spatial.colonies = []; }
            h.owners[0].callbacks.onState(h.state, {});
        }
        assert.doesNotMatch(text(h.el('planet-content')), /private_resource|987/);
        assert.equal(h.owners[0].commands.length, 0);
    });
}

async function economyHarness()
{
    const h = await jobsHarness();
    const planet = h.state.planets[0];
    planet.name = 'Колония';
    planet.reportMonth = 3;
    planet.balances = { energy: 10 };
    planet.report = { allocated: { farmer: { workforce: 2, bonusWorkforce: 0, effectiveWorkforce: 2 }, miner: { workforce: 1, bonusWorkforce: 0, effectiveWorkforce: 1 } }, produced: { energy: 12 }, jobUpkeep: { energy: 1 }, buildingUpkeep: { energy: 1 }, upkeep: { energy: 2 }, net: { energy: 10 }, unemployedWorkforce: 0 };
    h.owners[0].callbacks.onState(h.state, {});
    h.document.querySelector('[data-panel="economy"]').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    h.economyInput = (planetId, jobId) => descendants(h.el('economy-content')).find(node => node.tagName === 'input' && node.dataset.planetKey === JSON.stringify(planetId) && node.dataset.jobId === jobId);
    return h;
}

test('ECON-001 focused Apply button does not freeze authoritative month or report refresh', async () =>
{
    const h = await economyHarness();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    const apply = descendants(h.el('economy-content')).find(node => node.tagName === 'button');
    assert.ok(apply);
    apply.focus();
    h.state.month = 4;
    h.state.planets[0].jobs.farmer = 3;
    h.state.planets[0].reportMonth = 4;
    h.state.planets[0].report.produced.energy = 18;
    h.state.planets[0].balances.energy = 16;
    h.owners[0].callbacks.onState(h.state, {});
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    assert.match(text(h.el('economy-content')), /Закрытых экономических месяцев: 4/);
    assert.match(text(h.el('economy-content')), /Отчёт месяца 4/);
    const table = descendants(h.el('economy-content')).find(node => node.tagName === 'table');
    const energyRow = table.children.find(row => row.children[0].textContent === 'Энергия');
    assert.equal(energyRow.children[1].textContent, new Intl.NumberFormat('ru-RU').format(18));
    assert.equal(h.economyInput(11, 'farmer').value, '3');
    assert.equal(h.owners[0].commands.length, 0);
});

test('ECON-001 dirty focused numeric draft survives received updates without changing model or sending', async () =>
{
    const h = await economyHarness();
    const input = h.economyInput(11, 'farmer');
    input.value = '3.5'; input.selectionStart = 1; input.selectionEnd = 3; input.focus();
    h.state.month = 4; h.state.planets[0].jobs.farmer = 4; h.state.planets[0].reportMonth = 4;
    h.owners[0].callbacks.onState(h.state, {});
    const refreshed = h.economyInput(11, 'farmer');
    assert.equal(refreshed.value, '3.5');
    assert.equal(h.document.activeElement, refreshed);
    assert.equal(refreshed.selectionStart, 1); assert.equal(refreshed.selectionEnd, 3);
    assert.equal(h.state.planets[0].jobs.farmer, 4);
    assert.equal(h.owners[0].commands.length, 0);
});

test('ECON-001 unchanged focused input adopts the received count', async () =>
{
    const h = await economyHarness();
    const input = h.economyInput(11, 'farmer');
    input.focus(); h.state.planets[0].jobs.farmer = 5; h.state.month = 5;
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.economyInput(11, 'farmer').value, '5');
    assert.equal(h.owners[0].commands.length, 0);
});

for (const invalidation of ['disconnect-reconnect', 'asset-readiness', 'observer-perspective', 'owner-change', 'removed-planet', 'removed-job'])
{
    test(`ECON-001 discards an unsent draft after ${invalidation}`, async () =>
    {
        const h = await economyHarness();
        const input = h.economyInput(11, 'farmer'); input.value = '3.5'; input.focus();
        if (invalidation === 'disconnect-reconnect') { h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Отключено', false); h.owners[0].connected = true; h.owners[0].callbacks.onStatus('Подключено', true); }
        if (invalidation === 'asset-readiness') { h.state.participants[0].assetReady = false; h.owners[0].callbacks.onState(h.state, {}); h.state.participants[0].assetReady = true; }
        if (invalidation === 'observer-perspective') { h.state.you = 3; h.state.viewContext = { mode: 'observer', participantId: 1 }; h.state.participants.push({ id: 3, role: 'observer', assetReady: true }); }
        if (invalidation === 'owner-change') { h.state.planets[0].ownerId = 2; }
        if (invalidation === 'removed-planet') { h.state.planets = []; }
        if (invalidation === 'removed-job') { h.state.catalog.jobs = h.state.catalog.jobs.filter(job => job.id !== 'farmer'); }
        h.state.month++;
        h.owners[0].callbacks.onState(h.state, {});
        if (invalidation === 'disconnect-reconnect') { h.owners[0].callbacks.onState(h.state, {}); }
        if (invalidation === 'asset-readiness') { h.state.participants[0].assetReady = true; h.state.month++; h.owners[0].callbacks.onState(h.state, {}); }
        if (invalidation === 'owner-change')
        {
            const changed = h.economyInput(11, 'farmer');
            assert.equal(changed.value, '2'); assert.equal(changed.disabled, true);
            h.state.planets[0].ownerId = 1; h.state.month++; h.owners[0].callbacks.onState(h.state, {});
        }
        const received = invalidation === 'removed-planet' || invalidation === 'removed-job' ? null : h.economyInput(11, 'farmer');
        if (received) { assert.equal(received.value, String(h.state.planets[0].jobs.farmer)); }
        assert.equal(h.owners[0].commands.length, 0);
    });
}

for (const actor of [1, 2])
{
    test(`Jobs controls send actor ${actor}'s exact economic binding and wait for received counts`, async () =>
    {
        const h = await jobsHarness(actor); const owner = h.owners[0];
        assert.equal(h.job('unknown'), null);
        for (const count of [0, 0.5, 6])
        {
            const control = h.job('farmer'); assert.equal(control.input.value, '2'); control.input.value = String(count); control.button.click();
            const command = owner.commands.at(-1);
            assert.deepEqual(command, { commandId: `id-${owner.commands.length - 1}`, type: 'setJobs', planetId: 11, job: 'farmer', count });
            assert.equal(h.state.planets[0].jobs.farmer, 2); assert.equal(h.job('farmer').button.disabled, true); assert.equal(h.job('miner').button.disabled, false);
            control.button.handlers.get('click')(); assert.equal(owner.commands.length, count === 0 ? 1 : count === 0.5 ? 2 : 3);
            owner.pending.delete(command.commandId); owner.callbacks.onResult({ commandId: command.commandId, accepted: count !== 0.5, code: 'RESULT', message: 'Result' });
            assert.equal(h.job('farmer').input.value, '2'); assert.equal(h.job('farmer').button.disabled, false);
        }
        h.state.planets[0].jobs.farmer = 6; owner.callbacks.onState(h.state, {}); assert.equal(h.job('farmer').input.value, '6');
    });
}

test('Jobs focused vacancy draft survives same-context refresh and resets on tab and pending transitions', async () =>
{
    const h = await jobsHarness(); const input = h.job('farmer').input; input.value = '3.5'; input.focus();
    h.state.planets[0].population++; h.state.planets[0].jobs.farmer = 4; h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.job('farmer').input.value, '3.5'); assert.equal(h.document.activeElement, h.job('farmer').input); assert.equal(h.state.planets[0].jobs.farmer, 4); assert.equal(h.owners[0].commands.length, 0);
    h.tabs.infrastructure.onclick(); h.tabs.jobs.onclick(); assert.equal(h.job('farmer').input.value, '4');
    h.job('farmer').input.value = '5'; h.job('farmer').input.focus(); h.job('farmer').button.click(); assert.equal(h.job('farmer').input.value, '4'); assert.equal(h.job('farmer').input.disabled, true);
});

test('Jobs unchanged focused vacancy adopts authoritative received count', async () =>
{
    const h = await jobsHarness(); h.job('farmer').input.focus(); h.state.planets[0].jobs.farmer = 4; h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.job('farmer').input.value, '4'); assert.equal(h.document.activeElement, h.job('farmer').input);
});

test('Jobs controls reject invalid raw values visibly without changing received economy', async () =>
{
    const h = await jobsHarness(); const before = structuredClone(h.state.planets);
    for (const raw of ['', ' ', '-0.5', '6.5', '0.25', 'Infinity', 'NaN'])
    {
        const control = h.job('farmer'); control.input.value = raw; control.button.click();
        assert.equal(h.owners[0].commands.length, 0); assert.match(h.el('error').textContent, /0.*6/);
    }
    assert.deepEqual(h.state.planets, before);
});

for (const invalidation of ['reopen', 'tab', 'tab-return', 'perspective', 'removed', 'relinked', 'ownership', 'catalog', 'stale', 'assets', 'observer', 'context'])
{
    test(`Jobs detached handlers cannot submit after ${invalidation}`, async () =>
    {
        const h = await jobsHarness(); const owner = h.owners[0]; const old = h.job('farmer'); old.input.value = '3';
        if (invalidation === 'reopen') { h.el('planet-dialog').close(); await h.el('planet-dialog').emit('close'); h.openJobs(); }
        if (invalidation === 'tab') { h.tabs.infrastructure.onclick(); }
        if (invalidation === 'tab-return') { h.tabs.infrastructure.onclick(); h.tabs.jobs.onclick(); }
        if (invalidation === 'perspective') { h.state.you = 2; h.state.viewContext.participantId = 2; }
        if (invalidation === 'removed') { h.state.spatial.colonies = []; }
        if (invalidation === 'relinked') { h.state.spatial.colonies[0].economyPlanetId = 22; }
        if (invalidation === 'ownership') { h.state.planets[0].ownerId = 2; }
        if (invalidation === 'catalog') { h.state.catalog.jobs = []; }
        if (invalidation === 'stale') { owner.connected = false; owner.callbacks.onStatus('Reconnect', false); }
        if (invalidation === 'assets') { h.state.participants[0].assetReady = false; }
        if (invalidation === 'observer') { h.state.participants[0].role = 'observer'; }
        if (invalidation === 'context') { h.renderers[0].callbacks.onContextState('lost'); }
        old.button.handlers.get('click')(); assert.equal(owner.commands.length, 0); assert.ok(h.el('error').textContent);
    });
}

test('Matching observer Jobs counts stay read-only and absent economic links never create controls', async () =>
{
    const h = await jobsHarness(); h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true });
    h.owners[0].callbacks.onState(h.state, {}); h.openJobs();
    assert.equal(h.job('farmer').input.value, '2'); assert.equal(h.job('farmer').button.disabled, true);
    h.job('farmer').button.handlers.get('click')(); assert.equal(h.owners[0].commands.length, 0);
    h.state.spatial.colonies[0].economyPlanetId = null; h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.job('farmer'), null);
});

test('Catalog removal refreshes Jobs controls even when the displayed name already equals its ID', async () =>
{
    const h = await jobsHarness(); h.state.catalog.jobs[0].name = 'farmer';
    h.owners[0].callbacks.onState(h.state, {}); assert.ok(h.job('farmer'));
    h.state.catalog.jobs = h.state.catalog.jobs.filter(job => job.id !== 'farmer');
    h.owners[0].callbacks.onState(h.state, {}); assert.equal(h.job('farmer'), null); assert.ok(h.job('miner'));
});

async function constructionHarness(actor = 1, panelRenderer = renderPlanetPanel)
{
    const h = await jobsHarness(actor, panelRenderer);
    h.development = { colonyId: 'C1', economyPlanetId: 11, ownerParticipantId: actor, revision: 4, gates: [],
        slots: [{ slotId: 'free', kind: 'building', reservedByOrderId: null, installedDefinitionId: null, gates: [] }], installed: [],
        catalog: [{ definitionId: 'test-building', kind: 'building', eligibility: 'available', slotIds: ['free'], imageDefinitionKey: null, gates: [], quote: { policyId: 'test-resolved', costs: { minerals: 40 }, durationDays: 10, infrastructureUpkeep: { energy: 2 }, jobCapacity: { farmer: 200 } } }],
        queue: [{ orderId: 'existing-order', slotId: 'other-slot', definitionId: 'test-building', kind: 'building', status: 'pending', paidCosts: { minerals: 40 }, completedWork: 2, requiredWork: 10, workUnit: 'policyWork', gates: [], refundQuote: { policyId: 'test-refund', amounts: { minerals: 40 }, previewId: 'refund-preview', losses: {} } }],
        features: [{ instanceId: 'feature-A', definitionId: 'test-blocker', clearanceDefinitionId: 'test-clearance', clearable: true, gates: [] }], jobs: [] };
    h.state.development = { contentProfileId: 'test', gates: [], colonies: [h.development] };
    h.update = () => h.owners[0].callbacks.onState(h.state, {}); h.update(); h.tabs.infrastructure.onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    h.buttons = () => descendants(h.el('planet-content')).filter(node => node.tagName?.toLowerCase() === 'button');
    h.inspect = (kind, slotId = 'free', definitionId) => h.buttons().find(node => node.dataset.planetInspect === kind && node.dataset.slotId === slotId && (definitionId == null || node.dataset.definitionId === definitionId));
    h.build = () =>
    {
        h.inspect('slot')?.click(); h.inspect('catalog')?.click();
        return h.buttons().find(node => node.dataset.constructionType === 'enqueueConstruction');
    };
    h.cancel = () => h.buttons().find(node => node.dataset.constructionType === 'cancelConstruction');
    return h;
}

function addReorderQueue(development, ids = ['active-order', 'waiting-a', 'waiting-b', 'waiting-c'])
{
    development.queuePolicy = { policyId: 'ordinary-sequential-v1', activeCapacity: 1, canReorder: true };
    development.queue = ids.map(orderId => ({ ...structuredClone(development.queue[0]), orderId, slotId: `slot-${orderId}` }));
}

async function cancellationNoticeHarness()
{
    const h = await constructionHarness(); h.development = h.state.development.colonies[0];
    h.development.queue = [{ ...h.development.queue[0], orderId: 'cancelled-order', slotId: 'free', status: 'cancelled', cancellationReason: 'non-positive-speed' }];
    h.update();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    h.noticeButtons = () => descendants(h.el('planet-notifications')).filter(node => node.className === 'planet-notice-order');
    return h;
}

test('Уведомление автоотмены открывает актуальную инфраструктуру без команды и движения карты; dismiss не повторяется', async () =>
{
    const h = await cancellationNoticeHarness();
    const button = h.noticeButtons()[0]; assert.ok(button); const camera = h.renderers[0].camera.position.value;
    h.tabs.jobs.onclick(); button.click();
    assert.equal(h.el('planet-dialog').open, true); assert.equal(h.tabs.infrastructure['aria-selected'], 'true');
    assert.equal(h.renderers[0].camera.position.value, camera); assert.equal(h.owners[0].commands.length, 0);
    h.update(); assert.equal(h.noticeButtons().length, 1);
    const dismiss = h.el('planet-notifications').children[0].children.find(node => node.tagName.toLowerCase() === 'button');
    dismiss.click(); h.update(); assert.equal(h.el('planet-notifications').hidden, true);
    h.el('planet-dialog').open = false; button.onclick();
    assert.equal(h.el('planet-dialog').open, false); assert.equal(h.owners[0].commands.length, 0);
});

test('Уведомление не открывает старый POV, потерянную колонию или цель при потере готовности', async () =>
{
    for (const change of ['pov', 'owner', 'connection'])
    {
        const h = await cancellationNoticeHarness(), button = h.noticeButtons()[0]; assert.ok(button);
        h.el('planet-dialog').open = false;
        if (change === 'pov') { h.state.viewContext.participantId = 2; h.update(); }
        if (change === 'owner') { h.state.spatial.colonies[0].ownerParticipantId = 2; h.update(); }
        if (change === 'connection') { h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Отключено', false); }
        button.onclick(); assert.equal(h.el('planet-dialog').open, false, change); assert.equal(h.owners[0].commands.length, 0, change);
        assert.equal(h.el('planet-notifications').hidden, true, change);
    }
});

test('Перестановка меняет соседние ожидающие заказы и сохраняет активную голову', () =>
{
    const h = { development: { revision: 4, gates: [], catalog: [], slots: [], installed: [], queue: [{ orderId: 'seed', status: 'pending', gates: [], refundQuote: { policyId: 'seed', amounts: {} } }] } };
    addReorderQueue(h.development);
    const operations = constructionOperations({ id: 'C1', gates: [], development: h.development }).filter(item => item.type === 'reorderConstruction');
    assert.deepEqual(operations.map(({ orderId, direction, orderIds }) => ({ orderId, direction, orderIds })), [
        { orderId: 'waiting-a', direction: 'down', orderIds: ['active-order', 'waiting-b', 'waiting-a', 'waiting-c'] },
        { orderId: 'waiting-b', direction: 'up', orderIds: ['active-order', 'waiting-b', 'waiting-a', 'waiting-c'] },
        { orderId: 'waiting-b', direction: 'down', orderIds: ['active-order', 'waiting-a', 'waiting-c', 'waiting-b'] },
        { orderId: 'waiting-c', direction: 'up', orderIds: ['active-order', 'waiting-a', 'waiting-c', 'waiting-b'] }
    ]);
    assert.ok(operations.every(item => item.orderIds[0] === 'active-order' && item.orderId !== 'active-order'));
});

test('Кнопки очереди требуют точной policy обычной очереди и уникальных ID ожидающих заказов', async () =>
{
    const panel = (root, model, options) => renderPlanetWindowView(root, model, { ...options, appendConstruction(parent, identity) { const result = options.appendConstruction(parent, identity); for (const child of parent.children ?? []) { if (child.tagName) { child.tagName = child.tagName.toUpperCase(); } } return result; } });
    const h = await constructionHarness(1, panel); h.development = h.state.development.colonies[0]; addReorderQueue(h.development); h.update();
    assert.deepEqual(h.development.queuePolicy, { policyId: 'ordinary-sequential-v1', activeCapacity: 1, canReorder: true });
    assert.equal(constructionOperations({ id: 'C1', gates: [], development: h.development }).filter(item => item.type === 'reorderConstruction').length, 4);
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'reorderConstruction').length, 4);
    assert.equal(h.buttons().some(button => button.dataset.orderId === 'active-order' && button.dataset.constructionType === 'reorderConstruction'), false);
    delete h.development.queuePolicy; h.update();
    assert.equal(constructionOperations({ id: 'C1', gates: [], development: h.development }).filter(item => item.type === 'reorderConstruction').length, 0);
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'reorderConstruction').length, 0);
    h.development.queuePolicy = { policyId: 'ordinary-sequential-v1', activeCapacity: 2, canReorder: true }; h.update();
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'reorderConstruction').length, 0);
    h.development.queuePolicy = { policyId: 'another-policy', activeCapacity: 1, canReorder: true }; h.update();
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'reorderConstruction').length, 0);
    h.development.queuePolicy = { policyId: 'ordinary-sequential-v1', activeCapacity: 1, canReorder: true };
    h.development.queue[2].orderId = h.development.queue[1].orderId; h.update();
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'reorderConstruction').length, 0);
});

test('Очередь отправляет полный список один раз и отвергает устаревший callback', async () =>
{
    const panel = (root, model, options) => renderPlanetWindowView(root, model, { ...options, appendConstruction(parent, identity) { const result = options.appendConstruction(parent, identity); for (const child of parent.children ?? []) { if (child.tagName) { child.tagName = child.tagName.toUpperCase(); } } return result; } });
    const h = await constructionHarness(1, panel); h.development = h.state.development.colonies[0]; addReorderQueue(h.development); h.update();
    let button = h.buttons().find(item => item.dataset.constructionType === 'reorderConstruction' && item.dataset.orderId === 'waiting-a' && item.dataset.direction === 'down');
    assert.ok(button); button.click();
    assert.deepEqual(JSON.parse(JSON.stringify(h.owners[0].commands[0])), { commandId: 'id-0', type: 'reorderConstruction', colonyId: 'C1', expectedColonyRevision: 4, orderIds: ['active-order', 'waiting-b', 'waiting-a', 'waiting-c'] });

    const stale = await constructionHarness(1, panel); stale.development = stale.state.development.colonies[0]; addReorderQueue(stale.development); stale.update();
    button = stale.buttons().find(item => item.dataset.constructionType === 'reorderConstruction' && item.dataset.orderId === 'waiting-a' && item.dataset.direction === 'down');
    [stale.development.queue[1], stale.development.queue[2]] = [stale.development.queue[2], stale.development.queue[1]];
    button.onclick();
    assert.equal(stale.owners[0].commands.length, 0);
});

test('Runtime здание в зоне использует проверенный profile key и открывает точные сведения без flat-дубля', async () =>
{
    const h = await constructionHarness();
    h.profile.planetImages = new Map([['building.building_capital', {}]]);
    h.development.startupDistricts = [{ instanceId: 'city', definitionId: 'district_city', level: 3 }];
    h.development.startupZones = [{ instanceId: 'zone', districtInstanceId: 'city', slotInstanceId: 'zone-slot', zoneId: 'zone_research_unity', slotNumber: 1 }];
    h.development.startupBuildings = [{ instanceId: 'startup-capital', definitionId: 'building_capital', slotInstanceId: 'building-0', zoneId: 'zone_research_unity', compatibleZoneInstanceIds: ['zone'] }];
    h.development.slots = [{ slotId: 'building-0', kind: 'building', zoneInstanceId: 'zone', installedDefinitionId: 'building_capital', reservedByOrderId: null, gates: [] }, { slotId: 'free', kind: 'building', zoneInstanceId: 'zone', installedDefinitionId: null, reservedByOrderId: null, gates: [] }];
    h.development.installed = [{ slotId: 'building-0', kind: 'building', definitionId: 'building_capital', imageDefinitionKey: null, infrastructureUpkeep: { energy: 1 }, jobCapacity: {} }];
    await h.importFiles(); h.update();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    const content = descendants(h.el('planet-content'));
    assert.equal(content.filter(item => item.tagName === 'img' && item.alt === 'building.building_capital').length, 1);
    assert.equal(content.filter(item => item.dataset.buildingInstance === 'startup-capital').length, 1);
    assert.equal(content.filter(item => item.className?.split(' ').includes('planet-slot')).length, 1);
    const inspected = h.inspect('installed', 'building-0', 'building_capital'); assert.ok(inspected);
    inspected.click();
    assert.equal(h.owners[0].commands.length, 0);
    assert.ok(h.el('planet-content').children.some(item => item.className === 'planet-layout has-context'));
});

test('Каталог фильтрует exact slot/kind и позволяет штатный заказ сразу с карточки', async () =>
{
    const h = await constructionHarness(); const before = structuredClone(h.state);
    h.development.slots.push({ ...structuredClone(h.development.slots[0]), slotId: 'second' });
    h.development.catalog[0].slotIds.push('second');
    h.development.catalog.push({ ...structuredClone(h.development.catalog[0]), definitionId: 'other-building', slotIds: ['second'] }, { ...structuredClone(h.development.catalog[0]), definitionId: 'wrong-kind', kind: 'zone' });
    h.update();
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'enqueueConstruction').length, 0);
    h.inspect('slot').click();
    assert.deepEqual(h.buttons().filter(button => button.dataset.planetInspect === 'catalog').map(button => button.dataset.definitionId), ['test-building']);
    assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'enqueueConstruction').length, 1);
    h.inspect('catalog', 'free', 'test-building').click();
    const actions = h.buttons().filter(button => button.dataset.constructionType === 'enqueueConstruction'); assert.equal(actions.length, 1); actions[0].click();
    assert.equal(h.owners[0].commands[0].slotId, 'free'); assert.equal(h.owners[0].commands[0].definitionId, 'test-building');
    h.buttons().find(button => button.dataset.planetInspect === 'close').click();
    assert.equal(h.buttons().filter(button => button.dataset.planetInspect === 'catalog').length, 0);
    assert.deepEqual(h.state.planets, before.planets); assert.equal(h.development.queue.length, 1);
});

test('Inspection переживает день и месяц, обновляет quote, но старое действие не исполняется', async () =>
{
    const h = await constructionHarness(); const oldBuild = h.build();
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    h.development.revision++; h.development.catalog[0].quote.costs.minerals = 41; h.update();
    assert.match(text(h.el('planet-content')), /Стоимость:.*41/);
    assert.ok(h.buttons().find(button => button.dataset.constructionType === 'enqueueConstruction'));
    oldBuild.onclick(); assert.equal(h.owners[0].commands.length, 0);
    h.state.planets[0].reportMonth++; h.development.revision++; h.update();
    const currentBuild = h.buttons().find(button => button.dataset.constructionType === 'enqueueConstruction');
    assert.ok(currentBuild); currentBuild.click();
    assert.equal(h.owners[0].commands[0].expectedColonyRevision, 6);
});

test('Сведения установленного здания обновляются при завершении заказа без смены identity', async () =>
{
    const h = await constructionHarness();
    h.development.slots[0].installedDefinitionId = 'test-building';
    h.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'test-building', infrastructureUpkeep: { energy: 2 }, jobCapacity: { farmer: 200 } }];
    h.update(); h.inspect('installed', 'free', 'test-building').click();
    h.development.revision++; h.development.queue = []; h.development.installed[0].jobCapacity.farmer = 300; h.update();
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    assert.match(text(h.el('planet-content')), /Вместимость профессий:.*300/);
    assert.ok(h.el('planet-content').children.some(node => node.className === 'planet-layout has-context'));
});

for (const change of ['revision', 'slot-removal', 'slot-zone', 'slot-source', 'catalog-removal', 'tab-return', 'reopen', 'perspective', 'logout'])
{
    test(`Planet inspection rejects detached navigation after ${change}`, async () =>
    {
        const h = await constructionHarness(); h.inspect('slot').click(); const old = h.inspect('catalog');
        if (change === 'revision') { h.development.revision++; h.update(); }
        if (change === 'slot-removal') { h.development.slots = []; h.update(); }
        if (change === 'slot-zone') { h.development.slots[0].zoneInstanceId = 'another-zone'; h.update(); }
        if (change === 'slot-source') { h.development.slots[0].installedDefinitionId = 'another-building'; h.update(); }
        if (change === 'catalog-removal') { h.development.catalog = []; h.update(); }
        if (change === 'tab-return') { h.tabs.jobs.onclick(); h.tabs.infrastructure.onclick(); }
        if (change === 'reopen') { h.el('planet-dialog').close(); await h.el('planet-dialog').emit('close'); h.openJobs(); h.tabs.infrastructure.onclick(); }
        if (change === 'perspective') { h.state.viewContext = { mode: 'general', participantId: null }; h.update(); }
        if (change === 'logout') { h.el('clear-login').onclick(); }
        old.onclick(); assert.equal(h.owners[0].commands.length, 0); assert.ok(h.el('error').textContent);
        assert.equal(h.buttons().filter(button => button.dataset.constructionType === 'enqueueConstruction').length, change === 'revision' ? 1 : 0);
    });
}

for (const actor of [1, 2])
{
    test(`Construction actor ${actor} sends exact envelopes and never applies ACK effects locally`, async () =>
    {
        const h = await constructionHarness(actor); const owner = h.owners[0]; const before = structuredClone(h.state);
        const build = h.build(); build.click();
        assert.deepEqual(owner.commands[0], { commandId: 'id-0', type: 'enqueueConstruction', colonyId: 'C1', expectedColonyRevision: 4, definitionId: 'test-building', slotId: 'free', orderId: 'test-order-1' });
        assert.equal(h.build().disabled, true); assert.equal(h.cancel().disabled, true);
        build.onclick(); assert.equal(owner.commands.length, 1); assert.deepEqual(h.state, before);
        owner.pending.delete('id-0'); owner.callbacks.onResult({ commandId: 'id-0', accepted: true, code: 'OK', message: 'Accepted' });
        assert.deepEqual(h.state, before); assert.equal(h.build().disabled, false);
        h.cancel().click();
        assert.deepEqual(owner.commands[1], { commandId: 'id-1', type: 'cancelConstruction', colonyId: 'C1', expectedColonyRevision: 4, orderId: 'existing-order', refundPreviewId: 'refund-preview', confirmRefundLoss: false });
        owner.pending.delete('id-1'); owner.callbacks.onResult({ commandId: 'id-1', accepted: false, code: 'DEPENDENCY_UNRESOLVED', message: 'Refused' });
        assert.deepEqual(h.state, before); assert.equal(h.cancel().disabled, false);
        h.build().click(); assert.equal(owner.commands[2].orderId, 'test-order-2');
        h.development.revision = 5; h.development.slots[0].reservedByOrderId = 'test-order-2'; h.update();
        assert.equal(h.build(), undefined); assert.equal(h.development.installed.length, 0);
    });
}

for (const change of ['revision', 'quote', 'slot', 'installed', 'catalog', 'ownership', 'perspective', 'stale', 'tab', 'tab-return', 'reopen'])
{
    test(`Construction stale enqueue handlers reject changed ${change}`, async () =>
    {
        const h = await constructionHarness(); const button = h.build();
        if (change === 'revision') { h.development.revision++; }
        if (change === 'quote') { h.development.catalog[0].quote.costs.minerals++; }
        if (change === 'slot') { h.development.slots[0].reservedByOrderId = 'other-order'; }
        if (change === 'installed') { h.development.installed.push({ slotId: 'free' }); }
        if (change === 'catalog') { h.development.catalog[0].eligibility = 'unresolved'; }
        if (change === 'ownership') { h.state.spatial.colonies[0].ownerParticipantId = 2; }
        if (change === 'perspective') { h.state.you = 2; h.state.viewContext.participantId = 2; }
        if (change === 'stale') { h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Reconnect', false); }
        if (change === 'tab') { h.tabs.features.onclick(); }
        if (change === 'tab-return') { h.tabs.features.onclick(); h.tabs.infrastructure.onclick(); }
        if (change === 'reopen') { h.el('planet-dialog').close(); await h.el('planet-dialog').emit('close'); h.openJobs(); h.tabs.infrastructure.onclick(); }
        button.onclick(); assert.equal(h.owners[0].commands.length, 0); assert.ok(h.el('error').textContent);
    });
}

test('District and zone enqueue use the same bounded envelope with their exact compatible slot', async () =>
{
    for (const kind of ['district', 'zone'])
    {
        const h = await constructionHarness(); h.development.catalog[0].kind = kind; h.development.slots[0].kind = kind; h.update(); h.build().click();
        assert.deepEqual(h.owners[0].commands[0], { commandId: 'id-0', type: 'enqueueConstruction', colonyId: 'C1', expectedColonyRevision: 4, definitionId: 'test-building', slotId: 'free', orderId: 'test-order-1' });
    }
});

test('Детали установленного здания отправляют явное улучшение с серверной привязкой', async () =>
{
    const h = await constructionHarness();
    h.development.slots[0].installedDefinitionId = 'building_old';
    h.development.slots[0].zoneInstanceId = 'zone-1';
    h.development.installed.push({ slotId: 'free', definitionId: 'building_old', kind: 'building', infrastructureUpkeep: {}, jobCapacity: {}, upgrades: [{ sourceDefinitionId: 'building_old', definitionId: 'building_new', slotId: 'free', zoneInstanceId: 'zone-1', expectedColonyRevision: 4, eligibility: 'available', canAfford: true, canEnqueue: true, commandType: 'enqueueUpgrade', gates: [], quote: { policyId: 'upgrade-policy', durationDays: 10, costs: { minerals: 40 }, infrastructureUpkeep: {}, jobCapacity: {} } }] });
    h.update();
    h.inspect('installed', 'free', 'building_old').click();
    const upgrade = h.buttons().find(button => button.dataset.constructionType === 'enqueueUpgrade');
    assert.ok(upgrade); assert.match(upgrade.textContent, /^Улучшить/);
    assert.equal(upgrade.className, 'planet-upgrade-target'); assert.equal(upgrade.dataset.availability, 'available');
    assert.match(upgrade['aria-label'], /building_new/);
    const before = structuredClone(h.state);
    upgrade.click();
    assert.deepEqual(h.owners[0].commands[0], { commandId: 'id-0', type: 'enqueueUpgrade', colonyId: 'C1', expectedColonyRevision: 4, sourceDefinitionId: 'building_old', definitionId: 'building_new', slotId: 'free', zoneInstanceId: 'zone-1', orderId: 'test-order-1' });
    const pendingUpgrade = h.buttons().find(button => button.dataset.constructionType === 'enqueueUpgrade'); assert.ok(pendingUpgrade?.disabled);
    pendingUpgrade.click(); assert.equal(h.owners[0].commands.length, 1);
    upgrade.onclick(); assert.equal(h.owners[0].commands.length, 1);
    h.owners[0].pending.delete('id-0'); h.owners[0].callbacks.onResult({ commandId: 'id-0', accepted: true, code: 'OK', message: 'Accepted' });
    assert.deepEqual(h.state, before);
    assert.equal(h.buttons().find(button => button.dataset.constructionType === 'enqueueUpgrade')?.disabled, false);
    assert.equal(h.owners[0].commands.length, 1);
});

test('Toggle установленного здания использует точную capability, не блокируется ожидающим upgrade и отвергает старый callback', async () =>
{
    const panel = (root, model, options) => renderPlanetWindowView(root, model, { ...options, appendConstruction(parent, identity) { const result = options.appendConstruction(parent, identity); for (const child of parent.children ?? []) { if (child.tagName) { child.tagName = child.tagName.toUpperCase(); } } return result; } });
    const h = await constructionHarness(1, panel);
    h.development.slots[0].installedDefinitionId = 'building_old';
    h.development.slots[0].reservedByOrderId = 'upgrade-order';
    h.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'building_old', enabled: false,
        toggle: { canToggle: true, targetEnabled: true, gates: [] }, infrastructureUpkeep: {}, jobCapacity: {}, upgrades: [] }];
    h.development.queue.push({ orderId: 'upgrade-order', slotId: 'free', definitionId: 'building_new', kind: 'building', status: 'pending', gates: [] });
    assert.equal(constructionOperations({ id: 'C1', gates: [], development: h.development }).some(item => item.type === 'setBuildingEnabled'), true);
    h.update();
    const installedInspection = () => h.buttons().find(button => { try { const value = JSON.parse(button.dataset.inspectionIdentity); return value?.kind === 'installed' && value.slotId === 'free' && value.definitionId === 'building_old'; } catch { return false; } });
    installedInspection().click();
    const action = h.buttons().find(button => button.dataset.constructionType === 'setBuildingEnabled');
    assert.ok(action); assert.equal(action.textContent, 'Включить'); assert.equal(action.disabled, false);
    action.click();
    assert.deepEqual(h.owners[0].commands[0], { commandId: 'id-0', type: 'setBuildingEnabled', colonyId: 'C1', expectedColonyRevision: 4, definitionId: 'building_old', slotId: 'free', enabled: true });
    assert.equal(h.buttons().find(button => button.dataset.constructionType === 'setBuildingEnabled').disabled, true, 'in-flight toggle блокирует повтор');
    action.onclick(); assert.equal(h.owners[0].commands.length, 1);

    const stale = await constructionHarness(1, panel);
    stale.development.slots[0].installedDefinitionId = 'building_old';
    stale.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'building_old', enabled: true,
        toggle: { canToggle: true, targetEnabled: false, gates: [] }, infrastructureUpkeep: {}, jobCapacity: {}, upgrades: [] }];
    stale.update();
    stale.buttons().find(button => { try { const value = JSON.parse(button.dataset.inspectionIdentity); return value?.kind === 'installed' && value.slotId === 'free' && value.definitionId === 'building_old'; } catch { return false; } }).click();
    const oldAction = stale.buttons().find(button => button.dataset.constructionType === 'setBuildingEnabled');
    assert.ok(oldAction);
    stale.development.installed[0].enabled = false; stale.development.installed[0].toggle.targetEnabled = true;
    oldAction.onclick();
    assert.equal(stale.owners[0].commands.length, 0, 'изменённое состояние делает старый fingerprint недействительным');
});

async function zoneReplacementHarness(removed = true)
{
    const panel = (root, model, options) => renderPlanetWindowView(root, model, { ...options, appendConstruction(parent, identity) { const result = options.appendConstruction(parent, identity); for (const child of parent.children ?? []) { if (child.tagName) child.tagName = child.tagName.toUpperCase(); } return result; } });
    const h = await constructionHarness(1, panel);
    const quote = { policyId: 'zone-replacement-test', costs: { minerals: 850 }, durationDays: 360, infrastructureUpkeep: {}, jobCapacity: { foundry: 300 } };
    const target = { sourceDefinitionId: 'zone_industrial', definitionId: 'zone_foundry', slotId: 'zone-slot', zoneInstanceId: 'zone-instance', expectedColonyRevision: 4,
        previewId: 'zone-preview', removedBuildings: removed ? [{ slotId: 'factory-slot', definitionId: 'building_factory_1', localizationKey: 'building_factory_1' }] : [], quote, canAfford: true, canEnqueue: true, gates: [] };
    Object.assign(h.development, {
        startupDistricts: [{ instanceId: 'city-instance', definitionId: 'district_city', level: 3 }],
        startupZones: [{ instanceId: 'zone-instance', districtInstanceId: 'city-instance', slotInstanceId: 'zone-slot', zoneId: 'zone_industrial', slotNumber: 2 }],
        startupBuildings: [], catalog: [], queue: [],
        slots: [{ slotId: 'zone-slot', kind: 'zone', zoneInstanceId: 'zone-instance', installedDefinitionId: 'zone_industrial', reservedByOrderId: null, gates: [] },
            ...['factory-slot', 'empty-1', 'empty-2'].map(slotId => ({ slotId, kind: 'building', zoneInstanceId: 'zone-instance', installedDefinitionId: removed && slotId === 'factory-slot' ? 'building_factory_1' : null, reservedByOrderId: null, gates: [] }))],
        installed: [{ slotId: 'zone-slot', kind: 'zone', definitionId: 'zone_industrial', zoneReplacements: [target] },
            ...(removed ? [{ slotId: 'factory-slot', kind: 'building', definitionId: 'building_factory_1', enabled: true }] : [])]
    });
    h.update();
    h.openReplacement = () =>
    {
        h.buttons().find(button => { try { return JSON.parse(button.dataset.inspectionIdentity)?.kind === 'zoneCatalog'; } catch { return false; } }).click();
        h.buttons().find(button => button.textContent === 'Заменить').click();
        return h.buttons().find(button => button.dataset.constructionType === 'enqueueZoneReplacement');
    };
    h.confirm = () => h.el('planet-zone-replacement-content').children[3].children.find(button => button.textContent === 'Заменить');
    return h;
}

test('Замена зоны подтверждает точные имена без возврата, отмена не отправляет, confirm отправляет одну strict команду', async () =>
{
    const h = await zoneReplacementHarness(); const camera = h.renderers[0].camera.position.value;
    const before = structuredClone(h.development); const choice = h.openReplacement(); assert.ok(choice); choice.click();
    assert.equal(h.el('planet-zone-replacement-dialog').open, true); assert.equal(h.owners[0].commands.length, 0);
    const contentText = node => [node.textContent, ...node.children.map(contentText)].join(' ');
    assert.match(contentText(h.el('planet-zone-replacement-content')), /building_factory_1/);
    assert.match(contentText(h.el('planet-zone-replacement-content')), /Возврат ресурсов не предусмотрен/);
    h.el('planet-zone-replacement-content').children[3].children.find(button => button.textContent === 'Отмена').click();
    assert.equal(h.el('planet-zone-replacement-dialog').open, false); assert.equal(h.owners[0].commands.length, 0);
    choice.click(); const confirm = h.confirm(); confirm.click(); confirm.click();
    assert.deepEqual(h.owners[0].commands, [{ commandId: 'id-0', type: 'enqueueZoneReplacement', colonyId: 'C1', expectedColonyRevision: 4,
        definitionId: 'zone_foundry', slotId: 'zone-slot', orderId: 'test-order-1', sourceDefinitionId: 'zone_industrial', zoneInstanceId: 'zone-instance', zoneReplacementPreviewId: 'zone-preview', confirmZoneRemoval: true }]);
    assert.deepEqual(h.development, before, 'Клиент не применяет removal/completion до серверного snapshot');
    assert.equal(h.renderers[0].camera.position.value, camera);
});

test('Замена зоны с пустым списком заказывается одним кликом без модального согласия', async () =>
{
    const h = await zoneReplacementHarness(false); h.openReplacement().click();
    assert.equal(h.el('planet-zone-replacement-dialog').open, false); assert.equal(h.owners[0].commands.length, 1);
    assert.equal(h.owners[0].commands[0].type, 'enqueueZoneReplacement'); assert.equal(h.owners[0].commands[0].confirmZoneRemoval, false);
});

test('Согласие замены зоны не привязывается к новому snapshot/POV/сессии/вкладке и закрывается вместе с окном', async () =>
{
    for (const change of ['snapshot', 'revision', 'pov', 'session', 'tab', 'close', 'connection'])
    {
        const h = await zoneReplacementHarness(); h.openReplacement().click(); const confirm = h.confirm();
        if (change === 'revision') h.development.revision++;
        if (change === 'pov') h.state.viewContext = { mode: 'general', participantId: null };
        if (change === 'session') h.state.sessionId = 'new-session';
        if (['snapshot', 'revision', 'pov', 'session'].includes(change)) h.update();
        if (change === 'tab') h.tabs.jobs.onclick();
        if (change === 'close') h.el('planet-dialog').close();
        if (change === 'connection') { h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Отключено', false); }
        confirm.click();
        assert.equal(h.owners[0].commands.length, 0, change);
        assert.equal(h.el('planet-zone-replacement-dialog').open, false, change);
    }
});

test('Demolition dialog требует подтверждение без возврата, отменяет без команды и отвергает изменённый snapshot', async () =>
{
    const panel = (root, model, options) => renderPlanetWindowView(root, model, { ...options, appendConstruction(parent, identity) { const result = options.appendConstruction(parent, identity); for (const child of parent.children ?? []) { if (child.tagName) child.tagName = child.tagName.toUpperCase(); } return result; } });
    const h = await constructionHarness(1, panel);
    h.development.slots[0].installedDefinitionId = 'building_old';
    h.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'building_old', localizationKey: 'BUILDING_OLD', name: 'Старое здание', demolition: { canDemolish: true, gates: [] } }];
    h.update();
    const inspectInstalled = (view, definitionId) => view.buttons().find(button => { try { const value = JSON.parse(button.dataset.inspectionIdentity); return value?.kind === 'installed' && value.slotId === 'free' && value.definitionId === definitionId; } catch { return false; } });
    inspectInstalled(h, 'building_old').click();
    const demolish = () => h.buttons().find(button => button.dataset.constructionType === 'demolishBuilding');
    const action = demolish(); assert.ok(action); assert.equal(action.textContent, 'Демонтировать');
    action.click();
    const content = h.el('planet-demolition-content');
    assert.equal(h.el('planet-demolition-dialog').open, true);
    const contentText = node => [node.textContent, ...node.children.map(contentText)].join(' ');
    assert.match(contentText(content), /Старое здание/); assert.match(contentText(content), /Возврат ресурсов не предусмотрен/);
    const confirm = content.children[2].children.find(button => button.textContent === 'Демонтировать');
    assert.ok(confirm); assert.ok(content.children[2].children.some(button => button.textContent === 'Отмена'));
    content.children[2].children.find(button => button.textContent === 'Отмена').click();
    assert.equal(h.el('planet-demolition-dialog').open, false); assert.equal(h.owners[0].commands.length, 0);
    demolish().click();
    const currentConfirm = h.el('planet-demolition-content').children[2].children.find(button => button.textContent === 'Демонтировать');
    currentConfirm.click(); currentConfirm.click();
    assert.deepEqual(h.owners[0].commands[0], { commandId: 'id-0', type: 'demolishBuilding', colonyId: 'C1', expectedColonyRevision: 4, definitionId: 'building_old', slotId: 'free', confirmDemolition: true });

    let stale;
    stale = await constructionHarness(1, panel);
    stale.development.slots[0].installedDefinitionId = 'building_old';
    stale.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'building_old', demolition: { canDemolish: true, gates: [] } }];
    stale.update(); inspectInstalled(stale, 'building_old').click();
    stale.buttons().find(button => button.dataset.constructionType === 'demolishBuilding').click();
    const staleConfirm = stale.el('planet-demolition-content').children[2].children.find(button => button.textContent === 'Демонтировать');
    stale.development.revision++; stale.update();
    assert.equal(stale.el('planet-demolition-dialog').open, false, 'state update closes the stale confirmation');
    staleConfirm.click();
    assert.equal(stale.owners[0].commands.length, 0, 'confirm callback cannot rebind to a newer snapshot');
    assert.equal(stale.el('planet-dialog').open, true, 'stale confirmation leaves the planet window open');
});

test('Удаление осматриваемого instance закрывает только inspector; upgrade сохраняет его на том же слоте', async () =>
{
    const panel = (root, model, options) => renderPlanetWindowView(root, model, { ...options, appendConstruction(parent, identity) { const result = options.appendConstruction(parent, identity); for (const child of parent.children ?? []) { if (child.tagName) child.tagName = child.tagName.toUpperCase(); } return result; } });
    const removed = await constructionHarness(1, panel);
    removed.development.slots[0].installedDefinitionId = 'building_old';
    removed.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'building_old', demolition: { canDemolish: true, gates: [] } }];
    removed.update();
    const select = (view, id) => view.buttons().find(button => { try { const value = JSON.parse(button.dataset.inspectionIdentity); return value?.kind === 'installed' && value.slotId === 'free' && value.definitionId === id; } catch { return false; } });
    select(removed, 'building_old').click();
    assert.ok(removed.buttons().some(button => button.dataset.inspectionIdentity === 'null'));
    removed.development.slots[0].installedDefinitionId = null; removed.development.installed = []; removed.update();
    assert.equal(removed.el('planet-dialog').open, true); assert.equal(removed.buttons().some(button => button.dataset.inspectionIdentity === 'null'), false);

    const upgraded = await constructionHarness(1, panel);
    upgraded.development.slots[0].installedDefinitionId = 'building_old';
    upgraded.development.installed = [{ slotId: 'free', kind: 'building', definitionId: 'building_old', demolition: { canDemolish: true, gates: [] } }];
    upgraded.update(); select(upgraded, 'building_old').click();
    upgraded.development.slots[0].installedDefinitionId = 'building_new'; upgraded.development.installed[0].definitionId = 'building_new'; upgraded.update();
    assert.equal(upgraded.el('planet-dialog').open, true); assert.ok(upgraded.buttons().some(button => button.dataset.inspectionIdentity === 'null'));
    assert.ok(select(upgraded, 'building_new'), 'same occupied slot retains the installed inspector after upgrade');
});

for (const change of ['revision', 'source', 'target', 'zone', 'quote', 'option-removal', 'can-afford-false', 'can-afford-null', 'can-enqueue', 'gates', 'observer'])
{
    test(`Обработчик улучшения отклоняет устаревшее состояние: ${change}`, async () =>
    {
        const h = await constructionHarness(); h.development.slots[0].installedDefinitionId = 'building_old'; h.development.slots[0].zoneInstanceId = 'zone-1';
        const option = { sourceDefinitionId: 'building_old', definitionId: 'building_new', slotId: 'free', zoneInstanceId: 'zone-1', expectedColonyRevision: 4, eligibility: 'available', canAfford: true, canEnqueue: true, commandType: 'enqueueUpgrade', gates: [], quote: { policyId: 'upgrade-policy', durationDays: 10, costs: { minerals: 40 }, infrastructureUpkeep: {}, jobCapacity: {} } };
        h.development.installed.push({ slotId: 'free', definitionId: 'building_old', kind: 'building', infrastructureUpkeep: {}, jobCapacity: {}, upgrades: [option] }); h.update();
        h.inspect('installed', 'free', 'building_old').click(); const button = h.buttons().find(item => item.dataset.constructionType === 'enqueueUpgrade'); assert.ok(button);
        if (change === 'revision') { h.development.revision++; }
        if (change === 'source') { option.sourceDefinitionId = 'building_changed'; }
        if (change === 'target') { option.definitionId = 'building_changed'; }
        if (change === 'zone') { option.zoneInstanceId = 'zone-2'; }
        if (change === 'quote') { option.quote.costs.minerals++; }
        if (change === 'option-removal') { h.development.installed[0].upgrades = []; }
        if (change === 'can-afford-false') { option.canAfford = false; }
        if (change === 'can-afford-null') { option.canAfford = null; }
        if (change === 'can-enqueue') { option.canEnqueue = false; }
        if (change === 'gates') { option.gates = [{ code: 'blocked' }]; }
        if (change === 'observer') { h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true }); h.state.viewContext.participantId = 3; }
        button.onclick(); assert.equal(h.owners[0].commands.length, 0); assert.ok(h.el('error').textContent);
    });
}

test('Construction remains read-only for observers and feature clearance has no invented slot mapping', async () =>
{
    const h = await constructionHarness(); const before = structuredClone(h.state);
    h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true }); h.update(); h.openJobs(); h.tabs.infrastructure.onclick();
    assert.equal(h.build().disabled, true); assert.equal(h.cancel().disabled, true); h.build().onclick(); h.cancel().onclick(); assert.equal(h.owners[0].commands.length, 0);
    h.tabs.features.onclick(); assert.equal(h.buttons().length, 0);
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    assert.match(text(h.el('planet-content')), /связь экземпляра особенности/);
    assert.deepEqual(h.state.development, before.development);
});

for (const gate of ['global', 'colony', 'catalog', 'slot', 'null-quote', 'bad-duration', 'bad-cost', 'wrong-kind', 'duplicate-slot', 'duplicate-definition', 'invalid-id', 'unsafe-revision'])
{
    test(`Construction cannot enable enqueue with ${gate}`, async () =>
    {
        const h = await constructionHarness(); const reason = [{ code: 'unresolved', dependencyId: 'test' }];
        if (gate === 'global') { h.state.development.gates = reason; }
        if (gate === 'colony') { h.development.gates = reason; }
        if (gate === 'catalog') { h.development.catalog[0].gates = reason; }
        if (gate === 'slot') { h.development.slots[0].gates = reason; }
        if (gate === 'null-quote') { h.development.catalog[0].quote = null; }
        if (gate === 'bad-duration') { h.development.catalog[0].quote.durationDays = 0; }
        if (gate === 'bad-cost') { h.development.catalog[0].quote.costs.minerals = -1; }
        if (gate === 'wrong-kind') { h.development.slots[0].kind = 'zone'; }
        if (gate === 'duplicate-slot') { h.development.slots.push(structuredClone(h.development.slots[0])); }
        if (gate === 'duplicate-definition') { h.development.catalog.push(structuredClone(h.development.catalog[0])); }
        if (gate === 'invalid-id') { h.development.catalog[0].definitionId = 'x'.repeat(161); }
        if (gate === 'unsafe-revision') { h.development.revision = Number.MAX_SAFE_INTEGER + 1; }
        h.update(); assert.equal(h.build(), undefined); assert.equal(h.owners[0].commands.length, 0);
    });
}

test('Cancellation revalidates received status, revision and refund without fabricating a refund', async () =>
{
    for (const change of ['refund', 'status', 'revision', 'gate', 'missing'])
    {
        const h = await constructionHarness(); const button = h.cancel();
        if (change === 'refund') { h.development.queue[0].refundQuote.amounts.minerals++; }
        if (change === 'status') { h.development.queue[0].status = 'completed'; }
        if (change === 'revision') { h.development.revision++; }
        if (change === 'gate') { h.development.queue[0].gates = [{ code: 'unresolved', dependencyId: 'refund' }]; }
        if (change === 'missing') { h.development.queue[0].refundQuote = null; }
        const before = structuredClone(h.state); button.onclick(); assert.equal(h.owners[0].commands.length, 0); assert.deepEqual(h.state, before);
        h.update(); if (change !== 'refund' && change !== 'revision') { assert.equal(h.cancel(), undefined); }
    }
});

async function clearanceHarness(actor = 1)
{
    const h = await constructionHarness(actor);
    h.development.features[0].clearanceSlotId = 'clearance-slot';
    h.development.slots.push({ slotId: 'clearance-slot', kind: 'blockerClearance', reservedByOrderId: null, installedDefinitionId: null, gates: [] });
    h.development.catalog.push({ definitionId: 'test-clearance', kind: 'blockerClearance', eligibility: 'available', slotIds: ['clearance-slot'], imageDefinitionKey: null, gates: [], quote: { policyId: 'test-clearance-policy', costs: { energy: 25 }, durationDays: 3, infrastructureUpkeep: {}, jobCapacity: {} } });
    h.update(); h.tabs.features.onclick(); h.clear = () => h.buttons().find(node => node.textContent.startsWith('Расчистить'));
    return h;
}

for (const actor of [1, 2])
{
    test(`Clearance actor ${actor} uses exact received feature-slot binding and snapshot-only effects`, async () =>
    {
        const h = await clearanceHarness(actor); const owner = h.owners[0]; const before = structuredClone(h.state);
        const text = node => [node.textContent, ...node.children.map(text)].join(' ');
        assert.match(text(h.el('planet-content')), /Энергия: 25/); assert.match(text(h.el('planet-content')), /3 дней/);
        const button = h.clear(); button.click();
        assert.deepEqual(owner.commands[0], { commandId: 'id-0', type: 'clearBlocker', colonyId: 'C1', expectedColonyRevision: 4, definitionId: 'test-clearance', slotId: 'clearance-slot', orderId: 'test-order-1' });
        assert.ok(h.buttons().every(item => item.disabled)); button.onclick(); assert.equal(owner.commands.length, 1);
        owner.pending.delete('id-0'); owner.callbacks.onResult({ commandId: 'id-0', accepted: true, code: 'OK', message: 'Accepted' });
        assert.deepEqual(h.state, before); assert.ok(h.clear());
        h.development.features = []; h.development.revision++; h.update(); assert.equal(h.clear(), undefined);
    });
}

for (const change of ['missing-slot', 'null-slot', 'wrong-slot-kind', 'wrong-definition', 'slot-not-listed', 'duplicate-feature', 'duplicate-instance', 'feature-gate', 'passive', 'reserved', 'quote', 'instance-replaced', 'revision', 'tab', 'tab-return'])
{
    test(`Clearance rejects stale or ambiguous ${change} without deriving a mapping`, async () =>
    {
        const h = await clearanceHarness(); const button = h.clear();
        if (change === 'missing-slot') { delete h.development.features[0].clearanceSlotId; }
        if (change === 'null-slot') { h.development.features[0].clearanceSlotId = null; }
        if (change === 'wrong-slot-kind') { h.development.slots[1].kind = 'building'; }
        if (change === 'wrong-definition') { h.development.features[0].clearanceDefinitionId = 'other-clearance'; }
        if (change === 'slot-not-listed') { h.development.catalog[1].slotIds = []; }
        if (change === 'duplicate-feature') { h.development.features.push({ ...h.development.features[0], instanceId: 'feature-B' }); }
        if (change === 'duplicate-instance') { h.development.features.push({ ...h.development.features[0], clearanceSlotId: 'another-slot' }); }
        if (change === 'feature-gate') { h.development.features[0].gates = [{ code: 'unresolved', dependencyId: 'effects' }]; }
        if (change === 'passive') { h.development.features[0].clearable = false; }
        if (change === 'reserved') { h.development.slots[1].reservedByOrderId = 'another-order'; }
        if (change === 'quote') { h.development.catalog[1].quote.costs.energy++; }
        if (change === 'instance-replaced') { h.development.features[0].instanceId = 'feature-B'; }
        if (change === 'revision') { h.development.revision++; }
        if (change === 'tab') { h.tabs.infrastructure.onclick(); }
        if (change === 'tab-return') { h.tabs.infrastructure.onclick(); h.tabs.features.onclick(); }
        const before = structuredClone(h.state); button.onclick(); assert.equal(h.owners[0].commands.length, 0); assert.deepEqual(h.state, before); assert.ok(h.el('error').textContent);
        h.update();
        if (!['quote', 'instance-replaced', 'revision', 'tab-return'].includes(change)) { assert.equal(h.clear(), undefined); }
    });
}

test('Matching observer clearance is read-only and unavailable quotes never enable a clear command', async () =>
{
    const h = await clearanceHarness(); h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true }); h.update(); h.openJobs(); h.tabs.features.onclick();
    assert.equal(h.clear().disabled, true); h.clear().onclick(); assert.equal(h.owners[0].commands.length, 0);
    h.development.catalog[1].quote = null; h.update(); assert.equal(h.clear(), undefined);
    h.tabs.infrastructure.onclick(); assert.equal(h.buttons().filter(button => button.textContent.startsWith('Расчистить')).length, 0);
});

test('Research panel sends only a current owned choice and clears its pending state on ACK', async () =>
{
    const h = harness();
    h.state.research = { empires: [{ participantId: 1, revision: 7, areas: ['physics', 'society', 'engineering'].map(id => ({ id, activeTechnologyId: null, progress: 0, monthlyResearch: 10, reserve: 20, choices: [{ id: `${id}-one`, name: 'Технология', area: id, cost: 100, effects: [{ description: 'Бонус', status: 'deferred' }] }] })), researched: [] }] };
    await h.importFiles(); h.ready(); h.el('research-toggle').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-physics').onclick();
    const choose = descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one');
    choose.onclick();
    assert.deepEqual(h.owners[0].commands.at(-1), { type: 'selectResearch', area: 'physics', technologyId: 'physics-one', expectedResearchRevision: 7, commandId: 'id-0' });
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one').disabled, true);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-society').onclick();
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-society-society-one').disabled, true);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-engineering').onclick();
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-engineering-engineering-one').disabled, true);
    h.owners[0].callbacks.onResult({ commandId: 'id-0', accepted: true, code: 'ok', message: 'accepted' });
    assert.equal(h.owners[0].commands.length, 1);
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-engineering-engineering-one').disabled, false);
});

test('Периодический снимок без изменений исследования сохраняет нажатую кнопку выбора', async () =>
{
    const h = harness();
    h.state.research = { empires: [{ participantId: 1, revision: 7, areas: [{ id: 'physics', activeTechnologyId: null, progress: 0, monthlyResearch: 10, reserve: 20, choices: [{ id: 'physics-one', name: 'Технология', area: 'physics', cost: 100, effects: [] }] }], researched: [] }] };
    await h.importFiles(); h.ready(); h.el('research-toggle').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-physics').click();
    const choice = descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one');
    h.state.date.day++;
    h.owners[0].callbacks.onState(structuredClone(h.state), {});
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one'), choice);
    choice.click();
    assert.equal(h.owners[0].commands.at(-1)?.type, 'selectResearch');
});

test('Потеря свежести обновляет research choice и отвергает старый callback', async () =>
{
    const h = harness();
    h.state.research = { empires: [{ participantId: 1, revision: 7, areas: [{ id: 'physics', activeTechnologyId: null, choices: [{ id: 'physics-one', name: 'Технология', area: 'physics', cost: 100, effects: [] }] }], researched: [] }] };
    await h.importFiles(); h.ready(); h.el('research-toggle').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-physics').click();
    const choice = descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one');
    h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Ожидание снимка', false);
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one').disabled, true);
    choice.onclick();
    assert.equal(h.owners[0].commands.some(command => command.type === 'selectResearch'), false);
});

test('Игровой research consumer передаёт локальному resolver исходные ключи и параметры', async () =>
{
    const h = harness({ localizationValues: { synthetic_tech: 'Тестовое исследование', synthetic_building: 'Тестовое сооружение', PHYSICS: 'Тестовая область', TECH_UNLOCK_BUILDING: 'Тестовая строка открытия' } });
    h.state.research = { empires: [{ participantId: 1, revision: 7, areas: [{ id: 'physics', activeTechnologyId: null, progress: 0, monthlyResearch: 10, reserve: 20, choices: [{ id: 'synthetic_id', nameKey: 'synthetic_tech', cost: 100, effects: [{ sourceKey: 'unlock_building', targetDefinitionId: 'synthetic_building_id', localizationKey: 'synthetic_building', status: 'active' }] }] }], researched: [] }] };
    await h.importFiles(); h.ready(); h.el('research-toggle').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-physics').onclick();
    assert.ok(descendants(h.el('research-side')).some(node => node.textContent === 'Тестовое исследование'));
    const unlock = h.options.localizationResolveRequests.find(call => call.key === 'TECH_UNLOCK_BUILDING');
    assert.deepEqual(structuredClone(unlock.request.parameters), { NAME: 'Тестовое сооружение' });
    const area = h.options.localizationResolveRequests.find(call => call.key === 'SELECT_TECHNOLOGY');
    assert.deepEqual(structuredClone(area.request.parameters), { AREA: 'Тестовая область' });
});

for (const reset of ['perspective', 'logout'])
{
test(`Диагностика исследований видима и очищается: ${reset}`, async () =>
{
    const h = harness({
        localizationValues: { private_reference: 'Тестовая незавершённая ссылка', private_scope: 'Тестовая область' },
        localizationDiagnostics: { private_reference: [{ code: 'unknown-reference' }], private_scope: [{ code: 'unsupported-scope' }] }
    });
    h.state.research = { empires: [{ participantId: 1, revision: 7, areas: [{ id: 'physics', activeTechnologyId: null, choices: [{ id: 'synthetic_id', nameKey: 'private_research_missing', descriptionKey: 'private_reference', categoryKey: 'private_scope', effects: [] }] }], researched: [] }] };
    await h.importFiles(); h.ready(); h.el('research-toggle').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    const text = node => [node.textContent, ...descendants(node).map(child => child.textContent)].join(' ');
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-physics').onclick();
    const status = descendants(h.el('research-panel')).find(node => node.className?.includes('localization-diagnostics'));
    assert.ok(status); assert.match(text(status), /private_research_missing.*missing-key/);
    assert.match(text(status), /private_reference.*unknown-reference/); assert.match(text(status), /private_scope.*unsupported-scope/);
    if (reset === 'perspective')
    {
        h.state.you = 3; h.state.participants.push({ id: 3, role: 'observer', assetReady: true }); h.state.viewContext = { mode: 'general', participantId: null };
        h.owners[0].callbacks.onState(h.state, {});
    }
    else { h.el('clear-login').onclick(); }
    assert.doesNotMatch(text(h.el('research-panel')) + text(h.el('research-side')), /private_research_missing|private_reference|private_scope|synthetic_id/);
});
}

test('Research proposal handler rejects stale revisions and observer POV remains read-only', async () =>
{
    const h = harness();
    h.state.research = { empires: [{ participantId: 1, revision: 7, areas: ['physics'].map(id => ({ id, activeTechnologyId: null, progress: 0, monthlyResearch: 10, reserve: 20, choices: [{ id: `${id}-one`, name: 'Технология', area: id, cost: 100, effects: [] }] })), researched: [] }] };
    await h.importFiles(); h.ready(); h.el('research-toggle').onclick();
    const descendants = node => node.children.flatMap(child => [child, ...descendants(child)]);
    descendants(h.el('research-panel')).find(node => node.dataset.testid === 'research-choose-physics').onclick();
    const oldChoice = descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one');
    h.state.research.empires[0] = { ...h.state.research.empires[0], revision: 8 }; oldChoice.onclick();
    assert.equal(h.owners[0].commands.filter(command => command.type === 'selectResearch').length, 0);
    h.state.you = 2; h.state.participants.push({ id: 2, role: 'observer' }); h.state.viewContext = { mode: 'player', participantId: 1 };
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(descendants(h.el('research-side')).find(node => node.dataset.testid === 'research-select-physics-physics-one').disabled, true);
});

test('M3 UI: реальные handlers публикуют профиль после GPU и учитывают старый CPU/converted', async () =>
{
    const h = harness(); await h.importFiles(); assert.equal(h.owners[0].profiles.length, 1); assert.equal(h.renderers[0].profiles.length, 1);
    await h.importFiles(); assert.equal(h.imports[1].maxDecodedBytes, 512 * 1048576 - 100 - 40 - 16 * 1048576);
    assert.equal(h.renderers[0].profiles[1].limits.maxConvertedBytes, 512 * 1048576 - 200 - 16 * 1048576);
    h.options.gpuError = true; await h.importFiles(); assert.equal(h.owners[0].profiles.length, 2); assert.match(h.el('error').textContent, /GPU failure/);
});

for (const retained of [false, true])
{
    test(`Ошибка лимита памяти объясняет отказ по-русски, прежний набор: ${retained}`, async () =>
    {
        const h = harness();
        if (retained) { await h.importFiles(); }
        const reason = 'M3 profile: Exceeded decoded memory budget before decoding. Reload the page, then reimport.';
        h.options.importFailure = new Error(reason); await h.importFiles();
        const message = h.el('error').textContent;
        assert.match(message, /Импорт не выполнен: превышен лимит памяти/);
        assert.match(message, /Перезагрузите страницу и повторите импорт/);
        assert.equal(message.includes('Прежний набор сохранён.'), retained);
        assert.ok(message.endsWith(`Техническая причина: ${reason}`));
        assert.equal(h.owners[0].profiles.length, retained ? 1 : 0);
        assert.equal(h.renderers[0].profiles.length, retained ? 1 : 0);
        if (retained) { assert.equal(h.imports[1].maxDecodedBytes, (512 - 16) * 1048576 - 100 - 40); }
    });
}
test('M3 UI: ошибка иконок не вызывает GPU publish/assetReady', async () =>
{
    const h = harness(); h.options.noCanvas = true; await h.importFiles(); assert.equal(h.renderers[0].profiles.length, 0); assert.equal(h.owners[0].profiles.length, 0); assert.match(h.el('error').textContent, /Canvas2D/);
});
test('M3 UI: поздний импорт после pagehide не публикуется', async () =>
{
    const h = harness(); let release; h.options.deferred = new Promise(resolve => { release = resolve; });
    h.el('files').files = [{}]; h.el('files').onchange(); await h.settle();
    await h.window.emit('pagehide'); release(); await h.settle(); assert.equal(h.owners[0].profiles.length, 0); assert.equal(h.renderers[0].profiles.length, 0); assert.equal(h.renderers[0].disposed, true);
});
test('M3 UI: кнопки и callback intents disabled в stress/для observer, доступны своему fleet', async () =>
{
    const h = harness(); await h.importFiles(); h.ready(); h.el('destination').value = 'S01'; h.el('move-system').onclick(); assert.equal(h.owners[0].commands.length, 1);
    h.el('stress').onclick(); h.el('move-system').onclick(); assert.equal(h.owners[0].commands.length, 1); assert.equal(h.el('move-system').disabled, true);
    h.el('galaxy').onclick(); h.state.participants[0].role = 'observer'; h.owners[0].callbacks.onState(h.state, {}); h.el('move-system').onclick(); assert.equal(h.owners[0].commands.length, 1);
});
test('M3 UI: system zoom-out callback returns local identity to galaxy without command or fleet changes', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    h.el('destination').value = 'S01'; h.el('move-system').onclick();
    const commands = h.owners[0].commands.length;
    h.renderers[0].callbacks.onOpenSystem('S01');
    assert.equal(h.el('view-name').textContent, 'Система');
    assert.deepEqual(h.el('camera-preset').options.map(option => option.value), ['system-overview', 'system-close']);
    h.renderers[0].callbacks.onSystemZoomOut();
    assert.equal(h.el('view-name').textContent, 'Галактика');
    assert.deepEqual(h.el('camera-preset').options.map(option => option.value), ['galaxy-far', 'galaxy-near']);
    assert.equal(h.el('command-fleet').textContent, 'Флот');
    assert.equal(h.owners[0].commands.length, commands);
    h.el('stress').onclick(); h.renderers[0].callbacks.onSystemZoomOut();
    assert.equal(h.el('view-name').textContent, 'Галактика');
});
test('M3 UI: ACK не разблокирует приказы после context loss/restore без нового снимка', async () =>
{
    const h = harness(); await h.importFiles(); h.ready(); h.el('destination').value = 'S01';
    h.renderers[0].callbacks.onContextState('lost'); h.owners[0].callbacks.onState(h.state, {}); h.owners[0].callbacks.onStatus('Подключено', true);
    h.el('move-system').onclick(); assert.equal(h.owners[0].commands.length, 0);
    h.renderers[0].callbacks.onContextState('restored'); h.owners[0].callbacks.onStatus('Подключено', true);
    h.el('move-system').onclick(); assert.equal(h.owners[0].commands.length, 0); assert.equal(h.owners[0].refresh, true);
    h.owners[0].callbacks.onState(h.state, {}); h.el('move-system').onclick(); assert.equal(h.owners[0].commands.length, 1);
});

test('M3 UI: clear-login отменяет поздний импорт и не публикует прежний источник', async () =>
{
    const h = harness(); await h.importFiles();
    let release; h.options.deferred = new Promise(resolve => { release = resolve; });
    h.el('files').files = [{}]; h.el('files').onchange(); await h.settle();
    const cancels = h.options.cancelCount;
    h.el('clear-login').onclick();
    assert.ok(h.options.cancelCount > cancels, 'Очистка должна отменить worker.');
    release(); await h.settle();
    assert.equal(h.owners[0].profiles.length, 1, 'Поздний старый набор не подтверждает новый вход.');
    assert.equal(h.renderers.length, 1, 'Поздний импорт не пересоздаёт renderer.');
    assert.equal(h.renderers[0].disposed, true);
    assert.equal(h.el('cancel-import').disabled, true);
});

test('M3 UI: clear-login инвалидирует ещё открытый directory picker', async () =>
{
    const h = harness(); let release;
    h.window.showDirectoryPicker = () => new Promise(resolve => { release = resolve; });
    const picker = h.el('folder').onclick();
    h.el('clear-login').onclick(); release({ kind: 'directory' });
    await picker; await h.settle();
    assert.equal(h.imports.length, 0);
    assert.equal(h.owners[0].profiles.length, 0);
});

test('M3 UI: clear-login очищает старую партию из DOM и навигации', async () =>
{
    const h = harness(); await h.importFiles(); h.ready(); h.el('stress').onclick();
    assert.ok(h.el('participants').children.length); assert.ok(h.el('outliner').children.length);
    h.el('clear-login').onclick();
    for (const id of ['participants', 'outliner', 'resources', 'economy-content', 'traits', 'buildings', 'destination', 'commands']) { assert.equal(h.el(id).children.length, 0, id); }
    assert.equal(h.el('view-name').textContent, 'Галактика');
    assert.equal(h.el('selection').children.length, 0);
    assert.equal(h.el('command-fleet').textContent, '');
    assert.equal(h.el('stale').hidden, true); assert.equal(h.el('move-system').disabled, true);
    await h.importFiles(); h.ready(); h.el('destination').value = 'S01'; h.el('move-system').onclick();
    assert.equal(h.owners[0].commands.length, 1, 'Новая партия не наследует stress mode.');
});

test('M3 UI: outliner до импорта disabled и не вызывает renderer.select без snapshot', async () =>
{
    const h = harness(); h.owners[0].callbacks.onState(h.state, {});
    assert.ok(h.el('outliner').children.length);
    for (const button of h.el('outliner').querySelectorAll('[data-kind]')) { assert.equal(button.disabled, true); assert.doesNotThrow(() => button.click()); }
    await h.importFiles();
    assert.ok(h.el('outliner').querySelectorAll('[data-kind]').every(button => !button.disabled));
    const button = h.el('outliner').querySelectorAll('[data-kind]')[0];
    assert.doesNotThrow(() => h.el('outliner').onclick({ target: { closest: () => button } }));
});

test('Fleet command panel retains its owner through inspection and reconciles pending results without changing the route', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const text = node => [node.textContent, ...node.children.map(text)].join(' ');
    h.state.spatial.bodies.push({ id: 'B1', systemId: 'S01', name: 'Planet', kind: 'planet', ownerParticipantId: 1 });
    h.state.spatial.fleets.push({ id: 'F2', name: 'Foreign', ownerParticipantId: 2, shipCount: 2, phase: 'idle', location: { kind: 'system', systemId: 'S01' } });
    h.renderers[0].callbacks.onSelect({ kind: 'body', id: 'B1' });
    assert.equal(h.el('command-fleet').textContent, 'Флот');
    h.el('destination').value = 'S01'; h.el('move-system').onclick();
    assert.equal(h.owners[0].commands[0].fleetId, 'F1');
    assert.match(text(h.el('movement')), /Корабли 3/);
    assert.match(text(h.el('movement')), /1 · К системе/);
    assert.match(text(h.el('movement')), /Прибытие Недоступно/);
    assert.equal(h.state.spatial.fleets[0].movement, undefined);
    h.renderers[0].callbacks.onSelect({ kind: 'fleet', id: 'F2' });
    assert.equal(h.el('command-fleet').textContent, 'Флот');
    assert.match(text(h.el('selection')), /Foreign/);
    const id = h.owners[0].commands[0].commandId;
    h.owners[0].pending.delete(id);
    h.owners[0].callbacks.onResult({ commandId: id, accepted: false, code: 'OUT_OF_SCOPE', message: 'Rejected' });
    assert.match(text(h.el('movement')), /Ожидают подтверждения Нет/);
    assert.equal(h.state.spatial.fleets[0].movement, undefined);
    h.el('move-system').onclick();
    h.owners[0].connected = false; h.owners[0].callbacks.onStatus('Reconnect', false);
    assert.match(text(h.el('movement')), /1 · К системе/);
    const acceptedId = h.owners[0].commands[1].commandId;
    h.owners[0].pending.delete(acceptedId);
    h.owners[0].callbacks.onResult({ commandId: acceptedId, accepted: true, code: 'OK', message: 'Accepted' });
    assert.match(text(h.el('movement')), /Ожидают подтверждения Нет/);
    assert.equal(h.state.spatial.fleets[0].movement, undefined, 'ACK does not fabricate movement.');
    h.state.spatial.fleets[0].ownerParticipantId = 2;
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.el('order-controls').hidden, true);
    assert.equal(h.el('command-fleet').textContent, '');
});

test('Renderer point and system callbacks preserve exact commands and authority for both player actors', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const callbacks = h.renderers[0].callbacks; const owner = h.owners[0];
    h.state.spatial.systems.push({ id: 'S02', name: 'Destination' });
    h.state.spatial.bodies.push({ id: 'B1', systemId: 'S01', name: 'Planet', kind: 'planet' });
    callbacks.onSelect({ kind: 'body', id: 'B1' });
    callbacks.onPointOrder({ systemId: 'S01', x: 10, z: -20 });
    assert.deepEqual(owner.commands[0], { commandId: 'id-0', type: 'moveFleetToPoint', fleetId: 'F1', systemId: 'S01', x: 10, z: -20 });
    callbacks.onSystemOrder('S02');
    assert.deepEqual(owner.commands[1], { commandId: 'id-1', type: 'moveFleetToSystem', fleetId: 'F1', targetSystemId: 'S02' });
    assert.equal(h.state.spatial.fleets[0].movement, undefined);
    h.state.spatial.fleets[0].location = { kind: 'transit', fromSystemId: 'S01', toSystemId: 'S02' };
    callbacks.onPointOrder({ systemId: 'S01', x: 0, z: 0 });
    assert.equal(owner.commands.length, 2); assert.match(h.el('hint').textContent, /вне перехода/);
    callbacks.onSystemOrder('S01'); assert.equal(owner.commands.length, 3);
    owner.connected = false;
    owner.callbacks.onStatus('Reconnect', false); assert.equal(h.el('stale').hidden, false);
    callbacks.onPointOrder({ systemId: 'S01', x: 0, z: 0 }); callbacks.onSystemOrder('S02'); assert.equal(owner.commands.length, 3);
    owner.connected = true; owner.callbacks.onStatus('Connected', true); h.state.participants[0].role = 'observer';
    callbacks.onPointOrder({ systemId: 'S01', x: 3, z: 4 }); callbacks.onSystemOrder('S02'); assert.equal(owner.commands.length, 3);

    h.state.participants[0].role = 'player'; h.state.you = 2; h.state.viewContext.participantId = 2;
    h.state.participants.push({ id: 2, role: 'player', assetReady: true });
    h.state.spatial.fleets.push({ id: 'F2', name: 'Second actor fleet', ownerParticipantId: 2, shipCount: 1, phase: 'idle', location: { kind: 'system', systemId: 'S02' } });
    owner.callbacks.onState(h.state, {});
    callbacks.onSelect({ kind: 'fleet', id: 'F2' });
    callbacks.onSelect({ kind: 'body', id: 'B1' });
    callbacks.onPointOrder({ systemId: 'S02', x: -4, z: 8 });
    assert.deepEqual(owner.commands[3], { commandId: 'id-3', type: 'moveFleetToPoint', fleetId: 'F2', systemId: 'S02', x: -4, z: 8 });
    callbacks.onSystemOrder('S01');
    assert.deepEqual(owner.commands[4], { commandId: 'id-4', type: 'moveFleetToSystem', fleetId: 'F2', targetSystemId: 'S01' });
    assert.equal(h.state.spatial.fleets[1].movement, undefined);
});

test('После выхода из лобби новый вход не использует сохранённый токен', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const owner = h.owners[0];
    owner.callbacks.onWelcome({ profileId: 'profile', profileDigest: 'digest' }, { name: 'A', role: 'player', joinCode: 'code', token: 'old-token', sessionId: 'old-session', pending: [] });
    owner.callbacks.onStatus('Подключено', true);
    assert.equal(h.el('name').disabled, true);
    owner.disconnect(); owner.callbacks.onLobbyLeft(); owner.callbacks.onStatus('Отключено.', false);
    assert.equal(h.el('name').disabled, false);
    await h.el('join').emit('submit');
    assert.equal(owner.login.token, undefined); assert.equal(owner.login.sessionId, undefined);
    assert.equal(owner.login.pending, undefined);
});

test('Хост исключает других участников только в лобби; старые кнопки теряют права', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const owner = h.owners[0];
    h.state.phase = 'lobby';
    h.state.participants.push({ id: 2, name: 'Игрок', role: 'player', connected: true }, { id: 3, name: 'Зритель', role: 'observer', connected: true });
    const update = () => owner.callbacks.onState(h.state, {});
    const kicks = () => h.el('participants').children.flatMap(row => row.children).filter(button => button.tagName === 'button' && button.textContent === 'Исключить');
    update(); assert.equal(kicks().length, 2);
    kicks()[0].click();
    assert.deepEqual(owner.commands.at(-1), { commandId: 'id-0', type: 'kickParticipant', targetParticipantId: 2 });
    const oldClick = kicks()[1].onclick;
    h.state.participants[0].isHost = false; update();
    assert.equal(kicks().length, 0); oldClick(); assert.equal(owner.commands.length, 1);
    h.state.participants[0].isHost = true; h.state.phase = 'running'; update();
    assert.equal(kicks().length, 0); oldClick(); assert.equal(owner.commands.length, 1);
});

test('Lobby templates send exact owned commands and display only snapshot-confirmed choices', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const owner = h.owners[0]; const selector = h.el('empire-template');
    const catalog = ['economic', 'scientific', 'military'].map(templateId => ({ templateId, authorityId: 'auth_democratic', originId: 'origin_default', ethics: ['ethic_materialist'], civics: ['civic_meritocracy'], traits: ['trait_intelligent'] }));
    const update = () => owner.callbacks.onState(h.state, {});
    h.state.phase = 'lobby'; update();
    assert.equal(selector.disabled, true, 'Absent optional contract stays unavailable.');
    h.state.empireSetup = { catalog, selectedTemplateId: null }; update();
    assert.equal(selector.disabled, false); assert.equal(selector.value, '');
    const economy = structuredClone(h.state.planets);
    selector.value = 'economic'; selector.onchange();
    assert.deepEqual(owner.commands[0], { commandId: 'id-0', type: 'selectEmpireTemplate', templateId: 'economic' });
    assert.equal(selector.value, '', 'Sending does not select optimistically.');
    assert.equal(selector.disabled, true);
    selector.value = 'military'; selector.onchange(); assert.equal(owner.commands.length, 1);
    owner.pending.delete('id-0'); owner.callbacks.onResult({ commandId: 'id-0', accepted: false, code: 'INVALID_COMMAND', message: 'Rejected' });
    assert.equal(selector.disabled, false); assert.equal(selector.value, '');
    selector.value = 'scientific'; selector.onchange();
    owner.pending.delete('id-1'); owner.callbacks.onResult({ commandId: 'id-1', accepted: true, code: 'OK', message: 'Accepted' });
    assert.equal(selector.value, '', 'ACK is not a selected-template snapshot.');
    h.state.empireSetup.selectedTemplateId = 'scientific'; update();
    assert.equal(selector.value, 'scientific');
    assert.match(h.el('empire-template-details').title, /auth_democratic/);
    assert.match(h.el('empire-template-details').title, /origin_default/);
    assert.match(h.el('empire-template-details').title, /trait_intelligent/);
    assert.doesNotMatch(h.el('empire-template-details').textContent, /auth_democratic|origin_default|trait_intelligent/);
    owner.connected = false; owner.callbacks.onStatus('Reconnect', false);
    assert.equal(selector.disabled, true); assert.equal(selector.value, 'scientific');
    owner.connected = true; owner.callbacks.onStatus('Connected', true);
    assert.equal(selector.disabled, false); assert.equal(selector.value, 'scientific');
    selector.value = 'forged'; selector.onchange(); assert.equal(owner.commands.length, 2); assert.equal(selector.value, 'scientific');
    h.state.you = 2; h.state.participants.push({ id: 2, role: 'player' }); h.state.viewContext.participantId = 2;
    h.state.empireSetup.selectedTemplateId = 'military'; update();
    assert.equal(selector.value, 'military'); assert.equal(selector.disabled, false);
    h.state.phase = 'running'; update(); selector.value = 'economic'; selector.onchange();
    assert.equal(selector.disabled, true); assert.equal(owner.commands.length, 2);
    assert.deepEqual(h.state.planets, economy);
    h.el('clear-login').onclick();
    assert.equal(selector.value, ''); assert.equal(selector.disabled, true);
    assert.doesNotMatch(h.el('empire-template-details').textContent, /trait_intelligent|auth_democratic/);
});

test('Legacy fixture keeps trait configuration without sending the removed building selection', async () =>
{
    const html = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3.html', import.meta.url), 'utf8');
    assert.doesNotMatch(html, /id="buildings"/);
    const h = harness(); await h.importFiles(); h.ready();
    h.state.phase = 'lobby'; h.state.catalog.traits = [{ id: 'trait_adaptive', name: 'Адаптивность' }];
    h.state.catalog.buildings = [{ id: 'building_old', name: 'Старое здание' }];
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.el('traits').children.length, 1);
    h.el('traits').querySelectorAll = selector => selector === 'input:checked' ? [{ value: 'trait_adaptive' }] : [];
    h.el('configure').onsubmit({ preventDefault() {} });
    assert.deepEqual(JSON.parse(JSON.stringify(h.owners[0].commands[0])), { commandId: 'id-0', type: 'configure', traits: ['trait_adaptive'], buildings: [] });
});

test('Production Galaxy lobby uses preset traits and never sends a custom trait command', async () =>
{
    const html = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3.html', import.meta.url), 'utf8');
    assert.match(html, /id="configure" hidden/);
    const h = harness(); await h.importFiles(); h.ready();
    h.state.phase = 'lobby';
    h.state.galaxy = { stage: 'unprepared', settings: {}, scenarios: [], canPrepare: false };
    h.state.catalog.traits = [{ id: 'trait_custom', name: 'Ручной признак' }];
    h.state.empireSetup = { catalog: [
        { templateId: 'economic', authorityId: 'auth_democratic', originId: 'origin_default', ethics: [], civics: [], traits: ['trait_economic'] },
        { templateId: 'scientific', authorityId: 'auth_democratic', originId: 'origin_default', ethics: [], civics: [], traits: ['trait_scientific'] },
    ], selectedTemplateId: 'economic' };
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.el('configure').hidden, true);
    assert.equal(h.el('traits').children.length, 0);
    assert.match(h.el('empire-template-details').title, /trait_economic/);
    h.el('configure').onsubmit({ preventDefault() {} });
    assert.equal(h.owners[0].commands.length, 0);
    h.el('empire-template').value = 'scientific'; h.el('empire-template').onchange();
    assert.equal(h.owners[0].commands[0].type, 'selectEmpireTemplate');
    assert.equal(h.owners[0].commands[0].templateId, 'scientific');
    h.owners[0].callbacks.onResult({ commandId: 'old-config', accepted: false, code: 'PRESET_CONFIGURATION_ONLY', message: 'raw backend text' });
    assert.equal(h.el('error').textContent, 'Сейчас конфигурация задаётся выбранным пресетом.');
});

async function sharedLobbyHarness()
{
    const h = harness(); await h.el('join').emit('submit'); h.ready();
    h.state.phase = 'lobby'; h.state.sequence = 1; h.state.participants[0].connected = true; h.state.participants[0].assetReady = false; h.state.participants[0].ready = false;
    h.state.participants.push({ id: 2, role: 'player', connected: true, assetReady: false, ready: false });
    h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, startPending: false, worldFingerprint: null, settings: { seed: 'old', scenarioId: 'tiny', shapeId: 'elliptical', density: 1, nebulae: 1, guaranteedColonies: 0, gatewayFrequency: 1, habitableMultiplier: 1, wormholePairs: 0 }, scenarios: [{ id: 'tiny', stars: 200, shapes: ['elliptical'], minimumDensity: 0.5, maximumDensity: 2, gatewayFrequency: { minimum: 0, maximum: 5, default: 1 }, wormholePairs: { minimum: 0, maximum: 5, default: 0 } }], canPrepare: false };
    const owner = h.owners[0]; const update = () => owner.callbacks.onState(h.state, {});
    const result = (index, accepted, sequence = null) => { const commandId = owner.commands[index].commandId; owner.pending.delete(commandId); owner.callbacks.onResult({ commandId, accepted, code: accepted ? 'OK' : 'REJECTED', message: accepted ? 'Принято.' : 'Отказ.', stateSequence: sequence }); };
    update(); return { h, owner, update, result };
}

test('Подключить и Отключить отражают попытку, связь, повтор и отказ входа', async () =>
{
    const h = harness(); const owner = h.owners[0];
    assert.equal(h.el('connect').disabled, false); assert.equal(h.el('disconnect').disabled, true);
    await h.el('join').emit('submit');
    assert.equal(h.el('connect').disabled, true); assert.equal(h.el('disconnect').disabled, false);
    h.ready(); assert.equal(h.el('connect').disabled, true); assert.equal(h.el('disconnect').disabled, false);
    owner.socket = null; owner.retryAt = 1000; owner.callbacks.onStatus('Переподключение…', false);
    assert.equal(h.el('connect').disabled, true); assert.equal(h.el('disconnect').disabled, false);
    owner.stopped = true; owner.retryAt = null; owner.callbacks.onStatus('Сервер отклонил вход.', false);
    assert.equal(h.el('connect').disabled, false); assert.equal(h.el('disconnect').disabled, true);
    await h.el('join').emit('submit'); h.el('disconnect').onclick();
    assert.equal(h.el('connect').disabled, false); assert.equal(h.el('disconnect').disabled, true);
});

test('Production Galaxy lobby hides obsolete manual Apply and Prepare buttons', () =>
{
    const html = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3.html', import.meta.url), 'utf8');
    assert.match(html, /id="save-galaxy-settings" hidden/);
    assert.match(html, /id="prepare-galaxy" type="button" hidden/);
    assert.match(html, /Гарантированные миры<select|Гарантированные миры<input/);
});

test('Guaranteed world count is validated and confirmed in the shared lobby', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    assert.match(h.el('galaxy-status').textContent, /Гарантированные миры: 0/);
    h.el('galaxy-guaranteed-colonies').value = '1.5'; h.el('galaxy-guaranteed-colonies').oninput(); h.flushTimers();
    assert.equal(owner.commands.length, 0, 'Fractional setting must not be sent.');
    h.el('galaxy-guaranteed-colonies').value = '3'; h.el('galaxy-guaranteed-colonies').oninput(); h.flushTimers();
    assert.equal(owner.commands.length, 0, 'Out of range setting must not be sent.');
    h.el('galaxy-guaranteed-colonies').value = '2'; h.el('galaxy-guaranteed-colonies').oninput(); h.flushTimers();
    assert.equal(owner.commands[0].galaxyGuaranteedColonies, 2);
    result(0, true, 2); h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2;
    h.state.galaxy.settings.guaranteedColonies = 2; update();
    assert.equal(h.el('galaxy-guaranteed-colonies').value, '2');
    assert.match(h.el('galaxy-status').textContent, /Гарантированные миры: 2/);
});

test('Habitable multiplier validates 0..5/five decimals, publishes host setting and waits for matching peer-visible confirmation', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    assert.equal(h.el('galaxy-habitable-multiplier').value, '1');
    assert.equal(h.el('galaxy-habitable-multiplier').min, '0'); assert.equal(h.el('galaxy-habitable-multiplier').max, '5');
    h.el('galaxy-habitable-multiplier').value = '5.000001'; h.el('galaxy-habitable-multiplier').oninput(); h.flushTimers();
    assert.equal(owner.commands.length, 0, 'Six decimal places must not be sent.');
    h.el('galaxy-habitable-multiplier').value = '5.00001'; h.el('galaxy-habitable-multiplier').oninput(); h.flushTimers();
    assert.equal(owner.commands.length, 0, 'Value above five must not be sent.');
    h.el('galaxy-habitable-multiplier').value = '0.29'; h.el('galaxy-habitable-multiplier').oninput(); h.flushTimers();
    assert.equal(owner.commands[0].galaxyHabitableMultiplier, 0.29);
    assert.equal(h.el('ready').disabled, true, 'Ready waits for the setting confirmation.');
    result(0, true, 2); h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2; h.state.galaxy.settings.habitableMultiplier = 0.29; update();
    h.state.you = 2; h.state.viewContext.participantId = 2; update();
    assert.equal(h.el('galaxy-habitable-multiplier').value, '0.29');
    assert.equal(h.el('galaxy-habitable-multiplier').disabled, true, 'Peer cannot edit shared setting.');
    assert.equal(h.el('ready').disabled, false, 'Confirmed setting is visible to peer and clears pending state.');
});

test('Частота червоточин принимает целые 0..5, отправляет настройку и показывает подтверждение участнику', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    const input = h.el('galaxy-wormhole-pairs');
    assert.equal(input.value, '0'); assert.equal(input.min, '0'); assert.equal(input.max, '5');
    for (const invalid of ['2.5', '6'])
    {
        input.value = invalid; input.oninput(); h.flushTimers();
        assert.equal(owner.commands.length, 0, `${invalid} must not be sent.`);
    }
    input.value = '5'; input.oninput(); h.flushTimers();
    assert.equal(owner.commands[0].type, 'setGalaxySettings'); assert.equal(owner.commands[0].galaxyWormholePairs, 5);
    assert.equal(h.el('ready').disabled, true, 'Ready waits for the setting confirmation.');
    result(0, true, 2); h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2; h.state.galaxy.settings.wormholePairs = 5; update();
    assert.equal(input.value, '5');
    h.state.you = 2; h.state.viewContext.participantId = 2; update();
    assert.equal(input.value, '5'); assert.equal(input.disabled, true, 'Peer cannot edit shared setting.'); assert.equal(h.el('ready').disabled,false);
    h.state.you = 1; h.state.viewContext.participantId = 1; update();
    input.value = '0'; input.oninput(); h.flushTimers();
    assert.equal(owner.commands[1].galaxyWormholePairs, 0, 'Zero is a valid lower boundary.');
});

test('Общие настройки публикуются до готовности, а первый Start требует явных Ready обоих игроков', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    assert.equal(h.el('ready').disabled, false); assert.equal(h.el('start').disabled, true);
    h.el('galaxy-seed').value = 'new'; h.el('galaxy-seed').oninput(); h.el('galaxy-seed').onchange(); h.flushTimers();
    assert.equal(owner.commands[0].type, 'setGalaxySettings'); assert.equal(owner.commands[0].galaxySeed, 'new');
    assert.equal(h.el('ready').disabled, true);
    result(0, true, 2); h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2; h.state.galaxy.settings.seed = 'new'; update();
    assert.equal(h.el('ready').disabled, false);
    h.state.you = 2; h.state.viewContext.participantId = 2; update();
    assert.equal(h.el('galaxy-seed').value, 'new'); assert.equal(h.el('galaxy-seed').disabled, true);
    h.state.you = 1; h.state.viewContext.participantId = 1; update();
    h.el('ready').onclick(); assert.deepEqual(JSON.parse(JSON.stringify(owner.commands[1])), { commandId: 'id-1', type: 'ready', ready: true, expectedLobbyRevision: 2 });
    h.state.sequence = 3; h.state.participants[0].ready = true; update(); assert.equal(h.el('start').disabled, true);
    h.state.sequence = 4; h.state.participants[1].ready = true; h.state.galaxy.canPrepare = true; update();
    assert.equal(h.el('start').disabled, false); h.el('start').onclick(); h.el('start').onclick();
    assert.deepEqual(owner.commands.map(command => command.type), ['setGalaxySettings', 'ready', 'start']);
    assert.deepEqual(JSON.parse(JSON.stringify(owner.commands[2])), { commandId: 'id-2', type: 'start', expectedLobbyRevision: 2 });
    assert.equal(h.imports.length, 0);
});

test('Ввод seed без blur отправляет общую настройку после паузы и блокирует Ready до подтверждения', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    h.el('galaxy-seed').value = 'typed'; h.el('galaxy-seed').oninput();
    assert.equal(h.el('ready').disabled, true);
    assert.equal(owner.commands.length, 0);
    h.flushTimers();
    assert.equal(owner.commands[0].type, 'setGalaxySettings'); assert.equal(owner.commands[0].galaxySeed, 'typed');
    assert.equal(h.el('ready').disabled, true);
    result(0, true, 2); assert.equal(h.el('ready').disabled, true);
    h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2; h.state.galaxy.settings.seed = 'typed'; update();
    assert.equal(h.el('ready').disabled, false);
});

test('Передача хоста отменяет неотправленный seed и возвращает видимые серверные настройки', async () =>
{
    const { h, owner, update } = await sharedLobbyHarness();
    h.el('galaxy-seed').value = 'unsent-draft'; h.el('galaxy-seed').oninput();
    assert.equal(h.el('ready').disabled, true);
    h.state.sequence = 2; h.state.participants[0].isHost = false; h.state.participants[1].isHost = true; update(); h.flushTimers();
    assert.equal(owner.commands.length, 0);
    assert.equal(h.el('galaxy-seed').value, 'old');
    assert.equal(h.el('galaxy-seed').disabled, true);
    assert.equal(h.el('ready').disabled, false);
});

for (const count of [1, 2, 3, 10, 20]) test(`Лобби и автоматический старт после подтверждения файлов всех ${count} игроков`, async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    h.state.participants = Array.from({ length: count }, (_, index) => ({ id: index + 1, name: `Игрок ${index + 1}`, role: 'player', isHost: index === 0, connected: true, ready: true, assetReady: false }));
    h.state.participants.push({ id: count + 1, role: 'observer', connected: false, ready: false, assetReady: false });
    h.state.galaxy.canPrepare = true; update();
    assert.equal(h.el('ready').disabled, false);
    assert.equal(h.el('start').disabled, false);
    assert.doesNotMatch(h.el('setup-progress').textContent, /Ожидается второй игрок/);
    await h.importFiles();
    h.el('start').onclick(); result(0, true, 2);
    h.state.sequence = 2; h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.startPending = true; h.state.galaxy.worldFingerprint = 'world-count';
    h.state.assetProfile = { profileId: 'profile', profileDigest: 'digest' }; update(); await h.settle(); await h.settle();
    assert.doesNotMatch(h.el('galaxy-status').textContent, /обоими игроками/);
    owner.assetReady = true;
    for (const player of h.state.participants.filter(item => item.role === 'player').slice(0, -1)) player.assetReady = true;
    h.state.sequence = 3; update();
    assert.equal(owner.commands.length, 1, 'Последнее подтверждение файлов ещё не пришло.');
    h.state.participants[count - 1].assetReady = true; h.state.sequence = 4; update();
    assert.deepEqual(owner.commands.map(command => command.type), ['start', 'start'], `${h.el('import-status').textContent} | ${h.el('galaxy-status').textContent} | ${h.el('error').textContent} | candidate=${JSON.stringify(owner.candidate)}`);
    assert.equal(owner.commands[1].galaxyWorldFingerprint, 'world-count');
    result(1, true, 5); h.state.sequence = 5; h.state.phase = 'running'; h.state.galaxy.stage = 'running'; update();
    assert.equal(owner.commands.length, 2);
});
test('После первого Start текущий профиль импортируется из выбранного источника и второй Start ждёт оба ACK', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    await h.importFiles(); assert.equal(h.imports.length, 0, 'До генерации источник запоминается без декодирования старого профиля.');
    const candidateCalls = []; const setCandidate = owner.setGalaxyCandidate.bind(owner); owner.setGalaxyCandidate = (...args) => { candidateCalls.push(args); return setCandidate(...args); };
    h.state.participants[0].ready = true; h.state.participants[1].ready = true; h.state.galaxy.canPrepare = true; update();
    h.el('start').onclick(); result(0, true, 2);
    h.state.sequence = 2; h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.startPending = true; h.state.galaxy.worldFingerprint = 'world-one';
    h.state.assetProfile = { profileId: 'profile', profileDigest: 'digest' }; update(); await h.settle(); await h.settle();
    assert.equal(h.imports.length, 1);
    assert.deepEqual(owner.candidate, { profileId: 'profile', profileDigest: 'digest', galaxyWorldFingerprint: 'world-one' }, `${h.el('import-status').textContent} | ${h.el('galaxy-status').textContent} | ${h.el('error').textContent} | candidateCalls=${JSON.stringify(candidateCalls)} | version=${owner.candidateVersion}`);
    assert.deepEqual(owner.commands.map(command => command.type), ['start']);
    owner.assetReady = true; h.state.sequence = 3; h.state.participants[0].assetReady = true; update();
    assert.equal(owner.commands.length, 1, 'Второй игрок ещё не подтвердил файлы.');
    h.state.sequence = 4; h.state.participants[1].assetReady = true; update();
    assert.equal(owner.commands[1].type, 'start');
    assert.equal(owner.commands[1].expectedLobbyRevision, 1);
    assert.equal(owner.commands[1].galaxyWorldFingerprint, 'world-one');
    assert.equal(owner.commands.some(command => command.type === 'ready'), false, 'Готовность после генерации не дублируется.');
    result(1, true, 5); h.state.sequence = 5; h.state.phase = 'running'; h.state.galaxy.stage = 'running'; update();
    assert.equal(owner.commands.length, 2);
});

test('Отказ первого Start и смена версии лобби останавливают автоматическое завершение', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    h.state.participants[0].ready = true; h.state.participants[1].ready = true; h.state.galaxy.canPrepare = true; update();
    h.el('start').onclick(); result(0, false);
    assert.equal(h.el('start').disabled, false);
    h.el('start').onclick(); result(1, true, 2);
    h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2; h.state.galaxy.canPrepare = false; update();
    assert.match(h.el('error').textContent, /версия лобби изменилась/);
    assert.equal(owner.commands.some(command => command.type === 'prepareGalaxy' || command.type === 'ready'), false);
});

test('Пустой seed принимает только подтверждённый сервером seed до готовности', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    h.el('galaxy-seed').value = ' '; h.el('galaxy-seed').oninput(); h.el('galaxy-seed').onchange();
    assert.equal(owner.commands[0].galaxySeed, ' ');
    result(0, true, 2); assert.equal(h.el('ready').disabled, true);
    h.state.sequence = 2; h.state.galaxy.settings.seed = 'server-generated-seed'; h.state.galaxy.lobbyRevision = 2; update();
    assert.equal(h.el('ready').disabled, false);
    assert.equal(h.el('galaxy-seed').value, 'server-generated-seed');
});

test('Ошибка текущего импорта оставляет явный отказ и разрешает повторную проверку источника', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    await h.importFiles(); h.options.importFailure = new Error('missing source');
    h.state.participants[0].ready = true; h.state.participants[1].ready = true; h.state.galaxy.canPrepare = true; update();
    h.el('start').onclick(); result(0, true, 2);
    h.state.sequence = 2; h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.startPending = true; h.state.galaxy.worldFingerprint = 'world-one'; update(); await h.settle();
    assert.match(h.el('error').textContent, /missing source/);
    assert.equal(owner.commands.length, 1);
    h.options.importFailure = null; await h.importFiles();
    assert.equal(h.imports.length, 2);
    assert.equal(owner.candidate.galaxyWorldFingerprint, 'world-one');
});

test('Выбранная до генерации папка перечитывается по текущему manifest без второго запроса доступа', async () =>
{
    const { h, update } = await sharedLobbyHarness();
    const handle = { name: 'Stellaris' }; let pickerCalls = 0;
    h.window.showDirectoryPicker = async () => { pickerCalls++; return handle; };
    await h.el('folder').onclick(); await h.settle();
    assert.equal(h.imports.length, 0);
    h.state.sequence = 2; h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.startPending = true; h.state.galaxy.worldFingerprint = 'world-one'; update(); await h.settle(); await h.settle();
    assert.equal(pickerCalls, 1);
    assert.equal(h.imports[0].source.mode, 'directory');
    assert.equal(h.imports[0].source.handle, handle);
});

test('Новый источник до генерации не замещается завершением старого импорта', async () =>
{
    const h = harness(); h.ready();
    let finishOld; h.options.deferred = new Promise(resolve => { finishOld = resolve; });
    h.el('files').files = [{ name: 'old.dds' }]; h.el('files').onchange(); await h.settle();
    assert.equal(h.imports.length, 1);
    h.state.phase = 'lobby'; h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, startPending: false, settings: {}, scenarios: [] };
    h.owners[0].callbacks.onState(h.state, {});
    h.el('files').files = [{ name: 'new.dds' }]; h.el('files').onchange();
    h.options.deferred = null; finishOld(); await h.settle();
    h.state.sequence = 2; h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.startPending = true; h.state.galaxy.worldFingerprint = 'world-one';
    h.owners[0].callbacks.onState(h.state, {}); await h.settle(); await h.settle();
    assert.equal(h.imports.length, 2);
    assert.equal(h.imports[1].source.files[0].name, 'new.dds');
    assert.equal(h.owners[0].candidate?.galaxyWorldFingerprint, 'world-one');
});

test('Устаревший профиль лобби освобождается перед импортом нового мира в пределах прежнего бюджета', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const oldRenderer = h.renderers[0]; const oldBudget = h.imports[0].maxDecodedBytes;
    h.profile.decodedBytes = 326 * 1048576;
    h.options.manifestFetch = async () => ({ profileId: 'generated', profileDigest: 'generated-digest', files: [] });
    h.state.phase = 'lobby'; h.state.galaxy = { stage: 'awaitingAssets', lobbyRevision: 1, startPending: true, worldFingerprint: 'world-one', settings: {}, scenarios: [] };
    h.state.assetProfile = { profileId: 'generated', profileDigest: 'generated-digest' };
    h.owners[0].callbacks.onState(h.state, {}); await h.settle(); await h.settle();
    assert.equal(oldRenderer.disposed, true);
    assert.equal(h.imports[1].maxDecodedBytes, oldBudget);
    assert.deepEqual(h.owners[0].candidate, { profileId: 'generated', profileDigest: 'generated-digest', galaxyWorldFingerprint: 'world-one' });
});

test('Отказ финального Start не зацикливается и допускает явный повтор с той же версией', async () =>
{
    const { h, owner, update, result } = await sharedLobbyHarness();
    h.state.participants[0].ready = true; h.state.participants[1].ready = true; h.state.galaxy.canPrepare = true; update();
    h.el('start').onclick(); result(0, true, 2);
    h.state.sequence = 2; h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.startPending = true; h.state.galaxy.worldFingerprint = 'world-one';
    h.state.participants[0].assetReady = true; h.state.participants[1].assetReady = true;
    owner.candidate = { profileId: 'profile', profileDigest: 'digest', galaxyWorldFingerprint: 'world-one' };
    update(); assert.equal(owner.commands[1].type, 'start');
    result(1, false); owner.callbacks.onStatus('Подключено', true);
    assert.equal(owner.commands.length, 2, 'Отказ не запускает бесконечный повтор.');
    assert.equal(h.el('start').disabled, false);
    h.el('start').onclick(); assert.equal(owner.commands[2].type, 'start');
    assert.equal(owner.commands[2].expectedLobbyRevision, 1);
    assert.equal(owner.commands[2].galaxyWorldFingerprint, 'world-one');
});
test('Лобби показывает точный этап подтверждения файлов при заблокированной готовности', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    const owner = h.owners[0]; owner.sent = new Set();
    h.state.phase = 'lobby'; h.state.participants[0].assetReady = false;
    h.state.galaxy = { stage: 'awaitingAssets', worldFingerprint: 'world-one', settings: {}, scenarios: [] };
    h.state.assetProfile = { profileId: 'other', profileDigest: 'other' };
    const update = () => owner.callbacks.onState(h.state, {});
    update(); assert.match(h.el('galaxy-status').textContent, /не совпадает с текущим профилем/);
    h.state.assetProfile = { profileId: 'profile', profileDigest: 'digest' };
    update(); assert.match(h.el('galaxy-status').textContent, /кандидат ещё не сопоставлен/);
    owner.candidate = { profileId: 'profile', profileDigest: 'digest', galaxyWorldFingerprint: 'world-one' };
    update(); assert.match(h.el('galaxy-status').textContent, /ещё не поставлено в очередь/);
    owner.assetCommandId = 'ack-one'; update(); assert.match(h.el('galaxy-status').textContent, /ожидает отправки/);
    owner.sent.add('ack-one'); update(); assert.match(h.el('galaxy-status').textContent, /отправлено; ожидается ответ/);
    owner.assetReady = true; update(); assert.match(h.el('galaxy-status').textContent, /ожидается серверный снимок/);
    h.state.participants[0].assetReady = true; update(); assert.match(h.el('galaxy-status').textContent, /Локальные файлы подтверждены/);
});

test('File fallback lists the current generated manifest rather than static baseline paths', async () =>
{
    const h = harness({ manifestFiles: [{ relativePath: 'gfx/models/planets/pc_desert.dds' }, { relativePath: 'gfx/models/planets/pc_ocean.dds' }] });
    const count = h.document.createElement('span'); count.dataset.fileCount = ''; h.document.append(count);
    assert.match(h.el('required-files').textContent, /после подготовки/);
    assert.doesNotMatch(h.el('required-files').textContent, /^file$/m);
    await h.importFiles(); h.ready();
    h.state.phase = 'lobby'; h.state.galaxy = { stage: 'unprepared', settings: {}, scenarios: [], canPrepare: false };
    h.owners[0].callbacks.onState(h.state, {});
    assert.match(h.el('required-files').textContent, /после подготовки/);
    h.state.galaxy.stage = 'awaitingAssets'; h.state.galaxy.worldFingerprint = 'generated-one';
    h.owners[0].candidateVersion++;
    h.owners[0].callbacks.onState(h.state, {}); await h.settle(); await h.settle();
    assert.match(h.el('required-files').textContent, /gfx\/models\/planets\/pc_desert\.dds/);
    assert.match(h.el('required-files').textContent, /gfx\/models\/planets\/pc_ocean\.dds/);
    assert.match(h.el('required-files').textContent, /localisation\/russian/);
    assert.doesNotMatch(h.el('required-files').textContent, /^file$/m);
    assert.match(count.textContent, /^6 файлов текущего профиля/);
});

test('Завершение импорта повторно сверяет локальный профиль при смене версии кандидата', async () =>
{
    const h = harness(); const owner = h.owners[0];
    h.state.phase = 'lobby'; h.state.participants[0].assetReady = false;
    h.state.galaxy = { stage: 'awaitingAssets', worldFingerprint: 'world-one', settings: {}, scenarios: [] };
    owner.connected = true; owner.callbacks.onStatus('Подключено', true); owner.callbacks.onState(h.state, {}); await h.settle();
    let release; h.options.deferred = new Promise(resolve => { release = resolve; });
    h.el('files').files = [{}]; h.el('files').onchange(); await h.settle();
    assert.equal(h.imports.length, 1);
    owner.candidateVersion++; owner.callbacks.onState(h.state, {}); await h.settle();
    release(); await h.settle(); await h.settle();
    assert.deepEqual(owner.candidate, { profileId: 'profile', profileDigest: 'digest', galaxyWorldFingerprint: 'world-one' });
});

test('Client hydrates compact development before renderer and panel consumers see the snapshot', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    h.state.development = { sourceRecipeTerms: [['production', 'minerals', 2, null, 'Included', 'Planet']], catalogSlotSets: [['slot-a']], colonies: [{ catalog: [{ definitionId: 'building_lab', sourceRecipe: null, compatibleSlotIds: null, sourceRecipeIndices: [0], compatibleSlotSetIndex: 0 }] }] };
    h.owners[0].callbacks.onState(h.state, {});
    const entry = h.renderers[0].current.development.colonies[0].catalog[0];
    assert.deepEqual(entry.compatibleSlotIds, ['slot-a']);
    assert.equal(entry.sourceRecipe[0].targetId, 'minerals');
    h.state.development.colonies[0].catalog[0].sourceRecipeIndices = [99];
    assert.throws(() => h.owners[0].callbacks.onState(h.state, {}), /Некорректные таблицы развития/);
    assert.equal(h.renderers[0].current.development.colonies[0].catalog[0], entry);
});

test('Remote candidate change clears old File fallback paths before a failed or delayed manifest fetch', async () =>
{
    const h = harness({ manifestFiles: [{ relativePath: 'gfx/old-world.dds' }] });
    const count = h.document.createElement('span'); count.dataset.fileCount = ''; h.document.append(count);
    await h.importFiles(); h.ready();
    h.state.phase = 'lobby'; h.state.galaxy = { stage: 'awaitingAssets', worldFingerprint: 'world-one', settings: {}, scenarios: [] };
    h.owners[0].candidateVersion++; h.owners[0].callbacks.onState(h.state, {}); await h.settle();
    assert.match(h.el('required-files').textContent, /old-world/);
    let rejectDelayed;
    h.options.manifestFetch = () => new Promise((resolve, reject) => { rejectDelayed = reject; });
    h.state.galaxy.worldFingerprint = 'world-two'; h.owners[0].candidateVersion++;
    h.owners[0].callbacks.onState(h.state, {});
    assert.match(h.el('required-files').textContent, /после подготовки/);
    assert.doesNotMatch(count.textContent, /файлов текущего профиля/);
    h.options.manifestFetch = async () => ({ profileId: 'next', profileDigest: 'next-digest', files: [{ relativePath: 'gfx/new-world.dds' }] });
    h.state.galaxy.worldFingerprint = 'world-three'; h.owners[0].candidateVersion++;
    h.owners[0].callbacks.onState(h.state, {}); await h.settle(); await h.settle();
    assert.match(h.el('required-files').textContent, /new-world/);
    rejectDelayed(new Error('Старый GET завершился поздно')); await h.settle();
    assert.match(h.el('required-files').textContent, /new-world/);
    assert.doesNotMatch(h.el('galaxy-status').textContent, /Старый GET/);
    h.options.manifestFetch = async () => { throw new Error('Текущий manifest недоступен'); };
    h.state.galaxy.worldFingerprint = 'world-four'; h.owners[0].candidateVersion++;
    h.owners[0].callbacks.onState(h.state, {}); await h.settle();
    assert.match(h.el('required-files').textContent, /после подготовки/);
    assert.doesNotMatch(h.el('required-files').textContent, /new-world/);
    assert.doesNotMatch(count.textContent, /файлов текущего профиля/);
    assert.match(h.el('galaxy-status').textContent, /Текущий manifest недоступен/);
});

test('Galaxy inspection binds both open and clear detail to the running fingerprint and permitted view', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    h.state.galaxy = { stage: 'running', worldFingerprint: 'world-one', settings: {}, scenarios: [] };
    h.owners[0].candidate = { profileId: 'profile', profileDigest: 'digest', galaxyWorldFingerprint: 'world-one' };
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.renderers[0].callbacks.onOpenSystem('S01'), true);
    assert.deepEqual(JSON.parse(JSON.stringify(h.owners[0].commands[0])), { commandId: 'id-0', type: 'inspectGalaxySystem', systemId: 'S01', galaxyWorldFingerprint: 'world-one' });
    h.el('galaxy').onclick();
    assert.deepEqual(JSON.parse(JSON.stringify(h.owners[0].commands[1])), { commandId: 'id-1', type: 'inspectGalaxySystem', systemId: '', galaxyWorldFingerprint: 'world-one' });
    h.state.viewContext = { mode: 'general', participantId: null }; h.state.spatial.systems = [];
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(h.renderers[0].callbacks.onOpenSystem('S01'), false);
    assert.equal(h.owners[0].commands.length, 2);
});

test('Observer and unconfirmed lobby contexts cannot submit or retain another player template', async () =>
{
    const h = harness(); await h.importFiles(); h.ready();
    h.state.phase = 'lobby'; h.state.participants.push({ id: 2, role: 'player' }); h.state.participants[0].role = 'observer';
    h.state.viewContext = { mode: 'player', participantId: 2 };
    h.state.empireSetup = { catalog: [{ templateId: 'economic', authorityId: 'PRIVATE_AUTHORITY', originId: 'origin', ethics: [], civics: [], traits: [] }], selectedTemplateId: 'economic' };
    const selector = h.el('empire-template');
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(selector.value, 'economic'); assert.equal(selector.disabled, true);
    selector.onchange(); assert.equal(h.owners[0].commands.length, 0);
    for (const context of [{ mode: 'general', participantId: null }, { mode: 'pending', participantId: 2 }, { mode: 'player', participantId: 999 }])
    {
        h.state.viewContext = context; h.owners[0].callbacks.onState(h.state, {});
        assert.equal(selector.value, ''); assert.equal(selector.disabled, true);
        assert.doesNotMatch(h.el('empire-template-details').textContent, /PRIVATE_AUTHORITY/);
    }
    h.state.participants[0].role = 'player'; h.state.viewContext = { mode: 'player', participantId: 2 };
    h.owners[0].callbacks.onState(h.state, {});
    assert.equal(selector.value, ''); assert.equal(selector.disabled, true);
    selector.value = 'economic'; selector.onchange(); assert.equal(h.owners[0].commands.length, 0);
});

test('Planet image replacement budgets both handles, preserves old images on failure and refreshes an unchanged open window', async () =>
{
    const h = harness(); const key = 'building.sample'; h.profile.planetImages = new Map([[key, {}]]);
    h.state.spatial.bodies.push({ id: 'B1', kind: 'planet', name: 'Colony', systemId: 'S01', ownerParticipantId: 1 });
    h.state.spatial.colonies.push({ id: 'C1', bodyId: 'B1', ownerParticipantId: 1, economyPlanetId: 1 });
    h.state.development = { gates: [], colonies: [{ colonyId: 'C1', ownerParticipantId: 1, economyPlanetId: 1, slots: [], installed: [], queue: [], features: [], jobs: [], gates: [], catalog: [{ definitionId: 'sample', kind: 'building', imageDefinitionKey: key, eligibility: 'unresolved', slotIds: [], quote: null, gates: [] }] }] };
    await h.importFiles(); h.ready(); h.renderers[0].callbacks.onSelect({ kind: 'colony', id: 'C1' }); h.el('open-planet').onclick();
    const images = node => node.children.flatMap(child => [...(child.tagName === 'img' ? [child] : []), ...images(child)]);
    assert.equal(images(h.el('planet-content'))[0].src, 'blob:planet-0');
    assert.equal(h.imageRequests[0].maxDecodedBytes, 16 * 1048576);
    assert.equal(h.renderers[0].profiles[0].limits.maxConvertedBytes, 512 * 1048576 - 100 - 16 - 16 * 1048576);
    h.options.gpuError = true; await h.importFiles();
    assert.equal(h.imports[1].maxDecodedBytes, 512 * 1048576 - 100 - 40 - 16 - 16 * 1048576);
    assert.equal(h.imageStages[0].disposed, false); assert.equal(h.imageStages[1].disposed, true); assert.equal(h.uiStages[1].disposed, true);
    assert.equal(images(h.el('planet-content'))[0].src, 'blob:planet-0');
    assert.equal(h.owners[0].profiles.length, 1);
    h.options.gpuError = false; await h.importFiles();
    assert.equal(h.imageStages[0].disposed, true); assert.equal(h.imageStages[2].disposed, false);
    assert.equal(images(h.el('planet-content'))[0].src, 'blob:planet-2');
    assert.equal(h.renderers[0].profiles[1].limits.maxConvertedBytes, 512 * 1048576 - 100 - 100 - 16 - 16 - 16 * 1048576);
    h.el('clear-login').onclick(); assert.equal(h.imageStages[2].disposed, true); assert.equal(h.el('planet-content').children.length, 0);
});

for (const cancel of ['cancel-import', 'clear-login', 'pagehide'])
{
    test(`Late planet image staging after ${cancel} releases candidates without publishing readiness`, async () =>
    {
        const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles();
        let release; h.options.imageDeferred = new Promise(resolve => { release = resolve; });
        h.el('files').files = [{}]; h.el('files').onchange(); await h.settle();
        assert.equal(h.imageRequests.length, 2);
        if (cancel === 'pagehide') { await h.window.emit('pagehide'); } else { h.el(cancel).onclick(); }
        assert.equal(h.imageRequests[1].signal.aborted, true);
        release(); await h.settle();
        assert.equal(h.imageStages[1].disposed, true); assert.equal(h.uiStages[1].disposed, true);
        assert.equal(h.owners[0].profiles.length, 1);
        assert.equal(h.imageStages[0].disposed, cancel !== 'cancel-import');
    });
}

test('Restored planet images delay readiness, survive file cancellation and dispose on the next pagehide', async () =>
{
    const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles(); h.ready();
    await h.window.emit('pagehide'); assert.equal(h.imageStages[0].disposed, true);
    let release; h.options.imageDeferred = new Promise(resolve => { release = resolve; });
    const restored = h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.imageRequests.length, 2); assert.equal(h.owners[0].profiles.length, 1);
    h.el('cancel-import').onclick(); assert.equal(h.imageRequests[1].signal.aborted, false);
    release(); await restored;
    assert.equal(h.owners[0].profiles.length, 2); assert.equal(h.imageStages[1].disposed, false);
    await h.window.emit('pagehide'); assert.equal(h.imageStages[1].disposed, true);
});

for (const failureAt of ['image', 'gpu'])
{
    test(`Failed ${failureAt} restoration disposes candidate handles and blocks readiness`, async () =>
    {
        const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles();
        await h.window.emit('pagehide');
        h.options.imageFailure = failureAt === 'image'; h.options.gpuError = failureAt === 'gpu';
        await h.window.emit('pageshow', { persisted: true });
        assert.equal(h.owners[0].profiles.length, 1); assert.equal(h.uiStages[1].disposed, true);
        if (failureAt === 'gpu') { assert.equal(h.imageStages[1].disposed, true); }
        assert.equal(h.el('connect').disabled, true); assert.match(h.el('error').textContent, /Planet PNG failure|GPU failure/);
    });
}

test('Clearing login during delayed image restoration aborts and disposes both staged handles', async () =>
{
    const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles();
    await h.window.emit('pagehide');
    let release; h.options.imageDeferred = new Promise(resolve => { release = resolve; });
    const restored = h.window.emit('pageshow', { persisted: true }); await h.settle();
    h.el('clear-login').onclick(); assert.equal(h.imageRequests[1].signal.aborted, true);
    release(); await restored;
    assert.equal(h.imageStages[1].disposed, true); assert.equal(h.uiStages[1].disposed, true);
    assert.equal(h.renderers.length, 1); assert.equal(h.owners[0].profiles.length, 1);
});

test('Restoration rejects new file/folder imports while staging and reimport recovers a failed restoration', async () =>
{
    const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles();
    await h.window.emit('pagehide');
    let release; h.options.imageDeferred = new Promise(resolve => { release = resolve; });
    const restored = h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.el('files').disabled, true); assert.equal(h.el('folder').disabled, true);
    const imports = h.imports.length;
    h.el('files').files = [{}]; h.el('files').onchange();
    let picked = 0; h.window.showDirectoryPicker = async () => { picked++; return {}; };
    await h.el('folder').onclick(); await h.settle();
    assert.equal(h.imports.length, imports); assert.equal(picked, 0);
    assert.match(h.el('error').textContent, /Дождитесь восстановления/);
    h.options.imageFailure = true; release(); await restored;
    assert.equal(h.el('files').disabled, false); assert.equal(h.el('connect').disabled, true);
    h.options.imageFailure = false; h.options.imageDeferred = null;
    await h.importFiles(); assert.equal(h.owners[0].profiles.length, 2); assert.equal(h.el('connect').disabled, false);
});

test('New imports and restoration wait for cancelled image buffers to be released before staging', async () =>
{
    const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles();
    let release; h.options.imageDeferred = new Promise(resolve => { release = resolve; });
    h.el('files').files = [{}]; h.el('files').onchange(); await h.settle();
    assert.equal(h.imageRequests.length, 2);
    h.el('files').files = [{}]; h.el('files').onchange(); await h.settle();
    assert.equal(h.imageRequests.length, 2, 'Replacement waits for previous encoding cleanup.');
    assert.equal(h.imageRequests[1].signal.aborted, true);
    await h.window.emit('pagehide');
    const restored = h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.imageRequests.length, 2, 'Restoration also waits for the same cleanup.');
    h.options.imageDeferred = null; release(); await restored; await h.settle();
    assert.equal(h.imageStages[1].disposed, true); assert.equal(h.uiStages[1].disposed, true);
    assert.equal(h.imageRequests.length, 3); assert.equal(h.imageStages[2].disposed, false);
    assert.equal(h.owners[0].profiles.length, 2);
});

test('Rapid BFCache cycles wait for earlier restoration buffers before starting another restoration', async () =>
{
    const h = harness(); h.profile.planetImages = new Map([['building.sample', {}]]); await h.importFiles();
    await h.window.emit('pagehide');
    let release; h.options.imageDeferred = new Promise(resolve => { release = resolve; });
    const first = h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.imageRequests.length, 2);
    await h.window.emit('pagehide');
    const second = h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.imageRequests.length, 2); assert.equal(h.imageRequests[1].signal.aborted, true);
    h.options.imageDeferred = null; release(); await first; await second;
    assert.equal(h.imageStages[1].disposed, true); assert.equal(h.uiStages[1].disposed, true);
    assert.equal(h.imageStages[2].disposed, false); assert.equal(h.owners[0].profiles.length, 2);
});
