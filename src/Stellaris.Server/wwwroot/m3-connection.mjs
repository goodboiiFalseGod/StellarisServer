import { hydrateResearchTables } from './m3-research-tables.mjs';

// Connection owner. Asset bytes are never stored or transmitted here.
export function validateSimulationRate(value)
{
    const fields = ['status', 'automaticDaysPerRealSecond', 'completedAutomaticDays', 'windowSeconds', 'sampledAtMilliseconds'];
    if (!value || typeof value !== 'object' || Array.isArray(value) || Object.keys(value).length !== fields.length || !fields.every(field => Object.hasOwn(value, field))) { return null; }
    const { status, automaticDaysPerRealSecond: rate, completedAutomaticDays: count, windowSeconds: window, sampledAtMilliseconds: sampledAt } = value;
    if (!['measured', 'warming-up', 'paused', 'not-running', 'unavailable'].includes(status) || !Number.isInteger(count) || count < 0 || count > 512 || !Number.isFinite(window) || window < 0 || window > 10) { return null; }
    if (sampledAt === null ? status !== 'unavailable' : !Number.isFinite(sampledAt) || sampledAt < 0) { return null; }
    if (status === 'measured')
    {
        if (window < 2 || !Number.isFinite(rate) || rate < 0 || Math.abs(rate - count / window) > 1e-9 * Math.max(1, Math.abs(rate), count / window)) { return null; }
    }
    else if (rate !== null) { return null; }
    return Object.freeze({ status, automaticDaysPerRealSecond: rate, completedAutomaticDays: count, windowSeconds: window, sampledAtMilliseconds: sampledAt });
}

export class M3Connection
{
    constructor({ url, onState = () => {}, onStatus = () => {}, onWelcome = () => {}, onResult = () => {}, onLobbyLeft = () => {}, now = () => performance.now(), socketFactory = address => new WebSocket(address), setInterval: interval = (...args) => globalThis.setInterval(...args), clearInterval: clear = id => globalThis.clearInterval(id) } = {})
    {
        Object.assign(this, { url, onState, onStatus, onWelcome, onResult, onLobbyLeft, now, socketFactory, interval, clear });
        this.socket = null; this.login = null; this.sessionId = null; this.sequence = -1;
        this.pending = new Map(); this.sent = new Set(); this.state = null;
        this.welcomed = false; this.fresh = false; this.assetReady = false; this.profile = null;
        this.galaxyMode = false; this.candidate = null; this.candidateVersion = 0; this.candidateMinSequence = 0; this.assetAckSequence = -1;
        this.assetCommandId = null; this.assetCommand = null; this.retries = 0; this.retryAt = null; this.stopped = true;
        this.lastResponse = 0; this.lastPing = 0; this.lastSend = -Infinity; this.timer = null;
        this.forceFreshSnapshot = false;
        this.simulationRate = null; this.simulationRateReceivedAt = null;
    }

    connect(login)
    {
        const sameSession = this.login?.token && this.login.token === login.token && this.login.sessionId === login.sessionId;
        const candidateBound = new Set(['setGalaxySettings', 'selectEmpireTemplate', 'prepareGalaxy', 'configure', 'ready', 'start', 'inspectGalaxySystem']);
        const retained = (this.login?.token && this.login.token === login.token ? [...this.pending.values()] : login.pending ?? []).filter(command => !this.galaxyMode || !candidateBound.has(command?.type));
        if (this.login && !sameSession) { this.profile = null; this.galaxyMode = false; }
        this.disconnect(); this.login = { ...login }; this.sessionId = login.sessionId ?? null;
        this.candidateMinSequence = 0; this.assetAckSequence = -1;
        this.sequence = -1; this.state = null; this.pending.clear(); this.retries = 0;
        for (const command of retained.slice(0, 100)) { if (typeof command?.commandId === 'string' && command.type !== 'assetReady') { this.pending.set(command.commandId, command); } }
        this.stopped = false; this.timer = this.interval(() => this.pump(), 125); this.open();
    }

