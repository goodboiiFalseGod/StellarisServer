import { M3Connection } from './m3-connection.mjs';
import { parseLatency, createLatencySocketFactory } from './m3-latency.mjs';
import { M3Importer } from './m3-modules/m3-importer.mjs';
import { validateM3Manifest } from './m3-modules/m3-manifest.mjs';
import { M3WorldRenderer } from './m3-modules/m3-world-renderer.mjs';
import { resolveSpatialNames } from './m3-modules/m3-world-view.mjs';
import { selectedObject, commandFleet, selectionCard, fleetOrderRows, outlinerGroups, resourceItems, identityPresentation, cameraOptions, labelInsets, viewParticipantId, perspectiveKey, observerViews, simulationRateText, decimalPrecisionWithin } from './m3-ui-presenter.mjs';
import { prepareUiSprites } from './m3-ui-sprites.mjs';
import { outlinerNavigation } from './m3-outliner-navigation.mjs';
import { planetPanelModel, resolvePlanetInspection, appendPlanetWorkforceControls } from './m3-planet-panel.mjs';
import { renderPlanetWindowView as renderPlanetPanel } from './m3-planet-window-view.mjs';
import { preparePlanetImages } from './m3-modules/m3-planet-images.mjs';
import { appendJobControls } from './m3-job-controls.mjs';
import { constructionIdentifier, constructionOperations } from './m3-construction-controls.mjs';
import { researchEmpire, researchPanelState, renderResearchPanel } from './m3-research-panel.mjs';
import { createResearchImageStore } from './m3-modules/m3-research-images.mjs';
import { loadLocalLocalization, LOCALIZATION_FILES_452 } from './m3-localization.mjs';
import { hydrateDevelopmentTables } from './m3-development-tables.mjs';
import { createPlanetWindow } from './m3-planet-window.mjs';
import { createPlanetNotifications } from './m3-planet-notifications.mjs';
import { renderPlanetNotifications } from './m3-planet-notification-view.mjs';
import { renderConstructionRefund, renderConstructionRefundLosses } from './m3-construction-refund-view.mjs';

const $ = id => document.getElementById(id);
const planetWindow = createPlanetWindow($('planet-dialog'), { header: $('planet-dialog').querySelector('.pm-title') });
const number = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 3 });
const MAX_MEMORY = 512 * 1024 * 1024;
const RESEARCH_IMAGE_RESERVE = 16 * 1024 * 1024;
const names = { energy: 'Энергия', minerals: 'Минералы', food: 'Пища', consumer_goods: 'Товары массового спроса', biomass: 'Биомасса', alloys: 'Сплавы', trade: 'Торговля', influence: 'Влияние', unity: 'Единство', physics_research: 'Физика', society_research: 'Общество', engineering_research: 'Инженерия', volatile_motes: 'Летучие пылинки', exotic_gases: 'Экзотические газы', rare_crystals: 'Редкие кристаллы', sr_living_metal: 'Живой металл', sr_zro: 'Зро', sr_dark_matter: 'Тёмная материя', nanites: 'Наниты', minor_artifacts: 'Малые артефакты', astral_threads: 'Астральные нити', entropy_crystals: 'Кристаллы энтропии' };
const importer = new M3Importer();
let renderer = null; let state = null; let profile = null; let manifest = null; let selected = null;
let localization = null;
const localizationIssues = new Map();
const researchLocalizationIssues = new Map();
const localized = (key, fallback, options, ledger = localizationIssues) =>
{
    if (!localization) { return fallback ?? key; }
    const result = localization.resolve(key, options);
    for (const issue of result.diagnostics)
    {
        if (ledger.size < 100) { ledger.set(`${key}:${issue.code}`, { key, ...issue }); }
    }
    return result.diagnostics.some(issue => issue.code === 'missing-key') ? fallback ?? key : result.text;
};
const localizationPaths = ['english', 'russian'].flatMap(language => LOCALIZATION_FILES_452.map(template => `localisation/${language}/${template.replace('{language}', language)}`));
const localizationNames = new Set(localizationPaths.map(path => path.split('/').at(-1)));
let commandFleetId = null; let currentSystem = null; let stressView = false; let fresh = false; let importing = false;
let active = true; let frameId = null; let importGeneration = 0; let iconNodes = new Map();
let contextAwaitingSnapshot = false; let contextLost = false;
let uiSprites = null;
let planetImages = null; let imageImportAbort = null; let imageRestoreAbort = null;
let researchImages = null; let acceptedSource = null; let selectedSource = null; let selectedSourceVersion = 0; let generatedImportAttemptKey = ''; let researchRenderGeneration = 0;
let importSettled = Promise.resolve();
let restoreSettled = Promise.resolve();
let restorationId = 0; let restoringProfile = false;
let outlinerCollapsed = false;
let planetSelection = null; let planetTab = 'infrastructure'; let planetViewKey = ''; const expandedPlanetJobs = new Set();
let planetInteractionId = 0;
let planetSnapshotId = 0;
let planetRefund = null; let planetRefundKey = ''; let refundLossNotice = null; let refundLossNoticeKey = '';
let planetDemolition = null;
let planetZoneReplacement = null;
const refundAttempts = new Map();
let planetInspection = null; let planetInspectionKey = ''; let planetInspectionSource = null;
const planetNotices = createPlanetNotifications(); let planetNoticeRenderKey = ''; let planetNoticeLocalization = null;
let researchView = researchPanelState(); let researchParticipant = null; const pendingResearch = new Map();
let researchRenderCache = null;
let economyKey = ''; let catalogKey = ''; let metricAt = 0; let frames = [];
let frameMetrics = '';
let saved = readSaved();
let galaxyViewKey = ''; let galaxyManifestAttemptKey = ''; let galaxyManifestAttemptId = 0;
let hostStart = null;
let hostStartRetryRequired = false; let hostStartRetryRevision = null;
let galaxySettingsPending = null; let galaxySettingsDirty = false;
let galaxySettingsTimer = null;
let requiredManifestKey = '';

function readSaved()
{
    try { return JSON.parse(sessionStorage.getItem('stellaris.m3.tab') ?? '{}'); }
    catch { return {}; }
}
function persist()
{
    if (saved.token && connection?.login?.token === saved.token) { saved.pending = [...connection.pending.values()].filter(command => command.type !== 'assetReady' && (!state?.galaxy || !['setGalaxySettings', 'selectEmpireTemplate', 'prepareGalaxy', 'configure', 'ready', 'start', 'inspectGalaxySystem'].includes(command.type))); }
    try { sessionStorage.setItem('stellaris.m3.tab', JSON.stringify(saved)); }
    catch { error('Браузер запретил сохранение входа вкладки; после reload возврат может быть недоступен.'); }
}
function element(tag, value)
{
    const node = document.createElement(tag); node.textContent = value; return node;
}
function error(message) { $('error').textContent = message; $('error').hidden = !message; }
function appendLocalizationStatus(root, ledger = localizationIssues)
{
    if (!localization) { return; }
    const details = document.createElement('details'); details.className = 'planet-diagnostics localization-diagnostics';
    const issues = [...localization.diagnostics, ...ledger.values()];
    details.append(element('summary', `Локализация: русский · ${localization.stats.files} файлов · ${issues.length} диагностик`));
    for (const issue of issues.slice(0, 20)) { details.append(element('p', `${issue.key ?? issue.path ?? ''} · ${issue.code}: ${issue.message ?? ''}`)); }
    if (issues.length > 20) { details.append(element('p', 'Показаны первые 20 диагностик.')); }
    root.append(details);
}
function hint(message) { $('hint').textContent = message; }
function localizePlanetChrome()
{
    const keys = { infrastructure: ['PLANET_TAB', 'Инфраструктура'], features: ['PLANET_MANAGEMENT', 'Управление'], jobs: ['ECONOMY', 'Экономика'], holdings: ['HOLDINGS', 'Владения'], armies: ['ARMIES', 'Армии'] };
    for (const button of document.querySelectorAll('[data-planet-tab]'))
    { const entry = keys[button.dataset.planetTab]; if (entry) { button.textContent = localized(...entry); } }
    const focus = $('show-planet-on-map');
    focus.textContent = localization ? '⌖' : 'Показать на карте';
    const focusLabel = localized('ZOOM_TO_PLANET', 'Показать колонию на карте');
    focus.setAttribute('aria-label', focusLabel); focus.title = focusLabel;
    for (const [id, key, fallback] of [['planet-previous', 'PREV', 'Предыдущая колония'], ['planet-next', 'NEXT', 'Следующая колония']])
    { const button = $(id); const label = localized(key, fallback); button.setAttribute('aria-label', label); button.title = label; }
    for (const button of $('planet-dialog').querySelectorAll('[data-close]'))
    { const label = `${localized('CLOSE_TITLE', 'Закрыть')} колонию`; button.setAttribute('aria-label', label); button.title = label; }
}
function readablePlanetColonies()
{
    const result = []; const counts = new Map(); const colonies = state?.spatial?.colonies ?? [];
    for (const colony of colonies) { if (typeof colony.id === 'string' && colony.id) { counts.set(colony.id, (counts.get(colony.id) ?? 0) + 1); } }
    for (const colony of colonies)
    {
        if (typeof colony.id !== 'string' || !colony.id || counts.get(colony.id) !== 1) { continue; }
        const model = planetPanelModel(state, { kind: 'colony', id: colony.id });
        if (model?.id === colony.id) { result.push(model); }
    }
    return result;
}
function bindPlanetCycleButton(button, direction, binding)
{
    button.onclick = () =>
    {
        const colonies = readablePlanetColonies();
        const current = planetPanelModel(state, planetSelection);
        if (!$('planet-dialog').open || binding.interactionId !== planetInteractionId || binding.snapshotId !== planetSnapshotId || binding.context !== perspectiveKey(state) || planetSelection?.context !== binding.context || planetSelection?.id !== binding.colonyId || current?.id !== binding.colonyId || !active || !fresh || !connection.ready() || restoringProfile || contextAwaitingSnapshot || contextLost || !profile || me()?.assetReady !== true || colonies.length < 2)
        {
            error('Навигация колонии недоступна. Дождитесь актуального обзора, подключения и импорта.'); return;
        }
        const index = colonies.findIndex(colony => colony.id === binding.colonyId);
        if (index < 0) { error('Колония исчезла или больше не доступна в текущем обзоре.'); return; }
        const next = colonies[(index + direction + colonies.length) % colonies.length];
        ++planetInteractionId; planetSelection = { kind: 'colony', id: next.id, context: perspectiveKey(state) };
        planetInspection = null; planetInspectionKey = ''; planetInspectionSource = null; expandedPlanetJobs.clear(); planetViewKey = '';
        error(''); renderPlanetWindow();
    };
}
function ownFleet() { return commandFleet(state, commandFleetId); }
function planetNoticeContext() { return JSON.stringify([state?.sessionId ?? null, state ? perspectiveKey(state) : null, state?.phase ?? null]); }
function canReadPlanetNotices()
{
    return active && fresh && connection.ready() && !restoringProfile && !contextAwaitingSnapshot && !contextLost && !!profile && me()?.assetReady === true && state?.phase === 'running';
}
function renderPlanetNotices()
{
    const context = planetNoticeContext(), groups = planetNotices.sync(context, readablePlanetColonies());
    const available = canReadPlanetNotices(), key = JSON.stringify([planetNotices.version, available]);
    if (key === planetNoticeRenderKey && localization === planetNoticeLocalization) return;
    planetNoticeRenderKey = key; planetNoticeLocalization = localization;
    const epoch = planetNotices.generation;
    renderPlanetNotifications($('planet-notifications'), available ? groups : [], { document, resolveLocalization: localized,
        onDismiss: colonyId =>
        {
            if (context !== planetNoticeContext() || !canReadPlanetNotices()) return;
            if (planetNotices.dismiss(epoch, colonyId)) renderPlanetNotices();
        },
        onOpen: (colonyId, orderId) =>
        {
            const notice = planetNotices.get(epoch, colonyId, orderId);
            const current = planetPanelModel(state, { kind: 'colony', id: colonyId });
            if (!notice || !current || context !== planetNoticeContext() || !canReadPlanetNotices())
            { error('Колония из уведомления недоступна в актуальном обзоре.'); return; }
            const slot = current.development?.slots.find(item => item.slotId === notice.slotId);
            const installed = current.development?.installed.find(item => item.slotId === notice.slotId);
            const target = slot && slot.kind !== 'district' ? resolvePlanetInspection(current, { kind: installed ? 'installed' : 'slot', slotId: slot.slotId }) : null;
            if (!$('research-panel').hidden) closeResearch();
            ++planetInteractionId; planetSelection = { kind: 'colony', id: current.id, context: perspectiveKey(state) }; planetTab = 'infrastructure';
            planetInspection = target; planetInspectionSource = planetInspectionSourceKey(current, target);
            planetInspectionKey = JSON.stringify([perspectiveKey(state), current.id, current.ownerId]); planetViewKey = ''; expandedPlanetJobs.clear();
            error(''); renderPlanetWindow(); openPanel('planet-dialog');
            const descendants = node => Array.from(node.children ?? []).flatMap(child => [child, ...descendants(child)]);
            const candidates = descendants($('planet-content')).filter(node =>
            {
                if (slot?.kind === 'district') return node.dataset?.districtDefinition === notice.definitionId;
                if (!target || !node.dataset?.inspectionIdentity) return false;
                try { return JSON.parse(node.dataset.inspectionIdentity).slotId === target.slotId; } catch { return false; }
            });
            if (candidates.length === 1) { if (slot?.kind === 'district') candidates[0].tabIndex = -1; candidates[0].focus(); }
        }
    });
}
function me() { return state?.participants.find(participant => participant.id === state.you); }
function canPlay() { return active && fresh && !restoringProfile && !contextAwaitingSnapshot && connection.ready() && !!profile && !stressView && state?.phase === 'running' && me()?.role === 'player'; }
function send(type, fields = {})
{
    try
    {
        if (contextAwaitingSnapshot) { throw new Error('Дождитесь свежего снимка после восстановления графики.'); }
        const id = connection.command(type, fields);
        if (state?.galaxy && ['setGalaxySettings', 'selectEmpireTemplate', 'prepareGalaxy'].includes(type)) { showRequiredFiles(null); galaxyManifestAttemptKey = ''; }
        persist(); commandRow(id, `Ожидает: ${type}`); error(''); return id;
    }
    catch (failure) { error(failure.message); return null; }
}
function commandRow(id, message, status = 'pending')
{
    let node = [...$('commands').children].find(child => child.dataset.id === id);
    if (!node) { node = element('li', ''); node.dataset.id = id; $('commands').prepend(node); }
    node.textContent = `${id.slice(0, 8)} · ${message}`;
    node.dataset.status = status;
    $('command-feedback').textContent = message; $('command-feedback').title = node.textContent;
    $('command-feedback').dataset.status = status;
    $('pending-count').textContent = `Ожидают ${connection.pending.size}`;
    while ($('commands').children.length > 100) { $('commands').lastChild.remove(); }
    renderSelection(); renderControls();
}
function select(value)
{
    selected = value;
    if(value?.kind==='gateway' || value?.kind==='wormhole')
    {
        const object=(value.kind==='gateway'?state?.spatial?.gateways:state?.spatial?.wormholes)?.find(item=>item.id===value.id);
        if(object&&currentSystem!==object.systemId) { try { renderer?.showSystem(object.systemId); currentSystem=object.systemId; stressView=false; } catch(failure) { error(failure.message); } }
    }
    if (planetSelection && value && ['colony', 'body'].includes(value.kind))
    {
        const model = planetPanelModel(state, value);
        if (model?.id === planetSelection.id) { /* Та же читаемая колония сохраняет окно и его привязки. */ }
        else if (model) { ++planetInteractionId; planetSelection = { kind: 'colony', id: model.id, context: perspectiveKey(state) }; planetInspection = null; planetInspectionSource = null; planetViewKey = ''; expandedPlanetJobs.clear(); renderPlanetWindow(); }
        else { closePlanetWindow(); }
    }
    else if (planetSelection && value) { closePlanetWindow(); }
    const fleet = state?.spatial?.fleets.find(item => value?.kind === 'fleet' && item.id === value.id);
    if (fleet?.ownerParticipantId === state.you && me()?.role === 'player') { commandFleetId = fleet.id; renderer?.setCommandFleet(commandFleetId); }
    renderSelection(); renderControls();
}
function openSystem(id)
{
    if (state?.galaxy?.stage === 'running' && !sendGalaxyInspection(id)) { return false; }
    try { renderer.showSystem(id); currentSystem = id; stressView = false; renderIdentity(); renderControls(); return true; }
    catch (failure) { error(failure.message); return false; }
}
function sendGalaxyInspection(systemId)
{
    const fingerprint = state?.galaxy?.worldFingerprint;
    if (!fingerprint || !connection.ready() || !['player', 'observer'].includes(me()?.role) || !state?.spatial?.systems?.some(system => system.id === systemId) && systemId !== '') { error('Детали системы недоступны в текущем обзоре.'); return false; }
    return !!send('inspectGalaxySystem', { systemId, galaxyWorldFingerprint: fingerprint });
}
function pointOrder({ systemId, x, z })
{
    if (!canPlay() || !ownFleet() || ownFleet().location.kind !== 'system') { hint('Выберите собственный флот вне перехода.'); return; }
    send('moveFleetToPoint', { fleetId: commandFleetId, systemId, x, z });
}
function systemOrder(id)
{
    if (!canPlay() || !ownFleet()) { hint('Выберите собственный флот для приказа.'); return; }
    send('moveFleetToSystem', { fleetId: commandFleetId, targetSystemId: id });
}
function createRenderer()
{
    renderer = new M3WorldRenderer($('canvas'), { resolveName: key => localized(key, key), onSelect: select, onOpenSystem: openSystem, onSystemZoomOut: () => { if (state?.galaxy?.stage === 'running') { sendGalaxyInspection(''); } currentSystem = null; stressView = false; renderIdentity(); renderControls(); renderSelection(); }, onPointOrder: pointOrder, onSystemOrder: systemOrder, onHint: hint, onContextState: status =>
    {
        contextAwaitingSnapshot = true;
        contextLost = status === 'lost';
        if (contextLost) { fresh = false; error('WebGL context потерян. Приказы заблокированы; CPU-набор сохранён.'); }
        else { connection.requestSnapshotRefresh(); error('Контекст восстановлен из памяти. Ожидается свежий снимок сервера.'); }
        renderControls(); if (state) { renderResearch(); }
    } });
    renderer.resize();
}

