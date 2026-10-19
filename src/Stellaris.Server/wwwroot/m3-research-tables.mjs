const fail = detail => { throw new Error(`Некорректная таблица исследований: ${detail}.`); };
const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);

export function hydrateResearchTables(snapshot)
{
    const research = snapshot?.research;
    if (research == null || !Object.hasOwn(research, 'encoding')) { return snapshot; }
    if (!isObject(research) || research.encoding !== 'shared-unavailable-v1') { fail('encoding'); }
    if (!Array.isArray(research.empires) || !research.empires.every(isObject)) { fail('empires'); }
    if (!Array.isArray(research.unavailableTable) || !research.unavailableTable.every(entry =>
        isObject(entry) && typeof entry.technologyId === 'string' && typeof entry.name === 'string' &&
        Array.isArray(entry.gates) && entry.gates.every(gate => typeof gate === 'string') &&
        (entry.nameKey === undefined || entry.nameKey === null || typeof entry.nameKey === 'string')))
    { fail('unavailableTable'); }
    if (!Array.isArray(research.unavailableIndices) || research.unavailableIndices.length !== research.empires.length)
    { fail('unavailableIndices'); }

    const unavailable = research.unavailableIndices.map((indices, empireIndex) =>
    {
        if (indices === null) { return null; }
        if (!Array.isArray(indices) || !indices.every(index => Number.isInteger(index) && index >= 0 && index < research.unavailableTable.length))
        { fail(`unavailableIndices[${empireIndex}]`); }
        return indices.map(index => research.unavailableTable[index]);
    });
    const empires = research.empires.map((empire, index) => ({ ...empire, unavailable: unavailable[index] }));
    const hydratedResearch = { ...research, empires };
    delete hydratedResearch.encoding;
    delete hydratedResearch.unavailableTable;
    delete hydratedResearch.unavailableIndices;
    return { ...snapshot, research: hydratedResearch };
}
