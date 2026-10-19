import test from 'node:test';
import assert from 'node:assert/strict';
import { renderPlanetWindowView } from '../../src/Stellaris.Server/wwwroot/m3-planet-window-view.mjs';

class FakeElement
{
    constructor(tagName, document)
    {
        this.tagName = tagName.toUpperCase(); this.tag = tagName; this.ownerDocument = document;
        this.children = []; this.parentNode = null; this.dataset = {}; this.attributes = {};
        this._text = ''; this.className = ''; this.scrollTop = 0; this.disabled = false;
    }
    get textContent() { return this._text + this.children.map(child => child.textContent).join(''); }
    set textContent(value) { this.replaceChildren(); this._text = String(value ?? ''); }
    get classList() { return { contains: name => this.className.split(/\s+/).includes(name) }; }
    append(...nodes) { for (const child of nodes) { if (child instanceof FakeElement) { child.remove(); child.parentNode = this; this.children.push(child); } else { this._text += String(child); } } }
    replaceChildren(...nodes)
    {
        const replacements = [...nodes];
        for (const child of this.children) child.remove();
        this.children = []; this._text = '';
        this.append(...replacements);
    }
    remove()
    {
        const parent = this.parentNode;
        if (parent) { parent.children = parent.children.filter(child => child !== this); this.parentNode = null; }
        const active = this.ownerDocument?.activeElement;
        for (let current = active; current; current = current.parentNode) { if (current === this) { this.ownerDocument.activeElement = null; break; } }
    }
    setAttribute(name, value) { this.attributes[name] = String(value); if (name === 'aria-label') this.ariaLabel = String(value); }
    getAttribute(name) { return Object.hasOwn(this.attributes, name) ? this.attributes[name] : null; }
    focus() { this.ownerDocument.activeElement = this; }
    matchesClass(name) { return this.className.split(/\s+/).includes(name); }
    all() { return this.children.flatMap(child => [child, ...child.all()]); }
    querySelectorAll(selector)
    {
        const descendants = this.all();
        if (selector === '.planet-context button')
        {
            const pane = descendants.find(item => item.matchesClass('planet-context'));
            return pane?.all().filter(item => item.tagName === 'BUTTON') ?? [];
        }
        if (selector.startsWith('.')) { const name = selector.slice(1); return descendants.filter(item => item.matchesClass(name)); }
        return [];
    }
    querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
}

const makeDocument = () =>
{
    const document = { activeElement: null, createElement(tag) { return new FakeElement(tag, document); } };
    return document;
};
const has = (element, name) => element?.className?.split(/\s+/).includes(name) ?? false;
const descendants = element => element.all();
const one = (element, name) => descendants(element).find(child => has(child, name));
const many = (element, name) => descendants(element).filter(child => has(child, name));
const imageAppender = (document, keys = []) => (parent, key) =>
{
    if (!key) return false;
    const image = document.createElement('img'); image.className = 'planet-art'; image.dataset.imageKey = key;
    parent.append(image); keys.push(key); return true;
};

function setDistrictPool(dto, { capacity = 8, districtQueue = [{ definitionId: 'district_city' }, { definitionId: 'district_mining' }] } = {})
{
    dto.installed.push({ slotId: 'installed-city', kind: 'district', definitionId: 'district_city' }, { slotId: 'installed-mine', kind: 'district', definitionId: 'district_mining' });
    dto.queue = districtQueue.map((item, index) => ({ orderId: `district-order-${index}`, kind: 'district', status: 'pending', gates: [], ...item }));
    const installed = dto.installed.filter(item => item.kind === 'district').length;
    const reserved = dto.queue.filter(item => item.kind === 'district' && item.status === 'pending').length;
    dto.districtPool = { policyId: 'ordinary-district-slot-pool-v1', capacity, installed, reserved, free: capacity - installed - reserved };
}

function setDistrictTypePools(dto, { uncapped = false } = {})
{
    dto.startupDistricts.push({ instanceId: 'generator-instance', definitionId: 'district_generator', level: 1 });
    dto.installed.push({ slotId: 'type-city', kind: 'district', definitionId: 'district_city' }, { slotId: 'type-mine', kind: 'district', definitionId: 'district_mining' }, { slotId: 'type-generator', kind: 'district', definitionId: 'district_generator' });
    dto.queue = ['district_city', 'district_mining', 'district_generator'].map((definitionId, index) => ({ orderId: `type-order-${index}`, kind: 'district', definitionId, status: 'pending', gates: [] }));
    dto.districtPool = { policyId: 'ordinary-district-slot-pool-v1', capacity: 10, installed: 3, reserved: 3, free: 4 };
    const memberDefinitionIds = ['district_mining', 'district_generator'];
    const common = { policyId: 'ordinary-source-district-type-pool-v1', sharedPoolId: 'resource-deposits', uncapped, capacity: uncapped ? null : 6, installed: 2, reserved: 2, free: uncapped ? null : 2, memberDefinitionIds };
    dto.districtTypePools = memberDefinitionIds.map(definitionId => ({ ...structuredClone(common), definitionId }));
}

function fixture(id = 'colony-1')
{
    const city = { instanceId: 'city-instance', definitionId: 'district_city', level: 3, imageDefinitionKey: 'image:city' };
    const rural = { instanceId: 'mine-instance', definitionId: 'district_mining', level: 2, imageDefinitionKey: 'image:mining' };
    const dto = {
        revision: 4, gates: [],
        slots: [
            { slotId: 'base-zone', kind: 'specialization', zoneInstanceId: null, installedDefinitionId: null, reservedByOrderId: null, gates: [] },
            { slotId: 'research-zone', kind: 'specialization', installedDefinitionId: null, reservedByOrderId: null, gates: [] },
            { slotId: 'base-building', kind: 'building', zoneInstanceId: 'base-instance', installedDefinitionId: 'building_capital', reservedByOrderId: null, gates: [] },
            { slotId: 'research-building', kind: 'building', zoneInstanceId: 'research-instance', installedDefinitionId: 'building_research_lab_1', reservedByOrderId: null, gates: [] },
            { slotId: 'free-building', kind: 'building', installedDefinitionId: null, reservedByOrderId: null, gates: [] },
            { slotId: 'district-target', kind: 'district', installedDefinitionId: null, reservedByOrderId: null, gates: [] },
        ],
        installed: [
            { slotId: 'base-building', kind: 'building', definitionId: 'building_capital', instanceId: 'capital-instance', imageDefinitionKey: 'image:capital' },
            { slotId: 'research-building', kind: 'building', definitionId: 'building_research_lab_1', instanceId: 'lab-instance', imageDefinitionKey: 'image:lab' },
        ],
        catalog: [
            { definitionId: 'district_city', kind: 'district', slotIds: ['district-target'], compatibleSlotIds: ['district-target'], eligibility: 'available', canAfford: true, gates: [], quote: { policyId: 'quote-city', durationDays: 30, costs: {}, infrastructureUpkeep: {}, jobCapacity: {} } },
            { definitionId: 'building_new', kind: 'building', slotIds: ['free-building'], compatibleSlotIds: ['free-building'], eligibility: 'available', canAfford: true, gates: [], imageDefinitionKey: 'image:new-building', quote: { policyId: 'quote-building', durationDays: 20, costs: { minerals: 10 }, infrastructureUpkeep: {}, jobCapacity: {} } },
        ],
        queue: [], jobs: [], features: [],
        startupDistricts: [city, rural],
        startupZones: [
            { instanceId: 'base-instance', districtInstanceId: city.instanceId, slotInstanceId: 'base-zone', zoneId: 'zone_default', slotNumber: 0 },
            { instanceId: 'research-instance', districtInstanceId: city.instanceId, slotInstanceId: 'research-zone', zoneId: 'zone_research_unity', slotNumber: 1 },
        ],
        startupBuildings: [
            { instanceId: 'base-capital-start', definitionId: 'building_capital', slotInstanceId: 'base-building', zoneId: 'zone_default', compatibleZoneInstanceIds: ['base-instance'], imageDefinitionKey: 'image:capital' },
            { instanceId: 'research-lab-start', definitionId: 'building_research_lab_1', slotInstanceId: 'research-building', zoneId: 'zone_research_unity', compatibleZoneInstanceIds: ['research-instance'], imageDefinitionKey: 'image:lab' },
        ],
        startupFeatures: [], startupDistrictCaps: [],
    };
    const model = { id, ownerId: 'player-1', name: 'Митра', economy: { population: 12, workforce: 8 }, development: dto, gates: [] };
    return { model, dto };
}

