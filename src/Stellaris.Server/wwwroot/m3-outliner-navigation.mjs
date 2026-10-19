import { selectedObject, viewParticipantId } from './m3-ui-presenter.mjs';

const systems = state => state?.spatial?.systems ?? [];

function unavailable(reason)
{
    return { systemId: null, reason };
}

export function outlinerNavigation(state, selection)
{
    const ownerId = viewParticipantId(state);
    if (ownerId == null)
    {
        if (state?.viewContext?.mode === 'general') { return unavailable('В общем обзоре личная навигация недоступна.'); }
        if (state?.viewContext?.mode === 'pending' || state?.participants?.find(item => item.id === state?.you)?.role === 'pendingObserver') { return unavailable('Ожидайте подтверждения личного обзора.'); }
        return unavailable('Личный обзор не подтверждён.');
    }

    const value = selectedObject(state, selection);
    if (!value) { return unavailable('Объект отсутствует в текущем обзоре.'); }
    if (value.ownerParticipantId !== ownerId) { return unavailable('Объект не принадлежит участнику текущего обзора.'); }

    let systemId;
    if (selection.kind === 'colony')
    {
        const body = state?.spatial?.bodies?.find(item => item.id === value.bodyId);
        if (!body) { return unavailable('Тело колонии отсутствует в текущем обзоре.'); }
        systemId = body.systemId;
    }
    else if (selection.kind === 'body') { systemId = value.systemId; }
    else if (selection.kind === 'fleet')
    {
        if (value.location?.kind !== 'system') { return unavailable('Флот находится в пути; его система недоступна для навигации.'); }
        systemId = value.location.systemId;
    }
    else { return unavailable('Для этого типа объекта переход не поддерживается.'); }

    if (!systems(state).some(item => item.id === systemId)) { return unavailable('Система объекта отсутствует в текущем обзоре.'); }
    return { systemId, reason: null };
}
