import { LOCALIZATION_FILES_452 } from './m3-localization-paths.mjs';
import { parseLocalization, createLocalizationSnapshot } from './m3-localization-core.mjs';

export { LOCALIZATION_FILES_452 };
const MAX_FILE_BYTES = 16 * 1048576;
const MAX_TOTAL_BYTES = 64 * 1048576;
const MAX_ENTRIES = 250000;
const MAX_DIAGNOSTICS = 1000;
const supportedLanguages = new Set(['russian', 'english']);

function failure(code, message, path) {
    return Object.assign(new Error(message), { code, ...(path ? { path } : {}) });
}
function checkAbort(signal) {
    if (signal?.aborted) { throw new DOMException('Чтение локализации отменено.', 'AbortError'); }
}
function safePath(path) {
    if (typeof path !== 'string' || !path || path.includes('\\') || path.includes(':') || path.includes('\0') || path.split('/').some(part => !part || part === '.' || part === '..')) {
        throw failure('unsafe-path', 'Недопустимый относительный путь локализации.');
    }
    return path;
}

// Читает только фиксированные относительные пути: без fetch, upload и постоянного кеша.
export async function loadLocalLocalization(source, { language = 'russian', signal } = {}) {
    if (!supportedLanguages.has(language)) { throw failure('unsupported-language', 'Поддерживаются русский и английский языки.'); }
    checkAbort(signal);
    const languages = language === 'english' ? ['english'] : ['english', language];
    const requests = languages.flatMap(lang => LOCALIZATION_FILES_452.map(template => ({
        language: lang, path: `localisation/${lang}/${template.replace('{language}', lang)}`
    })));
    const requestedPaths = new Set(requests.map(item => item.path));
    const requestedNames = new Map(requests.map(item => [item.path.split('/').at(-1), item.path]));
    let files = null;
    if (source?.mode === 'files' && Array.isArray(source.files)) {
        files = new Map();
        for (const file of source.files) {
            checkAbort(signal);
            if (!file || typeof file.name !== 'string') { throw failure('invalid-source', 'Неверный File в источнике локализации.'); }
            let path;
            if (file.webkitRelativePath) {
                const relative = safePath(file.webkitRelativePath);
                // webkitdirectory включает имя выбранного корня; иные префиксы не допускаются.
                path = relative.startsWith('localisation/') ? relative : relative.slice(relative.indexOf('/') + 1);
                if (!path.startsWith('localisation/')) { continue; }
            } else {
                safePath(file.name);
                path = requestedNames.get(file.name);
            }
            if (!requestedPaths.has(path)) { continue; }
            if (files.has(path)) { throw failure('duplicate-path', 'Повторный путь локализации.', path); }
            files.set(path, file);
        }
    } else if (source?.mode !== 'directory' || typeof source.handle?.getDirectoryHandle !== 'function') {
        throw failure('invalid-source', 'Нужна выбранная локальная папка или File snapshots.');
    }
    const directories = new Map();
    if (!files) { directories.set('', source.handle); }
    async function resolveFile(path) {
        if (files) {
            const file = files.get(path);
            if (!file) { throw failure('missing-source', 'В выбранных файлах отсутствует штатная локализация; выберите папку Stellaris.', path); }
            return file;
        }
        const parts = safePath(path).split('/');
        let prefix = '', directory = source.handle;
        for (const part of parts.slice(0, -1)) {
            prefix += `${part}/`;
            if (!directories.has(prefix)) {
                directories.set(prefix, await directory.getDirectoryHandle(part));
                checkAbort(signal);
            }
            directory = directories.get(prefix);
        }
        const handle = await directory.getFileHandle(parts.at(-1));
        checkAbort(signal);
        const file = await handle.getFile();
        checkAbort(signal);
        return file;
    }
    const entries = new Map(), diagnostics = [];
    const addDiagnostic = value => { if (diagnostics.length < MAX_DIAGNOSTICS) { diagnostics.push(value); } };
    let totalBytes = 0, count = 0, fallbackEntries = 0;
    for (const request of requests) {
        checkAbort(signal);
        let file;
        try { file = await resolveFile(request.path); }
        catch (error) {
            checkAbort(signal);
            if (error.code) { throw error; }
            throw failure(error.name === 'NotFoundError' ? 'missing-source' : 'source-read-failed', `Не удалось прочитать локализацию: ${request.path}`, request.path);
        }
        if (!Number.isSafeInteger(file.size) || file.size < 0 || file.size > MAX_FILE_BYTES || totalBytes + file.size > MAX_TOTAL_BYTES) {
            throw failure('byte-limit', 'Превышен размер локализации.', request.path);
        }
        let bytes;
        try { bytes = await file.arrayBuffer(); }
        catch (error) { checkAbort(signal); throw failure('source-read-failed', 'Ошибка чтения файла локализации.', request.path); }
        checkAbort(signal);
        if (!(bytes instanceof ArrayBuffer) || bytes.byteLength !== file.size) { throw failure('invalid-size', 'Размер прочитанного файла локализации изменился.', request.path); }
        totalBytes += bytes.byteLength;
        let text;
        try { text = new TextDecoder('utf-8', { fatal: true }).decode(bytes); }
        catch { throw failure('invalid-utf8', 'Некорректный UTF-8 в локализации.', request.path); }
        const parsed = parseLocalization(text, request);
        parsed.diagnostics.forEach(addDiagnostic);
        // Совпадение English и выбранного языка ожидаемо; внутри языка — диагностика.
        for (const [key, value] of parsed.entries) {
            const prior = entries.get(key);
            if (prior?.language === request.language) { addDiagnostic({ code: 'duplicate-key', path: request.path, key, previousPath: prior.path }); }
            entries.set(key, { value, language: request.language, path: request.path });
            if (entries.size > MAX_ENTRIES) { throw failure('entry-limit', 'Превышено число ключей локализации.', request.path); }
        }
        ++count;
        // Отдаём управление браузеру, чтобы UI мог обработать отмену между файлами.
        if (count % 8 === 0) { await new Promise(resolve => setTimeout(resolve, 0)); checkAbort(signal); }
    }
    for (const entry of entries.values()) { if (entry.language !== language) { ++fallbackEntries; } }
    checkAbort(signal);
    return createLocalizationSnapshot(Array.from(entries, ([key, entry]) => [key, entry.value]), {
        diagnostics,
        stats: { language, files: count, bytes: totalBytes, fallbackEntries, profileId: 'stellaris-localization-452-v1' }
    });
}
