import { decodeDds as decodeDdsBase, inspectDds as inspectDdsBase } from './formats.mjs';
import { sha256 } from './m3-manifest.mjs';

export const MAX_RESEARCH_IMAGE_BYTES = 16 * 1024 * 1024;
export const MAX_RESEARCH_ICON_DIMENSION = 512;
const ICON_ROOT = 'gfx/interface/icons/technologies/';
const ICON_FILE = /^[A-Za-z0-9_-][A-Za-z0-9_.-]*\.dds$/i;
const HASH = /^[0-9a-f]{64}$/i;
const BGR24_LINEAR_FLAGS = 0x81007;
const BGR24_PITCH_FLAGS = 0x100f;
const BGR24_MASKS = [0x00ff0000, 0x0000ff00, 0x000000ff, 0];

function bgr24Candidate(input)
{
    return input instanceof ArrayBuffer && input.byteLength >= 128 && new DataView(input).getUint32(88, true) === 24;
}

function inspectBgr24(input)
{
    if (!(input instanceof ArrayBuffer) || input.byteLength < 128 || input.byteLength > MAX_RESEARCH_IMAGE_BYTES) { throw new Error('DDS BGR24: недопустимая длина файла.'); }
    const view = new DataView(input);
    if (view.getUint32(0, true) !== 0x20534444 || view.getUint32(4, true) !== 124 || view.getUint32(76, true) !== 32) { throw new Error('DDS BGR24: повреждённый заголовок.'); }
    if (view.getUint32(80, true) !== 0x40 || view.getUint32(84, true) !== 0 || BGR24_MASKS.some((mask, index) => view.getUint32(92 + index * 4, true) !== mask)) { throw new Error('DDS BGR24: неподдерживаемые pixel flags или маски каналов.'); }
    const width = view.getUint32(16, true); const height = view.getUint32(12, true); const mipCount = view.getUint32(28, true);
    if (!width || !height || width > MAX_RESEARCH_ICON_DIMENSION || height > MAX_RESEARCH_ICON_DIMENSION || (mipCount !== 0 && mipCount !== 1)) { throw new Error('DDS BGR24: недопустимые размеры или mip count.'); }
    const flags = view.getUint32(8, true); const declaredSize = view.getUint32(20, true);
    const rowPitch = width * 3; const payloadBytes = rowPitch * height; const decodedBytes = width * height * 4;
    const validLinearSize = flags === BGR24_LINEAR_FLAGS && declaredSize === payloadBytes;
    const validPitch = flags === BGR24_PITCH_FLAGS && declaredSize === rowPitch;
    if ((flags !== BGR24_LINEAR_FLAGS && flags !== BGR24_PITCH_FLAGS) || view.getUint32(24, true) !== 0 || view.getUint32(108, true) !== 0x1000 || view.getUint32(112, true) !== 0) { throw new Error('DDS BGR24: неподдерживаемые flags, depth или caps.'); }
    if ((!validLinearSize && !validPitch) || 128 + payloadBytes !== input.byteLength || decodedBytes > MAX_RESEARCH_IMAGE_BYTES) { throw new Error('DDS BGR24: длина payload/pitch не совпадает с заголовком.'); }
    return { width, height, codec: 'BGR24', mipCount: 1, faceCount: 1, decodedBytes, isCubemap: false, rowPitch };
}

function validateResearchLayout(layout)
{
    if (!layout || layout.width <= 0 || layout.height <= 0 || layout.width > MAX_RESEARCH_ICON_DIMENSION || layout.height > MAX_RESEARCH_ICON_DIMENSION || layout.decodedBytes > MAX_RESEARCH_IMAGE_BYTES) { throw new Error(`DDS: допустимый размер иконки не более ${MAX_RESEARCH_ICON_DIMENSION}×${MAX_RESEARCH_ICON_DIMENSION} и 16 MiB.`); }
    return layout;
}

export function inspectResearchDds(input, options = {})
{
    if (bgr24Candidate(input))
    {
        if (options == null || typeof options !== 'object' || Array.isArray(options) || Object.keys(options).length) { throw new Error('DDS BGR24: дополнительные параметры декодирования не поддерживаются.'); }
        return inspectBgr24(input);
    }
    if (!(input instanceof ArrayBuffer) || input.byteLength > MAX_RESEARCH_IMAGE_BYTES) { throw new Error('DDS: размер файла превышает лимит 16 MiB.'); }
    return validateResearchLayout(inspectDdsBase(input, options));
}

