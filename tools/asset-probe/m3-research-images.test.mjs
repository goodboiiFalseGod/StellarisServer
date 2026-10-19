import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readdir, readFile, stat } from 'node:fs/promises';
import { join } from 'node:path';
import { createResearchImageStore, researchIconRecord, inspectResearchDds, decodeResearchDds, MAX_RESEARCH_IMAGE_BYTES } from './m3-research-images.mjs';

const ICON_ROOT = 'gfx/interface/icons/technologies/';
const hash = bytes => createHash('sha256').update(Buffer.from(bytes)).digest('hex');
function rgbaDds(width = 2, height = 2)
{
    const pitch = width * 4; const data = Buffer.alloc(128 + pitch * height, 0);
    data.write('DDS ', 0); data.writeUInt32LE(124, 4); data.writeUInt32LE(0x100f, 8); data.writeUInt32LE(height, 12); data.writeUInt32LE(width, 16); data.writeUInt32LE(pitch, 20);
    data.writeUInt32LE(32, 76); data.writeUInt32LE(0x41, 80); data.writeUInt32LE(32, 88);
    [0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000].forEach((mask, index) => data.writeUInt32LE(mask, 92 + index * 4));
    for (let i = 128; i < data.length; i += 4) { data.set([12, 34, 56, 255], i); }
    return data;
}
function bgr24Dds(width = 2, height = 1, { flags = 0x81007 } = {})
{
    const pitch = width * 3; const payloadBytes = pitch * height; const declaredSize = flags === 0x100f ? pitch : payloadBytes; const data = Buffer.alloc(128 + payloadBytes, 0);
    data.write('DDS ', 0); data.writeUInt32LE(124, 4); data.writeUInt32LE(flags, 8); data.writeUInt32LE(height, 12); data.writeUInt32LE(width, 16); data.writeUInt32LE(declaredSize, 20);
    data.writeUInt32LE(0, 24); data.writeUInt32LE(0, 28); data.writeUInt32LE(32, 76); data.writeUInt32LE(0x40, 80); data.writeUInt32LE(0, 84); data.writeUInt32LE(24, 88);
    [0x00ff0000, 0x0000ff00, 0x000000ff, 0].forEach((mask, index) => data.writeUInt32LE(mask, 92 + index * 4)); data.writeUInt32LE(0x1000, 108);
    data.set([30, 20, 10, 3, 2, 1], 128); return data;
}
function directoryHandle(files, { onRead = () => {} } = {})
{
    const node = path => ({
        async getDirectoryHandle(name) { return node(`${path ? `${path}/` : ''}${name}`); },
        async getFileHandle(name)
        {
            const relativePath = `${path ? `${path}/` : ''}${name}`;
            const bytes = files.get(relativePath); if (!bytes) { const failure = new Error(); failure.name = 'NotFoundError'; throw failure; }
            return { async getFile() { return { size: bytes.byteLength, async arrayBuffer() { onRead(); return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength); } }; } };
        }
    });
    return node('');
}
class Canvas
{
    constructor() { this.width = 0; this.height = 0; this.context = { putImageData: image => { this.pixels = image.data; } }; }
    getContext() { return this.context; }
    toBlob(callback) { callback(new Blob([this.pixels])); }
}
function setup(source, overrides = {})
{
    const urls = { created: [], revoked: [], createObjectURL(blob) { const url = `blob:research-${this.created.length}`; this.created.push({ url, blob }); return url; }, revokeObjectURL(url) { this.revoked.push(url); } };
    const document = { createElement: () => new Canvas() };
    const store = createResearchImageStore({ source, document, urls, pixels: data => ({ data }), hash, ...overrides });
    return { store, urls };
}
function technology(bytes, overrides = {})
{
    return { id: 'tech_test', icon: { relativePath: `${ICON_ROOT}tech_test.dds`, sha256: hash(bytes), byteLength: bytes.byteLength }, ...overrides };
}

test('research icon metadata accepts canonical paths and rejects traversal or forged metadata', () =>
{
    const bytes = rgbaDds(); const value = technology(bytes);
    assert.deepEqual(researchIconRecord(value), { relativePath: `${ICON_ROOT}tech_test.dds`, sha256: hash(bytes), byteLength: bytes.byteLength });
    for (const relativePath of ['../secret.dds', 'gfx/interface/icons/resources/energy.dds', `${ICON_ROOT}../x.dds`, `${ICON_ROOT}nested/icon.dds`, `${ICON_ROOT}.hidden.dds`])
    {
        assert.throws(() => researchIconRecord(technology(bytes, { icon: { ...value.icon, relativePath } })), /Путь иконки/);
    }
    assert.throws(() => researchIconRecord(technology(bytes, { icon: { ...value.icon, sha256: 'bad' } })), /метаданные/);
    assert.throws(() => researchIconRecord({ id: 'tech_missing', iconDiagnostic: 'icon-missing-or-invalid' }), /icon-missing-or-invalid/);
});