    open()
    {
        if (this.stopped) { return; }
        this.fresh = false; this.welcomed = false; this.assetReady = false; this.assetCommandId = null; this.assetCommand = null;
        this.candidate = null; ++this.candidateVersion;
        this.sent.clear(); this.retryAt = null; this.lastResponse = this.now(); this.lastPing = this.now();
        this.simulationRate = null; this.simulationRateReceivedAt = null;
        const socket = this.socketFactory(this.url); this.socket = socket;
        this.onStatus('Подключение…', false);
        socket.addEventListener('open', () =>
        {
            if (this.socket !== socket || !this.alive()) { return; }
            socket.send(JSON.stringify({ type: 'join', protocol: 2, name: this.login.name, role: this.login.role, joinCode: this.login.joinCode, ...(this.login.token ? { token: this.login.token } : {}) }));
        });
        socket.addEventListener('message', event =>
        {
            if (this.socket !== socket || !this.alive()) { return; }
            try
            {
                if (typeof event.data !== 'string' || new TextEncoder().encode(event.data).byteLength > 1024 * 1024) { throw new Error('Сообщение превышает лимит.'); }
                const message = JSON.parse(event.data);
                this.receive(message); this.lastResponse = this.now();
            }
            catch (error) { this.stopped = true; this.lose(`Ошибка протокола: ${error.message}`); }
        });
        socket.addEventListener('close', () => { if (this.socket === socket) { this.lose('Связь потеряна.'); } });
        socket.addEventListener('error', () => { if (this.socket === socket) { this.lose('Ошибка соединения.'); } });
    }

    receive(message)
    {
        switch (message.type)
        {
            case 'welcome':
                if (this.welcomed || message.protocol !== 2 || !Number.isInteger(message.participantId) || !message.token || !message.sessionId || !message.profileId || !message.profileDigest) { throw new Error('Некорректный welcome.'); }
                if (this.sessionId && this.sessionId !== message.sessionId) { throw new Error('Сессия перезапущена; очистите вход и импортируйте набор заново.'); }
                this.sessionId = message.sessionId; this.login.token = message.token; this.login.sessionId = message.sessionId;
                this.welcome = message; this.sequence = -1; this.welcomed = true; this.retries = 0;
                this.onWelcome(message, { ...this.login });
                break;
            case 'snapshot':
                message = { ...message, state: hydrateResearchTables(message.state) };
                if (!this.welcomed || message.state?.protocol !== 2 || message.state.sessionId !== this.sessionId || !Number.isSafeInteger(message.state.sequence) || message.state.sequence < 0 || message.state.you !== this.welcome.participantId) { throw new Error('Некорректный snapshot или чужая сессия.'); }
                if (message.state.sequence > this.sequence || this.forceFreshSnapshot && message.state.sequence === this.sequence)
                {
                    const reset = !this.fresh || this.forceFreshSnapshot; this.forceFreshSnapshot = false;
                    this.syncGalaxy(message.state); this.state = message.state; this.sequence = message.state.sequence;
                    this.fresh = true; this.onState(this.state, { resetInterpolation: reset, receivedAt: this.now() });
                }
                if (message.state.sequence === this.sequence) { this.acceptSimulationRate(message.state.simulationRate); }
                this.confirmProfile(); this.onStatus('Подключено', this.ready());
                break;
            case 'result':
                if (!this.welcomed || typeof message.result?.commandId !== 'string' || typeof message.result.accepted !== 'boolean') { throw new Error('Некорректный result.'); }
                const resolved = this.pending.get(message.result.commandId);
                this.pending.delete(message.result.commandId); this.sent.delete(message.result.commandId);
                if (message.result.commandId === this.assetCommandId)
                {
                    this.assetReady = message.result.accepted; this.assetCommandId = null; this.assetCommand = null;
                    if (this.assetReady)
                    {
                        if (message.result.stateSequence != null && (!Number.isSafeInteger(message.result.stateSequence) || message.result.stateSequence < 0)) { throw new Error('Некорректная версия подтверждения ассетов.'); }
                        // Снимок, созданный до принятия ACK, может прийти после результата команды.
                        this.assetAckSequence = message.result.stateSequence ?? this.sequence + 1;
                    }
                    if (!this.assetReady && !this.galaxyMode) { this.profile = null; }
                    if (!this.assetReady && this.galaxyMode) { this.candidateMinSequence = this.sequence + 1; this.invalidateGalaxyCandidate(); }
                }
                if (!message.result.accepted && this.galaxyMode && ['setGalaxySettings', 'selectEmpireTemplate', 'prepareGalaxy'].includes(resolved?.type)) { this.candidateMinSequence = this.sequence; this.requestSnapshotRefresh(); }
                this.onResult(message.result); this.onStatus('Подключено', this.ready());
                break;
            case 'pong':
                if (!this.welcomed) { throw new Error('pong до welcome.'); }
                break;
            case 'error':
                this.stopped = true; this.lose(message.message || 'Сервер отклонил вход.', message.code === 'LOBBY_REMOVED' || !this.welcomed && this.state?.phase !== 'running');
                break;
            default: throw new Error('Неизвестное сообщение.');
        }
    }

