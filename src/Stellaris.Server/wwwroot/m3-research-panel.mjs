const AREAS = [
    { id: 'physics', key: 'PHYSICS', label: 'Физика', color: 'physics' },
    { id: 'society', key: 'SOCIETY', label: 'Общество', color: 'society' },
    { id: 'engineering', key: 'ENGINEERING', label: 'Инженерия', color: 'engineering' }
];
const number = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });

export function researchEmpire(state, participantId)
{
    if (participantId == null || !Array.isArray(state?.research?.empires)) { return null; }
    return state.research.empires.find(empire => empire.participantId === participantId) ?? null;
}

export function researchPanelState()
{
    return { choicesArea: null, researchedArea: 'all' };
}

export function renderResearchPanel(main, side, empire, view, { document, canChoose, pendingAreas = new Set(), imageStore = null, isCurrent = () => true, onRequestDirectory = () => {}, onChoose = () => {}, onClose = () => {}, resolveLocalization = (key, fallback) => fallback ?? key } = {})
{
    const areaLabels = Object.fromEntries(AREAS.map(area => [area.id, resolveLocalization(area.key, area.label)]));
    const node = (tag, text, className) => { const result = document.createElement(tag); if (text != null) { result.textContent = String(text); } if (className) { result.className = className; } return result; };
    const button = (text, action, { disabled = false, testId, className = '' } = {}) =>
    {
        const result = node('button', text, className); result.type = 'button'; result.disabled = disabled;
        if (testId) { result.dataset.testid = testId; result.dataset.researchFocus = testId; }
        result.onclick = action; return result;
    };
    const active = document.activeElement?.dataset?.researchFocus;
    main.replaceChildren(); side.replaceChildren();
    const heading = node('div', null, 'research-heading');
    heading.append(node('h2', resolveLocalization('TECHNOLOGY', 'Технологии')));
    const mainActions = node('div', null, 'research-heading-actions');
    const sourceNote = node('span', '', 'research-source-note'); sourceNote.hidden = true;
    mainActions.append(sourceNote);
    if (empire) { mainActions.append(button('Папка игры', onRequestDirectory, { testId: 'research-icon-source', className: 'research-source-button' })); }
    mainActions.append(button(resolveLocalization('RESEARCHED', 'Изучено'), () => { view.choicesArea = 'researched'; view.researchedArea = 'all'; onChoose(); }, { testId: 'research-open-researched', className: 'research-tab' }));
    mainActions.append(button('×', onClose, { testId: 'research-close', className: 'research-close', disabled: false }));
    heading.append(mainActions);
    main.append(heading);
    if (!empire)
    {
        main.append(node('p', 'Технологии недоступны в этой перспективе.', 'research-empty'));
        side.hidden = true; main.dataset.testid = 'research-panel'; return;
    }
    for (const area of AREAS)
    {
        const value = empire.areas?.find(item => item.id === area.id);
        const section = node('section', null, `research-area research-${area.color}`); section.dataset.area = area.id;
        const title = node('div', null, 'research-area-heading');
        title.append(node('span', null, 'research-glyph'), node('h3', areaLabels[area.id])); section.append(title);
        if (!value)
        {
            section.append(node('p', 'Нет данных направления.', 'research-muted'));
            main.append(section); continue;
        }
        const technology = value.choices?.find(item => item.id === value.activeTechnologyId) ?? null;
        const completed = value.lastCompletedTechnologyId == null ? null : empire.researched?.find(item => item.id === value.lastCompletedTechnologyId) ?? { id: value.lastCompletedTechnologyId };
        const progress = Number.isFinite(value.progress) && technology && Number.isFinite(technology.cost) && technology.cost > 0 ? Math.max(0, Math.min(100, value.progress / technology.cost * 100)) : null;
        if (technology) { appendTechnologyIcon(section, technology, { document, node, imageStore, isCurrent, resolveLocalization, onIconUnavailable: reason => setSourceNote(sourceNote, reason) }); }
        section.append(node('p', technology ? technologyName(technology, resolveLocalization) : 'Исследование не выбрано', technology ? 'research-tech-name' : 'research-muted'));
        if (progress != null)
        {
            const meter = document.createElement('progress'); meter.max = 100; meter.value = progress; meter.setAttribute('aria-label', `Прогресс: ${Math.round(progress)}%`); section.append(meter);
            section.append(node('p', `${number.format(value.progress)} / ${number.format(technology.cost)} · ${number.format(value.monthlyResearch ?? 0)} в месяц`, 'research-metrics'));
        }
        else { section.append(node('p', `${number.format(value.monthlyResearch ?? 0)} в месяц`, 'research-metrics')); }
        section.append(node('p', `Резерв: ${number.format(value.reserve ?? 0)}`, 'research-reserve'));
        if (completed) { section.append(node('p', `Исследование завершено: ${technologyName(completed, resolveLocalization)} · выберите следующую технологию`, 'research-complete')); }
        const choose = button(technology && progress != null ? 'Сменить исследование' : 'Выбрать технологию', () => { view.choicesArea = area.id; view.lastArea = area.id; view.researchedArea = 'all'; onChoose(); }, { disabled: false, testId: `research-choose-${area.id}` });
        section.append(choose); main.append(section);
    }
    main.dataset.testid = 'research-panel';
    if (!view.choicesArea) { side.hidden = true; return; }
    side.hidden = false;
    const sideHeading = node('div', null, 'research-heading');
    sideHeading.append(node('h2', view.choicesArea === 'researched' ? resolveLocalization('RESEARCHED', 'Изученные технологии') : resolveLocalization('SELECT_TECHNOLOGY', `Выбор · ${areaLabels[view.choicesArea] ?? ''}`, { parameters: { AREA: areaLabels[view.choicesArea] ?? '' } })), button('×', () => { view.choicesArea = null; onChoose(); }, { testId: 'research-side-close', className: 'research-close' }));
    side.append(sideHeading);
    const tabs = node('div', null, 'research-tabs');
    tabs.append(button('Предложения', () => { view.choicesArea = view.lastArea ?? 'physics'; view.researchedArea = 'all'; onChoose(); }, { testId: 'research-tab-choices', disabled: view.choicesArea !== 'researched', className: 'research-tab' }));
    tabs.append(button(resolveLocalization('RESEARCHED', 'Изучено'), () => { view.choicesArea = 'researched'; onChoose(); }, { testId: 'research-tab-researched', disabled: view.choicesArea === 'researched', className: 'research-tab' })); side.append(tabs);
    if (view.choicesArea === 'researched')
    {
        const filters = node('div', null, 'research-filters');
        for (const [id, label] of [['all', 'Все'], ...AREAS.map(area => [area.id, areaLabels[area.id]])])
        {
            filters.append(button(label, () => { view.researchedArea = id; onChoose(); }, { testId: `research-filter-${id}`, className: 'research-filter' }));
            filters.lastChild.setAttribute('aria-pressed', String(view.researchedArea === id));
        }
        const list = (empire.researched ?? []).filter(item => view.researchedArea === 'all' || item.area === view.researchedArea);
        const listContent = node('div', null, 'research-list');
        listContent.append(...(list.length ? list.map(technology => technologyCard(technology, { document, node, button, imageStore, isCurrent, resolveLocalization, onIconUnavailable: reason => setSourceNote(sourceNote, reason) })) : [node('p', 'Изученных технологий в этом направлении пока нет.', 'research-empty')]));
        side.append(listContent, filters);
    }
    else
    {
        const area = view.choicesArea;
        const value = empire.areas?.find(item => item.id === area);
        const choices = value?.choices ?? [];
        const listContent = node('div', null, 'research-list');
        listContent.append(...(choices.length ? choices.map(technology =>
        {
            const card = technologyCard(technology, { document, node, button, imageStore, isCurrent, resolveLocalization, onIconUnavailable: reason => setSourceNote(sourceNote, reason) });
            const alreadyActive = technology.id === value.activeTechnologyId;
            const select = button(alreadyActive ? 'Исследуется' : pendingAreas.has(area) ? 'Ожидается ответ…' : 'Исследовать', () => onChoose(area, technology.id, empire.revision), { disabled: !canChoose || pendingAreas.has(area) || alreadyActive, testId: `research-select-${area}-${technology.id}`, className: 'research-select' });
            card.append(select); return card;
        }) : [node('p', 'Предложений для этого направления нет.', 'research-empty')]));
        side.append(listContent);
    }
    if (active) { [...main.querySelectorAll('[data-research-focus]'), ...side.querySelectorAll('[data-research-focus]')].find(item => item.dataset.researchFocus === active)?.focus({ preventScroll: true }); }
}

