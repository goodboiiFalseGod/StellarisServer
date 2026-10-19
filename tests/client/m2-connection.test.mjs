import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../src/Stellaris.Server/wwwroot/app.js', import.meta.url), 'utf8');

function harness(withToken = true) {
  let now = 0;
  let nextTimer = 1;
  let random = 0;
  const timers = new Map();
  const sockets = [];
  const elements = new Map();
  const values = new Map();
  if (withToken) values.set('stellaris.m2.tab', JSON.stringify({ name: 'Игрок', joinCode: 'test', token: 'token', sessionId: 'session' }));
  class Element {
    constructor() { this.handlers = new Map(); this.children = []; this.dataset = {}; this.value = ''; }
    addEventListener(type, handler) { this.handlers.set(type, handler); }
    emit(type) { this.handlers.get(type)?.({ preventDefault() {} }); }
    replaceChildren(...children) { this.children = children; }
    append(...children) { this.children.push(...children); }
    prepend(child) { this.children.unshift(child); }
  }
  class Socket {
    static OPEN = 1;
    constructor() { this.readyState = 0; this.handlers = new Map(); this.sent = []; this.closeCount = 0; sockets.push(this); }
    addEventListener(type, handler) { this.handlers.set(type, handler); }
    emit(type, data) { this.handlers.get(type)?.({ data: JSON.stringify(data) }); }
    open() { this.readyState = 1; this.emit('open'); }
    send(data) { this.sent.push(JSON.parse(data)); }
    // Намеренно без close event: потерянное соединение может остаться OPEN.
    close() { this.closeCount++; }
  }
  function schedule(callback, delay, interval = false) {
    const id = nextTimer++;
    timers.set(id, { callback, at: now + delay, delay, interval });
    return id;
  }
  const document = new Element();
  document.getElementById = id => {
    if (!elements.has(id)) elements.set(id, new Element());
    return elements.get(id);
  };
  document.createElement = () => new Element();
  const window = new Element();
  const context = vm.createContext({
    document, window, WebSocket: Socket, location: { protocol: 'http:', host: 'localhost' },
    performance: { now: () => now }, console, Intl, Uint8Array,
    sessionStorage: { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) },
    crypto: { getRandomValues: array => array.fill(++random) }, confirm: () => true,
    setTimeout: (callback, delay) => schedule(callback, delay), clearTimeout: id => timers.delete(id),
    setInterval: (callback, delay) => schedule(callback, delay, true), clearInterval: id => timers.delete(id)
  });
  vm.runInContext(source, context, { filename: 'app.js' });
  const el = id => document.getElementById(id);
  function advance(milliseconds) {
    const end = now + milliseconds;
    while (true) {
      const due = [...timers].filter(([, timer]) => timer.at <= end).sort((a, b) => a[1].at - b[1].at)[0];
      if (!due) break;
      const [id, timer] = due;
      now = Math.max(now, timer.at);
      if (timer.interval) timer.at = now + timer.delay;
      else timers.delete(id);
      timer.callback();
    }
    now = end;
  }
  function connect() {
    el('name').value = 'Игрок'; el('join-code').value = 'test';
    el('join-form').emit('submit');
    const socket = sockets.at(-1);
    socket.open();
    return socket;
  }
  const welcome = socket => socket.emit('message', { type: 'welcome', protocol: 1, participantId: 1, token: 'token', sessionId: 'session' });
  const snapshot = (socket, sequence = 1, overrides = {}) => socket.emit('message', { type: 'snapshot', state: {
    protocol: 1, sessionId: 'session', sequence, you: 1, participants: [], planets: [],
    phase: 'running', month: 0, paused: true, speed: 1, lastTickMilliseconds: 0, ...overrides
  } });
  const join = () => { const socket = connect(); welcome(socket); snapshot(socket); return socket; };
  return { el, sockets, connect, welcome, snapshot, join, advance, jump: ms => { now += ms; },
    online: () => vm.runInContext('online()', context), document, window,
    pending: () => JSON.parse(values.get('stellaris.m2.tab')).pending };
}

test('Запуск доступен при любом положительном числе готовых подключённых игроков', () => {
  for (const count of [1, 2, 3, 10, 20]) {
    const h = harness(); const socket = h.connect(); h.welcome(socket);
    const participants = Array.from({ length: count }, (_, i) => ({ id: i + 1, name: `Игрок ${i + 1}`, role: 'player', isHost: i === 0, connected: true, ready: true }));
    participants.push({ id: count + 1, name: 'Наблюдатель', role: 'pendingObserver', connected: false, ready: false });
    h.snapshot(socket, 1, { phase: 'lobby', participants });
    assert.equal(h.el('start').disabled, false);
    assert.equal(h.el('participants').children.length, count + 1);
    participants[count - 1].ready = false;
    h.snapshot(socket, 2, { phase: 'lobby', participants });
    assert.equal(h.el('start').disabled, true);
    participants[count - 1].ready = true; participants[count - 1].connected = false;
    h.snapshot(socket, 3, { phase: 'lobby', participants });
    assert.equal(h.el('start').disabled, true);
    h.snapshot(socket, 4, { phase: 'lobby', participants: [] });
    assert.equal(h.el('start').disabled, true);
  }
});

test('OPEN без ответов: тайм-аут блокирует команды и запускает возврат без события close', () => {
  const h = harness(); const socket = h.join();
  h.advance(29999); assert.equal(h.el('step').disabled, false);
  h.advance(1);
  assert.equal(socket.readyState, 1); assert.equal(socket.closeCount, 1);
  assert.equal(h.el('step').disabled, true); assert.equal(h.el('stale').hidden, false);
  h.advance(1000); assert.equal(h.sockets.length, 2);
});

