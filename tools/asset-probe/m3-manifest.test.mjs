import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { PROFILE_PATHS } from './m3-profile.mjs';
import { createLocalM3Manifest, validateM3Manifest, canonicalManifestText } from './m3-manifest.mjs';

const buffers = () => new Map(PROFILE_PATHS.map(path => [path, new Uint8Array([1, 2, 3]).buffer]));
test('Manifest: exact 390, canonical ordinal sorting, SHA-256 and shuffled metadata', async () =>
{
    const manifest = await createLocalM3Manifest(buffers());
    assert.equal(manifest.schemaVersion, 1);
    assert.equal(manifest.files.length, 390);
    assert.equal(manifest.importerVersion, 9); assert.equal(manifest.materialProfileVersion, 5);
    assert.equal(manifest.profileId, 'm3-static-452-v12');
    assert.ok(manifest.visuals.every(visual => visual.materialProfile === 'm3-static-galaxy-v4' && visual.dependencies.length === 390));
    assert.equal(manifest.visuals.length, 4);
    assert.equal(createHash('sha256').update(canonicalManifestText(manifest), 'utf8').digest('hex'), manifest.profileDigest);
    manifest.files.reverse(); manifest.visuals.reverse();
    for (const visual of manifest.visuals) { visual.dependencies.reverse(); }
    const checked = await validateM3Manifest(manifest);
    assert.deepEqual(checked.files.map(file => file.relativePath), [...PROFILE_PATHS].sort());
    assert.equal(checked.profileDigest, manifest.profileDigest);
    manifest.files[0].byteLength = 99;
    assert.equal(checked.files.at(-1).byteLength, 3);
    const vector = { profileId: 'профиль', gameVersion: '4.5.2', schemaVersion: 1, importerVersion: 1, materialProfileVersion: 1, files: [{ relativePath: 'я', byteLength: 2, sha256: 'bb' }, { relativePath: 'a', byteLength: 1, sha256: 'aa' }] };
    assert.equal(canonicalManifestText(vector), 'профиль\n4.5.2\n1\n1\n1\na\t1\taa\nя\t2\tbb\n');
});

test('Manifest: версии, paths, дубликаты, extra, kind, hash, length, visuals, digest отклоняются', async () =>
{
    const good = await createLocalM3Manifest(buffers());
    const mutations = [
        value => { value.gameVersion = '4.5.3'; }, value => { value.schemaVersion = 2; },
        value => { value.profileId = 'm3-static-452-v3'; }, value => { value.importerVersion = 2; }, value => { value.materialProfileVersion = 2; },
        value => { value.profileId = 'm3-static-452-v4'; }, value => { value.importerVersion = 3; }, value => { value.materialProfileVersion = 3; },
        value => { value.profileId = 'm3-static-452-v5'; }, value => { value.importerVersion = 4; },
        value => { value.profileId = 'm3-static-452-v6'; }, value => { value.importerVersion = 5; },
        value => { value.profileId = 'm3-static-452-v7'; }, value => { value.importerVersion = 6; },
        value => { value.profileId = 'm3-static-452-v8'; }, value => { value.importerVersion = 7; },
        value => { value.files[0].relativePath = '../outside.dds'; }, value => { value.files[0] = value.files[1]; },
        value => { value.files.push(value.files[0]); }, value => { value.files.pop(); },
        value => { value.files[0].kind = 'shader'; }, value => { value.files[0].sha256 = 'A'.repeat(64); },
        value => { value.files[0].byteLength = -1; }, value => { value.files[0].byteLength = 64 * 1024 * 1024 + 1; },
        value => { value.files[0].byteLength = 1.5; }, value => { value.files[0].byteLength = 1; },
        value => { value.profileDigest = '0'.repeat(64); }, value => { value.extra = true; },
        value => { value.visuals[0].dependencies.pop(); }, value => { value.visuals[0].materialProfile = 'shader'; },
        value => { value.visuals[0] = value.visuals[1]; }, value => { value.visuals[0].visualId = 'unknown'; },
        value => { value.files[0].extra = true; }
    ];
    for (const mutate of mutations) { const changed = structuredClone(good); mutate(changed); await assert.rejects(validateM3Manifest(changed)); }
    await assert.rejects(validateM3Manifest({ huge: 'x'.repeat(1024 * 1024) }), /1 MiB/);
});

test('Manifest: real 4.5.2 — all 390 metadata and canonical digest', { skip: !process.env.STELLARIS_GAME_ROOT }, async () =>
{
    const actual = new Map();
    for (const path of PROFILE_PATHS) { const bytes = await readFile(resolve(process.env.STELLARIS_GAME_ROOT, path)); actual.set(path, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)); }
    const manifest = await createLocalM3Manifest(actual);
    assert.equal(manifest.files.reduce((sum, file) => sum + file.byteLength, 0), 191249125);
    assert.equal(manifest.profileDigest, 'c1a2234ecd9fd1390bd02cb37a3f64547612d28894c17865aa5f5de7ffb4340c');
    for (const [path, byteLength, hash] of [
        ['gfx/map/center.dds', 1398256, '25877c1ae840c8c22d6b6767f5d17efd81b451a66c717ee5b762d4cedb28b90c'],
        ['gfx/map/dust.dds', 1398256, '4d08f9d176682e4e1ed5fa6b6b34e18f06774428c551ec89200aa24cda55d691'],
        ['gfx/map/galaxycolor.dds', 5592560, '51b27b3ec6e7ceae7608cad7de303bbef0ee8dc48bbe06c9d6ce324f3124d34a'],
        ['gfx/map/sky_core.dds', 25165952, '8f0607ef879fc46cb656df9fca3f1c90e9d9c750db4b1a2405867fa0b5eae684'],
        ['gfx/map/sky_mid.dds', 25165952, '514f1d3c467a376d6ce8cc821be69847f7c14d88d2a0debcb05dd64342001df4'],
        ['gfx/map/sky_rim.dds', 25165952, 'cb2298e6054527fd2816de2dc13b9c72a7dded57ed4d3bcbc2ff36152dd61c04']
    ])
    {
        const file = manifest.files.find(file => file.relativePath === path);
        assert.equal(file.byteLength, byteLength); assert.equal(file.sha256, hash); assert.equal(file.kind, 'dds');
    }
    assert.equal((await validateM3Manifest(manifest)).profileDigest, manifest.profileDigest);
});