function setSourceNote(note, reason)
{
    if (note.hidden) { note.textContent = 'Иконки недоступны'; note.title = String(reason); note.setAttribute('aria-label', `Источник иконок недоступен: ${reason}`); note.hidden = false; }
}
function technologyName(technology, resolveLocalization)
{
    const unavailable = `${technology.id ?? 'Технология'} · название недоступно`;
    return technology.nameKey ? resolveLocalization(technology.nameKey, unavailable) : unavailable;
}

function effectText(effect, resolveLocalization)
{
    const reasons = { 'unknown-effect-swap': 'Условия замены эффекта не определены.', 'unsupported-effect-expression': 'Выражение эффекта пока не поддержано.' };
    const code = typeof effect.diagnosticCode === 'string' && effect.diagnosticCode ? effect.diagnosticCode : null;
    const reason = code ? Object.hasOwn(reasons, code) ? reasons[code] : `Причина недоступности: ${code}` : null;
    const describe = text => reason ? `${text} · ${reason}` : text;
    const fallback = effect.targetDefinitionId ?? effect.sourceKey ?? 'Эффект без описания';
    const label = effect.localizationKey ? resolveLocalization(effect.localizationKey, fallback) : fallback;
    if (effect.sourceKey === 'unlock_building' && typeof effect.targetDefinitionId === 'string')
    {
        return describe(resolveLocalization('TECH_UNLOCK_BUILDING', `Открывает сооружение: ${label}`, { parameters: { NAME: label } }));
    }
    if (effect.amount == null) { return describe(['number', 'percent'].includes(effect.format) ? `${label} · числовое значение эффекта не разрешено` : label); }
    if (!Number.isFinite(effect.amount) || !['number', 'percent'].includes(effect.format)) { return describe(`${label} · числовое представление эффекта недоступно`); }
    const amount = effect.format === 'percent' ? effect.amount * 100 : effect.amount;
    if (!Number.isFinite(amount)) { return describe(`${label} · числовое представление эффекта недоступно`); }
    return describe(`${label}: ${amount > 0 ? '+' : ''}${number.format(amount)}${effect.format === 'percent' ? '%' : ''}`);
}