    setProfile(profileId, profileDigest)
    {
        this.profile = { profileId, profileDigest }; this.confirmProfile();
    }
    setGalaxyCandidate(profileId, profileDigest, galaxyWorldFingerprint, expectedVersion = this.candidateVersion)
    {
        const galaxy = this.state?.galaxy;
        if (!this.galaxyMode || expectedVersion !== this.candidateVersion || !['awaitingAssets', 'running'].includes(galaxy?.stage) || this.sequence < this.candidateMinSequence || galaxy.worldFingerprint !== galaxyWorldFingerprint || !profileId || !profileDigest || !galaxyWorldFingerprint) { return false; }
        const next = { profileId, profileDigest, galaxyWorldFingerprint };
        if (JSON.stringify(this.candidate) !== JSON.stringify(next)) { this.assetReady = false; this.assetCommandId = null; this.assetCommand = null; this.candidate = next; }
        this.confirmProfile(); return true;
    }
    invalidateGalaxyCandidate()
    {
        this.candidate = null; this.assetReady = false; this.assetCommandId = null; this.assetCommand = null; this.assetAckSequence = -1; ++this.candidateVersion;
        for (const [id, command] of this.pending) { if (command.type === 'inspectGalaxySystem' || command.type === 'start' && command.galaxyWorldFingerprint) { this.pending.delete(id); this.sent.delete(id); } }
    }
    syncGalaxy(snapshot)
    {
        if (!snapshot.galaxy) { return; }
        if (!this.galaxyMode) { for (const [id, command] of this.pending) { if (['setGalaxySettings', 'selectEmpireTemplate', 'prepareGalaxy', 'configure', 'ready', 'start', 'inspectGalaxySystem'].includes(command.type)) { this.pending.delete(id); this.sent.delete(id); } } }
        this.galaxyMode = true;
        const galaxy = snapshot.galaxy;
        if (this.state?.galaxy?.lobbyRevision !== undefined && this.state.galaxy.lobbyRevision !== galaxy.lobbyRevision)
        {
            for (const [id, command] of this.pending) { if (['ready', 'start'].includes(command.type)) { this.pending.delete(id); this.sent.delete(id); } }
        }
        const oldAck = this.state?.participants?.find(participant => participant.id === this.state.you)?.assetReady === true;
        const nextAck = snapshot.participants?.find(participant => participant.id === snapshot.you)?.assetReady === true;
        if (!['awaitingAssets', 'running'].includes(galaxy.stage) || !galaxy.worldFingerprint || this.candidate && this.candidate.galaxyWorldFingerprint !== galaxy.worldFingerprint || oldAck && !nextAck || this.assetReady && snapshot.sequence >= this.assetAckSequence && !nextAck || snapshot.sequence < this.candidateMinSequence)
        { this.invalidateGalaxyCandidate(); }
    }
    requestSnapshotRefresh() { this.forceFreshSnapshot = true; }

