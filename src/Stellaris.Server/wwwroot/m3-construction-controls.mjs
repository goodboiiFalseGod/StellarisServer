export function constructionIdentifier(value)
{
    return typeof value === 'string' && value.length > 0 && value.length <= 160 && !/[\u0000-\u001f\u007f-\u009f]/.test(value);
}

function amounts(values)
{
    return values != null && typeof values === 'object' && !Array.isArray(values) && Object.entries(values).every(([id, amount]) => constructionIdentifier(id) && typeof amount === 'number' && Number.isFinite(amount) && amount >= 0);
}

function unique(items, key, id)
{
    return items.filter(item => item[key] === id).length === 1;
}

function zoneReplacementOperations(development, installed, slot)
{
    const queue = development.queue;
    const slots = development.slots;
    const targets = installed.zoneReplacements;
    if (installed.kind !== 'zone' || !Array.isArray(targets) || !Array.isArray(queue) || !Array.isArray(slots)
        || !constructionIdentifier(installed.slotId) || !unique(development.installed, 'slotId', installed.slotId)
        || !slot || slot.kind !== 'zone' || slot.slotId !== installed.slotId || !unique(slots, 'slotId', slot.slotId)
        || slot.installedDefinitionId !== installed.definitionId || !constructionIdentifier(installed.definitionId)
        || !constructionIdentifier(slot.zoneInstanceId) || !Array.isArray(slot.gates) || slot.gates.length || slot.reservedByOrderId != null) { return []; }

    const children = slots.filter(item => item.kind === 'building' && item.zoneInstanceId === slot.zoneInstanceId);
    if (children.some(child => !constructionIdentifier(child.slotId) || !unique(slots, 'slotId', child.slotId)
        || !Array.isArray(child.gates) || child.gates.length || child.reservedByOrderId != null)) { return []; }
    const childIds = new Set(children.map(child => child.slotId));
    if (queue.some(order => order.status === 'pending' && (order.slotId === slot.slotId
        || order.zoneReplacement?.zoneInstanceId === slot.zoneInstanceId
        || childIds.has(order.slotId) || order.zoneInstanceId === slot.zoneInstanceId))) { return []; }

    const operations = [];
    for (const target of targets)
    {
        if (!target || typeof target !== 'object' || Array.isArray(target)) { continue; }
        const removed = target.removedBuildings;
        const quote = target.quote;
        if (!constructionIdentifier(target.sourceDefinitionId) || target.sourceDefinitionId !== installed.definitionId
            || !constructionIdentifier(target.definitionId) || target.definitionId === installed.definitionId
            || !unique(targets, 'definitionId', target.definitionId)
            || target.slotId !== slot.slotId || target.zoneInstanceId !== slot.zoneInstanceId
            || target.expectedColonyRevision !== development.revision || !constructionIdentifier(target.previewId)
            || target.canEnqueue !== true || target.canAfford !== true || !Array.isArray(target.gates) || target.gates.length
            || !quote || !constructionIdentifier(quote.policyId) || typeof quote.durationDays !== 'number'
            || !Number.isFinite(quote.durationDays) || quote.durationDays <= 0 || !amounts(quote.costs)
            || !amounts(quote.infrastructureUpkeep) || !amounts(quote.jobCapacity) || !Array.isArray(removed)) { continue; }

        const seenRemoved = new Set();
        const removedBuildings = [];
        let validRemoved = true;
        for (const building of removed)
        {
            if (!building || !constructionIdentifier(building.slotId) || !constructionIdentifier(building.definitionId)
                || seenRemoved.has(building.slotId) || (building.localizationKey != null && !constructionIdentifier(building.localizationKey)))
            { validRemoved = false; break; }
            seenRemoved.add(building.slotId);
            const buildingSlot = slots.find(item => item.slotId === building.slotId);
            const installedBuildings = development.installed.filter(item => item.slotId === building.slotId);
            if (!childIds.has(building.slotId) || !buildingSlot || !unique(slots, 'slotId', building.slotId)
                || buildingSlot.kind !== 'building' || buildingSlot.zoneInstanceId !== slot.zoneInstanceId
                || buildingSlot.installedDefinitionId !== building.definitionId || buildingSlot.reservedByOrderId != null
                || !Array.isArray(buildingSlot.gates) || buildingSlot.gates.length
                || installedBuildings.length !== 1 || installedBuildings[0].kind !== 'building'
                || installedBuildings[0].definitionId !== building.definitionId || !unique(development.installed, 'slotId', building.slotId))
            { validRemoved = false; break; }
            removedBuildings.push({ slotId: building.slotId, definitionId: building.definitionId, localizationKey: building.localizationKey ?? null });
        }
        if (!validRemoved) { continue; }
        operations.push({ type: 'enqueueZoneReplacement', sourceDefinitionId: target.sourceDefinitionId,
            definitionId: target.definitionId, slotId: target.slotId, zoneInstanceId: target.zoneInstanceId,
            zoneReplacementPreviewId: target.previewId, removedBuildings, quote });
    }
    return operations;
}

