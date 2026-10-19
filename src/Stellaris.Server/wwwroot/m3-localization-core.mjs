const MAX_DIAGNOSTICS = 1000;
const MAX_DEPTH = 32;
const MAX_OUTPUT = 65536;
const MAX_EXPANSIONS = 10000;

function diagnostic(code, message, path, line) {
    return Object.freeze({ code, message, ...(path ? { path } : {}), ...(line ? { line } : {}) });
}

function addDiagnostic(list, item) {
    if (list.length < MAX_DIAGNOSTICS) { list.push(item); }
}

function decodeQuoted(value) {
    let result = '';
    for (let i = 0; i < value.length; i++) {
        const ch = value[i];
        if (ch !== '\\') { result += ch; continue; }
        if (i + 1 >= value.length) { result += '\\'; continue; }
        const next = value[++i];
        if (next === 'n') { result += '\n'; }
        else if (next === 't') { result += '\t'; }
        else if (next === '"' || next === '\\') { result += next; }
        else { result += `\\${next}`; }
    }
    return result;
}

function quotedValue(input) {
    let escaped = false;
    for (let index = 0; index < input.length; ++index) {
        const character = input[index];
        if (!escaped && character === '"' && /^\s*(?:#.*)?$/.test(input.slice(index + 1))) { return input.slice(0, index); }
        if (!escaped && character === '\\') { escaped = true; }
        else { escaped = false; }
    }
    return null;
}

export function parseLocalization(text, { language, path = '' } = {}) {
    const entries = new Map();
    const diagnostics = [];
    if (typeof text !== 'string') {
        return { entries, diagnostics: [diagnostic('invalid-input', 'Ожидался текст локализации.', path)] };
    }
    const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/);
    let headerSeen = false;
    let failed = false;
    for (let i = 0; i < lines.length; i++) {
        const raw = lines[i];
        const trimmed = raw.trim();
        if (!trimmed || trimmed.startsWith('#')) { continue; }
        if (!headerSeen) {
            const header = trimmed.match(/^l_([A-Za-z0-9_]+):(?:\s*(\d+))?\s*(?:#.*)?$/);
            if (!header) {
                addDiagnostic(diagnostics, diagnostic('wrong-header', 'Ожидался заголовок l_language:.', path, i + 1));
                failed = true;
            } else {
                headerSeen = true;
                if (language && header[1].toLowerCase() !== String(language).toLowerCase()) {
                    addDiagnostic(diagnostics, diagnostic('language-mismatch', `Язык заголовка ${header[1]} не совпадает с ${language}.`, path, i + 1));
                    failed = true;
                }
            }
            if (failed) { break; }
            continue;
        }
        // В штатной локализации встречаются внутренние неэкранированные кавычки.
        // Закрывающая кавычка предшествует пробелам/комментарию; кавычки комментария не часть текста.
        const match = raw.match(/^\s*([^\s:#]+)\s*:\s*(?:\d+\s*)?"(.*)$/);
        const quoted = match ? quotedValue(match[2]) : null;
        if (quoted == null) {
            addDiagnostic(diagnostics, diagnostic('malformed-line', 'Ожидалась запись key:version "значение".', path, i + 1));
            continue;
        }
        if (entries.has(match[1])) { addDiagnostic(diagnostics, diagnostic('duplicate-key', `Повторный ключ локализации: ${match[1]}.`, path, i + 1)); }
        entries.set(match[1], decodeQuoted(quoted));
    }
    if (!headerSeen && !failed) {
        addDiagnostic(diagnostics, diagnostic('wrong-header', 'Отсутствует заголовок l_language:.', path, 1));
        failed = true;
    }
    if (failed) {
        const error = new Error(diagnostics[0]?.message ?? 'Некорректный заголовок локализации.');
        error.code = diagnostics[0]?.code ?? 'wrong-header';
        error.path = path;
        error.line = diagnostics[0]?.line ?? 1;
        throw error;
    }
    return { entries, diagnostics };
}

function plainText(value) {
    return value.replace(/§./gs, '').replace(/£([^£\r\n]+)£/g, '[$1]');
}

export function createLocalizationSnapshot(entries, { diagnostics = [], stats = {} } = {}) {
    const values = new Map(entries instanceof Map ? entries : entries);
    const baseDiagnostics = Array.from(diagnostics).slice(0, MAX_DIAGNOSTICS);
    const snapshotDiagnostics = Object.freeze(baseDiagnostics.map(item => Object.freeze({ ...item })));
    const snapshotStats = Object.freeze({ ...stats, entries: values.size });

    function resolve(key, { parameters = {} } = {}) {
        const found = values.get(key);
        const issues = [];
        if (typeof found !== 'string') {
            addDiagnostic(issues, diagnostic('missing-key', `Не найден ключ локализации: ${key}`));
            return { text: String(key), diagnostics: issues };
        }
        const ownParam = (name) => parameters != null && Object.prototype.hasOwnProperty.call(Object(parameters), name) && typeof parameters[name] === 'string';
        let expansionsRemaining = MAX_EXPANSIONS;
        const expand = (input, stack, depth) => {
            if (depth > MAX_DEPTH) {
                addDiagnostic(issues, diagnostic('max-depth', 'Глубина ссылок локализации превышает 32.'));
                return input;
            }
            let out = '';
            const refs = /\$([^$]+)\$/g;
            let last = 0;
            for (let match; (match = refs.exec(input));) {
                out += input.slice(last, match.index);
                if (expansionsRemaining <= 0) {
                    addDiagnostic(issues, diagnostic('max-expansions', 'Превышен лимит раскрытия ссылок локализации.'));
                    out += match[0] + input.slice(refs.lastIndex);
                    return out.slice(0, MAX_OUTPUT);
                }
                expansionsRemaining--;
                const token = match[1];
                const [name, style] = token.split('|', 2);
                let replacement;
                if (ownParam(name)) { replacement = parameters[name]; }
                else if (!values.has(name)) {
                    addDiagnostic(issues, diagnostic('unknown-reference', `Неизвестная ссылка локализации: ${match[0]}`));
                    replacement = match[0];
                } else if (stack.includes(name)) {
                    addDiagnostic(issues, diagnostic('cycle', `Циклическая ссылка локализации: ${match[0]}.`));
                    replacement = match[0];
                } else {
                    replacement = expand(values.get(name), [...stack, name], depth + 1);
                }
                if (style && !['Y', 'y', 'G', 'g', 'R', 'r', 'B', 'b'].includes(style)) {
                    addDiagnostic(issues, diagnostic('unknown-format', `Неизвестный формат локализации: |${style}`));
                }
                out += replacement;
                last = refs.lastIndex;
                if (out.length > MAX_OUTPUT) {
                    addDiagnostic(issues, diagnostic('max-output', 'Результат локализации превышает 65536 символов.'));
                    return out.slice(0, MAX_OUTPUT);
                }
            }
            out += input.slice(last);
            if (out.length > MAX_OUTPUT) {
                addDiagnostic(issues, diagnostic('max-output', 'Результат локализации превышает 65536 символов.'));
                return out.slice(0, MAX_OUTPUT);
            }
            return out;
        };
        const expanded = expand(found, [key], 0);
        for (const scope of expanded.matchAll(/\[([^\]\r\n]+)\]/g)) {
            if (/\.|^Get[A-Za-z0-9_]*$|\(/.test(scope[1])) {
                addDiagnostic(issues, diagnostic('scope-expression', `Неразрешённое выражение в скобках: ${scope[0]}`));
            }
        }
        let result = plainText(expanded);
        if (result.length > MAX_OUTPUT) {
            addDiagnostic(issues, diagnostic('max-output', 'Результат локализации превышает 65536 символов.'));
            result = result.slice(0, MAX_OUTPUT);
        }
        return { text: result, diagnostics: issues.slice(0, MAX_DIAGNOSTICS) };
    }

    return Object.freeze({
        resolve,
        raw(key) { return values.get(key); },
        text(key, fallback) { return values.has(key) ? resolve(key).text : (fallback ?? String(key)); },
        stats: snapshotStats,
        diagnostics: snapshotDiagnostics,
    });
}
