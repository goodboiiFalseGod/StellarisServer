// Генерация только authored mappings; игровые bytes остаются в локальной установке.
import { readFile, writeFile, readdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { parseVisualDefinitions, requireBlock, optionalScalar, requireScalar } from './visual-definitions.mjs';
import { inspectDds } from './formats.mjs';
import { BASE_PLANET_IMAGES as baseline } from './m3-planet-assets.mjs';

const [root, inventoryPath, outputPath, csOutputPath] = process.argv.slice(2);
if (!csOutputPath) throw new Error('Usage: node generate-ordinary-planet-assets.mjs ROOT INVENTORY OUTPUT CS_OUTPUT');
const inventory = JSON.parse(await readFile(inventoryPath, 'utf8'));
const asts = new Map();
async function ast(path) {
    if (!asts.has(path)) asts.set(path, parseVisualDefinitions(await readFile(resolve(root, path), 'utf8'), { allowQuotedWhitespace: true }));
    return asts.get(path);
}
const sprites = new Map();
for (const file of (await readdir(resolve(root, 'interface'))).filter(file => file.endsWith('.gfx')).sort()) {
    const path = 'interface/' + file;
    const roots = (await ast(path)).filter(entry => entry.key === 'spriteTypes');
    for (const group of roots) for (const entry of group.value) {
        if (!Array.isArray(entry.value)) continue;
        const name = optionalScalar(entry.value, 'name', null), texture = optionalScalar(entry.value, 'texturefile', optionalScalar(entry.value, 'textureFile', null));
        if (name && texture) {
            const value = { spriteSource: path, sourcePath: texture.replaceAll('\\', '/'), type: entry.key, frames: optionalScalar(entry.value, 'noOfFrames', 1) };
            if (!sprites.has(name)) sprites.set(name, value);
            else if (sprites.get(name).sourcePath !== value.sourcePath) sprites.set(name, null);
        }
    }
}
const added = [];
const gates = [];
async function zoneIcon(fields, chain = []) {
    const direct = optionalScalar(fields, 'icon', null);
    if (direct) return { icon: direct, inlinePath: null };
    const candidates = [];
    for (const entry of fields.filter(entry => entry.key === 'inline_script')) {
        const script = typeof entry.value === 'string' ? entry.value : requireScalar(entry.value, 'script');
        if (chain.includes(script)) throw new Error('Inline icon cycle');
        const path = `common/inline_scripts/${script}.txt`;
        const result = await zoneIcon(await ast(path), [...chain, script]);
        if (result) candidates.push({ ...result, inlinePath: path });
    }
    if (candidates.length > 1) throw new Error('Ambiguous zone icon');
    return candidates[0] ?? null;
}
async function add(category, item) {
    if (baseline.some(record => record.key === `${category}.${item.Id}`)) return;
    const fields = requireBlock(await ast(item.RelativePath), item.Id);
    let icon = item.IconId ?? item.Id, inlinePath = null, conceptId = null;
    let spriteId = `local-${category}:${item.Id}`, spriteSource = null, type = 'spriteType', sourcePath;
    if (category === 'zone') {
        const found = await zoneIcon(fields);
        if (!found) throw new Error('Missing zone icon: ' + item.Id);
        icon = found.icon; inlinePath = found.inlinePath; spriteId = icon;
        const gfx = sprites.get(icon);
        if (!gfx) throw new Error('Missing/ambiguous GFX: ' + icon);
        ({ spriteSource, sourcePath, type } = gfx);
    } else if (category === 'building') sourcePath = `gfx/interface/icons/buildings/${icon}.dds`;
    else if (category === 'job') {
        icon = item.IconId ?? item.Id;
        conceptId = item.IconConceptId ?? null;
        sourcePath = item.LocalIconPath ?? `gfx/interface/icons/jobs/job_${icon}.dds`;
        if (conceptId) sourcePath = requireScalar(requireBlock(await ast('common/game_concepts/01_job_concepts.txt'), conceptId), 'icon');
    } else if (category === 'socialClass') {
        sourcePath = `gfx/interface/icons/pop_categories/pop_cat_${item.Id}.dds`;
        icon = `pop_cat_${item.Id}`;
        if (item.Id === 'civilian') {
            spriteId = 'GFX_civilian';
            ({ spriteSource, sourcePath, type } = sprites.get(spriteId));
        }
    } else {
        const aliases = { advanced_logic: 'GFX_crisis_icon_advanced_logic', exotic_gases: 'GFX_resource_exotic_gases_large', rare_crystals: 'GFX_resource_rare_crystals_large', volatile_motes: 'GFX_resource_volatile_motes_large', sr_dark_matter: 'GFX_resource_sr_dark_matter_large', sr_living_metal: 'GFX_resource_sr_living_metal_large', nanites: 'GFX_resource_nanites_large' };
        spriteId = aliases[item.Id] ?? 'GFX_resource_' + item.Id;
        const gfx = sprites.get(spriteId);
        if (!gfx && item.Id === 'nanites') {
            spriteId = 'local-resource:nanites'; sourcePath = 'gfx/interface/icons/resources/nanites.dds';
        } else {
            if (!gfx) throw new Error('Missing resource GFX: ' + item.Id);
            if (gfx.frames !== 1) {
                gates.push({ key: `${category}.${item.Id}`, reason: 'sprite-frame-selection-unresolved', spriteId, ...gfx });
                return;
            }
            ({ spriteSource, sourcePath, type } = gfx);
        }
    }
    const bytes = await readFile(resolve(root, sourcePath));
    const info = inspectDds(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
    if (info.mipCount !== 1 || info.faceCount !== 1) throw new Error('Unexpected DDS layout: ' + sourcePath);
    added.push({ key: `${category}.${item.Id}`, category, id: item.Id, definitionPath: item.RelativePath,
        spriteId, spriteSource, sourcePath, width: info.width, height: info.height, inlinePath, type, border: { x: 0, y: 0 },
        codec: info.codec, sourceIcon: icon, conceptId, sourceStratum: category === 'job' ? optionalScalar(fields, 'category', null) : null,
        conceptAlias: item.IconAlias ?? null });
}
for (const item of inventory.Buildings) await add('building', item);
for (const item of inventory.Zones) await add('zone', item);
for (const item of inventory.Jobs) await add('job', item);
for (const id of ['ruler', 'specialist', 'worker', 'civilian']) await add('socialClass', { Id: id, RelativePath: `common/pop_categories/${id === 'civilian' ? '02_other_categories' : '00_social_classes'}.txt` });
for (const id of inventory.Resources) await add('resource', { Id: id, RelativePath: 'common/strategic_resources/00_strategic_resources.txt' });
added.sort((a, b) => a.key.localeCompare(b.key));
await writeFile(outputPath, '// Source-validated ordinary icon mappings; игровые bytes не включены.\nexport const ORDINARY_PLANET_IMAGE_GATES = Object.freeze(' + JSON.stringify(gates, null, 2) + '.map(Object.freeze));\nexport const ORDINARY_PLANET_IMAGES = Object.freeze(' + JSON.stringify(added, null, 2) + '.map(record => Object.freeze({ ...record, border: Object.freeze(record.border) })));\n');
const all = [...baseline, ...added];
const paths = [...new Set(all.flatMap(record => [record.definitionPath, record.inlinePath, record.spriteSource, record.sourcePath, record.conceptId ? 'common/game_concepts/01_job_concepts.txt' : null]).filter(Boolean))].sort();
await writeFile(csOutputPath, 'using System.Collections.Frozen;\n\nnamespace Stellaris.Core.Assets;\n\n// Source-validated closure; игровые bytes не включены.\ninternal static class PlanetAssetProfile\n{\n    public static readonly IReadOnlyList<string> Paths = Array.AsReadOnly(new[]\n    {\n' + paths.map(path => '        ' + JSON.stringify(path) + ',').join('\n') + '\n    });\n\n    public static readonly IReadOnlyDictionary<string, (string Definition, string Texture)> Bindings =\n        new Dictionary<string, (string, string)>(StringComparer.Ordinal)\n        {\n' + all.filter(record => record.definitionPath).map(record => `            [${JSON.stringify(record.key)}] = (${JSON.stringify(record.definitionPath)}, ${JSON.stringify(record.sourcePath)}),`).join('\n') + '\n        }.ToFrozenDictionary(StringComparer.Ordinal);\n}\n');
const images = new Map(all.map(record => [record.sourcePath, record]));
console.log(JSON.stringify({ definitions: all.length, added: added.length, files: paths.length, textures: images.size,
    decodedBytes: [...images.values()].reduce((sum, record) => sum + record.width * record.height * 4, 0) }));
