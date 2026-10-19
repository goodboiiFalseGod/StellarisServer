import test from 'node:test';
import assert from 'node:assert/strict';
import { M3Connection, validateSimulationRate } from '../../src/Stellaris.Server/wwwroot/m3-connection.mjs';

function harness()
{
    let now = 0; const sockets = []; const events = [];
    class Socket
    {
        constructor() { this.readyState = 0; this.listeners = new Map(); this.sent = []; sockets.push(this); }
        addEventListener(type, callback) { this.listeners.set(type, callback); }
        send(text) { this.sent.push(JSON.parse(text)); }
        close() { this.readyState = 3; }
        emit(type, value) { if (type === 'open') { this.readyState = 1; } this.listeners.get(type)?.(type === 'message' ? { data: JSON.stringify(value) } : {}); }
    }
    const owner = new M3Connection({ url: 'ws://local/ws', now: () => now, socketFactory: () => new Socket(), setInterval: () => 1, clearInterval: () => {}, onState: (...args) => events.push(args), onStatus: (...args) => events.push(args) });
    owner.connect({ name: 'A', role: 'player', joinCode: 'test-code' });
    const welcome = { type: 'welcome', protocol: 2, participantId: 1, token: 'token', sessionId: 'session', profileId: 'profile', profileDigest: 'digest' };
    const state = { protocol: 2, sessionId: 'session', sequence: 1, you: 1 };
    function sync(socket = sockets.at(-1)) { socket.emit('open'); socket.emit('message', welcome); socket.emit('message', { type: 'snapshot', state }); }
    function ready() { sync(); owner.setProfile('profile', 'digest'); owner.pump(); sockets.at(-1).emit('message', { type: 'result', result: { commandId: owner.assetCommandId, accepted: true } }); }
    return { owner, sockets, events, sync, ready, state, welcome, time: value => { now = value; } };
}

const rate = (sampledAtMilliseconds = 100, overrides = {}) => ({ status: 'measured', automaticDaysPerRealSecond: 2, completedAutomaticDays: 4, windowSeconds: 2, sampledAtMilliseconds, ...overrides });

test('Таблица исследований раскрывается до публикации нового snapshot', () =>
{
    const h = harness(); h.ready();
    h.state.sequence++;
    h.state.research = { encoding: 'shared-unavailable-v1', empires: [{ participantId: 1, unavailable: null }, { participantId: 2, unavailable: null }],
        unavailableTable: [{ technologyId: 'x', name: 'x', gates: ['unknown'] }], unavailableIndices: [[0, 0], []] };
    h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    assert.deepEqual(h.owner.state.research.empires[0].unavailable, [{ technologyId: 'x', name: 'x', gates: ['unknown'] }, { technologyId: 'x', name: 'x', gates: ['unknown'] }]);
    assert.deepEqual(h.owner.state.research.empires[1].unavailable, []);
    assert.equal(Object.hasOwn(h.owner.state.research, 'encoding'), false);
});

