import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { webcrypto } from 'node:crypto';
import vm from 'node:vm';

const source = readFileSync(new URL('./m3-app.mjs', import.meta.url), 'utf8');
const imports = source.match(/^import .*;\r?$/gm) ?? [];
assert.equal(imports.length, 2, 'Harness заменяет только два известных import, а не исполняемый код.');
assert.match(imports[0], /m3-profile\.mjs/);
assert.match(imports[1], /m3-renderer\.mjs/);
const executable = source.replace(/^import .*;\r?$/gm, '');
const paths = ['gfx/test/geometry.mesh', 'gfx/test/texture.dds'];
const visualId = 'ship.corvette.mammalian';

function sample(name)
{
    return { name, meshes: [{ name: 'shape', indices: new Uint16Array([0, 1, 2]), material: { shader: 'PdxMeshShip' } }], poseReport: { staticBindPose: false } };
}
function profile(name)
{
    return { name, decodedBytes: 16, samples: new Map([[visualId, sample(name)]]), icons: new Map([['gfx/icons/test.dds', { width: 1, height: 1, rgba: new Uint8Array([10, 20, 30, 100]) }]]) };
}

function harness()
{
    const elements = new Map();
    const renderers = [];
    const builds = [];
    const options = { no2d: false, loadFailure: false, convertedBytes: 0, candidate: profile('first') };
    let raf = null;
    let clock = 0;
    class Element
    {
        constructor(tag = 'div') { this.tagName = tag; this.handlers = new Map(); this.children = []; this.textContent = ''; this.value = ''; this.files = []; this.classList = { toggle() {} }; }
        addEventListener(name, callback) { this.handlers.set(name, callback); }
        async emit(name, args = {}) { await this.handlers.get(name)?.({ preventDefault() {}, ...args }); }
        append(child) { this.children.push(child); }
        replaceChildren(...children) { this.children = children; }
        getContext(kind)
        {
            assert.equal(kind, '2d');
            return options.no2d ? null : { putImageData: (data, x, y) => { this.imageData = data; assert.equal(x, 0); assert.equal(y, 0); } };
        }
    }
    class FakeRenderer
    {
        constructor(canvas) { this.canvas = canvas; this.loads = []; this.loadLimits = []; this.renderCount = 0; this.clearCount = 0; this.disposeCount = 0; this.resizeCount = 0; renderers.push(this); }
        resize() { this.resizeCount++; }
        load(value, limits)
        {
            this.loads.push(value);
            this.loadLimits.push(limits);
            if (options.loadFailure) { throw new Error('Synthetic GPU preparation failure'); }
            this.current = value;
        }
        stats() { return { geometries: this.current ? 1 : 0, convertedBytes: this.current ? options.convertedBytes : 0 }; }
        resetCamera() { this.resetCount = (this.resetCount ?? 0) + 1; }
        render() { this.renderCount++; }
        clear() { this.clearCount++; this.current = null; }
        dispose() { this.disposeCount++; this.current = null; }
    }
    const document = new Element();
    document.getElementById = id =>
    {
        if (!elements.has(id)) { elements.set(id, new Element()); }
        return elements.get(id);
    };
    document.createElement = tag => new Element(tag);
    const window = new Element();
    const context = vm.createContext({
        document, window, console, Map, Uint8Array, Uint8ClampedArray, crypto: webcrypto,
        PROFILE_PATHS: paths, PROFILE_ID: 'test-profile', M3Renderer: FakeRenderer,
        buildM3Profile: (buffers, limits) => { builds.push({ buffers, limits }); return options.candidate; },
        performance: { now: () => ++clock }, isSecureContext: true,
        ImageData: class { constructor(data, width, height) { this.data = data; this.width = width; this.height = height; } },
        requestAnimationFrame: callback => { raf = callback; return 1; }
    });
    document.getElementById('visual').value = visualId;
    vm.runInContext(executable, context, { filename: 'm3-app.mjs' });
    const el = id => document.getElementById(id);
    const readReport = () => JSON.parse(el('report').textContent);
    const files = () => paths.map(path => ({ name: path.split('/').at(-1), size: 3, arrayBuffer: async () => new Uint8Array([1, 2, 3]).buffer }));
    async function importFiles(selected = files()) { el('files').files = selected; await el('files').emit('change'); }
    // pageshow намеренно не возвращает Promise action: ждём его microtask-finally.
    async function settle() { for (let i = 0; i < 8; i++) { await Promise.resolve(); } }
    return { el, window, renderers, builds, options, readReport, importFiles, files, settle, frame: () => raf() };
}

