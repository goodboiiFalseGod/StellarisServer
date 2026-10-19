import { parseVisualDefinitions, requireBlock, requireScalar, optionalScalar, selectNamedDefinition } from './visual-definitions.mjs';
import { ORDINARY_PLANET_IMAGES } from './m3-ordinary-planet-assets.mjs';
import { CONTINENTAL_ENVIRONMENT_PATHS, parseContinentalEnvironmentMetadata } from './m3-planet-environment.mjs';

const records = [];
function add(category, id, definitionPath, spriteId, spriteSource, sourcePath, width, height = width, inlinePath = null, type = 'spriteType', border = { x: 0, y: 0 }, codec = 'BGRA8', sourceIcon = id, conceptId = null, sourceStratum = null, conceptAlias = null)
{
    records.push(Object.freeze({ key: `${category}.${id}`, category, id, definitionPath, spriteId, spriteSource, sourcePath, width, height, inlinePath, type, border: Object.freeze(border), codec, sourceIcon, conceptId, sourceStratum, conceptAlias }));
}
for (const id of ['city', 'generator', 'mining', 'farming'])
{
    add('district', `district_${id}`, `common/districts/${id === 'city' ? '00_urban' : '02_rural'}_districts.txt`, `GFX_district_${id === 'city' ? 'cities' : id}`, 'interface/planet_view.gfx', `gfx/interface/icons/districts/district_${id}.dds`, 77);
}
for (const id of ['ark_city', 'ark_generator', 'ark_basic', 'ark_military', 'hive', 'nexus'])
{
    // Audited local icon mapping, not native GFX registration or animated grid-box selection.
    add('district', `district_${id}`, `common/districts/${id.startsWith('ark_') ? '07_ark' : '00_urban'}_districts.txt`, `local-district:district_${id}`, null, `gfx/interface/icons/districts/district_${id}.dds`, 77);
}
for (const [id, icon, inline] of [
    ['default', 'diplomatic_weight_large', null], ['urban', 'urban', null], ['research_unity', 'bureaucrat', null],
    ['industrial', 'industrial', 'industrial'], ['research', 'research', 'research'], ['unity', 'unity', 'unity'],
    ['foundry', 'foundry', 'industrial_foundry'], ['factory', 'artisan_industry', 'industrial_factory'],
    ['energy', 'energy', 'energy'], ['minerals', 'minerals', 'minerals'], ['food', 'food_market', 'food'], ['trade', 'trade', 'trade']
])
{
    const spriteIcon = id === 'factory' ? 'factory' : id === 'food' ? 'food' : icon;
    add('zone', `zone_${id}`, 'common/zones/00_zones.txt', id === 'default' ? 'GFX_diplomatic_weight_large' : `GFX_district_specialization_${spriteIcon}`, id === 'default' ? 'interface/galactic_community_view.gfx' : 'interface/icons.gfx', id === 'default' ? 'gfx/interface/icons/resources/diplomatic_weight_large.dds' : `gfx/interface/icons/districts/district_specialization_icons/district_specialization_${icon}.dds`, id === 'default' ? 32 : 64, id === 'default' ? 32 : 64, inline ? `common/inline_scripts/zones/shared_${inline}_zone.txt` : null);
}
for (const [id, spriteIcon, textureIcon, inline] of [
    ['research_unity', 'ark_research_unity_consumer_goods', 'culture_worker', null],
    ['industrial', 'ark_research_alloys_support', 'industrial', null],
    ['harvest', 'minerals', 'minerals', 'ark_harvest'], ['food', 'food', 'food_market', 'food'],
    ['reactor_physics', 'physics', 'physicist', 'research_physics'],
    ['farm_society', 'society', 'biology', 'research_society'],
    ['military_engineering', 'engineering', 'engineer', 'research_engineering']
])
{
    add('zone', `zone_ark_${id}`, 'common/zones/05_arkship_zones.txt', `GFX_district_specialization_${spriteIcon}`, 'interface/icons.gfx', `gfx/interface/icons/districts/district_specialization_icons/district_specialization_${textureIcon}.dds`, 64, 64, inline ? `common/inline_scripts/zones/shared_${inline}_zone.txt` : null);
}
for (const [id, file] of [
    ['colony_shelter', '00_capital'], ['capital', '00_capital'], ['factory_1', '04_manufacturing'], ['precinct_house', '02_government'],
    ['research_lab_1', '05_research'], ['stronghold', '09_army'], ['foundry_1', '04_manufacturing'], ['holo_theatres', '07_amenity'],
    ['bureaucratic_1', '08_unity'], ['hydroponics_farm', '03_resource'], ['medical_1', '07_amenity'], ['commercial_zone', '06_trade']
])
{
    add('building', `building_${id}`, `common/buildings/${file}_buildings.txt`, `GFX_building_${id}`, null, `gfx/interface/icons/buildings/building_${id}.dds`, 78);
}
for (const [id, file, icon = `building_${id}`] of [
    ['luxury_residence', '07_amenity'], ['temple', '08_unity'],
    ['hive_capital', '00_capital'], ['spawning_pool', '01_pop_assembly'], ['offspring_nest', '01_pop_assembly'], ['hive_warren', '07_amenity'],
    ['machine_capital', '00_capital'], ['machine_assembly_plant', '01_pop_assembly', 'building_robot_assembly_plant'], ['drone_storage', '07_amenity']
])
{
    const definitionId = `building_${id}`;
    add('building', definitionId, `common/buildings/${file}_buildings.txt`, `local-building:${definitionId}`, null, `gfx/interface/icons/buildings/${icon}.dds`, 78, 78, null, 'spriteType', { x: 0, y: 0 }, 'BGRA8', icon);
}
for (const [id, file] of [['farmer', '03_worker'], ['agri_drone', '04_gestalt']])
{
    add('job', id, `common/pop_jobs/${file}_jobs.txt`, `local-job:${id}`, null, `gfx/interface/icons/jobs/job_${id}.dds`, 30);
}
for (const [id, definitionFile, icon, gfxId = null, gfxPath = null, codec = 'BGRA8'] of [
    ['technician', '03_worker', 'technician'], ['miner', '03_worker', 'miner', 'GFX_job_miner', 'interface/planet_view.gfx', 'DXT5'],
    ['physicist', '02_specialist', 'physicist', 'GFX_job_physicist', 'interface/icons.gfx'],
    ['biologist', '02_specialist', 'biologist', 'GFX_job_biologist', 'interface/icons.gfx'],
    ['engineer', '02_specialist', 'engineer', 'GFX_job_engineer', 'interface/icons.gfx'],
    ['foundry', '02_specialist', 'foundry', 'GFX_job_foundry', 'interface/planet_view.gfx'],
    ['artisan', '02_specialist', 'artisan'], ['bureaucrat', '02_specialist', 'bureaucrat', 'GFX_job_bureaucrat', 'interface/planet_view.gfx'],
    ['politician', '01_ruler', 'politician', 'GFX_job_politician', 'interface/planet_view.gfx'],
    ['enforcer', '02_specialist', 'enforcer', 'GFX_job_enforcer', 'interface/planet_view.gfx'],
    ['replicator', '04_gestalt', 'replicator'], ['coordinator', '04_gestalt', 'coordinator'], ['patrol_drone', '04_gestalt', 'patrol_drone'],
    ['spawning_drone', '04_gestalt', 'spawning_drone'], ['synapse_drone', '04_gestalt', 'synapse_drone'],
    ['logistics_drone', '04_gestalt', 'low_tech_bureaucrat']
])
{
    const gfx = gfxId !== null;
    const spriteId = gfx ? gfxId : `local-job:${id}`;
    const spriteSource = gfx ? gfxPath : null;
    add('job', id, `common/pop_jobs/${definitionFile}_jobs.txt`, spriteId, spriteSource, `gfx/interface/icons/jobs/job_${icon}.dds`, 30, 30, null, 'spriteType', { x: 0, y: 0 }, codec, icon);
}
for (const [id, definitionFile, stratum, alias = null] of [
    ['clerk', '03_worker', 'worker', 'concept_clerks'], ['entertainer', '02_specialist', 'specialist'],
    ['healthcare', '02_specialist', 'specialist'], ['soldier', '02_specialist', 'specialist', 'concept_soldiers']
])
{
    add('job', id, `common/pop_jobs/${definitionFile}_jobs.txt`, `local-job:${id}`, null, `gfx/interface/icons/jobs/job_${id}.dds`, 30, 30, null, 'spriteType', { x: 0, y: 0 }, 'BGRA8', id, `concept_${id}`, stratum, alias);
}
for (const [id, spriteId, icon, width, height] of [
    ['energy', 'GFX_resource_energy', 'energy', 18, 18], ['minerals', 'GFX_resource_minerals', 'minerals', 18, 18], ['food', 'GFX_resource_food', 'food', 18, 18],
    ['alloys', 'GFX_resource_alloys', 'alloys', 20, 20], ['consumer_goods', 'GFX_resource_consumer_goods', 'consumer_goods', 19, 19],
    ['unity', 'GFX_resource_unity', 'unity', 18, 18], ['physics_research', 'GFX_resource_physics', 'physics_research', 18, 18],
    ['society_research', 'GFX_resource_society', 'society_research', 18, 18], ['engineering_research', 'GFX_resource_engineering', 'engineering_research', 18, 18],
    ['trade', 'GFX_resource_trade', 'trade', 20, 20]
])
{
    add('resource', id, 'common/strategic_resources/00_strategic_resources.txt', spriteId, 'interface/resources.gfx', `gfx/interface/icons/resources/${icon}.dds`, width, height);
}
add('ui', 'GFX_planetview_middle', null, 'GFX_planetview_middle', 'interface/planet_view.gfx', 'gfx/interface/planetview/planetview_middle.dds', 48, 48, null, 'corneredTileSpriteType', { x: 20, y: 20 });
add('ui', 'GFX_planet_building_bg', null, 'GFX_planet_building_bg', 'interface/planet_view.gfx', 'gfx/interface/icons/planet_backgrounds/planet_building_background.dds', 84, 114);
for (const [category, id, icon] of [
    ['blocker', 'd_failing_infrastructure', 'd_metal_boneyard'], ['blocker', 'd_decrepit_dwellings', 'd_slums'],
    ...['d_hot_springs', 'd_rushing_waterfalls', 'd_tempestous_mountain', 'd_veiny_cliffs', 'd_prosperous_mesa', 'd_rich_mountain', 'd_rugged_woods', 'd_fertile_lands', 'd_black_soil'].map(id => ['feature', id, id])
])
{
    add(category, id, `common/deposits/${category === 'blocker' ? '01_blocker' : '01_planetary'}_deposits.txt`, `local-deposit:${icon}`, null, `gfx/interface/icons/deposits/${icon}.dds`, 98, 75);
}

