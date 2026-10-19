import test from 'node:test';
import assert from 'node:assert/strict';
import { createLatencySocketFactory, parseLatency } from '../../src/Stellaris.Server/wwwroot/m3-latency.mjs';

class FakeSocket
{
    constructor() { this.readyState = 0; this.listeners = new Map(); this.sent = []; this.closeCalls = []; }
    addEventListener(type, callback) { const list = this.listeners.get(type) ?? []; list.push(callback); this.listeners.set(type, list); }
    send(value) { if (this.readyState !== 1) { throw new Error('socket is not open'); } this.sent.push(value); }
    close(...args)
    {
        if (args[0] !== undefined && args[0] !== 1000 && !(args[0] >= 3000 && args[0] <= 4999)) { throw new Error('invalid close code'); }
        this.closeCalls.push(args); this.readyState = 2;
    }
    emit(type, event = {})
    {
        if (type === 'open') { this.readyState = 1; }
        if (type === 'close') { this.readyState = 3; }
        for (const listener of this.listeners.get(type) ?? []) { listener(event); }
    }
}

function harness({ rtt = 200, random = () => 0.5 } = {})
{
    let time = 0; let nextId = 1;
    const timers = new Map(); const sockets = [];
    const factory = createLatencySocketFactory({ rtt, random, now: () => time,
        socketFactory: address => { const socket = new FakeSocket(); socket.address = address; sockets.push(socket); return socket; },
        setTimeout: (callback, delay) => { const id = nextId++; timers.set(id, { callback, due: time + delay }); return id; },
        clearTimeout: id => timers.delete(id) });
    function advance(milliseconds)
    {
        const target = time + milliseconds;
        while (true)
        {
            const due = [...timers].filter(([, timer]) => timer.due <= target).sort((a, b) => a[1].due - b[1].due || a[0] - b[0])[0];
            if (!due) { break; }
            time = due[1].due; timers.delete(due[0]); due[1].callback();
        }
        time = target;
    }
    const socket = factory('ws://example.test/ws');
    sockets[0].readyState = 1;
    return { socket, native: sockets[0], timers, advance, time: () => time, sockets };
}

test('latency parameter accepts presets and absent value, rejects invalid values in Russian', () =>
{
    assert.equal(parseLatency(''), 0);
    for (const value of [0, 200, 500, 1500, 5000, 40000]) { assert.equal(parseLatency(`?latency=${value}`), value); }
    for (const value of ['abc', '-1', '201', '1.5', '']) { assert.throws(() => parseLatency(`?latency=${encodeURIComponent(value)}`), /latency/i); }
    assert.throws(() => parseLatency('?latency=201'), /Недопустимое значение/);
});

test('zero RTT returns the original socket unchanged', () =>
{
    const native = new FakeSocket();
    const factory = createLatencySocketFactory({ rtt: 0, socketFactory: () => native });
    assert.equal(factory('ws://example.test'), native);
});

test('both directions wait half RTT and inbound event data is preserved by identity', () =>
{
    const h = harness(); const received = []; const event = { data: 'server text', origin: 'https://server.test', marker: {} };
    h.socket.addEventListener('message', value => received.push(value));
    h.native.emit('message', event);
    h.socket.send('client text');
    h.advance(99); assert.deepEqual(received, []); assert.deepEqual(h.native.sent, []);
    h.advance(1); assert.deepEqual(received, [event]); assert.deepEqual(h.native.sent, ['client text']);
});

test('independent jitter never reorders queued messages in either direction', () =>
{
    const samples = [1, 0, 0, 1]; let index = 0;
    const h = harness({ random: () => samples[index++] }); const incoming = [];
    h.socket.addEventListener('message', event => incoming.push(event.data));
    h.socket.send('out-late'); h.socket.send('out-early');
    h.native.emit('message', { data: 'in-late' }); h.native.emit('message', { data: 'in-early' });
    h.advance(80); assert.deepEqual(h.native.sent, []); assert.deepEqual(incoming, ['in-late']);
    h.advance(41); assert.deepEqual(h.native.sent, ['out-late', 'out-early']); assert.deepEqual(incoming, ['in-late', 'in-early']);
});

test('RTT 1500 delivers a continuous 5 Hz stream within 600–900 ms without accumulating delay', () =>
{
    const samples = [0, 1, 0.25, 0.75]; let index = 0;
    const h = harness({ rtt: 1500, random: () => samples[index++ % samples.length] }); const deliveries = [];
    h.socket.addEventListener('message', event => deliveries.push({ value: event.data, at: h.time() }));
    const sentAt = [];
    for (let i = 0; i < 20; i++)
    {
        sentAt.push(h.time()); h.native.emit('message', { data: `snapshot-${i}` }); h.advance(200);
    }
    h.advance(901);
    assert.equal(deliveries.length, sentAt.length);
    for (let i = 0; i < deliveries.length; i++)
    {
        assert.equal(deliveries[i].value, `snapshot-${i}`);
        const delay = deliveries[i].at - sentAt[i];
        assert.ok(delay >= 600 && delay <= 900, `сообщение ${i}: ${delay} мс`);
    }
});

