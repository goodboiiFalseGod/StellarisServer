import { viewParticipantId } from './m3-ui-presenter.mjs';

const number = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 3 });
const quantity = value => typeof value === 'number' && Number.isFinite(value) ? number.format(value) : 'Недоступно';
const signedQuantity = value => typeof value === 'number' && Number.isFinite(value) ? `${value > 0 ? '+' : ''}${number.format(value)}` : 'Недоступно';
const reportFields = [['produced', 'Произведено'], ['jobUpkeep', 'Содержание профессий'], ['buildingUpkeep', 'Содержание зданий'], ['upkeep', 'Общее содержание'], ['net', 'Итог за месяц']];
const kinds = { district: 'Район', zone: 'Специализация', building: 'Здание', blockerClearance: 'Расчистка' };
const labels = { district_city: 'Городские районы', district_generator: 'Энергетические районы', district_mining: 'Добывающие районы', district_farming: 'Сельскохозяйственные районы', zone_research_unity: 'Исследования и единство', zone_industrial: 'Промышленная зона', zone_foundry: 'Литейная зона', building_capital: 'Планетарная столица', building_research_lab_1: 'Исследовательские лаборатории', building_factory_1: 'Гражданские фабрики', building_foundry_1: 'Литейные заводы', building_precinct_house: 'Управление правопорядка', building_holo_theatres: 'Голографические театры', building_commercial_zone: 'Коммерческая зона', building_medical_1: 'Медицинский центр', building_stronghold: 'Крепость', technician: 'Техники', miner: 'Шахтёры', farmer: 'Фермеры', physicist: 'Физики', biologist: 'Биологи', engineer: 'Инженеры', foundry: 'Металлурги', artisan: 'Ремесленники', bureaucrat: 'Бюрократы', politician: 'Политики', enforcer: 'Силовики', clerk: 'Клерки', entertainer: 'Артисты', healthcare: 'Медицинские работники', soldier: 'Солдаты', worker: 'Рабочие', specialist: 'Специалисты', ruler: 'Правители', energy: 'Энергия', minerals: 'Минералы', food: 'Пища', alloys: 'Сплавы', consumer_goods: 'Товары массового спроса', unity: 'Единство', physics_research: 'Физика', society_research: 'Общество', engineering_research: 'Инженерия', trade: 'Торговля' };
const caption = id => labels[id] ?? id;
const bodyDisplayName = (body, bodies = []) =>
{
    if (body?.name && !/^pc_[a-z0-9_]+$/.test(body.name)) { return body.name; }
    const type = { star: 'Звезда', planet: 'Планета', moon: 'Спутник', station: 'Станция' }[body?.kind] ?? 'Объект';
    const siblings = bodies.filter(item => item.systemId === body?.systemId && item.kind === body?.kind).sort((a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0);
    const index = siblings.findIndex(item => item.id === body?.id);
    return `${type}${index >= 0 ? ` ${index + 1}` : ''}`;
};
// Проверенные определения 4.5.2 из 00_urban_districts и 02_rural_districts.
const districtPresentation = id => ['district_city', 'district_hive', 'district_nexus'].includes(id) ? 'city' : ['district_generator', 'district_mining', 'district_farming'].includes(id) ? 'resource' : 'other';
const catalogSlots = entry => Array.isArray(entry.compatibleSlotIds) ? entry.compatibleSlotIds : entry.slotIds;
const panelScrollContexts = new WeakMap();

// Только связи полученного графа; совместимость не означает установку во всех зонах.
export function planetStartupGraph(development)
{
    const zones = development?.startupZones ?? [];
    const districtIds = new Set((development?.startupDistricts ?? []).map(item => item.instanceId));
    const zoneById = new Map(zones.filter(item => districtIds.has(item.districtInstanceId)).map(item => [item.instanceId, item]));
    const slots = new Map((development?.slots ?? []).map(item => [item.slotId, item]));
    const startup = development?.startupBuildings ?? [];
    const installed = (development?.installed ?? []).filter(item => item.kind === 'building');
    const installedBySlot = new Map(installed.map(item => [item.slotId, item]));
    const exactZone = (building, slot) =>
    {
        if (slot?.zoneInstanceId != null) { return zoneById.has(slot.zoneInstanceId) ? slot.zoneInstanceId : null; }
        const compatible = building?.compatibleZoneInstanceIds ?? [];
        if (compatible.length === 1 && zoneById.has(compatible[0])) { return compatible[0]; }
        const matches = zones.filter(zone => zoneById.has(zone.instanceId) && zone.slotInstanceId === building?.slotInstanceId && zone.zoneId === building?.zoneId);
        return matches.length === 1 ? matches[0].instanceId : null;
    };
    const buildings = installed.map(item =>
    {
        const source = startup.find(value => value.slotInstanceId === item.slotId);
        const sameDefinition = source?.definitionId === item.definitionId;
        return { ...item, instanceId: sameDefinition ? source.instanceId : item.slotId, slotInstanceId: item.slotId,
            imageDefinitionKey: item.imageDefinitionKey ?? (sameDefinition ? source.imageDefinitionKey : null),
            zoneInstanceId: exactZone(source, slots.get(item.slotId)), compatibleZoneInstanceIds: [] };
    });
    for (const item of startup)
    {
        if (item.slotInstanceId != null && (installedBySlot.has(item.slotInstanceId) || slots.get(item.slotInstanceId)?.kind === 'building')) { continue; }
        buildings.push({ ...item, zoneInstanceId: exactZone(item, slots.get(item.slotInstanceId)) });
    }
    const assigned = new Set();
    const districts = (development?.startupDistricts ?? []).map(district => ({ ...district, zones: zones.filter(zone => zone.districtInstanceId === district.instanceId).map(zone => ({ ...zone, buildings: buildings.filter(building =>
    {
        if (building.zoneInstanceId !== zone.instanceId || assigned.has(building)) { return false; }
        assigned.add(building); return true;
    }) })) }));
    const features = new Map((development?.startupFeatures ?? []).map(item => [item.instanceId, { ...item, clearable: item.isBlocker, gates: [] }]));
    for (const item of development?.features ?? []) { features.set(item.instanceId, { ...features.get(item.instanceId), ...item, isHidden: false }); }
    return { districts, unplacedBuildings: buildings.filter(item => !assigned.has(item)), features: [...features.values()].filter(item => item.isHidden !== true) };
}

export function appendPlanetWorkforceControls(parent, { document, job, disabled = false, pending = false, reference = false, onRequest = () => {}, resolveLocalization = (key, fallback) => fallback ?? key })
{
    if (reference)
    {
        const controls = document.createElement('span'); controls.className = 'pm-job-step';
        const locked = disabled || pending || !Number.isFinite(job.capacity) || !Number.isFinite(job.limit);
        const label = job.localizationKey ? resolveLocalization(job.localizationKey, job.name ?? caption(job.jobId)) : job.name ?? caption(job.jobId);
        for (const direction of [-1, 1])
        {
            const button = document.createElement('button'); button.type = 'button'; button.textContent = direction < 0 ? '−' : '+'; button.disabled = locked;
            button.setAttribute('aria-label', `${direction < 0 ? 'Уменьшить' : 'Увеличить'} Workforce: ${label}`);
            button.title = '5% вместимости; Shift — минимум/максимум'; button.dataset.workforceDirection = String(direction);
            button.onclick = event => { if (!locked) onRequest(event?.ctrlKey ? { kind: 'unsupportedUnit' } : event?.shiftKey ? { kind: direction < 0 ? 'min' : 'max' } : { kind: 'adjust', direction }); };
            controls.append(button);
        }
        parent.append(controls); return controls;
    }
    const section = document.createElement('section'); section.className = 'planet-workforce-controls';
    const input = document.createElement('input'); input.type = 'number'; input.min = '0'; input.max = String(job.capacity); input.step = 'any'; input.value = String(job.limit); input.dataset.jobId = job.jobId;
    input.disabled = disabled || pending || !Number.isFinite(job.capacity) || !Number.isFinite(job.limit);
    input.setAttribute('aria-label', `Лимит Workforce: ${job.localizationKey ? resolveLocalization(job.localizationKey, job.name ?? caption(job.jobId)) : job.name ?? caption(job.jobId)}`);
    const request = value => { if (!input.disabled) { onRequest(value); } };
    for (const direction of [-1, 1])
    {
        const button = document.createElement('button'); button.type = 'button'; button.textContent = direction < 0 ? '−' : '+'; button.disabled = input.disabled; button.title = '5% вместимости; Shift — минимум/максимум'; button.dataset.workforceDirection = String(direction);
        button.onclick = event => request(event?.ctrlKey ? { kind: 'unsupportedUnit' } : event?.shiftKey ? { kind: direction < 0 ? 'min' : 'max' } : { kind: 'adjust', direction }); section.append(button);
    }
    const exact = document.createElement('details');
    const exactSummary = document.createElement('summary'); exactSummary.textContent = '…'; exactSummary.setAttribute('aria-label', 'Точный лимит Workforce'); exact.append(exactSummary);
    input.onfocus = () => { exact.open = true; };
    const apply = document.createElement('button'); apply.type = 'button'; apply.textContent = pending ? 'Ожидание ответа…' : 'Лимит WF'; apply.disabled = input.disabled; apply.onclick = () => request({ kind: 'absolute', raw: input.value });
    const fields = document.createElement('div'); fields.append(input, apply); exact.append(fields); section.append(exact); parent.append(section); return section;
}

// Adapter for existing economy plus reviewed M3DevelopmentProtocol; absence is not an empty executable colony.
export function planetPanelModel(state, selection)
{
    const owner = viewParticipantId(state);
    if (owner == null) { return null; }
    const colony = state?.spatial?.colonies?.find(item => item.ownerParticipantId === owner && (selection?.kind === 'colony' && item.id === selection.id || selection?.kind === 'body' && item.bodyId === selection.id));
    if (!colony) { return null; }
    const body = state.spatial.bodies?.find(item => item.id === colony.bodyId);
    if (!body) { return null; }
    const economy = colony.economyPlanetId == null ? null : state.planets?.find(item => item.id === colony.economyPlanetId && item.ownerId === owner) ?? null;
    const development = state.development?.colonies?.find(item => item.colonyId === colony.id && item.ownerParticipantId === owner && item.economyPlanetId === colony.economyPlanetId) ?? null;
    return {
        id: colony.id, name: bodyDisplayName(body, state.spatial.bodies ?? []), nameDiagnostic: `Объект: ${body.id} · Исходная подпись: ${body.name ?? 'не получена'}`, ownerId: owner,
        economy: economy ? { id: economy.id, population: economy.population, workforce: economy.workforce, report: economy.report == null ? null : structuredClone(economy.report), reportMonth: economy.reportMonth, jobs: Object.entries(economy.jobs ?? {}).map(([jobId, assigned]) => { const definition = state.catalog?.jobs?.find(item => item.id === jobId); return { jobId, name: definition?.name ?? jobId, localizationKey: definition?.localizationKey, assigned }; }) } : null,
        development: development ? structuredClone(development) : null,
        gates: structuredClone(state.development?.gates ?? []),
        contentProfileId: state.development?.contentProfileId ?? null
    };
}

export function resolvePlanetInspection(model, inspection)
{
    if (!inspection || !model?.development) { return null; }
    if (inspection.kind === 'zoneCatalog' || inspection.kind === 'zoneReplacementCatalog')
    {
        const development = model.development;
        if (![inspection.districtInstanceId, inspection.slotInstanceId, inspection.zoneInstanceId].every(value => typeof value === 'string' && value.length > 0)) { return null; }
        const zones = (development.startupZones ?? []).filter(zone => zone.instanceId === inspection.zoneInstanceId && zone.slotInstanceId === inspection.slotInstanceId && zone.districtInstanceId === inspection.districtInstanceId);
        const districts = (development.startupDistricts ?? []).filter(district => district.instanceId === inspection.districtInstanceId);
        if (zones.length !== 1 || districts.length !== 1) { return null; }
        if (inspection.definitionId != null && development.catalog.filter(item => item.kind === 'zone' && item.definitionId === inspection.definitionId).length !== 1) { return null; }
        return { kind: inspection.kind, districtInstanceId: inspection.districtInstanceId, slotInstanceId: inspection.slotInstanceId, zoneInstanceId: inspection.zoneInstanceId, ...(inspection.definitionId == null ? {} : { definitionId: inspection.definitionId }) };
    }
    const slots = model.development.slots.filter(slot => slot.slotId === inspection.slotId);
    if (slots.length !== 1) { return null; }
    const slot = slots[0];
    if (inspection.kind === 'slot') { return { kind: 'slot', slotId: slot.slotId }; }
    const values = inspection.kind === 'installed' ? model.development.installed.filter(item => item.slotId === slot.slotId && item.kind === slot.kind) : inspection.kind === 'catalog' ? model.development.catalog.filter(item => item.definitionId === inspection.definitionId && item.kind === slot.kind && catalogSlots(item).includes(slot.slotId)) : [];
    return values.length === 1 ? { kind: inspection.kind, slotId: slot.slotId, definitionId: values[0].definitionId } : inspection.kind === 'catalog' ? { kind: 'slot', slotId: slot.slotId } : null;
}

export function renderPlanetPanel(root, model, { document, tab = 'infrastructure', inspection = null, onInspect = () => {}, expandedJobs = new Set(), appendImage = () => false, appendVacancies = () => false, appendWorkforce = () => false, appendConstruction = () => {}, appendResourceIcon = () => false, resolveImageKey = () => null, resolveLocalization = (key, fallback) => fallback ?? key } = {})
{
    const scrollContext = model ? JSON.stringify([model.id, model.ownerId]) : null;
    const previousContext = panelScrollContexts.get(root);
    const inspectionContext = JSON.stringify([tab, inspection]);
    const focusButtons = [...(root.querySelectorAll?.('.planet-context button') ?? [])];
    const focusIndex = focusButtons.indexOf(document.activeElement);
    const buttonIdentity = button => button.dataset.planetActionIdentity ?? button.dataset.inspectionIdentity;
    const focusIdentity = focusIndex >= 0 ? buttonIdentity(focusButtons[focusIndex]) : null;
    const scrollPositions = previousContext?.identity === scrollContext ? ['.planet-infrastructure', '.planet-queue', ...(previousContext.inspection === inspectionContext ? ['.planet-context'] : [])].map(selector => [selector, root.querySelector?.(selector)?.scrollTop]) : [];
    const original = (key, fallback) => typeof key === 'string' && /^[A-Za-z0-9_.-]+$/.test(key) ? resolveLocalization(key, fallback) : fallback ?? key;
    const caption = id => original(id, labels[id] ?? id);
    const text = (key, fallback) => resolveLocalization(key, fallback);
    const jobName = (job, plural = true) =>
    {
        const preferredKey = plural ? job.pluralLocalizationKey ?? job.localizationKey : job.localizationKey;
        return preferredKey ? resolveLocalization(preferredKey, job.name ?? labels[job.jobId] ?? job.jobId) : job.name ?? labels[job.jobId] ?? job.jobId;
    };
    const resourceNames = new Map((model?.development?.economy?.resources ?? []).map(resource => [resource.resourceId, resource.localizationKey]));
    const resourceName = id => { const key = resourceNames.get(id); return key ? original(key, labels[id] ?? id) : caption(id); };
    const capacityName = id =>
    {
        const definition = (model?.development?.economy?.jobDefinitions ?? []).find(job => job.jobId === id);
        const job = definition ?? (model?.development?.economy?.jobs ?? model?.development?.jobs ?? model?.economy?.jobs ?? []).find(job => job.jobId === id);
        return job ? jobName(job, true) : labels[id] ?? id;
    };
    const node = (tag, text, className) => { const value = document.createElement(tag); if (text != null) { value.textContent = text; } if (className) { value.className = className; } return value; };
    const amounts = (values, kind = 'resource') => Object.entries(values ?? {}).map(([id, value]) => `${kind === 'job' ? capacityName(id) : resourceName(id)}: ${quantity(value)}`).join(' · ') || 'Нет';
    const gates = values => (values ?? []).map(gate => `${gate.code} (${gate.dependencyId})`).join(' · ');
    const diagnostic = (parent, values) =>
    {
        if (!values?.length) { return; }
        const details = node('details', null, 'planet-diagnostics'); details.append(node('summary', 'Технические причины недоступности'), node('p', gates(values))); parent.append(details);
    };
    const inspectButton = (parent, text, value) =>
    {
        const button = node('button', text, 'planet-nav-button'); button.type = 'button'; button.dataset.planetInspect = value?.kind ?? 'close'; if (value?.slotId != null) { button.dataset.slotId = value.slotId; } if (value?.districtInstanceId != null) { button.dataset.districtInstanceId = value.districtInstanceId; } if (value?.slotInstanceId != null) { button.dataset.slotInstanceId = value.slotInstanceId; } if (value?.zoneInstanceId != null) { button.dataset.zoneInstanceId = value.zoneInstanceId; } if (value?.definitionId != null) { button.dataset.definitionId = value.definitionId; }
        button.dataset.inspectionIdentity = JSON.stringify(value);
        const identityLabel = value?.slotId != null ? ` · место ${value.slotId}` : value?.zoneInstanceId != null ? ` · зона ${value.zoneInstanceId} · место ${value.slotInstanceId}` : '';
        button.ariaLabel = `${text}${identityLabel}${value?.definitionId != null ? ` · определение ${value.definitionId}` : ''}`;
        button.onclick = () => onInspect(value); parent.append(button); return button;
    };
    const art = (parent, key) => { if (key && !appendImage(parent, key)) { parent.append(node('small', `Изображение недоступно: ${key}`, 'planet-diagnostic')); } };
    const card = (title, key, entry = null) => { const value = node('article', null, 'planet-card'); value.append(node('h4', original(entry?.localizationKey ?? title, labels[title] ?? title))); art(value, key); return value; };
    const resourceLine = (parent, label, values, kind = 'resource') =>
    {
        const row = node('p', null, 'planet-resource-line'); row.dataset.amountLabel = label; row.append(node('span', `${label}: `));
        const entries = Object.entries(values ?? {});
        if (!entries.length) { row.append(node('span', 'Нет')); }
        for (const [id, amount] of entries)
        {
            const name = kind === 'job' ? capacityName(id) : resourceName(id);
            const item = node('span', null, 'planet-resource-amount'); item.title = name;
            item.dataset.amountKind = kind; item.dataset.amountId = id; item.dataset.amountValue = String(amount);
            if (kind === 'job') { art(item, resolveImageKey('job', id)); } else { appendResourceIcon(item, id); }
            item.append(node('span', `${name}: ${quantity(amount)}`)); row.append(item);
        }
        parent.append(row);
    };
    const progress = (parent, order) =>
    {
        const bar = node('progress', null, 'planet-order-progress');
        bar.title = `${original(order.localizationKey ?? order.definitionId, labels[order.definitionId] ?? order.definitionId)} · ${quantity(order.completedWork)} / ${quantity(order.requiredWork)} ${order.workUnit ?? ''}`;
        bar.ariaLabel = bar.title;
        if (Number.isFinite(order.requiredWork) && order.requiredWork > 0 && Number.isFinite(order.completedWork) && order.completedWork >= 0 && order.completedWork <= order.requiredWork)
        { bar.max = order.requiredWork; bar.value = order.completedWork; }
        parent.append(bar);
    };
    const slotProgress = (parent, slot) =>
    {
        if (!slot) { return; }
        const orders = (model.development?.queue ?? []).filter(order => !['completed', 'cancelled'].includes(order.status) && order.slotId === slot.slotId && (slot.reservedByOrderId == null || order.orderId === slot.reservedByOrderId));
        if (orders.length === 1) { progress(parent, orders[0]); }
    };
    const catalogMetadata = (parent, entry) =>
    {
        parent.dataset.eligibility = entry.eligibility;
        if (typeof entry.canAfford === 'boolean') { parent.dataset.affordability = entry.canAfford ? 'available' : 'insufficient-resources'; }
        if (Number.isFinite(entry.sourceTier)) { parent.append(node('small', `Уровень: ${quantity(entry.sourceTier)}`)); }
        if (entry.prerequisites?.length) { parent.append(node('p', `Технологии: ${entry.prerequisites.map(caption).join(', ')}`, 'planet-prerequisites')); }
        if (entry.upgradesTo?.length) { parent.append(node('p', `${text('UPGRADES_TO', 'Улучшается до')}: ${entry.upgradesTo.map(caption).join(', ')}`, 'planet-upgrade-chain')); }
        if (entry.canAfford === false) { parent.append(node('p', 'Недостаточно ресурсов.', 'planet-notice')); }
        if (entry.upgradeOperationGate) { parent.append(node('p', 'Исполнение улучшения ещё не подтверждено сервером.', 'planet-notice')); }
        if (entry.sourceRecipe?.length)
        {
            const details = node('details', null, 'planet-source-recipe'); details.append(node('summary', 'Параметры исходного определения'));
            for (const term of entry.sourceRecipe)
            { details.append(node('p', `${term.kind} · ${caption(term.targetId)}: ${term.value == null ? term.expression ?? 'Недоступно' : quantity(term.value)} · ${term.conditionDisposition}`)); }
            parent.append(details);
        }
    };
    if (!model) { panelScrollContexts.delete(root); root.replaceChildren(node('p', 'Колония недоступна в текущем обзоре.', 'planet-diagnostic')); return; }
    const overview = node('section', null, 'planet-overview');
    const portrait = node('section', null, 'planet-header-art');
    portrait.append(node('h3', model.name ?? 'Колония'), node('p', 'Панорама недоступна', 'planet-notice'));
    if (model.nameDiagnostic) { const details = node('details'); details.append(node('summary', 'Сведения об объекте'), node('p', model.nameDiagnostic)); portrait.append(details); }
    overview.append(portrait);
    const summary = node('div', null, 'planet-summary');
    summary.append(node('span', `Население (прототип сервера): ${quantity(model.economy?.population)}`), node('span', `Workforce (прототип сервера): ${quantity(model.economy?.workforce)}`));
    summary.append(node('span', 'Губернатор: недоступно'), node('span', 'Стабильность / преступность: недоступно'), node('span', 'Жильё / блага / безработица: недоступно'));
    const report = model.economy?.report;
    const month = model.economy?.reportMonth;
    const settlement = node('section', null, 'planet-settlement');
    settlement.append(node('h4', report != null && Number.isSafeInteger(month) && month > 0 ? `Закрытый месяц: ${quantity(month)}` : 'Отчёт закрытого месяца недоступен'));
    if (report == null || !Number.isSafeInteger(month) || month <= 0)
    {
        settlement.append(node('p', 'Недоступно', 'planet-diagnostic'));
    }
    else
    {
        const maps = reportFields.map(([field]) => report[field]);
        const resources = [...new Set(maps.flatMap(values => values && typeof values === 'object' && !Array.isArray(values) ? Object.keys(values) : []))].sort((a, b) => a.localeCompare(b, 'ru'));
        const net = node('div', null, 'planet-settlement-net');
        net.append(node('span', 'Итог за месяц', 'planet-settlement-label'));
        if (!resources.length) { net.append(node('span', 'Итог за месяц: Недоступно')); }
        for (const resourceId of resources)
        {
            const value = node('span', null, 'planet-settlement-resource');
            appendResourceIcon(value, resourceId);
            value.append(node('span', resourceName(resourceId)), node('strong', signedQuantity(report.net && Object.hasOwn(report.net, resourceId) ? report.net[resourceId] : undefined)));
            net.append(value);
        }
        settlement.append(net);
        for (const [field, label] of reportFields.slice(0, 4))
        {
            const flow = node('div', null, 'planet-settlement-flow'); flow.append(node('span', label, 'planet-settlement-label'));
            const values = report[field];
            for (const resourceId of resources)
            {
                const item = node('span', null, 'planet-settlement-resource'); appendResourceIcon(item, resourceId);
                item.append(node('span', resourceName(resourceId)), node('strong', quantity(values && Object.hasOwn(values, resourceId) ? values[resourceId] : undefined))); flow.append(item);
            }
            settlement.append(flow);
        }
        const breakdown = node('details', null, 'planet-settlement-breakdown');
        breakdown.append(node('summary', 'Производство и расходы'));
        if (!resources.length) { breakdown.append(node('p', 'Данные по ресурсам недоступны.')); }
        const table = node('table', null, 'planet-settlement-table');
        const head = node('thead'); const heading = node('tr');
        for (const label of ['Ресурс', ...reportFields.slice(0, 4).map(([, name]) => name)]) { heading.append(node('th', label)); }
        head.append(heading); table.append(head);
        const body = node('tbody');
        for (const resourceId of resources)
        {
            const value = node('tr'); const resource = node('td', null, 'planet-settlement-table-resource');
            appendResourceIcon(resource, resourceId);
            resource.append(node('span', resourceName(resourceId))); value.append(resource);
            for (const [field] of reportFields.slice(0, 4))
            {
                const values = report[field];
                value.append(node('td', quantity(values && Object.hasOwn(values, resourceId) ? values[resourceId] : undefined)));
            }
            body.append(value);
        }
        table.append(body);
        const tableScroll = node('div', null, 'planet-settlement-table-scroll'); tableScroll.append(table); breakdown.append(tableScroll);
        settlement.append(breakdown);
    }
    overview.append(summary); const nodes = [overview];
    if (!model.economy && !model.development?.economy) { nodes.push(node('p', 'Экономика колонии недоступна: нет подтверждённой связи с экономическим снимком.', 'planet-diagnostic')); }
    const development = model.development;
    const graph = planetStartupGraph(development);
    const liveEconomy = development?.economy;
    if (development?.planetClass != null) { summary.append(node('span', `Класс: ${caption(development.planetClass)}`)); }
    if (development?.planetSize != null) { summary.append(node('span', `Размер: ${quantity(development.planetSize)}`)); }
    if (liveEconomy)
    {
        summary.replaceChildren(node('span', `Население: ${quantity(liveEconomy.population)}`), node('span', `Базовая Workforce: ${quantity(liveEconomy.baseWorkforce)}`));
        settlement.replaceChildren(node('h4', liveEconomy.reportMonth > 0 ? `Закрытый месяц: ${quantity(liveEconomy.reportMonth)}` : 'Отчёт закрытого месяца недоступен'));
        const net = node('div', null, 'planet-settlement-net');
        for (const resource of liveEconomy.reportMonth > 0 ? liveEconomy.resources ?? [] : [])
        {
            const item = node('span', null, 'planet-settlement-resource'); appendResourceIcon(item, resource.resourceId);
            item.title = `${resourceName(resource.resourceId)} · Итог закрытого месяца${resource.descriptionKey ? ` · ${resolveLocalization(resource.descriptionKey, `Описание недоступно: ${resource.descriptionKey}`)}` : ''}`;
            item.append(node('span', resourceName(resource.resourceId)), node('strong', signedQuantity(resource.net))); net.append(item);
        }
        settlement.append(net);
        const production = node('div', null, 'planet-settlement-flow'); production.append(node('span', 'Произведено', 'planet-settlement-label'));
        for (const resource of liveEconomy.resources ?? [])
        {
            const item = node('span', null, 'planet-settlement-resource'); appendResourceIcon(item, resource.resourceId);
            item.append(node('span', resourceName(resource.resourceId)), node('strong', liveEconomy.reportMonth > 0 ? quantity(resource.produced) : 'Недоступно')); production.append(item);
        }
        settlement.append(production);
        const upkeep = node('div', null, 'planet-settlement-upkeep');
        for (const [field, label] of [['jobUpkeep', 'Профессии'], ['buildingUpkeep', 'Здания'], ['districtUpkeep', 'Районы'], ['zoneUpkeep', 'Зоны']])
        {
            const source = node('section', null, 'planet-settlement-source'); source.append(node('span', label, 'planet-settlement-source-label'));
            for (const resource of liveEconomy.resources ?? [])
            {
                const item = node('span', null, 'planet-settlement-resource'); appendResourceIcon(item, resource.resourceId);
                item.append(node('span', resourceName(resource.resourceId)), node('strong', liveEconomy.reportMonth > 0 ? quantity(resource[field]) : 'Недоступно')); source.append(item);
            }
            upkeep.append(source);
        }
        settlement.append(upkeep);
        const breakdown = node('details', null, 'planet-settlement-breakdown'); breakdown.append(node('summary', 'Запасы, производство и содержание'));
        const table = node('table', null, 'planet-settlement-table'); const header = node('tr');
        for (const title of ['Ресурс', 'Запас', 'Предел запаса', 'Произведено', 'Профессии', 'Здания', 'Районы', 'Зоны', 'Итог']) { header.append(node('th', title)); } table.append(header);
        for (const resource of liveEconomy.resources ?? [])
        {
            const row = node('tr'); const name = node('td', resourceName(resource.resourceId)); appendResourceIcon(name, resource.resourceId); row.append(name, node('td', quantity(resource.stock)), node('td', resource.storagePolicy === 'unlimited-reserve' ? 'Без ограничения' : quantity(resource.effectiveMaximum ?? resource.sourceMaximum)));
            for (const field of ['produced', 'jobUpkeep', 'buildingUpkeep', 'districtUpkeep', 'zoneUpkeep', 'net']) { row.append(node('td', liveEconomy.reportMonth > 0 ? (field === 'net' ? signedQuantity(resource[field]) : quantity(resource[field])) : 'Недоступно')); }
            table.append(row);
        }
        const scroll = node('div', null, 'planet-settlement-table-scroll'); scroll.append(table); breakdown.append(scroll); settlement.append(breakdown);
        if (development.planetClass != null) { const value = node('span', `Класс: ${original(development.planetClass, 'Название недоступно')}`); value.title = `Исходный класс: ${development.planetClass}`; summary.append(value); }
        if (development.planetSize != null) { summary.append(node('span', `Размер: ${quantity(development.planetSize)}`)); }
    }
    if (!development)
    {
        nodes.push(node('p', 'Развитие колонии пока недоступно: сервер не активировал подтверждённые данные строительства.', 'planet-diagnostic'));
        if (!['jobs', 'holdings', 'armies'].includes(tab)) { nodes.push(settlement); root.replaceChildren(...nodes); return; }
    }
    if (model.gates.length || development?.gates?.length)
    {
        overview.append(node('p', 'Часть действий недоступна: условия колонии ещё не подтверждены.', 'planet-notice'));
        diagnostic(overview, [...model.gates, ...(development?.gates ?? [])].filter((gate, index, all) => all.findIndex(item => item.code === gate.code && item.dependencyId === gate.dependencyId) === index));
    }
    if (tab === 'infrastructure' && development)
    {
        const grid = node('div', null, 'planet-layout');
        const main = node('section'); main.append(node('h3', text('DISTRICTS_AND_BUILDINGS_TITLE', 'Районы, специализации и здания')));
        main.className = 'planet-infrastructure';
        const zoneContainers = new Map();
        const groups = node('div', null, 'planet-district-groups');
        const cities = node('div', null, 'planet-city-districts');
        const rural = node('div', null, 'planet-resource-districts');
        const otherDistricts = node('div', null, 'planet-other-districts');
        const buildingCard = building =>
        {
            const value = card(building.definitionId, building.imageDefinitionKey ?? resolveImageKey('building', building.definitionId), building); value.dataset.buildingInstance = building.instanceId; value.className += ' planet-building-cell'; value.title = original(building.localizationKey ?? building.definitionId, labels[building.definitionId] ?? building.definitionId);
            const slot = development.slots.find(item => item.slotId === building.slotId);
            if (slot) { inspectButton(value, `Сведения: ${value.title}`, { kind: 'installed', slotId: building.slotId, definitionId: building.definitionId }); }
            if (slot?.reservedByOrderId != null) { const details = node('details', null, 'planet-identifiers'); details.append(node('summary', 'Сведения'), node('p', `Место: ${building.slotId} · Заказ: ${slot.reservedByOrderId}`)); value.append(details); }
            if (slot?.gates?.length) { diagnostic(value, slot.gates); }
            if (building.zoneInstanceId == null) { value.append(node('small', 'Точное размещение в специализации не получено.', 'planet-notice')); }
            slotProgress(value, slot);
            return value;
        };
        // Меняется только порядок представления; source occurrence graph остаётся неизменным.
        for (const district of graph.districts)
        {
            const group = node('article', null, 'planet-card planet-district-group'); group.dataset.districtInstance = district.instanceId;
            const heading = node('div', null, 'planet-district-heading'); heading.append(node('h4', original(district.localizationKey ?? district.definitionId, labels[district.definitionId] ?? district.definitionId)));
            group.append(heading); art(group, district.imageDefinitionKey ?? resolveImageKey('district', district.definitionId));
            const presentation = districtPresentation(district.definitionId);
            group.dataset.districtDefinition = district.definitionId;
            group.className += presentation === 'city' ? ' planet-city' : presentation === 'resource' ? ' planet-resource-district' : ' planet-other-district';
            heading.append(node('strong', `Уровень ${quantity(district.level)}`, 'planet-district-level'));
            const track = node('div', null, 'planet-district-track');
            track.append(node('span', 'Вместимость недоступна', 'planet-district-unavailable'));
            heading.append(track);
            const districtItems = development.installed.filter(item => item.kind === 'district' && item.definitionId === district.definitionId && development.slots.some(slot => slot.slotId === item.slotId));
            if (districtItems.length)
            {
                const chooser = node('details', null, 'planet-district-inspect'); chooser.append(node('summary', 'Сведения о районе'));
                for (const [index, item] of districtItems.entries())
                {
                    const details = node('details', null, 'planet-identifiers'); details.append(node('summary', districtItems.length > 1 ? `Место ${index + 1}` : 'Сведения'));
                    inspectButton(details, 'Открыть сведения', { kind: 'installed', slotId: item.slotId, definitionId: item.definitionId });
                    const slot = development.slots.find(slot => slot.slotId === item.slotId);
                    details.append(node('p', `ID места: ${item.slotId}`));
                    if (slot.reservedByOrderId != null) { details.append(node('p', `ID заказа: ${slot.reservedByOrderId}`)); }
                    diagnostic(details, slot.gates); chooser.append(details);
                }
                heading.append(chooser);
            }
            for (const zone of district.zones)
            {
                const value = card(zone.zoneId, zone.imageDefinitionKey ?? resolveImageKey('zone', zone.zoneId), zone); value.className += ' planet-zone'; value.dataset.zoneInstance = zone.instanceId;
                if (zone.zoneId === 'zone_default') { value.className += ' planet-zone-base'; }
                value.title = `${original(zone.localizationKey ?? zone.zoneId, labels[zone.zoneId] ?? zone.zoneId)} · Место специализации: ${quantity(zone.slotNumber)}`;
                const zoneSlot = development.slots.find(slot => slot.slotId === zone.slotInstanceId);
                const zoneInstalled = development.installed.find(item => item.kind === 'zone' && item.slotId === zoneSlot?.slotId && item.definitionId === zone.zoneId);
                if (zoneInstalled) { inspectButton(value, 'Сведения', { kind: 'installed', slotId: zoneSlot.slotId, definitionId: zoneInstalled.definitionId }); }
                inspectButton(value, 'Каталог специализаций', { kind: 'zoneCatalog', districtInstanceId: district.instanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId });
                if (zoneSlot?.reservedByOrderId != null) { const details = node('details', null, 'planet-identifiers'); details.append(node('summary', 'Сведения'), node('p', `Место: ${zoneSlot.slotId} · Заказ: ${zoneSlot.reservedByOrderId}`)); value.append(details); }
                if (zoneSlot?.gates?.length) { diagnostic(value, zoneSlot.gates); }
                const buildings = node('div', null, 'planet-zone-buildings');
                zoneContainers.set(zone.instanceId, buildings);
                for (const building of zone.buildings) { buildings.append(buildingCard(building)); }
                if (!zone.buildings.length && !development.slots.some(slot => slot.kind === 'building' && slot.zoneInstanceId === zone.instanceId)) { buildings.append(node('p', 'Установленные здания не получены.', 'planet-notice')); }
                value.append(buildings); group.append(value);
            }
            (presentation === 'city' ? cities : presentation === 'resource' ? rural : otherDistricts).append(group);
        }
        if (cities.children.length) { groups.append(cities); }
        if (rural.children.length) { groups.append(rural); }
        if (otherDistricts.children.length) { groups.append(otherDistricts); }
        if (graph.districts.length) { main.append(groups); }
        if (graph.unplacedBuildings.length)
        {
            const buildings = node('section', null, 'planet-startup-buildings'); buildings.append(node('h4', 'Здания колонии'));
            const cards = node('div', null, 'planet-cards'); for (const building of graph.unplacedBuildings) { cards.append(buildingCard(building)); } buildings.append(cards); main.append(buildings);
        }
        if (development.startupDistrictCaps?.length)
        {
            const caps = node('details', null, 'planet-diagnostics'); caps.append(node('summary', 'Полученные ограничения районов'));
            for (const cap of development.startupDistrictCaps) { caps.append(node('p', `${cap.modifierId}: ${quantity(cap.value)}`)); } main.append(caps);
        }
        const slots = node('div', null, 'planet-slots planet-cards planet-unplaced-slots');
        for (const slot of development.slots)
        {
            const startupZone = (development.startupZones ?? []).find(item => item.slotInstanceId === slot.slotId);
            const startupBuildings = (development.startupBuildings ?? []).filter(item => item.slotInstanceId === slot.slotId);
            const installed = development.installed.find(item => item.slotId === slot.slotId);
            const representedBuilding = installed?.kind === 'building' && (graph.unplacedBuildings.some(item => item.slotId === slot.slotId) || graph.districts.some(district => district.zones.some(zone => zone.buildings.some(item => item.slotId === slot.slotId))));
            const representedDistrict = installed?.kind === 'district' && graph.districts.some(item => item.definitionId === installed.definitionId);
            if (representedBuilding || representedDistrict || installed?.kind === 'zone' && startupZone?.zoneId === installed.definitionId) { continue; }
            const value = installed ? card(kinds[slot.kind] ?? slot.kind, installed.imageDefinitionKey) : node('article', null, 'planet-card');
            value.className += ' planet-slot';
            value.dataset.slotState = installed || slot.installedDefinitionId != null ? 'installed' : slot.reservedByOrderId != null ? 'reserved' : 'free';
            value.title = `${kinds[slot.kind] ?? slot.kind} · ${caption(installed?.definitionId ?? slot.installedDefinitionId ?? startupZone?.zoneId ?? (startupBuildings.length ? startupBuildings.map(item => caption(item.definitionId)).join(', ') : 'Свободное место'))}`;
            value.append(node('p', caption(installed?.definitionId ?? slot.installedDefinitionId ?? startupZone?.zoneId ?? (startupBuildings.length ? startupBuildings.map(item => caption(item.definitionId)).join(', ') : 'Свободное место'))));
            if (slot.reservedByOrderId != null) { const details = node('details', null, 'planet-identifiers'); details.append(node('summary', 'Сведения'), node('p', `ID места: ${slot.slotId} · ID заказа: ${slot.reservedByOrderId}`)); value.append(details); }
            if (slot.gates.length) { value.append(node('p', 'Условия места не подтверждены.', 'planet-notice')); }
            const supported = Object.hasOwn(kinds, slot.kind) && !startupZone && !startupBuildings.length;
            inspectButton(value, installed ? 'Сведения' : slot.reservedByOrderId != null ? 'Заказано' : slot.installedDefinitionId != null || !supported ? 'Сведения о месте' : 'Выбрать строительство', installed ? { kind: 'installed', slotId: slot.slotId, definitionId: installed.definitionId } : { kind: 'slot', slotId: slot.slotId });
            if (!supported && !installed) { value.append(node('p', 'Операция для этого места ещё не подтверждена сервером.', 'planet-notice')); }
            slotProgress(value, slot);
            const zoneContainer = slot.kind === 'building' && slot.zoneInstanceId != null ? zoneContainers.get(slot.zoneInstanceId) : null;
            if (zoneContainer) { zoneContainer.append(value); }
            else { slots.append(value); }
        }
        if (!development.slots.length)
        {
            slots.append(node('p', 'Подтверждённые места строительства пока не получены.', 'planet-diagnostic'));
            if (development.catalog.length)
            {
                const definitions = node('details', null, 'planet-diagnostics'); definitions.append(node('summary', `Полученные определения (${development.catalog.length})`));
                for (const entry of development.catalog)
                {
                    const value = card(entry.definitionId, entry.imageDefinitionKey); value.append(node('p', entry.quote ? `Стоимость: ${amounts(entry.quote.costs)} · Срок: ${quantity(entry.quote.durationDays)} дней` : 'Стоимость и срок не подтверждены')); diagnostic(value, entry.gates); definitions.append(value);
                }
                slots.append(definitions);
            }
        }
        if (slots.children.length)
        {
            const otherSlots = node('section', null, 'planet-other-slots'); otherSlots.append(node('h4', 'Места без подтверждённой связи с группой'), slots); main.append(otherSlots);
        }
        settlement.className += ' planet-economy-panel';
        if (graph.districts.length) { groups.append(settlement); }
        else { main.append(settlement); }
        const queue = node('section', null, 'planet-queue'); queue.append(node('h3', text('QUEUE', 'Очередь строительства')));
        const visibleOrders = development.queue.filter(order => !['completed', 'cancelled'].includes(order.status));
        for (const order of visibleOrders)
        {
            const value = node('article', null, 'planet-card planet-queue-order'); value.dataset.orderId = order.orderId;
            const statuses = { pending: 'В очереди' };
            value.append(node('h4', original(order.localizationKey ?? order.definitionId, labels[order.definitionId] ?? order.definitionId)), node('p', `${statuses[order.status] ?? 'Состояние недоступно'} · ${quantity(order.completedWork)} / ${quantity(order.requiredWork)}`));
            const details = node('details', null, 'planet-order-details'); details.append(node('summary', 'Сведения о заказе'), node('p', `ID заказа: ${order.orderId} · ID места: ${order.slotId ?? 'Недоступно'} · Состояние: ${order.status ?? 'Недоступно'} · Единица: ${order.workUnit ?? 'Недоступно'}`)); resourceLine(details, 'Оплачено', order.paidCosts);
            if (order.refundQuote) { resourceLine(details, 'Возврат', order.refundQuote.amounts); }
            value.append(details);
            diagnostic(value, order.gates);
            appendConstruction(value, { orderId: order.orderId });
            progress(value, order);
            queue.append(value);
        }
        if (!visibleOrders.length) { queue.append(node('p', 'Записей очереди в снимке нет.')); }
        grid.append(main, queue);
        const current = resolvePlanetInspection(model, inspection);
        if (current)
        {
            grid.className += ' has-context';
            const zoneCatalogMode = current.kind === 'zoneCatalog';
            const pane = node('section', null, 'planet-context'); pane.append(node('h3', zoneCatalogMode ? 'Каталог специализаций' : current.kind === 'slot' ? text('CONSTRUCTION_TITLE', 'Выбор строительства') : text('BUILDING_DETAILS_TITLE', 'Сведения')));
            inspectButton(pane, 'Закрыть сведения', null);
            const slot = zoneCatalogMode ? null : development.slots.find(item => item.slotId === current.slotId);
            if (zoneCatalogMode)
            {
                const zone = development.startupZones.find(item => item.instanceId === current.zoneInstanceId && item.slotInstanceId === current.slotInstanceId && item.districtInstanceId === current.districtInstanceId);
                pane.append(node('p', original(zone.localizationKey ?? zone.zoneId, labels[zone.zoneId] ?? zone.zoneId)));
                const identity = node('details', null, 'planet-identifiers'); identity.append(node('summary', 'Технические сведения'), node('p', `ID района: ${current.districtInstanceId} · ID места: ${current.slotInstanceId} · ID зоны: ${current.zoneInstanceId}`)); pane.append(identity);
                pane.append(node('p', 'Просмотр каталога. Совместимость и смена специализации пока недоступны.', 'planet-notice'));
                if (current.definitionId == null)
                {
                    const catalog = node('div', null, 'planet-catalog');
                    for (const entry of development.catalog.filter(item => item.kind === 'zone'))
                    {
                        const value = card(entry.definitionId, entry.imageDefinitionKey, entry); catalogMetadata(value, entry); diagnostic(value, entry.gates);
                        inspectButton(value, 'Подробнее', { kind: 'zoneCatalog', districtInstanceId: current.districtInstanceId, slotInstanceId: current.slotInstanceId, zoneInstanceId: current.zoneInstanceId, definitionId: entry.definitionId }); catalog.append(value);
                    }
                    if (!catalog.children.length) { catalog.append(node('p', 'Нет доступных вариантов', 'planet-notice')); }
                    pane.append(catalog);
                }
                else
                {
                    const entry = development.catalog.find(item => item.kind === 'zone' && item.definitionId === current.definitionId);
                    const value = card(entry.definitionId, entry.imageDefinitionKey, entry); value.className += ' planet-item-details';
                    if (entry.descriptionKey) { value.append(node('p', resolveLocalization(entry.descriptionKey, `Описание недоступно: ${entry.descriptionKey}`), 'planet-description')); }
                    catalogMetadata(value, entry); diagnostic(value, entry.gates);
                    inspectButton(value, 'К списку специализаций', { kind: 'zoneCatalog', districtInstanceId: current.districtInstanceId, slotInstanceId: current.slotInstanceId, zoneInstanceId: current.zoneInstanceId });
                    pane.append(value);
                }
            }
            else
            {
                pane.append(node('p', kinds[slot.kind] ?? slot.kind));
                const identity = node('details', null, 'planet-identifiers'); identity.append(node('summary', 'ID места'), node('p', slot.slotId)); pane.append(identity); diagnostic(pane, slot.gates);
                if (current.kind === 'slot')
                {
                    const catalog = node('div', null, 'planet-catalog');
                    for (const entry of development.catalog.filter(item => item.kind === slot.kind && catalogSlots(item).includes(slot.slotId)))
                    {
                        const value = card(entry.definitionId, entry.imageDefinitionKey, entry);
                        if (entry.quote)
                        {
                            resourceLine(value, 'Содержание', entry.quote.infrastructureUpkeep); resourceLine(value, 'Профессии', entry.quote.jobCapacity, 'job');
                            resourceLine(value, 'Стоимость', entry.quote.costs); value.append(node('p', `Срок: ${quantity(entry.quote.durationDays)} дней`, 'planet-notice'));
                        }
                        else { value.append(node('p', 'Стоимость и срок не подтверждены', 'planet-notice')); }
                        catalogMetadata(value, entry);
                        if (entry.eligibility !== 'available' || entry.gates.length) { value.append(node('p', 'Строительство недоступно: условия не подтверждены.', 'planet-notice')); }
                        if (!appendConstruction(value, { definitionId: entry.definitionId, slotId: slot.slotId })) { const unavailable = node('button', 'Построить'); unavailable.type = 'button'; unavailable.disabled = true; value.append(unavailable); }
                        inspectButton(value, 'Подробнее', { kind: 'catalog', slotId: slot.slotId, definitionId: entry.definitionId }); catalog.append(value);
                    }
                    if (!catalog.children.length) { catalog.append(node('p', 'Нет доступных вариантов', 'planet-notice')); }
                    pane.append(catalog);
                }
                else
                {
                    const entry = current.kind === 'installed' ? development.installed.find(item => item.slotId === slot.slotId && item.definitionId === current.definitionId) : development.catalog.find(item => item.definitionId === current.definitionId && item.kind === slot.kind && catalogSlots(item).includes(slot.slotId));
                    const value = card(entry.definitionId, entry.imageDefinitionKey, entry); value.className += ' planet-item-details';
                    if (entry.descriptionKey) { value.append(node('p', resolveLocalization(entry.descriptionKey, `Описание недоступно: ${entry.descriptionKey}`), 'planet-description')); }
                    if (current.kind === 'catalog')
                    {
                        catalogMetadata(value, entry);
                        inspectButton(value, 'К списку', { kind: 'slot', slotId: slot.slotId });
                        value.append(node('p', entry.quote ? `Стоимость: ${amounts(entry.quote.costs)} · Срок: ${quantity(entry.quote.durationDays)} дней` : 'Стоимость и срок не подтверждены'));
                        if (entry.quote) { value.append(node('p', `Содержание: ${amounts(entry.quote.infrastructureUpkeep)}`), node('p', `Вместимость профессий: ${amounts(entry.quote.jobCapacity, 'job')}`)); }
                        diagnostic(value, entry.gates);
                        if (!appendConstruction(value, { definitionId: entry.definitionId, slotId: slot.slotId })) { value.append(node('p', 'Строительство недоступно для этого места.', 'planet-notice')); }
                    }
                    else
                    {
                        value.append(node('p', `Содержание инфраструктуры: ${amounts(entry.infrastructureUpkeep)}`), node('p', `Вместимость профессий: ${amounts(entry.jobCapacity, 'job')}`));
                        if (slot.kind === 'building' && slot.installedDefinitionId === entry.definitionId)
                        {
                            const upgrades = node('section', null, 'planet-upgrades'); upgrades.append(node('h4', 'Доступные улучшения'));
                            let count = 0;
                            for (const target of Array.isArray(entry.upgrades) ? entry.upgrades : [])
                            {
                                const candidate = card('Улучшается до', null);
                                candidate.append(node('p', original(target.localizationKey ?? target.definitionId, labels[target.definitionId] ?? target.definitionId)));
                                candidate.className += ' planet-upgrade-target';
                                candidate.dataset.availability = target.canAfford === true ? 'available' : target.canAfford === false ? 'insufficient_resources' : 'unresolved';
                                candidate.append(node('p', target.quote ? `Стоимость: ${amounts(target.quote.costs)} · Срок: ${quantity(target.quote.durationDays)} дней` : 'Стоимость и срок не подтверждены', 'planet-notice'));
                                diagnostic(candidate, target.gates);
                                const appended = target.canAfford === true && appendConstruction(candidate, { definitionId: target.definitionId, sourceDefinitionId: target.sourceDefinitionId, slotId: slot.slotId, zoneInstanceId: target.zoneInstanceId, upgradeIcon: true, imageDefinitionKey: target.imageDefinitionKey });
                                if (!appended)
                                {
                                    const disabled = node('button', '↑', 'planet-upgrade-target'); disabled.type = 'button'; disabled.disabled = true;
                                    disabled.dataset.availability = candidate.dataset.availability; disabled.setAttribute('aria-label', `Улучшить до ${target.definitionId}`);
                                    art(disabled, target.imageDefinitionKey); candidate.append(disabled);
                                    candidate.append(node('p', target.canAfford === false ? 'Недостаточно ресурсов' : target.canAfford == null ? 'Доступность по ресурсам не подтверждена' : 'Улучшение недоступно по текущим условиям.', 'planet-notice'));
                                }
                                upgrades.append(candidate); count++;
                            }
                            if (count) { value.append(upgrades); }
                        }
                    }
                    pane.append(value);
                }
            }
            grid.append(pane);
        }
        nodes.push(grid);
    }
    else if (tab === 'features' && development)
    {
        const obstacles = node('section', null, 'planet-feature-group'); obstacles.append(node('h3', 'Препятствия'));
        const passives = node('section', null, 'planet-feature-group'); passives.append(node('h3', 'Пассивные особенности'));
        const obstacleCards = node('div', null, 'planet-feature-list'); const passiveCards = node('div', null, 'planet-feature-list');
        for (const feature of graph.features)
        {
            const value = card(feature.definitionId, feature.imageDefinitionKey ?? resolveImageKey(feature.clearable ? 'blocker' : 'feature', feature.definitionId), feature); value.dataset.featureInstance = feature.instanceId; value.dataset.clearable = String(feature.clearable); value.className += ' planet-feature-row';
            if (feature.descriptionKey) { value.append(node('p', resolveLocalization(feature.descriptionKey, `Описание недоступно: ${feature.descriptionKey}`), 'planet-description')); }
            value.append(node('p', feature.clearable ? 'Препятствие' : 'Пассивная особенность'));
            diagnostic(value, feature.gates);
            if (feature.hiddenFeatureInstanceId != null) { value.append(node('p', 'Скрывает связанную залежь.')); }
            if (feature.clearable && !appendConstruction(value, { instanceId: feature.instanceId })) { value.append(node('p', 'Расчистка недоступна: связь экземпляра особенности с местом расчистки или условия операции не подтверждены.', 'planet-diagnostic')); }
            (feature.clearable ? obstacleCards : passiveCards).append(value);
        }
        if (!obstacleCards.children.length && !passiveCards.children.length) { nodes.push(node('p', 'Полученный список особенностей пуст.')); }
        else
        {
            if (obstacleCards.children.length) { obstacles.append(obstacleCards); nodes.push(obstacles); }
            if (passiveCards.children.length) { passives.append(passiveCards); nodes.push(passives); }
        }
    }
    else if (tab === 'jobs')
    {
        const developmentJobs = !!liveEconomy || !!development?.jobs?.length;
        const jobs = liveEconomy?.jobs ?? (developmentJobs ? development.jobs : model.economy?.jobs ?? []);
        const categories = new Map();
        for (const job of jobs)
        {
            const key = developmentJobs ? job.sourceSocialClassId ?? job.categoryId : 'Вакансии из экономического снимка';
            if (!categories.has(key)) { categories.set(key, []); }
            categories.get(key).push(job);
        }
        for (const [category, values] of categories)
        {
            const details = node('details', null, 'planet-job-category'); details.open = expandedJobs.has(category); details.dataset.jobCategory = category;
            // Ключи social classes подтверждены в штатной localisation/megacorp_l_english.yml.
            const categoryKeys = { worker: 'pop_cat_worker', specialist: 'pop_cat_specialist', ruler: 'pop_cat_ruler', civilian: 'pop_cat_civilian' };
            const categoryKey = values[0]?.sourceSocialClassId != null
                ? categoryKeys[values[0].sourceSocialClassId]
                : values[0]?.categoryLocalizationKey ?? categoryKeys[category];
            const categoryHeading = node('summary');
            const classTitle = categoryKey ? resolveLocalization(categoryKey, labels[category] ?? category) : labels[category] ?? category;
            const classIconKey = values.find(job => job.socialClassImageDefinitionKey)?.socialClassImageDefinitionKey;
            if (classIconKey) { art(categoryHeading, classIconKey); }
            categoryHeading.append(node('span', `${classTitle} · ${values.length} типов`));
            const overview = node('span', null, 'planet-job-overview');
            for (const job of values)
            {
                const tile = node('span', null, 'planet-job-tile'); tile.title = jobName(job);
                if (developmentJobs) { art(tile, job.imageDefinitionKey ?? resolveImageKey('job', job.jobId)); }
                tile.append(node('span', jobName(job)), node('strong', quantity(job.assigned))); overview.append(tile);
            }
            categoryHeading.append(overview); details.append(categoryHeading);
            const rows = node('div', null, 'planet-job-rows');
            for (const job of values)
            {
                const value = node('article', null, 'planet-job');
                const heading = node('div', null, 'planet-job-heading');
                if (developmentJobs) { art(heading, job.imageDefinitionKey ?? resolveImageKey('job', job.jobId)); }
                heading.append(node('strong', jobName(job)));
                if (job.descriptionKey) { heading.title = resolveLocalization(job.descriptionKey, `Описание недоступно: ${job.descriptionKey}`); }
                value.append(heading);
                const values = node('div', null, 'planet-job-values'); values.append(node('span', `Назначено ${quantity(job.assigned)}`));
                if (developmentJobs)
                {
                    values.append(node('span', `Вместимость ${quantity(job.capacity)} · лимит ${quantity(job.limit)}`));
                    if (liveEconomy)
                    {
                        if (Number.isFinite(job.capacity) && job.capacity > 0 && Number.isFinite(job.assigned))
                        {
                            const meter = node('meter', null, 'planet-workforce-meter'); meter.min = '0'; meter.max = String(job.capacity); meter.value = String(job.assigned);
                            meter.setAttribute('aria-label', `Обычная Workforce ${quantity(job.assigned)} из вместимости ${quantity(job.capacity)}`); values.append(meter);
                        }
                        else { values.append(node('span', 'Шкала недоступна', 'planet-diagnostic')); }
                        const extra = node('details', null, 'planet-workforce-details'); extra.append(node('summary', 'Подробности Workforce'), node('span', `Обычная ${quantity(job.assigned)} · бонусная ${quantity(job.bonusWorkforce)} · эффективная ${quantity(job.effectiveWorkforce)}`)); values.append(extra);
                    }
                    else { values.append(node('span', `Workforce ${quantity(job.workforce)}`)); }
                }
                value.append(values);
                if (liveEconomy) { appendWorkforce(value, job); }
                rows.append(value);
            }
            details.append(rows);
            details.addEventListener('toggle', () => { if (details.open) { expandedJobs.add(category); } else { expandedJobs.delete(category); } });
            nodes.push(details);
        }
        if (!jobs.length) { nodes.push(node('p', 'Данные профессий недоступны.')); }
        const controls = node('section', null, 'planet-vacancies');
        controls.append(node('h3', 'Настройка вакансий'), node('p', 'Изменения учитываются при следующем месячном расчёте.'));
        let appended = false;
        if (!liveEconomy) { for (const job of model.economy?.jobs ?? []) { appended = appendVacancies(controls, job) || appended; } }
        if (appended) { nodes.push(controls); }
    }
    else if (tab === 'holdings' || tab === 'armies')
    {
        nodes.push(node('p', tab === 'holdings' ? 'Владения недоступны: сервер пока не передаёт владения и права управления ими.' : 'Армии недоступны: сервер пока не передаёт данные армий и Arkship.', 'planet-notice'));
    }
    if (tab !== 'infrastructure')
    {
        const economyDetails = node('details', null, 'planet-secondary-economy'); economyDetails.append(node('summary', 'Экономика колонии'), settlement); nodes.push(economyDetails);
    }
    root.replaceChildren(...nodes);
    panelScrollContexts.set(root, { identity: scrollContext, inspection: inspectionContext });
    for (const [selector, top] of scrollPositions)
    {
        const target = root.querySelector?.(selector);
        if (target && Number.isFinite(top)) { target.scrollTop = top; }
    }
    if (focusIdentity != null && previousContext?.identity === scrollContext)
    {
        const buttons = [...(root.querySelectorAll?.('.planet-context button') ?? [])].filter(button => !button.disabled);
        const next = buttons.find(button => buttonIdentity(button) === focusIdentity) ?? buttons[Math.min(focusIndex, buttons.length - 1)];
        next?.focus?.({ preventScroll: true });
    }
}
