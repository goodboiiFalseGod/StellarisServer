// Presentation only: input is the already-filtered server snapshot, never a world cache.
const number = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });
const simulationRateStatuses = { 'warming-up': 'прогрев', paused: 'пауза', 'not-running': 'не запущено', unavailable: 'недоступно' };
const phases = { idle: 'Ожидание', movingLocal: 'Движение', approachingExit: 'К гиперлинии', charging: 'Подготовка перехода', inTransit: 'Гиперпереход' };
const types = { star: 'Звезда', planet: 'Планета', moon: 'Спутник', station: 'Станция', system: 'Звёздная система', colony: 'Колония', fleet: 'Флот', gateway: 'Мегаструктура', wormhole: 'Червоточина' };
const systems = state => state?.spatial?.systems ?? [];
const systemName = (state, id) => systems(state).find(item => item.id === id)?.name ?? 'Недоступная система';
const participant = (state, id) => state?.participants?.find(item => item.id === id);
function bodyDisplayName(body, bodies = [])
{
    if (body?.name && !/^pc_[a-z0-9_]+$/.test(body.name)) { return body.name; }
    const kind = { star: 'Звезда', planet: 'Планета', moon: 'Спутник', station: 'Станция' }[body?.kind] ?? 'Объект';
    const index = bodies.filter(item => item.systemId === body?.systemId && item.kind === body?.kind).sort((a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0).findIndex(item => item.id === body?.id);
    return `${kind}${index >= 0 ? ` ${index + 1}` : ''}`;
}
const ownerName = (state, id) => id == null ? 'Без владельца' : participant(state, id)?.name ?? 'Неизвестный участник';

export function simulationRateText(diagnostic)
{
    const value = diagnostic?.value;
    if (value == null) { return 'Автоматически: нет данных'; }
    if (diagnostic.stale === true) { return 'Автоматически: устарело'; }
    if (value.status === 'measured')
    {
        return `Автоматически: ${number.format(value.automaticDaysPerRealSecond)} дн./с · завершено: ${value.completedAutomaticDays} · окно: ${number.format(value.windowSeconds)} с`;
    }
    return `Автоматически: ${simulationRateStatuses[value.status] ?? 'недоступно'}`;
}

export function decimalPrecisionWithin(value, maximumDigits)
{
    if (!Number.isFinite(value) || !Number.isSafeInteger(maximumDigits) || maximumDigits < 0) { return false; }
    const [mantissa, exponentText] = String(value).toLowerCase().split('e');
    const exponent = exponentText === undefined ? 0 : Number(exponentText);
    const fractionDigits = mantissa.split('.')[1]?.length ?? 0;
    return Number.isSafeInteger(exponent) && Math.max(0, fractionDigits - exponent) <= maximumDigits;
}

export function viewParticipantId(state)
{
    const context = state?.viewContext;
    const user = participant(state, state?.you);
    if (context?.mode !== 'player' || participant(state, context.participantId)?.role !== 'player') { return null; }
    if (user?.role === 'player' && context.participantId === state.you || user?.role === 'observer') { return context.participantId; }
    return null;
}

export function perspectiveKey(state)
{
    return JSON.stringify([state?.you ?? null, state?.viewContext?.mode ?? null, state?.viewContext?.participantId ?? null]);
}

export function observerViews(state)
{
    if (participant(state, state?.you)?.role !== 'observer' || !['general', 'player'].includes(state?.viewContext?.mode)) { return []; }
    return [[0, 'Общий обзор'], ...state.participants.filter(item => item.role === 'player').map(item => [item.id, `${item.name}${item.connected === false ? ' · offline' : ''}`])];
}

export function selectedObject(state, selection)
{
    const list = { system: 'systems', body: 'bodies', fleet: 'fleets', colony: 'colonies', gateway: 'gateways', wormhole: 'wormholes' }[selection?.kind];
    return list ? state?.spatial?.[list]?.find(item => item.id === selection.id) ?? null : null;
}

export function commandFleet(state, id)
{
    if (participant(state, state?.you)?.role !== 'player') { return null; }
    return state?.spatial?.fleets?.find(item => item.id === id && item.ownerParticipantId === state.you) ?? null;
}

export function fleetLocation(state, fleet)
{
    if (fleet.location?.kind === 'system') { return systemName(state, fleet.location.systemId); }
    if (fleet.location?.kind === 'transit') { return `${systemName(state, fleet.location.fromSystemId)} → ${systemName(state, fleet.location.toSystemId)}`; }
    return 'Положение недоступно';
}

export function movementRows(state, fleet)
{
    if (!fleet) { return []; }
    const rows = [['Состояние', phases[fleet.phase] ?? fleet.phase], ['Место', fleetLocation(state, fleet)], ['Прибытие', 'Недоступно: время конечного прибытия не получено']];
    const movement = fleet.movement;
    if (!movement) { return rows; }
    if (movement.target)
    {
        const point = movement.target.position;
        rows.push(['Цель', `${systemName(state, movement.target.systemId)}${point ? ` · X ${number.format(point.x)}, Z ${number.format(point.z)}` : ''}`]);
    }
    if (movement.routeSystemIds?.length) { rows.push(['Маршрут', movement.routeSystemIds.map(id => systemName(state, id)).join(' → ')]); }
    if (movement.pendingAction) { rows.push(['После прибытия', movement.pendingAction.systemId ? `К ${systemName(state, movement.pendingAction.systemId)}` : 'Остановиться']); }
    return rows;
}

export function fleetOrderRows(state, fleetId, pending)
{
    const fleet = commandFleet(state, fleetId);
    if (!fleet) { return []; }
    const labels = { moveFleetToPoint: 'К точке', moveFleetToSystem: 'К системе', stopFleet: 'Стоп' };
    const commands = [...pending].filter(command => command.fleetId === fleet.id && Object.hasOwn(labels, command.type));
    return [['Корабли', String(fleet.shipCount)], ...movementRows(state, fleet), ['Ожидают подтверждения', commands.length ? `${commands.length} · ${commands.map(command => labels[command.type]).join(' → ')} · маршрут ещё не подтверждён` : 'Нет']];
}

export function selectionCard(state, selection, resolveLocalization = (_key, fallback) => fallback)
{
    const value = selectedObject(state, selection);
    if (!value) { return null; }
    const body = selection.kind === 'colony' ? state.spatial.bodies.find(item => item.id === value.bodyId) : value;
    const title = selection.kind === 'body' || selection.kind === 'colony' ? bodyDisplayName(body, state.spatial.bodies) : value.name ?? value.id;
    const kind = selection.kind === 'body' ? value.kind : selection.kind;
    const rows = [];
    if (kind === 'system') { rows.push(['Видимые объекты', String(state.spatial.bodies.filter(item => item.systemId === value.id).length + (state.spatial.gateways??[]).filter(item=>item.systemId===value.id).length + (state.spatial.wormholes??[]).filter(item=>item.systemId===value.id).length)]); }
    else
    {
        if(kind==='gateway' || kind==='wormhole') { rows.push(['Состояние', value.status], ['Система', systemName(state,value.systemId)]); }
        else rows.push(['Владелец', ownerName(state, value.ownerParticipantId)]);
        if (kind === 'fleet') { rows.push(['Корабли', String(value.shipCount)], ...movementRows(state, value)); }
        else if (body?.systemId)
        {
            rows.push(['Система', systemName(state, body.systemId)]);
            if (body.worldClassId) { rows.push(['Класс', resolveLocalization(body.worldClassId, 'Название класса недоступно')]); }
            if (['planet', 'moon'].includes(body.kind) && Number.isSafeInteger(body.planetSize) && body.planetSize > 0) { rows.push(['Размер', String(body.planetSize)]); }
            for (const modifier of body.modifiers ?? []) { rows.push(['Модификатор', resolveLocalization(modifier.nameKey ?? modifier.modifierId, 'Название модификатора недоступно')]); }
        }
    }
    return { title, subtitle: types[kind] ?? kind, rows, ...(body && ['body', 'colony'].includes(selection.kind) ? { diagnostic: `Объект: ${body.id} · Исходная подпись: ${body.name ?? 'не получена'}` } : {}) };
}

export function outlinerGroups(state)
{
    const spatial = state?.spatial;
    const owner = viewParticipantId(state);
    if (!spatial || owner == null) { return []; }
    const groups = [
        { title: 'Колонии', rows: spatial.colonies.filter(item => item.ownerParticipantId === owner).map(item => { const body = spatial.bodies.find(value => value.id === item.bodyId); return { kind: 'colony', id: item.id, name: bodyDisplayName(body, spatial.bodies), detail: body ? systemName(state, body.systemId) : 'Положение недоступно', icon: 'gfx/interface/icons/planet.dds' }; }) },
        { title: 'Флоты', rows: spatial.fleets.filter(item => item.ownerParticipantId === owner).map(item => ({ kind: 'fleet', id: item.id, name: item.name, detail: `${item.shipCount} · ${phases[item.phase] ?? item.phase} · ${fleetLocation(state, item)}`, icon: 'gfx/interface/icons/fleet_size_icon.dds' })) },
        ...['station', 'planet', 'moon', 'star'].map(kind => ({ title: { station: 'Станции', planet: 'Планеты', moon: 'Спутники', star: 'Звёзды' }[kind], rows: spatial.bodies.filter(item => item.kind === kind && item.ownerParticipantId === owner && !spatial.colonies.some(colony => colony.bodyId === item.id)).map(item => ({ kind: 'body', id: item.id, name: bodyDisplayName(item, spatial.bodies), detail: systemName(state, item.systemId), icon: ['planet', 'moon'].includes(kind) ? 'gfx/interface/icons/planet.dds' : null })) }))
    ];
    return groups.filter(group => group.rows.length);
}

export function resourceItems(state)
{
    const owner = viewParticipantId(state);
    if (owner == null) { return []; }
    const planet = state?.planets?.find(item => item.ownerId === owner);
    return Object.entries(planet?.balances ?? {}).map(([id, value]) => ({ id, value: number.format(value), net: planet.report?.net?.[id] == null ? null : `${planet.report.net[id] > 0 ? '+' : ''}${number.format(planet.report.net[id])}` }));
}

export function identityPresentation(state, currentSystem, stress)
{
    const user = participant(state, state?.you);
    return {
        view: stress ? 'Тест · 200 корветов' : currentSystem ? systemName(state, currentSystem) : 'Галактика',
        name: user?.name ?? 'Stellaris Server',
        detail: user ? `${user.role === 'player' ? 'Игрок' : user.role === 'observer' ? `Наблюдатель · ${viewParticipantId(state) == null ? 'Общий обзор' : `Вид игрока: ${ownerName(state, viewParticipantId(state))}`} · только просмотр` : 'Ожидает допуска'}${user.isHost ? ' · хост' : ''}` : 'Локальный прототип M3',
        paused: state?.phase === 'running' && state.paused === true
    };
}

export function cameraOptions(currentSystem, stress)
{
    if (stress) { return []; }
    return currentSystem ? [['system-overview', 'Обзор системы'], ['system-close', 'Крупный план']] : [['galaxy-far', 'Дальний вид'], ['galaxy-near', 'Ближний вид']];
}

export function labelInsets(height, topbarBottom, plaqueTop, { canvas, leftPanel, rightPanel } = {})
{
    const intersects = panel => canvas && panel && panel.width > 0 && panel.height > 0 && panel.left < canvas.right && panel.right > canvas.left && panel.top < canvas.bottom && panel.bottom > canvas.top;
    const left = intersects(leftPanel) ? Math.max(16, leftPanel.right - canvas.left + 16) : 16;
    const right = intersects(rightPanel) ? Math.max(16, canvas.right - rightPanel.left + 16) : 16;
    // Preserve HUD/plaque policy; exhausted side bounds are handled by the renderer.
    return { top: Math.max(0, Math.min(height * .2, topbarBottom + 8)), right, bottom: Math.max(0, Math.min(height * .35, height - plaqueTop + 12)), left };
}
