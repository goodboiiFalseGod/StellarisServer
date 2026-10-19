import assert from 'node:assert/strict';
import test from 'node:test';
import { preparePlanetImages } from './m3-planet-images.mjs';

function makeEnvironment({ encode = ({ callback }) => callback({ type: 'image/png' }) } = {})
{
    const canvases = [];
    const created = [];
    const revoked = [];
    let nextUrl = 0;
    const document = {
        createElement(name)
        {
            assert.equal(name, 'canvas');
            const canvas = {
                width: 0,
                height: 0,
                draws: [],
                getContext(type)
                {
                    assert.equal(type, '2d');
                    return {
                        putImageData(image, x, y) { canvas.image = image; canvas.putPosition = [x, y]; },
                        drawImage(...args) { canvas.draws.push(args); }
                    };
                },
                toBlob(callback, type) { encode({ canvas, callback, type }); }
            };
            canvases.push(canvas);
            return canvas;
        }
    };
    const urls = {
        createObjectURL(blob) { const url = `blob:planet-${++nextUrl}`; created.push({ url, blob }); return url; },
        revokeObjectURL(url) { revoked.push(url); }
    };
    return { canvases, created, revoked, document, urls };
}

function planetImages(entries)
{
    return { planetImages: new Map(entries) };
}

function prepare(profile, options)
{
    return preparePlanetImages(profile, { pixels: (rgba, width, height) => ({ rgba, width, height }), ...options });
}

function entry({ width = 6, height = 1, frames = 3, frame = 2 } = {})
{
    return { image: { width, height, rgba: new Uint8Array(width * height * 4) }, frames, frame, spriteId: 'planet_sprite', sourcePath: 'gfx/planets/example.dds' };
}

test('encodes the selected horizontal frame and returns an immutable disposable handle', async () =>
{
    const env = makeEnvironment();
    const input = entry();
    let receivedPixels;
    const handle = await prepare(planetImages([['planet.example', input]]), {
        document: env.document,
        urls: env.urls,
        pixels(rgba, width, height) { receivedPixels = [rgba, width, height]; return { rgba, width, height }; }
    });

    const image = handle.get('planet.example');
    assert.equal(receivedPixels[0] instanceof Uint8ClampedArray, true);
    assert.equal(receivedPixels[0].buffer, input.image.rgba.buffer);
    assert.equal(receivedPixels[0].byteOffset, input.image.rgba.byteOffset);
    assert.deepEqual(receivedPixels.slice(1), [6, 1]);
    assert.deepEqual([image.width, image.height], [2, 1]);
    assert.equal(Object.isFrozen(image), true);
    assert.equal(env.canvases[1].draws[0][1], 2);
    assert.equal(env.canvases[1].draws[0][3], 2);
    assert.equal(handle.decodedBytes, 8);
    assert.throws(() => handle.get('planet.unknown'), /Unknown planet image/);

    handle.dispose();
    handle.dispose();
    assert.deepEqual(env.revoked, ['blob:planet-1']);
    assert.throws(() => handle.get('planet.example'), /disposed/);
});

test('rejects malformed dimensions and over-budget source plus retained images before canvas allocation', async () =>
{
    const env = makeEnvironment();
    const malformed = entry({ width: 5, frames: 2 });
    await assert.rejects(prepare(planetImages([['planet.bad', malformed]]), { document: env.document, urls: env.urls }), /frame count/);
    await assert.rejects(prepare(planetImages([['planet.ok', entry()]]), { document: env.document, urls: env.urls, maxDecodedBytes: 31 }), /budget/);
    await assert.rejects(prepare(planetImages([['planet.ok', entry()]]), { document: env.document, urls: env.urls, maxDecodedBytes: 16 * 1024 * 1024 + 1 }), /budget/);
    assert.equal(env.canvases.length, 0);
});

test('releases an allocated canvas when its 2D context is unavailable', async () =>
{
    let canvas;
    const document = {
        createElement()
        {
            canvas = { width: 0, height: 0, getContext() { return null; } };
            return canvas;
        }
    };
    await assert.rejects(prepare(planetImages([['planet.context', entry()]]), { document, urls: makeEnvironment().urls }), /2D canvas context is unavailable/);
    assert.deepEqual([canvas.width, canvas.height], [0, 0]);
});

test('cancellation while encoding rolls back canvases and does not create URLs', async () =>
{
    let pending;
    const env = makeEnvironment({ encode: ({ callback }) => { pending = callback; } });
    const controller = new AbortController();
    const result = prepare(planetImages([['planet.waiting', entry()]]), { document: env.document, urls: env.urls, signal: controller.signal });
    await Promise.resolve();
    controller.abort();
    pending({ type: 'image/png' });

    await assert.rejects(result, error => error.name === 'AbortError');
    assert.equal(env.created.length, 0);
    assert.equal(env.canvases.every(canvas => canvas.width === 0 && canvas.height === 0), true);
});

test('failure in a later image rolls back only staged URLs and leaves an earlier handle usable', async () =>
{
    let encoded = 0;
    const env = makeEnvironment({ encode: ({ canvas, callback }) => callback(++encoded === 3 ? null : { id: canvas.width }) });
    const prior = await prepare(planetImages([['planet.prior', entry()]]), { document: env.document, urls: env.urls });
    const failing = planetImages([
        ['planet.first', entry()],
        ['planet.second', entry({ width: 4, frames: 2 })]
    ]);

    await assert.rejects(prepare(failing, { document: env.document, urls: env.urls }), /could not be encoded/);
    assert.equal(prior.get('planet.prior').url, 'blob:planet-1');
    assert.deepEqual(env.revoked, ['blob:planet-2']);
    assert.equal(env.canvases.slice(2).every(canvas => canvas.width === 0 && canvas.height === 0), true);
    prior.dispose();
});