function setZoneReplacement(dto, { pending = false, gates = [], removed = ['building-zone-factory'] } = {})
{
    const instanceId = 'mine-zone-instance', zoneSlotId = 'mine-zone-slot';
    const children = [
        { slotId: 'building-zone-factory', definitionId: 'building_factory_1', localizationKey: 'building_factory_name', imageDefinitionKey: 'image:zone-factory' },
        { slotId: 'building-zone-foundry', definitionId: 'building_foundry_1', localizationKey: 'building_foundry_name', imageDefinitionKey: 'image:zone-foundry-building' },
        { slotId: 'building-zone-keep', definitionId: 'building_stronghold', localizationKey: 'building_keep_name', imageDefinitionKey: 'image:zone-keep' },
    ];
    const removedBuildings = removed.map(slotId => {
        const child = children.find(item => item.slotId === slotId);
        return { slotId, definitionId: child.definitionId, localizationKey: child.localizationKey };
    });
    const zone = { instanceId, districtInstanceId: 'mine-instance', slotInstanceId: zoneSlotId, zoneId: 'zone_industrial', slotNumber: 0, imageDefinitionKey: 'image:zone-industrial' };
    const replacement = {
        sourceDefinitionId: 'zone_industrial', definitionId: 'zone_foundry', slotId: zoneSlotId, zoneInstanceId: instanceId,
        expectedColonyRevision: dto.revision, previewId: 'zone-replacement-preview', removedBuildings,
        quote: { policyId: 'zone-replacement-policy', durationDays: 48, costs: { minerals: 125 }, infrastructureUpkeep: {}, jobCapacity: {} },
        canAfford: true, canEnqueue: gates.length === 0, gates, localizationKey: 'zone_foundry_name'
    };
    dto.slots.push({ slotId: zoneSlotId, kind: 'zone', zoneInstanceId: instanceId, districtInstanceId: 'mine-instance', zoneSlotDefinitionId: 'zone_slot_01',
        zoneDefinitionId: 'zone_industrial', installedDefinitionId: 'zone_industrial', reservedByOrderId: pending ? 'zone-replacement-order' : null, gates: [] });
    for (const child of children)
    {
        dto.slots.push({ slotId: child.slotId, kind: 'building', zoneInstanceId: instanceId, zoneDefinitionId: 'zone_industrial',
            installedDefinitionId: child.definitionId, reservedByOrderId: null, gates: [] });
        dto.installed.push({ slotId: child.slotId, kind: 'building', definitionId: child.definitionId, localizationKey: child.localizationKey, imageDefinitionKey: child.imageDefinitionKey });
        dto.startupBuildings.push({ instanceId: child.slotId + '-source', definitionId: child.definitionId, slotInstanceId: child.slotId,
            zoneId: 'zone_industrial', compatibleZoneInstanceIds: [instanceId], localizationKey: child.localizationKey, imageDefinitionKey: child.imageDefinitionKey });
    }
    Object.assign(dto.slots.find(item => item.slotId === 'base-zone'), { kind: 'zone', zoneInstanceId: 'base-instance', districtInstanceId: 'city-instance',
        zoneSlotDefinitionId: 'zone_slot_01', zoneDefinitionId: 'zone_default', installedDefinitionId: 'zone_default' });
    Object.assign(dto.slots.find(item => item.slotId === 'research-zone'), { kind: 'zone', zoneInstanceId: 'research-instance', districtInstanceId: 'city-instance',
        zoneSlotDefinitionId: 'zone_slot_01', zoneDefinitionId: 'zone_research_unity', installedDefinitionId: 'zone_research_unity' });
    dto.installed.push({ slotId: zoneSlotId, kind: 'zone', definitionId: 'zone_industrial', localizationKey: 'zone_industrial_name', imageDefinitionKey: 'image:zone-industrial', zoneReplacements: [replacement] });
    dto.startupZones.push(zone);
    dto.catalog.push({ definitionId: 'zone_foundry', kind: 'zone', slotIds: [zoneSlotId], compatibleSlotIds: [zoneSlotId], eligibility: 'available', canAfford: true, gates: [],
        imageDefinitionKey: 'image:zone-foundry', localizationKey: 'zone_foundry_name', quote: replacement.quote });
    if (pending)
    {
        dto.queue.push({ orderId: 'zone-replacement-order', slotId: zoneSlotId, definitionId: 'zone_foundry', kind: 'zone', status: 'pending',
            localizationKey: 'zone_foundry_name', completedWork: 25, requiredWork: 50, workUnit: 'baseDays', gates: [], refundQuote: { policyId: 'refund', amounts: {} },
            zoneReplacement: { sourceDefinitionId: 'zone_industrial', zoneInstanceId: instanceId, removedBuildings } });
    }
    return { zone, replacement, children };
}

function setBuildingReplacement(dto, { pending = true, slotId = 'research-building', zoneInstanceId = 'research-instance', zoneSlotId = 'research-zone', materializeZoneSlot = true, targetDefinitionId = 'building_research_lab_2' } = {})
{
    const slot = dto.slots.find(item => item.slotId === slotId), zone = dto.startupZones.find(item => item.instanceId === zoneInstanceId);
    if (materializeZoneSlot) Object.assign(dto.slots.find(item => item.slotId === zoneSlotId), { kind: 'zone', zoneInstanceId, districtInstanceId: zone.districtInstanceId,
        zoneSlotDefinitionId: 'zone_slot_01', zoneDefinitionId: zone.zoneId, installedDefinitionId: zone.zoneId });
    slot.zoneDefinitionId = zone.zoneId;
    const source = dto.installed.find(item => item.kind === 'building' && item.slotId === slot.slotId);
    source.localizationKey = 'lab_source_name';
    dto.startupBuildings.find(item => item.slotInstanceId === slot.slotId).localizationKey = 'lab_source_name';
    const replacement = { sourceDefinitionId: source.definitionId, zoneInstanceId };
    dto.catalog.push({ definitionId: targetDefinitionId, kind: 'building', slotIds: [slot.slotId], compatibleSlotIds: [slot.slotId], eligibility: 'available', canAfford: true,
        gates: [], imageDefinitionKey: 'image:replacement-lab', localizationKey: 'lab_target_name', quote: { policyId: 'replacement-quote', durationDays: 40, costs: {}, infrastructureUpkeep: {}, jobCapacity: {} } });
    if (pending)
    {
        slot.reservedByOrderId = 'building-replacement-order';
        dto.queue.push({ orderId: 'building-replacement-order', slotId: slot.slotId, definitionId: targetDefinitionId, kind: 'building', status: 'pending',
            localizationKey: 'lab_target_name', completedWork: 25, requiredWork: 50, gates: [], refundQuote: { policyId: 'refund', amounts: {} }, buildingReplacement: replacement });
    }
    return { slot, source, replacement };
}

