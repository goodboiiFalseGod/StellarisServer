import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../src/Stellaris.Server/wwwroot/', import.meta.url);

test('QA: actual game HTML supplies every literal client binding exactly once', async () =>
{
    const html = (await readFile(new URL('m3.html', root), 'utf8')).replace(/<!--[\s\S]*?-->/g, '');
    const source = await readFile(new URL('m3-client.mjs', root), 'utf8');
    // Static binding coverage only: this is not an HTML parser or a browser DOM.
    const ids = [...html.matchAll(/\sid\s*=\s*["']([^"']+)["']/g)].map(match => match[1]);
    assert.equal(new Set(ids).size, ids.length, 'Duplicate game HTML IDs');
    assert.match(source, /const \$ = id => document\.getElementById\(id\)/, 'Revisit binding discovery if the client accessor changes');
    const bindings = [...source.matchAll(/\$\(['"]([^'"]+)['"]\)/g)].map(match => match[1]);
    assert.ok(bindings.length > 0, 'Binding discovery must not silently return an empty set');
    for (const id of new Set(bindings))
    {
        assert.ok(ids.includes(id), `Game HTML is missing client binding: ${id}`);
    }
    assert.match(html, /<canvas\b[^>]*\sid=["']canvas["']/);
    assert.match(html, /<script\b[^>]*\stype=["']module["'][^>]*\ssrc=["']\/?m3-client\.mjs["']/);
});