test('Перестановка: UTF8 oversize отклонён до pending и отправки', () =>
{
    const h = harness(); h.ready();
    const before = h.owner.pending.size, sent = h.sockets[0].sent.length;
    const fields = { colonyId: 'colony', expectedColonyRevision: 0, orderIds: Array.from({ length: 110 }, (_, index) => 'я'.repeat(150) + index) };
    assert.throws(() => h.owner.command('reorderConstruction', fields), /16 KiB/);
    assert.equal(h.owner.pending.size, before); assert.equal(h.sockets[0].sent.length, sent);
    const id = h.owner.command('reorderConstruction', { ...fields, orderIds: ['a', 'c', 'b'] });
    assert.deepEqual(h.owner.pending.get(id).orderIds, ['a', 'c', 'b']);
});
test('Galaxy transport sends draft, explicit Ready and first Start before assets, then binds ACK to current candidate', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.participants = [{ id: 1, assetReady: false }];
    h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, startPending: false, worldFingerprint: null }; h.sync();
    assert.equal(h.owner.lobbyReady(), true); assert.equal(h.owner.ready(), false);
    assert.equal(h.owner.assetCommandId, null);
    h.owner.command('setGalaxySettings', { galaxySeed: 'seed', galaxyScenarioId: 'tiny', galaxyShapeId: 'spiral', galaxyDensity: 1 });
    h.owner.pump(); assert.equal(h.sockets[0].sent.at(-1).type, 'setGalaxySettings');
    h.state.sequence = 2; h.state.galaxy.lobbyRevision = 2; h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    h.owner.command('ready', { ready: true, expectedLobbyRevision: 2 }); h.time(125); h.owner.pump(); assert.equal(h.sockets[0].sent.at(-1).type, 'ready');
    h.owner.command('start', { expectedLobbyRevision: 2 }); h.time(250); h.owner.pump(); assert.equal(h.sockets[0].sent.at(-1).type, 'start');
    h.state.galaxy = { stage: 'awaitingAssets', lobbyRevision: 2, startPending: true, worldFingerprint: 'world-one' }; h.state.sequence = 3;
    h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    h.owner.setProfile('profile', 'digest');
    const version = h.owner.candidateVersion;
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'world-one', version), true);
    h.time(375); h.owner.pump();
    const firstAck = h.owner.assetCommandId;
    assert.deepEqual(h.sockets[0].sent.at(-1), { commandId: firstAck, type: 'assetReady', profileId: 'profile', profileDigest: 'digest', galaxyWorldFingerprint: 'world-one', ready: true });
    h.sockets[0].emit('message', { type: 'result', result: { commandId: firstAck, accepted: true } });
    assert.equal(h.owner.ready(), true);
    h.owner.invalidateGalaxyCandidate(); assert.equal(h.owner.ready(), false); assert.equal(h.owner.candidate, null);
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'world-one', version), false);
    h.state.sequence = 4; h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'world-one'), true);
    h.time(500); h.owner.pump();
    const secondAck = h.owner.assetCommandId;
    assert.notEqual(secondAck, firstAck);
    assert.equal(h.sockets[0].sent.at(-1).galaxyWorldFingerprint, 'world-one');
    h.owner.dispose();
});

test('Host lobby governance is transportable before graphics assets are confirmed', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, worldFingerprint: null }; h.sync();
    assert.equal(h.owner.ready(), false);
    h.owner.command('approveObserver', { targetParticipantId: 3 }); h.owner.pump();
    assert.equal(h.sockets[0].sent.at(-1).type, 'approveObserver');
    h.owner.command('transferHost', { targetParticipantId: 2 }); h.time(125); h.owner.pump();
    assert.equal(h.sockets[0].sent.at(-1).type, 'transferHost');
    h.owner.command('kickParticipant', { targetParticipantId: 2 }); h.time(250); h.owner.pump();
    assert.equal(h.sockets[0].sent.at(-1).type, 'kickParticipant');
    h.owner.dispose();
});

test('Pending Ready survives a same-revision snapshot and is discarded when lobby revision changes', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, worldFingerprint: null }; h.sync();
    const id = h.owner.command('ready', { ready: true, expectedLobbyRevision: 1 });
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 2, galaxy: { ...h.state.galaxy } } });
    assert.equal(h.owner.pending.has(id), true);
    h.owner.pump(); assert.equal(h.sockets[0].sent.at(-1).type, 'ready');
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 3, galaxy: { ...h.state.galaxy, lobbyRevision: 2 } } });
    assert.equal(h.owner.pending.has(id), false);
    assert.throws(() => h.owner.command('start', { expectedLobbyRevision: 1 }), /Версия настроек лобби/);
    h.owner.dispose();
});

test('Первый Start не теряется, если снимок той же версии пришёл до отправки', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, worldFingerprint: null }; h.sync();
    const id = h.owner.command('start', { expectedLobbyRevision: 1 });
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 2, galaxy: { ...h.state.galaxy } } });
    assert.equal(h.owner.pending.has(id), true);
    h.owner.pump(); assert.deepEqual(h.sockets[0].sent.at(-1), { commandId: id, type: 'start', expectedLobbyRevision: 1 });
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 3, galaxy: { ...h.state.galaxy, lobbyRevision: 2 } } });
    assert.equal(h.owner.pending.has(id), false);
    h.owner.dispose();
});

