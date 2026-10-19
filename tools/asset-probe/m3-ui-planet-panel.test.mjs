import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { planetPanelModel, renderPlanetPanel, resolvePlanetInspection } from '../../src/Stellaris.Server/wwwroot/m3-planet-panel.mjs';
import { perspectiveKey, viewParticipantId } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

function snapshot(owner = 1, observer = false)
{
    return { you: observer ? 3 : owner, viewContext: { mode: 'player', participantId: owner }, participants: [{ id: 1, role: 'player' }, { id: 2, role: 'player' }, { id: 3, role: 'observer' }],
        spatial: { bodies: [{ id: 'b1', name: 'One' }, { id: 'b2', name: 'Two' }], colonies: [{ id: 'c1', bodyId: 'b1', ownerParticipantId: 1, economyPlanetId: 11 }, { id: 'c2', bodyId: 'b2', ownerParticipantId: 2, economyPlanetId: 22 }] },
        planets: [{ id: 11, ownerId: 1, population: 600, workforce: 500, jobs: { farmer: 2 } }, { id: 22, ownerId: 2, population: 700, workforce: 600, jobs: {} }], catalog: { jobs: [{ id: 'farmer', name: 'Farmer' }] } };
}

class Element
{
    constructor(tag) { this.tag = tag; this.children = []; this.dataset = {}; this.events = {}; this.textContent = ''; this.open = false; }
    append(...nodes) { this.children.push(...nodes); }
    replaceChildren(...nodes) { this.children = nodes; }
    addEventListener(name, callback) { this.events[name] = callback; }
    get text() { return this.textContent + this.children.map(node => node.text).join(' '); }
    descendants(tag) { return this.children.flatMap(node => [...(node.tag === tag ? [node] : []), ...node.descendants(tag)]); }
}
const document = { createElement: tag => new Element(tag) };

test('Exact colony links isolate both players and observer POV; foreign and absent links never fall back', () =>
{
    for (const owner of [1, 2])
    {
        for (const observer of [false, true])
        {
            const state = snapshot(owner, observer);
            state.planets[0].report = { net: { minerals: 7 } }; state.planets[0].reportMonth = 3;
            state.planets[1].report = { net: { minerals: 99 } }; state.planets[1].reportMonth = 8;
            const model = planetPanelModel(state, { kind: 'colony', id: `c${owner}` });
            assert.equal(model.economy.population, owner === 1 ? 600 : 700);
            assert.equal(model.economy.report.net.minerals, owner === 1 ? 7 : 99);
            assert.equal(model.economy.reportMonth, owner === 1 ? 3 : 8);
            assert.equal(planetPanelModel(state, { kind: 'body', id: `b${owner}` }).id, `c${owner}`);
            assert.equal(planetPanelModel(state, { kind: 'colony', id: `c${owner === 1 ? 2 : 1}` }), null);
        }
    }
    const state = snapshot(); const selected = { kind: 'colony', id: 'c1' };
    state.planets[0].report = { net: { minerals: 7 } }; state.planets[1].report = { net: { minerals: 99 } };
    state.spatial.colonies[0].economyPlanetId = null; assert.equal(planetPanelModel(state, selected).economy, null);
    state.spatial.colonies[0].economyPlanetId = 22; assert.equal(planetPanelModel(state, selected).economy, null);
    for (const mode of ['general', 'pending']) { state.viewContext = { mode, participantId: null }; assert.equal(planetPanelModel(state, selected), null); }
    assert.equal(planetPanelModel(snapshot(), { kind: 'colony', id: 'removed' }), null);
});

test('Generated planet class IDs never appear as display names', () =>
{
    const state = snapshot();
    state.spatial.bodies[0] = { id: 'b1', name: 'pc_continental', kind: 'planet', systemId: 's1' };
    state.spatial.bodies[1] = { id: 'b2', name: 'pc_arid', kind: 'planet', systemId: 's1' };
    const selected = { kind: 'colony', id: 'c1' };
    assert.equal(planetPanelModel(state, selected).name, 'Планета 1');
    state.spatial.bodies[0].name = 'Aurelion';
    assert.equal(planetPanelModel(state, selected).name, 'Aurelion');
});

