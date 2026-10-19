import test from 'node:test';
import assert from 'node:assert/strict';
import { planetStartupGraph, renderPlanetPanel, appendPlanetWorkforceControls, resolvePlanetInspection } from '../../src/Stellaris.Server/wwwroot/m3-planet-panel.mjs';
import { constructionOperations } from '../../src/Stellaris.Server/wwwroot/m3-construction-controls.mjs';

class Element
{
    constructor(tag) { this.tag = tag; this.children = []; this.dataset = {}; this.textContent = ''; }
    append(...nodes) { this.children.push(...nodes); }
    replaceChildren(...nodes) { this.children = nodes; }
    setAttribute(name, value) { this[name] = value; }
    addEventListener() {}
    get text() { return [this.textContent, ...this.children.map(node => node.text)].join(' '); }
    all() { return this.children.flatMap(node => [node, ...node.all()]); }
}
const document = { createElement: tag => new Element(tag) };
function development()
{
    return { revision: 3, gates: [], slots: [{ slotId: 'z-slot', kind: 'specialization', installedDefinitionId: null, reservedByOrderId: null, gates: [] }], installed: [], catalog: [], queue: [], jobs: [], features: [],
        startupDistricts: [{ instanceId: 'city', definitionId: 'district_city', level: 3 }, { instanceId: 'mine', definitionId: 'district_mining', level: 2 }],
        startupZones: [{ instanceId: 'research', districtInstanceId: 'city', slotInstanceId: 'z-slot', zoneId: 'zone_research_unity', slotNumber: 1 }],
        startupBuildings: [{ instanceId: 'lab', definitionId: 'building_research_lab_1', slotInstanceId: 'z-slot', zoneId: 'zone_research_unity', compatibleZoneInstanceIds: ['research'] }, { instanceId: 'capital', definitionId: 'building_capital', slotInstanceId: null, compatibleZoneInstanceIds: [] }],
        startupFeatures: [{ instanceId: 'blocker', definitionId: 'd_slums', isBlocker: true, isHidden: false, hiddenFeatureInstanceId: 'hidden' }, { instanceId: 'hidden', definitionId: 'd_rich_mountain', isBlocker: false, isHidden: true, hiddenByBlockerInstanceId: 'blocker' }, { instanceId: 'passive', definitionId: 'd_hot_springs', isBlocker: false, isHidden: false }], startupDistrictCaps: [{ modifierId: 'district_mining_max', value: 8 }] };
}
const model = value => ({ id: 'colony', economy: null, development: value, gates: [] });

test('Сведения следуют установленному instance через upgrade и закрываются при его удалении', () =>
{
    const dto = development(); dto.slots = [{ slotId: 'building-slot', kind: 'building' }];
    dto.installed = [{ slotId: 'building-slot', kind: 'building', definitionId: 'old' }];
    const current = model(dto); const inspection = { kind: 'installed', slotId: 'building-slot', definitionId: 'old' };
    dto.installed[0].definitionId = 'new';
    assert.deepEqual(resolvePlanetInspection(current, inspection), { ...inspection, definitionId: 'new' });
    dto.installed = [];
    assert.equal(resolvePlanetInspection(current, inspection), null);
    dto.slots = [];
    assert.equal(resolvePlanetInspection(current, inspection), null);
});

test('Отсутствующие армии и владения не создают demo instances или действий', () =>
{
    for (const tab of ['armies', 'holdings'])
    {
        const root = new Element('div'); let requests = 0;
        renderPlanetPanel(root, model(development()), { document, tab, appendConstruction() { requests++; } });
        assert.match(root.text, /недоступны: сервер пока не передаёт/);
        assert.equal(requests, 0); assert.equal(root.all().filter(item => item.tag === 'button').length, 0);
    }
});

test('Завершённые и отменённые receipts не занимают видимую очередь и не оставляют progress свободного слота', () =>
{
    const dto = development(); dto.slots = [{ slotId: 'free', kind: 'building', reservedByOrderId: null, installedDefinitionId: null, gates: [] }];
    dto.queue = ['cancelled', 'completed'].map((status, index) => ({ orderId: `terminal-${index}`, slotId: 'free', definitionId: 'terminal', status, completedWork: 5, requiredWork: 10, gates: [] }));
    const root = new Element('div'); renderPlanetPanel(root, model(dto), { document });
    const queue = root.all().find(item => item.className === 'planet-queue');
    assert.match(queue.text, /Записей очереди в снимке нет/);
    assert.equal(root.all().filter(item => item.tag === 'progress').length, 0);
    assert.equal(dto.queue.length, 2, 'Серверная история не мутируется клиентом');
});

test('Живой каталог сохраняет фокус заказа, затем следующей карточки и закрытия пустого списка', () =>
{
    const doc = { activeElement: null, createElement: tag => new FocusElement(tag) };
    class FocusElement extends Element
    {
        focus() { doc.activeElement = this; }
        querySelectorAll(selector)
        {
            const pane = this.all().find(item => item.className === 'planet-context');
            return selector === '.planet-context button' ? pane?.all().filter(item => item.tag === 'button') ?? [] : [];
        }
    }
    const dto = development(); dto.slots = [{ slotId: 'free', kind: 'building', gates: [] }];
    dto.catalog = ['a', 'b'].map(definitionId => ({ definitionId, kind: 'building', eligibility: 'available', gates: [], slotIds: ['free'], quote: null }));
    const root = new FocusElement('div'); const current = model(dto);
    const draw = () => renderPlanetPanel(root, current, { document: doc, inspection: { kind: 'slot', slotId: 'free' }, appendConstruction(parent, identity)
    {
        const button = doc.createElement('button'); button.dataset.planetActionIdentity = identity.definitionId; parent.append(button); return true;
    } });
    draw(); root.querySelectorAll('.planet-context button').find(item => item.dataset.planetActionIdentity === 'a').focus();
    draw(); assert.equal(doc.activeElement.dataset.planetActionIdentity, 'a');
    dto.catalog.shift(); draw(); assert.equal(doc.activeElement.dataset.planetActionIdentity, 'b');
    dto.catalog = []; draw(); assert.equal(doc.activeElement.dataset.planetInspect, 'close');
});

