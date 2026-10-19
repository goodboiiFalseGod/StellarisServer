import test from 'node:test';
import assert from 'node:assert/strict';
import { researchEmpire, researchPanelState, renderResearchPanel } from '../../src/Stellaris.Server/wwwroot/m3-research-panel.mjs';

class Element
{
    constructor(tag = 'div') { this.tagName = tag; this.children = []; this.dataset = {}; this.attributes = {}; this.hidden = false; this.disabled = false; this.textContent = ''; }
    append(...nodes) { this.children.push(...nodes); }
    replaceChildren(...nodes) { this.children = [...nodes]; }
    setAttribute(name, value) { this.attributes[name] = value; }
    removeAttribute(name) { delete this.attributes[name]; }
    querySelectorAll(selector) { return this.children.flatMap(child => [...(selector === '[data-research-focus]' && child.dataset.researchFocus ? [child] : []), ...child.querySelectorAll(selector)]); }
    focus() { this.focused = true; }
    get lastChild() { return this.children.at(-1); }
}
const document = { activeElement: null, createElement: tag => new Element(tag) };
const tech = (id, area, status = 'deferred') => ({ id, area, name: id, category: 'category_industry', cost: 100, tier: 1, rare: true, effects: [{ description: 'Здание получает бонус', status }] });
const empire = { participantId: 7, revision: 12, areas: ['physics', 'society', 'engineering'].map((id, index) => ({ id, activeTechnologyId: index === 0 ? `${id}-choice` : null, progress: 20, monthlyResearch: 10 + index, reserve: 30 + index, choices: [tech(`${id}-choice`, id, id === 'engineering' ? 'deferred' : 'active')] })), researched: [tech('physics-known', 'physics', 'active'), tech('society-known', 'society')] };

function render(overrides = {})
{
    const main = new Element(); const side = new Element(); const view = { ...researchPanelState(), ...overrides };
    let chosen = null;
    renderResearchPanel(main, side, empire, { you: 7, role: 'player' }, { document, canChoose: true, pendingAreas: new Set(), onChoose: (...args) => { chosen = args; }, onClose() {} });
    return { main, side, view, chosen };
}
function descendants(node) { return node.children.flatMap(child => [child, ...descendants(child)]); }
function text(node) { return [node.textContent, ...descendants(node).map(child => child.textContent)].join(' '); }

test('selects only the matching POV and renders three independent science areas', () =>
{
    assert.equal(researchEmpire({ research: { empires: [empire] } }, 7), empire);
    assert.equal(researchEmpire({ research: { empires: [empire] } }, 8), null);
    const { main } = render();
    assert.equal(main.dataset.testid, 'research-panel');
    assert.deepEqual(main.children.filter(node => node.className?.startsWith('research-area')).map(node => node.dataset.area), ['physics', 'society', 'engineering']);
    assert.match(descendants(main).find(node => node.tagName === 'progress').attributes['aria-label'], /Прогресс/);
});

test('renders proposals, labels effect coverage, and disables pending/observer selection', () =>
{
    const view = { ...researchPanelState(), choicesArea: 'engineering' };
    const main = new Element(); const side = new Element();
    renderResearchPanel(main, side, empire, { ...view, you: 7, role: 'player' }, { document, canChoose: false, pendingAreas: new Set(['engineering']) });
    assert.equal(side.hidden, false);
    const all = descendants(side);
    assert.ok(all.some(node => node.tagName === 'summary' && node.textContent.includes('Отложено')));
    assert.ok(all.some(node => node.className === 'research-card-category' && node.textContent.includes('category_industry')));
    assert.ok(all.some(node => node.className === 'research-card-cost' && node.textContent === '100'));
    const choose = all.find(node => node.dataset.testid === 'research-select-engineering-engineering-choice');
    assert.equal(choose.disabled, true);
    renderResearchPanel(main, side, empire, { ...view, you: 99, role: 'observer' }, { document, canChoose: false });
    assert.equal(descendants(side).find(node => node.dataset.testid === 'research-select-engineering-engineering-choice').disabled, true);
});

