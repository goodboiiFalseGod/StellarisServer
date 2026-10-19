import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { PLANET_IMAGE_DEFINITIONS, PLANET_ASSET_PATHS, parsePlanetImageMetadata, validatePlanetImageInfo } from './m3-planet-assets.mjs';
import { inspectDds, decodeDds } from './formats.mjs';

const root = process.env.STELLARIS_GAME_ROOT;
const texts = async () => new Map(await Promise.all(PLANET_ASSET_PATHS.filter(path => !path.endsWith('.dds')).map(async path => [path, await readFile(resolve(root, path), 'utf8')])));

test('Planet inventory: immutable unique IDs and source closure', () =>
{
    assert.equal(PLANET_IMAGE_DEFINITIONS.length, 296);
    assert.equal(new Set(PLANET_IMAGE_DEFINITIONS.map(record => record.key)).size, 296);
    assert.equal(PLANET_ASSET_PATHS.length, 322);
    assert.ok(PLANET_IMAGE_DEFINITIONS.some(record => record.key === 'job.merchant'));
    assert.ok(PLANET_ASSET_PATHS.includes('gfx/interface/icons/jobs/job_merchant.dds'));
    assert.ok(Object.isFrozen(PLANET_ASSET_PATHS));
    assert.ok(PLANET_IMAGE_DEFINITIONS.every(record => Object.isFrozen(record) && Object.isFrozen(record.border)));
    assert.ok(!PLANET_ASSET_PATHS.some(path => /shader|\.\.|\\/.test(path)));
    assert.throws(() => validatePlanetImageInfo('unknown.dds', {}), /Unknown/);
    assert.throws(() => parsePlanetImageMetadata({}), /Map/);
});