test('Контракт улучшения требует явное разрешение сервера для точного экземпляра и запрещает вывод из catalog', () =>
{
    const quote = { policyId: 'resolved', durationDays: 10, costs: {}, infrastructureUpkeep: {}, jobCapacity: {} };
    const target = { sourceDefinitionId: 'building_old', definitionId: 'building_new', slotId: 'occupied', zoneInstanceId: 'zone-1', expectedColonyRevision: 4, quote, eligibility: 'available', canAfford: true, canEnqueue: true, commandType: 'enqueueUpgrade', gates: [] };
    const dto = { revision: 4, gates: [], slots: [{ slotId: 'occupied', kind: 'building', zoneInstanceId: 'zone-1', installedDefinitionId: 'building_old', reservedByOrderId: null, gates: [] }],
        installed: [{ slotId: 'occupied', definitionId: 'building_old', kind: 'building', upgrades: [target] }], queue: [], features: [], catalog: [{ definitionId: 'building_new', kind: 'building', eligibility: 'available', slotIds: ['occupied'], gates: [], quote }] };
    const operation = constructionOperations(model(dto))[0];
    assert.deepEqual(operation, { type: 'enqueueUpgrade', sourceDefinitionId: 'building_old', definitionId: 'building_new', slotId: 'occupied', zoneInstanceId: 'zone-1', quote });
    const invalid = [
        ['вариант отсутствует', value => { value.installed[0].upgrades = []; }],
        ['ревизия устарела', value => { value.installed[0].upgrades[0].expectedColonyRevision++; }],
        ['источник не совпадает', value => { value.installed[0].upgrades[0].sourceDefinitionId = 'building_other'; }],
        ['зона не совпадает', value => { value.installed[0].upgrades[0].zoneInstanceId = 'zone-other'; }],
        ['место не совпадает', value => { value.installed[0].upgrades[0].slotId = 'other'; }],
        ['дубликат цели', value => { value.installed[0].upgrades.push(structuredClone(value.installed[0].upgrades[0])); }],
        ['цена некорректна', value => { value.installed[0].upgrades[0].quote = null; }],
        ['команда неизвестна', value => { value.installed[0].upgrades[0].commandType = 'enqueueConstruction'; }],
        ['enqueue запрещён', value => { value.installed[0].upgrades[0].canEnqueue = false; }],
        ['условия не выполнены', value => { value.installed[0].upgrades[0].gates = [{ code: 'blocked' }]; }],
        ['место зарезервировано', value => { value.slots[0].reservedByOrderId = 'pending'; }],
        ['установка дублирована', value => { value.installed.push(structuredClone(value.installed[0])); }]
    ];
    for (const [reason, mutate] of invalid)
    {
        const changed = structuredClone(dto); mutate(changed); assert.deepEqual(constructionOperations(model(changed)).filter(item => item.type === 'enqueueUpgrade'), [], reason);
    }
    const legacy = structuredClone(dto); legacy.installed[0].upgrades = []; legacy.catalog[0].slotIds = ['occupied'];
    assert.deepEqual(constructionOperations(model(legacy)).filter(item => item.type === 'enqueueUpgrade' || item.type === 'enqueueConstruction'), []);
});

test('Installed details expose only operations accepted for the exact occupied slot', () =>
{
    const dto = { revision: 4, gates: [], slots: [{ slotId: 'occupied', kind: 'building', installedDefinitionId: 'building_old', reservedByOrderId: null, gates: [] }],
        installed: [{ slotId: 'occupied', definitionId: 'building_old', kind: 'building', infrastructureUpkeep: {}, jobCapacity: {}, upgrades: [{ sourceDefinitionId: 'building_old', definitionId: 'building_new', slotId: 'occupied', zoneInstanceId: 'zone-1', expectedColonyRevision: 4, eligibility: 'available', canAfford: true, canEnqueue: true, commandType: 'enqueueUpgrade', gates: [], quote: { policyId: 'resolved', durationDays: 10, costs: {}, infrastructureUpkeep: {}, jobCapacity: {} } }] }], queue: [], features: [],
        slots: [{ slotId: 'occupied', kind: 'building', zoneInstanceId: 'zone-1', installedDefinitionId: 'building_old', reservedByOrderId: null, gates: [] }], catalog: [] };
    const root = new Element('div'); const choices = [];
    const render = () => renderPlanetPanel(root, model(dto), { document, inspection: { kind: 'installed', slotId: 'occupied', definitionId: 'building_old' }, appendConstruction: (parent, identity) =>
    {
        const operation = constructionOperations(model(dto)).find(item => item.type === 'enqueueUpgrade' && item.definitionId === identity.definitionId && item.slotId === identity.slotId);
        if (!operation) { return false; }
        choices.push(operation); parent.append(new Element('button')); return true;
    } });
    render();
    assert.match(root.text, /Доступные улучшения/);
    assert.deepEqual(choices.map(item => ({ definitionId: item.definitionId, slotId: item.slotId, type: item.type })), [{ definitionId: 'building_new', slotId: 'occupied', type: 'enqueueUpgrade' }]);
    assert.equal(root.all().find(node => node.className?.includes('planet-upgrade-target')).dataset.availability, 'available');
    dto.installed[0].upgrades[0].canEnqueue = false; choices.length = 0; render();
    const blockedButton = root.all().find(node => node.tag === 'button' && node.className?.includes('planet-upgrade-target'));
    assert.equal(blockedButton.dataset.availability, 'available');
    assert.equal(blockedButton.disabled, true);
    assert.match(root.text, /Улучшение недоступно по текущим условиям/);
    assert.deepEqual(choices, []);
    dto.installed[0].upgrades[0].canEnqueue = true;
    for (const [canAfford, status, notice] of [[false, 'insufficient_resources', /Недостаточно ресурсов/], [null, 'unresolved', /Доступность по ресурсам не подтверждена/]])
    {
        dto.installed[0].upgrades[0].canAfford = canAfford; choices.length = 0; render();
        assert.equal(root.all().find(node => node.className?.includes('planet-upgrade-target')).dataset.availability, status);
        assert.match(root.text, notice); assert.deepEqual(choices, []);
    }
    dto.slots[0].kind = 'district'; dto.installed[0].kind = 'district';
    assert.deepEqual(constructionOperations(model(dto)).filter(item => item.type === 'enqueueUpgrade'), []);
});

