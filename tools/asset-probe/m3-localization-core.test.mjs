import test from 'node:test';
import assert from 'node:assert/strict';
import { parseLocalization, createLocalizationSnapshot } from '../../src/Stellaris.Server/wwwroot/m3-localization-core.mjs';

test('штатный формат допускает внутренние неэкранированные кавычки', () => {
    const parsed = parseLocalization('l_english:\n fiction:0 "A "quoted" word" # "comment"\n', { language: 'english' });
    assert.equal(parsed.entries.get('fiction'), 'A "quoted" word');
    assert.deepEqual(parsed.diagnostics, []);
});

test('parser accepts BOM, header, comments, quoted values and supported escapes', () => {
    const parsed = parseLocalization('\uFEFFl_english:2\n# comment\n greeting:0 "Hello\\nthere\\t\\\"\\\\!" # tail\n', { language: 'english', path: 'localisation/english/test.yml' });
    assert.equal(parsed.entries.get('greeting'), 'Hello\nthere\t"\\!');
    assert.deepEqual(parsed.diagnostics, []);
});

test('parser keeps unknown escapes and returns bounded line diagnostics on bad input', () => {
    const parsed = parseLocalization('l_english:\n a:0 "\\q"\ninvalid\n', { path: 'fiction.yml' });
    assert.equal(parsed.entries.get('a'), '\\q');
    assert.deepEqual(parsed.diagnostics.map(x => [x.code, x.path, x.line]), [['malformed-line', 'fiction.yml', 3]]);
    assert.throws(() => parseLocalization('wrong:header', { path: 'bad.yml' }), error => error.code === 'wrong-header' && error.path === 'bad.yml' && error.line === 1);
});

test('parser accepts optional version and diagnoses duplicate keys', () => {
    const parsed = parseLocalization('l_english:\na: "one"\nb:0"two"\nc:"three"\na:2 "four"', { language: 'english' });
    assert.deepEqual([...parsed.entries], [['a', 'four'], ['b', 'two'], ['c', 'three']]);
    assert.equal(parsed.diagnostics[0].code, 'duplicate-key');
    assert.throws(() => parseLocalization('# no header', { path: 'empty.yml' }), error => error.code === 'wrong-header' && error.path === 'empty.yml');
});

test('snapshot resolves recursive keys, Y format, parameters, safe text and fallbacks', () => {
    const source = new Map([['greeting', 'Hi $person$! $word|Y$ §Ycolor§! £energy£ [Root.GetName]'], ['word', 'ready']]);
    const snapshot = createLocalizationSnapshot(source);
    source.set('word', 'mutated');
    const resolved = snapshot.resolve('greeting', { parameters: { person: '<b>Ada</b>' } });
    assert.equal(resolved.text, 'Hi <b>Ada</b>! ready color [energy] [Root.GetName]');
    assert.equal(snapshot.text('missing'), 'missing');
    assert.equal(snapshot.text('missing', 'fallback'), 'fallback');
    assert.equal(snapshot.raw('greeting'), source.get('greeting'));
    assert.equal(snapshot.raw('missing'), undefined);
    assert.ok(resolved.diagnostics.some(x => x.code === 'scope-expression' && x.message.includes('Root.GetName')));
    assert.equal(snapshot.stats.entries, 2);
});

test('snapshot diagnoses cycles, missing references, inherited parameter injection and scope', () => {
    const params = Object.create({ injected: 'bad' });
    const snapshot = createLocalizationSnapshot(new Map([['a', '$b$ $missing$ $injected$ [Planet.GetName]'], ['b', '$a$']]));
    const result = snapshot.resolve('a', { parameters: params });
    assert.equal(result.text, '$a$ $missing$ $injected$ [Planet.GetName]');
    assert.ok(result.diagnostics.some(x => x.code === 'cycle'));
    assert.ok(result.diagnostics.some(x => x.code === 'unknown-reference' && x.message.includes('$missing$')));
    assert.ok(result.diagnostics.some(x => x.code === 'unknown-reference' && x.message.includes('$injected$')));
    assert.ok(result.diagnostics.some(x => x.code === 'scope-expression'));
});

test('snapshot caps recursive depth and resolved output', () => {
    const values = new Map([['long', 'x'.repeat(70000)], ['k0', '$k1$']]);
    for (let i = 1; i <= 34; i++) { values.set(`k${i}`, `$k${i + 1}$`); }
    values.set('k35', 'done');
    const snapshot = createLocalizationSnapshot(values);
    const long = snapshot.resolve('long');
    assert.equal(long.text.length, 65536);
    assert.ok(long.diagnostics.some(x => x.code === 'max-output'));
    assert.ok(snapshot.resolve('k0').diagnostics.some(x => x.code === 'max-depth'));
});

test('snapshot bounds exponential expansions and preserves unresolved tokens', () => {
    const values = new Map([['k0', '$k1$$k1$']]);
    for (let i = 1; i < 15; i++) { values.set(`k${i}`, `$k${i + 1}$$k${i + 1}$`); }
    values.set('k15', 'x');
    const result = createLocalizationSnapshot(values).resolve('k0');
    assert.ok(result.text.includes('$k'));
    assert.ok(result.diagnostics.some(x => x.code === 'max-expansions'));
});

test('scope diagnostics cover scope chains, bare getters and calls but ignore icon placeholders', () => {
    const snapshot = createLocalizationSnapshot(new Map([['scopes', '[Root.Owner.GetName] [GetSomething] [CalculateValue(1)] [energy] £unity£']]));
    const result = snapshot.resolve('scopes');
    assert.equal(result.text, '[Root.Owner.GetName] [GetSomething] [CalculateValue(1)] [energy] [unity]');
    assert.equal(result.diagnostics.filter(x => x.code === 'scope-expression').length, 3);
});

test('snapshot diagnostics and their records are immutable copies', () => {
    const original = { code: 'source', message: 'source message' };
    const snapshot = createLocalizationSnapshot(new Map(), { diagnostics: [original] });
    original.message = 'changed';
    assert.equal(snapshot.diagnostics[0].message, 'source message');
    assert.ok(Object.isFrozen(snapshot.diagnostics));
    assert.ok(Object.isFrozen(snapshot.diagnostics[0]));
});