test('локализует карточку, параметры направления и эффекты по проверенным ключам', async () =>
{
    const calls = [];
    const dictionary = {
        PHYSICS: 'Synthetic physics', TECHNOLOGY: 'Synthetic technology',
        TECH_UNLOCK_BUILDING: 'Synthetic unlock: {NAME}', SELECT_TECHNOLOGY: 'Synthetic selection: {AREA}', BUILDING_TEST_NAME: 'Synthetic building',
        TECH_TEST_NAME: 'Synthetic test technology', TECH_TEST_DESCRIPTION: 'Synthetic description',
        CATEGORY_TEST: 'Synthetic category', EFFECT_TEST: 'Synthetic effect'
    };
    const resolveLocalization = (key, fallback, options) =>
    {
        calls.push({ key, fallback, options });
        const template = dictionary[key];
        if (!template) { return fallback ?? key; }
        return template.replace('{NAME}', options?.parameters?.NAME ?? '').replace('{AREA}', options?.parameters?.AREA ?? '');
    };
    const technology = {
        id: 'test-tech', area: 'physics', name: 'ignored legacy name', nameKey: 'TECH_TEST_NAME',
        description: 'ignored legacy description', descriptionKey: 'TECH_TEST_DESCRIPTION',
        category: 'category_industry', categoryKey: 'CATEGORY_TEST', cost: 100,
        icon: { relativePath: 'synthetic/icon.dds', sha256: '0'.repeat(64), byteLength: 8 },
        effects: [
            { description: 'ignored effect text', localizationKey: 'EFFECT_TEST', amount: 0.05, format: 'percent', status: 'active' },
            { localizationKey: 'EFFECT_TEST', amount: -2, format: 'number', status: 'deferred' },
            { localizationKey: 'EFFECT_TEST', amount: 1, format: 'arbitraryTemplate', status: 'active' },
            { localizationKey: 'EFFECT_TEST', amount: null, format: 'percent', status: 'deferred' },
            { sourceKey: 'unlock_building', targetDefinitionId: 'synthetic_building', localizationKey: 'BUILDING_TEST_NAME', status: 'active' }
        ]
    };
    const value = { ...empire, areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: technology.id, choices: [technology] } : area) };
    const main = new Element(); const side = new Element();
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'physics' }, {
        document, canChoose: false, resolveLocalization,
        imageStore: { get: async () => ({ url: 'blob:synthetic', width: 32, height: 32 }) }
    });
    await Promise.resolve(); await Promise.resolve();
    const rendered = text(main) + ' ' + text(side);
    assert.match(rendered, /Synthetic test technology/);
    assert.match(rendered, /Synthetic description/);
    assert.match(rendered, /Synthetic category/);
    assert.match(rendered, /Synthetic effect: \+5%/);
    assert.match(rendered, /Synthetic effect: -2/);
    assert.match(rendered, /числовое представление эффекта недоступно/);
    assert.match(rendered, /числовое значение эффекта не разрешено/);
    assert.match(rendered, /Synthetic selection: Synthetic physics/);
    assert.match(rendered, /Synthetic unlock: Synthetic building/);
    assert.ok(descendants(main).some(node => node.tagName === 'img' && node.alt === 'Synthetic test technology'));
    assert.ok(calls.some(call => call.key === 'TECH_UNLOCK_BUILDING' && call.options?.parameters?.NAME === 'Synthetic building'));
    assert.ok(calls.some(call => call.key === 'SELECT_TECHNOLOGY' && call.options?.parameters?.AREA === 'Synthetic physics'));
    assert.equal(calls.some(call => call.key === 'arbitraryTemplate'), false);
});

test('отсутствующие ключи показывают явные ID без старого текста', () =>
{
    const missing = { id: 'synthetic_missing', name: 'Legacy English title', description: 'Legacy English description', category: 'category_unknown', effects: [{ description: 'Legacy English effect' }] };
    const value = { ...empire, areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: missing.id, choices: [missing] } : area) };
    const main = new Element(); const side = new Element();
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'physics' }, { document, canChoose: false });
    const rendered = text(main) + ' ' + text(side);
    assert.match(rendered, /synthetic_missing · название недоступно/);
    assert.match(rendered, /category_unknown/);
    assert.doesNotMatch(rendered, /Legacy English/);
});

