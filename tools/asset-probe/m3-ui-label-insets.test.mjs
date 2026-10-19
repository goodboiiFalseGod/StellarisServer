import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { labelInsets } from '../../src/Stellaris.Server/wwwroot/m3-ui-presenter.mjs';

const rect = (left, top, width, height) => ({ left, top, width, height, right: left + width, bottom: top + height });
const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
const syncSource = client.slice(client.indexOf('function syncLabelInsets()'), client.indexOf('function closeSelection()'));

test('Measured side panels exclude labels using canvas-relative inner edges and existing clearance', () =>
{
    const canvas = rect(50, 20, 1280, 720);
    const insets = labelInsets(720, 36, 610, { canvas, leftPanel: rect(110, 400, 260, 250), rightPanel: rect(1062, 120, 260, 210) });
    assert.deepEqual(insets, { top: 44, bottom: 122, left: 336, right: 284 });
    const wider = labelInsets(720, 36, 610, { canvas, rightPanel: rect(1000, 120, 322, 210) });
    assert.equal(wider.right, 346);
});

test('Absent, zero-size and off-canvas panels retain edge padding; overlapping panels may exhaust label area', () =>
{
    const canvas = rect(0, 0, 390, 844);
    for (const rightPanel of [null, rect(100, 100, 0, 200), rect(100, 100, 200, 0), rect(400, 100, 260, 200), rect(100, 850, 260, 200)])
    {
        assert.equal(labelInsets(844, 66, 731, { canvas, rightPanel }).right, 16);
    }
    const exhausted = labelInsets(844, 66, 731, { canvas, leftPanel: rect(42, 400, 340, 200), rightPanel: rect(125, 125, 260, 200) });
    assert.ok(exhausted.left + exhausted.right >= canvas.width);
});

function harness()
{
    let published; let personal = true;
    const nodes = new Map();
    const node = (bounds = rect(0, 0, 0, 0)) => ({ hidden: false, getBoundingClientRect: () => bounds, replaceChildren() {}, setAttribute() {}, querySelectorAll: () => [] });
    nodes.set('canvas', node(rect(50, 20, 1280, 720)));
    nodes.set('outliner-panel', node(rect(1062, 120, 260, 210)));
    nodes.set('pause-state', { ...node(rect(594, 610, 190, 20)), hidden: true });
    const selection = { ...node(rect(110, 400, 260, 250)), hidden: true };
    const context = {
        $: id => { if (!nodes.has(id)) { nodes.set(id, node()); } return nodes.get(id); },
        document: { querySelector: selector => selector === '.selection-panel' ? selection : selector === '.topbar' ? node(rect(0, 0, 1280, 56)) : node(rect(530, 630, 320, 100)), documentElement: { style: { setProperty() {} } } },
        renderer: { setLabelInsets(value) { published = value; }, resize() {} }, labelInsets,
        state: {}, selected: null, outlinerCollapsed: false, viewParticipantId: () => personal ? 1 : null, localized: (_key, fallback) => fallback,
        selectionCard: () => null, planetPanelModel: () => null, ownFleet: () => null, movementRows: () => [], element() {}, descriptionRows() {}
    };
    vm.runInNewContext(syncSource, context);
    return { context, selection, nodes, published: () => published, general: () => { personal = false; } };
}

test('Production sync and outliner toggle release collapsed/general bounds and remeasure resize', () =>
{
    const { context, nodes, published, general } = harness();
    const source = client.slice(client.indexOf('function toggleOutliner()'), client.indexOf("$('outliner-toggle').onclick"));
    vm.runInNewContext(source, context);
    context.syncLabelInsets(); assert.equal(published().right, 284); assert.equal(published().left, 16);
    context.toggleOutliner(); assert.equal(published().right, 16);
    context.toggleOutliner(); assert.equal(published().right, 284);
    general(); context.renderOutlinerVisibility(); context.syncLabelInsets(); assert.equal(published().right, 16);
    nodes.get('outliner-panel').hidden = false;
    nodes.get('canvas').getBoundingClientRect = () => rect(50, 20, 1500, 720);
    let resize;
    context.window = { addEventListener: (name, callback) => { assert.equal(name, 'resize'); resize = callback; } };
    const resizeSource = client.match(/window\.addEventListener\('resize',[^\n]+/)[0];
    vm.runInNewContext(resizeSource, context); resize(); assert.equal(published().right, 504);
});

test('Production selection rendering measures visible left panel and releases it when cleared', () =>
{
    const { context, selection, published } = harness();
    context.selectionCard = () => ({ title: 'Selected', subtitle: 'Fleet', rows: [] });
    vm.runInNewContext(client.slice(client.indexOf('function renderSelection()'), client.indexOf('function renderIdentity()')), context);
    context.renderSelection(); assert.equal(selection.hidden, false); assert.equal(published().left, 336);
    context.selectionCard = () => null;
    context.renderSelection(); assert.equal(selection.hidden, true); assert.equal(published().left, 16);
});
