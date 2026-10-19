import test from 'node:test';
import assert from 'node:assert/strict';
import { createStarMaterialParameters, createStaticGStarParameters } from './m3-star-material.mjs';

function fixture()
{
    return {
        lavaNoise: { isTexture: true, isCubeTexture: true }, lavaDiffuse: { isTexture: true, isDataTexture: true }, stoneDiffuse: { isTexture: true, isDataTexture: true },
        brightColor: [0.2, 0.4, 0.6], hotColor: [1, 0.5, 0.2], coldColor: [0.9, 0.3, 0.1],
        atmosphereColor: [1, 0.8, 0.5, 0.4], atmosphereWidth: 1, atmosphereIntensity: 1, noiseTime: 0,
        uvOffset: [0, 0], noiseBasis: [1, 0, 0, 0, 1, 0, 0, 0, 1]
    };
}

test('Материал заимствует текстуры и изолирует изменяемые uniforms экземпляров', () =>
{
    const source = fixture();
    const first = createStarMaterialParameters(source);
    const second = createStarMaterialParameters(source);
    assert.equal(first.uniforms.lavaNoise.value, source.lavaNoise);
    first.uniforms.hotColor.value[0] = 7;
    first.uniforms.noiseTime.value = 5;
    assert.equal(second.uniforms.hotColor.value[0], 1);
    assert.equal(source.hotColor[0], 1);
    assert.equal(second.uniforms.noiseTime.value, 0);
    assert.equal(first.transparent, false);
    assert.equal(first.depthWrite, true);
});

test('Статический профиль использует импортированные HSV/intensity и явную адаптацию alpha/time', () =>
{
    const surface = { lavaBrightColor: [0, 1, 1], lavaBrightIntensity: 2, lavaHotStoneColor: [1 / 3, 1, 1], lavaHotStoneIntensity: 3, lavaColdStoneColor: [2 / 3, 1, 1], lavaColdStoneIntensity: 4, atmosphereColor: [0, 0, 0.5], atmosphereWidth: 1.9, atmosphereIntensity: 0.5 };
    const result = createStaticGStarParameters(surface, fixture());
    assert.deepEqual(result.uniforms.brightColor.value, [2, 0, 0]);
    assert.deepEqual(result.uniforms.hotColor.value, [0, 3, 0]);
    assert.deepEqual(result.uniforms.coldColor.value, [0, 0, 4]);
    assert.deepEqual(result.uniforms.atmosphereColor.value, [0.5, 0.5, 0.5, 1]);
    assert.equal(result.uniforms.noiseTime.value, 0);
    for (const key of Object.keys(surface))
    {
        const invalid = { ...surface };
        delete invalid[key];
        assert.throws(() => createStaticGStarParameters(invalid, fixture()), /M3 star material/);
    }
});

test('Каждый обязательный вход отклоняется до создания материала; fallback отсутствует', () =>
{
    const source = fixture();
    for (const key of Object.keys(source))
    {
        const invalid = { ...source };
        delete invalid[key];
        assert.throws(() => createStarMaterialParameters(invalid), /M3 star material/);
    }
    assert.throws(() => createStarMaterialParameters(null), /input/);
});

test('Тип sampler и нечисловые/вырожденные параметры не проходят контракт', () =>
{
    for (const change of [
        { lavaNoise: { isTexture: true } }, { lavaDiffuse: { isTexture: true, isCubeTexture: true } },
        { lavaDiffuse: { isTexture: true, isData3DTexture: true } }, { stoneDiffuse: { isTexture: true, isDataArrayTexture: true } },
        { hotColor: [NaN, 0, 0] }, { hotColor: Array(3) }, { coldColor: [-1, 0, 0] }, { atmosphereColor: [1, 1, 1, 2] },
        { atmosphereIntensity: Infinity }, { noiseTime: -1 }, { uvOffset: [0] },
        { noiseBasis: Array(9).fill(0) }, { noiseBasis: [1, 0, 0, 1, 0, 0, 0, 0, 1] }
    ])
    {
        assert.throws(() => createStarMaterialParameters({ ...fixture(), ...change }), /M3 star material/);
    }
});