function render(root, document, model, overrides = {})
{
    const keys = [];
    renderPlanetWindowView(root, model, {
        document,
        resolveImageKey: (kind, id) => `resolved:${kind}:${id}`,
        appendImage: imageAppender(document, keys),
        resolveLocalization: (key, fallback) => fallback ?? key,
        ...overrides,
    });
    return keys;
}

test('очередь показывает состояния и соседние стрелки только при policy обычной очереди', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    dto.queue = ['active-order', 'waiting-a', 'waiting-b', 'waiting-c'].map(orderId => ({ orderId, slotId: `slot-${orderId}`, definitionId: 'building_new', kind: 'building', status: 'pending', completedWork: 0, requiredWork: 10, gates: [], refundQuote: { policyId: 'refund', amounts: {} } }));
    dto.queuePolicy = { policyId: 'ordinary-sequential-v1', activeCapacity: 1, canReorder: true };
    const received = [];
    const appendConstruction = (parent, identity) => { received.push(identity); const button = document.createElement('button'); button.dataset.orderId = identity.orderId; button.dataset.direction = identity.direction; parent.append(button); return true; };
    render(root, document, model, { appendConstruction });
    assert.equal(many(root, 'pm-order').map(row => row.children[1].textContent).join(','), 'Строится,Ожидает,Ожидает,Ожидает');
    assert.deepEqual(received.filter(item => item.direction != null), [
        { orderId: 'waiting-a', direction: 'down' },
        { orderId: 'waiting-b', direction: 'up' },
        { orderId: 'waiting-b', direction: 'down' },
        { orderId: 'waiting-c', direction: 'up' }
    ]);

    dto.queuePolicy = { policyId: 'unknown', activeCapacity: 1, canReorder: true }; received.length = 0;
    render(root, document, model, { appendConstruction });
    assert.equal(many(root, 'pm-order').map(row => row.children[1].textContent).join(','), 'В очереди,В очереди,В очереди,В очереди');
    assert.equal(received.some(item => item.direction != null), false);
});

test('pending building replacement показывает source/target 50/50, progress и старое disabled состояние на выбранном slot', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { slot } = setBuildingReplacement(dto);
    const inspection = { kind: 'installed', slotId: slot.slotId, definitionId: 'building_research_lab_1' };
    const localization = { lab_source_name: 'Исследовательская лаборатория', lab_target_name: 'Передовая лаборатория' };
    const installed = dto.installed.find(item => item.kind === 'building' && item.slotId === slot.slotId); installed.enabled = false;
    render(root, document, model, { inspection, resolveLocalization: (key, fallback) => localization[key] ?? fallback ?? key });
    const card = many(root, 'pm-slot').find(item => item.dataset.slotId === slot.slotId);
    assert.ok(has(card, 'is-replacing') && has(card, 'is-selected'));
    const split = one(card, 'pm-slot-replacement'); assert.ok(split); assert.equal(split.children.length, 2);
    assert.deepEqual(descendants(split).filter(item => item.tagName === 'IMG').map(item => item.dataset.imageKey), ['image:lab', 'image:replacement-lab']);
    assert.equal(one(card, 'pm-slot-progress').value, 25); assert.equal(one(card, 'pm-slot-progress').max, 50);
    assert.ok(one(card, 'pm-slot-disabled-badge'), 'старый instance остаётся выключенным до completion');
    for (const value of Object.values(localization)) { assert.ok(card.attributes['aria-label'].includes(value)); assert.ok(card.title.includes(value)); }
    assert.ok(card.attributes['aria-label'].includes('Выключено'));
});

test('каноническая default zone допускает building replacement без отдельного materialized zone slot', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { slot } = setBuildingReplacement(dto, { slotId: 'base-building', zoneInstanceId: 'base-instance', zoneSlotId: 'base-zone', materializeZoneSlot: false, targetDefinitionId: 'building_capital_alt' });
    assert.notEqual(dto.slots.find(item => item.slotId === 'base-zone').kind, 'zone', 'fixture сохраняет отсутствие materialized default zone slot');
    render(root, document, model);
    const card = many(root, 'pm-slot').find(item => item.dataset.slotId === slot.slotId);
    assert.ok(one(card, 'pm-slot-replacement'));
    assert.deepEqual(descendants(one(card, 'pm-slot-replacement')).filter(item => item.tagName === 'IMG').map(item => item.dataset.imageKey), ['image:capital', 'image:replacement-lab']);
});

test('cancel возвращает source art, completion показывает новое включённое здание на том же выбранном месте', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { slot } = setBuildingReplacement(dto);
    const inspection = { kind: 'installed', slotId: slot.slotId, definitionId: 'building_research_lab_1' };
    const source = dto.installed.find(item => item.kind === 'building' && item.slotId === slot.slotId); source.enabled = false;
    const renderSelected = () => render(root, document, model, { inspection });

    dto.queue[0].status = 'cancelled'; slot.reservedByOrderId = null; renderSelected();
    let card = many(root, 'pm-slot').find(item => item.dataset.slotId === slot.slotId);
    assert.equal(one(card, 'pm-slot-replacement'), undefined); assert.equal(one(card, 'pm-slot-progress'), undefined);
    assert.equal(one(card, 'pm-slot-art').children[0].dataset.imageKey, 'image:lab'); assert.ok(one(card, 'pm-slot-disabled-badge')); assert.ok(has(card, 'is-selected'));

    dto.queue[0].status = 'completed'; slot.installedDefinitionId = 'building_research_lab_2';
    source.definitionId = 'building_research_lab_2'; source.enabled = true; source.imageDefinitionKey = 'image:replacement-lab'; source.localizationKey = 'lab_target_name';
    dto.startupBuildings.find(item => item.slotInstanceId === slot.slotId).definitionId = 'building_research_lab_2';
    renderSelected(); card = many(root, 'pm-slot').find(item => item.dataset.slotId === slot.slotId);
    assert.equal(one(card, 'pm-slot-replacement'), undefined); assert.equal(one(card, 'pm-slot-progress'), undefined);
    assert.equal(one(card, 'pm-slot-art').children[0].dataset.imageKey, 'image:replacement-lab');
    assert.equal(one(card, 'pm-slot-disabled-badge'), undefined); assert.ok(has(card, 'is-selected'));
});

test('повреждённый или неоднозначный building replacement не показывает split или target preview', () =>
{
    const mutations = [
        ({ slot, source, replacement, order }) => { replacement.sourceDefinitionId = 'building_fake_source'; },
        ({ slot, replacement }) => { replacement.zoneInstanceId = 'foreign-zone'; },
        ({ order }) => { order.definitionId = null; },
        ({ order }) => { order.definitionId = ''; },
        ({ order }) => { order.definitionId = 7; },
        ({ order }) => { order.orderId = null; },
        ({ order }) => { order.orderId = ''; },
        ({ source, order }) => { order.definitionId = source.definitionId; },
        ({ order }) => { order.upgradeFromDefinitionId = 'building_research_lab_0'; },
        ({ order }) => { order.zoneReplacement = { sourceDefinitionId: 'zone_research_unity', zoneInstanceId: 'research-instance', removedBuildings: [] }; },
        ({ slot, dto }) => { slot.zoneInstanceId = 'foreign-zone'; },
        ({ slot }) => { slot.zoneDefinitionId = 'zone_other'; },
        ({ dto }) => { dto.startupZones = dto.startupZones.filter(zone => zone.instanceId !== 'research-instance'); },
        ({ dto }) => { dto.slots.find(item => item.slotId === 'research-zone').zoneInstanceId = 'foreign-zone'; },
        ({ dto }) => { dto.slots.push({ ...dto.slots.find(item => item.slotId === 'research-zone') }); },
        ({ dto }) => { dto.startupZones.find(zone => zone.instanceId === 'research-instance').slotInstanceId = ''; },
        ({ dto, order }) => { dto.queue.push({ ...order, orderId: 'second-building-replacement' }); },
    ];
    for (const mutate of mutations)
    {
        const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
        const { slot, source, replacement } = setBuildingReplacement(dto);
        const order = dto.queue[0]; mutate({ dto, slot, source, replacement, order });
        render(root, document, model);
        const card = many(root, 'pm-slot').find(item => item.dataset.slotId === slot.slotId);
        assert.equal(one(card, 'pm-slot-replacement'), undefined);
        assert.equal(descendants(card).some(item => item.dataset.imageKey === 'image:replacement-lab'), false);
    }
});