export function decodeResearchDds(input, options = {})
{
    const layout = inspectResearchDds(input, options);
    if (layout.isCubemap) { throw new Error('DDS: иконки технологий не могут быть cubemap.'); }
    if (layout.codec === 'BGR24')
    {
        const view = new DataView(input); const rgba = new Uint8Array(layout.decodedBytes);
        for (let y = 0; y < layout.height; y++)
        {
            for (let x = 0; x < layout.width; x++)
            {
                const source = 128 + y * layout.rowPitch + x * 3; const target = (y * layout.width + x) * 4;
                rgba[target] = view.getUint8(source + 2); rgba[target + 1] = view.getUint8(source + 1); rgba[target + 2] = view.getUint8(source); rgba[target + 3] = 255;
            }
        }
        return { width: layout.width, height: layout.height, rgba, codec: 'BGR24', mipCount: 1, decodedMip: 0 };
    }
    return decodeDdsBase(input, options);
}

export function researchIconRecord(technology)
{
    if (!technology || typeof technology !== 'object') { throw new Error('Некорректные данные технологии.'); }
    if (!technology.icon)
    {
        throw new Error(technology.iconDiagnostic ? `Иконка недоступна: ${technology.iconDiagnostic}.` : 'Сервер не предоставил проверяемые данные иконки.');
    }
    const icon = technology.icon;
    const path = icon.relativePath;
    if (typeof path !== 'string' || !path.startsWith(ICON_ROOT) || !ICON_FILE.test(path.slice(ICON_ROOT.length)) || path.slice(ICON_ROOT.length).includes('/'))
    {
        throw new Error('Путь иконки технологии недопустим.');
    }
    if (typeof icon.sha256 !== 'string' || !HASH.test(icon.sha256) || !Number.isSafeInteger(icon.byteLength) || icon.byteLength < 128 || icon.byteLength > MAX_RESEARCH_IMAGE_BYTES)
    {
        throw new Error('Серверные метаданные иконки повреждены.');
    }
    return Object.freeze({ relativePath: path, sha256: icon.sha256.toLowerCase(), byteLength: icon.byteLength });
}

function abortError()
{
    return new DOMException('Загрузка иконок исследований отменена.', 'AbortError');
}

function checkActive(signal, disposed)
{
    if (disposed || signal.aborted) { throw abortError(); }
}

async function readDirectoryFile(source, relativePath)
{
    if (source?.mode === 'files')
    {
        throw new Error('Иконки исследований отсутствуют в режиме отдельных файлов. Выберите папку установки игры для полного доступа к изображениям.');
    }
    if (source?.mode !== 'directory' || typeof source.handle?.getDirectoryHandle !== 'function')
    {
        throw new Error('Источник изображений больше недоступен. Выберите папку установки игры заново.');
    }
    const handle = source.handle;
    try
    {
        if (typeof handle.queryPermission === 'function')
        {
            let permission = await handle.queryPermission({ mode: 'read' });
            if (permission === 'prompt' && typeof handle.requestPermission === 'function') { permission = await handle.requestPermission({ mode: 'read' }); }
            if (permission !== 'granted') { throw new Error('Доступ к папке установки отозван. Выберите папку установки заново, чтобы загрузить иконку.'); }
        }
        let directory = handle;
        const segments = relativePath.split('/');
        for (const segment of segments.slice(0, -1)) { directory = await directory.getDirectoryHandle(segment); }
        const entry = await directory.getFileHandle(segments.at(-1));
        return await entry.getFile();
    }
    catch (failure)
    {
        if (failure instanceof Error && failure.message.startsWith('Доступ к папке')) { throw failure; }
        if (failure?.name === 'NotAllowedError' || failure?.name === 'SecurityError') { throw new Error('Браузер отказал в доступе к локальной папке. Выберите папку установки заново.'); }
        if (failure?.name === 'NotFoundError') { throw new Error('В выбранной папке нет этой иконки технологии. Проверьте версию локальной установки.'); }
        throw new Error('Не удалось прочитать иконку из выбранной папки установки.');
    }
}

