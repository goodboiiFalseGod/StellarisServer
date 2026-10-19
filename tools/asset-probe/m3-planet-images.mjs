const MAX_PLANET_IMAGE_BYTES = 16 * 1024 * 1024;

function abortError()
{
    return new DOMException('Planet image preparation was cancelled.', 'AbortError');
}

function assertNotAborted(signal)
{
    if (signal?.aborted) { throw abortError(); }
}

function makeCanvas(document, width, height)
{
    const canvas = document.createElement('canvas');
    try
    {
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext('2d');
        if (!context) { throw new Error('A 2D canvas context is unavailable.'); }
        return { canvas, context };
    }
    catch (error)
    {
        releaseCanvas(canvas);
        throw error instanceof Error ? error : new Error(String(error));
    }
}

function releaseCanvas(canvas)
{
    if (canvas)
    {
        canvas.width = 0;
        canvas.height = 0;
    }
}

function validateImage(id, entry)
{
    if (!entry || typeof entry !== 'object' || typeof entry.spriteId !== 'string' || !entry.spriteId || typeof entry.sourcePath !== 'string' || !entry.sourcePath)
    {
        throw new Error(`Planet image ${id} has invalid sprite metadata.`);
    }
    const { image, frames, frame = 1 } = entry;
    if (!image || typeof image !== 'object' || !Number.isSafeInteger(image.width) || image.width <= 0 || !Number.isSafeInteger(image.height) || image.height <= 0)
    {
        throw new Error(`Planet image ${id} has invalid dimensions.`);
    }
    if (!(image.rgba instanceof Uint8Array)) { throw new Error(`Planet image ${id} pixels must be a Uint8Array.`); }
    if (!Number.isSafeInteger(frames) || frames <= 0 || image.width % frames !== 0) { throw new Error(`Planet image ${id} has invalid frame count.`); }
    if (!Number.isSafeInteger(frame) || frame < 1 || frame > frames) { throw new Error(`Planet image ${id} has an invalid selected frame.`); }

    const sourceBytes = image.width * image.height * 4;
    const frameWidth = image.width / frames;
    const frameBytes = frameWidth * image.height * 4;
    if (!Number.isSafeInteger(sourceBytes) || !Number.isSafeInteger(frameBytes) || image.rgba.byteLength !== sourceBytes)
    {
        throw new Error(`Planet image ${id} pixel buffer size does not match its dimensions.`);
    }
    return { id, image, frame, frameWidth, sourceBytes, frameBytes };
}

export async function preparePlanetImages(profile, { document: dom = document, urls = URL, pixels = (rgba, width, height) => new ImageData(rgba, width, height), signal, maxDecodedBytes = MAX_PLANET_IMAGE_BYTES } = {})
{
    if (!profile || !(profile.planetImages instanceof Map) || profile.planetImages.size === 0) { throw new Error('Planet image profile must contain a non-empty Map.'); }
    if (!Number.isSafeInteger(maxDecodedBytes) || maxDecodedBytes < 0 || maxDecodedBytes > MAX_PLANET_IMAGE_BYTES) { throw new Error('Planet image budget must be a safe integer from 0 to 16 MiB.'); }

    const validated = [];
    let retainedBytes = 0;
    for (const [id, entry] of profile.planetImages)
    {
        if (typeof id !== 'string' || !id.includes('.')) { throw new Error('Planet image IDs must use category.definitionId keys.'); }
        const item = validateImage(id, entry);
        retainedBytes += item.frameBytes;
        if (!Number.isSafeInteger(retainedBytes) || item.sourceBytes + item.frameBytes + retainedBytes - item.frameBytes > maxDecodedBytes)
        {
            throw new Error(`Planet image profile exceeds the ${maxDecodedBytes}-byte decoded image budget.`);
        }
        validated.push(item);
    }
    const peakBytes = validated.reduce((peak, item) => Math.max(peak, item.sourceBytes + item.frameBytes + retainedBytes), 0);
    if (peakBytes > maxDecodedBytes) { throw new Error(`Planet image profile exceeds the ${maxDecodedBytes}-byte decoded image budget.`); }

    assertNotAborted(signal);
    const staged = new Map();
    const createdUrls = [];
    const canvases = new Set();
    try
    {
        for (const item of validated)
        {
            assertNotAborted(signal);
            const source = makeCanvas(dom, item.image.width, item.image.height);
            canvases.add(source.canvas);
            const selected = makeCanvas(dom, item.frameWidth, item.image.height);
            canvases.add(selected.canvas);
            const rgba = new Uint8ClampedArray(item.image.rgba.buffer, item.image.rgba.byteOffset, item.image.rgba.byteLength);
            source.context.putImageData(pixels(rgba, item.image.width, item.image.height), 0, 0);
            selected.context.drawImage(source.canvas, (item.frame - 1) * item.frameWidth, 0, item.frameWidth, item.image.height, 0, 0, item.frameWidth, item.image.height);
            const blob = await new Promise((resolve, reject) =>
            {
                try
                {
                    selected.canvas.toBlob(value => value ? resolve(value) : reject(new Error(`Planet image ${item.id} could not be encoded.`)), 'image/png');
                }
                catch (error) { reject(error instanceof Error ? error : new Error(String(error))); }
            });
            assertNotAborted(signal);
            const url = urls.createObjectURL(blob);
            createdUrls.push(url);
            staged.set(item.id, Object.freeze({ url, width: item.frameWidth, height: item.image.height }));
            releaseCanvas(source.canvas);
            releaseCanvas(selected.canvas);
            canvases.delete(source.canvas);
            canvases.delete(selected.canvas);
        }
    }
    catch (error)
    {
        for (const url of createdUrls) { urls.revokeObjectURL(url); }
        for (const canvas of canvases) { releaseCanvas(canvas); }
        throw error instanceof Error ? error : new Error(String(error));
    }

    let disposed = false;
    return Object.freeze({
        decodedBytes: retainedBytes,
        get(id)
        {
            if (disposed) { throw new Error('Planet image handle has been disposed.'); }
            const image = staged.get(id);
            if (!image) { throw new Error(`Unknown planet image: ${String(id)}.`); }
            return image;
        },
        dispose()
        {
            if (disposed) { return; }
            disposed = true;
            for (const url of createdUrls) { urls.revokeObjectURL(url); }
            createdUrls.length = 0;
            staged.clear();
        }
    });
}
