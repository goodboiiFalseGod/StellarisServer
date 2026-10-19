const positiveLosses = losses => Object.entries(losses && typeof losses === 'object' ? losses : {}).filter(([, amount]) => typeof amount === 'number' && Number.isFinite(amount) && amount > 0);

function resourceRows(root, entries, { document, appendResourceIcon = () => {}, formatAmount = value => String(value), resourceLabel = () => 'Ресурс' } = {})
{
    const list = document.createElement('div'); list.className = 'planet-refund-resources';
    for (const [resourceId, amount] of entries)
    {
        const pair = document.createElement('span'); pair.className = 'planet-refund-resource';
        const labelValue = resourceLabel(resourceId);
        const label = typeof labelValue === 'string' && labelValue.trim() ? labelValue : 'Ресурс';
        const formattedValue = formatAmount(amount);
        const formatted = formattedValue == null ? String(amount) : String(formattedValue);
        const icon = document.createElement('span'); icon.setAttribute('aria-hidden', 'true'); appendResourceIcon(icon, resourceId);
        const quantity = document.createElement('span'); quantity.textContent = formatted;
        pair.title = `${label}: ${formatted}`; pair.setAttribute('aria-label', `${label}: ${formatted}`);
        pair.append(icon, quantity); list.append(pair);
    }
    root.append(list);
}

/** Показывает подтверждение только для фактически положительных потерь возврата. */
export function renderConstructionRefund(root, losses, { document, appendResourceIcon, formatAmount, resourceLabel, onConfirm = () => {}, onKeep = () => {} } = {})
{
    root.replaceChildren();
    const entries = positiveLosses(losses);
    if (!entries.length) { root.hidden = true; return; }
    root.hidden = false;
    const heading = document.createElement('h3'); heading.textContent = 'Часть возврата будет потеряна';
    const explanation = document.createElement('p'); explanation.textContent = 'Подтвердите отмену заказа с указанной потерей возврата или сохраните заказ.';
    root.append(heading, explanation);
    resourceRows(root, entries, { document, appendResourceIcon, formatAmount, resourceLabel });
    const actions = document.createElement('div'); actions.className = 'planet-refund-actions';
    const confirm = document.createElement('button'); confirm.type = 'button'; confirm.textContent = 'Отменить заказ'; confirm.onclick = () => onConfirm();
    const keep = document.createElement('button'); keep.type = 'button'; keep.textContent = 'Сохранить заказ'; keep.onclick = () => onKeep();
    actions.append(confirm, keep); root.append(actions);
}

/** Отображает только уже зафиксированные положительные потери возврата. */
export function renderConstructionRefundLosses(root, losses, options = {})
{
    root.replaceChildren();
    const entries = positiveLosses(losses);
    if (!entries.length) { root.hidden = true; return; }
    root.hidden = false;
    const heading = options.document.createElement('h3'); heading.textContent = 'Потеря возврата'; root.append(heading);
    resourceRows(root, entries, options);
}