test('live оценка показывает серверные дни собственной работы, сохраняя тот же заказ и прогресс', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const order = { orderId: 'live-order', slotId: 'slot-live', definitionId: 'building_new', kind: 'building', status: 'pending', completedWork: 4, requiredWork: 10,
        executionState: 'active', workEstimatePolicyId: 'accepted-daily-work-v1', dailyWork: 2, remainingWorkDays: 3, gates: [], refundQuote: { policyId: 'refund', amounts: {} } };
    dto.queue = [order];
    render(root, document, model);
    const row = one(root, 'pm-order');
    assert.equal(row.dataset.orderId, 'live-order'); assert.equal(row.children[1].textContent, 'Строится');
    assert.equal(row.children[3].textContent, '◷ 3 дн. работы'); assert.equal(row.children.find(child => child.tagName === 'PROGRESS').value, 4);

    order.completedWork = 6; order.remainingWorkDays = 2;
    render(root, document, model);
    const updated = one(root, 'pm-order');
    assert.equal(updated.dataset.orderId, 'live-order'); assert.equal(updated.children[3].textContent, '◷ 2 дн. работы');
    assert.equal(updated.children.find(child => child.tagName === 'PROGRESS').value, 6);
});

test('waiting показывает только собственные рабочие дни, а неизвестные и приостановленные сроки не превращаются в ETA', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const makeOrder = (orderId, executionState, extra = {}) => ({ orderId, slotId: `slot-${orderId}`, definitionId: 'building_new', kind: 'building', status: 'pending', completedWork: 0,
        requiredWork: 10, executionState, workEstimatePolicyId: 'accepted-daily-work-v1', dailyWork: 1, remainingWorkDays: 0, gates: [], refundQuote: { policyId: 'refund', amounts: {} }, ...extra });
    dto.queue = [
        makeOrder('waiting-order', 'waiting', { remainingWorkDays: 7 }),
        makeOrder('paused-order', 'paused', { dailyWork: null, remainingWorkDays: null }),
        makeOrder('unknown-speed', 'active', { dailyWork: 0, remainingWorkDays: null }),
        makeOrder('unknown-policy', 'active', { workEstimatePolicyId: null }),
        { ...makeOrder('legacy-order', null), workEstimatePolicyId: null },
        { ...makeOrder('done-order', 'active'), status: 'completed' },
        { ...makeOrder('cancel-order', 'active'), status: 'cancelled' },
    ];
    dto.queuePolicy = { policyId: 'ordinary-sequential-v1', activeCapacity: 1, canReorder: true };
    render(root, document, model);
    const rows = many(root, 'pm-order');
    assert.deepEqual(rows.map(row => row.dataset.orderId), ['waiting-order', 'paused-order', 'unknown-speed', 'unknown-policy', 'legacy-order']);
    assert.equal(rows[0].children[1].textContent, 'Ожидает'); assert.equal(rows[0].children[3].textContent, 'Собственная работа: ◷ 7 дн. работы');
    assert.equal(rows[1].children[1].textContent, 'Приостановлено'); assert.equal(rows[1].children[3].textContent, 'Срок неизвестен');
    assert.equal(rows[2].children[3].textContent, 'Срок неизвестен'); assert.equal(rows[3].children[1].textContent, 'Ожидает');
    assert.equal(rows[3].children.filter(child => child.tagName === 'SMALL').some(child => child.textContent.includes('дн. работы') || child.textContent === 'Срок неизвестен'), false);
    assert.equal(rows[4].children[1].textContent, 'Ожидает'); assert.equal(rows[4].children.some(child => child.textContent.includes('дн. работы')), false);
});

test('показывает согласованный общий district pool и предел единственного city с pending районами отдельно от installed', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    setDistrictPool(dto);
    render(root, document, model, { tab: 'infrastructure' });
    const summary = one(root, 'pm-district-pool');
    assert.equal(summary.textContent, 'Свободных районов: 4'); assert.equal(summary.attributes['aria-label'], 'Районы: 2 + 2 / 8 · свободно: 4');
    assert.equal(one(root, 'pm-city').children.find(item => has(item, 'pm-district-count')).textContent, '1 + 1 / 6');
    assert.equal(dto.districtPool.installed, 2, 'ожидаемые заказы не добавляются в installed');
});

test('без точной согласованной pool policy общий счётчик и city limit остаются неизвестны', () =>
{
    for (const change of ['missing', 'policy', 'free', 'installed', 'reserved'])
    {
        const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture(); setDistrictPool(dto);
        if (change === 'missing') { delete dto.districtPool; }
        if (change === 'policy') { dto.districtPool.policyId = 'unknown-pool'; }
        if (change === 'free') { dto.districtPool.free++; }
        if (change === 'installed') { dto.districtPool.installed++; }
        if (change === 'reserved') { dto.districtPool.reserved++; }
        render(root, document, model, { tab: 'infrastructure' });
        assert.equal(one(root, 'pm-district-pool').textContent, 'Свободных районов: —', change);
        assert.ok(one(root, 'pm-city').children.find(item => has(item, 'pm-district-count')).textContent.endsWith('/ —'), change);
    }
});

test('не приписывает общий city предел нескольким экземплярам и сохраняет его неизвестным для других классов', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture(); setDistrictPool(dto);
    dto.startupDistricts.push({ instanceId: 'city-instance-2', definitionId: 'district_city', level: 1 });
    render(root, document, model);
    assert.ok(many(root, 'pm-city').every(card => one(card, 'pm-district-count').textContent.endsWith('/ —')));
    assert.ok(many(root, 'pm-rural').every(card => one(card, 'pm-district-count').textContent.endsWith('/ —')));
});

test('показывает shared type pool installed и pending для всех его rural definitions, сохраняя общий city pool', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture(); setDistrictTypePools(dto);
    render(root, document, model);
    const mining = many(root, 'pm-rural').find(card => card.dataset.districtDefinition === 'district_mining');
    const generator = many(root, 'pm-rural').find(card => card.dataset.districtDefinition === 'district_generator');
    assert.equal(one(mining, 'pm-district-count').textContent, '2 + 2 / 6');
    assert.equal(one(generator, 'pm-district-count').textContent, '2 + 2 / 6');
    assert.equal(one(root, 'pm-city').children.find(item => has(item, 'pm-district-count')).textContent, '1 + 1 / 6');
    assert.equal(dto.districtTypePools[0].installed, 2, 'число установки не включает ожидаемые уровни');
});

