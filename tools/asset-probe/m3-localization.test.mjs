import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, stat } from 'node:fs/promises';
import { resolve } from 'node:path';
import { loadLocalLocalization, LOCALIZATION_FILES_452 } from '../../src/Stellaris.Server/wwwroot/m3-localization.mjs';

function fixture() {
    const files = new Map();
    for (const language of ['english', 'russian']) {
        for (const [index, template] of LOCALIZATION_FILES_452.entries()) {
            const path = `localisation/${language}/${template.replace('{language}', language)}`;
            const text = `l_${language}:\n f${index}:0 "${language}-${index}"\n` + (index === 0 ? ' shared:0 "$f1$"\n' + (language === 'english' ? ' fallback:0 "fallback-value"\n' : '') : '');
            const bytes = new TextEncoder().encode(text);
            files.set(path, { name: path.split('/').at(-1), size: bytes.byteLength, arrayBuffer: async () => bytes.buffer });
        }
    }
    return files;
}
function directorySource(files, reads = []) {
    const directory = prefix => ({
        async getDirectoryHandle(part) { return directory(`${prefix}${part}/`); },
        async getFileHandle(name) {
            const path = prefix + name; reads.push(path);
            if (!files.has(path)) { throw new DOMException('Missing', 'NotFoundError'); }
            return { async getFile() { return files.get(path); } };
        }
    });
    return { mode: 'directory', handle: directory('') };
}

test('два источника дают одинаковые оригинальные ключи, fallback и фиксированный список чтений', async () => {
    const files = fixture(), reads = [];
    const fromFiles = await loadLocalLocalization({ mode: 'files', files: [...files.values()] });
    const fromDirectory = await loadLocalLocalization(directorySource(files, reads));
    assert.equal(fromFiles.text('shared'), 'russian-1');
    assert.equal(fromDirectory.text('shared'), fromFiles.text('shared'));
    assert.equal(fromFiles.text('fallback'), 'fallback-value');
    assert.equal(fromFiles.stats.fallbackEntries, 1);
    assert.equal(fromFiles.stats.files, 464);
    assert.deepEqual(reads, [...files.keys()]);
    assert.deepEqual(fromFiles.diagnostics, []);
});

test('дополнительный русификатор не читается, относительные File пути поддержаны', async () => {
    const files = [...fixture()].map(([path, file]) => ({ ...file, webkitRelativePath: `Stellaris/${path}` }));
    files.push({ name: 'rinfix_l_russian.yml', webkitRelativePath: 'Stellaris/localisation/russian/rinfix_l_russian.yml', arrayBuffer() { throw new Error('Не читать'); } });
    const result = await loadLocalLocalization({ mode: 'files', files });
    assert.equal(result.text('shared'), 'russian-1');
});

test('texture-only source, дубликаты, traversal и неподдержанный язык явно отклоняются', async () => {
    await assert.rejects(loadLocalLocalization({ mode: 'files', files: [{ name: 'fiction.dds' }] }), { code: 'missing-source' });
    const files = [...fixture().values()];
    await assert.rejects(loadLocalLocalization({ mode: 'files', files: [...files, files[0]] }), { code: 'duplicate-path' });
    await assert.rejects(loadLocalLocalization({ mode: 'files', files: [{ name: 'a', webkitRelativePath: 'root/../localisation/a.yml' }] }), { code: 'unsafe-path' });
    await assert.rejects(loadLocalLocalization({ mode: 'files', files }, { language: '../bad' }), { code: 'unsupported-language' });
});

test('ошибка UTF-8/размера/доступа и отмена не публикуют частичный словарь', async () => {
    const files = fixture(), first = files.keys().next().value;
    const original = files.get(first);
    files.set(first, { ...original, size: 1, arrayBuffer: async () => Uint8Array.of(255).buffer });
    await assert.rejects(loadLocalLocalization(directorySource(files)), { code: 'invalid-utf8' });
    files.set(first, { ...original, size: 17 * 1048576 });
    await assert.rejects(loadLocalLocalization(directorySource(files)), { code: 'byte-limit' });
    files.set(first, { ...original, size: original.size + 1 });
    await assert.rejects(loadLocalLocalization(directorySource(files)), { code: 'invalid-size' });
    const abort = new AbortController();
    files.set(first, { ...original, arrayBuffer: async () => { abort.abort(); return original.arrayBuffer(); } });
    await assert.rejects(loadLocalLocalization(directorySource(files), { signal: abort.signal }), { name: 'AbortError' });
    await assert.rejects(loadLocalLocalization({ mode: 'directory', handle: { async getDirectoryHandle() { throw new DOMException('Denied', 'NotAllowedError'); } } }), { code: 'source-read-failed' });
});

test('суммарный бюджет байтов отклоняет набор даже при допустимом размере каждого файла', async () => {
    const files = fixture();
    const large = new Uint8Array(16 * 1048576).fill(32);
    large.set(new TextEncoder().encode('l_english:\n'));
    for (const path of [...files.keys()].slice(0, 5)) {
        files.set(path, { size: large.byteLength, arrayBuffer: async () => large.buffer });
    }
    await assert.rejects(loadLocalLocalization(directorySource(files)), { code: 'byte-limit' });
});

test('бюджет уникальных ключей отклоняет файл с чрезмерным числом допустимых записей', async () => {
    const files = fixture(), first = files.keys().next().value;
    const bytes = new TextEncoder().encode('l_english:\n' + Array.from({ length: 250001 }, (_, index) => `fiction_${index}:0 ""\n`).join(''));
    assert.ok(bytes.byteLength < 16 * 1048576);
    files.set(first, { size: bytes.byteLength, arrayBuffer: async () => bytes.buffer });
    await assert.rejects(loadLocalLocalization(directorySource(files)), { code: 'entry-limit' });
});

test('оригинальная 4.5.2: reader/resolver без копирования игровых строк в fixtures', { skip: !process.env.STELLARIS_GAME_ROOT }, async () => {
    const root = process.env.STELLARIS_GAME_ROOT;
    const directory = prefix => ({
        async getDirectoryHandle(part) { return directory(`${prefix}${part}/`); },
        async getFileHandle(name) {
            const path = resolve(root, prefix + name);
            return { async getFile() {
                const size = (await stat(path)).size;
                return { size, async arrayBuffer() { const bytes = await readFile(path); return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength); } };
            } };
        }
    });
    const result = await loadLocalLocalization({ mode: 'directory', handle: directory('') });
    for (const key of ['pc_continental', 'job_miner', 'building_research_lab_1', 'zone_industrial', 'energy']) {
        const resolved = result.resolve(key);
        assert.notEqual(resolved.text, key);
        assert.ok(!resolved.diagnostics.some(item => item.code === 'missing-key' || item.code === 'unknown-reference'), key);
    }
    assert.equal(result.resolve('RINFIX_WIKI').diagnostics[0].code, 'missing-key');
    console.log(JSON.stringify({ localizationStats: result.stats, diagnostics: result.diagnostics.length, codes: [...new Set(result.diagnostics.map(item => item.code))] }));
});