test('BGR24 reader validates the narrow 24-bit header and produces opaque RGBA pixels', () =>
{
    const bytes = bgr24Dds(); const info = inspectResearchDds(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
    assert.deepEqual({ width: info.width, height: info.height, codec: info.codec, rowPitch: info.rowPitch, decodedBytes: info.decodedBytes }, { width: 2, height: 1, codec: 'BGR24', rowPitch: 6, decodedBytes: 8 });
    const image = decodeResearchDds(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
    assert.deepEqual([...image.rgba], [10, 20, 30, 255, 1, 2, 3, 255]);
    const pitchBytes = bgr24Dds(2, 2, { flags: 0x100f });
    assert.equal(inspectResearchDds(pitchBytes.buffer.slice(pitchBytes.byteOffset, pitchBytes.byteOffset + pitchBytes.byteLength)).rowPitch, 6);
    assert.deepEqual([...decodeResearchDds(pitchBytes.buffer.slice(pitchBytes.byteOffset, pitchBytes.byteOffset + pitchBytes.byteLength)).rgba], [10, 20, 30, 255, 1, 2, 3, 255, 0, 0, 0, 255, 0, 0, 0, 255]);
    for (const mutate of [
        value => value.writeUInt32LE(0x1007, 8),
        value => value.writeUInt32LE(5, 20),
        value => value.writeUInt32LE(0x000000ff, 92),
        value => value.writeUInt32LE(2, 28),
        value => value.writeUInt32LE(0x2000, 112)
    ])
    {
        const malformed = Buffer.from(bytes); mutate(malformed);
        assert.throws(() => inspectResearchDds(malformed.buffer.slice(malformed.byteOffset, malformed.byteOffset + malformed.byteLength)), /DDS BGR24/);
    }
    for (const malformed of [bytes.subarray(0, bytes.length - 1), Buffer.concat([bytes, Buffer.from([0])])])
    {
        assert.throws(() => inspectResearchDds(malformed.buffer.slice(malformed.byteOffset, malformed.byteOffset + malformed.byteLength)), /payload\/pitch/);
    }
    const oversized = bgr24Dds(513, 1);
    assert.throws(() => decodeResearchDds(oversized.buffer.slice(oversized.byteOffset, oversized.byteOffset + oversized.byteLength)), /недопустимые размеры/);
});

test('directory-backed loader validates length and SHA-256, deduplicates requests, and revokes URLs on dispose', async () =>
{
    const bytes = rgbaDds(); const record = technology(bytes); const files = new Map([[record.icon.relativePath, bytes]]); let reads = 0;
    const { store, urls } = setup({ mode: 'directory', handle: directoryHandle(files, { onRead: () => reads++ }) });
    const [first, second] = await Promise.all([store.get(record), store.get(record)]);
    assert.equal(first.url, second.url); assert.equal(reads, 1); assert.equal(urls.created.length, 1); assert.equal(store.decodedBytes, 16);
    store.dispose(); store.dispose(); assert.deepEqual(urls.revoked, [first.url]); assert.equal(store.decodedBytes, 0);
    await assert.rejects(store.get(record), /освобождено/);
});

test('loader rejects a file snapshot source and hash/length mismatches without creating URLs', async () =>
{
    const bytes = rgbaDds(); const record = technology(bytes); const source = { mode: 'files', files: [] };
    const { store, urls } = setup(source);
    await assert.rejects(store.get(record), /Выберите папку установки/);
    store.dispose(); assert.equal(urls.created.length, 0);
    for (const override of [{ sha256: '0'.repeat(64) }, { byteLength: record.icon.byteLength + 1 }])
    {
        const changed = { ...record, icon: { ...record.icon, ...override } };
        const loaded = setup({ mode: 'directory', handle: directoryHandle(new Map([[record.icon.relativePath, bytes]])) });
        await assert.rejects(loaded.store.get(changed), /не совпадает/); assert.equal(loaded.urls.created.length, 0); loaded.store.dispose();
    }
});

test('revoked source permission explains how to restore access without reading a file', async () =>
{
    const bytes = rgbaDds(); const record = technology(bytes); let reads = 0;
    const handle = directoryHandle(new Map([[record.icon.relativePath, bytes]]), { onRead: () => reads++ });
    handle.queryPermission = async () => 'denied'; handle.requestPermission = async () => 'denied';
    const { store, urls } = setup({ mode: 'directory', handle });
    await assert.rejects(store.get(record), /Выберите папку установки заново/);
    assert.equal(reads, 0); assert.equal(urls.created.length, 0); store.dispose();
});

test('dispose aborts a pending local read and leaves no canvas URLs or cached bytes', async () =>
{
    const bytes = rgbaDds(); const record = technology(bytes); let release;
    const source = { mode: 'directory', handle: { async getDirectoryHandle() { return this; }, async getFileHandle() { return { async getFile() { return { size: bytes.byteLength, arrayBuffer: () => new Promise(resolve => { release = () => resolve(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)); }) }; } }; } } };
    const { store, urls } = setup(source); const pending = store.get(record); await new Promise(resolve => setImmediate(resolve)); store.dispose(); release();
    await assert.rejects(pending, error => error.name === 'AbortError'); assert.equal(urls.created.length, 0); assert.equal(store.decodedBytes, 0);
});

test('image and working-set budgets cap dimensions and combined decoded memory at 16 MiB', async () =>
{
    const bytes = rgbaDds(2, 2); const record = technology(bytes);
    const oversized = setup({ mode: 'directory', handle: directoryHandle(new Map([[record.icon.relativePath, bytes]])) }, { inspect: () => ({ isCubemap: false, width: 513, height: 1, decodedBytes: 2052 }) });
    await assert.rejects(oversized.store.get(record), /512×512/); oversized.store.dispose();
    const limited = setup({ mode: 'directory', handle: directoryHandle(new Map([[record.icon.relativePath, bytes]])) }, { maxDecodedBytes: 16, inspect: () => ({ isCubemap: false, width: 2, height: 2, decodedBytes: 16 }) });
    await assert.rejects(limited.store.get(record), /лимит 16 MiB/); limited.store.dispose();
    assert.equal(MAX_RESEARCH_IMAGE_BYTES, 16 * 1024 * 1024);
});

test('actual installed technology DDS can be read and decoded when STELLARIS_GAME_ROOT is provided', async t =>
{
    const gameRoot = process.env.STELLARIS_GAME_ROOT;
    if (!gameRoot) { t.skip('STELLARIS_GAME_ROOT is not configured.'); return; }
    const directory = join(gameRoot, 'gfx/interface/icons/technologies');
    const entries = (await readdir(directory)).filter(name => /^[A-Za-z0-9_-][A-Za-z0-9_.-]*\.dds$/i.test(name));
    for (const name of entries)
    {
        const relativePath = `${ICON_ROOT}${name}`; const bytes = await readFile(join(directory, name));
        if (bytes.byteLength > MAX_RESEARCH_IMAGE_BYTES) { continue; }
        const source = { mode: 'directory', handle: directoryHandle(new Map([[relativePath, bytes]])) };
        const value = { id: name.slice(0, -4), icon: { relativePath, sha256: hash(bytes), byteLength: bytes.byteLength } };
        const { store } = setup(source);
        try { const image = await store.get(value); assert.ok(image.width <= 512 && image.height <= 512); assert.ok(image.decodedBytes > 0); return; }
        finally { store.dispose(); }
    }
    t.skip('No supported technology DDS within the 16 MiB input limit was found.');
});

test('known installed BGR24 technology icons pass the full local loader', async t =>
{
    const gameRoot = process.env.STELLARIS_GAME_ROOT;
    if (!gameRoot) { t.skip('STELLARIS_GAME_ROOT is not configured.'); return; }
    for (const name of ['tech_powered_exoskeletons.dds', 'tech_adaptive_bureaucracy.dds', 'tech_federation_code.dds'])
    {
        const relativePath = `${ICON_ROOT}${name}`; const bytes = await readFile(join(gameRoot, relativePath));
        const source = { mode: 'directory', handle: directoryHandle(new Map([[relativePath, bytes]])) };
        const value = { id: name.slice(0, -4), icon: { relativePath, sha256: hash(bytes), byteLength: bytes.byteLength } };
        const { store } = setup(source);
        try { const image = await store.get(value); assert.equal(image.width, 52); assert.equal(image.height, 52); assert.equal(image.decodedBytes, 52 * 52 * 4); }
        finally { store.dispose(); }
    }
});
