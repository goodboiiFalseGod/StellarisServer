import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';

const html = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3.html', import.meta.url), 'utf8');
const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);

test('Planet lower navigation follows content and retains the accessible Close control', () =>
{
    const dialog = html.match(/<dialog id="planet-dialog"[\s\S]*?<\/dialog>/)[0];
    assert.ok(dialog.indexOf('id="planet-content"') < dialog.indexOf('class="planet-tabs pm-tabs"'));
    assert.match(dialog, /data-close aria-label="Закрыть колонию"/);
    for (const tab of ['infrastructure', 'features', 'jobs']) { assert.match(dialog, new RegExp(`data-planet-tab="${tab}"`)); }
    assert.doesNotMatch(dialog, /aria-pressed=/);
    assert.equal([...dialog.matchAll(/aria-controls="planet-page"/g)].length, 5);
});

test('Planet map navigation is a single visible text action with usable narrow-screen controls', () =>
{
    const dialog = html.match(/<dialog id="planet-dialog"[\s\S]*?<\/dialog>/)[0];
    const heading = dialog.match(/<div class="dialog-heading pm-title">([\s\S]*?)<\/div>/)[1];
    assert.equal([...dialog.matchAll(/id="show-planet-on-map"/g)].length, 1);
    assert.match(heading, /<button id="show-planet-on-map" type="button" aria-label="Показать колонию на карте">Показать на карте<\/button>/);
    assert.match(heading, /<button data-close aria-label="Закрыть колонию">/);
    assert.doesNotMatch(heading, /id="show-planet-on-map"[^>]*disabled/);

    const css = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3.css', import.meta.url), 'utf8');
    const narrow = css.match(/@media \(max-width: 620px\) \{([^\n]+)/)[1];
    assert.match(css, /#show-planet-on-map\s*\{[^}]*min-height:\s*26px/);
    assert.match(narrow, /#planet-dialog \.dialog-heading\s*\{[^}]*flex-wrap:\s*wrap/);
    assert.match(narrow, /#planet-dialog \.dialog-heading \[data-close\]\s*\{[^}]*min-height:\s*32px/);
    assert.match(narrow, /#show-planet-on-map\s*\{[^}]*min-height:\s*30px;\s*white-space:\s*normal/);
    assert.match(narrow, /\.planet-tabs\s*\{[^}]*position:\s*sticky/);
    assert.match(html, /<p id="command-feedback" role="status">/);
});

test('DOM сохраняет уникальные точки подключения существующего клиента', () =>
{
    assert.equal(ids.length, new Set(ids).size, 'Дублированные id');
    const expected = 'clock resources host-controls pause speed step setup origin join name code role connect disconnect clear-login connection folder files cancel-import import-status required-files participants configure traits save-config ready start error stale galaxy reset-camera focus stress view-name outliner map canvas hint metrics selection command-fleet destination move-system stop movement economy economy-content commands'.split(' ');
    for (const id of expected) { assert.ok(ids.includes(id), `Missing binding ${id}`); }
    for (const id of ['x', 'z', 'move-point']) { assert.ok(!ids.includes(id), `Removed point-order control ${id} is still present`); }
    for (const [, id] of client.matchAll(/\$\('([^']+)'\)/g)) { assert.ok(ids.includes(id), `Нет элемента ${id}`); }
});

test('Формы, ограничения полей, варианты ролей и скоростей сохранены', () =>
{
    for (const id of ['join', 'configure']) { assert.match(html, new RegExp(`<form id="${id}"`)); }
    assert.match(html, /<input id="name" maxlength="32" required autocomplete="nickname">/);
    assert.match(html, /<input id="code" type="password" required autocomplete="off">/);
    assert.match(html, /<input id="files" type="file" multiple>/);
    assert.doesNotMatch(html, /class="coordinates"|id="(?:x|z|move-point)"/);
    assert.doesNotMatch(client, /\$\('(x|z|move-point)'\)/);
    const css = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3.css', import.meta.url), 'utf8');
    assert.doesNotMatch(css, /\.coordinates(?:\s|\{|\.)/);
    assert.match(html, /<option value="player">Игрок<\/option><option value="observer">Наблюдатель<\/option>/);
    assert.match(html, /<option value="1">1 день\/с<\/option><option value="2">2 дня\/с<\/option><option value="4">4 дня\/с<\/option>/);
});

test('Ошибки, лабораторная маркировка и исходный entrypoint доступны', () =>
{
    assert.match(html, /<div id="error" role="alert" hidden>/);
    assert.match(html, /<div id="stale" hidden>/);
    assert.match(html, /<span id="host-controls" hidden>/);
    assert.match(html, /<ol id="commands" aria-live="polite" tabindex="0" aria-label="Состояние отправленных команд">/);
    assert.match(html, /Лаборатория экономики/);
    assert.match(html, /CSS-аналог интерфейса/);
    assert.match(html, /<script type="module" src="\/m3-client.mjs"><\/script>/);
    assert.doesNotMatch(html, /\bon(?:click|change|submit|toggle)=/i);
});

test('Connection status and latest command feedback remain outside modal dialogs', () =>
{
    const ancestors = [];
    const found = new Set();
    const voidTags = new Set(['meta', 'link', 'input', 'br', 'hr', 'img']);
    for (const [token, close, rawTag, attributes] of html.matchAll(/<(\/)?([a-z][\w-]*)([^>]*)>/gi))
    {
        const tag = rawTag.toLowerCase();
        if (close) { assert.equal(ancestors.pop(), tag, `Unexpected closing tag ${token}`); continue; }
        const id = attributes.match(/\bid="([^"]+)"/)?.[1];
        if (['connection', 'command-feedback', 'pending-count', 'error', 'stale'].includes(id))
        {
            assert.ok(!ancestors.includes('details'), `${id} must remain accessible when details collapse`);
            assert.ok(!ancestors.includes('dialog'), `${id} starts outside modal dialogs`);
            found.add(id);
        }
        if (!voidTags.has(tag)) { ancestors.push(tag); }
    }
    assert.equal(found.size, 5);
    assert.equal(ancestors.length, 0);
});

test('Modal feedback precedes controls, keeps diagnostics clickable and survives delayed close/reopen without duplication', () =>
{
    const queued = [];
    class Node
    {
        constructor(id, tag = 'div') { this.id = id; this.tag = tag; this.children = []; this.dataset = {}; this.events = {}; this.open = false; }
        move(nodes, first) { for (const node of nodes) { if (node.parent) { node.parent.children = node.parent.children.filter(item => item !== node); } node.parent = this; } this.children = first ? [...nodes, ...this.children] : [...this.children, ...nodes]; }
        append(...nodes) { this.move(nodes, false); }
        prepend(...nodes) { this.move(nodes, true); }
        addEventListener(type, callback) { this.events[type] = callback; }
        showModal() { this.open = true; }
        close() { this.open = false; queued.push(() => this.events.close()); }
        closest() { return this.parent; }
        descendants() { return this.children.flatMap(node => [node, ...node.descendants()]); }
        querySelectorAll(selector) { return this.descendants().filter(node => selector === '.alerts, .network-status' ? ['alerts', 'network'].includes(node.id) : selector === 'dialog' ? node.tag === 'dialog' : selector === 'dialog[open]' ? node.tag === 'dialog' && node.open : selector === '[data-close]' ? node.dataset.close != null : selector === '[data-panel]' ? node.dataset.panel != null : false); }
    }
    const body = new Node('body'); const setup = new Node('setup-dialog', 'dialog'); const diagnostics = new Node('diagnostics-dialog', 'dialog'); const planet = new Node('planet-dialog', 'dialog');
    const status = new Node('network'); const alerts = new Node('alerts'); const pending = new Node('pending-toggle'); pending.dataset.panel = diagnostics.id; status.append(pending);
    for (const dialog of [setup, diagnostics]) { const close = new Node(`${dialog.id}-close`); close.dataset.close = ''; dialog.append(close); }
    body.append(status, alerts, setup, diagnostics, planet);
    const nodes = new Map(body.descendants().map(node => [node.id, node]));
    const context = { $: id => nodes.get(id), document: { body, querySelectorAll: selector => body.querySelectorAll(selector) }, planetWindow: { open() { planet.open = true; } } };
    vm.runInNewContext(client.slice(client.indexOf('function openPanel(id)'), client.indexOf('function toggleOutliner()')), context);
    context.openPanel(setup.id);
    assert.deepEqual(setup.children.map(node => node.id), ['network', 'alerts', 'setup-dialog-close']);
    pending.onclick(); while (queued.length) { queued.shift()(); }
    assert.equal(diagnostics.open, true); assert.equal(status.parent, diagnostics); assert.equal(alerts.parent, diagnostics);
    for (let count = 0; count < 3; count++)
    {
        nodes.get('diagnostics-dialog-close').onclick(); while (queued.length) { queued.shift()(); }
        assert.equal(status.parent, body); assert.equal(alerts.parent, body);
        context.openPanel(diagnostics.id);
        assert.deepEqual(diagnostics.children.map(node => node.id), ['network', 'alerts', 'diagnostics-dialog-close']);
        assert.equal(body.descendants().filter(node => node === status || node === alerts).length, 2);
    }
    context.openPanel(planet.id); while (queued.length) queued.shift()();
    assert.equal(planet.open, true); assert.equal(status.parent, body); assert.equal(alerts.parent, body);
    assert.equal(planet.children.length, 0, 'modeless окно сохраняет композицию без полосы feedback');
});