let latencyRtt = 0; let latencyError = '';
try { latencyRtt = parseLatency(location.search ?? ''); }
catch (failure) { latencyError = failure.message; }
$('latency-mode').value = latencyError ? '' : String(latencyRtt);
$('latency-status').textContent = latencyError || (latencyRtt ? `Симуляция RTT ${latencyRtt} мс · ±20% на путь` : 'Симуляция задержки выключена');
$('latency-settings').onsubmit = event =>
{
    event.preventDefault();
    const target = new URL(location.href);
    target.searchParams.set('latency', $('latency-mode').value);
    persist(); connection.disconnect(); location.assign(target.href);
};
const latencySocketFactory = createLatencySocketFactory({ rtt: latencyRtt });
const connection = new M3Connection({
    socketFactory: address =>
    {
        const socket = latencySocketFactory(address);
        if (latencyRtt) { socket.addEventListener('close', event => { if ([4009, 4011].includes(event.code)) { error(event.reason); } }); }
        return socket;
    },
    url: `${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/ws`,
    onLobbyLeft: () =>
    {
        delete saved.token; delete saved.sessionId; delete saved.pending; persist();
    },
    onStatus: (message, ready) =>
    {
        fresh = state?.galaxy ? connection.lobbyReady?.() ?? ready : ready; $('connection').textContent = message; $('stale').hidden = !state || fresh;
        if (!fresh && galaxySettingsDirty) { clearGalaxySettingsTimer(); galaxySettingsPending = null; galaxySettingsDirty = false; galaxyViewKey = ''; }
        $('pending-count').textContent = `Ожидают ${connection.pending.size}`;
        renderer?.setConnectionFresh(ready); renderControls(); renderSelection();
        if (state) { renderResearch(); }
        renderMetrics();
        if (fresh && state?.galaxy) { void syncGalaxyAssets(); }
        advanceHostStart();
    },
    onWelcome: (welcome, login) =>
    {
        saved = login; persist();
        if (!restoringProfile && profile && manifest?.profileId === welcome.profileId && manifest.profileDigest === welcome.profileDigest) { connection.setProfile(manifest.profileId, manifest.profileDigest); }
    },
    onState: (snapshot, options) =>
    {
        const started = state?.phase !== 'running' && snapshot.phase === 'running';
        const changedView = perspectiveKey(state) !== perspectiveKey(snapshot);
        ++planetSnapshotId;
        state = resolveSpatialNames(hydrateDevelopmentTables(snapshot), localized);
        if (state?.galaxy && (state.galaxy.stage !== 'unprepared' || me()?.role !== 'player' || !me()?.isHost))
        {
            if (galaxySettingsDirty || galaxySettingsPending) { galaxyViewKey = ''; }
            clearGalaxySettingsTimer(); galaxySettingsPending = null; galaxySettingsDirty = false;
        }
        if (changedView)
        {
            selected = null; commandFleetId = null; currentSystem = null; outlinerCollapsed = false;
            economyKey = ''; $('economy-content').replaceChildren(); $('destination').replaceChildren();
            renderer?.select(null); renderer?.setCommandFleet(null);
        }
        if (renderer && profile)
        {
            renderer.applySnapshot(state, { ...options, resetInterpolation: changedView || options?.resetInterpolation });
        }
        if (!contextLost) { contextAwaitingSnapshot = false; }
        if (selected && !findSelected()) { selected = null; renderer?.select(null); }
        if (!ownFleet()) { commandFleetId = null; renderer?.setCommandFleet(null); }
        if (currentSystem && !snapshot.spatial?.systems.some(system => system.id === currentSystem)) { currentSystem = null; }
        if (started) { $('setup-dialog').close(); }
        renderState();
        void syncGalaxyAssets();
        advanceGalaxySettings();
        advanceHostStart();
    },
    onResult: result =>
    {
        persist();
        const refundAttempt = refundAttempts.get(result.commandId);
        if (refundAttempt)
        {
            refundAttempts.delete(result.commandId);
            if (refundAttempt.context === perspectiveKey(state) && refundAttempt.sessionId === state?.sessionId)
            {
                if (result.accepted) { refundLossNotice = { ...refundAttempt, losses: result.refundLosses ?? {} }; }
                else if (result.code === 'REFUND_PREVIEW_CHANGED' || result.code === 'REFUND_CONFIRMATION_REQUIRED') { planetRefund = { ...refundAttempt, waitAfterSnapshotId: planetSnapshotId }; planetRefundKey = ''; }
            }
            renderPlanetRefund();
        }
        for (const [area, id] of pendingResearch) { if (id === result.commandId) { pendingResearch.delete(area); break; } }
        commandRow(result.commandId, `${result.accepted ? 'Принято' : 'Отказ'} · ${result.code}: ${result.message}`, result.accepted ? 'accepted' : 'rejected');
        if (!result.accepted) { error({ STALE_GALAXY_CANDIDATE: 'Кандидат галактики устарел. Дождитесь нового manifest и подтвердите локальный набор снова.', PRESET_CONFIGURATION_ONLY: 'Сейчас конфигурация задаётся выбранным пресетом.' }[result.code] ?? result.message); }
        acceptHostStartResult(result);
    }
});

$('origin').textContent = `Сервер: ${location.origin}`;
$('origin').title = 'Версия протокола: 2';
$('name').value = saved.name ?? ''; $('code').value = saved.joinCode ?? ''; $('role').value = saved.role ?? 'player';
function showRequiredFiles(candidateManifest)
{
    const key = candidateManifest ? `${candidateManifest.profileId}:${candidateManifest.profileDigest}` : '';
    if (key === requiredManifestKey && $('required-files').textContent) { return; }
    requiredManifestKey = key;
    const paths = candidateManifest ? [...new Set([...(candidateManifest.files ?? []).map(file => file.relativePath), ...localizationPaths])].sort() : [];
    $('required-files').textContent = paths.length ? paths.join('\n') : 'Точный список появится после подготовки галактики и проверки текущего manifest.';
    for (const node of document.querySelectorAll('[data-file-count]')) { node.textContent = paths.length ? `${paths.length} файлов текущего профиля (графика и EN/RU)` : 'Файлы текущего профиля после подготовки'; }
}
showRequiredFiles(null);
function openPanel(id)
{
    if ($(id).open) { return; }
    for (const dialog of document.querySelectorAll('dialog[open]')) { dialog.close(); }
    // Modeless окно не забирает HUD в свою композицию; модальным нужен доступный feedback.
    const feedback = [...document.querySelectorAll('.alerts, .network-status')];
    if (id === 'planet-dialog') { document.body.append(...feedback); }
    else { $(id).prepend(...feedback); }
    if (id === 'planet-dialog') { planetWindow.open(); }
    else { $(id).showModal(); }
}
for (const dialog of document.querySelectorAll('dialog'))
{
    dialog.addEventListener('close', () =>
    {
        if (dialog.open) { return; }
        for (const node of dialog.querySelectorAll('.alerts, .network-status')) { document.body.append(node); }
    });
}
for (const button of document.querySelectorAll('[data-panel]')) { button.onclick = () => openPanel(button.dataset.panel); }
for (const button of document.querySelectorAll('[data-close]')) { button.onclick = () => button.closest('dialog').close(); }
function toggleOutliner()
{
    if (viewParticipantId(state) == null) { return; }
    outlinerCollapsed = !outlinerCollapsed; renderOutlinerVisibility(); syncLabelInsets();
}
function renderOutlinerVisibility()
{
    const personal = viewParticipantId(state) != null;
    $('outliner-panel').hidden = !personal || outlinerCollapsed;
    $('outliner-toggle').hidden = !personal;
    $('outliner-toggle').setAttribute('aria-expanded', String(personal && !outlinerCollapsed));
}
$('outliner-toggle').onclick = toggleOutliner;
document.querySelector('[data-toggle-outliner]').onclick = toggleOutliner;
function renderResearch()
{
    const participantId = viewParticipantId(state);
    if (researchParticipant !== participantId)
    {
        researchParticipant = participantId; researchView = researchPanelState();
    }
    const empire = researchEmpire(state, participantId);
    const ownPlayer = participantId != null && participantId === state?.you && me()?.role === 'player';
    const canChoose = canPlay() && ownPlayer && viewParticipantId(state) === state.you;
    const pendingForEmpire = [...pendingResearch.keys()].some(key => key.startsWith(`${participantId}:`));
    const areasPending = pendingForEmpire ? new Set(['physics', 'society', 'engineering']) : new Set();
    const main = $('research-panel'); const side = $('research-side');
    const imageStore = main.hidden || importing ? null : researchImages;
    const key = JSON.stringify([participantId, empire, researchView, canChoose, [...areasPending], main.hidden]);
    if (researchRenderCache?.key === key && researchRenderCache.localization === localization && researchRenderCache.imageStore === imageStore) { return; }
    researchLocalizationIssues.clear();
    $('research-toggle').setAttribute('aria-expanded', String(!main.hidden));
    const renderGeneration = ++researchRenderGeneration;
    renderResearchPanel(main, side, empire, researchView, {
        document, canChoose, pendingAreas: areasPending, resolveLocalization: (key, fallback, options) => localized(key, fallback, options, researchLocalizationIssues),
        imageStore,
        isCurrent: () => renderGeneration === researchRenderGeneration && researchParticipant === participantId && !main.hidden,
        onRequestDirectory: () => { void chooseDirectory(); },
        onChoose: (area, technologyId, revision) =>
        {
            if (area && technologyId)
            {
                const current = researchEmpire(state, viewParticipantId(state));
                if (!canChoose || !canPlay() || viewParticipantId(state) !== participantId || !current || current.revision !== revision || !current.areas?.some(item => item.id === area && item.choices?.some(choice => choice.id === technologyId)))
                {
                    error('Предложение изменилось. Дождитесь свежего снимка технологий.'); return;
                }
                const commandId = send('selectResearch', { area, technologyId, expectedResearchRevision: revision });
                if (commandId) { pendingResearch.set(`${participantId}:${area}`, commandId); }
            }
            renderResearch();
        },
        onClose: closeResearch
    });
    appendLocalizationStatus(main, researchLocalizationIssues);
    researchRenderCache = { key, localization, imageStore };
    syncLabelInsets();
}
function closeResearch()
{
    $('research-panel').hidden = true; $('research-side').hidden = true; researchView = researchPanelState();
    $('research-toggle').setAttribute('aria-expanded', 'false'); renderResearch();
}
$('research-toggle').onclick = () =>
{
    if (!$('research-panel').hidden) { closeResearch(); return; }
    if ($('planet-dialog').open) { closePlanetWindow(); }
    $('research-panel').hidden = false; researchView = researchPanelState(); renderResearch();
};
window.addEventListener('keydown', event => { if (event.key === 'Escape' && !$('research-panel').hidden) { closeResearch(); } });
$('join').addEventListener('submit', event =>
{
    event.preventDefault();
    if (latencyError) { error(latencyError); return; }
    if (restoringProfile) { error('Дождитесь восстановления локального графического профиля, затем подключитесь.'); return; }
    if ($('connect').disabled) { return; }
    if (!renderer) { try { createRenderer(); } catch (failure) { error(failure.message); return; } }
    saved = { ...saved, name: $('name').value.trim(), joinCode: $('code').value, role: $('role').value }; persist();
    error(''); connection.connect(saved);
});
$('disconnect').onclick = () => { if (!$('disconnect').disabled) { connection.disconnect(); } };
$('clear-login').onclick = () =>
{
    imageImportAbort?.abort(); imageRestoreAbort?.abort();
    ++importGeneration; importer.cancel(); importing = false; $('cancel-import').disabled = true;
    ++restorationId; restoringProfile = false;
    connection.dispose(); saved = {}; persist(); state = null; selected = null; commandFleetId = null;
    closePlanetWindow();
    profile = null; manifest = null; localization = null; localizationIssues.clear(); renderer?.dispose(); renderer = null; iconNodes = new Map(); uiSprites?.dispose(); uiSprites = null; planetImages?.dispose(); planetImages = null; researchImages?.dispose(); researchImages = null; acceptedSource = null; ++researchRenderGeneration;
    localizePlanetChrome();
    $('ui-presentation-status').textContent = 'CSS-аналог интерфейса до импорта оригинальных UI-текстур.';
    economyKey = ''; catalogKey = ''; galaxyViewKey = ''; galaxyManifestAttemptKey = ''; showRequiredFiles(null); currentSystem = null; stressView = false; contextLost = false; contextAwaitingSnapshot = false;
    frameMetrics = ''; frames = []; metricAt = 0;
    for (const id of ['participants', 'outliner', 'resources', 'economy-content', 'traits', 'destination', 'commands']) { $(id).replaceChildren(); }
    pendingResearch.clear(); researchLocalizationIssues.clear(); researchParticipant = null; researchView = researchPanelState(); researchRenderCache = null; hostStart = null; hostStartRetryRequired = false; hostStartRetryRevision = null; clearGalaxySettingsTimer(); galaxySettingsPending = null; galaxySettingsDirty = false; selectedSource = null; ++selectedSourceVersion; generatedImportAttemptKey = ''; $('research-panel').replaceChildren(); $('research-side').replaceChildren(); $('research-panel').hidden = true; $('research-side').hidden = true;
    $('clock').textContent = '2200.01.01'; $('view-name').textContent = 'Галактика'; $('metrics').textContent = ''; $('stale').hidden = true;
    $('command-feedback').textContent = 'Команды ещё не отправлены'; $('pending-count').textContent = 'Ожидают 0';
    renderIdentity(); renderSelection(); error('');
    $('import-status').textContent = 'Вход очищен. Для новой сессии выберите источник заново.'; renderControls();
};