test('Building toggle requires an exact current capability but remains available during a pending upgrade', () =>
{
    const create = (enabled, targetEnabled) => ({ revision: 4, gates: [],
        slots: [{ slotId: 'occupied', kind: 'building', zoneInstanceId: 'zone', installedDefinitionId: 'building_old', reservedByOrderId: null, gates: [] }],
        installed: [{ slotId: 'occupied', kind: 'building', definitionId: 'building_old', enabled, toggle: { canToggle: true, targetEnabled, gates: [] }, upgrades: [] }],
        catalog: [], queue: [], features: [] });
    for (const [enabled, targetEnabled] of [[true, false], [false, true]])
    {
        const dto = create(enabled, targetEnabled);
        assert.deepEqual(constructionOperations(model(dto)).filter(item => item.type === 'setBuildingEnabled'), [
            { type: 'setBuildingEnabled', definitionId: 'building_old', slotId: 'occupied', enabled: targetEnabled }
        ]);
    }
    const pendingUpgrade = create(false, true);
    pendingUpgrade.slots[0].reservedByOrderId = 'upgrade-order';
    pendingUpgrade.queue.push({ orderId: 'upgrade-order', slotId: 'occupied', definitionId: 'building_new', kind: 'building', status: 'pending', gates: [] });
    assert.equal(constructionOperations(model(pendingUpgrade)).filter(item => item.type === 'setBuildingEnabled').length, 1);

    const invalid = [
        ['legacy enabled absent', dto => { delete dto.installed[0].enabled; }],
        ['malformed enabled', dto => { dto.installed[0].enabled = 'false'; }],
        ['unknown permission', dto => { dto.installed[0].toggle.gates = [{ code: 'dependency-unresolved', dependencyId: 'building-disable-permission-unresolved' }]; }],
        ['explicit forbidden', dto => { dto.installed[0].toggle.canToggle = false; dto.installed[0].toggle.gates = [{ code: 'dependency-unresolved', dependencyId: 'building-disable-forbidden' }]; }],
        ['false capability', dto => { dto.installed[0].toggle.canToggle = false; }],
        ['wrong target', dto => { dto.installed[0].toggle.targetEnabled = true; }],
        ['missing gates', dto => { delete dto.installed[0].toggle.gates; }],
        ['duplicate installed', dto => { dto.installed.push(structuredClone(dto.installed[0])); }],
        ['duplicate slot', dto => { dto.slots.push(structuredClone(dto.slots[0])); }],
        ['wrong installed definition', dto => { dto.slots[0].installedDefinitionId = 'building_other'; }],
        ['wrong slot kind', dto => { dto.slots[0].kind = 'district'; }],
    ];
    for (const [reason, mutate] of invalid)
    {
        const dto = create(true, false); mutate(dto);
        assert.deepEqual(constructionOperations(model(dto)).filter(item => item.type === 'setBuildingEnabled'), [], reason);
    }
});

test('Демонтаж доступен только для точного установленного здания с явным разрешением и свободным местом', () =>
{
    const create = () => ({ revision: 4, gates: [],
        slots: [{ slotId: 'occupied', kind: 'building', zoneInstanceId: 'zone', installedDefinitionId: 'building_old', reservedByOrderId: null, gates: [] }],
        installed: [{ slotId: 'occupied', kind: 'building', definitionId: 'building_old', demolition: { canDemolish: true, gates: [] } }],
        catalog: [], queue: [], features: [] });
    const dto = create();
    assert.deepEqual(constructionOperations(model(dto)).filter(item => item.type === 'demolishBuilding'), [
        { type: 'demolishBuilding', definitionId: 'building_old', slotId: 'occupied' }
    ]);
    const rejected = [
        ['missing capability', value => { delete value.installed[0].demolition; }],
        ['unknown capability', value => { value.installed[0].demolition.canDemolish = null; }],
        ['unresolved source permission', value => { value.installed[0].demolition.gates = [{ code: 'dependency-unresolved', dependencyId: 'building-demolition-permission-unresolved' }]; }],
        ['explicit source prohibition', value => { value.installed[0].demolition.canDemolish = false; value.installed[0].demolition.gates = [{ code: 'dependency-unresolved', dependencyId: 'building-demolition-forbidden' }]; }],
        ['unknown demolition gate', value => { value.installed[0].demolition.gates = [{ code: 'dependency-unresolved', dependencyId: 'other' }]; }],
        ['slot reserved', value => { value.slots[0].reservedByOrderId = 'order'; }],
        ['related pending order', value => { value.queue.push({ orderId: 'order', slotId: 'occupied', status: 'pending', gates: [] }); }],
        ['slot gated', value => { value.slots[0].gates = [{ code: 'dependency-unresolved', dependencyId: 'slot-unknown' }]; }],
        ['duplicate installed instance', value => { value.installed.push(structuredClone(value.installed[0])); }],
        ['duplicate actual slot', value => { value.slots.push(structuredClone(value.slots[0])); }],
        ['wrong installed definition', value => { value.slots[0].installedDefinitionId = 'other'; }],
        ['wrong slot kind', value => { value.slots[0].kind = 'district'; }],
    ];
    for (const [reason, mutate] of rejected)
    {
        const value = create(); mutate(value);
        assert.deepEqual(constructionOperations(model(value)).filter(item => item.type === 'demolishBuilding'), [], reason);
    }
});

test('Обновления снимка сохраняют прокрутку своей колонии, смена контекста сбрасывает её', () =>
{
    const root = new Element('div');
    root.querySelector = selector => root.all().find(node => node.className?.split(' ').includes(selector.slice(1))) ?? null;
    const colony = { ...model(development()), ownerId: 'owner' };
    const options = { document, inspection: { kind: 'slot', slotId: 'z-slot' } };
    renderPlanetPanel(root, colony, options);
    root.querySelector('.planet-infrastructure').scrollTop = 70;
    root.querySelector('.planet-queue').scrollTop = 25;
    root.querySelector('.planet-context').scrollTop = 120;
    renderPlanetPanel(root, structuredClone(colony), options);
    assert.equal(root.querySelector('.planet-infrastructure').scrollTop, 70);
    assert.equal(root.querySelector('.planet-queue').scrollTop, 25);
    assert.equal(root.querySelector('.planet-context').scrollTop, 120);
    renderPlanetPanel(root, colony, { document, inspection: null });
    assert.equal(root.querySelector('.planet-infrastructure').scrollTop, 70);
    renderPlanetPanel(root, colony, options);
    assert.equal(root.querySelector('.planet-context').scrollTop, undefined);
    renderPlanetPanel(root, { ...colony, ownerId: 'another-owner' }, options);
    assert.equal(root.querySelector('.planet-infrastructure').scrollTop, undefined);
    root.querySelector('.planet-infrastructure').scrollTop = 90;
    renderPlanetPanel(root, null, options);
    renderPlanetPanel(root, colony, options);
    assert.equal(root.querySelector('.planet-infrastructure').scrollTop, undefined);
});

test('Стартовый граф показывает реальные уровни, вложенные зоны и ровно один экземпляр здания', () =>
{
    const dto = development(); const before = structuredClone(dto); const graph = planetStartupGraph(dto);
    assert.equal(graph.districts[0].zones[0].buildings[0].instanceId, 'lab');
    assert.deepEqual(graph.unplacedBuildings.map(item => item.instanceId), ['capital']);
    const root = new Element('div'); renderPlanetPanel(root, model(dto), { document });
    assert.match(root.text, /Уровень 3/); assert.match(root.text, /Уровень 2/); assert.match(root.text, /Вместимость недоступна/);
    assert.equal(root.all().filter(node => node.dataset.buildingInstance === 'lab').length, 1);
    assert.equal(root.all().filter(node => node.dataset.zoneInstance === 'research').length, 1);
    assert.doesNotMatch(root.text, /Свободное место/);
    assert.match(root.text, /Операция для этого места ещё не подтверждена/);
    assert.deepEqual(dto, before);
});