test('Real 4.5.2: all selected roots, transitive bindings, formats and icon overrides', { skip: !root }, async () =>
{
    const metadata = parsePlanetImageMetadata(await texts());
    assert.equal(metadata.size, 296);
    assert.equal(metadata.get('blocker.d_failing_infrastructure').sourcePath, 'gfx/interface/icons/deposits/d_metal_boneyard.dds');
    assert.equal(metadata.get('blocker.d_decrepit_dwellings').sourcePath, 'gfx/interface/icons/deposits/d_slums.dds');
    assert.equal(metadata.get('zone.zone_factory').sourcePath, 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_artisan_industry.dds');
    assert.equal(metadata.get('zone.zone_food').sourcePath, 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_food_market.dds');
    const selected = [
        ['job.technician', 'gfx/interface/icons/jobs/job_technician.dds', 'local-job:technician'],
        ['job.miner', 'gfx/interface/icons/jobs/job_miner.dds', 'GFX_job_miner'],
        ['job.farmer', 'gfx/interface/icons/jobs/job_farmer.dds', 'local-job:farmer'],
        ['job.physicist', 'gfx/interface/icons/jobs/job_physicist.dds', 'GFX_job_physicist'],
        ['job.biologist', 'gfx/interface/icons/jobs/job_biologist.dds', 'GFX_job_biologist'],
        ['job.engineer', 'gfx/interface/icons/jobs/job_engineer.dds', 'GFX_job_engineer'],
        ['job.foundry', 'gfx/interface/icons/jobs/job_foundry.dds', 'GFX_job_foundry'],
        ['job.artisan', 'gfx/interface/icons/jobs/job_artisan.dds', 'local-job:artisan'],
        ['job.bureaucrat', 'gfx/interface/icons/jobs/job_bureaucrat.dds', 'GFX_job_bureaucrat'],
        ['job.politician', 'gfx/interface/icons/jobs/job_politician.dds', 'GFX_job_politician'],
        ['job.enforcer', 'gfx/interface/icons/jobs/job_enforcer.dds', 'GFX_job_enforcer'],
        ...['replicator', 'coordinator', 'patrol_drone', 'spawning_drone', 'synapse_drone', 'logistics_drone'].map(id => [`job.${id}`, `gfx/interface/icons/jobs/job_${id === 'logistics_drone' ? 'low_tech_bureaucrat' : id}.dds`, `local-job:${id}`]),
        ...['luxury_residence', 'temple', 'hive_capital', 'spawning_pool', 'offspring_nest', 'hive_warren', 'machine_capital', 'machine_assembly_plant', 'drone_storage'].map(id => [`building.building_${id}`, `gfx/interface/icons/buildings/${id === 'machine_assembly_plant' ? 'building_robot_assembly_plant' : `building_${id}`}.dds`])
    ];
    for (const [key, sourcePath, spriteId] of selected)
    {
        const record = PLANET_IMAGE_DEFINITIONS.find(item => item.key === key);
        assert.ok(record, `Missing selected planet asset ${key}.`);
        assert.equal(record.sourcePath, sourcePath, key);
        if (spriteId) { assert.equal(record.spriteId, spriteId, key); }
        assert.equal(metadata.get(key).sourcePath, sourcePath, key);
    }
    for (const [id, spriteId, sourcePath, width, height] of [
        ['alloys', 'GFX_resource_alloys', 'gfx/interface/icons/resources/alloys.dds', 20, 20],
        ['consumer_goods', 'GFX_resource_consumer_goods', 'gfx/interface/icons/resources/consumer_goods.dds', 19, 19],
        ['unity', 'GFX_resource_unity', 'gfx/interface/icons/resources/unity.dds', 18, 18],
        ['physics_research', 'GFX_resource_physics', 'gfx/interface/icons/resources/physics_research.dds', 18, 18],
        ['society_research', 'GFX_resource_society', 'gfx/interface/icons/resources/society_research.dds', 18, 18],
        ['engineering_research', 'GFX_resource_engineering', 'gfx/interface/icons/resources/engineering_research.dds', 18, 18],
        ['trade', 'GFX_resource_trade', 'gfx/interface/icons/resources/trade.dds', 20, 20],
        ['energy', 'GFX_resource_energy', 'gfx/interface/icons/resources/energy.dds', 18, 18],
        ['minerals', 'GFX_resource_minerals', 'gfx/interface/icons/resources/minerals.dds', 18, 18],
        ['food', 'GFX_resource_food', 'gfx/interface/icons/resources/food.dds', 18, 18]
    ])
    {
        const record = PLANET_IMAGE_DEFINITIONS.find(item => item.key === `resource.${id}`);
        assert.ok(record, `Missing resource image key ${id}.`);
        assert.deepEqual([record.spriteId, record.sourcePath, record.width, record.height], [spriteId, sourcePath, width, height]);
        assert.equal(metadata.get(`resource.${id}`).sourcePath, sourcePath);
    }
    const arkBindings = [
        ['district.district_ark_city', 'gfx/interface/icons/districts/district_ark_city.dds', 'local-district:district_ark_city', 'common/districts/07_ark_districts.txt', null],
        ['district.district_ark_generator', 'gfx/interface/icons/districts/district_ark_generator.dds', 'local-district:district_ark_generator', 'common/districts/07_ark_districts.txt', null],
        ['district.district_ark_basic', 'gfx/interface/icons/districts/district_ark_basic.dds', 'local-district:district_ark_basic', 'common/districts/07_ark_districts.txt', null],
        ['district.district_ark_military', 'gfx/interface/icons/districts/district_ark_military.dds', 'local-district:district_ark_military', 'common/districts/07_ark_districts.txt', null],
        ['district.district_hive', 'gfx/interface/icons/districts/district_hive.dds', 'local-district:district_hive', 'common/districts/00_urban_districts.txt', null],
        ['district.district_nexus', 'gfx/interface/icons/districts/district_nexus.dds', 'local-district:district_nexus', 'common/districts/00_urban_districts.txt', null],
        ['zone.zone_ark_research_unity', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_culture_worker.dds', 'GFX_district_specialization_ark_research_unity_consumer_goods', 'common/zones/05_arkship_zones.txt', null],
        ['zone.zone_ark_industrial', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_industrial.dds', 'GFX_district_specialization_ark_research_alloys_support', 'common/zones/05_arkship_zones.txt', null],
        ['zone.zone_ark_harvest', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_minerals.dds', 'GFX_district_specialization_minerals', 'common/zones/05_arkship_zones.txt', 'common/inline_scripts/zones/shared_ark_harvest_zone.txt'],
        ['zone.zone_ark_food', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_food_market.dds', 'GFX_district_specialization_food', 'common/zones/05_arkship_zones.txt', 'common/inline_scripts/zones/shared_food_zone.txt'],
        ['zone.zone_ark_reactor_physics', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_physicist.dds', 'GFX_district_specialization_physics', 'common/zones/05_arkship_zones.txt', 'common/inline_scripts/zones/shared_research_physics_zone.txt'],
        ['zone.zone_ark_farm_society', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_biology.dds', 'GFX_district_specialization_society', 'common/zones/05_arkship_zones.txt', 'common/inline_scripts/zones/shared_research_society_zone.txt'],
        ['zone.zone_ark_military_engineering', 'gfx/interface/icons/districts/district_specialization_icons/district_specialization_engineer.dds', 'GFX_district_specialization_engineering', 'common/zones/05_arkship_zones.txt', 'common/inline_scripts/zones/shared_research_engineering_zone.txt']
    ];
    for (const [key, sourcePath, spriteId, definitionPath, inlinePath] of arkBindings)
    {
        const record = PLANET_IMAGE_DEFINITIONS.find(item => item.key === key);
        assert.ok(record, `Missing Ark image key ${key}.`);
        assert.deepEqual([record.sourcePath, record.spriteId, record.definitionPath, record.inlinePath], [sourcePath, spriteId, definitionPath, inlinePath]);
        assert.equal(metadata.get(key).sourcePath, sourcePath);
    }
    let bytes = 0;
    const uniqueBytes = new Map();
    const districtHashKeys = new Set(['district.district_city', 'district.district_ark_city', 'district.district_ark_generator', 'district.district_ark_basic', 'district.district_ark_military', 'district.district_hive', 'district.district_nexus']);
    const districtHashes = [];
    for (const record of PLANET_IMAGE_DEFINITIONS)
    {
        const source = await readFile(resolve(root, record.sourcePath));
        const info = inspectDds(source.buffer.slice(source.byteOffset, source.byteOffset + source.byteLength));
        validatePlanetImageInfo(record.sourcePath, info);
        bytes += info.decodedBytes;
        uniqueBytes.set(record.sourcePath, info.decodedBytes);
        if (districtHashKeys.has(record.key)) { districtHashes.push(createHash('sha256').update(decodeDds(source.buffer.slice(source.byteOffset, source.byteOffset + source.byteLength)).rgba).digest('hex')); }
        assert.deepEqual(metadata.get(record.key), { sourcePath: record.sourcePath, spriteId: record.spriteId, frames: 1, frame: 1, border: record.border });
        for (const [key, value] of Object.entries({ width: info.width + 1, height: info.height + 1, codec: info.codec === 'DXT1' ? 'DXT5' : 'DXT1', faceCount: 6, mipCount: 2 }))
        {
            assert.throws(() => validatePlanetImageInfo(record.sourcePath, { ...info, [key]: value }), new RegExp(key));
        }
    }
    assert.equal(bytes, 11960524);
    assert.equal([...uniqueBytes.values()].reduce((sum, value) => sum + value, 0), 11412892);
    assert.equal(districtHashes.length, districtHashKeys.size);
    assert.equal(new Set(districtHashes).size, districtHashKeys.size);
});

test('Real bindings reject missing/duplicate roots, changed inline scripts/icons and sprite behavior', { skip: !root }, async () =>
{
    const original = await texts();
    for (const [path, mutate] of [
        ['common/districts/00_urban_districts.txt', text => text + '\ndistrict_city = {}'],
        ['common/districts/00_urban_districts.txt', text => text.replace('district_city = {', 'district_city = { icon = wrong_icon')],
        ['common/districts/00_urban_districts.txt', text => text.replace('district_city =', 'missing_city =')],
        ['common/zones/00_zones.txt', text => text.replace('zones/shared_industrial_zone', 'zones/shared_unknown_zone')],
        ['common/zones/00_zones.txt', text => text.replace('script = zones/shared_industrial_zone', 'script = zones/shared_industrial_zone script = zones/unknown')],
        ['common/inline_scripts/zones/shared_industrial_zone.txt', text => text.replace('GFX_district_specialization_industrial', 'GFX_wrong')],
        ['common/buildings/00_capital_buildings.txt', text => text.replace('building_colony_shelter = {', 'building_colony_shelter = { icon = other')],
        ['common/buildings/01_pop_assembly_buildings.txt', text => text.replace('icon = building_robot_assembly_plant', 'icon = building_machine_assembly_plant')],
        ['common/pop_jobs/03_worker_jobs.txt', text => text.replace('icon = miner', 'icon = wrong_miner')],
        ['common/game_concepts/01_job_concepts.txt', text => text.replace('gfx/interface/icons/jobs/job_clerk.dds', 'gfx/interface/icons/jobs/job_wrong.dds')],
        ['common/game_concepts/01_job_concepts.txt', text => text.replace('concept_clerks', 'concept_wrong_clerks')],
        ['common/strategic_resources/00_strategic_resources.txt', text => text + '\nalloys = {}'],
        ['interface/resources.gfx', text => text.replace('name = "GFX_resource_physics"\n\t\ttextureFile = "gfx/interface/icons/resources/physics_research.dds"', 'name = "GFX_resource_physics"\n\t\ttextureFile = "gfx/interface/icons/resources/engineering_research.dds"')],
        ['common/pop_jobs/03_worker_jobs.txt', text => text + '\nfarmer = {}'],
        ['common/deposits/01_blocker_deposits.txt', text => text.replace('icon = d_metal_boneyard', 'icon = d_slums')],
        ['interface/planet_view.gfx', text => text.replace('gfx/interface/icons/districts/district_city.dds', '../outside.dds')],
        ['interface/planet_view.gfx', text => text.replace('name = "GFX_district_cities"', 'name = "GFX_district_cities" noOfFrames = 2')],
        ['interface/planet_view.gfx', text => text.replace('name = "GFX_district_cities"', 'name = "GFX_district_cities" noofframes = 2')],
        ['interface/planet_view.gfx', text => text.replace('name = "GFX_district_cities"', 'name = "GFX_district_cities" effectfile = "unknown.shader"')],
        ['interface/planet_view.gfx', text => text.replace('name = "GFX_district_cities"', 'name = "GFX_district_cities" bordersize = { x = 0 y = 0 }')],
        ['interface/planet_view.gfx', text => text.replace('name = "GFX_district_cities"', 'name = "GFX_district_cities" animation = {}')],
        ['interface/planet_view.gfx', text => text.replace('name = "GFX_planetview_middle"', 'name = "GFX_planetview_middle" borderSize = { x = 20 y = 20 }')],
        ['common/districts/07_ark_districts.txt', text => text + '\ndistrict_ark_city = {}'],
        ['common/districts/07_ark_districts.txt', text => text.replace('district_ark_generator = {', 'missing_ark_generator = {')],
        ['common/districts/07_ark_districts.txt', text => text.replace('district_ark_basic = {', 'district_ark_basic = { icon = district_ark_wrong')],
        ['common/districts/00_urban_districts.txt', text => text.replace('district_hive = {', 'district_hive = { icon = district_hive_wrong')],
        ['common/zones/05_arkship_zones.txt', text => text.replace('icon = GFX_district_specialization_ark_research_unity_consumer_goods', 'icon = GFX_district_specialization_wrong')],
        ['common/zones/05_arkship_zones.txt', text => text.replace('script = zones/shared_ark_harvest_zone', 'script = zones/shared_wrong_harvest_zone')],
        ['common/zones/05_arkship_zones.txt', text => text.replace('script = zones/shared_food_zone', 'script = zones/shared_wrong_food_zone')],
        ['common/zones/05_arkship_zones.txt', text => text.replace('script = zones/shared_research_physics_zone', 'script = zones/shared_wrong_physics_zone')],
        ['common/inline_scripts/zones/shared_ark_harvest_zone.txt', text => text.replace('GFX_district_specialization_minerals', 'GFX_district_specialization_wrong')],
        ['common/inline_scripts/zones/shared_food_zone.txt', text => text.replace('GFX_district_specialization_food', 'GFX_district_specialization_wrong')],
        ['interface/icons.gfx', text => text.replace('name = "GFX_district_specialization_ark_research_alloys_support"\n\t\ttextureFile = "gfx/interface/icons/districts/district_specialization_icons/district_specialization_industrial.dds"', 'name = "GFX_district_specialization_ark_research_alloys_support"\n\t\ttextureFile = "gfx/interface/icons/districts/district_specialization_icons/district_specialization_minerals.dds"')]
    ])
    {
        const changed = new Map(original), modified = mutate(original.get(path));
        assert.notEqual(modified, original.get(path), path);
        changed.set(path, modified);
        assert.throws(() => parsePlanetImageMetadata(changed), Error, path);
    }
    for (const path of PLANET_ASSET_PATHS.filter(path => !path.endsWith('.dds')))
    {
        const changed = new Map(original); changed.delete(path);
        assert.throws(() => parsePlanetImageMetadata(changed), Error, path);
    }
});
