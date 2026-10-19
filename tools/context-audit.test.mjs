import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { audit } from './context-audit.mjs';

const THREAD = '11111111-2222-4333-8444-555555555555';

async function fixture(t)
{
    const root = await mkdtemp(join(tmpdir(), 'context-audit-'));
    t.after(() => rm(root, { recursive: true, force: true }));
    await mkdir(join(root, 'docs'), { recursive: true });
    for (const path of ['AGENTS.md', 'docs/DECISIONS.md', 'docs/OPEN-QUESTIONS.md', 'docs/CONTEXT-HANDOFF.md', 'docs/CONTEXT-WORKFLOW.md'])
    {
        await writeFile(join(root, path), 'x', 'utf8');
    }
    return {
        root,
        sessionsRoot: join(root, 'sessions'),
        state: join(root, 'artifacts', 'context-control', 'state.json'),
    };
}

async function writeRollout(config, text)
{
    const directory = join(config.sessionsRoot, 'nested', 'day');
    await mkdir(directory, { recursive: true });
    await writeFile(join(directory, `rollout-${THREAD}.jsonl`), text, 'utf8');
}

function tokenRecord(input, cumulative, cached, window = 1000)
{
    return JSON.stringify({
        type: 'event_msg',
        payload: {
            type: 'token_count',
            info: {
                total_token_usage: { input_tokens: cumulative },
                last_token_usage: { input_tokens: input, cached_input_tokens: cached },
                model_context_window: window,
            },
        },
    });
}

test('бюджетная граница включительна, --check падает только при превышении', async (t) =>
{
    const config = await fixture(t);
    await writeFile(join(config.root, 'AGENTS.md'), 'a'.repeat(11998));
    await writeFile(join(config.root, 'docs/DECISIONS.md'), 'b');
    await writeFile(join(config.root, 'docs/OPEN-QUESTIONS.md'), 'c');
    await writeFile(join(config.root, 'docs/CONTEXT-HANDOFF.md'), 'h'.repeat(6500));
    await writeFile(join(config.root, 'docs/CONTEXT-WORKFLOW.md'), 'w'.repeat(9000));
    const atLimit = await audit({ ...config, args: ['--check'] });
    assert.equal(atLimit.output.budgets.required.chars, 12000);
    assert.equal(atLimit.output.budgets.handoff.chars, 6500);
    assert.equal(atLimit.output.budgets.workflow.chars, 9000);
    assert.equal(atLimit.exitCode, 0);
    await writeFile(join(config.root, 'docs/OPEN-QUESTIONS.md'), 'cc');
    const overLimit = await audit({ ...config, args: ['--check'] });
    assert.equal(overLimit.output.budgets.required.exceeded, true);
    assert.equal(overLimit.exitCode, 1);
});

test('использует last input, а cumulative оставляет вне расчёта сигнала', async (t) =>
{
    const config = await fixture(t);
    await writeRollout(config, `${tokenRecord(599, 900000, 31)}\n`);
    const { output } = await audit({ ...config, threads: [THREAD] });
    assert.equal(output.sessions[0].inputTokens, 599);
    assert.equal(output.sessions[0].cachedInputTokens, 31);
    assert.equal(output.sessions[0].rotation, false);
    await writeRollout(config, `${tokenRecord(600, 900000, 40)}\n`);
    const next = await audit({ ...config, threads: [THREAD] });
    assert.equal(next.output.sessions[0].rotation, true);
});

test('unknown остаётся null, compaction учитывается, оборванная последняя строка пропускается', async (t) =>
{
    const config = await fixture(t);
    await writeRollout(config, `${JSON.stringify({ type: 'compacted' })}\n${tokenRecord(10, 4000, 2, 0)}\n{"type":"private partial`);
    const { output } = await audit({ ...config, threads: [THREAD] });
    const session = output.sessions[0];
    assert.equal(session.status, 'unknown');
    assert.equal(session.inputTokens, 10);
    assert.equal(session.contextWindow, 0);
    assert.equal(session.rotation, null);
    assert.equal(session.compactions, 1);
    assert.equal(JSON.stringify(output).includes('private partial'), false);
});

test('три compaction дают rotation без метрик; отсутствующий rollout остаётся unknown', async (t) =>
{
    const config = await fixture(t);
    await writeRollout(config, `${JSON.stringify({ type: 'compacted' })}\n`.repeat(3));
    const { output } = await audit({ ...config, threads: [THREAD, 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'] });
    assert.equal(output.sessions[0].inputTokens, null);
    assert.equal(output.sessions[0].rotation, true);
    assert.equal(output.sessions[0].compactions, 3);
    assert.equal(output.sessions[1].status, 'unknown');
    assert.equal(output.sessions[1].contextWindow, null);
});

test('ошибка целевой token_count записи даёт краткую диагностику без содержимого', async (t) =>
{
    const config = await fixture(t);
    await writeRollout(config, '{"type":"token_count","info": BROKEN_PRIVATE_PAYLOAD}\n');
    const { output } = await audit({ ...config, threads: [THREAD] });
    const session = output.sessions[0];
    assert.equal(session.status, 'unknown');
    assert.equal(session.diagnostics.length, 1);
    assert.match(session.diagnostics[0], /Некорректная JSONL-запись/);
    assert.equal(JSON.stringify(output).includes('BROKEN_PRIVATE_PAYLOAD'), false);
    assert.equal(JSON.stringify(output).includes(config.sessionsRoot), false);
});

test('повтор без изменений устойчив, state содержит только хеши и метрики документов', async (t) =>
{
    const config = await fixture(t);
    const first = await audit(config);
    const second = await audit(config);
    assert.equal(first.output.changed, true);
    assert.equal(second.output.changed, false);
    const state = JSON.parse(await readFile(config.state, 'utf8'));
    assert.deepEqual(Object.keys(state).sort(), ['hashes', 'sessions']);
});

test('рост обычных метрик не меняет сигнал изменения до смены порога', async (t) =>
{
    const config = await fixture(t);
    await writeRollout(config, `${tokenRecord(100, 100, 1)}\n`);
    const baseline = await audit({ ...config, threads: [THREAD] });
    await writeRollout(config, `${tokenRecord(200, 900000, 2)}\n`);
    const growth = await audit({ ...config, threads: [THREAD] });
    assert.equal(baseline.output.thresholdsChanged, true);
    assert.equal(growth.output.filesChanged, false);
    assert.equal(growth.output.thresholdsChanged, false);
    assert.equal(growth.output.changed, false);
});

test('результат компактен и не раскрывает сырые сообщения', async (t) =>
{
    const config = await fixture(t);
    await writeRollout(config, `${JSON.stringify({ type: 'user_message', content: 'секретный текст сообщения' })}\n${tokenRecord(1, 100, 0)}\n`);
    const { output } = await audit({ ...config, threads: [THREAD] });
    const json = JSON.stringify(output);
    assert.ok(json.length <= 4000);
    assert.equal(json.includes('секретный текст сообщения'), false);
});