test('Каталог зон сохраняет точную startup identity и остаётся только для чтения', () =>
{
    const dto = development(); dto.slots = [];
    dto.catalog = [
        { definitionId: 'zone_research_unity', kind: 'zone', localizationKey: 'zone_name', descriptionKey: 'zone_desc', imageDefinitionKey: 'zone-image', compatibleSlotIds: [], eligibility: 'available', gates: [] },
        { definitionId: 'zone_industrial', kind: 'zone', compatibleSlotIds: [], eligibility: 'available', gates: [] },
        { definitionId: 'building_research_lab_1', kind: 'building', compatibleSlotIds: [] }
    ];
    const current = model(dto); const zone = dto.startupZones[0];
    const inspection = { kind: 'zoneCatalog', districtInstanceId: 'city', slotInstanceId: 'z-slot', zoneInstanceId: 'research' };
    assert.deepEqual(resolvePlanetInspection(current, inspection), inspection);
    assert.deepEqual(resolvePlanetInspection(current, { ...inspection, definitionId: 'zone_industrial' }), { ...inspection, definitionId: 'zone_industrial' });

    const root = new Element('div'); const inspections = []; const constructionRequests = [];
    renderPlanetPanel(root, current, { document, onInspect: value => inspections.push(value), appendConstruction: (parent, identity) => constructionRequests.push(identity), resolveLocalization: key => key === 'zone_name' ? 'Исследования и единство' : key });
    const open = root.all().find(item => item.dataset.planetInspect === 'zoneCatalog');
    assert.equal(open.dataset.districtInstanceId, 'city'); assert.equal(open.dataset.slotInstanceId, 'z-slot'); assert.equal(open.dataset.zoneInstanceId, 'research');
    open.onclick(); assert.deepEqual(inspections, [inspection]);
    const resolveLocalization = key => ({ zone_name: 'Исследования и единство', zone_desc: 'Описание зоны из словаря', zone_industrial: 'Промышленная зона' })[key] ?? key;
    renderPlanetPanel(root, current, { document, inspection, onInspect: value => inspections.push(value), appendConstruction: (parent, identity) => constructionRequests.push(identity), resolveLocalization });
    const pane = root.all().find(item => item.className?.split(' ').includes('planet-context'));
    assert.match(pane.text, /Просмотр каталога\. Совместимость и смена специализации пока недоступны/);
    assert.match(pane.text, /Исследования и единство/); assert.match(pane.text, /Промышленная зона/);
    assert.doesNotMatch(pane.text, /building_research_lab_1/);
    assert.equal(root.all().filter(item => item.dataset.planetInspect === 'catalog').length, 0);
    const detailsButtons = root.all().filter(item => item.dataset.planetInspect === 'zoneCatalog' && item.textContent === 'Подробнее');
    assert.equal(new Set(detailsButtons.map(item => item.ariaLabel)).size, 2, 'Кнопки деталей различаются идентичностью зоны и определения');
    assert.equal(constructionRequests.length, 0);

    const detailInspection = { ...inspection, definitionId: 'zone_research_unity' };
    renderPlanetPanel(root, current, { document, inspection: detailInspection, appendConstruction: (parent, identity) => constructionRequests.push(identity), resolveLocalization });
    assert.match(root.all().find(item => item.className?.split(' ').includes('planet-context')).text, /Описание зоны из словаря/);
    assert.equal(constructionRequests.length, 0, 'Read-only detail не создаёт construction action');
    assert.equal(resolvePlanetInspection(current, { ...inspection, definitionId: 'building_research_lab_1' }), null, 'Wrong-kind detail не разрешается');
    assert.equal(resolvePlanetInspection(current, { ...inspection, definitionId: 'zone_missing' }), null, 'Неизвестная detail не разрешается');

    dto.startupZones = dto.startupZones.filter(item => item.instanceId !== zone.instanceId);
    assert.equal(resolvePlanetInspection(current, inspection), null, 'Удалённая startup zone инвалидирует открытый каталог');
    dto.startupZones.push({ ...zone }, { ...zone });
    assert.equal(resolvePlanetInspection(current, inspection), null, 'Неоднозначная startup identity не разрешается');
    dto.startupZones = [{ ...zone }]; dto.startupDistricts.push({ ...dto.startupDistricts[0] });
    assert.equal(resolvePlanetInspection(current, inspection), null, 'Неоднозначный родительский район не разрешается');
    dto.startupDistricts.pop(); dto.catalog.push({ ...dto.catalog[0] });
    assert.equal(resolvePlanetInspection(current, { ...inspection, definitionId: 'zone_research_unity' }), null, 'Повторное определение детали делает inspection неоднозначным');
});

test('Runtime installed подавляет стартовый дубль, неоднозначная совместимость не размножает здания', () =>
{
    const dto = development(); dto.installed.push({ slotId: 'z-slot', definitionId: 'building_research_lab_1', kind: 'building' });
    assert.equal(planetStartupGraph(dto).districts[0].zones[0].buildings.length, 1);
    assert.equal(planetStartupGraph(dto).districts[0].zones[0].buildings[0].instanceId, 'lab');
    dto.installed = []; dto.startupZones.push({ instanceId: 'research-2', districtInstanceId: 'city', slotInstanceId: 'other', zoneId: 'zone_research_unity', slotNumber: 2 });
    dto.startupBuildings[0].slotInstanceId = null; dto.startupBuildings[0].compatibleZoneInstanceIds.push('research-2');
    const graph = planetStartupGraph(dto); assert.equal(graph.unplacedBuildings.filter(item => item.instanceId === 'lab').length, 1);
    assert.equal(graph.districts[0].zones.flatMap(item => item.buildings).length, 0);
});

function livePlacement()
{
    const dto = development(); dto.startupDistricts = [{ instanceId: 'city', definitionId: 'district_city', level: 3 }];
    dto.startupZones = ['research', 'industry', 'foundry'].map((id, index) => ({ instanceId: id, districtInstanceId: 'city', slotInstanceId: `zone-${id}`, zoneId: `zone_${id}`, slotNumber: index + 1 }));
    const definitions = ['building_capital', 'building_research_lab_1', 'building_factory_1', 'building_precinct_house'];
    const zoneIds = ['research', 'research', 'industry', 'foundry'];
    dto.startupBuildings = definitions.map((definitionId, index) => ({ instanceId: `startup-${index}`, definitionId, slotInstanceId: `building-${index}`, zoneId: `zone_${zoneIds[index]}`, order: index, compatibleZoneInstanceIds: [zoneIds[index]], imageDefinitionKey: null }));
    dto.slots = definitions.map((definitionId, index) => ({ slotId: `building-${index}`, kind: 'building', zoneInstanceId: zoneIds[index], installedDefinitionId: definitionId, reservedByOrderId: null, gates: [] }));
    dto.slots.push({ slotId: 'free', kind: 'building', zoneInstanceId: 'foundry', installedDefinitionId: null, reservedByOrderId: null, gates: [] });
    dto.slots.push({ slotId: 'reserved', kind: 'building', zoneInstanceId: 'foundry', installedDefinitionId: null, reservedByOrderId: 'order-1', gates: [] });
    dto.installed = definitions.map((definitionId, index) => ({ slotId: `building-${index}`, definitionId, kind: 'building', imageDefinitionKey: null, infrastructureUpkeep: {}, jobCapacity: {} }));
    return dto;
}

