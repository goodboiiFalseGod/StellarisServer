// Собственный статический адаптер структуры PixelPdxMeshStar 4.5.2; параметры задаёт профиль.
const vertexShader = `
varying vec2 starUv;
varying vec3 starWorldPosition;
varying vec3 starWorldNormal;
varying vec3 starNoiseDirection;
uniform mat3 noiseBasis;
void main()
{
    vec4 world = modelMatrix * vec4(position, 1.0);
    starWorldPosition = world.xyz;
    starWorldNormal = normalize(mat3(modelMatrix) * normal);
    starNoiseDirection = noiseBasis * normalize(position);
    starUv = uv;
    gl_Position = projectionMatrix * viewMatrix * world;
}`;

const fragmentShader = `
uniform samplerCube lavaNoise;
uniform sampler2D lavaDiffuse;
uniform sampler2D stoneDiffuse;
uniform vec3 brightColor;
uniform vec3 hotColor;
uniform vec3 coldColor;
uniform vec4 atmosphereColor;
uniform float atmosphereWidth;
uniform float atmosphereIntensity;
uniform float noiseTime;
uniform vec2 uvOffset;
varying vec2 starUv;
varying vec3 starWorldPosition;
varying vec3 starWorldNormal;
varying vec3 starNoiseDirection;
void main()
{
    vec3 direction = normalize(starNoiseDirection);
    float field = textureCube(lavaNoise, direction).r
        + textureCube(lavaNoise, direction * 4.0).r
        + textureCube(lavaNoise, direction * 8.0).r - 1.5;
    float wave = sin((field + noiseTime * 0.08) * 10.0);
    float inverseWave = (1.0 - wave) * 0.5;
    vec3 toCamera = normalize(cameraPosition - starWorldPosition);
    vec2 parallax = inverseWave * toCamera.xz;
    vec2 surfaceUv = starUv + uvOffset;
    vec3 lava = texture2D(lavaDiffuse, surfaceUv * 10.0 - parallax * 0.1).rgb;
    vec3 stone = texture2D(stoneDiffuse, surfaceUv * 25.0 - parallax * 0.15).rgb;
    vec3 heated = stone * hotColor * (abs(inverseWave) + 0.1);
    heated += stone * coldColor * clamp(wave, 0.0, 1.0);
    float lavaMask = pow(clamp(-wave, 0.0, 1.0), 2.0);
    vec3 color = clamp(heated + lava * brightColor * pow(lavaMask, 0.7), 0.0, 1.0);
    float atmosphere = clamp(dot(normalize(starWorldNormal), -toCamera) + atmosphereWidth, 0.0, 1.0);
    color = mix(color, atmosphereColor.rgb, atmosphere * atmosphere * atmosphereColor.a * atmosphereIntensity);
    float rim = smoothstep(0.5, 1.0, 1.0 - dot(normalize(starWorldNormal), toCamera));
    color = clamp(color * (1.0 + 3.5 * rim), 0.0, 1.0);
    gl_FragColor = vec4(color * 2.0, 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
}`;

function fail(label)
{
    throw new Error(`M3 star material: неверный обязательный параметр ${label}.`);
}

function vector(value, length, label, min = -Infinity, max = Infinity)
{
    if (!Array.isArray(value) || value.length !== length || Array.from(value).some(item => !Number.isFinite(item) || item < min || item > max)) { fail(label); }
    return value.slice();
}

export function createStarMaterialParameters(input)
{
    if (!input || typeof input !== 'object') { fail('input'); }
    const uniforms = {};
    for (const name of ['lavaNoise', 'lavaDiffuse', 'stoneDiffuse'])
    {
        const texture = input[name];
        if (texture?.isTexture !== true || Boolean(texture.isCubeTexture) !== (name === 'lavaNoise')) { fail(name); }
        if (name !== 'lavaNoise' && (texture.isDataTexture !== true || texture.isData3DTexture || texture.isDataArrayTexture || texture.isCompressedArrayTexture || texture.isCubeArrayTexture)) { fail(name); }
        uniforms[name] = { value: texture };
    }
    for (const name of ['brightColor', 'hotColor', 'coldColor']) { uniforms[name] = { value: vector(input[name], 3, name, 0) }; }
    const atmosphere = vector(input.atmosphereColor, 4, 'atmosphereColor', 0);
    if (atmosphere[3] > 1) { fail('atmosphereColor.a'); }
    uniforms.atmosphereColor = { value: atmosphere };
    uniforms.uvOffset = { value: vector(input.uvOffset, 2, 'uvOffset') };
    uniforms.noiseBasis = { value: vector(input.noiseBasis, 9, 'noiseBasis') };
    // Проверяем ортонормальный basis: масштаб/вырождение не маскируют неверную ориентацию.
    const basis = uniforms.noiseBasis.value;
    for (let i = 0; i < 3; i++)
    {
        for (let j = i; j < 3; j++)
        {
            const dot = basis[i * 3] * basis[j * 3] + basis[i * 3 + 1] * basis[j * 3 + 1] + basis[i * 3 + 2] * basis[j * 3 + 2];
            if (Math.abs(dot - (i === j ? 1 : 0)) > 1e-6) { fail('noiseBasis'); }
        }
    }
    for (const name of ['atmosphereWidth', 'atmosphereIntensity', 'noiseTime'])
    {
        if (!Number.isFinite(input[name]) || input[name] < 0) { fail(name); }
        uniforms[name] = { value: input[name] };
    }
    return { uniforms, vertexShader, fragmentShader, transparent: false, depthTest: true, depthWrite: true, toneMapped: true };
}

function hsvRgb(hsv, intensity, label)
{
    const [h, s, v] = vector(hsv, 3, label, 0, 1);
    if (!Number.isFinite(intensity) || intensity < 0) { fail(label + ' intensity'); }
    // Прямой HSV→RGB без gamma transfer: явная политика линейных uniforms адаптера M3.
    return [5, 3, 1].map(offset =>
    {
        const sector = (h * 6 + offset) % 6;
        return (v - v * s * Math.max(0, Math.min(sector, 4 - sector, 1))) * intensity;
    });
}

export function createStaticGStarParameters(surface, textures)
{
    if (!surface || typeof surface !== 'object') { fail('starSurface'); }
    const atmosphere = hsvRgb(surface.atmosphereColor, 1, 'atmosphereColor');
    return createStarMaterialParameters({
        ...textures,
        brightColor: hsvRgb(surface.lavaBrightColor, surface.lavaBrightIntensity, 'lavaBrightColor'),
        hotColor: hsvRgb(surface.lavaHotStoneColor, surface.lavaHotStoneIntensity, 'lavaHotStoneColor'),
        coldColor: hsvRgb(surface.lavaColdStoneColor, surface.lavaColdStoneIntensity, 'lavaColdStoneColor'),
        atmosphereColor: [...atmosphere, 1], atmosphereWidth: surface.atmosphereWidth, atmosphereIntensity: surface.atmosphereIntensity,
        // Статические адаптации M3: не native runtime uniforms и не новая анимация.
        noiseTime: 0, uvOffset: [0, 0], noiseBasis: [1, 0, 0, 0, 1, 0, 0, 0, 1]
    });
}