// Исполнимые привязки берутся из полученного снимка; владелец команды повторно проверяет полномочия.
export function constructionOperations(model)
{
    const development = model?.development;
    if (!development || !constructionIdentifier(model.id) || !Number.isSafeInteger(development.revision) || development.revision < 0 || model.gates.length || development.gates.length) { return []; }
    const operations = [];
    for (const entry of development.catalog)
    {
        const quote = entry.quote;
        if (!['district', 'zone', 'building', 'blockerClearance'].includes(entry.kind) || !constructionIdentifier(entry.definitionId) || !unique(development.catalog, 'definitionId', entry.definitionId) || entry.eligibility !== 'available' || entry.canAfford === false || entry.gates.length || !quote || !constructionIdentifier(quote.policyId) || typeof quote.durationDays !== 'number' || !Number.isFinite(quote.durationDays) || quote.durationDays <= 0 || !amounts(quote.costs) || !amounts(quote.infrastructureUpkeep) || !amounts(quote.jobCapacity)) { continue; }
        for (const slotId of new Set(entry.slotIds))
        {
            const slot = development.slots.find(item => item.slotId === slotId);
            if (!constructionIdentifier(slotId) || !slot || !unique(development.slots, 'slotId', slotId) || slot.kind !== entry.kind || slot.gates.length || slot.reservedByOrderId != null || development.queue.some(item => item.slotId === slotId && item.status === 'pending')) { continue; }
            const installed = development.installed.filter(item => item.slotId === slotId);
            if (slot.installedDefinitionId != null || installed.length !== 0) { continue; }
            if (entry.kind === 'blockerClearance')
            {
                const features = development.features.filter(feature => feature.clearanceSlotId === slotId);
                const feature = features[0];
                if (features.length !== 1 || !constructionIdentifier(feature.instanceId) || !unique(development.features, 'instanceId', feature.instanceId) || !constructionIdentifier(feature.definitionId) || feature.clearable !== true || feature.gates.length || feature.clearanceDefinitionId !== entry.definitionId) { continue; }
                operations.push({ type: 'clearBlocker', definitionId: entry.definitionId, slotId, instanceId: feature.instanceId, featureDefinitionId: feature.definitionId, quote });
            }
            else { operations.push({ type: 'enqueueConstruction', definitionId: entry.definitionId, slotId, quote }); }
        }
    }
    for (const installed of development.installed)
    {
        const slot = development.slots.find(item => item.slotId === installed.slotId);
        operations.push(...zoneReplacementOperations(development, installed, slot));
        if (installed.kind !== 'building' || !constructionIdentifier(installed.definitionId) || !constructionIdentifier(installed.slotId) || !unique(development.installed, 'slotId', installed.slotId) || !slot || !constructionIdentifier(slot.slotId) || !unique(development.slots, 'slotId', slot.slotId) || slot.kind !== 'building' || slot.installedDefinitionId !== installed.definitionId) { continue; }
        const toggle = installed.toggle;
        if (typeof installed.enabled === 'boolean' && toggle?.canToggle === true && toggle.targetEnabled === !installed.enabled
            && Array.isArray(toggle.gates) && toggle.gates.length === 0)
        {
            operations.push({ type: 'setBuildingEnabled', definitionId: installed.definitionId, slotId: slot.slotId, enabled: toggle.targetEnabled });
        }
        const demolition = installed.demolition;
        const hasPendingOrder = development.queue.some(item => item.slotId === slot.slotId && item.status === 'pending');
        if (!slot.gates.length && slot.reservedByOrderId == null && !hasPendingOrder
            && demolition?.canDemolish === true && Array.isArray(demolition.gates) && demolition.gates.length === 0)
        {
            operations.push({ type: 'demolishBuilding', definitionId: installed.definitionId, slotId: slot.slotId });
        }
        if (slot.gates.length || slot.reservedByOrderId != null || development.queue.some(item => item.slotId === slot.slotId && item.status === 'pending')) { continue; }
        const upgrades = Array.isArray(installed.upgrades) ? installed.upgrades : [];
        for (const target of upgrades)
        {
            const quote = target.quote;
            if (!constructionIdentifier(target.definitionId) || target.definitionId === installed.definitionId || !unique(upgrades, 'definitionId', target.definitionId) || target.sourceDefinitionId !== installed.definitionId || target.slotId !== slot.slotId || !constructionIdentifier(target.zoneInstanceId) || target.zoneInstanceId !== slot.zoneInstanceId || target.expectedColonyRevision !== development.revision || target.commandType !== 'enqueueUpgrade' || target.canEnqueue !== true || target.canAfford !== true || target.eligibility !== 'available' || !Array.isArray(target.gates) || target.gates.length || !quote || !constructionIdentifier(quote.policyId) || typeof quote.durationDays !== 'number' || !Number.isFinite(quote.durationDays) || quote.durationDays <= 0 || !amounts(quote.costs) || !amounts(quote.infrastructureUpkeep) || !amounts(quote.jobCapacity)) { continue; }
            operations.push({ type: 'enqueueUpgrade', sourceDefinitionId: installed.definitionId, definitionId: target.definitionId, slotId: slot.slotId, zoneInstanceId: target.zoneInstanceId, quote });
        }
    }
    const pendingOrders = development.queue.filter(order => order.status === 'pending');
    const policy = development.queuePolicy;
    if (policy?.policyId === 'ordinary-sequential-v1' && policy.activeCapacity === 1 && policy.canReorder === true && pendingOrders.length >= 3)
    {
        const ids = pendingOrders.map(order => order.orderId);
        if (ids.every(constructionIdentifier) && new Set(ids).size === ids.length)
        {
            for (let index = 1; index < pendingOrders.length; index++)
            {
                for (const direction of ['up', 'down'])
                {
                    const neighbor = direction === 'up' ? index - 1 : index + 1;
                    if (neighbor < 1 || neighbor >= pendingOrders.length) { continue; }
                    const orderIds = [...ids];
                    [orderIds[index], orderIds[neighbor]] = [orderIds[neighbor], orderIds[index]];
                    operations.push({ type: 'reorderConstruction', orderIds, orderId: ids[index], direction });
                }
            }
        }
    }
    for (const order of development.queue)
    {
        if (!constructionIdentifier(order.orderId) || !unique(development.queue, 'orderId', order.orderId) || order.status !== 'pending' || order.gates.length || !order.refundQuote || !constructionIdentifier(order.refundQuote.policyId) || !amounts(order.refundQuote.amounts) || !constructionIdentifier(order.refundQuote.previewId) || !amounts(order.refundQuote.losses)) { continue; }
        operations.push({ type: 'cancelConstruction', orderId: order.orderId, refundQuote: order.refundQuote });
    }
    return operations;
}
