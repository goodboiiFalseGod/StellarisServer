// Учёт дополнительных CPU-массивов normal/specular, удерживаемых DataTexture.
export const MAX_CONVERTED_BYTES = 512 * 1024 * 1024;
const PHONG_SHADERS = new Set(['PdxMeshPlanetRings', 'PdxMeshShip', 'PdxMeshPlanet', 'PdxMeshClouds', 'PdxMeshPlanetEmissive', 'PdxMeshGasGiant', 'PdxMeshMoltenPlanet', 'PdxMeshBlackHole', 'PdxMeshTerra']);
const UNCONVERTED_SHADERS = new Set(['PdxMeshAtmosphere', 'PdxMeshAtmosphereStar', 'PdxMeshStar', 'PdxMeshAlphaAdditiveAnimateUV', 'PdxMeshAlphaAdditiveAnimateUVNoDissolve', 'PdxMeshBlackHoleBillboard', 'PdxMeshAlphaAdditive']);

export function computeConvertedBytes(sample)
{
    if (!sample || !Array.isArray(sample.meshes) || !(sample.images instanceof Map)) { throw new Error('M3 memory: ожидались meshes[] и images Map.'); }
    const used = new Set();
    let bytes = 0;
    for (const mesh of sample.meshes)
    {
        const shader = mesh?.material?.shader;
        if (UNCONVERTED_SHADERS.has(shader)) { continue; }
        if (!PHONG_SHADERS.has(shader)) { throw new Error(`M3 memory: неподдерживаемый shader ${String(shader)}.`); }
        for (const [channel, mode] of [['n', 'normal'], ['spec', 'specular']])
        {
            if(channel==='n'&&['PdxMeshGasGiant','PdxMeshMoltenPlanet'].includes(shader)) { continue; }
            const path = mesh.material[channel];
            if (typeof path !== 'string' || path.length === 0) { throw new Error(`M3 memory: отсутствует канал ${channel}.`); }
            const key = `${path}:${mode}`;
            if (used.has(key)) { continue; }
            const image = sample.images.get(path);
            if (!image || !Number.isInteger(image.width) || !Number.isInteger(image.height) || image.width < 1 || image.height < 1 || image.width > 8192 || image.height > 8192 || !(image.rgba instanceof Uint8Array) || image.rgba.byteLength !== image.width * image.height * 4)
            {
                throw new Error(`M3 memory: неверная декодированная текстура ${path}.`);
            }
            used.add(key);
            bytes += image.rgba.byteLength;
            if (!Number.isSafeInteger(bytes)) { throw new Error('M3 memory: переполнение decoded bytes.'); }
        }
    }
    return bytes;
}

export function requireConvertedBudget(candidateBytes, retainedBytes, maxConvertedBytes = MAX_CONVERTED_BYTES)
{
    if (![candidateBytes, retainedBytes, maxConvertedBytes].every(value => Number.isSafeInteger(value) && value >= 0) || maxConvertedBytes > MAX_CONVERTED_BYTES)
    {
        throw new Error('M3 memory: неверные размеры/budget.');
    }
    if (candidateBytes > maxConvertedBytes || retainedBytes > maxConvertedBytes - candidateBytes)
    {
        throw new Error('M3 memory: превышен converted memory budget старой и staged сцены.');
    }
}