export function createResearchImageStore({ source, document: dom = document, urls = URL, pixels = (rgba, width, height) => new ImageData(rgba, width, height), hash = sha256, decode = decodeResearchDds, inspect = inspectResearchDds, maxDecodedBytes = MAX_RESEARCH_IMAGE_BYTES, getAvailableDecodedBytes = () => maxDecodedBytes } = {})
{
    if (!Number.isSafeInteger(maxDecodedBytes) || maxDecodedBytes < 0 || maxDecodedBytes > MAX_RESEARCH_IMAGE_BYTES) { throw new Error('Бюджет иконок исследований должен быть от 0 до 16 MiB.'); }
    const images = new Map();
    const pending = new Map();
    const controllers = new Set();
    let retainedBytes = 0;
    let disposed = false;
    let queue = Promise.resolve();

    function load(record)
    {
        return queue.then(async () =>
        {
            checkActive({ aborted: false }, disposed);
            const controller = new AbortController(); controllers.add(controller);
            let canvas = null;
            try
            {
                const file = await readDirectoryFile(source, record.relativePath);
                checkActive(controller.signal, disposed);
                if (!file || file.size !== record.byteLength || typeof file.arrayBuffer !== 'function') { throw new Error('Размер локальной иконки не совпадает с серверными метаданными.'); }
                const bytes = await file.arrayBuffer();
                checkActive(controller.signal, disposed);
                if (!(bytes instanceof ArrayBuffer) || bytes.byteLength !== record.byteLength) { throw new Error('Длина локальной иконки не совпадает с серверными метаданными.'); }
                if (await hash(bytes) !== record.sha256) { throw new Error('SHA-256 локальной иконки не совпадает с серверными метаданными.'); }
                checkActive(controller.signal, disposed);
                const info = inspect(bytes);
                if (info.isCubemap || info.width <= 0 || info.height <= 0 || info.width > MAX_RESEARCH_ICON_DIMENSION || info.height > MAX_RESEARCH_ICON_DIMENSION || info.decodedBytes > MAX_RESEARCH_IMAGE_BYTES)
                {
                    throw new Error(`Размер иконки превышает допустимые ${MAX_RESEARCH_ICON_DIMENSION}×${MAX_RESEARCH_ICON_DIMENSION} пикселей.`);
                }
                const available = getAvailableDecodedBytes();
                const budget = Math.min(maxDecodedBytes, MAX_RESEARCH_IMAGE_BYTES, available);
                const peakBytes = retainedBytes + bytes.byteLength + info.decodedBytes * 2;
                if (!Number.isSafeInteger(budget) || budget < 0 || peakBytes > budget) { throw new Error('Достигнут лимит 16 MiB для изображений технологий.'); }
                const image = decode(bytes);
                checkActive(controller.signal, disposed);
                if (!(image.rgba instanceof Uint8Array) || image.width !== info.width || image.height !== info.height || image.rgba.byteLength !== info.decodedBytes) { throw new Error('Декодированная иконка не совпадает с проверенным DDS.'); }
                canvas = dom.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
                const context = canvas.getContext('2d');
                if (!context) { throw new Error('Canvas 2D недоступен для отображения иконки.'); }
                const rgba = new Uint8ClampedArray(image.rgba.buffer, image.rgba.byteOffset, image.rgba.byteLength);
                context.putImageData(pixels(rgba, image.width, image.height), 0, 0);
                const blob = await new Promise((resolve, reject) =>
                {
                    try { canvas.toBlob(value => value ? resolve(value) : reject(new Error('Не удалось подготовить иконку для браузера.')), 'image/png'); }
                    catch (failure) { reject(failure instanceof Error ? failure : new Error(String(failure))); }
                });
                checkActive(controller.signal, disposed);
                const url = urls.createObjectURL(blob);
                const handle = Object.freeze({ url, width: image.width, height: image.height, decodedBytes: info.decodedBytes });
                images.set(`${record.relativePath}:${record.sha256}`, handle); retainedBytes += info.decodedBytes;
                return handle;
            }
            finally
            {
                if (canvas) { canvas.width = 0; canvas.height = 0; }
                controllers.delete(controller);
            }
        });
    }

    return Object.freeze({
        get decodedBytes() { return retainedBytes; },
        async get(technology)
        {
            if (disposed) { throw new Error('Хранилище иконок исследований освобождено.'); }
            const record = researchIconRecord(technology);
            const key = `${record.relativePath}:${record.sha256}`;
            if (images.has(key)) { return images.get(key); }
            if (!pending.has(key))
            {
                const promise = load(record);
                pending.set(key, promise);
                queue = promise.catch(() => {});
                promise.finally(() => { if (pending.get(key) === promise) { pending.delete(key); } }).catch(() => {});
            }
            return pending.get(key);
        },
        dispose()
        {
            if (disposed) { return; }
            disposed = true;
            for (const controller of controllers) { controller.abort(); }
            controllers.clear();
            for (const image of images.values()) { urls.revokeObjectURL(image.url); }
            images.clear(); pending.clear(); retainedBytes = 0;
        }
    });
}