test('Реальный merged snapshot: четыре startup здания размещены в трёх зонах ровно по одному разу', () =>
{
    const dto = livePlacement(); const before = structuredClone(dto); const graph = planetStartupGraph(dto);
    assert.deepEqual(graph.districts[0].zones.map(zone => zone.buildings.length), [2, 1, 1]);
    assert.equal(graph.unplacedBuildings.length, 0);
    const root = new Element('div'); const keys = []; const inspections = [];
    renderPlanetPanel(root, model(dto), { document, resolveImageKey: (kind, id) => `${kind}.${id}`, appendImage: (parent, key) => { keys.push(key); return true; }, onInspect: item => inspections.push(item) });
    assert.equal(root.all().filter(item => item.dataset.buildingInstance).length, 4);
    assert.equal(root.all().filter(item => item.className?.split(' ').includes('planet-slot')).length, 2, 'Только свободное и зарезервированное места');
    assert.deepEqual(new Set(keys.filter(key => key.startsWith('building.'))), new Set(dto.installed.map(item => `building.${item.definitionId}`)));
    root.all().find(item => item.dataset.slotId === 'building-1' && item.dataset.planetInspect === 'installed').onclick();
    assert.deepEqual(inspections, [{ kind: 'installed', slotId: 'building-1', definitionId: 'building_research_lab_1' }]);
    assert.deepEqual(dto, before);
});

test('Новое runtime здание использует только явный zoneInstanceId; неизвестная зона остаётся unplaced', () =>
{
    const dto = livePlacement(); dto.slots.push({ slotId: 'new-foundry', kind: 'building', zoneInstanceId: 'foundry', installedDefinitionId: 'building_foundry_1', reservedByOrderId: null, gates: [] });
    dto.installed.push({ slotId: 'new-foundry', definitionId: 'building_foundry_1', kind: 'building', imageDefinitionKey: null });
    let graph = planetStartupGraph(dto); assert.equal(graph.districts[0].zones[2].buildings.length, 2);
    assert.equal(graph.districts[0].zones[2].buildings[1].slotId, 'new-foundry');
    dto.slots.at(-1).zoneInstanceId = 'unknown-zone'; graph = planetStartupGraph(dto);
    assert.equal(graph.unplacedBuildings.filter(item => item.slotId === 'new-foundry').length, 1);
    assert.equal(graph.districts[0].zones[2].buildings.length, 1);
    dto.slots.at(-1).zoneInstanceId = null; graph = planetStartupGraph(dto);
    assert.equal(graph.unplacedBuildings.filter(item => item.slotId === 'new-foundry').length, 1, 'ID-префикс слота не является размещением');
});

test('Пустая зона остаётся пустой, а занятое зарезервированное место не дублирует карточку здания', () =>
{
    const dto = livePlacement(); dto.installed = dto.installed.filter(item => item.slotId !== 'building-3'); dto.slots[3].installedDefinitionId = null;
    dto.slots[0].reservedByOrderId = 'order-upgrade';
    const graph = planetStartupGraph(dto); assert.equal(graph.districts[0].zones[2].buildings.length, 0);
    const root = new Element('div'); renderPlanetPanel(root, model(dto), { document });
    const emptyZone = root.all().find(item => item.dataset.zoneInstance === 'foundry');
    assert.equal(emptyZone.all().filter(item => item.dataset.buildingInstance).length, 0);
    assert.equal(emptyZone.all().filter(item => item.dataset.slotId === 'free').length, 1, 'Свободное место находится внутри полученной зоны');
    assert.match(root.text, /Заказ: order-upgrade/);
    assert.equal(root.all().filter(item => item.dataset.buildingInstance === 'startup-0').length, 1);
    assert.equal(root.all().filter(item => item.className?.split(' ').includes('planet-slot') && item.text.includes('building_capital')).length, 0);
});

test('Замена в занятом месте убирает старое startup определение и показывает authoritative runtime', () =>
{
    const dto = livePlacement(); dto.installed[1].definitionId = 'building_foundry_1'; dto.slots[1].installedDefinitionId = 'building_foundry_1';
    const graph = planetStartupGraph(dto);
    assert.equal(graph.districts[0].zones[0].buildings.length, 2);
    assert.equal(graph.districts[0].zones[0].buildings.some(item => item.definitionId === 'building_research_lab_1'), false);
    assert.equal(graph.districts[0].zones[0].buildings.some(item => item.definitionId === 'building_foundry_1'), true);
    dto.slots[1].zoneInstanceId = null;
    assert.equal(planetStartupGraph(dto).districts[0].zones[0].buildings.some(item => item.definitionId === 'building_foundry_1'), true, 'Старый exact slot сохраняет single-zone binding');
});

test('Прогресс относится к точному зарезервированному слоту и отдельно отображается в очереди', () =>
{
    const dto = livePlacement(); dto.slots.find(item => item.slotId === 'reserved').reservedByOrderId = 'order-a';
    dto.queue = [{ orderId: 'order-a', slotId: 'reserved', definitionId: 'building_research_lab_1', completedWork: 4, requiredWork: 10, workUnit: 'policyWork', status: 'active', paidCosts: {}, gates: [] },
        { orderId: 'order-b', slotId: 'free', definitionId: 'building_factory_1', completedWork: 0, requiredWork: 20, workUnit: 'policyWork', status: 'pending', paidCosts: {}, gates: [] }];
    const before = structuredClone(dto); const root = new Element('div'); renderPlanetPanel(root, model(dto), { document });
    const zone = root.all().find(item => item.dataset.zoneInstance === 'foundry');
    const reserved = zone.all().find(item => item.className?.split(' ').includes('planet-slot') && item.all().some(child => child.dataset.slotId === 'reserved'));
    const bar = reserved.all().find(item => item.tag === 'progress'); assert.equal(bar.value, 4); assert.equal(bar.max, 10);
    const queue = root.all().filter(item => item.dataset.orderId); assert.equal(queue.length, 2);
    assert.equal(queue.find(item => item.dataset.orderId === 'order-a').all().find(item => item.tag === 'progress').value, 4);
    assert.equal(zone.all().find(item => item.dataset.buildingInstance === 'startup-3').all().some(item => item.tag === 'progress'), false);
    assert.deepEqual(dto, before);
    dto.queue[0].requiredWork = 0; renderPlanetPanel(root, model(dto), { document });
    assert.equal(root.all().find(item => item.dataset.orderId === 'order-a').all().find(item => item.tag === 'progress').value, undefined, 'Неизвестный прогресс не становится 0%');
});