test('Galaxy connection rejects stale lobby revision and lobby disconnect releases credentials', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.participants = [{ id: 1, assetReady: false }];
    h.state.galaxy = { stage: 'awaitingAssets', lobbyRevision: 2, worldFingerprint: 'world-one' }; h.sync();
    h.owner.setProfile('profile', 'digest'); h.owner.setGalaxyCandidate('profile', 'digest', 'world-one');
    h.owner.pump(); const oldAck = h.owner.assetCommandId;
    h.state.sequence = 2; h.state.galaxy = { stage: 'awaitingAssets', lobbyRevision: 2, worldFingerprint: 'world-two' };
    h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    h.sockets[0].emit('message', { type: 'result', result: { commandId: oldAck, accepted: true } });
    assert.equal(h.owner.ready(), false);
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'world-one'), false);
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'world-two'), true);
    assert.throws(() => h.owner.command('ready', { ready: true, expectedLobbyRevision: 1 }), /Версия настроек лобби/);
    h.sockets[0].emit('close'); assert.equal(h.owner.candidate, null);
    h.time(1000); h.owner.pump();
    assert.equal(h.owner.ready(), false);
    assert.equal(h.sockets.length, 1);
    assert.equal(h.owner.login.token, undefined);
    assert.equal(h.owner.sessionId, null);
    h.owner.dispose();
});

test('Исключение до первого snapshot очищает токен и очередь без повторного входа', () =>
{
    const h = harness(); h.sockets[0].emit('open'); h.sockets[0].emit('message', h.welcome);
    let left = 0; h.owner.onLobbyLeft = () => left++;
    h.owner.pending.set('old', { type: 'transferHost', commandId: 'old' });
    h.sockets[0].emit('message', { type: 'error', code: 'LOBBY_REMOVED', message: 'Хост исключил вас из лобби.' });
    assert.equal(left, 1); assert.equal(h.owner.login.token, undefined);
    assert.equal(h.owner.pending.size, 0); assert.equal(h.owner.retryAt, null);
    assert.equal(h.owner.stopped, true); assert.equal(h.owner.socket, null);
    h.time(10000); h.owner.pump(); assert.equal(h.sockets.length, 1);
    assert.ok(h.events.some(event => /Хост исключил/.test(event[0])));
});

test('Обрыв до первого snapshot и отказ старому токену освобождают новый вход', () =>
{
    const h = harness(); h.sockets[0].emit('open'); h.sockets[0].emit('message', h.welcome);
    let left = 0; h.owner.onLobbyLeft = () => left++;
    h.owner.pending.set('old', { commandId: 'old', type: 'ready', ready: true });
    h.sockets[0].emit('close');
    assert.equal(h.owner.login.token, 'token'); assert.equal(h.owner.retryAt, 1000);
    h.time(1000); h.owner.pump(); h.sockets[1].emit('open');
    assert.equal(h.sockets[1].sent[0].token, 'token');
    h.sockets[1].emit('message', { type: 'error', message: 'Вход отклонён.' });
    assert.equal(left, 1); assert.equal(h.owner.login.token, undefined);
    assert.equal(h.owner.login.sessionId, undefined); assert.equal(h.owner.pending.size, 0);
    assert.equal(h.owner.retryAt, null);
    h.owner.connect(h.owner.login); h.sockets[2].emit('open');
    assert.equal(h.sockets[2].sent[0].token, undefined);
    h.owner.dispose();
});

test('Выход из лобби освобождает вход, выход из игры сохраняет reconnect', () =>
{
    for (const phase of ['lobby', 'running'])
    {
        const h = harness(); h.state.phase = phase; h.ready();
        let left = 0; h.owner.onLobbyLeft = () => left++;
        h.sockets[0].emit('close');
        assert.equal(left, phase === 'lobby' ? 1 : 0);
        assert.equal(h.owner.login.token, phase === 'lobby' ? undefined : 'token');
        assert.equal(h.owner.retryAt, phase === 'lobby' ? null : 1000);
        h.owner.dispose();
    }
});

test('A new server snapshot revokes an ACK when another prepare keeps the same world hash', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.participants = [{ id: 1, assetReady: false }];
    h.state.galaxy = { stage: 'awaitingAssets', worldFingerprint: 'same-world' }; h.sync();
    h.owner.setProfile('profile', 'digest'); h.owner.setGalaxyCandidate('profile', 'digest', 'same-world');
    h.owner.pump(); h.sockets[0].emit('message', { type: 'result', result: { commandId: h.owner.assetCommandId, accepted: true } });
    assert.equal(h.owner.ready(), true);
    h.state.sequence = 2; h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    assert.equal(h.owner.ready(), false); assert.equal(h.owner.candidate, null);
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'same-world'), true);
    h.time(125); h.owner.pump(); assert.equal(h.sockets[0].sent.at(-1).type, 'assetReady');
    h.owner.dispose();
});

