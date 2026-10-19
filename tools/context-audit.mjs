import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, open, readFile, readdir, writeFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';

const SCRIPT_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const BUDGETS = Object.freeze({ required: 12000, handoff: 6500, workflow: 9000 });

function sha256(value)
{
    return createHash('sha256').update(value).digest('hex');
}

function parseArgs(args)
{
    const options = { root: SCRIPT_ROOT, threads: [], check: false };
    for (let i = 0; i < args.length; i += 1)
    {
        const arg = args[i];
        if (arg === '--check')
        {
            options.check = true;
        }
        else if (arg === '--thread')
        {
            const id = args[++i];
            if (!id || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id))
            {
                throw new Error('--thread требует UUID');
            }
            options.threads.push(id);
        }
        else if (['--root', '--sessions-root', '--state'].includes(arg))
        {
            const value = args[++i];
            if (!value)
            {
                throw new Error(`${arg} требует путь`);
            }
            options[{ '--root': 'root', '--sessions-root': 'sessionsRoot', '--state': 'state' }[arg]] = value;
        }
        else
        {
            throw new Error(`Неизвестный аргумент: ${arg}`);
        }
    }
    options.root = resolve(options.root);
    options.sessionsRoot = resolve(options.sessionsRoot ?? join(process.env.CODEX_HOME || join(homedir(), '.codex'), 'sessions'));
    options.state = resolve(options.state ?? join(options.root, 'artifacts', 'context-control', 'state.json'));
    return options;
}

async function measureFile(root, relative)
{
    const path = join(root, relative);
    const content = await readFile(path, 'utf8');
    return { path: relative, chars: content.length, sha256: sha256(content) };
}

async function measureDocuments(root)
{
    const requiredPaths = ['AGENTS.md', 'docs/DECISIONS.md', 'docs/OPEN-QUESTIONS.md'];
    const files = await Promise.all(requiredPaths.map((path) => measureFile(root, path)));
    const handoff = await measureFile(root, 'docs/CONTEXT-HANDOFF.md');
    const workflow = await measureFile(root, 'docs/CONTEXT-WORKFLOW.md');
    const required = files.reduce((sum, file) => sum + file.chars, 0);
    return {
        files: [...files, handoff, workflow],
        budgets: {
            required: { chars: required, limit: BUDGETS.required, exceeded: required > BUDGETS.required },
            handoff: { chars: handoff.chars, limit: BUDGETS.handoff, exceeded: handoff.chars > BUDGETS.handoff },
            workflow: { chars: workflow.chars, limit: BUDGETS.workflow, exceeded: workflow.chars > BUDGETS.workflow },
        },
    };
}

async function findRollouts(sessionsRoot, threadId)
{
    const found = [];
    async function visit(directory)
    {
        let entries;
        try
        {
            entries = await readdir(directory, { withFileTypes: true });
        }
        catch (error)
        {
            if (error.code === 'ENOENT' && directory === sessionsRoot)
            {
                return;
            }
            throw error;
        }
        for (const entry of entries)
        {
            const path = join(directory, entry.name);
            if (entry.isDirectory())
            {
                await visit(path);
            }
            else if (entry.isFile() && /rollout.*\.jsonl$/i.test(entry.name) && entry.name.toLowerCase().includes(threadId.toLowerCase()))
            {
                found.push(path);
            }
        }
    }
    await visit(sessionsRoot);
    return found.sort();
}

function tokenInfo(record)
{
    if (record?.type === 'event_msg' && record.payload?.type === 'token_count')
    {
        return record.payload.info;
    }
    if (record?.type === 'token_count')
    {
        return record.info ?? record;
    }
    return null;
}

function compactedRecord(record)
{
    return record?.type === 'compacted' || record?.payload?.type === 'compacted';
}

function validCount(value)
{
    return Number.isSafeInteger(value) && value >= 0 ? value : null;
}

function skipSpace(text, index)
{
    while (index < text.length && /\s/.test(text[index])) index += 1;
    return index;
}

function valueEnd(text, start)
{
    const first = text[start];
    if (first === '"')
    {
        let escaped = false;
        for (let index = start + 1; index < text.length; index += 1)
        {
            if (escaped)
            {
                escaped = false;
            }
            else if (text[index] === '\\')
            {
                escaped = true;
            }
            else if (text[index] === '"')
            {
                return index + 1;
            }
        }
        return text.length;
    }
    if (first === '{' || first === '[')
    {
        const stack = [first === '{' ? '}' : ']'];
        let inString = false;
        let escaped = false;
        for (let index = start + 1; index < text.length; index += 1)
        {
            const character = text[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character === '\\')
                {
                    escaped = true;
                }
                else if (character === '"')
                {
                    inString = false;
                }
                continue;
            }
            if (character === '"')
            {
                inString = true;
            }
            else if (character === '{')
            {
                stack.push('}');
            }
            else if (character === '[')
            {
                stack.push(']');
            }
            else if (character === stack.at(-1))
            {
                stack.pop();
                if (stack.length === 0)
                {
                    return index + 1;
                }
            }
        }
        return text.length;
    }
    let index = start;
    while (index < text.length && !/[\s,}\]]/.test(text[index])) index += 1;
    return index;
}