export const BASE_PLANET_IMAGES = Object.freeze([...records]);
records.push(...ORDINARY_PLANET_IMAGES);
// Prefix класса и порядок environment подтверждены штатным planet portrait.
for (const layer of ['sky', 'l01', 'l02', 'l03', 'l04'])
{
    add('environment', 'pc_continental.' + layer, null, 'local-environment:pc_continental_' + layer, null,
        'gfx/portraits/environments/pc_continental_' + layer + '.dds', 800, 400);
}
export const PLANET_IMAGE_DEFINITIONS = Object.freeze(records);
export const PLANET_ASSET_PATHS = Object.freeze([...new Set([...records.flatMap(record => [record.definitionPath, record.inlinePath, record.spriteSource, record.sourcePath, record.conceptId ? 'common/game_concepts/01_job_concepts.txt' : null]).filter(Boolean), ...CONTINENTAL_ENVIRONMENT_PATHS])].sort());
export const MAX_PLANET_DECODED_BYTES = 16 * 1024 * 1024;
const fail = message => { throw new Error(`Planet assets: ${message}`); };
const equal = (actual, expected, label) => { if (actual !== expected) { fail(`Unexpected ${label}.`); } };

export function validatePlanetImageInfo(path, info)
{
    const record = records.find(record => record.sourcePath === path);
    if (!record) { fail('Unknown image path.'); }
    for (const [key, expected] of Object.entries({ width: record.width, height: record.height, codec: record.codec, mipCount: 1, faceCount: 1 })) { equal(info[key], expected, `${record.key}.${key}`); }
}

