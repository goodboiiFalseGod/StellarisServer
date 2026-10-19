import test from 'node:test';
import assert from 'node:assert/strict';
import { M3Importer } from './m3-importer.mjs';

function harness()
{
    const workers = [];
    class Worker
    {
        constructor() { this.listeners = new Map(); this.terminateCount = 0; workers.push(this); }
        addEventListener(type, callback) { this.listeners.set(type, callback); }
        removeEventListener(type) { this.listeners.delete(type); }
        postMessage(message) { this.request = message; }
        terminate() { this.terminateCount++; }
        emit(type, data) { this.listeners.get(type)?.(type === 'message' ? { data: { requestId: this.request.requestId, ...data } } : data); }
    }
    return { workers, owner: new M3Importer({ workerFactory: () => new Worker() }) };
}
test('Owner: typed generated error сохраняет known code/class, unknown metadata не повышается до trusted type',async()=>
{
    for(const [typed,expected] of [[{code:'shader',classId:'pc_toxic'},true],[{code:'memory',classId:null},true],[{code:'skin-policy',classId:'pc_broken'},true],[{code:'attachment-shader',classId:'pc_black_hole'},true],[{code:'unknown-test',classId:'pc_toxic'},false],[{code:'shader',classId:'../../x'},false],[{code:'shader',classId:'pc_toxic',extra:true},false]])
    {
        const h=harness(); const promise=h.owner.import({});
        h.workers[0].emit('message',{type:'error',message:'source failure',generatedVisualError:typed});
        await assert.rejects(promise,error=>error.message==='source failure'&&(expected?error.code===typed.code&&error.classId===typed.classId:error.code===undefined&&error.classId===undefined));
        assert.equal(h.workers[0].terminateCount,1);
    }
});
test('Owner: progress/result, одно terminate и поздние callbacks игнорируются', async () =>
{
    const h = harness(); const progress = [];
    const promise = h.owner.import({}, { onProgress: value => progress.push(value) });
    const worker = h.workers[0]; const old = worker.listeners.get('message');
    worker.emit('message', { type: 'progress', progress: { phase: 'read' } });
    worker.emit('message', { type: 'result', result: { verified: true } });
    assert.deepEqual(await promise, { verified: true });
    old({ data: { requestId: worker.request.requestId, type: 'progress', progress: { phase: 'late' } } });
    assert.equal(progress.length, 1); assert.equal(worker.terminateCount, 1);
    assert.equal(h.owner.active, null);
});
test('Owner: error/messageerror/worker error/callback exception завершают Promise и worker', async () =>
{
    for (const type of ['error', 'messageerror', 'reply-error', 'callback'])
    {
        const h = harness();
        const promise = h.owner.import({}, { onProgress: () => { throw new Error('callback failed'); } });
        const rejection = assert.rejects(promise);
        const worker = h.workers[0];
        if (type === 'reply-error') { worker.emit('message', { type: 'error', message: 'decode failed' }); }
        else if (type === 'callback') { worker.emit('message', { type: 'progress', progress: {} }); }
        else { worker.emit(type, { message: 'worker failed' }); }
        await rejection; assert.equal(worker.terminateCount, 1);
    }
});
test('Owner: abort до старта/во время работы, новый запрос и dispose', async () =>
{
    const h = harness(); const early = new AbortController(); early.abort();
    await assert.rejects(h.owner.import({}, { signal: early.signal }), { name: 'AbortError' });
    assert.equal(h.workers.length, 0);
    const controller = new AbortController();
    const first = h.owner.import({}, { signal: controller.signal }); const firstRejection = assert.rejects(first, { name: 'AbortError' });
    controller.abort(); await firstRejection; assert.equal(h.workers[0].terminateCount, 1);
    const second = h.owner.import({}); const secondRejection = assert.rejects(second, { name: 'AbortError' });
    const oldCallback = h.workers[1].listeners.get('message');
    const third = h.owner.import({}); await secondRejection;
    oldCallback({ data: { type: 'result', requestId: h.workers[1].request.requestId, result: 'late' } });
    const thirdRejection = assert.rejects(third, { name: 'AbortError' }); h.owner.dispose(); await thirdRejection;
    assert.deepEqual(h.workers.map(worker => worker.terminateCount), [1, 1, 1]);
    await assert.rejects(h.owner.import({}), /освобождён/);
});
test('Owner: factory/postMessage failure и неверное сообщение не зависают', async () =>
{
    await assert.rejects(new M3Importer({ workerFactory: () => { throw new Error('factory'); } }).import({}), /factory/);
    let terminations = 0;
    const owner = new M3Importer({ workerFactory: () => ({ addEventListener() {}, removeEventListener() {}, terminate() { terminations++; }, postMessage() { throw new Error('clone'); } }) });
    await assert.rejects(owner.import({}), /clone/); assert.equal(terminations, 1);
    const h = harness(); const promise = h.owner.import({}); const reject = assert.rejects(promise, /Неизвестное/);
    h.workers[0].emit('message', { type: 'unknown' }); await reject;
});
test('Owner: callback бросает null — это отклонение Promise, не успешный undefined', async () =>
{
    const h = harness(); const promise = h.owner.import({}, { onProgress: () => { throw null; } });
    const rejected = assert.rejects(promise, /null/);
    h.workers[0].emit('message', { type: 'progress', progress: {} });
    await rejected; assert.equal(h.workers[0].terminateCount, 1);
});

test('Owner: отмена staged cube игнорирует поздний результат, прежний профиль не изменён', async () =>
{
    const h = harness();
    const retainedFace = new Uint8Array([1, 2, 3, 255]);
    const retainedSprite = new Uint8Array([4, 5, 6, 255]);
    const retainedCore = new Uint8Array([7, 8, 9, 255]);
    const previous = { samples: new Map([['star.g', { images: new Map() }]]), backgrounds: new Map([['system.sky', { image: { faces: [retainedFace] } }]]), uiSprites: new Map([['frame.dds', { image: { rgba: retainedSprite } }]]), galaxyLayers: new Map([['core', { image: { rgba: retainedCore } }]]) };
    let current = previous;
    const controller = new AbortController();
    const request = h.owner.import({}, { signal: controller.signal }).then(value => { current = value.profile; });
    const rejected = assert.rejects(request, { name: 'AbortError' });
    const worker = h.workers[0], lateReply = worker.listeners.get('message');
    controller.abort();
    const cube = { faces: Array.from({ length: 6 }, () => new Uint8Array(4)), isCubemap: true };
    lateReply({ data: { requestId: worker.request.requestId, type: 'result', result: { profile: cube } } });
    await rejected;
    assert.equal(current, previous);
    assert.deepEqual([...retainedFace], [1, 2, 3, 255]);
    assert.deepEqual([...retainedSprite], [4, 5, 6, 255]);
    assert.deepEqual([...retainedCore], [7, 8, 9, 255]);
    assert.equal(worker.terminateCount, 1);
    assert.equal(worker.listeners.size, 0);
    assert.equal(h.owner.active, null);
});