    acceptSimulationRate(raw)
    {
        if (raw === undefined) { return; }
        const value = validateSimulationRate(raw);
        if (value?.sampledAtMilliseconds != null)
        {
            if (this.simulationRate?.sampledAtMilliseconds != null && value.sampledAtMilliseconds <= this.simulationRate.sampledAtMilliseconds) { return; }
            this.simulationRate = value; this.simulationRateReceivedAt = this.now();
        }
        else if (this.simulationRate?.sampledAtMilliseconds == null)
        {
            this.simulationRate = value ?? Object.freeze({ status: 'unavailable', automaticDaysPerRealSecond: null, completedAutomaticDays: 0, windowSeconds: 0, sampledAtMilliseconds: null });
            this.simulationRateReceivedAt = null;
        }
    }

    simulationDiagnostic()
    {
        const stale = this.simulationRateReceivedAt !== null && (!this.fresh || !this.welcomed || this.stopped || this.socket?.readyState !== 1 || this.now() - this.lastResponse >= 30000 || this.now() - this.simulationRateReceivedAt >= 2000);
        return { value: this.simulationRate, stale };
    }

    confirmProfile()
    {
        if (!this.profile || !this.fresh || !this.welcomed || this.assetReady || this.assetCommandId) { return; }
        const expected = this.galaxyMode ? this.candidate : this.welcome;
        if (!expected) { return; }
        if (this.profile.profileId !== expected.profileId || this.profile.profileDigest !== expected.profileDigest)
        {
            if (!this.galaxyMode) { this.profile = null; }
            this.onStatus('Набор не совпадает с сервером; нужен новый импорт.', false); return;
        }
        // Старые assetReady не replay: это подтверждение именно нового соединения.
        for (const [id, command] of this.pending) { if (command.type === 'assetReady') { this.pending.delete(id); this.sent.delete(id); } }
        const commandId = crypto.randomUUID(); this.assetCommandId = commandId;
        this.assetCommand = { commandId, type: 'assetReady', ...this.profile, ...(this.galaxyMode ? { galaxyWorldFingerprint: expected.galaxyWorldFingerprint } : {}), ready: true };
    }

    command(type, fields = {})
    {
        const galaxy = this.state?.galaxy;
        const lobbyCommand = ['setGalaxySettings', 'selectEmpireTemplate', 'configure', 'ready', 'approveObserver', 'transferHost', 'kickParticipant'].includes(type) || type === 'start' && galaxy?.stage === 'unprepared';
        const preAsset = this.galaxyMode && this.state?.phase === 'lobby' && lobbyCommand && this.lobbyReady();
        if (!this.ready() && !preAsset) { throw new Error('Команды недоступны: нужны связь, свежий снимок и импорт.'); }
        if (this.galaxyMode && ['ready', 'start'].includes(type) && (!Number.isSafeInteger(fields.expectedLobbyRevision) || fields.expectedLobbyRevision !== galaxy?.lobbyRevision)) { throw new Error('Версия настроек лобби изменилась. Дождитесь свежего снимка.'); }
        if (this.galaxyMode && (type === 'start' && galaxy?.stage !== 'unprepared' || type === 'inspectGalaxySystem') && (!this.candidate || fields.galaxyWorldFingerprint !== this.candidate.galaxyWorldFingerprint || fields.profileId && fields.profileId !== this.candidate.profileId || fields.profileDigest && fields.profileDigest !== this.candidate.profileDigest)) { throw new Error('Кандидат галактики устарел. Обновите профиль и подтверждение.'); }
        if (this.pending.size >= 100) { throw new Error('Очередь 100 команд заполнена.'); }
        const commandId = crypto.randomUUID();
        const command = { ...fields, type, commandId };
        if (new TextEncoder().encode(JSON.stringify(command)).byteLength > 16 * 1024) { throw new Error('Команда превышает лимит 16 KiB.'); }
        if (this.galaxyMode && ['setGalaxySettings', 'selectEmpireTemplate'].includes(type) || this.galaxyMode && type === 'start' && galaxy?.stage === 'unprepared') { this.candidateMinSequence = this.sequence + 1; this.invalidateGalaxyCandidate(); }
        this.pending.set(commandId, command);
        return commandId;
    }

