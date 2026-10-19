const ALLOWED_RTT = new Set([0, 200, 500, 1500, 5000, 40000]);
const MAX_MESSAGES = 512;
const MAX_BYTES = 16 * 1024 * 1024;
const encoder = new TextEncoder();

export function parseLatency(search)
{
    const params = new URLSearchParams(search);
    if (!params.has('latency')) { return 0; }
    const raw = params.get('latency');
    if (!/^\d+$/.test(raw ?? '')) { throw new Error('Недопустимое значение latency. Выберите 0, 200, 500, 1500, 5000 или 40000 мс.'); }
    const value = Number(raw);
    if (!ALLOWED_RTT.has(value)) { throw new Error('Недопустимое значение latency. Выберите 0, 200, 500, 1500, 5000 или 40000 мс.'); }
    return value;
}

export function createLatencySocketFactory({ rtt, socketFactory = address => new WebSocket(address), now = () => performance.now(), random = Math.random, setTimeout: schedule = (...args) => globalThis.setTimeout(...args), clearTimeout: cancel = id => globalThis.clearTimeout(id) } = {})
{
    if (!ALLOWED_RTT.has(rtt)) { throw new Error('Недопустимый RTT: выберите 0, 200, 500, 1500, 5000 или 40000 мс.'); }
    if (rtt === 0) { return address => socketFactory(address); }
    const halfRtt = rtt / 2;

    return address =>
    {
        const socket = socketFactory(address);
        let ended = false;
        let syntheticCloseDispatched = false;
        const listeners = new Map();
        const directions = [makeQueue('out'), makeQueue('in')];

        function clearQueue(queue)
        {
            if (queue.timer !== null) { cancel(queue.timer); queue.timer = null; }
            queue.items.length = 0; queue.bytes = 0; queue.lastDue = 0;
        }
        function end()
        {
            if (ended) { return; }
            ended = true;
            for (const queue of directions) { clearQueue(queue); }
        }
        function emit(type, fields)
        {
            const event = new Event(type);
            for (const [key, value] of Object.entries(fields)) { Object.defineProperty(event, key, { value, enumerable: true }); }
            dispatch(type, event);
        }
        function closeWithDiagnostic(code, reason)
        {
            end(); syntheticCloseDispatched = true;
            try { socket.close(code, reason); }
            catch (error) { try { console.error('Не удалось закрыть WebSocket после ошибки задержки.', error); } catch { /* Только диагностика. */ } }
            emit('close', { code, reason, wasClean: false });
        }
        function deliver(queue)
        {
            queue.timer = null;
            if (ended || queue.items.length === 0) { return; }
            const item = queue.items.shift();
            queue.bytes -= item.bytes;
            if (queue.direction === 'out')
            {
                try { socket.send(item.value); }
                catch (error)
                {
                    const reason = 'Ошибка отложенной отправки.';
                    try { console.error(reason, error); } catch { /* Диагностика не должна прерывать очистку. */ }
                    end();
                    try { socket.close(4011, reason); } catch (closeError) { try { console.error('Не удалось закрыть WebSocket после ошибки отправки.', closeError); } catch { /* Только диагностика. */ } }
                    emit('error', { message: reason, error });
                    syntheticCloseDispatched = true;
                    emit('close', { code: 4011, reason, wasClean: false });
                    return;
                }
            }
            else { dispatch('message', item.event); }
            if (queue.items.length) { arm(queue); }
            else { queue.lastDue = 0; }
        }
        function arm(queue)
        {
            if (ended || queue.timer !== null || queue.items.length === 0) { return; }
            const remaining = Math.max(0, queue.items[0].due - now());
            queue.timer = schedule(() => deliver(queue), remaining);
        }
        function enqueue(queue, value, byteLength, event = null)
        {
            if (ended) { return; }
            if (queue.items.length >= MAX_MESSAGES || queue.bytes + byteLength > MAX_BYTES)
            {
                const message = `Очередь задержки ${queue.direction === 'out' ? 'отправки' : 'получения'} переполнена; соединение закрыто.`;
                try { console.error(message); } catch { /* Диагностика не должна прерывать очистку. */ }
                closeWithDiagnostic(4009, message);
                return;
            }
            const jitter = 0.8 + Math.max(0, Math.min(1, Number(random()) || 0)) * 0.4;
            const due = Math.max(queue.lastDue, now() + halfRtt * jitter);
            queue.lastDue = due;
            queue.items.push({ value, event, bytes: byteLength, due });
            queue.bytes += byteLength;
            arm(queue);
        }
        function dispatch(type, event)
        {
            if (ended && type === 'message') { return; }
            for (const [callback, options] of [...(listeners.get(type) ?? new Map())])
            {
                if (options.once) { listeners.get(type).delete(callback); }
                if (typeof callback === 'function') { callback.call(wrapper, event); }
                else { callback?.handleEvent?.call(callback, event); }
            }
        }
        const nativeListeners = {
            open: event => { if (!ended) { dispatch('open', event); } },
            close: event => { end(); if (!syntheticCloseDispatched) { dispatch('close', event); } },
            error: event => { end(); dispatch('error', event); },
            message: event =>
            {
                if (ended) { return; }
                if (typeof event.data !== 'string') { dispatch('message', event); return; }
                enqueue(directions[1], event.data, encoder.encode(event.data).byteLength, event);
            }
        };
        for (const [type, listener] of Object.entries(nativeListeners)) { socket.addEventListener(type, listener); }
        const wrapper = {
            get readyState() { return ended && socket.readyState < 2 ? 2 : socket.readyState; },
            get bufferedAmount() { return socket.bufferedAmount ?? 0; },
            get extensions() { return socket.extensions ?? ''; },
            get protocol() { return socket.protocol ?? ''; },
            get url() { return socket.url ?? address; },
            send(data)
            {
                if (ended) { throw new DOMException('WebSocket is already in CLOSING or CLOSED state.', 'InvalidStateError'); }
                if (typeof data !== 'string') { socket.send(data); return; }
                enqueue(directions[0], data, encoder.encode(data).byteLength);
            },
            close(code, reason)
            {
                if (ended) { return; }
                socket.close(code, reason); end();
            },
            addEventListener(type, callback, options)
            {
                if (callback == null) { return; }
                let entries = listeners.get(type);
                if (!entries) { entries = new Map(); listeners.set(type, entries); }
                if (!entries.has(callback)) { entries.set(callback, { once: Boolean(typeof options === 'object' && options?.once) }); }
            },
            removeEventListener(type, callback) { listeners.get(type)?.delete(callback); },
            dispatchEvent(event) { dispatch(event.type, event); return !event.defaultPrevented; }
        };
        function makeQueue(direction) { return { direction, items: [], bytes: 0, timer: null, lastDue: 0 }; }
        return wrapper;
    };
}
