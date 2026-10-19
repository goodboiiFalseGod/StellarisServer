import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Execute the production lifecycle handler with a delayed browser image boundary.
const client = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/m3-client.mjs', import.meta.url), 'utf8');
const start = client.indexOf("window.addEventListener('pageshow',");
const end = client.indexOf('\ntry { createRenderer();', start);
const handlerSource = client.slice(start, end);

function runHandler(context)
{
    Object.assign(context, { AbortController, importSettled: Promise.resolve(), restoreSettled: Promise.resolve(), imageRestoreAbort: null, planetImages: null, planetViewKey: '', renderPlanetWindow() {} });
    if (context.renderer) { context.renderer.stats = () => ({ convertedBytes: 0 }); }
    vm.runInNewContext(handlerSource, context);
}

test('BFCache frame loop resumes even when a clear-login invalidates delayed UI restoration', async () =>
{
    let resume; let handler; let scheduled = 0; let disposed = 0; let created = 0;
    const context = {
        window: { addEventListener(name, callback) { assert.equal(name, 'pageshow'); handler = callback; } },
        active: false, importGeneration: 0, restorationId: 0, restoringProfile: false, profile: { decodedBytes: 100 }, uiSprites: null, state: null, renderer: null,
        MAX_MEMORY: 512 * 1024 * 1024, frame() {}, frameId: null, hint() {}, renderControls() {}, error(message) { assert.fail(message); },
        requestAnimationFrame() { scheduled++; return scheduled; },
        prepareUiSprites: () => new Promise(resolve => { resume = resolve; }),
        createRenderer() { created++; }
    };
    runHandler(context);
    const restoring = handler({ persisted: true });
    for (let i = 0; i < 8; i++) { await Promise.resolve(); }
    context.importGeneration++; context.restorationId++; context.profile = null; context.restoringProfile = false;
    resume({ dispose() { disposed++; }, apply() { assert.fail('Stale UI must not publish'); } });
    await restoring;
    assert.equal(scheduled, 1); assert.equal(disposed, 1); assert.equal(created, 0); assert.equal(context.active, true);
});

test('Connect is blocked until delayed restoration owns one renderer and publishes full profile readiness', async () =>
{
    let resume; let handler; let join; let created = 0; let readiness = 0; let rejected = 0;
    const context = {
        window: { addEventListener(name, callback) { handler = callback; } },
        active: false, restorationId: 0, restoringProfile: false, profile: { decodedBytes: 100 }, uiSprites: null, state: null, renderer: null,
        manifest: { profileId: 'fixture', profileDigest: 'digest' }, MAX_MEMORY: 512 * 1024 * 1024,
        frame() {}, frameId: null, hint() {}, renderControls() {}, renderIdentity() {}, error() { rejected++; },
        requestAnimationFrame() { return 1; },
        prepareUiSprites: () => new Promise(resolve => { resume = resolve; }),
        createRenderer() { created++; context.renderer = { setProfile() {} }; },
        connection: { setProfile() { assert.equal(context.restoringProfile, false); readiness++; } },
        $: () => ({ addEventListener(name, callback) { join = callback; } })
    };
    const joinSource = client.slice(client.indexOf("$('join').addEventListener('submit',"), client.indexOf("$('disconnect').onclick"));
    runHandler(context); vm.runInNewContext(joinSource, context);
    const restoring = handler({ persisted: true });
    for (let i = 0; i < 8; i++) { await Promise.resolve(); }
    join({ preventDefault() {} });
    assert.equal(created, 0); assert.equal(readiness, 0); assert.equal(rejected, 1);
    // Folder/file cancellation must not invalidate restoration of the unchanged accepted CPU profile.
    context.importGeneration = 5;
    resume({ decodedBytes: 16, dispose() {}, apply() {} }); await restoring;
    assert.equal(created, 1); assert.equal(readiness, 1); assert.equal(context.restoringProfile, false);
});

test('Restoration reuses an existing renderer instead of leaking a second GPU owner', async () =>
{
    let handler; let restored = 0;
    const context = {
        window: { addEventListener(name, callback) { handler = callback; } },
        active: false, restorationId: 0, restoringProfile: false, profile: { decodedBytes: 100 }, uiSprites: null, state: null,
        renderer: { setProfile() { restored++; } }, manifest: null, MAX_MEMORY: 512 * 1024 * 1024,
        frame() {}, hint() {}, renderControls() {}, renderIdentity() {}, error(message) { assert.fail(message); }, requestAnimationFrame() { return 1; },
        prepareUiSprites: async () => ({ decodedBytes: 16, dispose() {}, apply() {} }), createRenderer() { assert.fail('Duplicate renderer'); }
    };
    runHandler(context); await handler({ persisted: true }); assert.equal(restored, 1);
});

for (const failureAt of ['sprites', 'gpu'])
{
    test(`Failed ${failureAt} restoration keeps readiness blocked and exposes the failure`, async () =>
    {
        let handler; let failure;
        const context = {
            window: { addEventListener(name, callback) { handler = callback; } },
            active: false, restorationId: 0, restoringProfile: false, profile: { decodedBytes: 100 }, uiSprites: null, state: null,
            renderer: { setProfile() { if (failureAt === 'gpu') { throw new Error('fixture GPU failure'); } } },
            manifest: { profileId: 'fixture', profileDigest: 'digest' }, MAX_MEMORY: 512 * 1024 * 1024,
            frame() {}, hint() {}, renderControls() {}, renderIdentity() {}, error(message) { failure = message; }, requestAnimationFrame() { return 1; },
            connection: { setProfile() { assert.fail('Failed restoration must not publish readiness'); } },
            prepareUiSprites: async () => { if (failureAt === 'sprites') { throw new Error('fixture sprite failure'); } return { decodedBytes: 16, dispose() {}, apply() {} }; },
            createRenderer() { assert.fail('Existing renderer must be reused'); }
        };
        runHandler(context); await handler({ persisted: true });
        assert.equal(context.restoringProfile, true); assert.match(failure, /fixture .* failure/);
    });
}
