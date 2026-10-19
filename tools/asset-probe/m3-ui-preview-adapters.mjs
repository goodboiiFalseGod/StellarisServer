// Synthetic boundaries for the local UI preview only. Never imported by the game.
export const PROFILE_PATHS = [];
export const validateM3Manifest = () => { throw new Error('Импорт недоступен в DOM-макете.'); };
export const preparePlanetImages = () => { throw new Error('Planet image import is unavailable in the synthetic DOM preview.'); };
export class M3Importer { cancel() {} }
const systems = Array.from({ length: 16 }, (_, index) => ({ id: `s${index}`, name: `Система ${index + 1}` }));
const snapshot = {
    you: 1, viewContext: { mode: 'player', participantId: 1 }, phase: 'running', paused: true, speedDaysPerSecond: 1, month: 0, date: { year: 2200, month: 1, day: 1 },
    participants: [{ id: 1, name: 'DOM fixture', role: 'player', isHost: true, connected: true, ready: true, assetReady: true }, { id: 2, name: 'Other fixture', role: 'player', connected: false }, { id: 3, name: 'Observer fixture', role: 'observer', connected: true, assetReady: true }],
    catalog: { traits: [], buildings: [], jobs: [] },
    planets: [{ id: 'p1', ownerId: 1, name: 'Синтетическая колония', population: 10, workforce: 5, jobs: {}, balances: { energy: 1000, minerals: 750, food: 200 }, report: null }, { id: 'p2', ownerId: 2, name: 'Вторая синтетическая колония', population: 10, workforce: 5, jobs: {}, balances: { energy: 2000, minerals: 80, food: 900 }, report: null }],
    spatial: { systems, bodies: [{ id: 'b1', systemId: 's0', name: 'Синтетическая колония', kind: 'planet', ownerParticipantId: 1 }], colonies: [{ id: 'c1', bodyId: 'b1', ownerParticipantId: 1 }], fleets: [
        { id: 'f1', name: 'Собственный флот', ownerParticipantId: 1, shipCount: 3, phase: 'idle', location: { kind: 'system', systemId: 's0' }, movement: null },
        { id: 'f2', name: 'Чужой флот', ownerParticipantId: 2, shipCount: 2, phase: 'idle', location: { kind: 'system', systemId: 's1' }, movement: null }
    ] }
};
const observer = new URLSearchParams(location.search).has('observer');
function projected(target = observer ? 0 : 1)
{
    const state = structuredClone(snapshot);
    state.you = observer ? 3 : 1;
    state.viewContext = { mode: target ? 'player' : 'general', participantId: target || null };
    state.planets = state.planets.filter(planet => !target || planet.ownerId === target);
    return state;
}
export class M3Connection
{
    pending = new Map();
    constructor(callbacks) { this.callbacks = callbacks; this.state = projected(); queueMicrotask(() => { callbacks.onStatus('DOM fixture · связь имитируется', true); callbacks.onState(this.state, {}); }); }
    ready() { return true; }
    disconnect() { this.callbacks.onStatus('DOM fixture · отключено', false); }
    connect() { this.callbacks.onStatus('DOM fixture · связь имитируется', true); this.callbacks.onState(this.state, {}); }
    dispose() { this.disconnect(); }
    command(type, fields)
    {
        const commandId = crypto.randomUUID(); this.pending.set(commandId, { type });
        setTimeout(() =>
        {
            this.pending.delete(commandId);
            const accepted = observer && type === 'observerView' && [0, 1, 2].includes(fields?.targetParticipantId);
            this.callbacks.onResult({ commandId, accepted, code: 'synthetic', message: accepted ? 'Синтетическое переключение подтверждено.' : 'Синтетический отказ для проверки обратной связи.' });
            if (accepted) { this.state = projected(fields.targetParticipantId); this.callbacks.onState(this.state, {}); }
        }, 800); return commandId;
    }
}
export class M3WorldRenderer
{
    cameraPreset = 'galaxy-far';
    setCameraPreset(id) { this.cameraPreset = id; }
    resize() {} applySnapshot() {} setConnectionFresh() {} select() {} setCommandFleet() {} showSystem() {} showGalaxy() {} showStress200() {} resetCamera() {} focusSelection() {} render() {} dispose() {}
    stats() { return { synthetic: true, convertedBytes: 0, cameraPreset: this.cameraPreset }; }
}