test('Принятый ACK сохраняется при позднем снимке, созданном до подтверждения файлов', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.participants = [{ id: 1, assetReady: false }];
    h.state.galaxy = { stage: 'awaitingAssets', worldFingerprint: 'same-world' }; h.sync();
    h.owner.setProfile('profile', 'digest'); h.owner.setGalaxyCandidate('profile', 'digest', 'same-world');
    h.owner.pump();
    h.sockets[0].emit('message', { type: 'result', result: { commandId: h.owner.assetCommandId, accepted: true, stateSequence: 3 } });
    const version = h.owner.candidateVersion;
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 2 } });
    assert.equal(h.owner.ready(), true); assert.equal(h.owner.candidateVersion, version);
    assert.equal(h.owner.candidate.galaxyWorldFingerprint, 'same-world');
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 3, participants: [{ id: 1, assetReady: true }] } });
    assert.equal(h.owner.ready(), true); assert.equal(h.owner.assetCommandId, null);
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 4 } });
    assert.equal(h.owner.ready(), false); assert.equal(h.owner.candidate, null);
    h.owner.dispose();
});

test('Снимок самой версии ACK с неподтверждёнными файлами отзывает candidate', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.participants = [{ id: 1, assetReady: false }];
    h.state.galaxy = { stage: 'awaitingAssets', worldFingerprint: 'same-world' }; h.sync();
    h.owner.setProfile('profile', 'digest'); h.owner.setGalaxyCandidate('profile', 'digest', 'same-world'); h.owner.pump();
    h.sockets[0].emit('message', { type: 'result', result: { commandId: h.owner.assetCommandId, accepted: true, stateSequence: 3 } });
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 3 } });
    assert.equal(h.owner.ready(), false); assert.equal(h.owner.candidate, null); h.owner.dispose();
});

test('Galaxy logout clears the old sequence floor before a new session candidate', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.sequence = 100; h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, worldFingerprint: null }; h.sync();
    h.owner.command('start', { expectedLobbyRevision: 1 }); assert.equal(h.owner.candidateMinSequence, 101);
    h.owner.dispose(); assert.equal(h.owner.galaxyMode, false); assert.equal(h.owner.candidateMinSequence, 0);
    h.welcome.sessionId = 'new-session'; h.state.sessionId = 'new-session'; h.state.sequence = 1; h.state.galaxy = { stage: 'awaitingAssets', lobbyRevision: 1, worldFingerprint: 'new-world' };
    h.owner.connect({ name: 'A', role: 'player', joinCode: 'test-code' }); h.sync();
    h.owner.setProfile('profile', 'digest');
    assert.equal(h.owner.setGalaxyCandidate('profile', 'digest', 'new-world'), true);
    h.owner.dispose();
});

test('Explicit same-token connect never replays an earlier Galaxy Start command', () =>
{
    const h = harness(); h.state.phase = 'lobby'; h.state.galaxy = { stage: 'unprepared', lobbyRevision: 1, worldFingerprint: null }; h.sync();
    const oldId = h.owner.command('start', { expectedLobbyRevision: 1 });
    h.owner.connect({ ...h.owner.login });
    assert.equal(h.owner.pending.has(oldId), false);
    h.sync(); h.owner.pump();
    assert.equal(h.sockets.at(-1).sent.some(command => command.type === 'start'), false);
    h.owner.dispose();
});