test('Closed-month summary displays received signed flows, missing values, icons and cloned report on every tab', () =>
{
    const state = snapshot();
    const longResourceId = `unmapped:${'x'.repeat(160)}`;
    const report = { produced: { minerals: 4, energy: Number.NaN, crystal: 2, [longResourceId]: Number.MAX_VALUE }, jobUpkeep: { minerals: 1 }, buildingUpkeep: { minerals: 2 }, upkeep: { minerals: 3 }, net: { minerals: 0, energy: -2, food: 1, [longResourceId]: Number.MAX_VALUE } };
    state.planets[0].report = report; state.planets[0].reportMonth = 1; state.planets[0].balances = { alloy: 900 };
    const model = planetPanelModel(state, { kind: 'colony', id: 'c1' });
    assert.notEqual(model.economy.report, report); assert.deepEqual(model.economy.report, report); assert.equal(model.economy.reportMonth, 1);
    model.economy.report.net.minerals = 500; assert.equal(report.net.minerals, 0);
    model.economy.report.net.minerals = 0;
    const icons = [];
    for (const tab of ['infrastructure', 'features', 'jobs'])
    {
        const root = new Element('div');
        renderPlanetPanel(root, model, { document, tab, appendResourceIcon(parent, id) { icons.push(id); parent.append(new Element('img')); } });
        assert.match(root.text, /Закрытый месяц: 1/); assert.match(root.text, /Население \(прототип сервера\): 600/);
        assert.match(root.text, /Итог за месяц/); assert.match(root.text, /Минералы 0/); assert.match(root.text, /Энергия -2/); assert.match(root.text, /Пища \+1/); assert.match(root.text, /crystal Недоступно/);
        assert.ok(root.text.includes(longResourceId)); assert.match(root.text, /179/);
        assert.match(root.text, /Содержание профессий/); assert.match(root.text, /Общее содержание/);
        assert.doesNotMatch(root.text, /alloy/);
        const breakdown = root.descendants('details').find(item => item.className === 'planet-settlement-breakdown');
        assert.ok(breakdown); assert.equal(breakdown.open, false);
        const tableScroll = breakdown.descendants('div').find(item => item.className === 'planet-settlement-table-scroll'); assert.ok(tableScroll);
        const table = tableScroll.descendants('table')[0]; assert.ok(table);
        assert.deepEqual(table.descendants('th').map(item => item.textContent), ['Ресурс', 'Произведено', 'Содержание профессий', 'Содержание зданий', 'Общее содержание']);
        assert.match(table.text, /Энергия Недоступно/);
    }
    assert.ok(icons.includes('minerals')); assert.ok(icons.includes('energy')); assert.ok(icons.includes('food'));
    state.planets[0].reportMonth = 2;
    assert.equal(planetPanelModel(state, { kind: 'colony', id: 'c1' }).economy.reportMonth, 2);
});

test('Missing closed report or month remains unavailable without using balances or zero defaults', () =>
{
    const state = snapshot(); state.planets[0].balances = { minerals: 42 }; state.planets[0].report = null; state.planets[0].reportMonth = 0;
    const root = new Element('div'); renderPlanetPanel(root, planetPanelModel(state, { kind: 'colony', id: 'c1' }), { document });
    assert.match(root.text, /Отчёт закрытого месяца недоступен/); assert.doesNotMatch(root.text, /42|minerals/);
    state.planets[0].report = { net: { minerals: 0 } };
    renderPlanetPanel(root, planetPanelModel(state, { kind: 'colony', id: 'c1' }), { document });
    assert.match(root.text, /Отчёт закрытого месяца недоступен/); assert.doesNotMatch(root.text, /Итог за месяц: 0/);
    state.planets[0].report = { net: { minerals: 0 } }; delete state.planets[0].reportMonth;
    renderPlanetPanel(root, planetPanelModel(state, { kind: 'colony', id: 'c1' }), { document });
    assert.match(root.text, /Отчёт закрытого месяца недоступен/); assert.doesNotMatch(root.text, /Итог за месяц: 0/);
});

test('Omitted development diagnoses unavailable state with no construction controls or invented values', () =>
{
    const state = snapshot(); const before = structuredClone(state); const root = new Element('div');
    const model = planetPanelModel(state, { kind: 'colony', id: 'c1' });
    renderPlanetPanel(root, model, { document });
    assert.match(root.text, /Население \(прототип сервера\): 600/); assert.match(root.text, /сервер не активировал/);
    assert.equal(root.descendants('button').length, 0); assert.deepEqual(state, before);
    renderPlanetPanel(root, model, { document, tab: 'jobs' }); assert.match(root.text, /Farmer/);
});

