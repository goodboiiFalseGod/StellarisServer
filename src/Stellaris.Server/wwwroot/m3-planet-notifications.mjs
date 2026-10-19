import { constructionIdentifier } from './m3-construction-controls.mjs';

const automaticReasons = new Set(['non-positive-speed', 'condition-lost', 'ownership-changed', 'destroyed', 'payer-missing', 'calculation-failed']);

// Хранит только уже разрешённые POV-модели; контекст и видимость задаёт владелец клиента.
export function createPlanetNotifications()
{
    let context, generation = 0, version = 0;
    const seen = new Set(), notices = new Map();
    return {
        get generation() { return generation; },
        get version() { return version; },
        sync(nextContext, models)
        {
            if (context !== nextContext) { context = nextContext; generation++; version++; seen.clear(); notices.clear(); }
            const visible = new Map(models.filter(model => constructionIdentifier(model?.id) && model.development?.colonyId === model.id
                && model.development.ownerParticipantId === model.ownerId).map(model => [model.id, model]));
            for (const id of notices.keys()) if (!visible.has(id)) { notices.delete(id); version++; }
            for (const model of visible.values())
            {
                const existing = notices.get(model.id);
                if (existing && existing.colonyName !== model.name) { existing.colonyName = model.name; version++; }
                for (const order of model.development.queue ?? [])
                {
                    if (order.status !== 'cancelled' || !automaticReasons.has(order.cancellationReason)
                        || ![order.orderId, order.slotId, order.definitionId].every(constructionIdentifier)) continue;
                    const id = JSON.stringify([model.id, order.orderId]);
                    if (seen.has(id)) continue;
                    seen.add(id);
                    let group = notices.get(model.id);
                    if (!group) { group = { colonyId: model.id, colonyName: model.name, orders: new Map() }; notices.set(model.id, group); }
                    group.orders.set(order.orderId, Object.freeze({ orderId: order.orderId, slotId: order.slotId, definitionId: order.definitionId,
                        localizationKey: order.localizationKey, cancellationReason: order.cancellationReason }));
                    version++;
                }
            }
            return [...notices.values()].map(group => ({ colonyId: group.colonyId, colonyName: group.colonyName, orders: [...group.orders.values()] }));
        },
        get(epoch, colonyId, orderId) { return epoch === generation ? notices.get(colonyId)?.orders.get(orderId) ?? null : null; },
        dismiss(epoch, colonyId)
        {
            if (epoch !== generation || !notices.delete(colonyId)) return false;
            version++; return true;
        }
    };
}
