import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { inspectDds } from './formats.mjs';
import { CONTINENTAL_ENVIRONMENT_PATHS, parseContinentalEnvironmentMetadata } from './m3-planet-environment.mjs';

const classPath = CONTINENTAL_ENVIRONMENT_PATHS[0];
const portraitPath = CONTINENTAL_ENVIRONMENT_PATHS[1];
const fixture = new Map([
    [classPath, 'pc_unrelated = { picture = other } pc_continental = { entity = continental_planet }'],
    [portraitPath, `planet = {
        size = { x = 800 y = 400 }
        sky = { orientation = lower_left position = { x = 0 y = 0 } }
        environment = { orientation = lower_left position = { x = 0 y = 0 } }
        city = { orientation = lower_right position = { x = 20 y = 30 } min_pop = 999 }
        environment = { orientation = lower_left position = { x = 0 y = 0 } }
        environment = { orientation = lower_left position = { x = 0 y = 0 } }
        environment = { orientation = lower_left position = { x = 0 y = 0 } }
    }`]
]);

test('Continental metadata binds only five stable local environment layers', () =>
{
    const metadata = parseContinentalEnvironmentMetadata(fixture);
    assert.deepEqual([...metadata.keys()], ['environment.pc_continental.sky', 'environment.pc_continental.l01', 'environment.pc_continental.l02', 'environment.pc_continental.l03', 'environment.pc_continental.l04']);
    for (const [index, layer] of ['sky', 'l01', 'l02', 'l03', 'l04'].entries())
    {
        assert.deepEqual(metadata.get(`environment.pc_continental.${layer}`), {
            sourcePath: `gfx/portraits/environments/pc_continental_${layer}.dds`,
            spriteId: `local-environment:pc_continental_${layer}`,
            frames: 1,
            frame: 1,
            border: { x: 0, y: 0 }
        });
        assert.equal(CONTINENTAL_ENVIRONMENT_PATHS[index + 2], metadata.get(`environment.pc_continental.${layer}`).sourcePath);
    }
});

test('Metadata rejects missing, duplicate, unknown class roots and changed bindings', () =>
{
    const missingRoot = new Map(fixture);
    missingRoot.delete(classPath);
    assert.throws(() => parseContinentalEnvironmentMetadata(missingRoot), /Missing source text/);
    assert.throws(() => parseContinentalEnvironmentMetadata(new Map([[classPath, fixture.get(classPath)]])), /Missing source text/);
    const duplicateClass = new Map(fixture);
    duplicateClass.set(classPath, `${fixture.get(classPath)} pc_continental = {}`);
    assert.throws(() => parseContinentalEnvironmentMetadata(duplicateClass), /Поле pc_continental: ожидалась одна запись/);
    const unknownClass = new Map(fixture);
    unknownClass.set(classPath, 'pc_other = {}');
    assert.throws(() => parseContinentalEnvironmentMetadata(unknownClass), /Поле pc_continental: ожидалась одна запись/);
    const override = new Map(fixture);
    override.set(classPath, 'pc_continental = { picture = pc_gaia }');
    assert.throws(() => parseContinentalEnvironmentMetadata(override), /picture override/);
});

test('Metadata rejects incomplete or changed portrait layer contracts', () =>
{
    const missingLayer = new Map(fixture);
    missingLayer.set(portraitPath, fixture.get(portraitPath).replace(/\n\s*environment = \{ orientation = lower_left position = \{ x = 0 y = 0 \} \}\n\s*\}/, '\n    }'));
    assert.throws(() => parseContinentalEnvironmentMetadata(missingLayer), /exactly four environment layers/);
    const duplicateSky = new Map(fixture);
    duplicateSky.set(portraitPath, fixture.get(portraitPath).replace('environment = { orientation = lower_left position = { x = 0 y = 0 } }', 'sky = { orientation = lower_left position = { x = 0 y = 0 } }', 1));
    assert.throws(() => parseContinentalEnvironmentMetadata(duplicateSky), /exactly four environment layers/);
    const duplicateRoot = new Map(fixture);
    duplicateRoot.set(portraitPath, `${fixture.get(portraitPath)} planet = { size = { x = 800 y = 400 } }`);
    assert.throws(() => parseContinentalEnvironmentMetadata(duplicateRoot), /Поле planet: ожидалась одна запись/);
    const changedPosition = new Map(fixture);
    changedPosition.set(portraitPath, fixture.get(portraitPath).replace('position = { x = 0 y = 0 }', 'position = { x = 1 y = 0 }'));
    assert.throws(() => parseContinentalEnvironmentMetadata(changedPosition), /position must remain/);
    const changedSize = new Map(fixture);
    changedSize.set(portraitPath, fixture.get(portraitPath).replace('x = 800', 'x = 801'));
    assert.throws(() => parseContinentalEnvironmentMetadata(changedSize), /800x400/);
});

const gameRoot = process.env.STELLARIS_GAME_ROOT;
test('Реальные Stellaris 4.5.2 bindings и все пять файлов окружения', { skip: !gameRoot }, async () =>
{
    const texts = new Map();
    for (const path of CONTINENTAL_ENVIRONMENT_PATHS.slice(0, 2))
    {
        texts.set(path, await readFile(resolve(gameRoot, path), 'utf8'));
    }
    const metadata = parseContinentalEnvironmentMetadata(texts);
    for (const path of CONTINENTAL_ENVIRONMENT_PATHS.slice(2))
    {
        const bytes = await readFile(resolve(gameRoot, path));
        const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
        const image = inspectDds(buffer);
        assert.deepEqual({ width: image.width, height: image.height, codec: image.codec, mipCount: image.mipCount, faceCount: image.faceCount }, { width: 800, height: 400, codec: 'BGRA8', mipCount: 1, faceCount: 1 });
        assert.ok(metadata.has(`environment.pc_continental.${path.slice(-7, -4)}`), path);
    }
});