    lobbyReady() { return this.fresh && this.welcomed && this.socket?.readyState === 1 && !this.stopped && this.now() - this.lastResponse < 30000; }
    ready() { return this.fresh && this.welcomed && this.assetReady && this.socket?.readyState === 1 && !this.stopped && this.now() - this.lastResponse < 30000; }
    alive()
    {
        if (this.now() - this.lastResponse < 30000) { return true; }
        this.lose('Сервер не отвечает 30 секунд. Состояние устарело.'); return false;
    }

    pump()
    {
        if (this.stopped) { return; }
        if (!this.socket)
        {
            if (this.retryAt !== null && this.now() >= this.retryAt) { this.open(); }
            return;
        }
        if (!this.alive() || !this.welcomed || this.socket?.readyState !== 1) { return; }
        if (this.now() - this.lastPing >= 10000) { this.socket.send(JSON.stringify({ type: 'ping' })); this.lastPing = this.now(); }
        if (!this.fresh || this.now() - this.lastSend < 125) { return; }
        this.confirmProfile();
        const command = this.assetCommandId && !this.sent.has(this.assetCommandId) ? this.assetCommand : [...this.pending.values()].find(value => !this.sent.has(value.commandId) && (this.assetReady || this.galaxyMode && this.state?.phase === 'lobby' && (['setGalaxySettings', 'selectEmpireTemplate', 'configure', 'ready', 'approveObserver', 'transferHost', 'kickParticipant'].includes(value.type) || value.type === 'start' && this.state.galaxy?.stage === 'unprepared')));
        if (!command) { return; }
        this.socket.send(JSON.stringify(command)); this.sent.add(command.commandId); this.lastSend = this.now();
    }

    lose(message, lobbyRemoved = false)
    {
        const socket = this.socket; this.socket = null; this.fresh = false; this.welcomed = false; this.assetReady = false; this.assetCommandId = null; this.assetCommand = null; this.candidate = null; ++this.candidateVersion;
        if (this.galaxyMode) { for (const [id, command] of this.pending) { if (['setGalaxySettings', 'selectEmpireTemplate', 'prepareGalaxy', 'configure', 'ready', 'start', 'inspectGalaxySystem'].includes(command.type)) { this.pending.delete(id); this.sent.delete(id); } } }
        if ((this.state?.phase === 'lobby' || lobbyRemoved) && this.login?.token)
        {
            this.stopped = true; this.pending.clear(); this.sent.clear();
            delete this.login.token; delete this.login.sessionId; delete this.login.pending;
            this.sessionId = null;
            this.onLobbyLeft();
            message += ' Подключитесь заново.';
        }
        socket?.close();
        this.retryAt = !this.stopped && this.login?.token ? this.now() + Math.min(1000 * 2 ** Math.min(this.retries++, 3), 8000) : null;
        this.onStatus(message + (this.retryAt !== null ? ' Переподключение…' : ''), false);
    }

    disconnect()
    {
        this.stopped = true; this.retryAt = null;
        if (this.timer !== null) { this.clear(this.timer); this.timer = null; }
        this.lose('Отключено.');
    }
    dispose() { this.disconnect(); this.pending.clear(); this.login = null; this.sessionId = null; this.state = null; this.sequence = -1; this.profile = null; this.assetReady = false; this.assetCommandId = null; this.assetCommand = null; this.galaxyMode = false; this.candidate = null; this.candidateMinSequence = 0; this.assetAckSequence = -1; ++this.candidateVersion; this.simulationRate = null; this.simulationRateReceivedAt = null; }
}
