import { parsePdxMesh, decodeDds } from './formats.mjs';
import { ThreeAdapter, BabylonAdapter } from './adapters.mjs';

const base = 'gfx/models/ships/mammalian_01/';
const paths = [base + 'mammalian_01_corvette_S1.mesh', ...['diffuse', 'normal', 'specular'].map(x => base + `mammalian_01_corvette_${x}.dds`), base + '_mammalian_01_ships_meshes.gfx'];
const $ = id => document.getElementById(id);
const buttons = ['reload', 'clear', 'single', 'many', 'measure', 'lifecycle', 'reset-camera'];
let sample = null, adapters = null, count = 1, busy = false;
let report = { sourceMode: null, expectedFiles: paths, userAgent: navigator.userAgent, secureContext: isSecureContext, folderPickerAvailable: typeof window.showDirectoryPicker === 'function', material: 'approximate diffuse + converted RRxG normal + G²*0.4 specular; no gloss/metalness/emissive/team-color/PDX lighting' };
function status(message, error = false) { $('status').textContent = message; $('status').classList.toggle('error', error); }
function output() { $('report').textContent = JSON.stringify(report, null, 2); }
function controls() { for (const id of buttons) $(id).disabled = busy || !sample; $('folder').disabled = busy; $('files').disabled = busy; }
function errorText(error) { return error instanceof Error ? error.message : String(error); }
async function action(task) {
  if (busy) return;
  busy = true; controls();
  try { await task(); } catch (error) { status(errorText(error), true); report.lastError = errorText(error); output(); }
  finally { busy = false; controls(); }
}
function makeAdapters() {
  if (adapters) return;
  const three = new ThreeAdapter($('three'), text => { $('pick-three').textContent = 'Выбор: ' + text; });
  try { adapters = [three, new BabylonAdapter($('babylon'), text => { $('pick-babylon').textContent = 'Выбор: ' + text; })]; }
  catch (error) { three.dispose(); throw error; }
}
function resize() {
  if (!adapters) return;
  for (let i = 0; i < adapters.length; i++) {
    const rect = $(i === 0 ? 'three' : 'babylon').getBoundingClientRect();
    adapters[i].resize(Math.max(1, Math.round(rect.width)), Math.max(1, Math.round(rect.height)));
  }
}
function showSample(candidate = sample, nextCount = count) {
  makeAdapters();
  try { for (const adapter of adapters) adapter.load(candidate, nextCount); }
  catch (error) {
    // Предыдущие проверенные CPU-данные позволяют восстановить обе сцены после частичного отказа адаптера.
    for (const adapter of adapters) {
      try { if (sample) adapter.load(sample, count); else adapter.clear(); }
      catch (restoreError) { adapter.clear(); console.error('Не удалось восстановить сцену:', restoreError); }
    }
    throw error;
  }
  sample = candidate; count = nextCount;
  resize(); report.count = count; report.resources = adapters.map(x => x.stats()); output();
}
function shaderOverride(text) {
  // Ограниченная проверка только известного блока образца, не общий .gfx compiler.
  const clean = text.replace(/#[^\r\n]*/g, '');
  for (const match of clean.matchAll(/pdxmesh\s*=\s*\{/g)) {
    let level = 1, end = match.index + match[0].length;
    for (; end < clean.length && level; end++) { if (clean[end] === '{') level++; if (clean[end] === '}') level--; }
    if (level) throw new Error('GFX: незакрытый блок.');
    const block = clean.slice(match.index, end);
    if (!/name\s*=\s*"mammalian_01_corvette_S1_mesh"/.test(block)) continue;
    if (!block.includes(paths[0]) || !/shader\s*=\s*"PdxMeshShip"/.test(block)) throw new Error('GFX: неожиданный file/shader выбранного образца.');
    if (/texture_\w+\s*=/.test(block)) throw new Error('GFX: texture override пока не поддерживается.');
    return 'PdxMeshShip';
  }
  throw new Error('GFX: не найдено определение mammalian_01_corvette_S1_mesh.');
}
async function readFiles(files, mode) {
  const start = performance.now();
  const buffers = [], manifest = [];
  for (let i = 0; i < files.length; i++) {
    const file = files[i];
    if (!file || file.size > 64 * 1024 * 1024) throw new Error(`Отсутствует или слишком велик файл ${paths[i]}.`);
    const buffer = await file.arrayBuffer(); buffers.push(buffer);
    const hash = await crypto.subtle.digest('SHA-256', buffer);
    manifest.push({ file: paths[i], bytes: buffer.byteLength, sha256: [...new Uint8Array(hash)].map(x => x.toString(16).padStart(2, '0')).join('') });
  }
  const parsed = parsePdxMesh(buffers[0]);
  if (parsed.unsupportedFeatures.length || parsed.meshes.length !== 1) throw new Error('Образец содержит skin/skeleton или несколько meshes: визуальный стенд их не поддерживает.');
  const mesh = parsed.meshes[0];
  if (!mesh.normals || !mesh.uv) throw new Error('У образца отсутствуют normals/UV.');
  const effectiveShader = shaderOverride(new TextDecoder().decode(buffers[4]));
  for (const [field, i] of [['diff', 1], ['n', 2], ['spec', 3]]) {
    if (mesh.material[field] !== paths[i].split('/').at(-1)) throw new Error(`Mesh material ${field} ссылается на другой файл.`);
  }
  const next = { mesh, diffuse: decodeDds(buffers[1]), normal: decodeDds(buffers[2]), specular: decodeDds(buffers[3]) };
  // Невалидные входы не заменяют прежние успешно разобранные данные.
  showSample(next, 1);
  report = { ...report, lastError: null, sourceMode: mode, manifest, embeddedShader: mesh.material.shader, effectiveShader, vertices: mesh.positions.length / 3, triangles: mesh.indices.length / 3, skippedCollision: parsed.skippedCollision, textures: ['diffuse', 'normal', 'specular'].map(x => ({ channel: x, codec: next[x].codec, width: next[x].width, height: next[x].height, mipsInSource: next[x].mipCount, uploaded: 'decoded mip0; GPU generated mipmaps' })), readDecodeMilliseconds: performance.now() - start };
  output(); status('PASS: реальные mesh, GFX и три DDS прочитаны локально и переданы обоим рендерам. Материал приближённый.');
}
$('files').addEventListener('change', () => action(async () => {
  const chosen = [...$('files').files];
  if (!chosen.length) { status('Выбор отменён; предыдущие данные сохранены.'); return; }
  const byName = new Map();
  for (const file of chosen) { if (byName.has(file.name)) throw new Error('Повтор имени файла в образце.'); byName.set(file.name, file); }
  await readFiles(paths.map(path => byName.get(path.split('/').at(-1))), 'actual File input; no upload');
}));
$('folder').addEventListener('click', () => action(async () => {
  if (typeof window.showDirectoryPicker !== 'function' || !isSecureContext) throw new Error('Выбор папки недоступен в этом браузере/контексте. Используйте выбор пяти файлов образца.');
  try {
    const root = await window.showDirectoryPicker({ mode: 'read' });
    const files = [];
    for (const path of paths) {
      const parts = path.split('/'); let dir = root;
      for (const segment of parts.slice(0, -1)) dir = await dir.getDirectoryHandle(segment);
      files.push(await (await dir.getFileHandle(parts.at(-1))).getFile());
    }
    await readFiles(files, 'read-only directory handle');
  } catch (error) {
    if (error.name === 'AbortError') { status('Выбор отменён; предыдущие данные сохранены.'); return; }
    throw error;
  }
}));
$('reload').addEventListener('click', () => action(async () => { showSample(); status('Сцены пересозданы из прежних локальных данных.'); }));
$('clear').addEventListener('click', () => action(async () => { for (const adapter of adapters ?? []) adapter.clear(); report.afterClear = adapters?.map(x => x.stats()); output(); status('Сцены освобождены; локальные данные сохранены для повторной загрузки.'); }));
for (const [id, n] of [['single', 1], ['many', 200]]) $(id).addEventListener('click', () => action(async () => { showSample(sample, n); status(`Показано ${count} экземпляров общей геометрии.`); }));
$('reset-camera').addEventListener('click', () => { for (const adapter of adapters ?? []) adapter.resetCamera(); status('Камеры сброшены.'); });
const frame = () => new Promise(resolve => requestAnimationFrame(resolve));
let measurement = false;
async function measure(adapter) {
  for (let i = 0; i < 30; i++) { await frame(); adapter.render(); }
  const frameTimes = [], cpuTimes = []; let previous = await frame();
  for (let i = 0; i < 120; i++) {
    const now = await frame(); frameTimes.push(now - previous); previous = now;
    const start = performance.now(); adapter.render(); cpuTimes.push(performance.now() - start);
  }
  const stats = values => { const sorted = [...values].sort((a, b) => a - b); return { p50: sorted[Math.floor(sorted.length * 0.5)], p95: sorted[Math.floor(sorted.length * 0.95)], max: sorted.at(-1) }; };
  return { frames: frameTimes.length, frameMilliseconds: stats(frameTimes), renderCpuMilliseconds: stats(cpuTimes), ...adapter.stats() };
}
$('measure').addEventListener('click', () => action(async () => {
  showSample(); measurement = true;
  try {
    status('Измерение Three.js, затем Babylon.js; держите вкладку активной.');
    report.measurement = { count, viewport: [adapters[0].canvas.width, adapters[0].canvas.height], order: ['three', 'babylon'], three: await measure(adapters[0]), babylon: await measure(adapters[1]), warning: 'rAF includes vsync/browser scheduling; built-in lighting differs; not absolute GPU or whole-game benchmark' };
    output(); status('Измерения завершены. Это наблюдение текущего браузера, не бюджет всей игры.');
  } finally { measurement = false; }
}));
$('lifecycle').addEventListener('click', () => action(async () => {
  measurement = true;
  try {
    const cycles = [];
    for (let i = 0; i < 10; i++) { showSample(); await frame(); for (const adapter of adapters) { adapter.render(); adapter.clear(); } cycles.push(adapters.map(x => x.stats())); }
    report.lifecycle = { cycles: 10, afterClear: cycles, warning: 'live geometry/texture counters, not proof of full GPU memory' }; showSample(); output(); status('10 циклов создания/освобождения завершены.');
  } finally { measurement = false; }
}));
function animate() { if (!measurement) for (const adapter of adapters ?? []) adapter.render(); requestAnimationFrame(animate); }
window.addEventListener('resize', resize);
window.addEventListener('pagehide', () => { for (const adapter of adapters ?? []) adapter.dispose(); adapters = null; });
window.addEventListener('error', event => { status(event.message, true); });
$('capabilities').textContent = `Secure context: ${isSecureContext}; folder picker: ${report.folderPickerAvailable}. Стенд работает только с указанным образцом; пять относительных имён перечислены в отчёте.`;
output(); controls(); animate();