test('Ресурсные районы с собственными зонами остаются нижними группами; имена кнопок различают места', () =>
{
    const dto = livePlacement();
    for (const [id, zone] of [['generator', 'energy'], ['mining', 'minerals'], ['farming', 'food']])
    {
        dto.startupDistricts.push({ instanceId: id, definitionId: `district_${id}`, level: 2 });
        dto.startupZones.push({ instanceId: `${id}-zone`, districtInstanceId: id, slotInstanceId: `${id}-zone-slot`, zoneId: `zone_${zone}`, slotNumber: 1 });
    }
    const root = new Element('div'); renderPlanetPanel(root, model(dto), { document });
    const rural = root.all().find(item => item.className === 'planet-resource-districts');
    assert.deepEqual(rural.children.map(item => item.dataset.districtInstance), ['generator', 'mining', 'farming']);
    const cities = root.all().find(item => item.className === 'planet-city-districts'); assert.deepEqual(cities.children.map(item => item.dataset.districtInstance), ['city']);
    const free = root.all().find(item => item.dataset.slotId === 'free'); const reserved = root.all().find(item => item.dataset.slotId === 'reserved');
    assert.notEqual(free.ariaLabel, reserved.ariaLabel); assert.match(free.ariaLabel, /место free/); assert.match(reserved.ariaLabel, /место reserved/);
});

test('Явные original ключи профессии и описания имеют приоритет; текст не становится HTML', () =>
{
    const dto = livePlacement(); dto.economy = { population: 60, baseWorkforce: 60, reportMonth: 0, resources: [], jobs: [{ jobId: 'alias', localizationKey: 'original_job_key', name: 'Старое имя сервера', categoryId: 'specialist', capacity: 20, limit: 15 }] };
    dto.installed[0].localizationKey = 'original_building_key'; dto.installed[0].descriptionKey = 'original_description_key';
    const dictionary = { original_job_key: 'Оригинальная профессия', original_building_key: 'Оригинальное здание', original_description_key: '<img src=x> Описание оригинала' };
    const resolveLocalization = (key, fallback) => dictionary[key] ?? fallback ?? key;
    const root = new Element('div'); renderPlanetPanel(root, model(dto), { document, tab: 'jobs', resolveLocalization });
    assert.match(root.text, /Оригинальная профессия/); assert.doesNotMatch(root.text, /Старое имя сервера/);
    renderPlanetPanel(root, model(dto), { document, inspection: { kind: 'installed', slotId: 'building-0', definitionId: 'building_capital' }, resolveLocalization });
    assert.match(root.text, /Оригинальное здание/); assert.match(root.text, /<img src=x> Описание оригинала/);
    assert.equal(root.all().filter(item => item.tag === 'img').length, 0, 'Текст локализации не создаёт HTML узлы');
});

test('Локализация покрывает amounts, класс, feature/zone/queue metadata и подтверждённые категории', () =>
{
    const dto = livePlacement(); dto.planetClass = 'pc_continental';
    dto.economy = { population: 60, baseWorkforce: 60, reportMonth: 1, resources: [{ resourceId: 'energy', stock: 20, net: 3 }], jobs: [{ jobId: 'technician', localizationKey: 'job_original', name: 'Старое имя', categoryId: 'worker', assigned: 5, capacity: 20, limit: 20 }] };
    dto.installed[0].infrastructureUpkeep = { energy: 3 }; dto.installed[0].jobCapacity = { technician: 5 };
    dto.startupZones[0].localizationKey = 'zone_original';
    dto.queue = [{ orderId: 'order', slotId: 'reserved', definitionId: 'building_factory_1', localizationKey: 'queue_original', completedWork: 1, requiredWork: 10, workUnit: 'work', status: 'active', paidCosts: { energy: 2 }, gates: [] }];
    dto.features = [{ instanceId: 'feature', definitionId: 'feature_id', localizationKey: 'feature_original', descriptionKey: 'feature_desc', clearable: false, gates: [] }];
    const dictionary = { pc_continental: 'Континентальный мир', energy: 'Энергоресурс', job_original: 'Рабочий оригинала', zone_original: 'Зона оригинала', queue_original: 'Заказ оригинала', feature_original: 'Особенность оригинала', feature_desc: 'Описание особенности', pop_cat_worker: 'Категория оригинала' };
    const calls = []; const resolveLocalization = (key, fallback) => { calls.push(key); return dictionary[key] ?? fallback ?? key; };
    const root = new Element('div');
    renderPlanetPanel(root, model(dto), { document, resolveLocalization, inspection: { kind: 'installed', slotId: 'building-0', definitionId: 'building_capital' } });
    assert.match(root.text, /Класс: Континентальный мир/); assert.match(root.text, /Энергоресурс: 3/); assert.match(root.text, /Рабочий оригинала: 5/); assert.match(root.text, /Зона оригинала/); assert.match(root.text, /Заказ оригинала/);
    assert.match(root.all().find(item => item.dataset.orderId === 'order').all().find(item => item.tag === 'progress').title, /Заказ оригинала/);
    renderPlanetPanel(root, model(dto), { document, resolveLocalization, tab: 'features' }); assert.match(root.text, /Особенность оригинала/); assert.match(root.text, /Описание особенности/);
    renderPlanetPanel(root, model(dto), { document, resolveLocalization, tab: 'jobs' }); assert.match(root.text, /Категория оригинала/);
    assert.equal(calls.some(key => /[А-Яа-я]/.test(key)), false, 'Собственные готовые подписи не считаются ключами оригинала');
    const legacy = { id: 'c', gates: [], economy: { population: 1, workforce: 1, jobs: [], reportMonth: 1, report: { produced: { energy: 4 }, net: { energy: 3 } } } };
    renderPlanetPanel(root, legacy, { document, resolveLocalization }); assert.match(root.text, /Энергоресурс/); assert.doesNotMatch(root.text, /energy/);
});