test('PERF diagnostic validates bounded scalar values and measured ratio without coercion', () =>
{
    for (const value of [rate(), rate(0, { automaticDaysPerRealSecond: 0, completedAutomaticDays: 0 }), rate(1, { automaticDaysPerRealSecond: 51.2, completedAutomaticDays: 512, windowSeconds: 10 }), ...['warming-up', 'paused', 'not-running', 'unavailable'].map(status => rate(1, { status, automaticDaysPerRealSecond: null })), rate(null, { status: 'unavailable', automaticDaysPerRealSecond: null })])
    {
        assert.deepEqual(validateSimulationRate(value), value);
        assert.equal(Object.isFrozen(validateSimulationRate(value)), true);
    }
    for (const value of [null, [], {}, rate(1, { sampledAtMilliseconds: undefined }), rate(-1), rate(Infinity), rate(null), rate(1, { extra: true }), rate(1, { status: 'unknown' }), rate(1, { completedAutomaticDays: 513 }), rate(1, { completedAutomaticDays: -1 }), rate(1, { completedAutomaticDays: 1.5 }), rate(1, { windowSeconds: 1.99 }), rate(1, { windowSeconds: 10.01 }), rate(1, { windowSeconds: NaN }), rate(1, { automaticDaysPerRealSecond: Infinity }), rate(1, { automaticDaysPerRealSecond: '2' }), rate(1, { automaticDaysPerRealSecond: -1 }), rate(1, { automaticDaysPerRealSecond: 2.01 }), rate(1, { status: 'paused' })])
    {
        assert.equal(validateSimulationRate(value), null, JSON.stringify(value));
    }
});

test('PERF same-sequence diagnostic updates preserve world identity, interpolation, pending and readiness', () =>
{
    const h = harness(); h.sync(); h.owner.setProfile('profile', 'digest'); h.owner.pump();
    const assetId = h.owner.assetCommandId; const world = h.owner.state;
    const snapshots = () => h.events.filter(event => event[0]?.protocol === 2).length;
    const before = snapshots(); const sent = h.sockets[0].sent.length;
    h.time(500); h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, simulationRate: rate(500) } });
    assert.equal(h.owner.state, world); assert.equal(snapshots(), before);
    assert.equal(h.owner.assetCommandId, assetId); assert.equal(h.owner.assetReady, false); assert.equal(h.owner.ready(), false);
    assert.equal(h.sockets[0].sent.length, sent); assert.equal(h.owner.simulationRateReceivedAt, 500);
    h.sockets[0].emit('message', { type: 'result', result: { commandId: assetId, accepted: true } });
    const id = h.owner.command('stepDay');
    h.time(600); h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, simulationRate: rate(600) } });
    assert.ok(h.owner.pending.has(id)); assert.equal(h.owner.state, world); assert.equal(snapshots(), before);
    assert.equal(h.owner.ready(), true); assert.equal(h.owner.simulationRate.sampledAtMilliseconds, 600);
    h.owner.dispose();
});

test('PERF rejects lower world sequence and older/equal/invalid/missing samples without renewing age', () =>
{
    const h = harness(); h.ready();
    h.time(100); h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, simulationRate: rate(100) } });
    const first = h.owner.simulationRate;
    h.time(2099);
    for (const state of [{ ...h.state, sequence: 0, simulationRate: rate(900) }, { ...h.state, simulationRate: rate(99) }, { ...h.state, simulationRate: rate(100) }, { ...h.state, simulationRate: rate(200, { automaticDaysPerRealSecond: 999 }) }, { ...h.state }, { ...h.state, simulationRate: rate(null, { status: 'unavailable', automaticDaysPerRealSecond: null }) }])
    {
        h.sockets[0].emit('message', { type: 'snapshot', state });
        assert.equal(h.owner.simulationRate, first); assert.equal(h.owner.simulationRateReceivedAt, 100);
    }
    assert.equal(h.owner.simulationDiagnostic().stale, false);
    h.time(2100); assert.equal(h.owner.simulationDiagnostic().stale, true);
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, simulationRate: rate(201, { automaticDaysPerRealSecond: 0, completedAutomaticDays: 0 }) } });
    assert.equal(h.owner.simulationDiagnostic().stale, false); assert.equal(h.owner.simulationRate.automaticDaysPerRealSecond, 0);
    h.owner.dispose();
});

test('PERF missing and malformed diagnostics remain isolated from valid new world snapshots', () =>
{
    const h = harness(); h.sync(); assert.equal(h.owner.simulationDiagnostic().value, null);
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 2, simulationRate: { status: 'broken' } } });
    assert.equal(h.owner.sequence, 2); assert.equal(h.owner.state.sequence, 2); assert.equal(h.owner.stopped, false);
    assert.equal(h.owner.simulationRate.status, 'unavailable'); assert.equal(h.owner.simulationRateReceivedAt, null);
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 2, simulationRate: rate(null, { status: 'unavailable', automaticDaysPerRealSecond: null }) } });
    assert.equal(h.owner.simulationRateReceivedAt, null); assert.equal(h.owner.simulationDiagnostic().stale, false);
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 2, simulationRate: rate(1) } });
    assert.equal(h.owner.simulationRate.sampledAtMilliseconds, 1);
    h.owner.dispose();
});