test('название завершённой и изученной технологии берётся по ключу', () =>
{
    const researched = { id: 'synthetic_completed', name: 'Legacy English completed title', nameKey: 'COMPLETED_TEST_NAME' };
    const value = {
        ...empire,
        areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: null, lastCompletedTechnologyId: researched.id } : area),
        researched: [researched]
    };
    const resolver = (key, fallback) => key === 'COMPLETED_TEST_NAME' ? 'Synthetic completed title' : fallback ?? key;
    const main = new Element(); const side = new Element();
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'researched' }, { document, canChoose: false, resolveLocalization: resolver });
    assert.ok(text(main).includes('Synthetic completed title'));
    assert.ok(text(side).includes('Synthetic completed title'));
    assert.doesNotMatch(text(main) + ' ' + text(side), /Legacy English/);
});

test('Причины неразрешённых эффектов видимы без чтения старого текста и выдуманных значений', () =>
{
    const technology = { id: 'synthetic_effects', cost: 100, effects: [
        { sourceKey: 'synthetic_modifier', amount: null, format: 'percent', diagnosticCode: 'unknown-effect-swap', status: 'deferred', description: 'Ignored legacy cause' },
        { sourceKey: 'synthetic_modifier', amount: null, format: 'number', diagnosticCode: 'unsupported-effect-expression', status: 'deferred' },
        { sourceKey: 'synthetic_modifier', amount: null, format: 'number', diagnosticCode: 'constructor', status: 'deferred' },
        { sourceKey: 'unlock_building', targetDefinitionId: 'synthetic_building', amount: null, diagnosticCode: null, status: 'active' }
    ] };
    const value = { ...empire, areas: [{ id: 'physics', choices: [technology] }] };
    const main = new Element(); const side = new Element();
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'physics' }, { document, canChoose: false });
    const effects = descendants(side).filter(node => node.className?.startsWith('research-effect-details'));
    assert.match(effects[0].children[1].textContent, /числовое значение эффекта не разрешено.*Условия замены эффекта не определены/);
    assert.match(effects[1].children[1].textContent, /Выражение эффекта пока не поддержано/);
    assert.match(effects[2].children[1].textContent, /Причина недоступности: constructor/);
    assert.doesNotMatch(effects[3].children[1].textContent, /не разрешено|не поддержано|Причина недоступности/);
    assert.doesNotMatch(text(side), /Ignored legacy cause/);
});

test('researched filters retain category and no-data POV stays explicit', () =>
{
    const main = new Element(); const side = new Element(); const view = { ...researchPanelState(), choicesArea: 'researched', researchedArea: 'physics' };
    renderResearchPanel(main, side, empire, view, { document, canChoose: false, onClose() {} });
    assert.ok(descendants(side).some(node => node.dataset.technologyId === 'physics-known'));
    assert.equal(descendants(side).some(node => node.dataset.technologyId === 'society-known'), false);
    renderResearchPanel(main, side, null, view, { document, canChoose: false });
    assert.match(descendants(main).map(node => node.textContent).join(' '), /недоступны в этой перспективе/);
});

test('rerender keeps keyboard focus on a surviving control in the adjacent pane', () =>
{
    const main = new Element(); const side = new Element(); const view = { ...researchPanelState(), choicesArea: 'engineering' };
    renderResearchPanel(main, side, empire, view, { document, canChoose: true });
    const focused = descendants(side).find(node => node.dataset.testid === 'research-select-engineering-engineering-choice');
    document.activeElement = focused;
    renderResearchPanel(main, side, empire, view, { document, canChoose: true });
    assert.equal(descendants(side).find(node => node.dataset.testid === focused.dataset.testid).focused, true);
    document.activeElement = null;
});

test('completion notice uses the retained researched technology until the server clears it', () =>
{
    const main = new Element(); const side = new Element();
    const completedEmpire = { ...empire, areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: null, lastCompletedTechnologyId: 'physics-known' } : area) };
    renderResearchPanel(main, side, completedEmpire, researchPanelState(), { document, canChoose: false });
    assert.ok(descendants(main).some(node => node.className === 'research-complete' && node.textContent.includes('physics-known')));
});