test('Экономические metadata задают plural-профессию вместимости, исходный класс и alias ресурсов', () =>
{
    const dto = livePlacement();
    dto.economy = {
        profileId: 'synthetic', population: 20, baseWorkforce: 20, reportMonth: 1,
        jobDefinitions: [
            { jobId: 'unassigned', categoryId: 'economic_a', sourceSocialClassId: 'worker', localizationKey: 'job.singular', pluralLocalizationKey: 'job.plural', gates: [] },
            { jobId: 'job_a', categoryId: 'economic_a', sourceSocialClassId: 'worker', localizationKey: 'job.singular', pluralLocalizationKey: 'job.plural' },
            { jobId: 'job_b', categoryId: 'economic_b', sourceSocialClassId: 'worker', localizationKey: 'job.singular', pluralLocalizationKey: 'job.plural' },
            { jobId: 'legacy', categoryId: 'specialist' }
        ],
        jobs: [
            { jobId: 'job_a', categoryId: 'economic_a', sourceSocialClassId: 'worker', socialClassImageDefinitionKey: 'class.worker', localizationKey: 'job.singular', pluralLocalizationKey: 'job.plural', assigned: 2, capacity: 8, limit: 8 },
            { jobId: 'job_b', categoryId: 'economic_b', sourceSocialClassId: 'worker', localizationKey: 'job.singular', pluralLocalizationKey: 'job.plural', assigned: 3, capacity: 9, limit: 9 },
            { jobId: 'legacy', categoryId: 'specialist', assigned: 1, capacity: 2, limit: 2 }
        ],
        resources: [{ resourceId: 'resource_alias', localizationKey: 'resource.synthetic', stock: 12, sourceMaximum: 30, produced: 4, jobUpkeep: 1, buildingUpkeep: 0, districtUpkeep: 0, zoneUpkeep: 0, net: 3 }]
    };
    dto.catalog = [{ definitionId: 'synthetic_catalog', kind: 'building', eligibility: 'available', slotIds: ['free'], quote: { costs: {}, infrastructureUpkeep: {}, jobCapacity: { unassigned: 4, legacy: 1 }, durationDays: 1 }, gates: [] }];
    const dictionary = { 'job.singular': 'Одна профессия', 'job.plural': 'Много профессий', 'resource.synthetic': 'Синтетический ресурс', pop_cat_worker: 'Синтетический социальный класс' };
    const resolveLocalization = (key, fallback) => dictionary[key] ?? fallback ?? key;
    const root = new Element('div'); const imageKeys = [];
    renderPlanetPanel(root, model(dto), { document, tab: 'infrastructure', inspection: { kind: 'catalog', slotId: 'free', definitionId: 'synthetic_catalog' }, resolveLocalization });
    assert.match(root.text, /Много профессий: 4/);
    assert.doesNotMatch(root.text, /Одна профессия: 4/);
    renderPlanetPanel(root, model(dto), { document, tab: 'jobs', resolveLocalization, appendImage: (_parent, key) => { imageKeys.push(key); return true; } });
    assert.match(root.text, /Синтетический социальный класс/);
    assert.match(root.text, /Много профессий/);
    assert.ok(imageKeys.includes('class.worker'));
    assert.doesNotMatch(root.text, /Одна профессия/);
    assert.equal(root.all().filter(item => item.className === 'planet-job-category').length, 2, 'Две экономические категории рабочего класса объединяются');
    assert.match(root.text, /Специалисты/);
    renderPlanetPanel(root, model(dto), { document, inspection: { kind: 'installed', slotId: 'building-0', definitionId: 'building_capital' }, resolveLocalization });
    assert.match(root.text, /Синтетический ресурс/);
});

test('Legacy categoryLocalizationKey remains active when source social class metadata is absent', () =>
{
    const dto = livePlacement(); dto.economy = { population: 1, baseWorkforce: 1, reportMonth: 0, resources: [], jobs: [
        { jobId: 'legacy_job', categoryId: 'custom_category', categoryLocalizationKey: 'legacy.category', assigned: 1, capacity: 1, limit: 1 }
    ] };
    const root = new Element('div');
    renderPlanetPanel(root, model(dto), { document, tab: 'jobs', resolveLocalization: (key, fallback) => key === 'legacy.category' ? 'Сохранённая категория' : fallback ?? key });
    assert.match(root.text, /Сохранённая категория/);
});

test('Каталог скрывает locked tier, сохраняет вариант при нехватке денег и не создаёт неподтверждённое действие', () =>
{
    const dto = livePlacement();
    const entry = { definitionId: 'building_research_lab_2', kind: 'building', eligibility: 'unavailable', slotIds: [], compatibleSlotIds: ['free'], quote: null, gates: [{ code: 'prerequisite', dependencyId: 'tech_required' }], sourceTier: 2, prerequisites: ['tech_required'], upgradesTo: ['building_research_lab_3'], canAfford: null, localizationKey: 'lab_name' };
    dto.catalog = [entry]; const current = model(dto); const root = new Element('div');
    const inspection = { kind: 'catalog', slotId: 'free', definitionId: entry.definitionId };
    assert.deepEqual(resolvePlanetInspection(current, inspection), { kind: 'slot', slotId: 'free' });
    renderPlanetPanel(root, current, { document, inspection: { kind: 'slot', slotId: 'free' }, resolveLocalization: (key, fallback) => ({ lab_name: 'Улучшенная лаборатория', tech_required: 'Нужная технология' })[key] ?? fallback ?? key });
    assert.doesNotMatch(root.text, /Улучшенная лаборатория/); assert.match(root.text, /Нет доступных вариантов/);
    assert.equal(constructionOperations(current).length, 0);
    entry.eligibility = 'available'; entry.gates = []; entry.slotIds = ['free']; entry.quote = { policyId: 'quote', durationDays: 5, costs: { minerals: 10 }, infrastructureUpkeep: {}, jobCapacity: {} }; entry.canAfford = false;
    assert.equal(constructionOperations(current).length, 0, 'Серверная нехватка средств блокирует действие');
    renderPlanetPanel(root, current, { document, inspection }); assert.match(root.text, /Недостаточно ресурсов/);
    entry.canAfford = null; assert.equal(constructionOperations(current).filter(item => item.type === 'enqueueConstruction').length, 1, 'Legacy/null не подменяется false');
    entry.compatibleSlotIds = []; assert.deepEqual(resolvePlanetInspection(current, inspection), { kind: 'slot', slotId: 'free' }, 'При исчезновении определения сохраняется точное место');
    delete entry.compatibleSlotIds; assert.deepEqual(resolvePlanetInspection(current, inspection), inspection, 'Legacy wire сохраняется');
    dto.catalog.push(structuredClone(entry)); assert.deepEqual(resolvePlanetInspection(current, inspection), { kind: 'slot', slotId: 'free' }); assert.equal(constructionOperations(current).length, 0);
});

test('Неоднозначная legacy совместимость и удалённый runtime экземпляр не создают ложные карточки', () =>
{
    const dto = livePlacement(); dto.slots[0].zoneInstanceId = null; dto.startupBuildings[0].compatibleZoneInstanceIds = ['research', 'industry'];
    dto.startupBuildings[0].slotInstanceId = 'different-slot';
    let graph = planetStartupGraph(dto); assert.equal(graph.unplacedBuildings.filter(item => item.slotId === 'building-0').length, 1);
    dto.installed = dto.installed.filter(item => item.slotId !== 'building-1'); dto.slots[1].installedDefinitionId = null;
    graph = planetStartupGraph(dto);
    assert.equal(graph.districts[0].zones[0].buildings.some(item => item.definitionId === 'building_research_lab_1'), false);
});