test('open, close, and error remain immediate; close and error cancel queued work', () =>
{
    const h = harness(); const calls = [];
    for (const type of ['open', 'error', 'close']) { h.socket.addEventListener(type, event => calls.push([type, event])); }
    const open = {}; h.native.emit('open', open); assert.deepEqual(calls, [['open', open]]);
    h.socket.send('pending'); const error = {}; h.native.emit('error', error);
    assert.deepEqual(calls.at(-1), ['error', error]); assert.equal(h.timers.size, 0);
    h.native.emit('close', {}); assert.equal(calls.at(-1)[0], 'close');
    h.advance(1000); assert.deepEqual(h.native.sent, []);

    const second = harness(); second.socket.send('discarded'); second.socket.close(1000, 'done');
    assert.equal(second.timers.size, 0); second.advance(1000); assert.deepEqual(second.native.sent, []);
});

test('queue overflow reports a diagnostic, closes with 4009, and clears timers', () =>
{
    const h = harness(); const previous = console.error; const diagnostics = []; let closeEvent;
    h.socket.addEventListener('close', event => { closeEvent = event; });
    console.error = message => diagnostics.push(message);
    try
    {
        for (let i = 0; i < 513; i++) { h.socket.send('x'); }
        assert.equal(diagnostics.length, 1); assert.match(diagnostics[0], /переполнена/);
        assert.deepEqual(h.native.closeCalls, [[4009, diagnostics[0]]]); assert.equal(h.timers.size, 0);
        assert.equal(closeEvent.code, 4009); assert.equal(closeEvent.reason, diagnostics[0]); assert.equal(closeEvent.wasClean, false);
        h.native.emit('close', { code: 4009, reason: diagnostics[0], wasClean: false }); assert.equal(closeEvent.reason, diagnostics[0]);
    }
    finally { console.error = previous; }
});

test('UTF-8 byte budget counts multibyte payloads and overflows at 16 MiB', () =>
{
    const h = harness({ rtt: 40000 }); const previous = console.error; let diagnostic = '';
    console.error = message => { diagnostic = message; };
    try
    {
        const payload = 'é'.repeat(8 * 1024 * 1024); // 16 МиБ в UTF-8.
        h.socket.send(payload); assert.equal(h.timers.size, 1);
        h.socket.send('x'); assert.match(diagnostic, /переполнена/);
        assert.deepEqual(h.native.closeCalls, [[4009, diagnostic]]); assert.equal(h.timers.size, 0);
    }
    finally { console.error = previous; }
});

test('a delayed send failure emits diagnostic error and close events and cancels the queue', () =>
{
    const h = harness(); const calls = []; const previous = console.error;
    console.error = () => {};
    try
    {
        h.socket.addEventListener('error', event => calls.push(['error', event.message]));
        h.socket.addEventListener('close', event => calls.push(['close', event.code, event.reason]));
        h.socket.send('will fail'); h.native.readyState = 2;
        h.advance(101);
        assert.deepEqual(calls, [['error', 'Ошибка отложенной отправки.'], ['close', 4011, 'Ошибка отложенной отправки.']]);
        assert.equal(h.timers.size, 0); assert.deepEqual(h.native.closeCalls, [[4011, 'Ошибка отложенной отправки.']]);
        h.native.emit('close', { code: 4011, reason: 'Ошибка отложенной отправки.' });
        assert.equal(calls.length, 2, 'поздний нативный close не дублирует synthetic close');
    }
    finally { console.error = previous; }
});

test('once listener is removed before invocation and cannot fire again', () =>
{
    const h = harness(); let count = 0; const listener = () => { count++; };
    h.socket.addEventListener('open', listener, { once: true });
    h.native.emit('open', {}); h.native.emit('open', {});
    assert.equal(count, 1);
});

test('wrapper exposes native state and removes listeners after closure without delayed callbacks', () =>
{
    const h = harness(); const received = []; h.socket.addEventListener('message', event => received.push(event));
    h.native.emit('message', { data: 'queued' }); h.socket.close();
    assert.equal(h.socket.readyState, 2); h.native.readyState = 3; h.native.emit('close', {});
    assert.equal(h.socket.readyState, 3); h.advance(500); assert.deepEqual(received, []);
    assert.throws(() => h.socket.send('after close'), error => error.name === 'InvalidStateError');
});

test('readyState reports CLOSING immediately when an error arrives before native close', () =>
{
    const h = harness(); h.native.emit('error', {});
    assert.equal(h.native.readyState, 1); assert.equal(h.socket.readyState, 2);
});