test('невалидное или неоднозначное type-pool binding оставляет rural count неизвестным', () =>
{
    for (const change of ['policy', 'duplicate-members', 'missing-definition', 'duplicate-binding', 'installed', 'reserved', 'free'])
    {
        const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture(); setDistrictTypePools(dto);
        const pool = dto.districtTypePools[0];
        if (change === 'policy') { pool.policyId = 'unknown'; }
        if (change === 'duplicate-members') { pool.memberDefinitionIds = ['district_mining', 'district_mining']; }
        if (change === 'missing-definition') { pool.memberDefinitionIds = ['district_generator']; }
        if (change === 'duplicate-binding') { dto.districtTypePools.push(structuredClone(pool)); }
        if (change === 'installed') { pool.installed++; }
        if (change === 'reserved') { pool.reserved++; }
        if (change === 'free') { pool.free++; }
        render(root, document, model);
        const mining = many(root, 'pm-rural').find(card => card.dataset.districtDefinition === 'district_mining');
        assert.equal(one(mining, 'pm-district-count').textContent, '— / —', change);
        assert.equal(one(root, 'pm-city').children.find(item => has(item, 'pm-district-count')).textContent, '1 + 1 / 6', 'общий city pool остаётся независимым');
    }
});

test('uncapped type pool отображается как бесконечный предел', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture(); setDistrictTypePools(dto, { uncapped: true });
    render(root, document, model);
    const mining = many(root, 'pm-rural').find(card => card.dataset.districtDefinition === 'district_mining');
    assert.equal(one(mining, 'pm-district-count').textContent, '2 + 2 / ∞');
    assert.equal(one(root, 'pm-city').children.find(item => has(item, 'pm-district-count')).textContent, '1 + 1 / 6');
});

test('рендер собирает две колонки, выносит инспектор отдельным соседом и не дублирует зоны или здания', () =>
{
    const document = makeDocument(); const root = document.createElement('div'); const { model } = fixture();
    render(root, document, model, { inspection: { kind: 'slot', slotId: 'free-building' }, appendConstruction: (parent, identity) => { const button = document.createElement('button'); button.dataset.planetActionIdentity = identity.definitionId; parent.append(button); return true; } });

    const workspace = one(root, 'pm-workspace'); const children = workspace.children;
    assert.equal(children.length, 2);
    assert.ok(has(children[0], 'pm-main'));
    assert.ok(has(children[1], 'pm-side'));
    const pane = one(root, 'planet-context');
    assert.equal(pane.parentNode, root, 'каталог остаётся sibling компоновки main/side');
    assert.equal(many(root, 'pm-slot').find(item => item.dataset.slotId === 'free-building').matchesClass('is-selected'), true);
    assert.equal(one(root, 'pm-main').id, 'planet-page');
    assert.equal(many(root, 'pm-zone-surface').length, 1, 'обычная зона отображается один раз');
    assert.equal(many(root, 'pm-slot').filter(card => ['base-building', 'research-building'].includes(card.dataset.slotId)).length, 2);
    assert.equal(one(root, 'pm-base-slots')?.children.filter(child => child.dataset.slotId === 'base-building').length, 1);
    assert.equal(one(root, 'pm-city-zones')?.children.length, 1);
});

test('сохраняет реальные изображения в заголовке района и карточке каталога', () =>
{
    const document = makeDocument(); const root = document.createElement('div'); const { model } = fixture(); const keys = render(root, document, model, { inspection: { kind: 'slot', slotId: 'free-building' } });
    const districtTitle = one(root, 'pm-district-title');
    assert.ok(descendants(districtTitle).some(child => child.tagName === 'IMG' && child.dataset.imageKey === 'image:city'));
    const choice = many(root, 'pm-choice').find(item => item.textContent.includes('building_new'));
    assert.ok(descendants(one(choice, 'pm-choice-art')).some(child => child.tagName === 'IMG' && child.dataset.imageKey === 'image:new-building'), 'карточка сохраняет реальный арт в рамке образца');
    assert.ok(keys.includes('image:city') && keys.includes('image:new-building'));
});

test('не приписывает общие уровни и заказы нескольким экземплярам одного района', () =>
{
    const document = makeDocument(); const root = document.createElement('div'); const { model, dto } = fixture();
    dto.startupDistricts.push({ instanceId: 'second-mine', definitionId: 'district_mining', level: 4 });
    dto.installed.push(...Array.from({ length: 6 }, (_, index) => ({ slotId: `mine-${index}`, kind: 'district', definitionId: 'district_mining' })));
    dto.queue.push({ orderId: 'mine-order', slotId: 'district-target', kind: 'district', definitionId: 'district_mining', status: 'pending', gates: [] });
    render(root, document, model);
    const districts = many(root, 'pm-rural').filter(item => item.dataset.districtDefinition === 'district_mining');
    assert.deepEqual(districts.map(item => one(item, 'pm-district-count').textContent), ['2 / —', '4 / —']);
    assert.ok(districts.every(item => one(item, 'pm-meter').children.length === 0), 'общая шкала не приписывается одному экземпляру');
    assert.ok(districts.every(item => item.children.find(child => child.tagName === 'BUTTON').dataset.improve === 'district_mining'));
    assert.ok(many(root, 'pm-order').some(item => item.dataset.orderId === 'mine-order'));
});

test('передаёт точные district definition и slot из разрешённой construction operation', () =>
{
    const document = makeDocument(); const root = document.createElement('div'); const { model } = fixture(); const received = [];
    render(root, document, model, { appendConstruction(parent, identity) { received.push(identity); const button = document.createElement('button'); parent.append(button); return true; } });
    assert.deepEqual(received, [{ definitionId: 'district_city', slotId: 'district-target' }]);
    const before = structuredClone(model.development);
    render(root, document, model, { appendConstruction() { return false; } });
    const improve = many(root, 'pm-district-buttons')[0].children.find(child => child.tagName === 'BUTTON');
    assert.equal(improve.disabled, true, 'без подтверждённого append callback действие явно недоступно');
    assert.deepEqual(model.development, before, 'отрисовка не меняет снимок');
});

test('армии и владения сохраняют честное сообщение о неподдержанных данных', () =>
{
    for (const [tab, phrase] of [['armies', 'Сведения об армиях недоступны'], ['holdings', 'Сведения о владениях и правах управления недоступны']])
    {
        const document = makeDocument(); const root = document.createElement('div'); const { model } = fixture(); let actions = 0;
        render(root, document, model, { tab, appendConstruction() { actions++; return false; } });
        assert.ok(root.textContent.includes(phrase), root.textContent);
        assert.equal(actions, 0);
    }
});

test('при отсутствии development сохраняет unavailable и полученный legacy monthly report', () =>
{
    const document = makeDocument(); const root = document.createElement('div');
    const model = { id: 'legacy-colony', ownerId: 'player-1', gates: [], development: null, economy: { population: 4, reportMonth: 1, report: { produced: { minerals: 8 }, jobUpkeep: {}, buildingUpkeep: {}, upkeep: {}, net: { minerals: 8 } } } };
    render(root, document, model, { tab: 'infrastructure' });
    assert.ok(root.textContent.includes('Развитие колонии пока недоступно: сервер не активировал подтверждённые данные строительства.'), root.textContent);
    assert.ok(root.textContent.includes('Закрытый месяц: 1'), root.textContent);
    assert.ok(root.textContent.includes('8'), root.textContent);
});

test('вся карточка каталога отправляет ровно одну команду точного слота, без вложенных кнопок', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model } = fixture();
    const sent = [];
    render(root, document, model, { inspection: { kind: 'slot', slotId: 'free-building' }, appendConstruction(parent, identity) {
        const control = document.createElement('button'); control.onclick = () => sent.push(identity); parent.append(control);
    } });
    const card = one(root, 'pm-choice');
    assert.equal(card.tagName, 'BUTTON'); assert.equal(descendants(card).filter(item => item.tagName === 'BUTTON').length, 0);
    card.onclick();
    assert.deepEqual(sent, [{ definitionId: 'building_new', slotId: 'free-building' }]);
    assert.equal(model.development.queue.length, 0);
});