test('real icon image appears on current cards and an async completion cannot mutate a stale view', async () =>
{
    const technology = { ...empire.areas[0].choices[0], icon: { relativePath: 'gfx/interface/icons/technologies/tech.dds', sha256: '0'.repeat(64), byteLength: 128 } };
    const value = { ...empire, areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: technology.id, choices: [technology] } : area) };
    const main = new Element(); const side = new Element(); let resolveImage;
    const imageStore = { get: () => new Promise(resolve => { resolveImage = resolve; }) };
    let current = true;
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'physics' }, { document, canChoose: true, imageStore, isCurrent: () => current });
    const frame = descendants(side).find(node => node.dataset.testid === 'research-icon-physics-choice');
    resolveImage({ url: 'blob:technology', width: 64, height: 64 }); await Promise.resolve(); await Promise.resolve();
    assert.equal(descendants(frame).some(node => node.tagName === 'img' && node.src === 'blob:technology'), true);
    assert.equal(frame.attributes['aria-label'], undefined);
    assert.equal(frame.attributes.title, undefined);
    assert.equal(frame.dataset.missing, undefined);

    const staleMain = new Element(); const staleSide = new Element(); let finish;
    const staleStore = { get: () => new Promise(resolve => { finish = resolve; }) }; current = true;
    renderResearchPanel(staleMain, staleSide, value, researchPanelState(), { document, canChoose: false, imageStore: staleStore, isCurrent: () => current });
    const staleFrame = descendants(staleMain).find(node => node.dataset.testid === 'research-icon-physics-choice');
    current = false; finish({ url: 'blob:stale', width: 64, height: 64 }); await Promise.resolve(); await Promise.resolve();
    assert.equal(descendants(staleFrame).some(node => node.tagName === 'img'), false);
});

test('file snapshot icon failure keeps a fixed slot and one shared local directory action', async () =>
{
    const technology = { ...empire.areas[0].choices[0], icon: { relativePath: 'gfx/interface/icons/technologies/tech.dds', sha256: '0'.repeat(64), byteLength: 128 } };
    const value = { ...empire, areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: technology.id, choices: [technology] } : area) };
    const main = new Element(); const side = new Element(); let requested = 0;
    const imageStore = { get: async () => { throw new Error('Профиля файлов недостаточно. Выберите папку установки игры.'); } };
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'physics' }, { document, canChoose: true, imageStore, onRequestDirectory: () => requested++ });
    await Promise.resolve(); await Promise.resolve();
    const picker = descendants(main).find(node => node.dataset.testid === 'research-icon-source');
    const frame = descendants(side).find(node => node.dataset.testid === 'research-icon-physics-choice');
    assert.ok(picker); assert.equal(descendants(side).some(node => node.dataset.testid?.startsWith('research-icon-source-')), false);
    assert.equal(frame.dataset.missing, 'true');
    assert.match(frame.attributes.title, /Профиля файлов недостаточно/);
    assert.match(frame.attributes['aria-label'], /Профиля файлов недостаточно/);
    assert.ok(descendants(main).some(node => node.className === 'research-source-note' && !node.hidden && /Профиля файлов недостаточно/.test(node.title)));
    picker.onclick(); assert.equal(requested, 1);
});

test('unmapped icon keeps its diagnostic on the fixed fallback slot', () =>
{
    const missing = { ...empire.areas[0].choices[0], icon: null, iconDiagnostic: 'В каталоге нет проверенного сопоставления.' };
    const value = { ...empire, areas: empire.areas.map(area => area.id === 'physics' ? { ...area, activeTechnologyId: missing.id, choices: [missing] } : area) };
    const main = new Element(); const side = new Element();
    renderResearchPanel(main, side, value, { ...researchPanelState(), choicesArea: 'physics' }, { document, canChoose: false });
    const frame = descendants(side).find(node => node.dataset.testid === 'research-icon-physics-choice');
    assert.equal(frame.dataset.missing, 'true');
    assert.match(frame.attributes.title, /нет проверенного сопоставления/);
    assert.match(frame.attributes['aria-label'], /нет проверенного сопоставления/);
    assert.match(descendants(main).find(node => node.className === 'research-source-note').title, /нет проверенного сопоставления/);
});

test('opens researched history directly and puts filters after the scrollable list', () =>
{
    const main = new Element(); const side = new Element(); const view = researchPanelState();
    renderResearchPanel(main, side, empire, view, { document, canChoose: false });
    descendants(main).find(node => node.dataset.testid === 'research-open-researched').onclick();
    assert.equal(view.choicesArea, 'researched');
    renderResearchPanel(main, side, empire, view, { document, canChoose: false });
    assert.equal(side.children.at(-1).className, 'research-filters');
    assert.equal(side.children.at(-2).className, 'research-list');
});
