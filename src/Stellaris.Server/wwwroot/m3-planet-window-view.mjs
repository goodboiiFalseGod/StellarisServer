import { planetStartupGraph, resolvePlanetInspection } from './m3-planet-panel.mjs';
import { constructionOperations } from './m3-construction-controls.mjs';

const contexts = new WeakMap();
const children = node => Array.from(node?.children ?? []);
const all = node => children(node).flatMap(child => [child, ...all(child)]);
const has = (node, name) => String(node?.className ?? '').split(/\s+/).includes(name);
const find = (node, name) => all(node).find(child => has(child, name));
const format = value => Number.isFinite(value) ? new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value) : '—';
const workforceSummary = economy =>
{
    const jobs = economy?.jobs;
    const base = economy?.baseWorkforce;
    if (!Array.isArray(jobs) || jobs.length === 0 || !Number.isFinite(base) || base < 0) return null;
    const ids = new Set();
    let capacity = 0, assigned = 0;
    for (const job of jobs)
    {
        if (typeof job?.jobId !== 'string' || !job.jobId || ids.has(job.jobId)
            || !Number.isFinite(job.capacity) || job.capacity < 0
            || !Number.isFinite(job.assigned) || job.assigned < 0) return null;
        ids.add(job.jobId);
        capacity += job.capacity;
        assigned += job.assigned;
        if (!Number.isFinite(capacity) || !Number.isFinite(assigned)) return null;
    }
    const sumTolerance = Number.EPSILON * Math.max(1, base, assigned) * jobs.length;
    if (assigned - base > sumTolerance) return null;
    return { capacity, unemployed: Math.max(0, base - assigned) };
};
const cityKind = id => ['district_city', 'district_hive', 'district_nexus'].includes(id);
const colors = { district_city: '#6aaaca', district_hive: '#6aaaca', district_nexus: '#6aaaca', district_generator: '#b4b657', district_mining: '#b5746a', district_farming: '#79aa68' };
const districtPoolView = development =>
{
    const pool = development?.districtPool;
    if (pool?.policyId !== 'ordinary-district-slot-pool-v1' || !['capacity', 'installed', 'reserved', 'free'].every(key => Number.isSafeInteger(pool[key]) && pool[key] >= 0) || pool.free !== pool.capacity - pool.installed - pool.reserved || !Array.isArray(development.installed) || !Array.isArray(development.queue)) { return null; }
    const installed = development.installed.filter(item => item.kind === 'district').length;
    const pending = development.queue.filter(item => item.kind === 'district' && item.status === 'pending');
    if (installed !== pool.installed || pending.length !== pool.reserved) { return null; }
    const counts = entries =>
    {
        const result = new Map();
        for (const entry of entries) { result.set(entry.definitionId, (result.get(entry.definitionId) ?? 0) + 1); }
        return result;
    };
    return { ...pool, installedByDefinition: counts(development.installed.filter(item => item.kind === 'district')), pendingByDefinition: counts(pending) };
};
const districtTypePoolView = (development, definitionId) =>
{
    if (development?.districtTypePools == null) { return { status: 'absent' }; }
    const pools = development.districtTypePools;
    if (!Array.isArray(pools)) { return { status: 'invalid' }; }
    const matches = pools.filter(pool => pool?.definitionId === definitionId);
    if (matches.length !== 1) { return { status: 'invalid' }; }
    const pool = matches[0];
    const validMembers = value => Array.isArray(value?.memberDefinitionIds) && value.memberDefinitionIds.length > 0 && value.memberDefinitionIds.every(id => typeof id === 'string' && !!id) && new Set(value.memberDefinitionIds).size === value.memberDefinitionIds.length && value.memberDefinitionIds.includes(value.definitionId);
    if (typeof pool.sharedPoolId !== 'string' || !pool.sharedPoolId || pool.policyId !== 'ordinary-source-district-type-pool-v1' || typeof pool.definitionId !== 'string' || !pool.definitionId || !validMembers(pool)) { return { status: 'invalid' }; }
    if (pool.uncapped !== true && pool.uncapped !== false) { return { status: 'invalid' }; }
    if (pool.uncapped)
    {
        if (pool.capacity !== null || pool.free !== null) { return { status: 'invalid' }; }
    }
    else if (!Number.isSafeInteger(pool.capacity) || pool.capacity < 0 || !Number.isSafeInteger(pool.free) || pool.free < 0 || !Number.isSafeInteger(pool.capacity - pool.installed - pool.reserved) || pool.free !== Math.max(0, pool.capacity - pool.installed - pool.reserved)) { return { status: 'invalid' }; }
    if (!Number.isSafeInteger(pool.installed) || pool.installed < 0 || !Number.isSafeInteger(pool.reserved) || pool.reserved < 0 || !Array.isArray(development.installed) || !Array.isArray(development.queue)) { return { status: 'invalid' }; }
    const shared = pools.filter(item => item?.sharedPoolId === pool.sharedPoolId);
    const definitions = new Set();
    const membersKey = [...pool.memberDefinitionIds].sort().join('\u0000');
    for (const item of shared)
    {
        if (typeof item.definitionId !== 'string' || !validMembers(item) || definitions.has(item.definitionId) || item.policyId !== pool.policyId || item.uncapped !== pool.uncapped || item.capacity !== pool.capacity || item.installed !== pool.installed || item.reserved !== pool.reserved || item.free !== pool.free || [...item.memberDefinitionIds].sort().join('\u0000') !== membersKey) { return { status: 'invalid' }; }
        definitions.add(item.definitionId);
    }
    const members = new Set(pool.memberDefinitionIds);
    const installed = development.installed.filter(item => item.kind === 'district' && members.has(item.definitionId)).length;
    const reserved = development.queue.filter(item => item.kind === 'district' && item.status === 'pending' && members.has(item.definitionId)).length;
    if (installed !== pool.installed || reserved !== pool.reserved) { return { status: 'invalid' }; }
    return { status: 'valid', pool };
};