async function readManifest()
{
    const response = await fetch('/m3/asset-manifest', { cache: 'no-store' });
    if (!response.ok) { throw new Error('Сервер не предоставил профиль M3. Проверьте --mode m3.'); }
    const reader = response.body.getReader(); const chunks = []; let length = 0;
    try
    {
        while (true)
        {
            const part = await reader.read(); if (part.done) { break; }
            length += part.value.byteLength; if (length > 1024 * 1024) { throw new Error('Manifest превышает 1 MiB.'); }
            chunks.push(part.value);
        }
    }
    finally { await reader.cancel(); }
    const bytes = new Uint8Array(length); let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
    return validateM3Manifest(JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes)));
}
async function syncGalaxyAssets()
{
    const galaxy = state?.galaxy;
    if (!galaxy || !['awaitingAssets', 'running'].includes(galaxy.stage) || !galaxy.worldFingerprint || !active || !fresh || connection.sequence < connection.candidateMinSequence) { return; }
    const version = connection.candidateVersion;
    const key = `${version}:${galaxy.worldFingerprint}`;
    if (galaxyManifestAttemptKey === key) { return; }
    galaxyManifestAttemptKey = key; const attemptId = ++galaxyManifestAttemptId; showRequiredFiles(null);
    try
    {
        const current = await readManifest();
        if (!active || !fresh || attemptId !== galaxyManifestAttemptId || version !== connection.candidateVersion || state?.galaxy?.worldFingerprint !== galaxy.worldFingerprint) { return; }
        showRequiredFiles(current);
        if (profile && manifest?.profileId === current.profileId && manifest.profileDigest === current.profileDigest)
        {
            connection.setProfile(current.profileId, current.profileDigest);
            connection.setGalaxyCandidate(current.profileId, current.profileDigest, galaxy.worldFingerprint, version);
            $('galaxy-status').textContent = 'Локальный набор совпадает с текущим кандидатом. Ожидается новое подтверждение ассетов.';
        }
        else { $('galaxy-status').textContent = 'Галактика подготовлена. Выберите локальную папку Stellaris для текущего набора.'; }
        if (selectedSource && (!profile || manifest?.profileId !== current.profileId || manifest.profileDigest !== current.profileDigest))
        {
            const importKey = `${version}:${galaxy.worldFingerprint}:${current.profileDigest}:${selectedSourceVersion}`;
            if (generatedImportAttemptKey !== importKey)
            {
                generatedImportAttemptKey = importKey;
                releaseOutdatedLobbyProfile();
                void importSource(selectedSource);
            }
        }
        renderControls();
    }
    catch (failure)
    {
        if (attemptId === galaxyManifestAttemptId && key === galaxyManifestAttemptKey && state?.galaxy?.worldFingerprint === galaxy.worldFingerprint) { $('galaxy-status').textContent = `Не удалось проверить текущий manifest: ${failure.message}`; }
    }
}
function releaseOutdatedLobbyProfile()
{
    if (state?.phase !== 'lobby' || !profile) { return; }
    renderer?.dispose(); renderer = null;
    uiSprites?.dispose(); uiSprites = null; planetImages?.dispose(); planetImages = null; researchImages?.dispose(); researchImages = null;
    profile = null; manifest = null; iconNodes = new Map(); planetViewKey = ''; researchRenderCache = null;
    $('import-status').textContent = 'Прежний графический профиль освобождён; проверяется набор подготовленной галактики.';
}
function chooseLocalSource(source)
{
    selectedSource = source; ++selectedSourceVersion; generatedImportAttemptKey = '';
    if (state?.galaxy?.stage === 'unprepared')
    {
        ++importGeneration; imageImportAbort?.abort(); importer.cancel(); importing = false; $('cancel-import').disabled = true;
        $('import-status').textContent = 'Источник выбран. После начала игры браузер проверит файлы подготовленной галактики.';
        return;
    }
    if (state?.galaxy?.stage === 'awaitingAssets' && state?.phase === 'lobby' && profile && state.assetProfile && (manifest?.profileId !== state.assetProfile.profileId || manifest?.profileDigest !== state.assetProfile.profileDigest)) { releaseOutdatedLobbyProfile(); }
    void importSource(source);
}
function prepareIcons(candidate)
{
    const icons = new Map();
    for (const [id, image] of candidate.icons)
    {
        const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
        const pixels = new Uint8ClampedArray(image.rgba.buffer, image.rgba.byteOffset, image.rgba.byteLength);
        const context = canvas.getContext('2d'); if (!context) { throw new Error('Canvas2D недоступен для иконок.'); }
        context.putImageData(new ImageData(pixels, image.width, image.height), 0, 0); icons.set(id, canvas);
    }
    return icons;
}
function appendIcon(node, path)
{
    const icon = iconNodes.get(path);
    if (!icon) { return; }
    const displayed = document.createElement('canvas'); displayed.width = icon.width; displayed.height = icon.height;
    displayed.getContext('2d').drawImage(icon, 0, 0); displayed.style.verticalAlign = 'middle'; displayed.style.marginRight = '.4rem'; node.prepend(displayed);
}
function appendPlanetImage(parent, key)
{
    if (!planetImages || !profile?.planetImages?.has(key)) { return false; }
    const image = planetImages.get(key);
    const node = document.createElement('img'); node.src = image.url; node.width = image.width; node.height = image.height; node.alt = key; node.className = 'planet-art'; parent.append(node);
    return true;
}
async function importSource(source)
{
    if (imageRestoreAbort) { error('Дождитесь восстановления изображений перед новым импортом.'); return; }
    const generation = ++importGeneration; importing = true; $('cancel-import').disabled = false; error('');
    imageImportAbort?.abort(); const imageAbort = new AbortController(); imageImportAbort = imageAbort;
    const priorImport = importSettled; let releaseImport; importSettled = new Promise(resolve => { releaseImport = resolve; });
    const previousProfile = profile; const galaxyCandidateVersion = connection.candidateVersion; const galaxyFingerprint = state?.galaxy?.worldFingerprint;
    let stagedUi = null; let stagedPlanet = null;
    importer.cancel();
    try
    {
        await priorImport;
        if (!active || generation !== importGeneration) { return; }
        const candidateManifest = await readManifest();
        if (!active || generation !== importGeneration) { return; }
        $('import-status').textContent = 'Чтение штатной локализации EN/RU…';
        const stagedLocalization = await loadLocalLocalization(source, { language: 'russian', signal: imageAbort.signal });
        if (!active || generation !== importGeneration) { return; }
        // Полный источник сохраняется у reader. Graphics worker получает только свою часть,
        // при этом неизвестные дополнительные файлы по-прежнему отвергает его strict validator.
        const graphicsSource = source.mode === 'files' ? { ...source, files: source.files.filter(file => !localizationNames.has(file.name)) } : source;
        const retained = profile?.decodedBytes ?? 0;
        const converted = (renderer?.stats().convertedBytes ?? 0) + (uiSprites?.decodedBytes ?? 0) + (planetImages?.decodedBytes ?? 0) + (researchImages?.decodedBytes ?? 0) + RESEARCH_IMAGE_RESERVE;
        const result = await importer.import({ manifest: candidateManifest, source: graphicsSource, maxDecodedBytes: MAX_MEMORY - retained - converted }, { onProgress: progress => { $('import-status').textContent = `${progress.phase}: ${progress.checked}/${progress.total} ${progress.relativePath ?? ''}`; } });
        if (!active || generation !== importGeneration) { return; }
        const icons = prepareIcons(result.profile);
        stagedUi = await prepareUiSprites(result.profile, { maxDecodedBytes: MAX_MEMORY - retained - converted - result.decodedBytes });
        if (!active || generation !== importGeneration) { return; }
        if (result.profile.planetImages) { stagedPlanet = await preparePlanetImages(result.profile, { signal: imageAbort.signal, maxDecodedBytes: Math.min(16 * 1048576, MAX_MEMORY - retained - converted - result.decodedBytes - stagedUi.decodedBytes) }); }
        if (!active || generation !== importGeneration) { return; }
        if (!renderer) { createRenderer(); }
        renderer.setProfile(result.profile, { maxConvertedBytes: MAX_MEMORY - retained - result.decodedBytes - stagedUi.decodedBytes - (stagedPlanet?.decodedBytes ?? 0) - (uiSprites?.decodedBytes ?? 0) - (planetImages?.decodedBytes ?? 0) - (researchImages?.decodedBytes ?? 0) - RESEARCH_IMAGE_RESERVE });
        // Publish CPU/icons/UI and network readiness only after successful GPU staging.
        uiSprites?.dispose(); uiSprites = stagedUi; stagedUi = null; uiSprites.apply();
        planetImages?.dispose(); planetImages = stagedPlanet; stagedPlanet = null; planetViewKey = '';
        researchImages?.dispose(); researchImages = makeResearchImageStore(source); acceptedSource = source; selectedSource = source;
        $('ui-presentation-status').textContent = 'Оригинальные UI-текстуры · шрифт и состояния браузерные.';
        profile = result.profile; manifest = candidateManifest; iconNodes = icons; localization = stagedLocalization; localizationIssues.clear(); $('planet-localization-diagnostics').replaceChildren(); economyKey = ''; catalogKey = '';
        resolveSpatialNames(state, localized);
        renderer.refreshSpatialNames(state);
        if (!state?.galaxy || galaxyCandidateVersion === connection.candidateVersion && state.galaxy.worldFingerprint === galaxyFingerprint) { showRequiredFiles(candidateManifest); }
        imageRestoreAbort?.abort(); ++restorationId; restoringProfile = false;
        $('import-status').textContent = `Готово · ${result.mode} · ${(result.decodedBytes / 1048576).toFixed(1)} MiB · ${(result.milliseconds / 1000).toFixed(2)} с. Локализация: русский, ${localization.stats.files} файлов, ${localization.diagnostics.length} диагностик. Статические приближённые материалы.`;
        connection.setProfile(manifest.profileId, manifest.profileDigest);
        if (state?.galaxy) { ++galaxyManifestAttemptId; galaxyManifestAttemptKey = ''; void syncGalaxyAssets(); }
        if (state) { renderer.applySnapshot(state, { receivedAt: performance.now(), resetInterpolation: true }); renderState(); }
    }
    catch (failure)
    {
        if (generation === importGeneration && active)
        {
            const reason = failure.name === 'AbortError' ? 'Импорт отменён.' : failure.message;
            const memoryFailure = failure.name !== 'AbortError' && /memory budget|decoded (?:image )?budget/i.test(reason);
            const preserved = previousProfile != null && profile === previousProfile ? ' Прежний набор сохранён.' : '';
            $('import-status').textContent = `${failure.name === 'AbortError' ? 'Импорт отменён.' : 'Импорт не выполнен.'}${preserved}`;
            error(memoryFailure ? `Импорт не выполнен: превышен лимит памяти.${preserved} Перезагрузите страницу и повторите импорт. Техническая причина: ${reason}` : `${reason}${preserved}`);
        }
    }
    finally { stagedUi?.dispose(); stagedPlanet?.dispose(); releaseImport(); if (imageImportAbort === imageAbort) { imageImportAbort = null; } if (generation === importGeneration) { importing = false; $('cancel-import').disabled = true; renderControls(); } }
}
$('files').onchange = () => { const files = [...$('files').files]; $('files').value = ''; if (files.length) { chooseLocalSource({ mode: 'files', files }); } };
function makeResearchImageStore(source)
{
    return createResearchImageStore({ source, maxDecodedBytes: RESEARCH_IMAGE_RESERVE, getAvailableDecodedBytes: () => Math.max(0, MAX_MEMORY - (profile?.decodedBytes ?? 0) - (renderer?.stats().convertedBytes ?? 0) - (uiSprites?.decodedBytes ?? 0) - (planetImages?.decodedBytes ?? 0)) });
}
async function chooseDirectory()
{
    if (imageRestoreAbort) { error('Дождитесь восстановления изображений перед выбором папки.'); return; }
    if (typeof window.showDirectoryPicker !== 'function') { error('Этот браузер не поддерживает выбор локальной папки.'); return; }
    imageImportAbort?.abort();
    const generation = ++importGeneration;
    try
    {
        const handle = await window.showDirectoryPicker({ mode: 'read' });
        if (active && generation === importGeneration) { chooseLocalSource({ mode: 'directory', handle }); }
    }
    catch (failure)
    {
        if (!active || generation !== importGeneration) { return; }
        if (failure.name !== 'AbortError') { error(failure.message); }
        if (failure.name === 'AbortError') { error('Выбор папки отменён. Для завершения запуска выберите локальную папку снова.'); }
    }
}
$('folder').disabled = typeof window.showDirectoryPicker !== 'function';
$('folder').onclick = chooseDirectory;
$('cancel-import').onclick = () => { ++importGeneration; imageImportAbort?.abort(); importer.cancel(); importing = false; $('cancel-import').disabled = true; $('import-status').textContent = 'Импорт отменён. Для завершения запуска повторите импорт из выбранного источника.'; };