test('Скрытая залежь не активная особенность; runtime clearance сохраняет точную identity', () =>
{
    const dto = development(); dto.features.push({ instanceId: 'blocker', definitionId: 'd_slums', clearable: true, clearanceSlotId: 'clear-exact', gates: [] });
    const root = new Element('div'); const calls = [];
    renderPlanetPanel(root, model(dto), { document, tab: 'features', appendConstruction: (parent, identity) => calls.push(identity) });
    assert.match(root.text, /Препятствия/); assert.match(root.text, /Пассивные особенности/); assert.match(root.text, /Скрывает связанную залежь/);
    assert.doesNotMatch(root.text, /d_rich_mountain/); assert.equal(root.all().filter(item => item.dataset.featureInstance === 'blocker').length, 1);
    assert.deepEqual(calls, [{ instanceId: 'blocker' }]);
    dto.features.push({ instanceId: 'hidden', definitionId: 'd_rich_mountain', clearable: false, gates: [] });
    const revealed = planetStartupGraph(dto).features.find(item => item.instanceId === 'hidden');
    assert.equal(revealed.isHidden, false, 'Runtime-проекция открыла ранее скрытую залежь');
});

test('Raw WF регуляторы передают направление, Shift min/max, абсолютный draft; Ctrl не угадывает единицу', () =>
{
    const root = new Element('div'); const requests = []; const job = { jobId: 'miner', capacity: 240, limit: 107.25 };
    const controls = appendPlanetWorkforceControls(root, { document, job, onRequest: request => requests.push(request) });
    const [minus, plus, input, apply] = controls.children;
    plus.onclick({}); minus.onclick({}); plus.onclick({ shiftKey: true }); minus.onclick({ shiftKey: true }); plus.onclick({ ctrlKey: true }); input.value = '113.7'; apply.onclick();
    assert.deepEqual(requests, [{ kind: 'adjust', direction: 1 }, { kind: 'adjust', direction: -1 }, { kind: 'max' }, { kind: 'min' }, { kind: 'unsupportedUnit' }, { kind: 'absolute', raw: '113.7' }]);
    assert.equal(input.max, '240'); assert.equal(input.step, 'any'); assert.equal(job.limit, 107.25);
    for (const state of [{ disabled: true }, { pending: true }])
    {
        const section = appendPlanetWorkforceControls(root, { document, job, ...state, onRequest: () => assert.fail('disabled callback') });
        section.children[0].onclick({}); section.children[3].onclick(); assert.equal(section.children[2].disabled, true);
    }
});

test('Live экономика разделяет запасы/потоки/виды содержания; unlimited reserve без фиктивного cap', () =>
{
    const dto = development(); dto.economy = { profileId: 'basic', population: 60, baseWorkforce: 60, reportMonth: 1, jobs: [{ jobId: 'physicist', categoryId: 'specialist', imageDefinitionKey: 'job.physicist', capacity: 20, limit: 15, assigned: 12, bonusWorkforce: 3, effectiveWorkforce: 15 }], resources: [{ resourceId: 'physics_research', storagePolicy: 'unlimited-reserve', stock: 200, produced: 45, jobUpkeep: 1, buildingUpkeep: 2, districtUpkeep: 3, zoneUpkeep: 4, net: 35 }] };
    const root = new Element('div'); const keys = []; let controls = 0;
    renderPlanetPanel(root, model(dto), { document, tab: 'jobs', appendImage: (parent, key) => { keys.push(key); return true; }, appendResourceIcon: (parent, id) => keys.push(`resource.${id}`), appendWorkforce: () => { controls++; } });
    assert.match(root.text, /Базовая Workforce: 60/); assert.match(root.text, /бонусная 3/); assert.match(root.text, /эффективная 15/);
    const meter = root.all().find(item => item.tag === 'meter'); assert.equal(meter?.value, '12'); assert.equal(meter?.max, '20');
    assert.equal(meter?.['aria-label'], 'Обычная Workforce 12 из вместимости 20');
    assert.equal(root.all().filter(item => item.className === 'planet-settlement-source').length, 4);
    assert.match(root.text, /Профессии\s+Физика 1/); assert.match(root.text, /Здания\s+Физика 2/); assert.match(root.text, /Районы\s+Физика 3/); assert.match(root.text, /Зоны\s+Физика 4/);
    assert.match(root.text, /Без ограничения/); assert.match(root.text, /200/); assert.match(root.text, /\+35/); assert.equal(controls, 1);
    assert.deepEqual(new Set(keys), new Set(['resource.physics_research', 'job.physicist']));
    dto.economy.reportMonth = 0; renderPlanetPanel(root, model(dto), { document }); assert.doesNotMatch(root.text, /\+35/); assert.match(root.text, /200/);
});

test('Научные профессии с null server image key используют только полученные ключи импортированного профиля', () =>
{
    const dto = development();
    dto.economy = { profileId: 'basic', population: 40, baseWorkforce: 40, reportMonth: 0, resources: [], jobs: [
        ...['physicist', 'biologist', 'engineer'].map(jobId => ({ jobId, categoryId: 'specialist', imageDefinitionKey: null, capacity: 10, limit: 10, assigned: 5, bonusWorkforce: 0, effectiveWorkforce: 5 })),
        { jobId: 'unknown_job', categoryId: 'specialist', imageDefinitionKey: null },
        { jobId: 'miner', categoryId: 'worker', imageDefinitionKey: 'job.explicit_miner' }
    ] };
    const imported = new Set(['job.physicist', 'job.biologist', 'job.engineer']);
    const requested = []; const root = new Element('div');
    renderPlanetPanel(root, model(dto), { document, tab: 'jobs',
        resolveImageKey: (kind, id) => imported.has(`${kind}.${id}`) ? `${kind}.${id}` : null,
        appendImage: (parent, key) => { requested.push(key); return imported.has(key); } });
    assert.deepEqual([...new Set(requested)], ['job.physicist', 'job.biologist', 'job.engineer', 'job.explicit_miner']);
    assert.doesNotMatch(root.text, /Изображение недоступно: job\.unknown_job/);
    assert.match(root.text, /Изображение недоступно: job\.explicit_miner/);
    imported.clear(); requested.length = 0;
    renderPlanetPanel(root, model(dto), { document, tab: 'jobs', resolveImageKey: (kind, id) => imported.has(`${kind}.${id}`) ? `${kind}.${id}` : null, appendImage: (parent, key) => { requested.push(key); return false; } });
    assert.deepEqual([...new Set(requested)], ['job.explicit_miner'], 'При отсутствии импортированных научных ключей путь не угадывается');
});