export function parsePlanetImageMetadata(texts)
{
    if (!(texts instanceof Map)) { fail('Source text Map is required.'); }
    const environment = parseContinentalEnvironmentMetadata(texts);
    const asts = new Map();
    const ast = path =>
    {
        if (!asts.has(path)) { asts.set(path, parseVisualDefinitions(texts.get(path), { allowQuotedWhitespace: path.startsWith('common/') })); }
        return asts.get(path);
    };
    const result = new Map();
    for (const record of records)
    {
        if (record.category === 'environment')
        {
            const binding = environment.get(record.key);
            equal(binding?.sourcePath, record.sourcePath, record.key + '.source path');
            result.set(record.key, { ...binding });
            continue;
        }
        if (record.definitionPath)
        {
            const definition = requireBlock(ast(record.definitionPath), record.id);
            if (record.category === 'zone')
            {
                let iconFields = definition;
                if (record.inlinePath)
                {
                    const script = record.inlinePath.slice('common/inline_scripts/'.length, -4);
                    const bindings = definition.filter(entry => entry.key === 'inline_script' && entry.operator === '=' && Array.isArray(entry.value) && entry.value.some(field => field.key === 'script' && field.operator === '=' && field.value === script));
                    equal(bindings.length, 1, record.key + '.inline');
                    equal(requireScalar(bindings[0].value, 'script'), script, record.key + '.inline script');
                    if (definition.some(entry => entry.key === 'icon')) { fail(`Ambiguous direct/inline icon: ${record.key}.`); }
                    iconFields = ast(record.inlinePath);
                }
                equal(requireScalar(iconFields, 'icon'), record.spriteId, record.key + '.icon');
            }
            else if (record.category === 'building')
            {
                // Local asset path follows the definition's explicit icon override when one exists.
                equal(optionalScalar(definition, 'icon', record.id), record.sourceIcon, record.key + '.icon');
            }
            else if (record.category === 'job')
            {
                if (record.sourceStratum) { equal(requireScalar(definition, 'category'), record.sourceStratum, record.key + '.stratum'); }
                const swappable = definition.filter(entry => entry.key === 'swappable_data' && entry.operator === '=' && Array.isArray(entry.value));
                if (swappable.length > 1) { fail(`Duplicate swappable_data: ${record.key}.`); }
                const defaults = swappable.length ? swappable[0].value.filter(entry => entry.key === 'default' && entry.operator === '=' && Array.isArray(entry.value)) : [];
                if (defaults.length > 1) { fail(`Duplicate default job data: ${record.key}.`); }
                const iconFields = defaults.length ? defaults[0].value : definition;
                equal(optionalScalar(iconFields, 'icon', optionalScalar(definition, 'icon', record.id)), record.sourceIcon, record.key + '.icon');
                if (record.conceptId)
                {
                    const concept = requireBlock(ast('common/game_concepts/01_job_concepts.txt'), record.conceptId);
                    equal(requireScalar(concept, 'icon'), record.sourcePath, record.key + '.concept icon');
                    const aliases = concept.filter(entry => entry.key === 'alias' && entry.operator === '=' && Array.isArray(entry.value));
                    if (aliases.length !== (record.conceptAlias ? 1 : 0)) { fail(`Unexpected concept alias count: ${record.key}.`); }
                    if (record.conceptAlias)
                    {
                        equal(aliases[0].value.length, 1, record.key + '.concept alias cardinality');
                        equal(aliases[0].value[0].value, record.conceptAlias, record.key + '.concept alias');
                    }
                }
            }
            else if (record.category === 'district')
            {
                equal(optionalScalar(definition, 'icon', record.id), record.id, record.key + '.icon');
            }
            else if (record.category === 'blocker' || record.category === 'feature')
            {
                equal(optionalScalar(definition, 'icon', record.id), record.sourcePath.split('/').at(-1).slice(0, -4), record.key + '.icon');
            }
        }
        if (record.spriteSource)
        {
            const fields = selectNamedDefinition(requireBlock(ast(record.spriteSource), 'spriteTypes'), record.type, record.spriteId);
            if (fields.some(field => !['name', 'texturefile', 'textureFile', 'noOfFrames', 'borderSize', 'effectFile'].includes(field.key))) { fail(`Unsupported sprite fields: ${record.key}.`); }
            const paths = fields.filter(field => field.key?.toLowerCase() === 'texturefile');
            equal(paths.length, 1, record.key + '.texture count');
            equal(paths[0].operator, '=', record.key + '.texture operator');
            equal(paths[0].value, record.sourcePath, record.key + '.texture path');
            equal(optionalScalar(fields, 'noOfFrames', 1), 1, record.key + '.frames');
            if (record.type === 'corneredTileSpriteType')
            {
                const border = requireBlock(fields, 'borderSize');
                equal(border.length, 2, record.key + '.border');
                equal(requireScalar(border, 'x'), record.border.x, record.key + '.border.x');
                equal(requireScalar(border, 'y'), record.border.y, record.key + '.border.y');
                equal(requireScalar(fields, 'effectFile'), 'gfx/FX/buttonstate_onlydisable.shader', record.key + '.effect');
            }
            else if (fields.some(field => ['effectFile', 'borderSize'].includes(field.key))) { fail(`Unsupported sprite behavior: ${record.key}.`); }
        }
        result.set(record.key, { sourcePath: record.sourcePath, spriteId: record.spriteId, frames: 1, frame: 1, border: record.border });
    }
    return result;
}