test('PERF connection loss marks stale, reconnect resets sample ordering and keeps world validation', () =>
{
    const h = harness(); h.ready();
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, simulationRate: rate(5000) } });
    h.sockets[0].emit('close'); assert.equal(h.owner.simulationDiagnostic().stale, true);
    h.time(1000); h.owner.pump(); assert.equal(h.owner.simulationRate, null); assert.equal(h.owner.simulationRateReceivedAt, null);
    h.sync(); h.sockets[1].emit('message', { type: 'snapshot', state: { ...h.state, simulationRate: rate(1) } });
    assert.equal(h.owner.simulationRate.sampledAtMilliseconds, 1);
    h.sockets[1].emit('message', { type: 'snapshot', state: { ...h.state, sessionId: 'foreign', simulationRate: rate(9999) } });
    assert.equal(h.owner.stopped, true); assert.equal(h.owner.simulationRate.sampledAtMilliseconds, 1); assert.equal(h.owner.simulationDiagnostic().stale, true);
    h.owner.dispose();
    assert.equal(h.owner.simulationRate, null); assert.equal(h.owner.simulationRateReceivedAt, null);
});

test('M3: snapshot+импорт+новый ACK обязательны; не более8 отправок/сек', () =>
{
    const h = harness(); h.sync(); assert.throws(() => h.owner.command('start'));
    h.owner.setProfile('profile', 'digest'); h.owner.pump(); assert.equal(h.owner.ready(), false);
    const id = h.owner.assetCommandId; assert.equal(h.sockets[0].sent.at(-1).type, 'assetReady');
    h.sockets[0].emit('message', { type: 'result', result: { commandId: id, accepted: true } });
    assert.equal(h.owner.ready(), true);
    h.owner.command('stepDay'); h.owner.pump(); assert.equal(h.sockets[0].sent.length, 2);
    h.time(125); h.owner.pump(); assert.equal(h.sockets[0].sent.at(-1).type, 'stepDay');
    h.owner.dispose();
});

test('M3: timeout безclose, старые callbacks, reconnect readiness раньше replay', () =>
{
    const h = harness(); h.ready(); const commandId = h.owner.command('stepDay'); h.time(125); h.owner.pump();
    const old = h.sockets[0]; h.time(30001); h.owner.pump(); assert.equal(h.owner.ready(), false);
    h.time(31001); h.owner.pump(); h.sync();
    old.emit('message', { type: 'result', result: { commandId, accepted: true } }); assert.ok(h.owner.pending.has(commandId));
    h.owner.pump(); const newId = h.owner.assetCommandId;
    assert.equal(h.sockets[1].sent.at(-1).type, 'assetReady');
    h.sockets[1].emit('message', { type: 'result', result: { commandId: newId, accepted: true } });
    h.time(31126); h.owner.pump(); assert.equal(h.sockets[1].sent.at(-1).commandId, commandId);
    h.sockets[1].emit('message', { type: 'result', result: { commandId, accepted: true } }); assert.equal(h.owner.pending.size, 0);
    h.owner.dispose();
});

