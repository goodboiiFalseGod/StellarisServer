import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { parseVisualDefinitions, selectNamedDefinition, requireScalar, optionalScalar, requireBlock } from './visual-definitions.mjs';

test('Текстовые определения: quotes, comments, escapes, числа, списки и повторяемые блоки', () =>
{
    const entries = parseVisualDefinitions('\ufeff# комментарий\r\nentity={ name="a#{}=b" value="\\\"\\\\\\n\\r\\t" position={ 0 -1.5 +2e1 } state={name=idle} state={name=death} }');
    const entity = selectNamedDefinition(entries, 'entity', 'a#{}=b');
    assert.equal(requireScalar(entity, 'value'), '"\\\n\r\t');
    assert.deepEqual(requireBlock(entity, 'position').map(entry => entry.value), [0, -1.5, 20]);
    assert.equal(entity.filter(entry => entry.key === 'state').length, 2);
    assert.equal(requireScalar(selectNamedDefinition(entity, 'state', 'death'), 'name'), 'death');
    assert.equal(optionalScalar(entity, 'scale', 1), 1);
    assert.equal(requireScalar(parseVisualDefinitions('__proto__ = preserved'), '__proto__'), 'preserved');
});

test('Helpers отклоняют missing, duplicate, скаляр вместо блока и неверный оператор', () =>
{
    const entries = parseVisualDefinitions('entity={name=a} entity={name=a} scale=1 scale=2 block={} compare>=3');
    assert.throws(() => selectNamedDefinition(entries, 'entity', 'a'), /найдено 2/);
    assert.throws(() => selectNamedDefinition(entries, 'entity', 'missing'), /найдено 0/);
    assert.throws(() => requireScalar(entries, 'missing'), /найдено 0/);
    assert.throws(() => optionalScalar(entries, 'scale', 1), /найдено 2/);
    assert.throws(() => requireScalar(entries, 'block'), /скаляр/);
    assert.throws(() => requireBlock(parseVisualDefinitions('scale=1'), 'scale'), /блок/);
    assert.throws(() => requireScalar(entries, 'compare'), /оператор =/);
    assert.throws(() => selectNamedDefinition(parseVisualDefinitions('entity={name=a name=b}'), 'entity', 'a'), /найдено 2/);
    assert.throws(() => selectNamedDefinition(parseVisualDefinitions('entity=a'), 'entity', 'a'), /блок/);
    assert.throws(() => parseVisualDefinitions(null), TypeError);
});

test('Повреждённый текст явно отклоняется без исполнения unknown state', () =>
{
    for (const text of ['x={', '}', 'x="unterminated', 'x="escape\\', 'x="\\q"', 'x="line\nbreak"', 'x=\0', 'x=', 'x==1', '=x', 'x={ y= }', 'x=1e999'])
    {
        assert.throws(() => parseVisualDefinitions(text), Error, text);
    }
    const comparison = requireBlock(parseVisualDefinitions('unknown={ trigger={size>=a size!=b} script="globalThis.x=1" }'), 'unknown');
    assert.deepEqual(requireBlock(comparison, 'trigger').map(entry => entry.operator), ['>=', '!=']);
    assert.equal(requireScalar(comparison, 'script'), 'globalThis.x=1');
});

test('Защитные лимиты bytes/tokens/depth проверены по обе стороны границы', () =>
{
    const maxBytes = 16 * 1024 * 1024;
    assert.deepEqual(parseVisualDefinitions('#' + 'x'.repeat(maxBytes - 1)), []);
    assert.throws(() => parseVisualDefinitions('#' + 'x'.repeat(maxBytes)), /16 MiB/);
    assert.throws(() => parseVisualDefinitions('#' + 'я'.repeat(maxBytes / 2)), /16 MiB/);
    assert.equal(parseVisualDefinitions('x '.repeat(250000)).length, 250000);
    assert.throws(() => parseVisualDefinitions('x '.repeat(250001)), /250000 tokens/);
    assert.equal(parseVisualDefinitions('{'.repeat(32) + '0' + '}'.repeat(32)).length, 1);
    assert.throws(() => parseVisualDefinitions('{'.repeat(33) + '0' + '}'.repeat(33)), /nesting 32/);
});

const gameRoot = process.env.STELLARIS_GAME_ROOT;
test('Common definition quoted whitespace is opt-in and retains parser safety limits', () =>
{
    const text = 'description="line\r\nnext\tcolumn"';
    assert.throws(() => parseVisualDefinitions(text));
    assert.equal(requireScalar(parseVisualDefinitions(text, { allowQuotedWhitespace: true }), 'description'), 'line\r\nnext\tcolumn');
    for (const control of ['\0', '\x01', '\x0b', '\x7f'])
    {
        assert.throws(() => parseVisualDefinitions(`x="a${control}b"`, { allowQuotedWhitespace: true }));
    }
    assert.throws(() => parseVisualDefinitions('x="\\q"', { allowQuotedWhitespace: true }));
    assert.throws(() => parseVisualDefinitions('{'.repeat(33) + '0' + '}'.repeat(33), { allowQuotedWhitespace: true }), /nesting/);
    assert.throws(() => parseVisualDefinitions('x '.repeat(250001), { allowQuotedWhitespace: true }), /tokens/);
});

