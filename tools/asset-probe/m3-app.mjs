import { PROFILE_PATHS, PROFILE_ID, buildM3Profile } from './m3-profile.mjs';
import { M3Renderer } from './m3-renderer.mjs';

const $ = id => document.getElementById(id);
let profile = null;
let renderer = null;
let busy = false;
let contextLost = false;
let pageActive = true;
let importGeneration = 0;
let report = { profileId: PROFILE_ID, expectedFiles: PROFILE_PATHS, state: 'Ожидается импорт' };
function status(message, error = false) { $('status').textContent = message; $('status').classList.toggle('error', error); }
function output() { $('report').textContent = JSON.stringify(report, null, 2); }
function controls()
{
    for (const id of ['visual', 'reset', 'reload', 'clear']) { $(id).disabled = busy || !profile; }
    for (const id of ['folder', 'files']) { $(id).disabled = busy; }
}
function show(candidate = profile)
{
    if (!candidate) { throw new Error('Сначала импортируйте локальный набор.'); }
    if (contextLost) { throw new Error('Контекст WebGL потерян. Дождитесь восстановления и пересоздайте сцену.'); }
    if (!renderer) { renderer = new M3Renderer($('scene')); }
    renderer.resize();
    const sample = candidate.samples.get($('visual').value);
    if (!sample) { throw new Error('Выбранная модель отсутствует в проверенном наборе.'); }
    const cpuBytes = candidate.decodedBytes + (candidate !== profile ? (profile?.decodedBytes ?? 0) : 0);
    renderer.load(sample, { maxConvertedBytes: 512 * 1024 * 1024 - cpuBytes });
    $('stats').textContent = JSON.stringify({ visualId: $('visual').value, layers: sample.meshes.map((mesh, index) => ({ index, name: mesh.name, shader: mesh.material.shader, triangles: mesh.indices.length / 3 })), pose: sample.poseReport, gpu: renderer.stats() }, null, 2);
}
function prepareIcons(candidate)
{
    const rows = [];
    for (const [path, image] of candidate.icons)
    {
        const row = document.createElement('div');
        row.title = path;
        for (const theme of ['light', 'dark'])
        {
            const canvas = document.createElement('canvas');
            canvas.width = image.width;
            canvas.height = image.height;
            canvas.className = `icon-${theme}`;
            canvas.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(image.rgba.buffer, image.rgba.byteOffset, image.rgba.byteLength), image.width, image.height), 0, 0);
            row.append(canvas);
        }
        const label = document.createElement('span');
        label.textContent = path.split('/').at(-1);
        row.append(label);
        rows.push(row);
    }
    return rows;
}
async function action(task)
{
    if (busy) { return; }
    busy = true;
    controls();
    try { await task(); }
    catch (error) { report.lastError = error.message; status(error.message, true); output(); }
    finally { busy = false; controls(); }
}
async function importFiles(files, sourceMode)
{
    const generation = ++importGeneration;
    const checkActive = () =>
    {
        if (!pageActive || generation !== importGeneration) { throw new Error('Импорт отменён при закрытии или смене страницы. Выберите набор повторно.'); }
    };
    checkActive();
    const start = performance.now();
    const buffers = new Map();
    const hashes = [];
    for (const path of PROFILE_PATHS)
    {
        const file = files.get(path);
        if (!file || file.size > 64 * 1024 * 1024) { throw new Error(`Отсутствует или слишком велик файл ${path}.`); }
        const data = await file.arrayBuffer();
        checkActive();
        const hash = new Uint8Array(await crypto.subtle.digest('SHA-256', data));
        checkActive();
        hashes.push({ path, bytes: data.byteLength, sha256: Array.from(hash, value => value.toString(16).padStart(2, '0')).join('') });
        buffers.set(path, data);
    }
    const candidate = buildM3Profile(buffers, { maxDecodedBytes: 512 * 1024 * 1024 - (profile?.decodedBytes ?? 0) - (renderer?.stats().convertedBytes ?? 0) });
    const iconRows = prepareIcons(candidate);
    checkActive();
    show(candidate);
    $('icons').replaceChildren(...iconRows);
    profile = candidate;
    report = { profileId: PROFILE_ID, sourceMode, expectedFiles: PROFILE_PATHS, manifest: hashes, decodedBytes: profile.decodedBytes, readDecodeMilliseconds: performance.now() - start, state: 'Численная проверка пройдена; визуальный gate требует осмотра', limitations: 'Static bind pose, approximate materials, no animation/particles/audio, no persistent cache/upload; not production worker importer' };
    output();
    status('Набор прочитан локально. Проверьте все четыре модели и alpha иконок.');
}
$('files').addEventListener('change', () => action(async () =>
{
    const selected = Array.from($('files').files);
    if (!selected.length) { status('Выбор отменён; прежний набор сохранён.'); return; }
    const byName = new Map();
    for (const file of selected)
    {
        if (byName.has(file.name)) { throw new Error(`Неоднозначное имя ${file.name}.`); }
        byName.set(file.name, file);
    }
    await importFiles(new Map(PROFILE_PATHS.map(path => [path, byName.get(path.split('/').at(-1))])), 'Ограниченный test File input, без upload');
}));
$('folder').addEventListener('click', () => action(async () =>
{
    const generation = importGeneration;
    const checkFolderActive = () =>
    {
        if (!pageActive || generation !== importGeneration) { throw new Error('Выбор папки отменён при закрытии или смене страницы. Выберите папку повторно.'); }
    };
    if (!isSecureContext || typeof window.showDirectoryPicker !== 'function') { throw new Error('Read-only выбор папки недоступен; используйте File input тестового набора.'); }
    let root;
    try { root = await window.showDirectoryPicker({ mode: 'read' }); }
    catch (error) { if (error.name === 'AbortError') { status('Выбор отменён.'); return; } throw error; }
    const files = new Map();
    checkFolderActive();
    for (const path of PROFILE_PATHS)
    {
        const parts = path.split('/');
        let directory = root;
        for (const segment of parts.slice(0, -1)) { directory = await directory.getDirectoryHandle(segment); checkFolderActive(); }
        files.set(path, await (await directory.getFileHandle(parts.at(-1))).getFile());
        checkFolderActive();
    }
    await importFiles(files, 'Read-only DirectoryHandle');
}));
$('visual').addEventListener('change', () => action(async () => { show(); }));
$('reload').addEventListener('click', () => action(async () => { show(); status('Сцена пересоздана из памяти страницы.'); }));
$('reset').addEventListener('click', () => renderer?.resetCamera());
$('clear').addEventListener('click', () => { renderer?.clear(); $('stats').textContent = JSON.stringify({ state: 'Сцена освобождена', gpu: renderer?.stats() }, null, 2); status('GPU-сцена освобождена; набор в памяти доступен для пересоздания.'); });
window.addEventListener('resize', () => renderer?.resize());
window.addEventListener('pagehide', () => { pageActive = false; importGeneration++; renderer?.dispose(); renderer = null; });
window.addEventListener('pageshow', event =>
{
    pageActive = true;
    if (event.persisted && profile)
    {
        try { show(); }
        catch (error) { status(error.message, true); }
    }
});
window.addEventListener('error', event => status(event.message, true));
$('scene').addEventListener('webglcontextlost', event =>
{
    event.preventDefault();
    contextLost = true;
    status('Контекст WebGL потерян. Локальные данные сохранены; после восстановления нажмите «Пересоздать».', true);
});
$('scene').addEventListener('webglcontextrestored', () =>
{
    renderer?.dispose();
    renderer = null;
    contextLost = false;
    status('Контекст восстановлен. Нажмите «Пересоздать».');
});
function animate() { if (!contextLost) { renderer?.render(); } requestAnimationFrame(animate); }
output(); controls(); animate();