test('Импорт вызывает фактическую последовательность file/hash/build/icons/render и публикует manifest', async () =>
{
    const h = harness();
    await h.importFiles();
    assert.equal(h.builds.length, 1);
    assert.deepEqual([...h.builds[0].buffers.keys()], paths);
    assert.equal(h.builds[0].limits.maxDecodedBytes, 512 * 1024 * 1024);
    assert.equal(h.renderers.length, 1);
    assert.equal(h.renderers[0].current.name, 'first');
    assert.equal(h.el('icons').children.length, 1);
    assert.equal(h.el('icons').children[0].children.length, 3);
    assert.equal(h.el('icons').children[0].children[0].imageData.data[3], 100);
    assert.strictEqual(h.el('icons').children[0].children[0].imageData.data.buffer, h.options.candidate.icons.values().next().value.rgba.buffer);
    assert.strictEqual(h.el('icons').children[0].children[1].imageData.data.buffer, h.el('icons').children[0].children[0].imageData.data.buffer);
    const report = h.readReport();
    assert.equal(report.manifest.length, paths.length);
    assert.match(report.manifest[0].sha256, /^[0-9a-f]{64}$/);
    assert.equal(report.manifest[0].bytes, 3);
    assert.equal(h.el('reload').disabled, false);
});

test('Бюджеты учитывают старые CPU/converted данные при замене и не считают профиль дважды при reload', async () =>
{
    const h = harness();
    const maximum = 512 * 1024 * 1024;
    h.options.candidate.decodedBytes = 100;
    await h.importFiles();
    const renderer = h.renderers[0];
    assert.equal(h.builds[0].limits.maxDecodedBytes, maximum);
    assert.equal(renderer.loadLimits[0].maxConvertedBytes, maximum - 100);
    h.options.convertedBytes = 40;
    h.options.candidate = profile('replacement');
    h.options.candidate.decodedBytes = 200;
    await h.importFiles();
    assert.equal(h.builds[1].limits.maxDecodedBytes, maximum - 100 - 40);
    assert.equal(renderer.loadLimits[1].maxConvertedBytes, maximum - 100 - 200);
    await h.el('reload').emit('click');
    assert.equal(renderer.loadLimits[2].maxConvertedBytes, maximum - 200);
    assert.equal(h.builds.length, 2);
});

test('Ошибка подготовки иконок не вызывает renderer.load и сохраняет прежний профиль/UI/report', async () =>
{
    const h = harness(); await h.importFiles();
    const previousIcons = h.el('icons').children;
    const previousStats = h.el('stats').textContent;
    const previousReport = h.readReport();
    h.options.candidate = profile('replacement'); h.options.no2d = true;
    await h.importFiles();
    assert.equal(h.renderers[0].loads.length, 1);
    assert.equal(h.renderers[0].current.name, 'first');
    assert.equal(h.el('icons').children, previousIcons);
    assert.equal(h.el('stats').textContent, previousStats);
    const failed = h.readReport(); assert.ok(failed.lastError);
    delete failed.lastError; assert.deepEqual(failed, previousReport);
    h.options.no2d = false;
    await h.el('reload').emit('click');
    assert.equal(h.renderers[0].current.name, 'first');
    assert.equal(h.el('files').disabled, false);
    assert.equal(h.builds[1].limits.maxDecodedBytes, 512 * 1024 * 1024 - 16);
});

test('Ошибка renderer.load не публикует новый профиль или иконки', async () =>
{
    const h = harness(); await h.importFiles();
    const previousIcons = h.el('icons').children;
    const previousStats = h.el('stats').textContent;
    h.options.candidate = profile('replacement'); h.options.loadFailure = true;
    await h.importFiles();
    assert.match(h.readReport().lastError, /GPU preparation/);
    assert.equal(h.renderers[0].current.name, 'first');
    assert.equal(h.el('icons').children, previousIcons);
    assert.equal(h.el('stats').textContent, previousStats);
    h.options.loadFailure = false;
    await h.el('reload').emit('click');
    assert.equal(h.renderers[0].current.name, 'first');
});

test('File input: missing/duplicate/cancel дают видимую диагностику и не теряют проверенный набор', async () =>
{
    const h = harness(); await h.importFiles();
    await h.importFiles(h.files().slice(0, 1));
    assert.match(h.readReport().lastError, /Отсутствует/);
    assert.match(h.el('status').textContent, /Отсутствует/);
    assert.equal(h.renderers[0].loads.length, 1);
    await h.importFiles([h.files()[0], h.files()[0]]);
    assert.match(h.el('status').textContent, /Неоднозначное имя/);
    await h.importFiles([]);
    assert.match(h.el('status').textContent, /Выбор отменён/);
    assert.equal(h.renderers[0].loads.length, 1);
    assert.equal(h.el('reload').disabled, false);
});

