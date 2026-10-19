const reasons = new Map([
    ['non-positive-speed', 'Неположительная скорость строительства'],
    ['condition-lost', 'Условия строительства больше не выполнены'],
    ['ownership-changed', 'Смена владельца колонии'],
    ['destroyed', 'Колония уничтожена'],
    ['payer-missing', 'Исходный плательщик больше не существует; возврат невозможен'],
    ['calculation-failed', 'Ошибка расчёта строительства'],
]);

/** Отрисовывает только переданные контроллером уведомления автоматической отмены. */
export function renderPlanetNotifications(root, groups, { document, resolveLocalization = (key, fallback) => fallback, onOpen = () => {}, onDismiss = () => {} } = {})
{
    root.replaceChildren();
    let rowCount = 0;
    for (const group of Array.isArray(groups) ? groups : [])
    {
        if (!group || typeof group.colonyId !== 'string' || !group.colonyId) { continue; }
        const orders = (Array.isArray(group.orders) ? group.orders : []).filter(order => order && typeof order.orderId === 'string' && !!order.orderId && reasons.has(order.cancellationReason));
        if (!orders.length) { continue; }

        const section = document.createElement('section'); section.className = 'planet-notice';
        const colonyName = typeof group.colonyName === 'string' && group.colonyName ? group.colonyName : 'Колония';
        const heading = document.createElement('h3'); heading.textContent = colonyName;
        const dismiss = document.createElement('button'); dismiss.type = 'button'; dismiss.textContent = '×';
        dismiss.setAttribute('aria-label', 'Закрыть уведомления колонии ' + colonyName);
        dismiss.onclick = () => onDismiss(group.colonyId);
        section.append(heading, dismiss);

        const list = document.createElement('ul');
        for (const order of orders)
        {
            const item = document.createElement('li');
            const button = document.createElement('button'); button.type = 'button'; button.className = 'planet-notice-order';
            const key = order.localizationKey ?? order.definitionId;
            const localizedName = resolveLocalization(key, 'Заказ строительства');
            const name = typeof localizedName === 'string' && localizedName.trim() ? localizedName : 'Заказ строительства';
            const title = document.createElement('span'); title.textContent = name;
            const reason = document.createElement('small'); reason.className = 'planet-notice-reason'; reason.textContent = reasons.get(order.cancellationReason);
            button.append(title, reason); button.onclick = () => onOpen(group.colonyId, order.orderId);
            item.append(button); list.append(item); rowCount++;
        }
        section.append(list); root.append(section);
    }
    root.hidden = rowCount === 0;
}
