import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { decodeDds } from './formats.mjs';
import { prepareUiSprites, uiSpriteLayout, uiSpritePlan } from '../../src/Stellaris.Server/wwwroot/m3-ui-sprites.mjs';

const paths = ['gfx/interface/main/topbar_background.dds', 'gfx/interface/tiles/outliner_tile.dds', 'gfx/interface/tiles/green_bg_tile.dds', 'gfx/interface/buttons/outliner_entry.dds', 'gfx/interface/main/starsystem_panel.dds'];
function fixture(failAt = -1)
{
    const created = []; const revoked = []; const canvases = []; const crops = []; let blobs = 0;
    const document = {
        body: { dataset: {} }, head: { append(node) { node.isConnected = true; } },
        createElement(tag)
        {
            if (tag === 'style') { return { isConnected: false, remove() { this.isConnected = false; } }; }
            const node = { width: 0, height: 0, getContext: () => ({ putImageData() {}, drawImage(...args) { crops.push(args.slice(1)); } }), toBlob(callback) { callback(blobs++ === failAt ? null : {}); } };
            canvases.push(node); return node;
        }
    };
    const profile = { uiSprites: new Map(paths.map((path, index) => [path, { image: { width: index === 3 ? 6 : 2, height: 2, rgba: new Uint8Array(index === 3 ? 48 : 16) }, frames: index === 3 ? 3 : 1, border: { x: 0, y: 0 } }])) };
    return { profile, document, canvases, crops, created, revoked, options: { document, urls: { createObjectURL() { const value = `blob:fixture-${created.length}`; created.push(value); return value; }, revokeObjectURL(value) { revoked.push(value); } }, pixels: () => ({}) } };
}

test('Sprite layout crops one horizontal frame and rejects malformed boundaries before allocation', () =>
{
    const sprite = fixture().profile.uiSprites.get(paths[3]);
    assert.deepEqual(uiSpriteLayout(sprite), { width: 2, height: 2, decodedBytes: 16, slice: '0 0' });
    assert.throws(() => uiSpriteLayout({ ...sprite, frames: 4 }), /Некорректные/);
    assert.throws(() => uiSpriteLayout({ ...sprite, border: { x: 2, y: 0 } }), /Некорректные/);
});

test('UI staging leaves DOM unchanged until apply and releases every owned URL/canvas idempotently', async () =>
{
    const f = fixture(); const original = f.profile.uiSprites.get(paths[3]).image.rgba;
    const owner = await prepareUiSprites(f.profile, f.options);
    assert.equal(f.document.body.dataset.uiSprites, undefined);
    assert.equal(owner.decodedBytes, 80);
    assert.deepEqual(f.crops[3], [0, 0, 2, 2, 0, 0, 2, 2]);
    assert.ok(f.canvases.every(canvas => canvas.width === 0 && canvas.height === 0));
    owner.apply(); assert.equal(f.document.body.dataset.uiSprites, 'native');
    owner.dispose(); owner.dispose(); assert.deepEqual(f.revoked, f.created);
    assert.equal(f.document.body.dataset.uiSprites, undefined);
    assert.equal(f.profile.uiSprites.get(paths[3]).image.rgba, original);
    assert.throws(owner.apply, /освобождены/);
});

test('Failed staging disposes partial outputs and never removes the active published style', async () =>
{
    const active = fixture(); const owner = await prepareUiSprites(active.profile, active.options); owner.apply();
    const failed = fixture(2); failed.options.document.body = active.document.body;
    await assert.rejects(prepareUiSprites(failed.profile, failed.options), /подготовить/);
    assert.deepEqual(failed.revoked, failed.created);
    assert.ok(failed.canvases.every(canvas => canvas.width === 0 && canvas.height === 0));
    assert.equal(active.document.body.dataset.uiSprites, 'native'); owner.dispose();
});

test('Peak staging budget includes temporary full strip and rejects before canvas allocation', async () =>
{
    const f = fixture(); const plan = uiSpritePlan(f.profile);
    assert.equal(plan.decodedBytes, 80); assert.equal(plan.peakDecodedBytes, 112);
    await assert.rejects(prepareUiSprites(f.profile, { ...f.options, maxDecodedBytes: 111 }), /Недостаточно памяти/);
    assert.equal(f.canvases.length, 0); assert.equal(f.created.length, 0);
    const owner = await prepareUiSprites(f.profile, { ...f.options, maxDecodedBytes: 112 }); owner.dispose();
});

test('Real UI sprite frames retain the exact bounded source dimensions', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const borders = [[8, 8], [80, 30], [8, 8], [0, 0], [0, 0]];
    const sprites = new Map();
    for (const [index, path] of paths.entries())
    {
        const bytes = await readFile(resolve(process.env.STELLARIS_GAME_ROOT, path));
        const image = decodeDds(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
        sprites.set(path, { image, frames: index === 3 ? 3 : 1, border: { x: borders[index][0], y: borders[index][1] } });
    }
    const plan = uiSpritePlan({ uiSprites: sprites });
    assert.deepEqual(plan.entries.map(entry => [entry.layout.width, entry.layout.height]), [[36, 36], [180, 180], [20, 20], [255, 50], [440, 49]]);
    assert.equal(plan.decodedBytes, 273624); assert.equal(plan.peakDecodedBytes, 359864);
});