test('Clear освобождает GPU; reload использует прежние CPU-данные без повторного чтения', async () =>
{
    const h = harness(); await h.importFiles();
    const renderer = h.renderers[0];
    await h.el('clear').emit('click');
    assert.equal(renderer.clearCount, 1); assert.equal(renderer.current, null);
    const cleared = JSON.parse(h.el('stats').textContent);
    assert.equal(cleared.state, 'Сцена освобождена');
    assert.equal(cleared.gpu.geometries, 0);
    await h.el('reload').emit('click');
    assert.equal(renderer.current.name, 'first');
    assert.equal(h.builds.length, 1);
    await h.el('reset').emit('click'); assert.equal(renderer.resetCount, 1);
    await h.window.emit('resize'); assert.ok(renderer.resizeCount >= 3);
});

test('Pagehide освобождает renderer; persisted pageshow пересоздаёт сцену из памяти', async () =>
{
    const h = harness(); await h.importFiles();
    const original = h.renderers[0];
    await h.window.emit('pagehide'); assert.equal(original.disposeCount, 1);
    await h.window.emit('pageshow', { persisted: false }); await h.settle();
    assert.equal(h.renderers.length, 1);
    await h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.renderers.length, 2);
    assert.equal(h.renderers[1].current.name, 'first');
    assert.equal(h.builds.length, 1);
});

test('Отложенный import после pagehide отменяется, не пересоздаёт renderer и не заменяет CPU-профиль', async () =>
{
    const h = harness(); await h.importFiles();
    const previousIcons = h.el('icons').children;
    const original = h.renderers[0];
    let resolveRead;
    const selected = h.files();
    selected[0].arrayBuffer = () => new Promise(resolve => { resolveRead = resolve; });
    h.options.candidate = profile('replacement');
    const pending = h.importFiles(selected);
    assert.equal(typeof resolveRead, 'function');
    await h.window.emit('pagehide');
    assert.equal(original.disposeCount, 1);
    resolveRead(new Uint8Array([4, 5, 6]).buffer);
    await pending;
    assert.match(h.readReport().lastError, /Импорт отменён/);
    assert.equal(h.renderers.length, 1);
    assert.equal(h.builds.length, 1);
    assert.equal(h.el('icons').children, previousIcons);
    await h.window.emit('pageshow', { persisted: true }); await h.settle();
    assert.equal(h.renderers.length, 2);
    assert.equal(h.renderers[1].current.name, 'first');
});

test('Отложенный picker или обход папки не возобновляет старый импорт после pagehide/pageshow', async () =>
{
    for (const phase of ['picker', 'directory', 'file'])
    {
        const h = harness(); await h.importFiles();
        h.options.candidate = profile('replacement');
        let release;
        let directories = 0;
        const blocked = new Promise(resolve => { release = resolve; });
        const root = {
            getDirectoryHandle: async () =>
            {
                directories++;
                if (phase === 'directory' && directories === 1) { await blocked; }
                return root;
            },
            getFileHandle: async name => ({ getFile: async () =>
            {
                if (phase === 'file') { await blocked; }
                return h.files().find(file => file.name === name);
            } })
        };
        h.window.showDirectoryPicker = async options =>
        {
            assert.deepEqual(JSON.parse(JSON.stringify(options)), { mode: 'read' });
            if (phase === 'picker') { await blocked; }
            return root;
        };
        const pending = h.el('folder').emit('click');
        await h.settle();
        await h.window.emit('pagehide');
        await h.window.emit('pageshow', { persisted: true });
        // Страница снова active; именно старое generation должно запретить продолжение.
        assert.equal(h.renderers.length, 2, phase);
        release(); await pending;
        assert.match(h.readReport().lastError, /Выбор папки отменён/, phase);
        assert.equal(h.builds.length, 1, phase);
        assert.equal(h.renderers.length, 2, phase);
        assert.equal(h.renderers[1].current.name, 'first', phase);
        if (phase === 'picker') { assert.equal(directories, 0); }
        if (phase === 'directory') { assert.equal(directories, 1); }
    }
});

test('Context loss блокирует рендер/пересоздание, restored ждёт ручного reload', async () =>
{
    const h = harness(); await h.importFiles();
    const original = h.renderers[0]; let prevented = false;
    h.frame(); const renders = original.renderCount;
    await h.el('scene').emit('webglcontextlost', { preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    h.frame(); assert.equal(original.renderCount, renders);
    await h.el('reload').emit('click');
    assert.match(h.readReport().lastError, /Контекст WebGL потерян/);
    assert.equal(original.loads.length, 1);
    await h.el('scene').emit('webglcontextrestored');
    assert.equal(original.disposeCount, 1); assert.equal(h.renderers.length, 1);
    await h.el('reload').emit('click');
    assert.equal(h.renderers.length, 2); assert.equal(h.renderers[1].current.name, 'first');
});

test('Unknown visual отклоняется явно и не вызывает замену прежней сцены', async () =>
{
    const h = harness(); await h.importFiles();
    h.el('visual').value = 'unknown';
    await h.el('visual').emit('change');
    assert.match(h.readReport().lastError, /модель отсутствует/);
    assert.equal(h.renderers[0].loads.length, 1);
    assert.equal(h.renderers[0].current.name, 'first');
});