test('pong и snapshot с прежним sequence подтверждают связь на паузе', () => {
  const h = harness(); const socket = h.join();
  for (let i = 0; i < 8; i++) {
    h.advance(10000);
    if (i % 2) h.snapshot(socket); else socket.emit('message', { type: 'pong' });
  }
  assert.equal(h.online(), true); assert.equal(socket.closeCount, 0);
  assert.equal(h.sockets.length, 1);
});

test('Поздний ответ не оживляет просроченный socket; старые callbacks не влияют на новый', () => {
  const h = harness(); const old = h.join();
  h.jump(30000); old.emit('message', { type: 'pong' });
  assert.equal(h.online(), false); assert.equal(old.closeCount, 1);
  h.advance(1000); const next = h.sockets[1]; next.open(); h.welcome(next); h.snapshot(next);
  const status = h.el('connection-status').textContent;
  const error = h.el('error').textContent;
  old.emit('open'); old.emit('error'); old.emit('close'); h.welcome(old); h.snapshot(old, 999);
  assert.equal(h.online(), true); assert.equal(h.el('connection-status').textContent, status);
  assert.equal(h.el('error').textContent, error); assert.equal(old.sent.filter(x => x.type === 'join').length, 1);
});

test('Возврат вкладки и прямой ввод проверяют срок перед отправкой', () => {
  for (const event of ['visibilitychange', 'pageshow', 'input']) {
    const h = harness(); const socket = h.join();
    h.jump(31000);
    if (event === 'visibilitychange') h.document.emit(event);
    if (event === 'pageshow') h.window.emit(event);
    h.el('step').emit('click');
    assert.equal(h.online(), false); assert.equal(h.pending().length, 0);
    assert.equal(socket.sent.filter(x => x.type === 'step').length, 0);
    assert.equal(h.el('stale').hidden, false);
  }
});

test('Возврат ждёт snapshot, сохраняет commandId и прекращает повторы после ACK', () => {
  const h = harness(); const first = h.join();
  h.el('step').emit('click'); h.advance(125);
  const command = first.sent.find(x => x.type === 'step'); assert.ok(command);
  h.jump(30000); h.window.emit('pageshow'); h.advance(1000);
  const second = h.sockets[1]; second.open(); h.welcome(second); h.advance(500);
  assert.equal(second.sent.some(x => x.type === 'step'), false); assert.equal(h.el('step').disabled, true);
  h.snapshot(second); h.advance(125);
  const repeat = second.sent.find(x => x.type === 'step'); assert.deepEqual(repeat, command);
  second.emit('message', { type: 'result', result: { commandId: repeat.commandId, accepted: true, message: '' } });
  assert.equal(h.pending().length, 0);
  second.emit('close'); h.advance(1000);
  const third = h.sockets[2]; third.open(); h.welcome(third); h.snapshot(third); h.advance(500);
  assert.equal(third.sent.some(x => x.type === 'step'), false);
});

test('Отложенная отправка не обходит тайм-аут после возобновления таймеров', () => {
  const h = harness(); const socket = h.join();
  h.el('step').emit('click');
  const original = h.pending()[0];
  h.jump(31000); h.advance(0);
  assert.equal(socket.sent.some(x => x.type === 'step'), false);
  assert.deepEqual(h.pending()[0], original);
  assert.equal(h.online(), false);
});

test('Ручное отключение и очистка входа отменяют контроль срока и автовозврат', () => {
  for (const button of ['disconnect', 'clear-login']) {
    const h = harness(); const old = h.join(); h.el(button).emit('click');
    h.advance(60000); old.emit('close'); old.emit('message', { type: 'pong' });
    assert.equal(h.sockets.length, 1); assert.equal(h.online(), false); assert.equal(old.closeCount, 1);
  }
});

test('До welcome действует тайм-аут; без токена автоматической регистрации нет', () => {
  const h = harness(false); const socket = h.connect();
  h.advance(20000); socket.emit('message', { type: 'pong' });
  h.advance(10000); assert.equal(socket.closeCount, 1);
  h.advance(10000); assert.equal(h.sockets.length, 1); assert.equal(h.el('connect').disabled, false);
});

test('Чужая сессия не продлевает срок; отказ входа не запускает автовозврат', () => {
  const h = harness(); const socket = h.join();
  h.advance(20000); h.snapshot(socket, 2, { sessionId: 'other' });
  h.advance(10000); assert.equal(h.online(), false);
  const denied = harness(); const attempt = denied.connect();
  attempt.emit('message', { type: 'error', message: 'Вход отклонён' });
  denied.advance(60000); assert.equal(denied.sockets.length, 1); assert.equal(denied.online(), false);
});

test('Неверный ответ не продлевает срок, неизвестное сообщение отключает соединение', () => {
  const h = harness(); const socket = h.join();
  h.advance(20000);
  socket.emit('message', { type: 'result', result: { commandId: 'invalid', accepted: 'yes' } });
  assert.equal(h.online(), true);
  h.advance(10000);
  assert.equal(h.online(), false);
  assert.equal(socket.closeCount, 1);

  const unknown = harness(); const current = unknown.join();
  unknown.advance(20000);
  current.emit('message', { type: 'unknown' });
  assert.equal(unknown.online(), false);
  assert.match(unknown.el('error').textContent, /Неизвестный тип сообщения/);
  unknown.advance(60000);
  assert.equal(unknown.sockets.length, 1);
});