// Дерево и классы непосредственно из одобренного planet-menu-452.html.
// Только полученный граф и callbacks; renderer не создаёт игровые сущности.
export function renderPlanetWindowView(root, model, options = {})
{
    const { document, tab = 'infrastructure', inspection = null, onInspect = () => {},
        appendImage = () => false, appendResourceIcon = () => false, resolveImageKey = () => null,
        resolveLocalization = (key, fallback) => fallback ?? key, expandedJobs = new Set() } = options;
    const node = (tag, className, text) => { const item = document.createElement(tag); if (className) item.className = className; if (text != null) item.textContent = text; return item; };
    const button = (text, className, action, label = text) => { const item = node('button', className, text); item.type = 'button'; if (label) item.setAttribute('aria-label', label); item.onclick = action; return item; };
    const name = (entry, id = entry?.definitionId ?? entry?.zoneId ?? entry?.jobId) => resolveLocalization(entry?.pluralLocalizationKey ?? entry?.localizationKey ?? id, entry?.name ?? id ?? 'Недоступно');
    const image = (parent, key, className) =>
    {
        const frame = node('span', className);
        if (!key || !appendImage(frame, key)) { frame.dataset.missingImage = 'true'; frame.setAttribute('aria-label', 'Изображение недоступно'); }
        for (const child of children(frame)) child.alt = '';
        parent.append(frame); return frame;
    };
    const empty = (parent, text) => parent.append(node('div', 'pm-empty', text));
    const jobProductionBonuses = (parent, entry, enabled = true) =>
    {
        const targets = {
            planet_miners_minerals_produces_add: ['miner', 'minerals'],
            planet_farmers_food_produces_add: ['farmer', 'food'],
            planet_technician_energy_produces_add: ['technician', 'energy'],
            planet_physicists_physics_research_produces_add: ['physicist', 'physics_research'],
            planet_biologists_society_research_produces_add: ['biologist', 'society_research'],
            planet_engineers_engineering_research_produces_add: ['engineer', 'engineering_research']
        };
        for (const term of entry?.sourceRecipe ?? [])
        {
            const target = targets[term.targetId];
            if (!target || term.kind !== 'PlanetModifier' || !Number.isFinite(term.value) || term.conditionDisposition === 'InactiveInOrdinaryContext') continue;
            const [jobId, resourceId] = target, job = economy?.jobDefinitions?.find(item => item.jobId === jobId) ?? economy?.jobs?.find(item => item.jobId === jobId);
            const label = node('small', 'pm-building-bonus', (enabled ? 'К базовому выпуску' : 'При включении: к базовому выпуску') + ' · ' + name(job, jobId));
            resources(label, { [resourceId]: term.value });
            if (term.conditionDisposition === 'RequiresRuntimeContext') label.append(node('span', null, 'При выполнении условий'));
            parent.append(label);
        }
    };
    const disabled = (label, reason) => { const item = button(label); item.disabled = true; if (reason) item.title = reason; return item; };
    const previous = contexts.get(root);
    const identity = model ? JSON.stringify([model.id, model.ownerId, tab]) : null;
    const same = previous?.identity === identity && identity != null;
    const inspectionIdentity = JSON.stringify(inspection);
    const savedScroll = same ? ['pm-main', 'pm-side', ...(previous.inspection === inspectionIdentity ? ['pm-catalog-list', 'pm-building-body', 'pm-district-body'] : [])].map(key => [key, find(root, key)?.scrollTop]) : [];
    const active = document.activeElement;
    const focusIndex = all(find(root, 'planet-context')).filter(child => child.tagName === 'BUTTON').indexOf(active);
    const focusKey = active?.dataset?.planetActionIdentity ?? active?.dataset?.inspectionIdentity;
    root.replaceChildren();
    if (!model) { empty(root, 'Колония недоступна в текущем обзоре.'); contexts.delete(root); return; }
    contexts.set(root, { identity, inspection: inspectionIdentity }); root.dataset.referencePlanetMenu = 'true';
    const development = model.development;
    const economy = development?.economy;
    const workforce = workforceSummary(economy);
    const districtPool = districtPoolView(development);
    const sequentialQueue = development?.queuePolicy?.policyId === 'ordinary-sequential-v1' && development.queuePolicy.activeCapacity === 1;
    const liveWorkEstimate = order =>
    {
        if (order?.workEstimatePolicyId !== 'accepted-daily-work-v1' || !['active', 'waiting', 'paused'].includes(order?.executionState)) return null;
        const status = order.executionState === 'active' ? 'Строится' : order.executionState === 'waiting' ? 'Ожидает' : 'Приостановлено';
        const validEstimate = Number.isFinite(order.dailyWork) && order.dailyWork > 0 && Number.isFinite(order.remainingWorkDays) && order.remainingWorkDays >= 0;
        return { status, estimate: validEstimate && order.executionState !== 'paused' ? `◷ ${format(order.remainingWorkDays)} дн. работы` : 'Срок неизвестен' };
    };
    const graph = planetStartupGraph(development);
    const queue = (development?.queue ?? []).filter(order => !['completed', 'cancelled'].includes(order.status));
    const operations = constructionOperations(model);
    const zoneReplacementOrder = instanceId =>
    {
        const zones = (development?.startupZones ?? []).filter(zone => zone.instanceId === instanceId);
        if (zones.length !== 1) return null;
        const zone = zones[0], slots = development.slots.filter(slot => slot.kind === 'zone' && slot.slotId === zone.slotInstanceId && slot.zoneInstanceId === instanceId);
        const orders = queue.filter(order => order.kind === 'zone' && order.status === 'pending' && order.slotId === zone.slotInstanceId);
        if (slots.length !== 1 || orders.length !== 1 || slots[0].reservedByOrderId !== orders[0].orderId) return null;
        const order = orders[0], replacement = order.zoneReplacement;
        if (!replacement || replacement.zoneInstanceId !== instanceId || replacement.sourceDefinitionId !== zone.zoneId || order.definitionId === zone.zoneId
            || !Array.isArray(replacement.removedBuildings) || new Set(replacement.removedBuildings.map(item => item.slotId)).size !== replacement.removedBuildings.length) return null;
        if (!replacement.removedBuildings.every(item => development.slots.filter(slot => slot.kind === 'building' && slot.slotId === item.slotId && slot.zoneInstanceId === instanceId).length === 1
            && development.installed.filter(building => building.kind === 'building' && building.slotId === item.slotId && building.definitionId === item.definitionId).length === 1)) return null;
        return order;
    };
    const buildingReplacementOrder = slot =>
    {
        if (!slot || slot.kind !== 'building' || (development?.slots ?? []).filter(item => item.slotId === slot.slotId).length !== 1) return null;
        const zones = (development?.startupZones ?? []).filter(zone => zone.instanceId === slot.zoneInstanceId);
        if (zones.length !== 1) return null;
        const zone = zones[0];
        if (![zone.instanceId, zone.districtInstanceId, zone.slotInstanceId, zone.zoneId].every(value => typeof value === 'string' && value.length > 0)
            || graph.districts.filter(district => district.instanceId === zone.districtInstanceId).length !== 1
            || slot.zoneDefinitionId != null && slot.zoneDefinitionId !== zone.zoneId) return null;
        const runtimeZoneSlots = development.slots.filter(item => item.kind === 'zone' && item.slotId === zone.slotInstanceId);
        if (runtimeZoneSlots.length > 0 && (runtimeZoneSlots.length !== 1 || runtimeZoneSlots[0].zoneInstanceId !== zone.instanceId)) return null;
        const orders = queue.filter(order => order.kind === 'building' && order.status === 'pending' && order.slotId === slot.slotId);
        if (orders.length !== 1) return null;
        const order = orders[0], replacement = order.buildingReplacement;
        if (typeof order.orderId !== 'string' || !order.orderId || typeof order.definitionId !== 'string' || !order.definitionId
            || !replacement || typeof replacement.sourceDefinitionId !== 'string' || !replacement.sourceDefinitionId
            || typeof replacement.zoneInstanceId !== 'string' || !replacement.zoneInstanceId
            || order.definitionId === replacement.sourceDefinitionId || replacement.zoneInstanceId !== slot.zoneInstanceId
            || slot.reservedByOrderId !== order.orderId || order.upgradeFromDefinitionId != null || order.zoneInstanceId != null || order.zoneReplacement != null) return null;
        const installed = development.installed.filter(item => item.kind === 'building' && item.slotId === slot.slotId);
        if (installed.length !== 1 || installed[0].definitionId !== replacement.sourceDefinitionId || slot.installedDefinitionId !== replacement.sourceDefinitionId) return null;
        return { order, installed: installed[0] };
    };
    const current = resolvePlanetInspection(model, inspection);
    const inspect = (text, value, className) => { const item = button(text, className, () => onInspect(value)); item.dataset.inspectionIdentity = JSON.stringify(value); return item; };
    const command = (target, label, className) =>
    {
        const container = node('div'); options.appendConstruction?.(container, target);
        const item = all(container).find(child => child.tagName === 'BUTTON');
        if (!item) { const unavailable = disabled(label, 'Действие недоступно в текущем состоянии'); unavailable.className = className ?? ''; return unavailable; }
        item.textContent = label; item.className = className ?? ''; return item;
    };
    const resources = (parent, values, { missing = false, kind = 'resource' } = {}) =>
    {
        const row = node('small', 'pm-cost');
        for (const [id, value] of Object.entries(values ?? {}))
        {
            const item = node('span', 'pm-resource'); item.dataset.resourceId = id;
            const job = kind === 'job' ? economy?.jobDefinitions?.find(entry => entry.jobId === id) ?? economy?.jobs?.find(entry => entry.jobId === id) : null;
            const resourceName = kind === 'job' ? name(job, id) + ', WF' : resolveLocalization(id, id);
            const stock = economy?.resources?.find(resource => resource.resourceId === id)?.stock;
            const insufficient = missing && Number.isFinite(stock) && value > stock;
            if (insufficient) item.className += ' is-missing';
            item.title = resourceName; item.setAttribute('aria-label', resourceName + ': ' + format(value) + (insufficient ? ', недостаточно' : ''));
            const icon = node('span', 'pm-resource-icon');
            const loaded = kind === 'job' ? appendImage(icon, resolveImageKey('job', id)) : appendResourceIcon(icon, id);
            if (!loaded) { icon.dataset.missingImage = 'true'; icon.setAttribute('aria-label', resourceName); }
            for (const child of children(icon)) child.alt = '';
            item.append(icon, node('span', null, format(value))); row.append(item);
        }
        parent.append(row); return row;
    };
    const progress = (parent, order, slot = false) =>
    {
        const bar = node('progress', slot ? 'pm-slot-progress' : null);
        if (Number.isFinite(order.requiredWork) && order.requiredWork > 0 && Number.isFinite(order.completedWork)) { bar.max = order.requiredWork; bar.value = order.completedWork; }
        bar.setAttribute('aria-label', name(order) + ': ' + format(order.completedWork) + ' / ' + format(order.requiredWork)); parent.append(bar);
    };
    const head = node('div', 'pm-head'), landscape = node('div', 'pm-landscape'), summary = node('aside', 'pm-summary');
    const layers = typeof development?.planetClass === 'string'
        ? ['sky', 'l01', 'l02', 'l03', 'l04'].map(layer => resolveImageKey('environment', development.planetClass + '.' + layer)) : [];
    if (layers.length === 5 && layers.every(Boolean))
    {
        const environment = node('div', 'pm-environment'); environment.setAttribute('aria-hidden', 'true');
        for (const key of layers) image(environment, key, 'pm-environment-layer');
        if (children(environment).every(layer => layer.dataset.missingImage !== 'true')) landscape.append(environment);
    }
    landscape.append(node('span', 'pm-world-kind', development?.planetClass ? resolveLocalization(development.planetClass, development.planetClass) : 'Класс планеты недоступен'));
    const governor = node('div', 'pm-governor'); governor.append(node('span', 'pm-governor-portrait'), node('span', null, 'Губернатор: недоступно')); landscape.append(governor);
    const orb = node('span', 'pm-planet-orb'); orb.setAttribute('aria-label', 'Изображение планеты недоступно'); orb.dataset.missingImage = 'true';
    summary.append(orb, node('strong', null, 'Сводка колонии'), node('div', null, 'Пригодность: —'), node('div', null, 'Размер: ' + format(development?.planetSize)), node('div', null, 'Специализация: —'));
    const summaryActions = node('div', 'pm-summary-actions'); summaryActions.append(disabled('Решения', 'Система решений ещё не предоставлена сервером'), disabled('⚙', 'Система автоматизации ещё не предоставлена сервером')); summaryActions.children[1].setAttribute('aria-label', 'Автоматизация колонии');
    summary.append(summaryActions); head.append(landscape, summary);
    const stats = node('div', 'pm-stats');
    const prototypeWorkforceTitle = 'Показатель текущего прототипного распределения; WF — единица Workforce, не число профессий или Pops.';
    for (const [label, value, title] of [['Стабильность', null], ['Население', economy?.population ?? model.economy?.population], ['Преступность', null], ['Жильё', null], ['Блага', null],
        ['Безработица, WF', workforce?.unemployed ?? null, prototypeWorkforceTitle], ['Рабочие места, WF', workforce?.capacity ?? null, prototypeWorkforceTitle]])
    { const item = node('span', null, label + ' '); item.append(node('b', null, format(value))); if (title) item.title = title; else if (!Number.isFinite(value)) item.title = 'Показатель ещё не предоставлен сервером'; stats.append(item); }
    const workspace = node('div', 'pm-workspace'), main = node('main', 'pm-main'), side = node('aside', 'pm-side');
    main.id = 'planet-page'; main.setAttribute('role', 'tabpanel'); main.setAttribute('aria-labelledby', 'planet-tab-' + tab); workspace.append(main, side); root.append(head, stats, workspace);
    const buildingSlot = (slot, building) =>
    {
        const slotOrders = slot ? queue.filter(order => order.slotId === slot.slotId && (slot.reservedByOrderId == null || slot.reservedByOrderId === order.orderId)) : [];
        const order = slotOrders[0];
        const hasReplacementMetadata = slotOrders.some(item => item.buildingReplacement != null);
        const buildingReplacement = buildingReplacementOrder(slot);
        const installed = slot && development.installed.find(item => item.kind === 'building' && item.slotId === slot.slotId);
        const definitionId = installed?.definitionId ?? building?.definitionId ?? slot?.installedDefinitionId, slotId = slot?.slotId;
        const reserved = slot?.reservedByOrderId != null || !!order;
        const free = slot?.kind === 'building' && !definitionId && !reserved && Array.isArray(slot.gates) && slot.gates.length === 0;
        const target = slotId ? installed ? { kind: 'installed', slotId, definitionId } : { kind: 'slot', slotId } : null;
        const item = target ? inspect('', target, 'pm-slot') : disabled('', 'Точное место здания ещё не предоставлено сервером'); item.className = 'pm-slot';
        item.dataset.slotId = slotId ?? building?.slotInstanceId ?? '';
        const title = installed || building ? name(installed ?? building) : order ? name(order) : definitionId ? name({ definitionId }) : reserved ? 'Место зарезервировано' : free ? 'Свободный слот здания' : 'Место здания недоступно';
        const enabledState = installed?.enabled === true ? 'Включено' : installed?.enabled === false ? 'Выключено' : null;
        let accessibleTitle = enabledState ? `${title} · ${enabledState}` : title;
        item.setAttribute('aria-label', accessibleTitle); item.title = accessibleTitle; item.dataset.tooltip = accessibleTitle;
        const replacement = slot && zoneReplacementOrder(slot.zoneInstanceId);
        if (installed && replacement?.zoneReplacement.removedBuildings.some(item => item.slotId === slotId && item.definitionId === installed.definitionId))
        {
            item.className += ' is-pending-removal';
            const warning = node('span', 'pm-slot-removal-badge', '!'); warning.setAttribute('aria-hidden', 'true'); item.append(warning);
            const explanation = accessibleTitle + ' · Будет удалено при замене зоны без возврата';
            item.setAttribute('aria-label', explanation); item.title = explanation; item.dataset.tooltip = explanation;
        }
        const catalog = order && development.catalog.find(entry => entry.definitionId === order.definitionId && entry.kind === 'building');
        if (buildingReplacement)
        {
            const { order: replacementOrder, installed: source } = buildingReplacement;
            const target = catalog ?? replacementOrder;
            const sourceName = name(source), targetName = name(target);
            const replacementTitle = `${sourceName} заменяется на ${targetName}${enabledState ? ' · ' + enabledState : ''}`;
            accessibleTitle = replacementTitle;
            item.setAttribute('aria-label', accessibleTitle); item.title = accessibleTitle; item.dataset.tooltip = accessibleTitle;
            const split = node('span', 'pm-slot-replacement'), oldArt = node('span'), targetArt = node('span');
            image(oldArt, source.imageDefinitionKey ?? resolveImageKey('building', source.definitionId), 'pm-slot-art');
            image(targetArt, target.imageDefinitionKey ?? resolveImageKey('building', replacementOrder.definitionId), 'pm-slot-art');
            split.append(oldArt, targetArt); item.append(split);
        }
        else
        {
            const currentBuilding = installed ?? building;
            const currentArt = currentBuilding?.imageDefinitionKey ?? (currentBuilding?.definitionId ? resolveImageKey('building', currentBuilding.definitionId) : null);
            image(item, currentArt ?? (!hasReplacementMetadata && order ? catalog?.imageDefinitionKey ?? resolveImageKey(order.kind, order.definitionId) : null), 'pm-slot-art');
        }
        if (installed?.enabled === false) { item.className += ' is-disabled-building'; item.append(node('span', 'pm-slot-disabled-badge', 'Ⅱ')); }
        item.dataset.slotState = definitionId ? 'occupied' : reserved ? 'reserved' : free ? 'free' : 'unavailable';
        if (!definitionId && !order) item.textContent = free ? '+' : reserved ? '…' : '—';
        if (buildingReplacement) { item.className += ' is-replacing'; progress(item, buildingReplacement.order, true); }
        else if (order) { item.className += order.upgradeFromDefinitionId ? ' is-upgrading' : ' is-building'; if (order.upgradeFromDefinitionId) item.append(node('span', 'pm-slot-upgrade-badge', '↑')); progress(item, order, true); }
        if (current?.slotId === slotId) item.className += ' is-selected';
        return item;
    };
    const usedSlots = new Set();
    const slotGroup = (zone, base) =>
    {
        const slots = node('div', base ? 'pm-slots pm-base-slots' : 'pm-slots'); slots.setAttribute('aria-label', base ? 'Базовые здания' : 'Слоты зоны ' + name(zone));
        for (const slot of (development?.slots ?? []).filter(slot => slot.kind === 'building' && slot.zoneInstanceId === zone.instanceId))
        { usedSlots.add(slot.slotId); slots.append(buildingSlot(slot, zone.buildings.find(building => building.slotId === slot.slotId || building.slotInstanceId === slot.slotId))); }
        for (const building of zone.buildings.filter(building => !usedSlots.has(building.slotId ?? building.slotInstanceId)))
        { const id = building.slotId ?? building.slotInstanceId; if (id) usedSlots.add(id); slots.append(buildingSlot(null, building)); }
        return slots;
    };
    const zoneCard = (zone, district, city) =>
    {
        const base = zone.zoneId === 'zone_default', section = node('section', base ? null : city ? 'pm-city-zone pm-zone-surface' : 'pm-resource-zone pm-zone-surface');
        if (base) section.append(node('span', 'pm-zone-title', 'Базовые здания · ' + format((development?.slots ?? []).filter(slot => slot.kind === 'building' && slot.zoneInstanceId === zone.instanceId).length) + ' слотов'));
        else
        {
            const value = { kind: 'zoneCatalog', districtInstanceId: district.instanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId };
            const title = inspect(name(zone) + ' · зона', value, 'pm-zone-title pm-zone-open');
            const order = zoneReplacementOrder(zone.instanceId);
            if (order)
            {
                const target = development.catalog.find(entry => entry.kind === 'zone' && entry.definitionId === order.definitionId) ?? order;
                const split = node('span', 'pm-zone-replacement'), old = node('span'), next = node('span');
                image(old, zone.imageDefinitionKey ?? resolveImageKey('zone', zone.zoneId), 'pm-slot-art');
                image(next, target.imageDefinitionKey ?? resolveImageKey('zone', order.definitionId), 'pm-slot-art');
                split.append(old, next); title.replaceChildren(split); title.className += ' is-replacing'; progress(title, order, true);
                title.setAttribute('aria-label', name(zone) + ' заменяется на ' + name(target));
            }
            section.append(title);
            section.tabIndex = 0; section.setAttribute('role', 'group'); section.setAttribute('aria-label', 'Зона ' + name(zone));
            section.onclick = event =>
            {
                for (let target = event?.target; target && target !== section; target = target.parentNode)
                    if (['BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'A', 'SUMMARY'].includes(target.tagName)) return;
                onInspect(value);
            };
            section.onkeydown = event => { if (event.target === section && ['Enter', ' '].includes(event.key)) { event.preventDefault(); onInspect(value); } };
        }
        section.dataset.zoneInstance = zone.instanceId; section.append(slotGroup(zone, base)); return section;
    };
    const ruralZoneSlotCard = (slot, district) =>
    {
        const card = node('section', 'pm-resource-zone pm-zone-surface'); card.dataset.zoneSlotId = slot.slotId;
        card.setAttribute('role', 'group');
        const orderMatches = queue.filter(order => order.kind === 'zone' && order.slotId === slot.slotId
            && (slot.reservedByOrderId == null || order.orderId === slot.reservedByOrderId));
        if (slot.reservedByOrderId != null || orderMatches.length > 0)
        {
            if (orderMatches.length !== 1)
            {
                card.setAttribute('aria-label', 'Состояние строительства зоны неизвестно');
                card.append(disabled('Строительство зоны', 'Состояние заказа зоны неизвестно'));
                return card;
            }
            const order = orderMatches[0];
            card.setAttribute('aria-label', 'Строится зона');
            const marker = node('span', 'pm-slot-art', '⚒'); marker.setAttribute('aria-hidden', 'true'); card.append(marker);
            card.append(node('span', 'pm-zone-title', 'Зона строится'));
            progress(card, order, true);
            return card;
        }
        if (slot.installedDefinitionId != null)
        {
            card.setAttribute('aria-label', 'Данные установленной зоны недоступны');
            card.append(disabled('Зона установлена', 'Связь завершённой зоны с её местом недоступна'));
            return card;
        }
        const gates = Array.isArray(slot.gates) ? slot.gates : [];
        if (gates.length)
        {
            const keys = Array.isArray(slot.requirementLocalizationKeys) ? slot.requirementLocalizationKeys.filter(key => typeof key === 'string' && key.length > 0) : [];
            const unknown = keys.length === 0 || gates.some(gate => gate?.dependencyId === 'zone-slot-condition-unresolved');
            const requirements = keys.map(key => resolveLocalization(key, key));
            const label = unknown ? 'Условия разблокировки неизвестны' : requirements.join(' · ');
            card.setAttribute('aria-label', label);
            card.append(disabled('Зона недоступна', label));
            card.append(node('small', 'pm-district-copy', label));
            return card;
        }
        card.setAttribute('aria-label', 'Построить зону в районе ' + name(district));
        card.append(inspect('＋ Построить зону', { kind: 'slot', slotId: slot.slotId }, 'pm-zone-title pm-zone-open'));
        return card;
    };
    const districtCard = district =>
    {
        const city = cityKind(district.definitionId), card = node('section', city ? 'pm-city' : 'pm-rural');
        card.dataset.districtInstance = district.instanceId; card.dataset.districtDefinition = district.definitionId; card.setAttribute('style', '--district-color:' + (colors[district.definitionId] ?? '#6aaaca'));
        const isResourceDistrict = ['district_generator', 'district_mining', 'district_farming'].includes(district.definitionId);
        const districtIsUnique = graph.districts.filter(item => item.instanceId === district.instanceId).length === 1;
        const zoneSlots = isResourceDistrict && districtIsUnique ? (development?.slots ?? []).filter(slot => slot.kind === 'zone' && slot.districtInstanceId === district.instanceId
            && typeof slot.slotId === 'string' && slot.slotId.length > 0
            && development.slots.filter(candidate => candidate.slotId === slot.slotId).length === 1) : [];
        const heading = node('div', 'pm-district-heading'), title = node('span', 'pm-district-name', name(district)), artKey = district.imageDefinitionKey ?? resolveImageKey('district', district.definitionId);
        const operation = operations.find(item => item.type === 'enqueueConstruction' && item.definitionId === district.definitionId);
        const improve = operation ? command({ definitionId: operation.definitionId, slotId: operation.slotId }, 'Улучшить') : disabled('Улучшить');
        if (!operation) improve.title = ''; improve.setAttribute('aria-label', 'Улучшить: ' + name(district)); improve.dataset.improve = district.definitionId;
        if (city)
        {
            const label = node('div', 'pm-district-title'); image(label, artKey, 'pm-district-art'); label.append(title);
            const actions = node('div', 'pm-district-buttons'); actions.append(improve); heading.append(label, actions); card.append(heading);
            const grid = node('div', 'pm-city-grid'), zones = node('div', 'pm-city-zones');
            for (const zone of district.zones) (zone.zoneId === 'zone_default' ? grid : zones).append(zoneCard(zone, district, true));
            grid.append(zones); card.append(grid);
        }
        else
        {
            image(card, artKey, 'pm-district-art'); heading.append(title); card.append(heading, improve);
            for (const zone of district.zones) card.append(zoneCard(zone, district, false));
            const representedSlotIds = new Set(district.zones.map(zone => zone.slotInstanceId).filter(id => typeof id === 'string'));
            for (const slot of zoneSlots)
            {
                if (representedSlotIds.has(slot.slotId)) continue;
                card.append(ruralZoneSlotCard(slot, district));
            }
            if (!district.zones.length && !zoneSlots.length) empty(card, 'Данные специализации недоступны');
        }
        const unique = graph.districts.filter(item => item.definitionId === district.definitionId).length === 1;
        const installed = development.installed.filter(item => item.kind === 'district' && item.definitionId === district.definitionId);
        const level = unique && installed.length ? installed.length : district.level, pending = unique ? queue.filter(order => order.kind === 'district' && order.definitionId === district.definitionId).length : null;
        const uniqueInstance = typeof district.instanceId === 'string' && !!district.instanceId && graph.districts.filter(item => item.instanceId === district.instanceId).length === 1;
        const cityPool = districtPool && district.definitionId === 'district_city' && uniqueInstance && graph.districts.filter(item => item.definitionId === 'district_city').length === 1 ? districtPool : null;
        const cityInstalled = cityPool ? cityPool.installedByDefinition.get('district_city') ?? 0 : null;
        const cityPending = cityPool ? cityPool.pendingByDefinition.get('district_city') ?? 0 : null;
        const cityLimit = cityPool ? cityPool.capacity - (cityPool.installed - cityInstalled) - (cityPool.reserved - cityPending) : null;
        const typePool = !city ? districtTypePoolView(development, district.definitionId) : null;
        const ruralPool = !city && typePool?.status === 'valid' ? typePool.pool : null;
        const unknownRuralPool = !city && typePool?.status === 'invalid';
        const shownLevel = cityPool ? cityInstalled : ruralPool ? ruralPool.installed : unknownRuralPool ? null : level;
        const shownPending = cityPool ? cityPending : ruralPool ? ruralPool.reserved : unknownRuralPool ? null : pending;
        const shownLimit = cityPool ? format(cityLimit) : ruralPool ? ruralPool.uncapped ? '∞' : format(ruralPool.capacity) : '—';
        const meter = node('div', 'pm-meter');
        const capacity = cityPool ? cityLimit : ruralPool && !ruralPool.uncapped ? ruralPool.capacity : null;
        const occupied = shownLevel + (shownPending ?? 0);
        const knownCapacity = Number.isSafeInteger(capacity) && capacity >= occupied;
        const cells = knownCapacity ? capacity : occupied;
        meter.dataset.capacityKnown = String(knownCapacity);
        meter.setAttribute('aria-label', 'Занято: ' + format(shownLevel) + ' · строится: ' + format(shownPending) + ' · свободно: ' + (knownCapacity ? format(capacity - occupied) : '—'));
        meter.title = meter.getAttribute('aria-label');
        if (Number.isSafeInteger(shownLevel) && shownLevel >= 0 && (cityPool || ruralPool || unique))
            for (let i = 0; i < Math.min(128, cells); i++) meter.append(node('span', i < shownLevel ? 'filled' : i < occupied ? 'queued' : 'free'));
        card.append(meter, node('span', 'pm-district-count', format(shownLevel) + (shownPending ? ' + ' + shownPending : '') + ' / ' + shownLimit)); return card;
    };
    const report = parent =>
    {
        const section = node('section', 'pm-other'); section.append(node('h4', null, 'Закрытый месяц: ' + format(economy?.reportMonth ?? model.economy?.reportMonth)));
        for (const [field, label] of [['produced', 'Производство'], ['jobUpkeep', 'Профессии'], ['buildingUpkeep', 'Здания'], ['districtUpkeep', 'Районы'], ['zoneUpkeep', 'Зоны'], ['net', 'Итог за месяц']])
        {
            const row = node('div', 'pm-flow'); row.append(node('span', null, label));
            if (economy?.reportMonth > 0) resources(row, Object.fromEntries(economy.resources.map(value => [value.resourceId, value[field]]).filter(([,value]) => Number.isFinite(value))));
            else if (!economy && model.economy?.report?.[field]) resources(row, model.economy.report[field]); else empty(row, 'Отчёт недоступен');
            section.append(row);
        }
        parent.append(section);
    };
    if (tab === 'infrastructure')
    {
        const heading = node('div', 'pm-section-head'); heading.append(node('h4', null, 'Инфраструктура колонии'));
        const poolLabel = node('span', 'pm-district-pool', 'Свободных районов: ' + format(districtPool?.free));
        const poolDescription = districtPool ? `Районы: ${districtPool.installed}${districtPool.reserved ? ' + ' + districtPool.reserved : ''} / ${districtPool.capacity} · свободно: ${districtPool.free}` : 'Районы: данные недоступны';
        poolLabel.title = poolDescription; poolLabel.setAttribute('aria-label', poolDescription); heading.append(poolLabel); main.append(heading);
        if (!development) { empty(main, 'Развитие колонии пока недоступно: сервер не активировал подтверждённые данные строительства.'); report(main); }
        else
        {
            for (const district of graph.districts.filter(item => cityKind(item.definitionId))) main.append(districtCard(district));
            const rural = node('div', 'pm-rural-row'); for (const district of graph.districts.filter(item => !cityKind(item.definitionId))) rural.append(districtCard(district)); main.append(rural);
            const unplaced = development.slots.filter(slot => slot.kind === 'building' && !usedSlots.has(slot.slotId));
            if (unplaced.length) { const other = node('details', 'pm-other-slots'); other.append(node('summary', null, 'Места без подтверждённой связи с районом')); const slots = node('div', 'pm-slots'); for (const slot of unplaced) slots.append(buildingSlot(slot)); other.append(slots); main.append(other); }
        }
    }
    else if (tab === 'features')
    {
        const other = node('div', 'pm-other'), section = node('section'), heading = node('div', 'pm-section-head'); heading.append(node('h4', null, 'Особенности планеты'), node('span', null, development ? graph.features.filter(item => !item.clearable).length + ' особенности · ' + graph.features.filter(item => item.clearable).length + ' препятствия' : '— особенности · — препятствия')); section.append(heading);
        const grid = node('div', 'pm-feature-grid');
        for (const feature of graph.features)
        {
            const card = node('div', 'pm-feature'); card.dataset.featureInstance = feature.instanceId; image(card, feature.imageDefinitionKey ?? resolveImageKey(feature.clearable ? 'blocker' : 'feature', feature.definitionId), 'pm-feature-art');
            const copy = node('span', null, name(feature)); copy.append(node('small', null, feature.clearable ? 'Препятствие' : 'Пассивная особенность'));
            if (feature.descriptionKey) copy.title = resolveLocalization(feature.descriptionKey, '');
            if (feature.clearable) copy.append(command({ instanceId: feature.instanceId }, 'Расчистить')); card.append(copy); grid.append(card);
        }
        if (!graph.features.length) empty(grid, development ? 'Полученный список особенностей пуст' : 'Данные особенностей недоступны');
        section.append(grid); other.append(section);
        const population = node('section'), populationHeading = node('div', 'pm-section-head'); populationHeading.append(node('h4', null, 'Население и развитие'), disabled('Переселить', 'Сервер ещё не предоставляет переселение')); population.append(populationHeading);
        for (const [icon, label, detail] of [['♟', 'Основной вид', 'Рост населения · — / мес.'], ['⚙', 'Роботы', 'Сборка поселений · — / мес.'], ['↔', 'Миграция', 'Притяжение иммиграции']])
        { const row = node('div', 'pm-pop-row'), copy = node('span', null, label); copy.append(node('small', null, detail)); row.append(node('span', 'pm-row-icon', icon), copy, node('span', null, '—')); population.append(row); }
        other.append(population); main.append(other);
        const populationCard = node('div', 'pm-side-card'); populationCard.append(node('strong', null, 'Население'), node('div', null, format(economy?.population ?? model.economy?.population)), node('div', null, 'Рост: — / мес.'), node('div', null, 'Жильё: —')); side.append(populationCard);
        const sector = node('div', 'pm-side-card'); sector.append(node('strong', null, 'Сектор'), node('div', null, '—'), disabled('Назначить столицей', 'Сервер ещё не предоставляет управление сектором')); side.append(sector);
        const ascension = node('div', 'pm-side-card'), ascensionProgress = node('progress'); ascensionProgress.setAttribute('aria-label', 'Уровень вознесения неизвестен'); ascension.append(node('strong', null, 'Вознесение планеты'), node('div', null, 'Уровень — / —'), ascensionProgress, disabled('Возвысить', 'Сервер ещё не предоставляет вознесение')); side.append(ascension);
    }
    else if (tab === 'jobs')
    {
        const other = node('div', 'pm-other'), section = node('section'), heading = node('div', 'pm-section-head'); heading.append(node('h4', null, 'Рабочая сила и профессии'), node('span', null, 'Производство · Содержание')); section.append(heading);
        const groups = new Map();
        for (const job of economy?.jobs ?? development?.jobs ?? model.economy?.jobs ?? []) { if (Number.isFinite(job.capacity) && job.capacity <= 0 && !(job.assigned > 0)) continue; const key = job.sourceSocialClassId ?? job.categoryId ?? 'jobs'; if (!groups.has(key)) groups.set(key, []); groups.get(key).push(job); }
        const ranks = { ruler: 0, specialist: 1, worker: 2 };
        for (const [category, jobs] of [...groups].sort(([a], [b]) => (ranks[a] ?? 3) - (ranks[b] ?? 3)))
        {
            const classKeys = { worker: 'pop_cat_worker', specialist: 'pop_cat_specialist', ruler: 'pop_cat_ruler', civilian: 'pop_cat_civilian' };
            section.append(node('h4', null, resolveLocalization(classKeys[category] ?? jobs[0].categoryLocalizationKey ?? category, category)));
            for (const job of jobs)
            {
                const row = node('div', 'pm-job-row'); image(row, job.imageDefinitionKey ?? resolveImageKey('job', job.jobId), 'pm-row-icon');
                const title = node('span'); title.append(node('b', null, name(job)), node('small', null, format(job.assigned) + ' / ' + format(job.capacity) + ' WF'));
                title.title = 'Обычная ' + format(job.assigned) + ' · бонусная ' + format(job.bonusWorkforce) + ' · эффективная ' + format(job.effectiveWorkforce) + ' · лимит ' + format(job.limit);
                row.setAttribute('aria-label', name(job) + ' · ' + title.title); row.append(title);
                const bar = node('span', 'pm-job-bar');
                const ratio = Number.isFinite(job.capacity) && job.capacity > 0 && Number.isFinite(job.assigned) && job.assigned >= 0 ? Math.min(1, job.assigned / job.capacity) : null;
                for (let i = 0; i < 8; i++)
                {
                    const segment = node('i', ratio != null && i < Math.round(ratio * 8) ? 'on' : null); segment.setAttribute('aria-hidden', 'true'); bar.append(segment);
                }
                bar.setAttribute('role', 'img'); bar.setAttribute('aria-label', 'Workforce ' + format(job.assigned) + ' / ' + format(job.capacity)); row.append(bar);
                if (economy) options.appendWorkforce?.(row, job); else options.appendVacancies?.(row, job);
                for (const [field, caption] of [['produced', 'Производство'], ['upkeep', 'Содержание']])
                {
                    const value = node('span', 'pm-value' + (field === 'produced' ? ' good' : ''));
                    value.title = caption + ' · закрытый месяц ' + format(economy?.reportMonth);
                    value.setAttribute('aria-label', value.title);
                    if (!(economy?.reportMonth > 0) || job[field] == null) value.textContent = '—';
                    else
                    {
                        const amounts = Object.fromEntries(Object.entries(job[field]).filter(([, amount]) => Number.isFinite(amount) && amount !== 0));
                        if (Object.keys(amounts).length) resources(value, amounts); else value.textContent = '0';
                    }
                    row.append(value);
                }
                section.append(row);
            }
        }
        if (!groups.size) empty(section, 'Данные профессий недоступны');
        other.append(section); main.append(other);
        const workforceCard = node('div', 'pm-side-card');
        const occupied = workforce ? (economy?.baseWorkforce ?? model.economy?.workforce) - workforce.unemployed : null;
        workforceCard.append(node('strong', null, 'Сводка рабочей силы'), node('div', null, 'Занято, WF: ' + format(occupied)), node('div', null, 'Свободно, WF: ' + format(workforce ? workforce.capacity - occupied : null)), node('div', null, 'Безработные, WF: ' + format(workforce?.unemployed))); side.append(workforceCard);
        const classes = node('div', 'pm-side-card'); classes.append(node('strong', null, 'Классы'));
        for (const [category, jobs] of [...groups].sort(([a], [b]) => (ranks[a] ?? 3) - (ranks[b] ?? 3)))
        {
            const total = jobs.every(job => Number.isFinite(job.assigned)) ? jobs.reduce((sum, job) => sum + job.assigned, 0) : null;
            classes.append(node('div', null, resolveLocalization({ worker: 'pop_cat_worker', specialist: 'pop_cat_specialist', ruler: 'pop_cat_ruler', civilian: 'pop_cat_civilian' }[category] ?? category, category) + ', WF: ' + format(total)));
        }
        if (!groups.size) classes.append(node('div', null, '—')); side.append(classes);
        const balance = node('div', 'pm-side-card'); balance.append(node('strong', null, 'Баланс'));
        if (economy?.reportMonth > 0) resources(balance, Object.fromEntries(economy.resources.filter(item => Number.isFinite(item.net) && item.net !== 0).map(item => [item.resourceId, item.net]))); else balance.append(node('div', null, '—')); side.append(balance);
    }
    else if (tab === 'armies')
    {
        const other = node('div', 'pm-other'), armies = node('section'), heading = node('div', 'pm-section-head'); heading.append(node('h4', null, 'Планетарные армии'), disabled('Нанять армию', 'Сервер ещё не предоставляет найм армий')); armies.append(heading); empty(armies, 'Сведения об армиях недоступны');
        const orbital = node('section'); orbital.append(node('h4', null, 'Орбитальные армии')); empty(orbital, 'Сведения об орбитальных армиях недоступны'); other.append(armies, orbital); main.append(other);
        const defense = node('div', 'pm-side-card'); defense.append(node('strong', null, 'Оборона планеты'), node('div', null, 'Сила: —'), node('div', null, 'Урон от бомбардировки: —')); side.append(defense);
        const garrison = node('div', 'pm-side-card'); garrison.append(node('strong', null, 'Гарнизон'), node('div', null, 'Армии: —'), node('div', null, 'Генерал: —')); side.append(garrison);
    }
    else if (tab === 'holdings')
    {
        const other = node('div', 'pm-other'), holdings = node('section'), heading = node('div', 'pm-section-head'); heading.append(node('h4', null, 'Внешние владения'), node('span', null, '— / —')); holdings.append(heading); empty(holdings, 'Сведения о владениях и правах управления недоступны'); other.append(holdings); main.append(other);
        const summary = node('div', 'pm-side-card'); summary.append(node('strong', null, 'Владения'), node('div', null, 'Занято: — / —')); side.append(summary);
        const owners = node('div', 'pm-side-card'); owners.append(node('strong', null, 'Владельцы'), node('div', null, '—')); side.append(owners);
    }
    if (tab === 'infrastructure')
    {
        const flow = node('div', 'pm-flow'); flow.append(node('strong', null, 'Производство'));
        if (economy?.reportMonth > 0) resources(flow, Object.fromEntries(economy.resources.filter(item => Number.isFinite(item.produced) && item.produced !== 0).map(item => [item.resourceId, item.produced]))); else empty(flow, 'Отчёт закрытого месяца недоступен'); side.append(flow);
        const deficit = node('div', 'pm-flow'); deficit.append(node('span', null, 'Дефицит'));
        if (economy?.reportMonth > 0) { const values = economy.resources.filter(item => Number.isFinite(item.net) && item.net < 0); if (values.length) resources(deficit, Object.fromEntries(values.map(item => [item.resourceId, item.net]))); else empty(deficit, 'Нет'); }
        else empty(deficit, '—'); side.append(deficit);
    }
    if (tab === 'infrastructure' && development)
    {
        const section = node('section', 'pm-queue'); section.append(node('h4', null, 'Очередь строительства'));
        const orders = node('div', 'pm-orders'); orders.setAttribute('aria-live', 'polite');
        for (const [index, order] of queue.entries())
        {
            const row = node('div', 'pm-order'); row.dataset.orderId = order.orderId;
            const estimate = liveWorkEstimate(order);
            row.append(node('div', null, name(order)), node('small', null, estimate?.status ?? (sequentialQueue ? index === 0 ? 'Строится' : 'Ожидает' : 'В очереди')),
                node('small', null, 'Работа: ' + format(order.completedWork) + ' / ' + format(order.requiredWork)));
            if (estimate) row.append(node('small', null, order.executionState === 'waiting' && estimate.estimate !== 'Срок неизвестен' ? 'Собственная работа: ' + estimate.estimate : estimate.estimate));
            progress(row, order);
            const controls = node('div', 'pm-order-controls');
            if (index > 0)
            {
                for (const direction of ['up', 'down'])
                {
                    const operation = operations.find(item => item.type === 'reorderConstruction' && item.orderId === order.orderId && item.direction === direction);
                    if (!operation) { continue; }
                    const label = direction === 'up' ? 'Переместить выше' : 'Переместить ниже';
                    const move = command({ orderId: order.orderId, direction }, direction === 'up' ? '↑' : '↓', 'pm-queue-move');
                    move.setAttribute('aria-label', label); move.title = label; controls.append(move);
                }
            }
            controls.append(command({ orderId: order.orderId }, 'Отменить'));
            row.append(controls); orders.append(row);
        }
        if (!queue.length) empty(orders, 'Очередь пуста'); section.append(orders); side.append(section);
    }
    if (current && tab === 'infrastructure')
    {
        const installedMode = current.kind === 'installed', replacementMode = current.kind === 'zoneReplacementCatalog', zoneMode = current.kind === 'zoneCatalog' || replacementMode;
        const slot = !zoneMode ? development.slots.find(item => item.slotId === current.slotId) : null;
        const pane = node('aside', 'planet-context ' + (installedMode ? 'pm-building' : zoneMode && !replacementMode ? 'pm-district-panel' : 'pm-catalog'));
        const header = node('header', installedMode || zoneMode ? 'pm-building-head' : 'pm-catalog-head');
        header.append(node('span', null, installedMode ? 'Сведения о здании' : replacementMode ? 'Заменить специализацию' : zoneMode ? 'Сведения о зоне' : slot?.kind === 'zone' ? 'Построить зону' : 'Построить здание'), inspect('×', null));
        const body = node('div', installedMode ? 'pm-building-body' : zoneMode && !replacementMode ? 'pm-district-body' : 'pm-catalog-list'); pane.append(header, body);
        if (zoneMode)
        {
            const zone = development.startupZones.find(item => item.instanceId === current.zoneInstanceId);
            const installed = development.installed.find(item => item.kind === 'zone' && item.slotId === zone.slotInstanceId && item.definitionId === zone.zoneId);
            const replacements = Array.isArray(installed?.zoneReplacements) ? installed.zoneReplacements : [];
            if (replacementMode)
            {
                const entries = replacements.filter(entry => entry.definitionId !== zone.zoneId && entry.quote && Array.isArray(entry.gates)
                    && entry.gates.every(gate => gate?.dependencyId === 'insufficient-resources'));
                for (const entry of entries)
                {
                    const choice = command({ zoneReplacement: true, sourceDefinitionId: zone.zoneId, definitionId: entry.definitionId, slotId: zone.slotInstanceId,
                        zoneInstanceId: zone.instanceId, zoneReplacementPreviewId: entry.previewId }, '', 'pm-choice');
                    image(choice, entry.imageDefinitionKey ?? resolveImageKey('zone', entry.definitionId), 'pm-choice-art');
                    const copy = node('span', 'pm-choice-copy'); copy.append(node('strong', 'pm-choice-name', name(entry)));
                    if (entry.descriptionKey) copy.title = resolveLocalization(entry.descriptionKey, '');
                    resources(copy, entry.quote.infrastructureUpkeep); resources(copy, entry.quote.jobCapacity, { kind: 'job' });
                    resources(copy, entry.quote.costs, { missing: true }); copy.append(node('small', null, '◷ ' + format(entry.quote.durationDays)));
                    if (entry.removedBuildings?.length) copy.append(node('small', 'pm-removal-warning', 'Будут удалены без возврата: ' + entry.removedBuildings.map(item => name(item)).join(', ')));
                    choice.append(copy); if (entry.canAfford === false) { choice.disabled = true; choice.title = 'Недостаточно ресурсов'; } body.append(choice);
                }
                if (!entries.length) empty(body, replacements.some(entry => entry.gates?.some(gate => gate.dependencyId === 'numerical-modifiers-unresolved'))
                    ? 'Скорость строительства зоны ещё не подтверждена' : 'Пока нет доступных специализаций');
            }
            else
            {
            image(body, zone.imageDefinitionKey ?? resolveImageKey('zone', zone.zoneId), 'pm-district-hero'); body.append(node('h4', 'pm-district-name-panel', name(zone)));
            resources(body, installed?.infrastructureUpkeep); resources(body, installed?.jobCapacity, { kind: 'job' });
            const actions = node('div', 'pm-district-actions'); actions.append(replacements.length && !zoneReplacementOrder(zone.instanceId)
                ? inspect('Заменить', { ...current, kind: 'zoneReplacementCatalog' }) : disabled('Заменить', zoneReplacementOrder(zone.instanceId) ? 'Сначала отмените заказ замены зоны' : 'Нет доступных специализаций')); body.append(actions);
            const related = node('div', 'pm-district-zones'), source = graph.districts.flatMap(district => district.zones).find(item => item.instanceId === zone.instanceId);
            for (const building of source?.buildings ?? []) related.append(node('div', 'pm-zone-detail', name(building)));
            if (!related.children.length) empty(related, 'Установленных зданий нет'); body.append(related);
            }
        }
        else if (installedMode)
        {
            const entry = development.installed.find(item => item.slotId === slot.slotId && item.definitionId === current.definitionId);
            const toggleOperation = operations.find(item => item.type === 'setBuildingEnabled' && item.slotId === slot.slotId && item.definitionId === entry.definitionId);
            const capability = entry.toggle, toggleGates = Array.isArray(capability?.gates) ? capability.gates : null;
            const forbidden = toggleGates?.some(gate => gate?.dependencyId === 'building-disable-forbidden') === true;
            const unknown = typeof entry.enabled !== 'boolean' || !capability || typeof capability.canToggle !== 'boolean' || typeof capability.targetEnabled !== 'boolean' || !Array.isArray(toggleGates)
                || toggleGates.some(gate => gate?.dependencyId !== 'building-disable-forbidden')
                || capability.canToggle === true && !toggleOperation;
            const toggleReason = forbidden ? 'Это здание нельзя отключить' : unknown ? 'Правило отключения здания ещё не подтверждено' : 'Это здание нельзя отключить';
            const toggleLabel = entry.enabled === false ? 'Включить' : 'Отключить';
            const buildingEnabledState = entry.enabled === true ? 'Включено' : entry.enabled === false ? 'Выключено' : null;
            const actions = node('div', 'pm-building-actions');
            actions.append(toggleOperation ? command({ definitionId: toggleOperation.definitionId, slotId: toggleOperation.slotId, enabled: toggleOperation.enabled }, toggleLabel) : disabled(toggleLabel, toggleReason));
            const demolitionOperation = operations.find(item => item.type === 'demolishBuilding' && item.slotId === slot.slotId && item.definitionId === entry.definitionId);
            const demolition = entry.demolition, demolitionGates = Array.isArray(demolition?.gates) ? demolition.gates : null;
            const pendingDemolition = slot.reservedByOrderId != null || development.queue.some(item => item.slotId === slot.slotId && item.status === 'pending');
            const demolitionUnknown = !demolition || typeof demolition.canDemolish !== 'boolean' || !demolitionGates
                || demolitionGates.some(gate => !['building-demolition-forbidden', 'building-demolition-permission-unresolved', 'building-demolition-condition-refresh-unresolved'].includes(gate?.dependencyId))
                || demolition.canDemolish === true && !demolitionOperation;
            const demolitionForbidden = demolitionGates?.some(gate => gate?.dependencyId === 'building-demolition-forbidden') === true;
            const demolitionReason = pendingDemolition ? 'Сначала отмените ожидающий заказ в этом месте'
                : demolitionForbidden ? 'Исходные правила запрещают демонтаж этого здания'
                    : demolitionGates?.some(gate => gate?.dependencyId === 'building-demolition-condition-refresh-unresolved') ? 'Условия очереди строительства ещё не подтверждены'
                        : demolitionUnknown ? 'Правило демонтажа здания ещё не подтверждено' : 'Демонтаж этого здания недоступен';
            actions.append(demolitionOperation
                ? command({ definitionId: demolitionOperation.definitionId, slotId: demolitionOperation.slotId, demolition: true }, 'Демонтировать')
                : disabled('Демонтировать', demolitionReason));
            for (const label of ['Заменить', 'Ремонтировать']) actions.append(disabled(label, 'Действие ещё не предоставлено сервером')); body.append(actions);
            const card = node('section', 'pm-building-current'); image(card, entry.imageDefinitionKey ?? resolveImageKey(entry.kind, entry.definitionId), 'pm-building-portrait');
            const copy = node('div'); copy.append(node('div', 'pm-building-name', name(entry)), node('div', 'pm-building-state', buildingEnabledState ?? 'Состояние включения неизвестно'));
            const effects = node('div', 'pm-building-effects'); resources(effects, entry.infrastructureUpkeep); resources(effects, entry.jobCapacity, { kind: 'job' }); jobProductionBonuses(effects, development.catalog.find(item => item.definitionId === entry.definitionId), entry.enabled === true); copy.append(effects); card.append(copy); body.append(card, node('h4', 'pm-upgrade-heading', 'Доступное улучшение'));
            let count = 0;
            for (const target of entry.upgrades ?? [])
            {
                if (target.eligibility !== 'available' || target.gates?.length) continue;
                const control = command({ definitionId: target.definitionId, sourceDefinitionId: target.sourceDefinitionId, slotId: slot.slotId, zoneInstanceId: target.zoneInstanceId, upgradeIcon: true, imageDefinitionKey: target.imageDefinitionKey }, '', 'pm-building-upgrade');
                control.append(node('strong', null, name(target))); resources(control, target.quote?.costs, { missing: true }); control.append(node('small', null, '◷ ' + format(target.quote?.durationDays))); body.append(control); count++;
            }
            if (!count) empty(body, 'Нет доступных улучшений');
        }
        else
        {
            const compatibleEntries = development.catalog.filter(entry => entry.kind === slot.kind && (entry.compatibleSlotIds ?? entry.slotIds ?? []).includes(slot.slotId));
            const entries = compatibleEntries;
            for (const entry of entries)
            {
                const available = entry.eligibility === 'available' && !entry.gates?.length && entry.quote != null;
                const reason = entry.gates?.some(gate => gate.dependencyId === 'eligibility-unresolved') ? 'Не все условия строительства подтверждены'
                    : entry.gates?.some(gate => gate.dependencyId === 'not-eligible') ? 'Не выполнены требования строительства'
                        : entry.gates?.some(gate => gate.dependencyId === 'definition-execution-unresolved') ? 'Эффекты этого объекта ещё не подключены' : 'Строительство сейчас недоступно';
                const choice = available ? command({ definitionId: entry.definitionId, slotId: slot.slotId }, '', 'pm-choice') : disabled('', reason);
                choice.className = 'pm-choice';
                // Одна кнопка карточки: вложенных обработчиков и повторной отправки нет.
                image(choice, entry.imageDefinitionKey ?? resolveImageKey(entry.kind, entry.definitionId), 'pm-choice-art');
                const copy = node('span', 'pm-choice-copy'); copy.append(node('span', 'pm-choice-name', name(entry))); if (entry.descriptionKey) copy.title = resolveLocalization(entry.descriptionKey, '');
                resources(copy, entry.quote?.infrastructureUpkeep); resources(copy, entry.quote?.jobCapacity, { kind: 'job' }); jobProductionBonuses(copy, entry); resources(copy, entry.quote?.costs, { missing: true }); copy.append(node('small', null, '◷ ' + format(entry.quote?.durationDays))); choice.append(copy);
                if (!available) copy.append(node('small', 'pm-choice-status', reason));
                if (available && entry.canAfford === false) { choice.disabled = true; choice.title = 'Недостаточно ресурсов'; } body.append(choice);
            }
            if (!entries.length && slot.kind === 'zone')
            {
                const gates = compatibleEntries.flatMap(entry => Array.isArray(entry.gates) ? entry.gates : []);
                const speedUnknown = gates.some(gate => gate?.dependencyId === 'numerical-modifiers-unresolved');
                empty(body, speedUnknown ? 'Скорость строительства зоны ещё не подтверждена' : 'Пока нет доступных специализаций');
                const slotUnlockGated = Array.isArray(slot.gates) && slot.gates.some(gate => gate?.dependencyId === 'zone-slot-locked' || gate?.dependencyId === 'zone-slot-condition-unresolved');
                const requirementKeys = slotUnlockGated && Array.isArray(slot.requirementLocalizationKeys)
                    ? slot.requirementLocalizationKeys.filter(key => typeof key === 'string' && key.length > 0) : [];
                if (requirementKeys.length) body.append(node('small', 'pm-district-copy', requirementKeys.map(key => resolveLocalization(key, key)).join(' · ')));
            }
            else if (!entries.length) empty(body, 'Нет доступных вариантов');
        }
        root.append(pane);
    }
    for (const [key, top] of savedScroll) { const target = find(root, key); if (target && Number.isFinite(top)) target.scrollTop = top; }
    if (same && focusIndex >= 0)
    {
        const buttons = all(find(root, 'planet-context')).filter(item => item.tagName === 'BUTTON' && !item.disabled);
        const target = buttons.find(item => (item.dataset.planetActionIdentity ?? item.dataset.inspectionIdentity) === focusKey) ?? buttons[Math.min(focusIndex, buttons.length - 1)]; target?.focus?.({ preventScroll: true });
    }
}