function findSelected()
{
    return selectedObject(state, selected);
}
function descriptionRows(rows)
{
    const list = document.createElement('dl');
    for (const [label, value] of rows) { list.append(element('dt', label), element('dd', value)); }
    return list;
}
function renderSelection()
{
    const card = selectionCard(state, selected, localized); const fleet = ownFleet();
    document.querySelector('.selection-panel').hidden = !card && !fleet;
    $('selection').hidden = !card;
    const compactRows = rows =>
    {
        const main = rows.filter(([label]) => !['Маршрут', 'Прибытие'].includes(label));
        const extra = rows.filter(([label]) => ['Маршрут', 'Прибытие'].includes(label));
        const result = [descriptionRows(main)];
        if (extra.length) { const details = document.createElement('details'); details.append(element('summary', 'Маршрут и прибытие'), descriptionRows(extra)); result.push(details); }
        return result;
    };
    $('selection').replaceChildren(...(card ? [element('h3', card.title), element('p', card.subtitle), ...compactRows(card.rows)] : []));
    const systemId = selected?.kind === 'system' ? selected.id : ['gateway','wormhole'].includes(selected?.kind) ? selectedObject(state,selected)?.systemId : currentSystem;
    const nebula = state?.spatial?.nebulae?.find(item => item.systemIds.includes(systemId));
    if (card && nebula) { $('selection').append(element('p', `Туманность: ${localized(nebula.nameKey, nebula.nameKey || 'Название недоступно')}`)); }
    if (card && selected?.kind === 'fleet')
    {
        const selectedFleet = selectedObject(state, selected);
        const composition = element('p', selectedFleet?.visualId === 'ship.corvette.mammalian' ? `Корветы · ${selectedFleet.shipCount}` : `Корабли · ${selectedFleet?.shipCount ?? 'нет данных'}`);
        composition.className = 'fleet-composition';
        const rows = [...$('selection').children]; $('selection').replaceChildren(...rows.slice(0, 2), composition, ...rows.slice(2));
    }
    if (card?.diagnostic) { const details = document.createElement('details'); details.append(element('summary', 'Сведения об объекте'), element('p', card.diagnostic)); $('selection').append(details); }
    $('open-planet').hidden = !planetPanelModel(state, selected);
    $('order-controls').hidden = !fleet;
    $('command-fleet').textContent = fleet?.name ?? '';
    $('movement').replaceChildren(...(fleet ? compactRows(fleetOrderRows(state, fleet.id, connection.pending.values())) : []));
    for (const button of $('outliner').querySelectorAll('[data-kind]')) { button.setAttribute('aria-current', String(button.dataset.kind === selected?.kind && button.dataset.id === selected?.id)); }
    syncLabelInsets();
}
function renderIdentity()
{
    const identity = identityPresentation(state, currentSystem, stressView);
    $('view-name').textContent = identity.view; $('identity-label').textContent = identity.name;
    $('identity-detail').textContent = identity.detail; $('pause-state').hidden = !identity.paused;
    const nebula = state?.spatial?.nebulae?.find(item => item.systemIds.includes(currentSystem));
    if (nebula) { $('identity-detail').textContent += ` · Туманность: ${localized(nebula.nameKey, nebula.nameKey || 'Название недоступно')}`; }
    renderOutlinerVisibility();
    const views = observerViews(state);
    $('observer-view').hidden = views.length === 0;
    const viewOptions = views.map(([id, label]) => [String(id), label]);
    if (JSON.stringify([...$('observer-view').options].map(option => [option.value, option.textContent])) !== JSON.stringify(viewOptions))
    {
        $('observer-view').replaceChildren(...viewOptions.map(([id, label]) => { const option = element('option', label); option.value = id; return option; }));
    }
    $('observer-view').value = String(viewParticipantId(state) ?? 0);
    const options = cameraOptions(currentSystem, stressView);
    const preset = renderer?.stats().cameraPreset;
    if (JSON.stringify([...$('camera-preset').options].map(option => option.value)) !== JSON.stringify(options.map(([id]) => id)))
    {
        $('camera-preset').replaceChildren(...options.map(([id, label]) => { const option = element('option', label); option.value = id; return option; }));
    }
    if (options.some(([id]) => id === preset)) { $('camera-preset').value = preset; }
    syncLabelInsets();
}
function syncLabelInsets()
{
    const canvas = $('canvas').getBoundingClientRect(); const height = canvas.height;
    const plaque = document.querySelector('.identity-plaque').getBoundingClientRect();
    const top = $('pause-state').hidden ? plaque.top : Math.min(plaque.top, $('pause-state').getBoundingClientRect().top);
    document.documentElement.style.setProperty('--ui-bottom', `${Math.max(0, height - top)}px`);
    if (height > 0 && typeof renderer?.setLabelInsets === 'function')
    {
        const left = document.querySelector('.selection-panel'); const right = $('outliner-panel');
        renderer.setLabelInsets(labelInsets(height, document.querySelector('.topbar').getBoundingClientRect().bottom - canvas.top, top - canvas.top, { canvas, leftPanel: left.hidden ? null : left.getBoundingClientRect(), rightPanel: right.hidden ? null : right.getBoundingClientRect() }));
    }
}
function closeSelection()
{
    selected = null; commandFleetId = null; renderer?.select(null); renderer?.setCommandFleet(null); renderSelection(); renderControls();
}
$('close-selection').onclick = closeSelection;
function closePlanetWindow()
{
    ++planetInteractionId;
    closePlanetRefund();
    closePlanetDemolition();
    closePlanetZoneReplacement();
    $('planet-localization-diagnostics').replaceChildren();
    $('show-planet-on-map').onclick = null;
    if ($('planet-dialog').open) { $('planet-dialog').close(); }
    planetSelection = null; planetViewKey = ''; planetInspection = null; planetInspectionKey = ''; planetInspectionSource = null; expandedPlanetJobs.clear();
    $('planet-content').replaceChildren(); $('planet-heading').textContent = 'Колония';
}
function canEditPlanet(model)
{
    return canPlay() && me()?.assetReady === true && model?.ownerId === state.you && viewParticipantId(state) === state.you && Number.isSafeInteger(model?.economy?.id ?? model?.development?.economyPlanetId);
}
function pendingPlanetJobs(model)
{
    return [...connection.pending.values()].filter(command => command.type === 'setJobs' && command.planetId === model?.economy?.id || ['adjustWorkforceLimit', 'setWorkforceLimitMin', 'setWorkforceLimitMax', 'setWorkforceLimit'].includes(command.type) && command.colonyId === model?.id).map(command => command.job);
}
function submitPlanetWorkforce(binding, request)
{
    const model = planetPanelModel(state, planetSelection);
    if (!$('planet-dialog').open || planetTab !== 'jobs' || binding.interactionId !== planetInteractionId || binding.context !== perspectiveKey(state) || planetSelection?.context !== binding.context || model?.id !== binding.colonyId || model?.development?.revision !== binding.revision || !canEditPlanet(model)) { error('Лимит Workforce недоступен: требуется актуальный снимок собственной колонии.'); return; }
    const jobs = model.development?.economy?.jobs?.filter(job => job.jobId === binding.jobId) ?? [];
    const job = jobs[0];
    if (jobs.length !== 1 || !Number.isFinite(job.capacity) || job.capacity < 0 || !Number.isFinite(job.limit) || !Number.isSafeInteger(binding.revision) || binding.revision < 0) { error('Параметры профессии недоступны.'); return; }
    if (pendingPlanetJobs(model).length || pendingPlanetConstruction(model).length) { error('Дождитесь подтверждения изменения колонии.'); return; }
    const fields = { colonyId: model.id, expectedColonyRevision: binding.revision, job: job.jobId };
    if (request.kind === 'unsupportedUnit') { error('Изменение на одно место недоступно: единица Workforce ещё не определена.'); return; }
    if (request.kind === 'adjust' && [-1, 1].includes(request.direction)) { send('adjustWorkforceLimit', { ...fields, count: request.direction }); return; }
    if (request.kind === 'min' || request.kind === 'max') { send(request.kind === 'min' ? 'setWorkforceLimitMin' : 'setWorkforceLimitMax', fields); return; }
    const count = request.kind === 'absolute' && typeof request.raw === 'string' && request.raw.trim() ? Number(request.raw) : NaN;
    if (!Number.isFinite(count) || count < 0 || count > job.capacity) { error('Лимит должен быть числом от 0 до полученной вместимости.'); return; }
    send('setWorkforceLimit', { ...fields, count });
}
function submitPlanetJobs(binding, raw)
{
    const model = planetPanelModel(state, planetSelection);
    if (!$('planet-dialog').open || planetTab !== 'jobs' || binding.interactionId !== planetInteractionId || binding.context !== perspectiveKey(state) || planetSelection?.context !== binding.context || model?.id !== binding.colonyId || model?.economy?.id !== binding.planetId || model?.development?.economy != null || model?.development?.revision !== binding.revision || model?.contentProfileId !== binding.contentProfileId || !canEditPlanet(model)) { error('Вакансии недоступны. Откройте собственную колонию после подключения и импорта.'); return; }
    if (!state.catalog.jobs.some(job => job.id === binding.jobId) || !model.economy.jobs.some(job => job.jobId === binding.jobId)) { error('Эта профессия недоступна для изменения вакансий.'); return; }
    if (pendingPlanetJobs(model).includes(binding.jobId)) { error('Дождитесь подтверждения предыдущего изменения вакансий.'); return; }
    const count = typeof raw === 'string' && raw.trim() ? Number(raw) : NaN;
    if (!Number.isFinite(count) || count < 0 || count > 6 || !Number.isInteger(count * 2)) { error('Допустимо 0–6 вакансий с шагом 0.5.'); return; }
    send('setJobs', { planetId: binding.planetId, job: binding.jobId, count });
}
function pendingPlanetConstruction(model)
{
    return [...connection.pending.values()].filter(command => ['enqueueConstruction', 'enqueueUpgrade', 'enqueueZoneReplacement', 'cancelConstruction', 'clearBlocker', 'reorderConstruction', 'setBuildingEnabled', 'demolishBuilding'].includes(command.type) && command.colonyId === model.id);
}
function closePlanetZoneReplacement()
{
    planetZoneReplacement = null;
    const dialog = $('planet-zone-replacement-dialog'); if (dialog.open) dialog.close();
}
function openPlanetZoneReplacement(binding, operation)
{
    closePlanetZoneReplacement();
    const intent = Object.freeze({ ...binding }); planetZoneReplacement = intent;
    const content = $('planet-zone-replacement-content'); content.replaceChildren();
    const heading = document.createElement('h3'); heading.textContent = 'Подтвердить замену специализации';
    const explanation = document.createElement('p'); explanation.textContent = 'При завершении замены будут удалены перечисленные здания. Возврат ресурсов не предусмотрен.';
    const list = document.createElement('ul');
    for (const building of operation.removedBuildings)
    {
        const item = document.createElement('li'); item.textContent = localized(building.localizationKey ?? building.definitionId, building.definitionId); list.append(item);
    }
    const actions = document.createElement('div'); actions.className = 'planet-refund-actions';
    const cancel = document.createElement('button'); cancel.type = 'button'; cancel.textContent = 'Отмена'; cancel.onclick = closePlanetZoneReplacement;
    const confirm = document.createElement('button'); confirm.type = 'button'; confirm.textContent = 'Заменить'; confirm.onclick = () =>
    {
        if (planetZoneReplacement !== intent) return;
        closePlanetZoneReplacement(); submitPlanetConstruction(intent, false, false, true);
    };
    actions.append(cancel, confirm); content.append(heading, explanation, list, actions);
    $('planet-zone-replacement-dialog').showModal();
}
function validatePlanetZoneReplacement(model)
{
    const intent = planetZoneReplacement;
    if (!intent) return;
    const current = constructionOperations(model).find(item => JSON.stringify(item) === intent.fingerprint);
    if (!$('planet-zone-replacement-dialog').open || intent.sessionId !== state?.sessionId || intent.interactionId !== planetInteractionId || intent.snapshotId !== planetSnapshotId || intent.context !== perspectiveKey(state) || planetSelection?.context !== intent.context || model?.id !== intent.colonyId || model?.development?.revision !== intent.revision || planetTab !== intent.tab || !canEditPlanet(model) || !current || pendingPlanetConstruction(model).length || pendingPlanetJobs(model).length)
    { closePlanetZoneReplacement(); }
}
function closePlanetRefund()
{
    planetRefund = null; planetRefundKey = ''; $('planet-refund-dialog').close();
}
function closePlanetDemolition()
{
    planetDemolition = null;
    const dialog = $('planet-demolition-dialog'); if (dialog.open) dialog.close();
}
function openPlanetDemolition(binding, buildingName)
{
    closePlanetDemolition();
    const intent = Object.freeze({ ...binding, buildingName }); planetDemolition = intent;
    const content = $('planet-demolition-content'); content.replaceChildren();
    const heading = document.createElement('h3'); heading.textContent = 'Подтвердить демонтаж';
    const explanation = document.createElement('p'); explanation.textContent = `Здание «${buildingName}» будет удалено. Возврат ресурсов не предусмотрен.`;
    const actions = document.createElement('div'); actions.className = 'planet-refund-actions';
    const cancel = document.createElement('button'); cancel.type = 'button'; cancel.textContent = 'Отмена'; cancel.onclick = closePlanetDemolition;
    const confirm = document.createElement('button'); confirm.type = 'button'; confirm.textContent = 'Демонтировать'; confirm.onclick = () =>
    {
        if (planetDemolition !== intent) return;
        closePlanetDemolition(); submitPlanetConstruction(intent, false, true);
    };
    actions.append(cancel, confirm); content.append(heading, explanation, actions);
    $('planet-demolition-dialog').showModal();
}
function validatePlanetDemolition(model)
{
    const intent = planetDemolition;
    if (!intent) return;
    const current = constructionOperations(model).find(item => JSON.stringify(item) === intent.fingerprint);
    if (!$('planet-demolition-dialog').open || intent.sessionId !== state?.sessionId || intent.interactionId !== planetInteractionId || intent.snapshotId !== planetSnapshotId || intent.context !== perspectiveKey(state) || planetSelection?.context !== intent.context || model?.id !== intent.colonyId || model?.development?.revision !== intent.revision || planetTab !== intent.tab || !canEditPlanet(model) || !current || pendingPlanetConstruction(model).length || pendingPlanetJobs(model).length)
    { closePlanetDemolition(); }
}
function refundViewOptions()
{
    return { document, appendResourceIcon: (parent, id) => appendPlanetImage(parent, `resource.${id}`), formatAmount: value => number.format(value), resourceLabel: id => localized(id, 'Ресурс') };
}
function renderPlanetRefund()
{
    const model = planetPanelModel(state, planetSelection);
    if (refundLossNotice && (refundLossNotice.sessionId !== state?.sessionId || refundLossNotice.context !== perspectiveKey(state) || !canReadPlanetNotices() || !readablePlanetColonies().some(item => item.id === refundLossNotice.colonyId))) { refundLossNotice = null; }
    const lossKey = JSON.stringify(refundLossNotice);
    if (lossKey !== refundLossNoticeKey) { refundLossNoticeKey = lossKey; renderConstructionRefundLosses($('planet-refund-losses'), refundLossNotice?.losses ?? {}, refundViewOptions()); }
    if (!planetRefund) { return; }
    const intent = planetRefund;
    if (!$('planet-dialog').open || intent.sessionId !== state?.sessionId || intent.tab !== planetTab || intent.interactionId !== planetInteractionId || intent.context !== perspectiveKey(state) || model?.id !== intent.colonyId || !canEditPlanet(model)) { closePlanetRefund(); return; }
    if (intent.waitAfterSnapshotId >= planetSnapshotId) { $('planet-refund-dialog').close(); return; }
    const operation = constructionOperations(model).find(item => item.type === 'cancelConstruction' && item.orderId === intent.orderId);
    if (!operation || !Object.values(operation.refundQuote.losses).some(value => value > 0)) { closePlanetRefund(); return; }
    const fingerprint = JSON.stringify(operation);
    if (planetRefundKey === fingerprint) { return; }
    planetRefundKey = fingerprint;
    renderConstructionRefund($('planet-refund-content'), operation.refundQuote.losses, { ...refundViewOptions(), onKeep: closePlanetRefund, onConfirm: () =>
    {
        const current = planetPanelModel(state, planetSelection);
        if (planetRefund !== intent || planetRefundKey !== fingerprint || !current) { return; }
        submitPlanetConstruction({ ...intent, snapshotId: planetSnapshotId, revision: current.development?.revision, fingerprint }, true);
    } });
    if (!$('planet-refund-dialog').open) { $('planet-refund-dialog').showModal(); }
}
$('planet-refund-dialog').oncancel = closePlanetRefund;
$('planet-demolition-dialog').oncancel = closePlanetDemolition;
$('planet-demolition-dialog').addEventListener('close', () => { if (!$('planet-demolition-dialog').open) planetDemolition = null; });
$('planet-zone-replacement-dialog').oncancel = closePlanetZoneReplacement;
$('planet-zone-replacement-dialog').addEventListener('close', () => { if (!$('planet-zone-replacement-dialog').open) planetZoneReplacement = null; });
function submitPlanetConstruction(binding, confirmedRefund = false, confirmedDemolition = false, confirmedZoneRemoval = false)
{
    const model = planetPanelModel(state, planetSelection);
    if (!$('planet-dialog').open || planetTab !== binding.tab || binding.sessionId !== state?.sessionId || binding.interactionId !== planetInteractionId || binding.snapshotId !== planetSnapshotId || binding.context !== perspectiveKey(state) || planetSelection?.context !== binding.context || model?.id !== binding.colonyId || model?.development?.revision !== binding.revision || !canEditPlanet(model)) { error('Строительство недоступно: откройте собственную колонию с актуальным снимком.'); return; }
    const operation = constructionOperations(model).find(item => JSON.stringify(item) === binding.fingerprint);
    if (!operation) { error('Операция изменилась или недоступна. Проверьте актуальные условия колонии.'); return; }
    if (pendingPlanetConstruction(model).length || pendingPlanetJobs(model).length) { error('Дождитесь подтверждения предыдущего изменения колонии.'); return; }
    const fields = { colonyId: model.id, expectedColonyRevision: binding.revision };
    if (operation.type === 'cancelConstruction')
    {
        if (Object.values(operation.refundQuote.losses).some(value => value > 0) && !confirmedRefund)
        { planetRefund = { ...binding, sessionId: state?.sessionId, orderId: operation.orderId }; planetRefundKey = ''; renderPlanetRefund(); return; }
        const id = send(operation.type, { ...fields, orderId: operation.orderId, refundPreviewId: operation.refundQuote.previewId, confirmRefundLoss: confirmedRefund });
        if (id) { refundAttempts.set(id, { ...binding, sessionId: state?.sessionId, orderId: operation.orderId }); closePlanetRefund(); }
        return;
    }
    if (operation.type === 'reorderConstruction') { send(operation.type, { ...fields, orderIds: [...operation.orderIds] }); return; }
    if (operation.type === 'setBuildingEnabled')
    {
        send(operation.type, { ...fields, definitionId: operation.definitionId, slotId: operation.slotId, enabled: operation.enabled }); return;
    }
    if (operation.type === 'demolishBuilding')
    {
        if (!confirmedDemolition)
        {
            const installed = model.development.installed.filter(item => item.kind === 'building' && item.slotId === operation.slotId && item.definitionId === operation.definitionId);
            if (installed.length !== 1) { error('Здание для демонтажа изменилось.'); return; }
            const buildingName = localized(installed[0].localizationKey ?? installed[0].definitionId, installed[0].name ?? installed[0].definitionId);
            openPlanetDemolition(binding, buildingName); return;
        }
        send(operation.type, { ...fields, definitionId: operation.definitionId, slotId: operation.slotId, confirmDemolition: true }); return;
    }
    if (operation.type === 'enqueueZoneReplacement' && operation.removedBuildings.length && !confirmedZoneRemoval)
    { openPlanetZoneReplacement(binding, operation); return; }
    try
    {
        const orderId = crypto.randomUUID();
        if (!constructionIdentifier(orderId)) { throw new Error('Не удалось создать идентификатор заказа.'); }
        send(operation.type, { ...fields, definitionId: operation.definitionId, slotId: operation.slotId, orderId,
            ...(['enqueueUpgrade', 'enqueueZoneReplacement'].includes(operation.type) ? { sourceDefinitionId: operation.sourceDefinitionId, zoneInstanceId: operation.zoneInstanceId } : {}),
            ...(operation.type === 'enqueueZoneReplacement' ? { zoneReplacementPreviewId: operation.zoneReplacementPreviewId, confirmZoneRemoval: confirmedZoneRemoval } : {}) });
    }
    catch (failure) { error(failure.message); }
}
function planetInspectionSourceKey(model, inspection)
{
    if (inspection?.slotId == null) { return null; }
    const slot = model?.development?.slots.find(item => item.slotId === inspection.slotId);
    if (!slot) { return null; }
    const installed = inspection.kind === 'installed' ? model.development.installed.some(item => item.slotId === slot.slotId && item.kind === slot.kind) : null;
    return JSON.stringify([slot.slotId, slot.kind, slot.zoneInstanceId, installed]);
}
function renderPlanetWindow()
{
    localizePlanetChrome();
    if (!planetSelection) { return; }
    const model = planetPanelModel(state, planetSelection);
    if (!model || planetSelection.context !== perspectiveKey(state)) { closePlanetWindow(); return; }
    validatePlanetDemolition(model);
    validatePlanetZoneReplacement(model);
    const inspectionKey = JSON.stringify([perspectiveKey(state), model.id, model.ownerId]);
    const resolvedInspection = resolvePlanetInspection(model, planetInspection);
    if (planetInspectionKey !== inspectionKey || planetInspection && (!resolvedInspection || planetInspectionSource !== planetInspectionSourceKey(model, planetInspection))) { if (planetInspection) { ++planetInteractionId; } planetInspection = null; planetInspectionSource = null; planetInspectionKey = inspectionKey; }
    else if (planetInspection) { planetInspection = resolvedInspection; }
    const cycle = readablePlanetColonies(); const cycleBinding = { interactionId: planetInteractionId, snapshotId: planetSnapshotId, context: planetSelection.context, colonyId: model.id };
    const cycleEnabled = cycle.length > 1 && active && fresh && connection.ready() && !restoringProfile && !contextAwaitingSnapshot && !contextLost && !!profile && me()?.assetReady === true;
    const previousColony = $('planet-previous'); const nextColony = $('planet-next');
    previousColony.disabled = !cycleEnabled; nextColony.disabled = !cycleEnabled;
    bindPlanetCycleButton(previousColony, -1, cycleBinding); bindPlanetCycleButton(nextColony, 1, cycleBinding);
    const navigationBinding = { interactionId: planetInteractionId, snapshotId: planetSnapshotId, context: planetSelection.context, colonyId: model.id };
    $('show-planet-on-map').onclick = () =>
    {
        const current = planetPanelModel(state, planetSelection);
        if (!$('planet-dialog').open || navigationBinding.interactionId !== planetInteractionId || navigationBinding.snapshotId !== planetSnapshotId || navigationBinding.context !== perspectiveKey(state) || planetSelection?.context !== navigationBinding.context || planetSelection?.id !== navigationBinding.colonyId || current?.id !== navigationBinding.colonyId || !active || !fresh || !connection.ready() || restoringProfile || contextAwaitingSnapshot || contextLost || !profile || !renderer || me()?.assetReady !== true)
        {
            error('Колония недоступна для перехода. Дождитесь актуального обзора, подключения и импорта.'); return;
        }
        const value = { kind: 'colony', id: current.id };
        const navigation = outlinerNavigation(state, value);
        if (navigation.reason) { error(navigation.reason); return; }
        const previous = { systemId: currentSystem, stress: stressView, selection: selected };
        const camera = renderer.camera; const controls = renderer.controls;
        const cameraState = camera && controls ? { position: camera.position.clone(), target: controls.target.clone(), fov: camera.fov, near: camera.near, far: camera.far, preset: renderer.cameraPreset, detail: renderer.detailLevel } : null;
        let selectionChanged = false;
        try
        {
            renderer.select(value); selectionChanged = true; select(value);
            if (currentSystem !== navigation.systemId || stressView) { if (!openSystem(navigation.systemId)) { throw new Error($('error').textContent); } }
            renderer.focusSelection(); error('');
        }
        catch (failure)
        {
            try
            {
                if (previous.stress) { renderer.showStress200(); }
                else if (previous.systemId && previous.systemId !== currentSystem) { renderer.showSystem(previous.systemId); }
                else if (!previous.systemId && currentSystem) { renderer.showGalaxy(); }
                currentSystem = previous.systemId; stressView = previous.stress;
                if (selectionChanged) { renderer.select(previous.selection); select(previous.selection); }
                if (cameraState)
                {
                    camera.position.copy(cameraState.position); controls.target.copy(cameraState.target);
                    camera.fov = cameraState.fov; camera.near = cameraState.near; camera.far = cameraState.far;
                    renderer.cameraPreset = cameraState.preset; renderer.detailLevel = cameraState.detail;
                    camera.updateProjectionMatrix(); controls.update();
                }
                renderIdentity(); renderControls();
            }
            catch (rollbackFailure) { error(`${failure.message} Возврат вида не удался: ${rollbackFailure.message}`); return; }
            error(failure.message);
        }
    };
    const editable = canEditPlanet(model); const pending = pendingPlanetJobs(model);
    const constructionPending = pendingPlanetConstruction(model); const operations = constructionOperations(model);
    const knownJobs = state.catalog.jobs.map(job => job.id);
    const key = JSON.stringify([model, planetTab, planetInspection, editable, pending, knownJobs, constructionPending]);
    if (key === planetViewKey) { return; }
    planetViewKey = key; $('planet-heading').textContent = model.name;
    for (const button of document.querySelectorAll('[data-planet-tab]')) { button.setAttribute('aria-selected', String(button.dataset.planetTab === planetTab)); }
    const interactionId = planetInteractionId; const context = perspectiveKey(state);
    const draftContext = JSON.stringify([interactionId, context, model.id, model.economy?.id]);
    const focused = document.activeElement;
    const draft = planetTab === 'jobs' && editable && $('planet-content').contains(focused) && !focused.disabled && focused.dataset.planetJobContext === draftContext ? { jobId: focused.dataset.jobId, value: focused.value, receivedValue: focused.dataset.receivedValue, start: focused.selectionStart, end: focused.selectionEnd } : null;
    let restoreDraft = null;
    // Runtime diagnostics относятся только к заново разрешённой текущей колонии.
    // Ключи предыдущего приватного снимка не переносятся в другой POV/foreign state.
    localizationIssues.clear();
    renderPlanetPanel($('planet-content'), model, { document, tab: planetTab, inspection: planetInspection, onInspect: value =>
    {
        const current = planetPanelModel(state, planetSelection);
        if (!$('planet-dialog').open || planetTab !== 'infrastructure' || planetViewKey !== key || interactionId !== planetInteractionId || perspectiveKey(state) !== context || planetSelection?.context !== context || current?.id !== model.id || current?.development?.revision !== model.development?.revision) { error('Сведения изменились. Откройте место строительства заново.'); return; }
        const resolved = value == null ? null : resolvePlanetInspection(current, value);
        if (value != null && !resolved) { error('Выбранное место или определение больше недоступно.'); return; }
        planetInspection = resolved; planetInspectionSource = planetInspectionSourceKey(current, resolved); ++planetInteractionId; planetViewKey = ''; renderPlanetWindow();
    }, expandedJobs: expandedPlanetJobs, appendImage: appendPlanetImage, resolveLocalization: localized,
    resolveImageKey: (kind, id) => { const key = `${kind}.${id}`; return profile?.planetImages?.has(key) ? key : null; },
    appendResourceIcon: (parent, id) => appendPlanetImage(parent, `resource.${id}`),
    appendWorkforce: (parent, job) =>
    {
        const section = appendPlanetWorkforceControls(parent, { document, job, reference: true, resolveLocalization: localized, disabled: !editable, pending: pending.length > 0 || constructionPending.length > 0, onRequest: request => submitPlanetWorkforce({ interactionId, context, colonyId: model.id, revision: model.development.revision, jobId: job.jobId }, request) });
        return true;
    }, appendVacancies: (parent, job) =>
    {
        if (!knownJobs.includes(job.jobId)) { return false; }
        const section = appendJobControls(parent, { document, jobId: job.jobId, count: job.assigned, disabled: !editable, pending: pending.includes(job.jobId), onSubmit: raw => submitPlanetJobs({ interactionId, context, colonyId: model.id, planetId: model.economy.id, jobId: job.jobId, revision: model.development?.revision, contentProfileId: model.contentProfileId }, raw) });
        const input = section.querySelectorAll('input')[0];
        input.dataset.planetJobContext = draftContext; input.dataset.jobId = job.jobId; input.dataset.receivedValue = String(job.assigned);
        if (draft?.jobId === job.jobId && !input.disabled) { if (draft.value !== draft.receivedValue) { input.value = draft.value; } restoreDraft = input; }
        return true;
    }, appendConstruction: (parent, identity) =>
    {
        let appended = false;
        const matching = operations.filter(item => identity.zoneReplacement === true
            ? item.type === 'enqueueZoneReplacement' && item.sourceDefinitionId === identity.sourceDefinitionId && item.definitionId === identity.definitionId && item.slotId === identity.slotId && item.zoneInstanceId === identity.zoneInstanceId && item.zoneReplacementPreviewId === identity.zoneReplacementPreviewId
            : identity.direction != null ? item.type === 'reorderConstruction' && item.orderId === identity.orderId && item.direction === identity.direction : identity.instanceId != null ? item.type === 'clearBlocker' && item.instanceId === identity.instanceId : identity.orderId != null ? item.type === 'cancelConstruction' && item.orderId === identity.orderId : identity.demolition === true ? item.type === 'demolishBuilding' && item.definitionId === identity.definitionId && item.slotId === identity.slotId : identity.enabled != null ? item.type === 'setBuildingEnabled' && item.definitionId === identity.definitionId && item.slotId === identity.slotId && item.enabled === identity.enabled : identity.upgradeIcon ? item.type === 'enqueueUpgrade' && item.sourceDefinitionId === identity.sourceDefinitionId && item.definitionId === identity.definitionId && item.slotId === identity.slotId && item.zoneInstanceId === identity.zoneInstanceId : item.type === 'enqueueConstruction' && item.definitionId === identity.definitionId && item.slotId === identity.slotId);
        for (const operation of matching)
        {
            if (operation.type === 'clearBlocker') { parent.append(element('p', `Стоимость: ${Object.entries(operation.quote.costs).map(([id, amount]) => `${localized(id, names[id] ?? id)}: ${number.format(amount)}`).join(' · ') || 'Нет'} · Срок: ${number.format(operation.quote.durationDays)} дней`)); }
            const button = element('button', constructionPending.length ? 'Ожидание ответа…' : operation.type === 'setBuildingEnabled' ? operation.enabled ? 'Включить' : 'Отключить' : operation.type === 'cancelConstruction' ? `${localized('CANCEL', 'Отменить')} заказ ${operation.orderId}` : operation.type === 'clearBlocker' ? `${localized('CLEAR_BLOCKER', 'Расчистить')} · ${operation.instanceId} · ${operation.slotId}` : `${operation.type === 'enqueueUpgrade' ? localized('UPGRADE', 'Улучшить') : localized('BUILD', 'Построить')} · ${operation.slotId}`);
            button.type = 'button'; button.disabled = !editable || constructionPending.length > 0 || pending.length > 0;
            button.dataset.constructionType = operation.type;
            if (operation.type === 'reorderConstruction') { button.dataset.orderId = operation.orderId; button.dataset.direction = operation.direction; }
            button.dataset.planetActionIdentity = JSON.stringify([operation.type, operation.orderId, operation.direction, operation.slotId, operation.definitionId, operation.enabled, identity.demolition === true]);
            if (operation.type === 'enqueueUpgrade' && identity.upgradeIcon)
            {
                button.className = 'planet-upgrade-target'; button.dataset.availability = 'available';
                button.setAttribute('aria-label', `Улучшить до ${operation.definitionId} · ${operation.slotId}`);
                if (identity.imageDefinitionKey) { appendPlanetImage(button, identity.imageDefinitionKey); }
            }
            const binding = { sessionId: state?.sessionId, interactionId, snapshotId: planetSnapshotId, context, colonyId: model.id, revision: model.development.revision, tab: planetTab, fingerprint: JSON.stringify(operation) };
            button.onclick = () => submitPlanetConstruction(binding); parent.append(button);
            appended = true;
        }
        return appended;
    } });
    $('planet-localization-diagnostics').replaceChildren();
    appendLocalizationStatus($('planet-localization-diagnostics'));
    planetWindow.layout();
    if (restoreDraft) { const exact = restoreDraft.closest('details'); if (exact) exact.open = true; restoreDraft.focus({ preventScroll: true }); if (draft.start != null && draft.end != null) { restoreDraft.setSelectionRange(draft.start, draft.end); } }
}
$('open-planet').onclick = () =>
{
    const model = planetPanelModel(state, selected);
    if (!model) { error('Колония недоступна в текущем обзоре.'); return; }
    if (!$('research-panel').hidden) { closeResearch(); }
    ++planetInteractionId; planetSelection = { kind: 'colony', id: model.id, context: perspectiveKey(state) }; planetViewKey = ''; planetInspection = null; planetInspectionKey = '';
    renderPlanetWindow(); openPanel('planet-dialog');
};
$('planet-dialog').addEventListener('close', () => { if (!$('planet-dialog').open) { closePlanetWindow(); } });
for (const button of document.querySelectorAll('[data-planet-tab]'))
{
    button.onclick = () => { if (planetTab !== button.dataset.planetTab) { ++planetInteractionId; planetInspection = null; } planetTab = button.dataset.planetTab; renderPlanetWindow(); };
}
function renderOutliner()
{
    const focused = $('outliner').contains(document.activeElement) ? { kind: document.activeElement.dataset.kind, id: document.activeElement.dataset.id } : null;
    const groups = outlinerGroups(state);
    const nodes = groups.map(group =>
    {
        const section = document.createElement('section'); section.className = 'outliner-group';
        const heading = element('h3', group.title); heading.append(element('span', String(group.rows.length))); section.append(heading);
        for (const row of group.rows)
        {
            const button = document.createElement('button'); button.dataset.kind = row.kind; button.dataset.id = row.id;
            button.disabled = !profile || !renderer;
            const text = document.createElement('span'); text.className = 'outliner-text';
            const name = element('strong', row.name); name.className = 'outliner-name';
            const detail = element('small', row.detail); detail.className = 'outliner-detail';
            text.append(name, detail); button.append(text);
            if (row.icon) { appendIcon(button, row.icon); }
            section.append(button);
        }
        return section;
    });
    $('outliner').replaceChildren(...(nodes.length ? nodes : [element('p', 'Собственных объектов пока нет.')]));
    if (focused) { [...$('outliner').querySelectorAll('[data-kind]')].find(node => node.dataset.kind === focused.kind && node.dataset.id === focused.id)?.focus({ preventScroll: true }); }
}
$('outliner').onclick = event =>
{
    const button = event.target.closest('[data-kind]'); if (!button || button.disabled) { return; }
    const value = { kind: button.dataset.kind, id: button.dataset.id }; renderer?.select(value); select(value);
};
$('outliner').ondblclick = event =>
{
    const button = event.target.closest('[data-kind]'); if (!button || button.disabled) { return; }
    const value = { kind: button.dataset.kind, id: button.dataset.id };
    const navigation = outlinerNavigation(state, value);
    if (navigation.reason) { error(navigation.reason); return; }
    if (!openSystem(navigation.systemId)) { return; }
    try { renderer.select(value); select(value); renderer.focusSelection(); }
    catch (failure) { error(failure.message); }
};
function renderControls()
{
    const online = connection.ready() && fresh && active && !restoringProfile && !contextAwaitingSnapshot; const player = me()?.role === 'player'; const host = !!me()?.isHost;
    const lobbyOnline = (connection.lobbyReady?.() ?? connection.ready()) && fresh && active && !restoringProfile && !contextAwaitingSnapshot;
    $('configure').hidden = !state || !!state.galaxy;
    if (state?.galaxy) { $('traits').replaceChildren(); catalogKey = ''; }
    $('save-config').disabled = $('configure').hidden || !online || !player || state?.phase !== 'lobby';
    const galaxy = state?.galaxy;
    const draftBusy = galaxySettingsDirty || !!galaxySettingsPending || [...connection.pending.values()].some(command => command.type === 'setGalaxySettings' || command.type === 'selectEmpireTemplate');
    const players = state?.participants?.filter(item => item.role === 'player') ?? [];
    $('setup').dataset.step = !lobbyOnline ? '1' : galaxy?.stage === 'awaitingAssets' ? '2' : '3';
    $('setup-progress').textContent = !lobbyOnline ? 'Подключитесь к серверу, чтобы выбрать империю и войти в лобби.'
        : state?.phase === 'running' ? 'Партия запущена. Закройте это окно, чтобы вернуться к карте.'
        : galaxy?.stage === 'awaitingAssets' ? 'Галактика подготовлена. Каждый игрок проверяет локальные файлы; после подтверждений партия начнётся автоматически.'
        : galaxy?.startPending ? 'Запуск партии выполняется. Дождитесь подготовки галактики и проверки файлов.'
        : draftBusy ? 'Дождитесь подтверждения настроек перед готовностью.'
        : players.length === 0 ? 'Ожидаются игроки. Выберите шаблон империи и настройки галактики.'
        : players.some(item => !item.connected) ? 'Ожидается подключение всех игроков.'
        : players.some(item => !item.ready) ? `Ожидается готовность: ${players.filter(item => !item.ready).map(item => item.name).join(', ')}.`
        : host ? 'Все игроки готовы. Можно начать игру.' : 'Все игроки готовы. Ожидается запуск хостом.';
    $('ready').disabled = galaxy ? !lobbyOnline || !player || state.phase !== 'lobby' || !Number.isSafeInteger(galaxy.lobbyRevision) || draftBusy || galaxy.stage !== 'unprepared' && !(galaxy.startPending && me()?.ready) : !online || !player || state?.phase !== 'lobby';
    const retryStart = galaxy?.stage === 'awaitingAssets' && galaxy.startPending && hostStartRetryRequired && !!connection.candidate && connection.ready() && players.length > 0 && players.every(item => item.connected && item.assetReady);
    $('start').disabled = galaxy ? !lobbyOnline || !player || !host || state.phase !== 'lobby' || !!hostStart || !(galaxy.stage === 'unprepared' && !galaxy.startPending && galaxy.canPrepare && !draftBusy || retryStart) : !online || !host || state?.phase !== 'lobby';
    $('cancel-start').hidden = !galaxy?.startPending || !me()?.ready; $('cancel-start').disabled = !lobbyOnline || !galaxy?.startPending;
    $('host-controls').hidden = !host;
    for (const id of ['pause', 'speed', 'step']) { $(id).disabled = !online || !host || state?.phase !== 'running' || id === 'step' && !state?.paused; }
    const fleet = ownFleet();
    $('move-system').disabled = !canPlay() || !fleet; $('stop').disabled = !canPlay() || !fleet;
    for (const id of ['galaxy', 'reset-camera', 'focus', 'stress']) { $(id).disabled = !profile || !state?.spatial || !renderer || restoringProfile || contextLost; }
    $('camera-preset').disabled = !profile || !state?.spatial || !renderer || stressView || restoringProfile || contextLost;
    $('name').disabled = !!saved.token; $('role').disabled = !!saved.token;
    const connecting = connection.stopped === false && (connection.socket != null || connection.retryAt != null);
    $('connect').disabled = restoringProfile || connecting;
    $('disconnect').disabled = !connecting;
    $('folder').disabled = !!imageRestoreAbort || typeof window.showDirectoryPicker !== 'function'; $('files').disabled = !!imageRestoreAbort;
    $('observer-view').disabled = !online || !profile || !me()?.assetReady || observerViews(state).length === 0;
    renderEconomy();
    renderEmpireSetup(online);
    renderGalaxySetup(lobbyOnline);
    renderPlanetNotices();
    renderPlanetWindow();
    renderPlanetRefund();
    renderResearch();
}
function renderEmpireSetup(online)
{
    const setup = viewParticipantId(state) == null ? null : state?.empireSetup;
    const catalog = setup?.catalog ?? [];
    const selector = $('empire-template');
    const choices = [['', 'Шаблон не выбран'], ...catalog.map(item => [item.templateId, { economic: 'Экономический', scientific: 'Научный', military: 'Военный' }[item.templateId] ?? item.templateId])];
    if (JSON.stringify([...selector.options].map(option => [option.value, option.textContent])) !== JSON.stringify(choices))
    {
        selector.replaceChildren(...choices.map(([id, label]) => { const option = element('option', label); option.value = id; option.disabled = !id; return option; }));
    }
    const confirmed = catalog.find(item => item.templateId === setup?.selectedTemplateId);
    selector.value = confirmed?.templateId ?? '';
    selector.disabled = !(state?.galaxy ? connection.lobbyReady?.() ?? online : online) || !state?.galaxy && !profile || me()?.role !== 'player' || state?.phase !== 'lobby' || !!state?.galaxy?.startPending || !catalog.length || [...connection.pending.values()].some(command => command.type === 'selectEmpireTemplate');
    const displayDefinition = id => localized(id, 'Название недоступно');
    $('empire-template-details').textContent = confirmed ? `Власть: ${displayDefinition(confirmed.authorityId)} · Происхождение: ${displayDefinition(confirmed.originId)} · Этика: ${confirmed.ethics.map(displayDefinition).join(', ')} · Принципы: ${confirmed.civics.map(displayDefinition).join(', ')} · Признаки: ${confirmed.traits.map(displayDefinition).join(', ')}` : setup ? 'Выберите шаблон империи перед строительством.' : 'Шаблоны пока недоступны в текущем обзоре.';
    $('empire-template-details').title = confirmed ? `Источники: ${[confirmed.authorityId, confirmed.originId, ...confirmed.ethics, ...confirmed.civics, ...confirmed.traits].join(', ')}` : '';
}
$('empire-template').onchange = () =>
{
    const templateId = $('empire-template').value;
    const valid = !$('empire-template').disabled && me()?.role === 'player' && state?.phase === 'lobby' && viewParticipantId(state) === state.you && state?.empireSetup?.catalog.some(item => item.templateId === templateId);
    renderControls();
    if (!valid) { error('Шаблон недоступен. Дождитесь свежего собственного лобби и импорта.'); return; }
    send('selectEmpireTemplate', { templateId });
};
function renderGalaxySetup(online)
{
    const galaxy = state?.galaxy; $('galaxy-setup').hidden = !galaxy;
    if (!galaxy) { galaxyViewKey = ''; return; }
    if (galaxy.stage === 'unprepared') { showRequiredFiles(null); }
    const scenarios = Array.isArray(galaxy.scenarios) ? galaxy.scenarios : [];
    const key = JSON.stringify([galaxy.settings, scenarios]);
    if (key !== galaxyViewKey)
    {
        galaxyViewKey = key;
        $('galaxy-seed').value = galaxy.settings?.seed ?? '';
        const sizes = { tiny: 'Крошечная', small: 'Малая', medium: 'Средняя', large: 'Большая', huge: 'Огромная' };
        $('galaxy-scenario').replaceChildren(...scenarios.map(item => { const option = element('option', `${sizes[item.id] ?? 'Размер'} · ${item.stars ?? '?'} звёзд`); option.value = item.id; option.title = item.id; return option; }));
        $('galaxy-scenario').value = galaxy.settings?.scenarioId ?? '';
        renderGalaxyShapes();
        $('galaxy-shape').value = galaxy.settings?.shapeId ?? '';
        $('galaxy-density').value = galaxy.settings?.density == null ? '' : String(galaxy.settings.density);
        $('galaxy-nebulae').value = String(galaxy.settings?.nebulae ?? 0);
        $('galaxy-guaranteed-colonies').value = String(galaxy.settings?.guaranteedColonies ?? 0);
        $('galaxy-habitable-multiplier').value = String(galaxy.settings?.habitableMultiplier ?? 1);
        const selectedScenario = scenarios.find(item => item.id === galaxy.settings?.scenarioId);
        $('galaxy-gateway-frequency').value = String(galaxy.settings?.gatewayFrequency ?? selectedScenario?.gatewayFrequency?.default ?? 0);
        $('galaxy-wormhole-pairs').value = String(galaxy.settings?.wormholePairs ?? selectedScenario?.wormholePairs?.default ?? 0);
    }
    const editable = online && me()?.role === 'player' && me()?.isHost && state?.phase === 'lobby' && galaxy.stage === 'unprepared' && !galaxy.startPending && !galaxySettingsPending;
    for (const id of ['galaxy-seed', 'galaxy-scenario', 'galaxy-shape', 'galaxy-density', 'galaxy-nebulae', 'galaxy-guaranteed-colonies', 'galaxy-gateway-frequency', 'galaxy-wormhole-pairs', 'galaxy-habitable-multiplier', 'save-galaxy-settings']) { $(id).disabled = !editable || !scenarios.length; }
    $('prepare-galaxy').disabled = true;
    const stage = { unprepared: 'Хост выбирает общие настройки; игроки подтверждают готовность.', awaitingAssets: 'Галактика подготовлена; браузеры проверяют локальные файлы.', running: 'Галактика запущена.' }[galaxy.stage] ?? 'Состояние галактики неизвестно.';
    const content = typeof galaxy.contentProfile === 'string' ? galaxy.contentProfile : galaxy.contentProfile?.id ?? 'не указан';
    const coverage = typeof galaxy.coverage === 'string' ? galaxy.coverage : galaxy.coverage?.summary ?? '';
    let readiness = '';
    if (galaxy.stage === 'awaitingAssets')
    {
        const localMatchesServer = !state.assetProfile || manifest?.profileId === state.assetProfile.profileId && manifest?.profileDigest === state.assetProfile.profileDigest;
        readiness = !online ? 'Связь с лобби ожидает свежего снимка.'
            : !profile || !manifest ? 'Локальный набор ещё не импортирован.'
            : !localMatchesServer ? 'Импортированный набор не совпадает с текущим профилем сервера.'
            : !connection.candidate ? 'Локальный набор опубликован; кандидат ещё не сопоставлен клиентом.'
            : !connection.assetReady && !connection.assetCommandId ? 'Кандидат сопоставлен; подтверждение файлов ещё не поставлено в очередь.'
            : !connection.assetReady ? connection.sent?.has(connection.assetCommandId) ? 'Подтверждение файлов отправлено; ожидается ответ сервера.' : 'Подтверждение файлов ожидает отправки.'
            : !me()?.assetReady ? 'Подтверждение принято; ожидается серверный снимок готовности файлов.'
            : 'Локальные файлы подтверждены.';
    }
    const startStatus = hostStartRetryRequired && galaxy.startPending ? 'Автоматическое завершение отклонено; хост может повторить «Начать игру» с текущим кандидатом.' : galaxy.startPending ? 'Начало игры подтверждено; запуск завершится после проверки файлов всеми игроками.' : galaxySettingsDirty ? 'Настройки изменены; дождитесь подтверждения сервера перед готовностью.' : '';
    $('galaxy-status').textContent = `${stage} ${readiness} ${startStatus} Гарантированные миры: ${galaxy.settings?.guaranteedColonies ?? 0}. ${galaxy.systemCount == null ? '' : `Систем: ${galaxy.systemCount}.`} ${coverage}`.trim();
    $('galaxy-status').title = `Профиль: ${content}`;
}
function renderGalaxyShapes()
{
    const scenario = state?.galaxy?.scenarios?.find(item => item.id === $('galaxy-scenario').value);
    const shapes = { elliptical: 'Эллиптическая', spiral_2: 'Спиральная · 2 рукава', spiral_4: 'Спиральная · 4 рукава', ring: 'Кольцевая', bar: 'Спиральная с перемычкой' };
    $('galaxy-shape').replaceChildren(...(scenario?.shapes ?? []).map(id => { const option = element('option', shapes[id] ?? 'Название формы недоступно'); option.value = id; option.title = id; return option; }));
    const range = scenario?.hyperlaneDensity ?? (scenario?.minimumDensity != null && scenario?.maximumDensity != null ? { minimum: scenario.minimumDensity, maximum: scenario.maximumDensity } : null);
    if (range) { $('galaxy-density').min = String(range.minimum); $('galaxy-density').max = String(range.maximum); }
    const maximum = scenario?.maximumNebulae ?? 0;
    $('galaxy-nebulae').max = String(maximum);
    const count = Number($('galaxy-nebulae').value);
    $('galaxy-nebulae').value = String(Math.max(0, Math.min(maximum, Number.isFinite(count) ? Math.trunc(count) : maximum)));
    const guaranteed = Number($('galaxy-guaranteed-colonies').value);
    $('galaxy-guaranteed-colonies').max = String(scenario?.maximumGuaranteedColonies ?? 2);
    $('galaxy-guaranteed-colonies').value = String(Math.max(0, Math.min(scenario?.maximumGuaranteedColonies ?? 2,
        Number.isFinite(guaranteed) ? Math.trunc(guaranteed) : 0)));
    const gateway = Number($('galaxy-gateway-frequency').value);
    const gatewayMinimum = scenario?.gatewayFrequency?.minimum ?? 0;
    const gatewayMaximum = scenario?.gatewayFrequency?.maximum ?? 5;
    $('galaxy-gateway-frequency').min = String(gatewayMinimum);
    $('galaxy-gateway-frequency').max = String(gatewayMaximum);
    $('galaxy-gateway-frequency').value = String(Math.max(gatewayMinimum, Math.min(gatewayMaximum,
        Number.isFinite(gateway) ? gateway : scenario?.gatewayFrequency?.default ?? gatewayMinimum)));
    const wormholeRange = scenario?.wormholePairs;
    const wormholeMinimum = wormholeRange?.minimum ?? 0;
    const wormholeMaximum = wormholeRange?.maximum ?? 5;
    const wormholes = Number($('galaxy-wormhole-pairs').value);
    $('galaxy-wormhole-pairs').min = String(wormholeMinimum);
    $('galaxy-wormhole-pairs').max = String(wormholeMaximum);
    $('galaxy-wormhole-pairs').value = String(Math.max(wormholeMinimum, Math.min(wormholeMaximum,
        Number.isSafeInteger(wormholes) ? wormholes : wormholeRange?.default ?? wormholeMinimum)));
    const habitableMultiplier = Number($('galaxy-habitable-multiplier').value);
    $('galaxy-habitable-multiplier').min = '0';
    $('galaxy-habitable-multiplier').max = '5';
    $('galaxy-habitable-multiplier').step = '0.00001';
    $('galaxy-habitable-multiplier').value = String(Math.max(0, Math.min(5, Number.isFinite(habitableMultiplier) ? habitableMultiplier : 1)));
}
function lobbyRevision()
{
    const revision = state?.galaxy?.lobbyRevision;
    return Number.isSafeInteger(revision) && revision >= 0 ? revision : null;
}
function cancelHostStart(message = '')
{
    if (!hostStart) { return; }
    hostStart = null;
    if (message) { error(message); }
    renderControls();
}
function hostStartSettings()
{
    const galaxy = state?.galaxy;
    const scenarioId = $('galaxy-scenario').value; const shapeId = $('galaxy-shape').value;
    const scenario = galaxy?.scenarios?.find(item => item.id === scenarioId);
    const density = Number($('galaxy-density').value);
    const nebulae = Number($('galaxy-nebulae').value);
    if (!$('galaxy-nebulae').value || !Number.isSafeInteger(nebulae) || nebulae < 0 || nebulae > scenario?.maximumNebulae) { return null; }
    const guaranteedColonies = Number($('galaxy-guaranteed-colonies').value);
    if (!$('galaxy-guaranteed-colonies').value || !Number.isSafeInteger(guaranteedColonies) || guaranteedColonies < 0 || guaranteedColonies > (scenario?.maximumGuaranteedColonies ?? 2)) { return null; }
    const gatewayFrequency = Number($('galaxy-gateway-frequency').value);
    const gatewayRange = scenario?.gatewayFrequency;
    if (!$('galaxy-gateway-frequency').value || !Number.isFinite(gatewayFrequency) || gatewayFrequency < (gatewayRange?.minimum ?? 0) || gatewayFrequency > (gatewayRange?.maximum ?? 5) || !decimalPrecisionWithin(gatewayFrequency, 5)) { return null; }
    const wormholePairs = Number($('galaxy-wormhole-pairs').value);
    const wormholeRange = scenario?.wormholePairs;
    if (!$('galaxy-wormhole-pairs').value || !Number.isSafeInteger(wormholePairs) || wormholePairs < (wormholeRange?.minimum ?? 0) || wormholePairs > (wormholeRange?.maximum ?? 5)) { return null; }
    const habitableMultiplier = Number($('galaxy-habitable-multiplier').value);
    if (!$('galaxy-habitable-multiplier').value || !Number.isFinite(habitableMultiplier) || habitableMultiplier < 0 || habitableMultiplier > 5 || !decimalPrecisionWithin(habitableMultiplier, 5)) { return null; }
    const minimum = Number(scenario?.minimumDensity ?? scenario?.hyperlaneDensity?.minimum);
    const maximum = Number(scenario?.maximumDensity ?? scenario?.hyperlaneDensity?.maximum);
    if (!scenario || !scenario.shapes?.includes(shapeId) || !$('galaxy-density').value || !Number.isFinite(density) || !Number.isFinite(minimum) || !Number.isFinite(maximum) || density < minimum || density > maximum) { return null; }
    return { galaxySeed: $('galaxy-seed').value, galaxyScenarioId: scenarioId, galaxyShapeId: shapeId, galaxyDensity: density, galaxyNebulae: nebulae, galaxyGuaranteedColonies: guaranteedColonies, galaxyGatewayFrequency: gatewayFrequency, galaxyHabitableMultiplier: habitableMultiplier, galaxyWormholePairs: wormholePairs };
}
function confirmedGalaxySettings(requested, confirmed)
{
    const seed = requested.galaxySeed.trim() ? confirmed?.seed === requested.galaxySeed : typeof confirmed?.seed === 'string' && !!confirmed.seed.trim();
    return seed && confirmed?.scenarioId === requested.galaxyScenarioId && confirmed?.shapeId === requested.galaxyShapeId && Number(confirmed?.density) === requested.galaxyDensity && confirmed?.nebulae === requested.galaxyNebulae && (confirmed?.guaranteedColonies ?? 0) === requested.galaxyGuaranteedColonies && Number(confirmed?.gatewayFrequency) === requested.galaxyGatewayFrequency && Number(confirmed?.habitableMultiplier) === requested.galaxyHabitableMultiplier && confirmed?.wormholePairs === requested.galaxyWormholePairs;
}
function advanceGalaxySettings()
{
    const pending = galaxySettingsPending;
    if (!pending || !pending.accepted || state?.sequence < pending.minimumSequence) { return; }
    if (state?.galaxy?.stage !== 'unprepared' || !confirmedGalaxySettings(pending.settings, state.galaxy.settings))
    { galaxySettingsPending = null; error('Настройки галактики изменились до подтверждения. Выберите их снова.'); renderControls(); return; }
    galaxySettingsPending = null; galaxySettingsDirty = false; renderControls();
}
function acceptHostStartResult(result)
{
    if (galaxySettingsPending?.commandId === result.commandId)
    {
        if (!result.accepted) { galaxySettingsPending = null; renderControls(); return; }
        if (!Number.isSafeInteger(result.stateSequence) || result.stateSequence < 0) { galaxySettingsPending = null; error('Настройки не подтверждены версией состояния.'); renderControls(); return; }
        galaxySettingsPending.accepted = true; galaxySettingsPending.minimumSequence = result.stateSequence;
        advanceGalaxySettings(); return;
    }
    const flow = hostStart;
    if (!flow || ![flow.initialCommandId, flow.finalCommandId].includes(result.commandId)) { return; }
    if (!result.accepted) { if (result.commandId === flow.finalCommandId) { hostStartRetryRequired = true; hostStartRetryRevision = flow.revision; } cancelHostStart(); return; }
    if (!Number.isSafeInteger(result.stateSequence) || result.stateSequence < 0) { cancelHostStart('Запуск остановлен: ответ сервера не содержит версии состояния.'); return; }
    if (result.commandId === flow.initialCommandId) { flow.initialAccepted = true; flow.minimumSequence = result.stateSequence; }
    else { flow.finalAccepted = true; flow.minimumSequence = result.stateSequence; }
    renderControls(); advanceHostStart();
}
function advanceHostStart()
{
    const galaxy = state?.galaxy;
    if (state?.phase === 'running') { if (hostStart || hostStartRetryRequired) { hostStart = null; hostStartRetryRequired = false; hostStartRetryRevision = null; renderControls(); } return; }
    if (!galaxy || state?.phase !== 'lobby' || !galaxy.startPending || !Number.isSafeInteger(galaxy.lobbyRevision))
    { if (hostStart && galaxy?.lobbyRevision !== hostStart.revision) { cancelHostStart('Запуск остановлен: версия лобби изменилась.'); } return; }
    if (hostStartRetryRequired && hostStartRetryRevision !== galaxy.lobbyRevision) { hostStartRetryRequired = false; hostStartRetryRevision = null; }
    if (!hostStart && !hostStartRetryRequired && me()?.isHost && me()?.role === 'player')
    {
        hostStart = { sessionId: state.sessionId, participantId: state.you, revision: galaxy.lobbyRevision, initialCommandId: null, initialAccepted: true, finalCommandId: null, finalAccepted: false, minimumSequence: state.sequence, fingerprint: galaxy.worldFingerprint, candidateVersion: null };
    }
    const flow = hostStart;
    if (!flow) { return; }
    if (state.sessionId !== flow.sessionId || state.you !== flow.participantId || galaxy.lobbyRevision !== flow.revision || me()?.role !== 'player' || !me()?.isHost || !active || !fresh || contextAwaitingSnapshot || !(connection.lobbyReady?.() ?? connection.ready()))
    { cancelHostStart('Автоматическое завершение запуска остановлено: связь, права или версия лобби изменились.'); return; }
    if (!flow.initialAccepted || state.sequence < flow.minimumSequence || galaxy.stage !== 'awaitingAssets' || !galaxy.worldFingerprint) { return; }
    if (flow.fingerprint && flow.fingerprint !== galaxy.worldFingerprint || flow.candidateVersion != null && flow.candidateVersion !== connection.candidateVersion)
    { cancelHostStart('Автоматическое завершение запуска остановлено: кандидат галактики изменился.'); return; }
    flow.fingerprint ??= galaxy.worldFingerprint;
    if (!connection.candidate || connection.candidate.galaxyWorldFingerprint !== flow.fingerprint || !connection.ready() || !me()?.assetReady) { return; }
    flow.candidateVersion ??= connection.candidateVersion;
    const players = state.participants.filter(item => item.role === 'player');
    if (players.length === 0 || players.some(item => !item.connected || !item.assetReady)) { return; }
    if (flow.finalCommandId) { return; }
    flow.finalCommandId = send('start', { expectedLobbyRevision: flow.revision, ...connection.candidate });
    if (!flow.finalCommandId) { cancelHostStart(); } else { renderControls(); }
}
function submitGalaxySettings()
{
    clearGalaxySettingsTimer();
    if (state?.galaxy?.stage !== 'unprepared' || !me()?.isHost || galaxySettingsPending) { return; }
    const settings = hostStartSettings();
    if (!settings) { galaxySettingsDirty = true; error('Выберите поддерживаемые размер, форму и плотность галактики.'); renderControls(); return; }
    const commandId = send('setGalaxySettings', settings);
    if (!commandId) { galaxySettingsDirty = true; renderControls(); return; }
    galaxySettingsDirty = true; galaxySettingsPending = { commandId, settings, accepted: false, minimumSequence: -1 };
    renderControls();
}
function clearGalaxySettingsTimer()
{
    if (galaxySettingsTimer !== null) { clearTimeout(galaxySettingsTimer); galaxySettingsTimer = null; }
}
function scheduleGalaxySettings()
{
    galaxySettingsDirty = true; renderControls(); clearGalaxySettingsTimer();
    galaxySettingsTimer = setTimeout(() => { galaxySettingsTimer = null; if (active && fresh && state?.phase === 'lobby' && state.galaxy?.stage === 'unprepared' && me()?.isHost) { submitGalaxySettings(); } }, 350);
}
$('galaxy-scenario').onchange = () => { renderGalaxyShapes(); submitGalaxySettings(); };
for (const id of ['galaxy-seed', 'galaxy-shape', 'galaxy-density', 'galaxy-nebulae', 'galaxy-guaranteed-colonies', 'galaxy-gateway-frequency', 'galaxy-wormhole-pairs', 'galaxy-habitable-multiplier'])
{
    $(id).oninput = scheduleGalaxySettings;
    $(id).onchange = submitGalaxySettings;
}
$('galaxy-settings').onsubmit = event =>
{
    event.preventDefault();
    if (!$('save-galaxy-settings').disabled) { submitGalaxySettings(); }
};
$('prepare-galaxy').onclick = () => {};
function renderState()
{
    localizationIssues.clear();
    $('clock').textContent = `${state.date.year}.${String(state.date.month).padStart(2, '0')}.${String(state.date.day).padStart(2, '0')}`;
    $('pause').textContent = state.paused ? 'Продолжить' : 'Пауза'; $('speed').value = String(state.speedDaysPerSecond);
    $('ready').textContent = me()?.ready ? 'Снять готовность' : 'Готов';
    if (state.lastError) { error(state.lastError); }
    $('participants').replaceChildren(...state.participants.map(participant =>
    {
        const node = element('li'); node.className = 'participant-row';
        node.append(element('strong', `${participant.name}${participant.isHost ? ' · хост' : ''}`), element('span', { player: 'Игрок', observer: 'Наблюдатель', pendingObserver: 'Ожидает допуска' }[participant.role] ?? 'Участник'), element('span', participant.connected ? 'Подключён' : participant.afk ? 'Нет активности' : 'Отключён'), element('span', participant.role === 'player' ? participant.ready ? 'Готов' : 'Не готов' : 'Просмотр'));
        if (me()?.isHost && participant.id !== state.you)
        {
            if (participant.role === 'pendingObserver') { const button = element('button', 'Разрешить наблюдение'); button.onclick = () => send('approveObserver', { targetParticipantId: participant.id }); node.append(button); }
            else if (participant.role === 'player' && participant.connected) { const button = element('button', 'Передать хоста'); button.onclick = () => send('transferHost', { targetParticipantId: participant.id }); node.append(button); }
            if (state.phase === 'lobby')
            {
                const button = element('button', 'Исключить');
                button.disabled = !fresh || !(connection.lobbyReady?.() ?? connection.ready());
                button.onclick = () => { if (!button.disabled && fresh && me()?.isHost && state?.phase === 'lobby') { send('kickParticipant', { targetParticipantId: participant.id }); } };
                node.append(button);
            }
        }
        return node;
    }));
    const key = JSON.stringify(state.catalog);
    if (!state.galaxy && key !== catalogKey)
    {
        catalogKey = key;
        for (const [id, values] of [['traits', state.catalog.traits]])
        {
            $(id).replaceChildren(...values.map(value => { const label = element('label', localized(value.localizationKey ?? value.id, value.name)); const input = document.createElement('input'); input.type = 'checkbox'; input.value = value.id; label.prepend(input); return label; }));
        }
    }
    const spatial = state.spatial;
    renderOutliner();
    const previous = $('destination').value;
    $('destination').replaceChildren(...(spatial?.systems ?? []).map(system => { const option = element('option', system.name); option.value = system.id; return option; }));
    if ([...$('destination').options].some(option => option.value === previous)) { $('destination').value = previous; }
    // Порядок main.gui 4.5.2; состав вложенных групп — interface/resource_groups.
    const resourceOrder = ['energy', 'minerals', 'food', 'consumer_goods', 'biomass', 'alloys', 'trade', 'volatile_motes', 'exotic_gases', 'rare_crystals', 'sr_living_metal', 'sr_zro', 'sr_dark_matter', 'nanites', 'minor_artifacts', 'astral_threads', 'entropy_crystals', 'influence', 'unity', 'physics_research', 'society_research', 'engineering_research'];
    const rank = id => { const index = resourceOrder.indexOf(id); return index < 0 ? resourceOrder.length : index; };
    const resources = resourceItems(state).sort((left, right) => rank(left.id) - rank(right.id));
    const resourceNumber = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 });
    const compactResourceValue = (value, signed = false) =>
    {
        const exact = String(value); const numeric = Number(exact.replace(/[\s\u00a0\u202f]/g, '').replace(',', '.'));
        if (!Number.isFinite(numeric)) { return exact; }
        const magnitude = Math.abs(numeric);
        const compact = magnitude >= 1_000_000 ? `${resourceNumber.format(numeric / 1_000_000)}млн` : magnitude >= 1_000 ? `${resourceNumber.format(numeric / 1_000)}тыс` : resourceNumber.format(numeric);
        return signed && numeric > 0 ? `+${compact}` : compact;
    };
    const resourceMetadata = new Map((state.development?.colonies ?? []).filter(colony => colony.ownerParticipantId === viewParticipantId(state)).flatMap(colony => colony.economy?.resources ?? []).map(resource => [resource.resourceId, resource]));
    const rareOpen = [...$('resources').children].find(node => node.className === 'rare-resources')?.open ?? false;
    const primaryResources = new Set(['energy', 'minerals', 'food', 'consumer_goods', 'biomass', 'alloys', 'trade', 'influence', 'unity', 'physics_research', 'society_research', 'engineering_research']);
    const resourceNodes = resources.map(({ id, value, net }) =>
    {
        const metadata = resourceMetadata.get(id);
        const name = localized(metadata?.localizationKey ?? id, names[id] ?? id); const path = `gfx/interface/icons/resources/${id}.dds`;
        const planetIconKey = `resource.${id}`;
        const hasIcon = iconNodes.has(path) || !!planetImages && profile?.planetImages?.has(planetIconKey);
        const node = document.createElement('span'); node.className = 'resource'; node.dataset.resourceId = id; node.dataset.iconMissing = String(!hasIcon);
        const exactFlow = net == null ? 'Месячный поток недоступен.' : `Месячный поток: ${net}.`;
        node.title = `${name}: запас ${value}; ${net == null ? 'поток за месяц недоступен' : `поток ${net}/мес`}`;
        node.setAttribute('role', 'group'); node.setAttribute('aria-label', `${name}. Запас: ${value}. ${exactFlow}`);
        const text = document.createElement('span'); text.className = 'resource-values';
        text.append(element('strong', compactResourceValue(value)));
        const flow = element('small', net == null ? '—' : compactResourceValue(net, true));
        flow.dataset.flow = net == null ? 'unavailable' : net.startsWith('-') ? 'negative' : net.startsWith('+') ? 'positive' : 'zero';
        flow.title = net == null ? 'Месячный поток недоступен' : `Точный месячный поток: ${net}`;
        text.append(flow);
        if (hasIcon)
        {
            if (iconNodes.has(path)) { appendIcon(node, path); } else { appendPlanetImage(node, planetIconKey); }
            const icon = node.children[0];
            if (['canvas', 'img'].includes(icon?.tagName?.toLowerCase()))
            { icon.setAttribute('aria-hidden', 'true'); icon.style.marginRight = '0'; }
        }
        else
        {
            const fallback = element('span', name); fallback.className = 'resource-label'; fallback.setAttribute('aria-hidden', 'true'); node.append(fallback);
        }
        node.append(text); return node;
    });
    const primary = resourceNodes.filter(node => primaryResources.has(node.dataset.resourceId));
    const rare = resourceNodes.filter(node => !primaryResources.has(node.dataset.resourceId));
    if (rare.length)
    {
        const details = document.createElement('details'); details.className = 'rare-resources'; details.open = rareOpen;
        details.append(element('summary', `Редкие · ${rare.length}`));
        const values = element('div'); values.className = 'rare-resource-values'; values.append(...rare); details.append(values); primary.push(details);
    }
    $('resources').replaceChildren(...(resourceNodes.length ? primary : [element('span', viewParticipantId(state) != null ? 'Ресурсы пока недоступны' : 'Общий обзор · экономика в лаборатории')]));
    renderEconomy(); renderIdentity(); renderSelection(); renderControls(); renderPlanetWindow(); renderResearch();
}
function renderEconomy()
{
    if (!state) { return; }
    const context = perspectiveKey(state);
    const editable = canPlay() && me()?.assetReady === true && viewParticipantId(state) === state.you;
    const key = JSON.stringify([state.month, state.planets, state.catalog.jobs, context, editable]);
    if (key === economyKey) { return; }
    const root = $('economy-content'); const focused = document.activeElement;
    const draft = editable && root.contains(focused) && focused.type === 'number' && !focused.disabled && focused.dataset.economyContext === context ? { planetKey: focused.dataset.planetKey, jobId: focused.dataset.jobId, owner: focused.dataset.owner, value: focused.value, receivedValue: focused.dataset.receivedValue, start: focused.selectionStart, end: focused.selectionEnd, direction: focused.selectionDirection } : null;
    let restore = null;
    economyKey = key;
    const nodes = [element('p', `Закрытых экономических месяцев: ${state.month}. Первый отчёт появится на 2200.02.01.`)];
    for (const planet of state.planets)
    {
        nodes.push(element('h3', `${planet.name} · Pop ${planet.population} · Workforce ${planet.workforce}`));
        for (const job of state.catalog.jobs)
        {
            const row = element('div', job.localizationKey ? localized(job.localizationKey, job.name) : job.name); row.className = 'economy-row';
            const input = document.createElement('input'); input.type = 'number'; input.min = 0; input.max = 6; input.step = .5; input.value = String(planet.jobs[job.id] ?? '');
            input.disabled = !editable || planet.ownerId !== state.you || !Object.hasOwn(planet.jobs, job.id);
            input.dataset.owner = String(planet.ownerId);
            input.dataset.planetKey = JSON.stringify(planet.id); input.dataset.jobId = job.id; input.dataset.economyContext = context; input.dataset.receivedValue = input.value;
            if (draft && !input.disabled && draft.planetKey === input.dataset.planetKey && draft.jobId === job.id && draft.owner === input.dataset.owner)
            {
                if (draft.value !== draft.receivedValue) { input.value = draft.value; }
                restore = input;
            }
            const button = element('button', 'Вакансии'); button.disabled = input.disabled; button.onclick = () => send('setJobs', { planetId: planet.id, job: job.id, count: Number(input.value) });
            button.dataset.owner = String(planet.ownerId);
            const allocation = planet.report?.allocated[job.id]; const details = element('span', allocation ? `WF ${allocation.workforce} + ${allocation.bonusWorkforce} = ${allocation.effectiveWorkforce}` : 'Без отчёта');
            row.append(input, button, details); nodes.push(row);
        }
        if (planet.report)
        {
            const table = document.createElement('table'); const head = document.createElement('tr');
            for (const label of ['Ресурс', 'Выпуск', 'Профессии', 'Здания', 'Общий расход', 'Чистый поток']) { head.append(element('th', label)); } table.append(head);
            for (const resource of Object.keys(planet.balances))
            {
                const row = document.createElement('tr'); row.append(element('td', localized(resource, names[resource] ?? resource)));
                for (const field of ['produced', 'jobUpkeep', 'buildingUpkeep', 'upkeep', 'net']) { row.append(element('td', number.format(planet.report[field][resource]))); } table.append(row);
            }
            nodes.push(table, element('p', `Отчёт месяца ${planet.reportMonth}; безработная Workforce ${planet.report.unemployedWorkforce}`));
        }
    }
    root.replaceChildren(...nodes);
    if (restore)
    {
        restore.focus({ preventScroll: true });
        if (draft.start != null && draft.end != null) { restore.setSelectionRange(draft.start, draft.end, draft.direction); }
    }
}
$('configure').onsubmit = event => { event.preventDefault(); if ($('configure').hidden || $('save-config').disabled) { return; } send('configure', { traits: [...$('traits').querySelectorAll('input:checked')].map(input => input.value), buildings: [] }); };
$('ready').onclick = () => { if (!$('ready').disabled) { send('ready', { ready: !me()?.ready, ...(state?.galaxy ? { expectedLobbyRevision: lobbyRevision() } : {}) }); } };
$('start').onclick = () =>
{
    if ($('start').disabled) { return; }
    if (!state?.galaxy) { send('start'); return; }
    const revision = lobbyRevision();
    if (revision == null) { error('Дождитесь версии общего лобби.'); return; }
    if (state.galaxy.startPending)
    {
        hostStartRetryRequired = false; hostStartRetryRevision = null;
        hostStart = { sessionId: state.sessionId, participantId: state.you, revision, initialCommandId: null, initialAccepted: true, finalCommandId: null, finalAccepted: false, minimumSequence: state.sequence, fingerprint: state.galaxy.worldFingerprint, candidateVersion: connection.candidateVersion };
        advanceHostStart(); return;
    }
    hostStartRetryRequired = false; hostStartRetryRevision = null;
    hostStart = { sessionId: state.sessionId, participantId: state.you, revision, initialCommandId: null, initialAccepted: false, finalCommandId: null, finalAccepted: false, minimumSequence: -1, fingerprint: null, candidateVersion: null };
    hostStart.initialCommandId = send('start', { expectedLobbyRevision: revision });
    if (!hostStart.initialCommandId) { cancelHostStart(); } else { renderControls(); }
};
$('cancel-start').onclick = () => { if (!$('cancel-start').disabled) { send('ready', { ready: false, expectedLobbyRevision: lobbyRevision() }); } };
$('pause').onclick = () => send('pause', { paused: !state.paused }); $('speed').onchange = () => send('speed', { speed: Number($('speed').value) }); $('step').onclick = () => send('stepDay');
$('move-system').onclick = () => systemOrder($('destination').value);
$('observer-view').onchange = () =>
{
    const targetParticipantId = Number($('observer-view').value);
    $('observer-view').value = String(viewParticipantId(state) ?? 0);
    if ($('observer-view').disabled || !observerViews(state).some(([id]) => id === targetParticipantId)) { error('Перспектива недоступна. Дождитесь подключения и допуска наблюдателя.'); return; }
    send('observerView', { targetParticipantId });
};
$('stop').onclick = () => { if (canPlay() && ownFleet()) { send('stopFleet', { fleetId: commandFleetId }); } };
$('galaxy').onclick = () => { if (state?.galaxy?.stage === 'running') { sendGalaxyInspection(''); } renderer?.showGalaxy(); currentSystem = null; stressView = false; renderIdentity(); renderControls(); };
$('stress').onclick = () => { renderer?.showStress200(); currentSystem = null; stressView = true; renderIdentity(); renderControls(); };
$('camera-preset').onchange = () =>
{
    try { renderer.setCameraPreset($('camera-preset').value); renderIdentity(); }
    catch (failure) { error(failure.message); }
};
$('reset-camera').onclick = () => { try { renderer?.resetCamera(); renderIdentity(); } catch (failure) { error(failure.message); } };
$('focus').onclick = () => renderer?.focusSelection();
document.addEventListener('keydown', event =>
{
    if (event.key !== 'Escape' || document.querySelector('dialog[open]') || event.target.closest('input,textarea,select,[contenteditable="true"]')) { return; }
    if (selected || commandFleetId) { closeSelection(); }
    else { $('galaxy').click(); }
});
window.addEventListener('resize', () => { renderer?.resize(); syncLabelInsets(); });
function frame(now)
{
    if (!active) { return; }
    try
    {
        renderer?.render(now); frames.push(now); while (frames.length > 200) { frames.shift(); }
        if (renderer && now - metricAt > 1000)
        {
            metricAt = now; const durations = frames.slice(1).map((time, index) => time - frames[index]).sort((a, b) => a - b);
            const stats = renderer.stats(); const fps = frames.length > 1 ? (frames.length - 1) * 1000 / (frames.at(-1) - frames[0]) : 0;
            frameMetrics = `${fps.toFixed(1)} FPS · p95 ${(durations[Math.floor(durations.length * .95)] ?? 0).toFixed(1)} ms · ${JSON.stringify(stats)}`;
        }
        renderMetrics();
    }
    catch (failure) { error(failure.message); }
    frameId = requestAnimationFrame(frame);
}
function renderMetrics()
{
    const speed = Number.isFinite(state?.speedDaysPerSecond) ? `${number.format(state.speedDaysPerSecond)} дн/с` : 'недоступно';
    const tick = Number.isFinite(state?.lastTickMilliseconds) ? `${number.format(state.lastTickMilliseconds)} мс` : 'недоступно';
    const text = [`Задано: ${speed}`, simulationRateText(connection.simulationDiagnostic()), `Последний тик: ${tick}`, frameMetrics].filter(Boolean).join(' · ');
    if ($('metrics').textContent !== text) { $('metrics').textContent = text; }
}
window.addEventListener('pagehide', () => { active = false; hostStart = null; clearGalaxySettingsTimer(); galaxySettingsPending = null; galaxySettingsDirty = false; galaxyViewKey = ''; ++importGeneration; ++restorationId; ++researchRenderGeneration; restoringProfile = false; imageImportAbort?.abort(); imageRestoreAbort?.abort(); importer.cancel(); connection.disconnect(); cancelAnimationFrame(frameId); renderer?.dispose(); renderer = null; uiSprites?.dispose(); uiSprites = null; planetImages?.dispose(); planetImages = null; researchImages?.dispose(); researchImages = null; planetViewKey = ''; $('planet-content').replaceChildren(); });
window.addEventListener('pageshow', async event =>
{
    if (!event.persisted) { return; }
    active = true; restoringProfile = !!profile;
    hint('Страница восстановлена. Подключитесь вручную.'); frameId = requestAnimationFrame(frame);
    const generation = ++restorationId; const restoring = profile;
    const priorRestore = restoreSettled; let releaseRestore; restoreSettled = new Promise(resolve => { releaseRestore = resolve; });
    imageRestoreAbort?.abort(); const imageAbort = new AbortController(); imageRestoreAbort = imageAbort;
    renderControls();
    let restoredUi = null; let restoredPlanet = null;
    try
    {
        await priorRestore;
        await importSettled;
        if (!active || generation !== restorationId || profile !== restoring) { return; }
        if (profile)
        {
            const retained = (renderer?.stats().convertedBytes ?? 0) + (uiSprites?.decodedBytes ?? 0) + (planetImages?.decodedBytes ?? 0) + (researchImages?.decodedBytes ?? 0);
            restoredUi = await prepareUiSprites(restoring, { maxDecodedBytes: MAX_MEMORY - restoring.decodedBytes - retained - RESEARCH_IMAGE_RESERVE });
            if (!active || generation !== restorationId || profile !== restoring) { return; }
            if (restoring.planetImages) { restoredPlanet = await preparePlanetImages(restoring, { signal: imageAbort.signal, maxDecodedBytes: Math.min(16 * 1048576, MAX_MEMORY - restoring.decodedBytes - retained - restoredUi.decodedBytes - RESEARCH_IMAGE_RESERVE) }); }
            if (!active || generation !== restorationId || profile !== restoring) { return; }
        }
        if (!renderer) { createRenderer(); }
        if (profile)
        {
            renderer.setProfile(profile, { maxConvertedBytes: MAX_MEMORY - profile.decodedBytes - (restoredUi?.decodedBytes ?? 0) - (restoredPlanet?.decodedBytes ?? 0) - (uiSprites?.decodedBytes ?? 0) - (planetImages?.decodedBytes ?? 0) - (researchImages?.decodedBytes ?? 0) - RESEARCH_IMAGE_RESERVE });
            uiSprites?.dispose(); uiSprites = restoredUi; restoredUi = null; uiSprites.apply();
            planetImages?.dispose(); planetImages = restoredPlanet; restoredPlanet = null; planetViewKey = '';
            researchImages?.dispose(); researchImages = acceptedSource ? makeResearchImageStore(acceptedSource) : null;
        }
        if (state) { renderer.applySnapshot(state, { receivedAt: performance.now(), resetInterpolation: true }); }
        renderPlanetWindow();
        restoringProfile = false;
        if (profile && manifest) { connection.setProfile(manifest.profileId, manifest.profileDigest); }
    }
    catch (failure) { if (active && generation === restorationId) { restoringProfile = !!profile; error(failure.message); } }
    finally { restoredUi?.dispose(); restoredPlanet?.dispose(); releaseRestore(); if (imageRestoreAbort === imageAbort) { imageRestoreAbort = null; } }
    renderIdentity(); renderControls();
});
try { createRenderer(); } catch (failure) { error(failure.message); }
frameId = requestAnimationFrame(frame); renderIdentity(); renderControls();
if (!saved.token) { openPanel('setup-dialog'); }