test('поверхность зоны принимает click/Enter; вложенный слот не вызывает второй выбор', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model } = fixture(); const requests = [];
    render(root, document, model, { onInspect: value => requests.push(value) });
    const zone = one(root, 'pm-zone-surface'), slot = one(zone, 'pm-slot');
    zone.onclick({ target: zone });
    assert.equal(requests.length, 1); assert.equal(requests[0].zoneInstanceId, 'research-instance');
    slot.onclick(); zone.onclick({ target: slot });
    assert.equal(requests.length, 2); assert.equal(requests[1].slotId, 'research-building');
    let prevented = false;
    zone.onkeydown({ target: zone, key: 'Enter', preventDefault() { prevented = true; } });
    assert.equal(requests.length, 3); assert.equal(prevented, true);
});

test('место ресурсной зоны показывает общий ход стройки, а после завершения — зону и три места зданий', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const slot = { slotId: 'mine-zone-energy', kind: 'zone', districtInstanceId: 'mine-instance', zoneSlotDefinitionId: 'slot_energy', requirementLocalizationKeys: [], gates: [], reservedByOrderId: 'zone-order', installedDefinitionId: null };
    dto.slots.push(slot);
    dto.queue.push({ orderId: 'zone-order', slotId: slot.slotId, definitionId: 'zone_energy', kind: 'zone', status: 'pending', completedWork: 18, requiredWork: 60, gates: [] });
    const requestedImages = [];
    render(root, document, model, { resolveImageKey(kind, id) { requestedImages.push([kind, id]); return kind === 'construction' ? null : `${kind}.${id}`; } });
    let card = one(root, 'pm-resource-zone');
    assert.ok(card, 'показывается карточка занятого resource zone slot');
    assert.equal(card.dataset.zoneInstance, undefined, 'pending не создаёт видимую установленную зону');
    const marker = one(card, 'pm-slot-art');
    assert.equal(marker.textContent, '⚒', 'общий pending-маркер не зависит от image resolver');
    assert.equal(marker.attributes['aria-hidden'], 'true');
    assert.equal(one(card, 'pm-slot-progress').value, 18);
    assert.equal(one(card, 'pm-slot-progress').max, 60);
    assert.equal(requestedImages.some(([kind]) => kind === 'construction'), false, 'для generic pending-маркера art не запрашивается');
    assert.equal(requestedImages.some(([kind, id]) => kind === 'zone' && id === 'zone_energy'), false, 'до completion не запрашивается арт выбранной зоны');
    assert.equal(one(card, 'pm-slots'), undefined, 'до completion building slots отсутствуют');

    dto.queue = [];
    slot.reservedByOrderId = null; slot.installedDefinitionId = 'zone_energy';
    const zone = { instanceId: 'mine-zone-energy/zone', districtInstanceId: 'mine-instance', slotInstanceId: slot.slotId, zoneId: 'zone_energy' };
    dto.startupZones.push(zone);
    for (let index = 0; index < 3; index++) dto.slots.push({ slotId: `${slot.slotId}/building/${index}`, kind: 'building', zoneInstanceId: zone.instanceId, installedDefinitionId: null, reservedByOrderId: null, gates: [] });
    requestedImages.length = 0;
    render(root, document, model, { resolveImageKey(kind, id) { requestedImages.push([kind, id]); return `${kind}.${id}`; } });
    card = many(root, 'pm-resource-zone').find(item => item.dataset.zoneInstance === zone.instanceId);
    assert.ok(card, 'completed runtime zone отображается как существующая zone card');
    assert.equal(many(card, 'pm-slot').length, 3);
});

test('пустое место зоны открывает точный inspector, locked показывает source requirement, неизвестное и повторное остаются закрыты', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const slot = { slotId: 'mine-zone-slot', kind: 'zone', districtInstanceId: 'mine-instance', zoneSlotDefinitionId: 'slot_energy', requirementLocalizationKeys: [], gates: [], reservedByOrderId: null, installedDefinitionId: null };
    dto.slots.push(slot);
    const requests = [];
    render(root, document, model, { onInspect: value => requests.push(value) });
    const emptyCard = many(root, 'pm-resource-zone').find(item => item.dataset.zoneSlotId === slot.slotId);
    one(emptyCard, 'pm-zone-open').onclick();
    assert.deepEqual(requests, [{ kind: 'slot', slotId: slot.slotId }]);
    render(root, document, model, { inspection: { kind: 'slot', slotId: slot.slotId } });
    assert.ok(one(root, 'planet-context').children[0].textContent.includes('Построить зону'));

    slot.gates = [{ code: 'dependency-unresolved', dependencyId: 'zone-slot-locked' }]; slot.requirementLocalizationKeys = ['ZONE_REQUIREMENT_KEY'];
    render(root, document, model, { resolveLocalization: (key, fallback) => key === 'ZONE_REQUIREMENT_KEY' ? 'Требование источника' : fallback ?? key });
    let card = many(root, 'pm-resource-zone').find(item => item.dataset.zoneSlotId === slot.slotId);
    assert.equal(one(card, 'pm-district-copy').textContent, 'Требование источника');
    assert.equal(one(card, 'pm-zone-open'), undefined);

    slot.gates = [{ code: 'dependency-unresolved', dependencyId: 'zone-slot-condition-unresolved' }]; slot.requirementLocalizationKeys = [];
    render(root, document, model);
    card = many(root, 'pm-resource-zone').find(item => item.dataset.zoneSlotId === slot.slotId);
    assert.ok(card.textContent.includes('Условия разблокировки неизвестны'));
    assert.equal(one(card, 'pm-zone-open'), undefined);

    dto.slots.push({ ...slot });
    render(root, document, model);
    assert.equal(many(root, 'pm-resource-zone').some(item => item.dataset.zoneSlotId === slot.slotId), false, 'неоднозначный повтор slotId не создаёт карточку или callback');
});

test('каталог различает дефицит каждой пары, скрывает запреты и сохраняет пустую панель', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    dto.economy = { jobs: [], reportMonth: 0, resources: [{ resourceId: 'minerals', stock: 4 }, { resourceId: 'energy', stock: 99 }] };
    dto.catalog[1].quote.costs = { minerals: 10, energy: 2 }; dto.catalog[1].canAfford = false;
    dto.catalog.push({ ...dto.catalog[1], definitionId: 'locked-tech', eligibility: 'unavailable' });
    const options = { tab: 'infrastructure', inspection: { kind: 'slot', slotId: 'free-building' } };
    render(root, document, model, options);
    const card = one(root, 'pm-choice'); assert.equal(card.disabled, true); assert.equal(many(root, 'pm-choice').length, 1);
    const pairs = many(card, 'pm-resource');
    assert.equal(pairs.find(item => item.dataset.resourceId === 'minerals').matchesClass('is-missing'), true);
    assert.equal(pairs.find(item => item.dataset.resourceId === 'energy').matchesClass('is-missing'), false);
    dto.catalog = [];
    render(root, document, model, options);
    assert.ok(one(root, 'planet-context').textContent.includes('Нет доступных вариантов'));
});