test('Received development preserves nullable quotes, image diagnostics, work units and expandable jobs', () =>
{
    const state = snapshot(); state.development = { contentProfileId: 'test', gates: [], colonies: [{ colonyId: 'c1', economyPlanetId: 11, ownerParticipantId: 1, gates: [], slots: [], installed: [],
        catalog: [{ definitionId: 'building_hydroponics_farm', kind: 'building', imageDefinitionKey: 'building.building_hydroponics_farm', eligibility: 'unresolved', slotIds: [], quote: null, gates: [] }],
        queue: [{ orderId: 'order', slotId: 'slot', definitionId: 'building', status: 'pending', completedWork: 2, requiredWork: 10, workUnit: 'policyWork', paidCosts: { minerals: 25 }, refundQuote: null, gates: [] }],
        features: [{ instanceId: 'passive-instance', definitionId: 'passive', clearable: false, gates: [] }, { instanceId: 'blocker-instance', definitionId: 'blocker', clearable: true, gates: [] }],
        jobs: [{ jobId: 'farmer', categoryId: 'worker', capacity: null, assigned: 3, workforce: null }] }] };
    const model = planetPanelModel(state, { kind: 'colony', id: 'c1' }); const root = new Element('div');
    renderPlanetPanel(root, model, { document });
    assert.match(root.text, /Стоимость и срок не подтверждены/); assert.match(root.text, /Изображение недоступно: building\.building_hydroponics_farm/);
    assert.match(root.text, /2 \/ 10/); assert.match(root.text, /Единица: policyWork/); assert.match(root.text, /Минералы: 25/); assert.equal(root.descendants('button').length, 0);
    const clearanceBindings = [];
    renderPlanetPanel(root, model, { document, tab: 'features', appendConstruction(parent, identity) { clearanceBindings.push(identity); return false; } });
    assert.match(root.text, /Пассивная особенность/); assert.match(root.text, /Препятствия/);
    assert.deepEqual(root.children.filter(item => item.className === 'planet-feature-group').map(item => item.children[0]?.textContent), ['Препятствия', 'Пассивные особенности']);
    assert.deepEqual(clearanceBindings, [{ instanceId: 'blocker-instance' }]);
    const expandedJobs = new Set(); renderPlanetPanel(root, model, { document, tab: 'jobs', expandedJobs });
    assert.match(root.text, /Вместимость Недоступно · лимит Недоступно/);
    const details = root.descendants('details').find(item => item.dataset.jobCategory); details.open = true; details.events.toggle();
    renderPlanetPanel(root, model, { document, tab: 'jobs', expandedJobs }); assert.equal(root.descendants('details').find(item => item.dataset.jobCategory).open, true);
    model.development.catalog.length = 0; assert.equal(state.development.colonies[0].catalog.length, 1);
});

test('Production planet window reconciles snapshots, clears old POV and ignores delayed close events after reopen', () =>
{
    const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
    const source = client.slice(client.indexOf('function closePlanetWindow()'), client.indexOf('function renderOutliner()'));
    const nodes = new Map(); let renders = 0; let errors = 0; let closeHandler;
    const context = { state: snapshot(), selected: { kind: 'colony', id: 'c1' }, planetSelection: null, planetTab: 'infrastructure', planetViewKey: '', planetInteractionId: 0, planetSnapshotId: 0, planetInspection: null, planetInspectionKey: '', expandedPlanetJobs: new Set(), planetPanelModel, resolvePlanetInspection, perspectiveKey, viewParticipantId, connection: { pending: new Map() }, canPlay: () => false, me: () => null, appendJobControls() {},
        $: id => { if (!nodes.has(id)) { nodes.set(id, { open: false, textContent: '', replaceChildren() {}, close() { this.open = false; }, addEventListener(name, callback) { closeHandler = callback; } }); } return nodes.get(id); },
        document: { querySelectorAll: () => [] }, planetWindow: { layout() {} }, closeResearch() {}, localizePlanetChrome() {}, localizationIssues: new Map(), localized: (key, fallback) => fallback ?? key, appendLocalizationStatus() {}, constructionOperations: () => [], appendPlanetImage() {}, renderPlanetPanel() { renders++; }, openPanel: id => { nodes.get(id).open = true; }, error() { errors++; } };
    vm.runInNewContext(source, context);
    nodes.get('open-planet').onclick(); assert.equal(nodes.get('planet-dialog').open, true); assert.equal(renders, 1);
    context.renderPlanetWindow(); assert.equal(renders, 1);
    context.state.planets[0].population = 601; context.renderPlanetWindow(); assert.equal(renders, 2);
    context.state.planets[0].report = { net: { minerals: 3 } }; context.state.planets[0].reportMonth = 1; context.renderPlanetWindow(); assert.equal(renders, 3);
    context.state.planets[0].reportMonth = 2; context.renderPlanetWindow(); assert.equal(renders, 4);
    closeHandler(); assert.equal(nodes.get('planet-dialog').open, true);
    context.state = snapshot(2, true); context.renderPlanetWindow(); assert.equal(nodes.get('planet-dialog').open, false); assert.equal(context.planetSelection, null);
    context.selected = { kind: 'colony', id: 'c2' }; nodes.get('open-planet').onclick(); assert.equal(nodes.get('planet-dialog').open, true);
    context.state.spatial.colonies = []; context.renderPlanetWindow(); assert.equal(nodes.get('planet-dialog').open, false);
    nodes.get('open-planet').onclick(); assert.equal(errors, 1);
});