function propertyToken(text, objectStart, propertyName)
{
    if (text[objectStart] !== '{')
    {
        return null;
    }
    let index = objectStart + 1;
    while (index < text.length)
    {
        index = skipSpace(text, index);
        if (text[index] === '}')
        {
            return null;
        }
        if (text[index] !== '"')
        {
            return null;
        }
        const keyEnd = valueEnd(text, index);
        let key;
        try
        {
            key = JSON.parse(text.slice(index, keyEnd));
        }
        catch
        {
            return null;
        }
        index = skipSpace(text, keyEnd);
        if (text[index] !== ':')
        {
            return null;
        }
        const start = skipSpace(text, index + 1);
        const end = valueEnd(text, start);
        if (key === propertyName)
        {
            return { start, end };
        }
        index = skipSpace(text, end);
        if (text[index] === ',')
        {
            index += 1;
        }
        else if (text[index] === '}')
        {
            return null;
        }
        else
        {
            return null;
        }
    }
    return null;
}

function stringProperty(text, objectStart, propertyName)
{
    const token = propertyToken(text, objectStart, propertyName);
    if (!token || text[token.start] !== '"')
    {
        return null;
    }
    try
    {
        return JSON.parse(text.slice(token.start, token.end));
    }
    catch
    {
        return null;
    }
}

function structuralRecordKind(line)
{
    const type = stringProperty(line, skipSpace(line, 0), 'type');
    if (type === 'token_count' || type === 'compacted')
    {
        return type;
    }
    if (type !== 'event_msg')
    {
        return null;
    }
    const payload = propertyToken(line, skipSpace(line, 0), 'payload');
    if (!payload || line[payload.start] !== '{')
    {
        return null;
    }
    const payloadType = stringProperty(line, payload.start, 'type');
    return payloadType === 'token_count' || payloadType === 'compacted' ? payloadType : null;
}

async function readRollout(path)
{
    let endedWithNewline = true;
    const handle = await open(path, 'r');
    try
    {
        const stats = await handle.stat();
        if (stats.size > 0)
        {
            const last = Buffer.alloc(1);
            await handle.read(last, 0, 1, stats.size - 1);
            endedWithNewline = last[0] === 10;
        }
    }
    finally
    {
        await handle.close();
    }

    let latest = null;
    let compactions = 0;
    let lineNumber = 0;
    let finalLineNumber = 0;
    const malformedLines = [];
    const diagnostics = [];
    const stream = createReadStream(path, { encoding: 'utf8' });
    const lines = readline.createInterface({ input: stream, crlfDelay: Infinity });
    try
    {
        for await (const line of lines)
        {
            lineNumber += 1;
            if (!line.trim())
            {
                continue;
            }
            finalLineNumber = lineNumber;
            const kind = structuralRecordKind(line);
            if (!kind)
            {
                continue;
            }
            let record;
            try
            {
                record = JSON.parse(line);
            }
            catch
            {
                malformedLines.push(lineNumber);
                continue;
            }
            if (!record || typeof record !== 'object' || Array.isArray(record))
            {
                diagnostics.push(`Некорректная схема JSONL в ${basename(path)} (строка ${lineNumber})`);
                continue;
            }
            if (kind === 'compacted' && compactedRecord(record))
            {
                compactions += 1;
            }
            const info = tokenInfo(record);
            if (info !== null)
            {
                if (!info || typeof info !== 'object')
                {
                    diagnostics.push(`Некорректная token_count схема в ${basename(path)} (строка ${lineNumber})`);
                    continue;
                }
                const last = info.last_token_usage;
                latest = {
                    inputTokens: validCount(last?.input_tokens),
                    cachedInputTokens: validCount(last?.cached_input_tokens),
                    contextWindow: validCount(info.model_context_window),
                };
                if (!last || latest.inputTokens === null || latest.contextWindow === null)
                {
                    diagnostics.push(`Метрики token_count неполны в ${basename(path)} (строка ${lineNumber})`);
                }
            }
        }
    }
    finally
    {
        lines.close();
        stream.destroy();
    }
    for (const malformedLine of malformedLines)
    {
        if (!endedWithNewline && malformedLine === finalLineNumber)
        {
            continue;
        }
        diagnostics.push(`Некорректная JSONL-запись в ${basename(path)} (строка ${malformedLine})`);
    }
    return { latest, compactions, diagnostics };
}