test('Реальная 4.5.2: корвет, planet/star overrides и современная составная starbase', { skip: !gameRoot }, async () =>
{
    const load = async relativePath => parseVisualDefinitions(await readFile(resolve(gameRoot, relativePath), 'utf8'));
    const shipFolder = 'gfx/models/ships/mammalian_01/';
    const ships = await load(shipFolder + '_mammalian_01_ships_entities.asset');
    const corvette = selectNamedDefinition(ships, 'entity', 'mammalian_01_corvette_S1_entity');
    assert.equal(requireScalar(corvette, 'pdxmesh'), 'mammalian_01_corvette_S1_mesh');
    const shipMeshes = requireBlock(await load(shipFolder + '_mammalian_01_ships_meshes.gfx'), 'objectTypes');
    const corvetteMesh = selectNamedDefinition(shipMeshes, 'pdxmesh', requireScalar(corvette, 'pdxmesh'));
    assert.equal(requireScalar(corvetteMesh, 'file'), shipFolder + 'mammalian_01_corvette_S1.mesh');
    assert.equal(requireScalar(corvetteMesh, 'scale'), 1);
    assert.equal(requireScalar(requireBlock(corvetteMesh, 'meshsettings'), 'shader'), 'PdxMeshShip');

    const planetFolder = 'gfx/models/planets/';
    const planet = selectNamedDefinition(await load(planetFolder + '_planetary_entities.asset'), 'entity', 'continental_planet_01_entity');
    assert.equal(requireScalar(planet, 'pdxmesh'), 'planet_clouded_mesh');
    assert.equal(optionalScalar(planet, 'scale', 1), 1);
    assert.equal(planet.filter(entry => entry.key === 'meshsettings').length, 4);
    for (const [shape, diffuse, normal, specular] of [
        ['planet_geosphereShape', 'continental_01_diffuse.dds', 'continental_01_normals.dds', 'continental_01_spec.dds'],
        ['polesShape', 'poles_1_diffuse.dds', 'poles_1_normal.dds', 'poles_1_specular.dds'],
        ['clouds_geosphereShape', 'clouds_medium_diffuse.dds', 'clouds_medium_normal.dds', 'clouds_medium_specular.dds'],
        ['clouds_shadow_geosphereShape', 'clouds_medium_shadow_diffuse.dds', 'nonormal.dds', 'nospec.dds']
    ])
    {
        const override = selectNamedDefinition(planet, 'meshsettings', shape);
        assert.equal(requireScalar(override, 'texture_diffuse'), diffuse);
        assert.equal(requireScalar(override, 'texture_normal'), normal);
        assert.equal(requireScalar(override, 'texture_specular'), specular);
    }
    const planetMesh = selectNamedDefinition(requireBlock(await load(planetFolder + '_planetary_meshes.gfx'), 'objectTypes'), 'pdxmesh', 'planet_clouded_mesh');
    assert.equal(requireScalar(planetMesh, 'file'), planetFolder + 'planet_clouded.mesh');
    assert.equal(requireScalar(planetMesh, 'scale'), 1);
    const star = selectNamedDefinition(await load(planetFolder + '_star_entities.asset'), 'entity', 'g_star_class_star_entity');
    assert.equal(requireScalar(star, 'pdxmesh'), 'star_mesh');
    assert.equal(requireScalar(star, 'scale'), 1.3);
    const starMesh = selectNamedDefinition(requireBlock(await load(planetFolder + '_star_meshes.gfx'), 'objectTypes'), 'pdxmesh', 'star_mesh');
    assert.equal(requireScalar(starMesh, 'file'), planetFolder + 'base_star.mesh');
    assert.equal(requireScalar(starMesh, 'scale'), 1);

    const stationFolder = 'gfx/models/ships/starbases/';
    const stationEntities = await load(stationFolder + '_starbase_entities.asset');
    const stationMeshes = requireBlock(await load(stationFolder + '_starbase_meshes.gfx'), 'objectTypes');
    for (const [name, meshName, file] of [
        ['mammalian_01_starbase_outpost_entity', 'mammalian_01_outpost_frame_mesh', 'mammalian_01_outpost_frame.mesh'],
        ['mammalian_01_starbase_outpost_section_entity', 'mammalian_01_outpost_mesh', 'mammalian_01_outpost.mesh']
    ])
    {
        const entity = selectNamedDefinition(stationEntities, 'entity', name);
        assert.equal(requireScalar(entity, 'pdxmesh'), meshName);
        assert.equal(optionalScalar(entity, 'scale', 1), 1);
        assert.equal(entity.filter(entry => entry.key === 'attach').length, 0);
        const mesh = selectNamedDefinition(stationMeshes, 'pdxmesh', meshName);
        assert.equal(requireScalar(mesh, 'file'), stationFolder + file);
        assert.equal(requireScalar(mesh, 'scale'), 1);
    }
    const outpost = requireBlock(await load('common/ship_sizes/00_starbases.txt'), 'starbase_outpost');
    const slots = requireBlock(outpost, 'section_slots');
    assert.equal(slots.length, 1);
    assert.equal(requireScalar(requireBlock(slots, 'core'), 'locator'), 'part1');
    const section = selectNamedDefinition(await load('common/section_templates/starbase.txt'), 'ship_section_template', 'OUTPOST_STARBASE_SECTION', 'key');
    assert.equal(requireScalar(section, 'ship_size'), 'starbase_outpost');
    assert.equal(requireScalar(section, 'fits_on_slot'), 'core');
    assert.equal(requireScalar(section, 'entity'), 'starbase_outpost_section_entity');
});
