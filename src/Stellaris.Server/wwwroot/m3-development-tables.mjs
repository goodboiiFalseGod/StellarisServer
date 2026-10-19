// Компактные таблицы исходного каталога разворачиваются до UI presenter.
const fail = detail => { throw new Error(`Некорректные таблицы развития: ${detail}.`); };
const identifier = value => typeof value === 'string' && value.length > 0;
const index = (value, length) => Number.isSafeInteger(value) && value >= 0 && value < length;

export function hydrateDevelopmentTables(snapshot)
{
    const development = snapshot?.development;
    if (!development) { return snapshot; }
    const hasTerms = Object.hasOwn(development, 'sourceRecipeTerms');
    const hasSlots = Object.hasOwn(development, 'catalogSlotSets');
    const hasEncoding = Object.hasOwn(development, 'sourceRecipeEncoding');
    const interned = hasEncoding && development.sourceRecipeEncoding === 'interned-strings-v1';
    if (hasEncoding && !interned || interned && (!Array.isArray(development.sourceRecipeStrings) || !development.sourceRecipeStrings.every(value => typeof value === 'string')) || !interned && Object.hasOwn(development, 'sourceRecipeStrings')) { fail('sourceRecipeEncoding'); }
    if (!hasTerms && !hasSlots)
    {
        if (hasEncoding) { fail('кодировка без таблиц'); }
        if (development.colonies?.some(colony => colony.catalog?.some(entry => Object.hasOwn(entry, 'sourceRecipeIndices') || Object.hasOwn(entry, 'compatibleSlotSetIndex')))) { fail('индексы без таблиц'); }
        return snapshot;
    }
    if (!hasTerms || !hasSlots || !Array.isArray(development.sourceRecipeTerms) || !Array.isArray(development.catalogSlotSets) || !Array.isArray(development.colonies)) { fail('форма таблиц'); }
    const strings = development.sourceRecipeStrings;
    const terms = development.sourceRecipeTerms.map((row, position) =>
    {
        if (!Array.isArray(row) || row.length !== 6 || row[2] !== null && (typeof row[2] !== 'number' || !Number.isFinite(row[2]))) { fail(`sourceRecipeTerms[${position}]`); }
        const value = (column, optional = false) =>
        {
            const raw = row[column];
            if (optional && raw === null) { return null; }
            const resolved = interned ? index(raw, strings.length) ? strings[raw] : null : raw;
            if (optional ? typeof resolved !== 'string' : !identifier(resolved)) { fail(`sourceRecipeTerms[${position}][${column}]`); }
            return resolved;
        };
        return { kind: value(0), targetId: value(1), value: row[2], expression: value(3, true), conditionDisposition: value(4), applicationScope: value(5) };
    });
    const slotSets = development.catalogSlotSets.map((row, position) =>
    {
        if (!Array.isArray(row) || !row.every(identifier)) { fail(`catalogSlotSets[${position}]`); }
        return row;
    });
    const colonies = development.colonies.map((colony, colonyIndex) =>
    {
        if (!Array.isArray(colony?.catalog)) { fail(`colonies[${colonyIndex}].catalog`); }
        return { ...colony, catalog: colony.catalog.map((entry, entryIndex) =>
        {
            if (!entry || entry.sourceRecipe != null || entry.compatibleSlotIds != null || entry.sourceRecipeIndices != null && (!Array.isArray(entry.sourceRecipeIndices) || !entry.sourceRecipeIndices.every(value => index(value, terms.length))) || entry.compatibleSlotSetIndex != null && !index(entry.compatibleSlotSetIndex, slotSets.length)) { fail(`colonies[${colonyIndex}].catalog[${entryIndex}]`); }
            return { ...entry,
                ...(entry.sourceRecipeIndices == null ? {} : { sourceRecipe: entry.sourceRecipeIndices.map(value => ({ ...terms[value] })) }),
                ...(entry.compatibleSlotSetIndex == null ? {} : { compatibleSlotIds: [...slotSets[entry.compatibleSlotSetIndex]] }) };
        }) };
    });
    return { ...snapshot, development: { ...development, colonies } };
}
