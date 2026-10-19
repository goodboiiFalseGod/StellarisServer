// Ограниченный reader определений: AST сохраняет повторы, скрипты не исполняются.
const MAX_BYTES = 16 * 1024 * 1024;
const MAX_TOKENS = 250000;
const MAX_DEPTH = 32;
const NUMBER = /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/;

export function parseVisualDefinitions(text, { allowQuotedWhitespace = false } = {})
{
    if (typeof text !== 'string') { throw new TypeError('Определения должны быть строкой.'); }
    if (text.length > MAX_BYTES || new TextEncoder().encode(text).byteLength > MAX_BYTES) { throw new Error('Определения превышают 16 MiB.'); }
    let position = 0;
    let tokenCount = 0;
    const lookahead = [];
    const fail = message => { throw new Error(`${message} (позиция ${position}).`); };
    function readToken()
    {
        while (position < text.length)
        {
            if (/\s/.test(text[position])) { position++; continue; }
            if (text[position] === '#')
            {
                while (position < text.length && text[position] !== '\n' && text[position] !== '\r') { position++; }
                continue;
            }
            break;
        }
        if (position === text.length) { return null; }
        if (++tokenCount > MAX_TOKENS) { fail('Превышен предел 250000 tokens'); }
        const first = text[position++];
        if (first === '{' || first === '}') { return { type: first }; }
        if ('=<>!'.includes(first))
        {
            let value = first;
            if (text[position] === '=') { value += text[position++]; }
            if (value === '!' || value === '==') { fail('Неподдержанный оператор'); }
            return { type: 'operator', value };
        }
        if (first === '"')
        {
            let value = '';
            while (position < text.length)
            {
                const character = text[position++];
                if (character === '"') { return { type: 'scalar', value }; }
                if (character.charCodeAt(0) === 127 || character.charCodeAt(0) < 32 && !(allowQuotedWhitespace && ['\n', '\r', '\t'].includes(character))) { fail('Управляющий символ в строке'); }
                if (character !== '\\') { value += character; continue; }
                if (position === text.length) { fail('Незавершённый escape'); }
                const escaped = text[position++];
                const escapes = { '"': '"', '\\': '\\', n: '\n', r: '\r', t: '\t' };
                if (!Object.hasOwn(escapes, escaped)) { fail('Неизвестный escape'); }
                value += escapes[escaped];
            }
            fail('Незакрытая строка');
        }
        let value = first;
        while (position < text.length && !/[\s{}=<>!#"]/.test(text[position])) { value += text[position++]; }
        if (/[\x00-\x1f\x7f]/.test(value)) { fail('Управляющий символ в токене'); }
        if (NUMBER.test(value))
        {
            const numeric = Number(value);
            if (!Number.isFinite(numeric)) { fail('Неконечное число'); }
            return { type: 'scalar', value: numeric };
        }
        return { type: 'scalar', value };
    }
    function peek(index = 0)
    {
        while (lookahead.length <= index) { lookahead.push(readToken()); }
        return lookahead[index];
    }
    function take() { const token = peek(); lookahead.shift(); return token; }
    function value(depth)
    {
        const token = take();
        if (token?.type === 'scalar') { return token.value; }
        if (token?.type !== '{') { fail('Ожидалось значение или блок'); }
        if (depth >= MAX_DEPTH) { fail('Превышен предел nesting 32'); }
        return entries(depth + 1, true);
    }
    function entries(depth, nested)
    {
        const result = [];
        while (peek() !== null)
        {
            if (peek().type === '}')
            {
                if (!nested) { fail('Лишняя закрывающая скобка'); }
                take();
                return result;
            }
            if (peek().type === 'scalar' && peek(1)?.type === 'operator')
            {
                const key = String(take().value);
                const operator = take().value;
                result.push({ key, operator, value: value(depth) });
            }
            else { result.push({ key: null, operator: null, value: value(depth) }); }
        }
        if (nested) { fail('Незакрытый блок'); }
        return result;
    }
    return entries(0, false);
}

function uniqueField(entries, key, optional)
{
    if (!Array.isArray(entries) || typeof key !== 'string') { throw new TypeError('Ожидались entries[] и строковый ключ.'); }
    const matches = entries.filter(entry => entry.key === key);
    if (matches.length === 0 && optional) { return undefined; }
    if (matches.length !== 1) { throw new Error(`Поле ${key}: ожидалась одна запись, найдено ${matches.length}.`); }
    if (matches[0].operator !== '=') { throw new Error(`Поле ${key}: нужен оператор =.`); }
    return matches[0];
}

export function requireScalar(entries, key)
{
    const entry = uniqueField(entries, key, false);
    if (Array.isArray(entry.value)) { throw new Error(`Поле ${key}: ожидался скаляр.`); }
    return entry.value;
}

export function optionalScalar(entries, key, fallback = undefined)
{
    const entry = uniqueField(entries, key, true);
    if (!entry) { return fallback; }
    if (Array.isArray(entry.value)) { throw new Error(`Поле ${key}: ожидался скаляр.`); }
    return entry.value;
}

export function requireBlock(entries, key)
{
    const entry = uniqueField(entries, key, false);
    if (!Array.isArray(entry.value)) { throw new Error(`Поле ${key}: ожидался блок.`); }
    return entry.value;
}

export function selectNamedDefinition(entries, key, name, nameKey = 'name')
{
    if (!Array.isArray(entries) || typeof key !== 'string' || typeof name !== 'string' || typeof nameKey !== 'string') { throw new TypeError('Нужны entries[] и строковые ключи/имя.'); }
    const matches = [];
    for (const entry of entries.filter(item => item.key === key))
    {
        if (entry.operator !== '=' || !Array.isArray(entry.value)) { throw new Error(`Определение ${key}: ожидался блок с =.`); }
        if (optionalScalar(entry.value, nameKey) === name) { matches.push(entry.value); }
    }
    if (matches.length !== 1) { throw new Error(`Определение ${key}/${name}: ожидалось одно, найдено ${matches.length}.`); }
    return matches[0];
}