test('M3: timeout reconnect refusal stops retries; manual return requires fresh asset ACK before same-ID replay', () =>
{
    const h = harness(); h.state.phase = 'running'; h.ready();
    const commandId = h.owner.command('stepDay'); h.time(125); h.owner.pump();
    const originalAssetId = h.sockets[0].sent.find(message => message.type === 'assetReady').commandId;
    const old = h.sockets[0];
    h.time(10000); h.owner.pump(); assert.equal(old.sent.at(-1).type, 'ping');
    h.time(20000); h.owner.pump(); assert.equal(old.sent.at(-1).type, 'ping');
    assert.ok(h.owner.pending.has(commandId));
    assert.equal(old.sent.filter(message => message.commandId === commandId).length, 1);

    h.time(30000); h.owner.pump();
    assert.equal(h.owner.socket, null); assert.equal(h.owner.fresh, false); assert.equal(h.owner.ready(), false);
    assert.ok(h.owner.pending.has(commandId)); assert.equal(h.owner.retryAt, 31000);
    h.time(30999); h.owner.pump(); assert.equal(h.sockets.length, 1);
    h.time(31000); h.owner.pump(); assert.equal(h.sockets.length, 2);
    const refused = h.sockets[1]; refused.emit('open');
    assert.deepEqual(refused.sent.at(-1), { type: 'join', protocol: 2, name: 'A', role: 'player', joinCode: 'test-code', token: 'token' });
    refused.emit('message', { type: 'error', message: 'Вход отклонён: проверьте protocol=2, код доступа и данные участника.' });
    assert.equal(h.owner.stopped, true); assert.equal(h.owner.retryAt, null);
    assert.equal(h.owner.socket, null); assert.equal(h.owner.fresh, false); assert.equal(h.owner.ready(), false);
    assert.ok(h.owner.pending.has(commandId));
    assert.ok(h.events.some(event => event[0] === 'Вход отклонён: проверьте protocol=2, код доступа и данные участника.' && event[1] === false));
    assert.equal(refused.sent.some(message => message.type === 'stepDay' || message.type === 'assetReady'), false);

    old.emit('message', { type: 'result', result: { commandId, accepted: true } });
    refused.emit('close');
    h.time(100000); h.owner.pump(); assert.equal(h.sockets.length, 2);
    assert.ok(h.owner.pending.has(commandId));

    h.owner.connect({ ...h.owner.login }); h.sync(); h.owner.pump();
    assert.equal(h.sockets.length, 3); assert.equal(h.owner.fresh, true); assert.equal(h.owner.ready(), false);
    const newAssetId = h.owner.assetCommandId;
    assert.notEqual(newAssetId, null); assert.notEqual(newAssetId, originalAssetId);
    assert.equal(h.sockets[2].sent.at(-1).type, 'assetReady');
    assert.ok(h.owner.pending.has(commandId));
    assert.equal(h.sockets[2].sent.some(message => message.commandId === commandId), false);
    h.sockets[2].emit('message', { type: 'result', result: { commandId: newAssetId, accepted: true } });
    h.time(100125); h.owner.pump(); assert.equal(h.sockets[2].sent.at(-1).commandId, commandId);
    h.sockets[2].emit('message', { type: 'result', result: { commandId, accepted: true } });
    assert.equal(h.owner.pending.has(commandId), false);
    h.owner.dispose();
});

test('M3: pong/тот жеsnapshot продлевают pause liveness, чужаяsession прекращает reconnect', () =>
{
    const h = harness(); h.ready(); h.time(29000); h.sockets[0].emit('message', { type: 'pong' });
    h.time(58000); h.sockets[0].emit('message', { type: 'snapshot', state: h.state }); assert.equal(h.owner.ready(), true);
    h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sessionId: 'foreign' } }); assert.equal(h.owner.socket, null); assert.equal(h.owner.retryAt, null);
    h.owner.dispose();
});

test('M3: queue100, mismatch и отключение исключают поздние команды', () =>
{
    const h = harness(); h.ready(); for (let i = 0; i < 100; i++) { h.owner.command('stepDay'); }
    assert.throws(() => h.owner.command('stepDay'), /100/);
    const count = h.sockets[0].sent.length; h.owner.disconnect(); h.time(100000); h.owner.pump(); assert.equal(h.sockets[0].sent.length, count);
    const other = harness(); other.sync(); other.owner.setProfile('wrong', 'digest'); other.owner.pump(); assert.equal(other.owner.ready(), false); assert.equal(other.sockets[0].sent.length, 1); other.owner.dispose();
});
test('M3: запрос fresh snapshot после context restore принимает тот же sequence, не более старый', () =>
{
    const h = harness(); h.ready(); const count = h.events.filter(event => event[0]?.protocol === 2).length;
    h.owner.requestSnapshotRefresh(); h.sockets[0].emit('message', { type: 'snapshot', state: { ...h.state, sequence: 0 } });
    assert.equal(h.owner.forceFreshSnapshot, true);
    h.sockets[0].emit('message', { type: 'snapshot', state: h.state });
    assert.equal(h.owner.forceFreshSnapshot, false); assert.equal(h.events.filter(event => event[0]?.protocol === 2).length, count + 1);
    h.owner.dispose();
});