async function measureThread(sessionsRoot, threadId)
{
    const diagnostics = [];
    let paths;
    try
    {
        paths = await findRollouts(sessionsRoot, threadId);
    }
    catch (error)
    {
        return { threadId, status: 'unknown', inputTokens: null, cachedInputTokens: null, contextWindow: null, compactions: null, rotation: null, reasons: ['ошибка чтения каталога сессий'], diagnostics: [`${error.code ?? 'IO_ERROR'}: не удалось прочитать каталог`] };
    }
    if (paths.length === 0)
    {
        return { threadId, status: 'unknown', inputTokens: null, cachedInputTokens: null, contextWindow: null, compactions: null, rotation: null, reasons: ['rollout не найден'], diagnostics };
    }
    let latest = null;
    let compactions = 0;
    for (const path of paths)
    {
        try
        {
            const metrics = await readRollout(path);
            diagnostics.push(...metrics.diagnostics);
            compactions += metrics.compactions;
            if (metrics.latest) latest = metrics.latest;
        }
        catch (error)
        {
            diagnostics.push(`Ошибка чтения rollout ${basename(path)}: ${error.code ?? 'IO_ERROR'}`);
        }
    }
    const ratio = latest?.inputTokens !== null && latest?.contextWindow > 0
        ? latest.inputTokens / latest.contextWindow
        : null;
    const reasons = [];
    if (ratio !== null && ratio >= 0.6)
    {
        reasons.push('input достиг 60% окна');
    }
    if (compactions >= 3)
    {
        reasons.push('compaction не менее 3');
    }
    if (ratio === null)
    {
        reasons.push('input/window unknown');
    }
    return {
        threadId,
        status: latest && ratio !== null ? 'known' : 'unknown',
        inputTokens: latest?.inputTokens ?? null,
        cachedInputTokens: latest?.cachedInputTokens ?? null,
        contextWindow: latest?.contextWindow ?? null,
        compactions,
        rotation: ratio !== null ? ratio >= 0.6 || compactions >= 3 : compactions >= 3 ? true : null,
        reasons,
        diagnostics: diagnostics.slice(0, 6).map((diagnostic) => diagnostic.slice(0, 180)),
    };
}

async function loadState(path)
{
    try
    {
        const value = JSON.parse(await readFile(path, 'utf8'));
        if (!value || typeof value !== 'object' || !value.hashes) throw new Error('неверная схема state.json');
        if (!value.sessions || typeof value.sessions !== 'object') value.sessions = {};
        return value;
    }
    catch (error)
    {
        if (error.code === 'ENOENT')
        {
            return { hashes: {} };
        }
        throw new Error(`Не удалось прочитать state: ${error.message}`);
    }
}

export async function audit(options = {})
{
    const config = parseArgs(options.args ?? []);
    if (options.root)
    {
        config.root = resolve(options.root);
        if (!options.state && !(options.args ?? []).includes('--state')) config.state = join(config.root, 'artifacts', 'context-control', 'state.json');
    }
    if (options.sessionsRoot)
    {
        config.sessionsRoot = resolve(options.sessionsRoot);
    }
    if (options.state)
    {
        config.state = resolve(options.state);
    }
    if (options.threads)
    {
        config.threads = [...options.threads];
    }
    const documentMetrics = await measureDocuments(config.root);
    const priorState = await loadState(config.state);
    const hashes = Object.fromEntries(documentMetrics.files.map(({ path, sha256: hash }) => [path, hash]));
    const filesChanged = JSON.stringify(priorState.hashes) !== JSON.stringify(hashes);
    const sessions = await Promise.all(config.threads.map(async (id, index) =>
    ({
        role: index === 0 ? 'coordinator' : index === 1 ? 'integrator' : `thread-${index + 1}`,
        ...await measureThread(config.sessionsRoot, id),
    })));
    const sessionStates = Object.fromEntries(sessions.map(({ threadId, status, rotation }) => [threadId, { status, rotation }]));
    const thresholdsChanged = JSON.stringify(priorState.sessions) !== JSON.stringify(sessionStates);
    await mkdir(dirname(config.state), { recursive: true });
    await writeFile(config.state, `${JSON.stringify({ hashes, sessions: sessionStates }, null, 2)}\n`, 'utf8');
    const output = {
        changed: filesChanged || thresholdsChanged,
        filesChanged,
        thresholdsChanged,
        budgets: documentMetrics.budgets,
        files: documentMetrics.files.map(({ path, chars, sha256: hash }) => ({ path, chars, sha256: hash })),
        sessions,
    };
    return {
        output,
        exitCode: config.check && Object.values(documentMetrics.budgets).some((budget) => budget.exceeded) ? 1 : 0,
    };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url))
{
    try
    {
        const { output, exitCode } = await audit({ args: process.argv.slice(2) });
        const json = JSON.stringify(output);
        if (json.length > 4000)
        {
            throw new Error(`Результат превышает 4000 символов (${json.length})`);
        }
        process.stdout.write(`${json}\n`);
        process.exitCode = exitCode;
    }
    catch (error)
    {
        process.stderr.write(`${JSON.stringify({ error: error.message })}\n`);
        process.exitCode = 2;
    }
}