function briefEffectText(effect, resolveLocalization)
{
    const fallback = effect.targetDefinitionId ?? effect.sourceKey ?? 'Эффект';
    const label = effect.localizationKey ? resolveLocalization(effect.localizationKey, fallback) : fallback;
    if (effect.sourceKey === 'unlock_building' && typeof effect.targetDefinitionId === 'string')
    {
        return resolveLocalization('TECH_UNLOCK_BUILDING', `Открывает сооружение: ${label}`, { parameters: { NAME: label } });
    }
    if (Number.isFinite(effect.amount) && ['number', 'percent'].includes(effect.format))
    {
        const amount = effect.format === 'percent' ? effect.amount * 100 : effect.amount;
        if (Number.isFinite(amount)) { return `${label}: ${amount > 0 ? '+' : ''}${number.format(amount)}${effect.format === 'percent' ? '%' : ''}`; }
    }
    return label;
}

function technologyCard(technology, { document, node, button, imageStore, isCurrent, resolveLocalization, onIconUnavailable })
{
    const card = node('article', null, 'research-card'); card.dataset.technologyId = technology.id ?? '';
    appendTechnologyIcon(card, technology, { document, node, imageStore, isCurrent, resolveLocalization, onIconUnavailable });
    const category = technology.categoryKey ? resolveLocalization(technology.categoryKey, `Категория · ${technology.category ?? technology.categoryKey}`) : technology.category == null ? null : `Категория · ${technology.category}`;
    card.dataset.area = technology.area ?? '';
    const title = node('div', null, `research-card-heading research-${technology.area ?? 'unknown'}`);
    const titleText = node('div', null, 'research-card-title');
    titleText.append(node('h3', technologyName(technology, resolveLocalization)));
    if (category) { titleText.append(node('span', category, 'research-card-category')); }
    title.append(titleText, node('span', technology.cost == null ? 'Стоимость недоступна' : number.format(technology.cost), 'research-card-cost'));
    card.append(title);
    const details = [technology.rare === true ? 'Редкая' : technology.rarity, technology.tier == null ? null : `Уровень ${technology.tier}`].filter(Boolean);
    if (details.length) { card.append(node('p', details.join(' · '), 'research-card-meta')); }
    for (const effect of technology.effects ?? [])
    {
        const detail = document.createElement('details'); detail.className = `research-effect-details research-effect-${effect.status ?? 'deferred'}`;
        const summary = document.createElement('summary');
        const brief = briefEffectText(effect, resolveLocalization);
        summary.textContent = effect.status === 'deferred' ? `Отложено · ${brief}` : brief;
        detail.append(summary, node('p', effectText(effect, resolveLocalization), 'research-effect-full'));
        card.append(detail);
    }
    if (technology.descriptionKey) { const description = document.createElement('details'); description.className = 'research-description-details'; description.append(node('summary', 'Описание')); description.append(node('p', resolveLocalization(technology.descriptionKey, 'Описание технологии недоступно.'), 'research-description')); card.append(description); }
    return card;
}