test('zone catalog сообщает о неизвестной скорости и показывает общее gated-состояние с исходным требованием', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const slot = { slotId: 'mine-zone-inspection', kind: 'zone', districtInstanceId: 'mine-instance', zoneSlotDefinitionId: 'slot_energy', requirementLocalizationKeys: ['ZONE_REQUIREMENT_KEY'], gates: [], reservedByOrderId: null, installedDefinitionId: null };
    dto.slots.push(slot);
    const entry = { definitionId: 'zone_energy', kind: 'zone', slotIds: [slot.slotId], compatibleSlotIds: [slot.slotId], eligibility: 'unavailable', gates: [{ code: 'dependency-unresolved', dependencyId: 'numerical-modifiers-unresolved' }] };
    dto.catalog.push(entry);
    const options = { tab: 'infrastructure', inspection: { kind: 'slot', slotId: slot.slotId } };
    render(root, document, model, { ...options, resolveLocalization: (key, fallback) => key === 'ZONE_REQUIREMENT_KEY' ? 'Нужна технология' : fallback ?? key });
    let panel = one(root, 'planet-context');
    assert.ok(panel.textContent.includes('Скорость строительства зоны ещё не подтверждена'));
    assert.equal(panel.textContent.includes('Нужна технология'), false, 'текст slot requirement не выводится для отдельного speed gate');
    assert.equal(many(root, 'pm-choice').length, 0, 'gated definition не отображается как вариант или кнопка');

    entry.gates = [{ code: 'dependency-unresolved', dependencyId: 'zone-slot-locked' }];
    slot.gates = [{ code: 'dependency-unresolved', dependencyId: 'zone-slot-locked' }];
    render(root, document, model, { ...options, resolveLocalization: (key, fallback) => key === 'ZONE_REQUIREMENT_KEY' ? 'Требование источника' : fallback ?? key });
    panel = one(root, 'planet-context');
    assert.ok(panel.textContent.includes('Пока нет доступных специализаций'));
    assert.ok(panel.textContent.includes('Требование источника'));
    assert.equal(many(root, 'pm-choice').length, 0, 'прочие gated definitions также не открывают actions');
});

test('выключенное здание сохраняет обычный арт, показывает состояние и требует точный toggle capability', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const installed = dto.installed.find(item => item.slotId === 'research-building');
    installed.enabled = false; installed.toggle = { canToggle: true, targetEnabled: true, gates: [] };
    installed.infrastructureUpkeep = { energy: 3 }; installed.jobCapacity = { physicist: 200 };
    render(root, document, model);
    const mapSlot = many(root, 'pm-slot').find(item => item.dataset.slotId === 'research-building');
    assert.ok(mapSlot.matchesClass('is-disabled-building'));
    assert.ok(one(mapSlot, 'pm-slot-disabled-badge'));
    assert.equal(one(mapSlot, 'pm-slot-art').dataset.missingImage, undefined, 'иконка выключенного здания не затемняется и остаётся обычной');
    assert.ok(mapSlot.attributes['aria-label'].includes('Выключено'));

    const appendConstruction = (parent, identity) => { const control = document.createElement('button'); control.dataset.enabled = String(identity.enabled); parent.append(control); return true; };
    const options = { inspection: { kind: 'installed', slotId: 'research-building', definitionId: installed.definitionId }, appendConstruction };
    render(root, document, model, options);
    const pane = one(root, 'planet-context'), action = one(pane, 'pm-building-actions').children[0];
    assert.equal(action.textContent, 'Включить'); assert.equal(action.disabled, false);
    assert.ok(pane.textContent.includes('Выключено'));
    assert.equal(many(pane, 'pm-resource').some(item => item.dataset.resourceId === 'energy'), true, 'отображаются полученные server effects');

    delete installed.enabled; delete installed.toggle;
    render(root, document, model, options);
    const unknown = one(one(root, 'planet-context'), 'pm-building-actions').children[0];
    assert.equal(unknown.disabled, true); assert.equal(unknown.textContent, 'Отключить');
    assert.equal(unknown.title, 'Правило отключения здания ещё не подтверждено');

    installed.enabled = false; installed.toggle = { canToggle: false, targetEnabled: true, gates: [{ code: 'dependency-unresolved', dependencyId: 'building-disable-forbidden' }] };
    render(root, document, model, options);
    const forbidden = one(one(root, 'planet-context'), 'pm-building-actions').children[0];
    assert.equal(forbidden.disabled, true); assert.equal(forbidden.textContent, 'Включить');
    assert.equal(forbidden.title, 'Это здание нельзя отключить');
});

test('Демонтаж отображается только по явной capability и объясняет unknown, source denial и pending', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const installed = dto.installed.find(item => item.slotId === 'research-building');
    const appendConstruction = (parent, identity) => { const control = document.createElement('button'); control.dataset.demolition = String(identity.demolition === true); parent.append(control); return true; };
    const options = { inspection: { kind: 'installed', slotId: installed.slotId, definitionId: installed.definitionId }, appendConstruction };
    installed.demolition = { canDemolish: true, gates: [] };
    render(root, document, model, options);
    let action = one(one(root, 'planet-context'), 'pm-building-actions').children[1];
    assert.equal(action.textContent, 'Демонтировать'); assert.equal(action.disabled, false); assert.equal(action.dataset.demolition, 'true');

    delete installed.demolition;
    render(root, document, model, options);
    action = one(one(root, 'planet-context'), 'pm-building-actions').children[1];
    assert.equal(action.disabled, true); assert.equal(action.title, 'Правило демонтажа здания ещё не подтверждено');

    installed.demolition = { canDemolish: false, gates: [{ code: 'dependency-unresolved', dependencyId: 'building-demolition-forbidden' }] };
    render(root, document, model, options);
    action = one(one(root, 'planet-context'), 'pm-building-actions').children[1];
    assert.equal(action.disabled, true); assert.equal(action.title, 'Исходные правила запрещают демонтаж этого здания');

    installed.demolition = { canDemolish: false, gates: [{ code: 'dependency-unresolved', dependencyId: 'building-demolition-condition-refresh-unresolved' }] };
    dto.slots.find(item => item.slotId === installed.slotId).reservedByOrderId = 'pending-order';
    render(root, document, model, options);
    action = one(one(root, 'planet-context'), 'pm-building-actions').children[1];
    assert.equal(action.disabled, true); assert.equal(action.title, 'Сначала отмените ожидающий заказ в этом месте');
});

test('панорама использует пять полученных слоёв своего класса; неполный набор не смешивается', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    dto.planetClass = 'pc_continental';
    const keys = render(root, document, model);
    const environment = one(root, 'pm-environment');
    assert.equal(environment.children.length, 5);
    assert.ok(keys.includes('resolved:environment:pc_continental.sky'));
    assert.ok(keys.includes('resolved:environment:pc_continental.l04'));
    render(root, document, model, { resolveImageKey: (kind, id) => kind === 'environment' && id.endsWith('.l03') ? null : `${kind}.${id}` });
    assert.equal(one(root, 'pm-environment'), undefined);
    assert.ok(one(root, 'pm-head').textContent.includes('pc_continental'));
});

test('сохраняет прокрутку и фокус при обновлении той же колонии, но не переносит их между колониями', () =>
{
    const document = makeDocument(); const root = document.createElement('div'); const first = fixture('colony-1').model;
    const draw = model => render(root, document, model, {
        inspection: { kind: 'slot', slotId: 'free-building' },
        appendConstruction(parent, identity) { const button = document.createElement('button'); button.dataset.planetActionIdentity = identity.definitionId; parent.append(button); return true; },
    });
    draw(first);
    const action = root.querySelectorAll('.planet-context button').find(button => button.dataset.planetActionIdentity === 'building_new');
    action.focus();
    one(root, 'pm-main').scrollTop = 81; one(root, 'pm-side').scrollTop = 32; one(root, 'pm-catalog-list').scrollTop = 17;
    draw(first);
    assert.equal(document.activeElement.dataset.planetActionIdentity, 'building_new');
    assert.equal(one(root, 'pm-main').scrollTop, 81);
    assert.equal(one(root, 'pm-side').scrollTop, 32);
    assert.equal(one(root, 'pm-catalog-list').scrollTop, 17);

    const second = fixture('colony-2').model;
    draw(second);
    assert.equal(document.activeElement, null, 'фокус старой колонии не переносится на новую');
    assert.equal(one(root, 'pm-main').scrollTop, 0);
    assert.equal(one(root, 'pm-side').scrollTop, 0);
    assert.equal(one(root, 'pm-catalog-list').scrollTop, 0);
});