test('M3: dispose очищает профиль; новый вход требует новый импорт', () =>
{
    const h = harness(); h.ready(); h.owner.dispose();
    assert.equal(h.owner.profile, null);
    assert.equal(h.owner.assetReady, false);
    h.owner.connect({ name: 'B', role: 'player', joinCode: 'new-code' }); h.sync(); h.owner.pump();
    assert.equal(h.owner.assetCommandId, null);
    assert.equal(h.owner.ready(), false);
    assert.equal(h.sockets.at(-1).sent.length, 1);
    h.owner.dispose();
});

test('M3: смена token/session identity не наследует подтверждённый профиль', () =>
{
    const h = harness(); h.ready();
    h.owner.connect({ name: 'B', role: 'player', joinCode: 'new-code', token: 'other-token', sessionId: 'session' });
    h.sync(); h.owner.pump();
    assert.equal(h.owner.profile, null);
    assert.equal(h.owner.assetCommandId, null);
    assert.equal(h.owner.ready(), false);
    h.owner.dispose();
});

test('M3: импорт до первоначального входа сохраняется для первого welcome', () =>
{
    const h = harness(); h.owner.dispose();
    h.owner.setProfile('profile', 'digest');
    h.owner.connect({ name: 'A', role: 'player', joinCode: 'code' }); h.sync(); h.owner.pump();
    assert.equal(h.sockets.at(-1).sent.at(-1).type, 'assetReady');
    h.owner.dispose();
});

test('M3: background без pump не принимает команды после liveness30с и ready не имеет side effects', () =>
{
    const h = harness(); h.ready(); const oldSocket = h.owner.socket; const eventCount = h.events.length;
    h.time(30000);
    assert.equal(h.owner.ready(), false); assert.equal(h.owner.ready(), false);
    assert.throws(() => h.owner.command('stepDay'));
    assert.equal(h.owner.pending.size, 0);
    assert.equal(h.owner.socket, oldSocket); assert.equal(h.events.length, eventCount);
    h.owner.pump(); assert.equal(h.owner.socket, null);
    h.owner.dispose();
});

test('M3: reconnect полной gameplay queue не добавляет101-й pending и подтверждает assets раньше replay', () =>
{
    const h = harness(); h.ready();
    for (let i = 0; i < 100; i++) { h.owner.command('stepDay'); }
    h.time(30000); h.owner.pump(); h.time(31000); h.owner.pump(); h.sync(); h.owner.pump();
    assert.equal(h.owner.pending.size, 100);
    assert.equal(h.sockets.at(-1).sent.at(-1).type, 'assetReady');
    assert.ok([...h.owner.pending.values()].every(command => command.type !== 'assetReady'));
    h.sockets.at(-1).emit('message', { type: 'result', result: { commandId: h.owner.assetCommandId, accepted: true } });
    h.time(31125); h.owner.pump();
    assert.equal(h.sockets.at(-1).sent.at(-1).type, 'stepDay');
    assert.equal(h.owner.pending.size, 100);
    h.owner.dispose();
});

test('M3: default browser timers сохраняют globalThis receiver без injection', () =>
{
    const originalSet = globalThis.setInterval;
    const originalClear = globalThis.clearInterval;
    const calls = [];
    let owner;
    try
    {
        globalThis.setInterval = function(callback, milliseconds)
        {
            assert.equal(this, globalThis, 'Window.setInterval требует корректный receiver.');
            assert.equal(typeof callback, 'function'); assert.equal(milliseconds, 125);
            calls.push('set'); return 47;
        };
        globalThis.clearInterval = function(id)
        {
            assert.equal(this, globalThis, 'Window.clearInterval требует корректный receiver.');
            assert.equal(id, 47); calls.push('clear');
        };
        owner = new M3Connection({ url: 'ws://local/ws', socketFactory: () => ({ addEventListener() {}, close() {} }) });
        owner.connect({ name: 'A', role: 'player', joinCode: 'code' });
        owner.disconnect(); owner.dispose();
        assert.deepEqual(calls, ['set', 'clear']);
    }
    finally
    {
        owner?.dispose();
        globalThis.setInterval = originalSet;
        globalThis.clearInterval = originalClear;
    }
});