function appendTechnologyIcon(parent, technology, { document, node, imageStore, isCurrent, resolveLocalization, onIconUnavailable })
{
    const frame = node('div', null, 'research-icon-frame'); frame.dataset.testid = `research-icon-${technology.id ?? 'unknown'}`;
    parent.append(frame);
    const showUnavailable = reason =>
    {
        const message = String(reason || 'Причина недоступности не предоставлена.');
        frame.dataset.missing = 'true';
        frame.setAttribute('title', `Иконка технологии недоступна: ${message}`);
        frame.setAttribute('aria-label', `Иконка технологии недоступна. Причина: ${message}`);
        onIconUnavailable?.(message);
    };
    if (!technology.icon)
    {
        showUnavailable(technology.iconDiagnostic ?? 'Сервер не предоставил проверяемую иконку.'); return;
    }
    if (!imageStore) { showUnavailable('Локальные файлы технологии не загружены.'); return; }
    imageStore.get(technology).then(image =>
    {
        if (!isCurrent()) { return; }
        const icon = document.createElement('img'); icon.src = image.url; icon.width = image.width; icon.height = image.height;
        icon.alt = technologyName(technology, resolveLocalization); icon.className = 'research-icon';
        delete frame.dataset.missing; frame.removeAttribute('title'); frame.removeAttribute('aria-label'); frame.replaceChildren(icon);
    }).catch(failure =>
    {
        if (!isCurrent()) { return; }
        showUnavailable(failure instanceof Error ? failure.message : String(failure ?? 'Не удалось загрузить иконку.'));
    });
}