test('из карточки установленной зоны открывается replacement catalog с той же идентичностью occurrence', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { zone } = setZoneReplacement(dto);
    const inspection = { kind: 'zoneCatalog', districtInstanceId: zone.districtInstanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId };
    const requests = [];
    render(root, document, model, { inspection, onInspect: value => requests.push(value) });
    const replace = one(one(root, 'planet-context'), 'pm-district-actions').children.find(item => item.textContent === 'Заменить');
    assert.ok(replace && !replace.disabled, 'source preview предлагает переход к замене');
    replace.onclick();
    assert.deepEqual(requests, [{ ...inspection, kind: 'zoneReplacementCatalog' }]);
});

test('replacement catalog показывает целую карточку с ценой, сроком и точным списком удаляемых зданий', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { zone, replacement } = setZoneReplacement(dto);
    const commands = [];
    render(root, document, model, {
        inspection: { kind: 'zoneReplacementCatalog', districtInstanceId: zone.districtInstanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId },
        appendConstruction(parent, identity) { commands.push(identity); const button = document.createElement('button'); parent.append(button); return true; },
        resolveLocalization(key, fallback) { return ({ zone_foundry_name: 'Литейная специализация', building_factory_name: 'Фабрики' })[key] ?? fallback ?? key; }
    });
    const pane = one(root, 'planet-context'), choice = one(pane, 'pm-choice');
    assert.ok(choice && !choice.disabled);
    assert.ok(choice.textContent.includes('Литейная специализация'));
    assert.ok(choice.textContent.includes('125') && choice.textContent.includes('48'), 'цена и длительность остаются на карточке');
    assert.ok(choice.textContent.includes('Будут удалены без возврата: Фабрики'));
    assert.deepEqual(commands.filter(identity => identity.zoneReplacement), [{ zoneReplacement: true, sourceDefinitionId: 'zone_industrial', definitionId: 'zone_foundry', slotId: zone.slotInstanceId,
        zoneInstanceId: zone.instanceId, zoneReplacementPreviewId: replacement.previewId }]);
});

test('unknown native speed оставляет пустое объяснение без отключённой карточки замены', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { zone, replacement } = setZoneReplacement(dto, { gates: [{ code: 'dependency-unresolved', dependencyId: 'numerical-modifiers-unresolved' }] });
    replacement.canEnqueue = false;
    render(root, document, model, { inspection: { kind: 'zoneReplacementCatalog', districtInstanceId: zone.districtInstanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId } });
    const pane = one(root, 'planet-context');
    assert.ok(pane.textContent.includes('Скорость строительства зоны ещё не подтверждена'));
    assert.equal(many(pane, 'pm-choice').length, 0, 'unknown native speed не превращается в locked target card');
});

test('pending replacement показывает 50/50, предупреждает только точное удаление и не отключает здание', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { zone, children } = setZoneReplacement(dto, { pending: true });
    render(root, document, model, { resolveLocalization: (key, fallback) => fallback ?? key });
    const card = many(root, 'pm-zone-surface').find(item => item.dataset.zoneInstance === zone.instanceId);
    const split = one(card, 'pm-zone-replacement');
    assert.ok(split); assert.equal(split.children.length, 2);
    assert.deepEqual(descendants(split).filter(item => item.tagName === 'IMG').map(item => item.dataset.imageKey), ['image:zone-industrial', 'image:zone-foundry']);
    const progressBar = one(card, 'pm-slot-progress');
    assert.equal(progressBar.value, 25); assert.equal(progressBar.max, 50);
    const removed = many(card, 'pm-slot').find(item => item.dataset.slotId === children[0].slotId);
    const survivor = many(card, 'pm-slot').find(item => item.dataset.slotId === children[1].slotId);
    assert.ok(has(removed, 'is-pending-removal') && one(removed, 'pm-slot-removal-badge'));
    assert.equal(removed.disabled, false); assert.equal(has(removed, 'is-disabled-building'), false);
    assert.equal(has(survivor, 'is-pending-removal'), false); assert.equal(one(survivor, 'pm-slot-removal-badge'), undefined);

    dto.queue[0].status = 'cancelled'; dto.slots.find(item => item.slotId === zone.slotInstanceId).reservedByOrderId = null;
    render(root, document, model, { inspection: { kind: 'zoneCatalog', districtInstanceId: zone.districtInstanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId } });
    const reverted = many(root, 'pm-zone-surface').find(item => item.dataset.zoneInstance === zone.instanceId);
    assert.equal(one(reverted, 'pm-zone-replacement'), undefined);
    assert.equal(has(many(reverted, 'pm-slot').find(item => item.dataset.slotId === children[0].slotId), 'is-pending-removal'), false);
    const hero = one(one(root, 'planet-context'), 'pm-district-hero');
    assert.ok(descendants(hero).some(item => item.tagName === 'IMG' && item.dataset.imageKey === 'image:zone-industrial'), 'cancel сохраняет старый арт зоны');
});

test('completed replacement отражает новую startup zone, оставляет её места и убирает только установленное factory', () =>
{
    const document = makeDocument(), root = document.createElement('div'); const { model, dto } = fixture();
    const { zone, children } = setZoneReplacement(dto, { pending: true });
    dto.queue[0].status = 'completed';
    dto.slots.find(item => item.slotId === zone.slotInstanceId).reservedByOrderId = null;
    dto.startupZones.find(item => item.instanceId === zone.instanceId).zoneId = 'zone_foundry';
    dto.startupZones.find(item => item.instanceId === zone.instanceId).imageDefinitionKey = 'image:zone-foundry';
    dto.slots.filter(item => item.kind === 'building' && item.zoneInstanceId === zone.instanceId).forEach(item => { item.zoneDefinitionId = 'zone_foundry'; });
    dto.slots.find(item => item.slotId === children[0].slotId).installedDefinitionId = null;
    const parent = dto.installed.find(item => item.slotId === zone.slotInstanceId);
    parent.definitionId = 'zone_foundry'; parent.imageDefinitionKey = 'image:zone-foundry'; parent.zoneReplacements = [];
    dto.installed = dto.installed.filter(item => item.slotId !== children[0].slotId);

    const inspection = { kind: 'zoneCatalog', districtInstanceId: zone.districtInstanceId, slotInstanceId: zone.slotInstanceId, zoneInstanceId: zone.instanceId };
    render(root, document, model, { inspection });
    const card = many(root, 'pm-zone-surface').find(item => item.dataset.zoneInstance === zone.instanceId);
    assert.ok(one(card, 'pm-zone-title').textContent.includes('zone_foundry'));
    const slotCards = many(card, 'pm-slot');
    assert.equal(slotCards.length, 3, 'три канонических building slots остаются после замены');
    const removedSlot = slotCards.find(item => item.dataset.slotId === children[0].slotId);
    assert.equal(removedSlot.textContent, '+'); assert.equal(has(removedSlot, 'is-pending-removal'), false);
    assert.equal(has(removedSlot, 'is-disabled-building'), false);
    const hero = one(one(root, 'planet-context'), 'pm-district-hero');
    assert.ok(descendants(hero).some(item => item.tagName === 'IMG' && item.dataset.imageKey === 'image:zone-foundry'));
});
