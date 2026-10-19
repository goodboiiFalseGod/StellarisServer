// Borrow importer pixels; own only derived browser images, URLs and one stylesheet.
const bindings = [
    ['topbar', 'gfx/interface/main/topbar_background.dds'],
    ['outliner', 'gfx/interface/tiles/outliner_tile.dds'],
    ['header', 'gfx/interface/tiles/green_bg_tile.dds'],
    ['member', 'gfx/interface/buttons/outliner_entry.dds'],
    ['identity', 'gfx/interface/main/starsystem_panel.dds']
];

export function uiSpriteLayout(sprite)
{
    const { image, frames, border } = sprite ?? {};
    if (!image || !Number.isInteger(image.width) || !Number.isInteger(image.height) || image.width <= 0 || image.height <= 0 || !Number.isInteger(frames) || frames <= 0 || image.width % frames || !Number.isInteger(border?.x) || !Number.isInteger(border?.y) || border.x < 0 || border.y < 0 || border.x * 2 > image.width / frames || border.y * 2 > image.height || image.rgba?.byteLength !== image.width * image.height * 4)
    {
        throw new Error('Некорректные размеры UI-спрайта. Повторно импортируйте профиль.');
    }
    return { width: image.width / frames, height: image.height, decodedBytes: image.width / frames * image.height * 4, slice: `${border.y} ${border.x}` };
}

export function uiSpritePlan(profile)
{
    if (!(profile.uiSprites instanceof Map)) { throw new Error('Профиль не содержит UI-спрайты. Обновите страницу и импортируйте набор заново.'); }
    let decodedBytes = 0; let peakDecodedBytes = 0;
    const entries = bindings.map(([name, path]) =>
    {
        const sprite = profile.uiSprites.get(path); const layout = uiSpriteLayout(sprite);
        peakDecodedBytes = Math.max(peakDecodedBytes, decodedBytes + sprite.image.rgba.byteLength + layout.decodedBytes);
        decodedBytes += layout.decodedBytes;
        return { name, sprite, layout };
    });
    return { entries, decodedBytes, peakDecodedBytes };
}

export async function prepareUiSprites(profile, { document: dom = document, urls = URL, pixels = (rgba, width, height) => new ImageData(rgba, width, height), maxDecodedBytes = Infinity } = {})
{
    const plan = uiSpritePlan(profile);
    if (!(maxDecodedBytes >= plan.peakDecodedBytes)) { throw new Error('Недостаточно памяти для UI-спрайтов. Обновите страницу и импортируйте набор заново.'); }
    const ownedUrls = []; const canvases = []; const variables = []; let decodedBytes = 0;
    const style = dom.createElement('style'); let disposed = false;
    const dispose = () =>
    {
        if (disposed) { return; } disposed = true;
        if (style.isConnected) { delete dom.body.dataset.uiSprites; }
        style.remove();
        for (const url of ownedUrls) { urls.revokeObjectURL(url); }
        for (const canvas of canvases) { canvas.width = 0; canvas.height = 0; }
    };
    try
    {
        for (const { name, sprite, layout } of plan.entries)
        {
            const source = dom.createElement('canvas'); source.width = sprite.image.width; source.height = sprite.image.height; canvases.push(source);
            const context = source.getContext('2d'); if (!context) { throw new Error('Canvas2D недоступен для UI-спрайтов.'); }
            context.putImageData(pixels(new Uint8ClampedArray(sprite.image.rgba.buffer, sprite.image.rgba.byteOffset, sprite.image.rgba.byteLength), source.width, source.height), 0, 0);
            const frame = dom.createElement('canvas'); frame.width = layout.width; frame.height = layout.height; canvases.push(frame);
            const frameContext = frame.getContext('2d'); if (!frameContext) { throw new Error('Canvas2D недоступен для кадра UI.'); }
            frameContext.drawImage(source, 0, 0, layout.width, layout.height, 0, 0, layout.width, layout.height);
            const blob = await new Promise(resolve => frame.toBlob(resolve, 'image/png'));
            if (!blob) { throw new Error('Не удалось подготовить UI-спрайт. Прежний интерфейс сохранён.'); }
            const url = urls.createObjectURL(blob); ownedUrls.push(url);
            variables.push(`--ui-${name}:url("${url}");--ui-${name}-slice:${layout.slice};`); decodedBytes += layout.decodedBytes;
            source.width = 0; source.height = 0; frame.width = 0; frame.height = 0;
        }
        style.textContent = `:root{${variables.join('')}}`;
        return { decodedBytes, apply: () => { if (disposed) { throw new Error('UI-спрайты уже освобождены.'); } dom.head.append(style); dom.body.dataset.uiSprites = 'native'; }, dispose };
    }
    catch (failure) { dispose(); throw failure; }
}
