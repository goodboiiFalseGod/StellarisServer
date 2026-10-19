import * as THREE from 'three';

export const GALAXY_LAYER_PATHS = Object.freeze({ core: 'gfx/map/center.dds', dust: 'gfx/map/dust.dds', color: 'gfx/map/galaxycolor.dds' });

// Native file binding and patch placement are inferred; dimensions are browser art calibration.
export function createGalaxyLayerTextures(layers)
{
    if (!(layers instanceof Map) || layers.size !== 3) { throw new Error('M3: incomplete galaxy layers.'); }
    const textures = new Map();
    try
    {
        for (const [id, path] of Object.entries(GALAXY_LAYER_PATHS))
        {
            const layer = layers.get(id);
            const image = layer?.image;
            if (layer?.sourcePath !== path || layer.encoding !== 'raw-rgba' || image?.isCubemap || !Number.isSafeInteger(image?.width) || !Number.isSafeInteger(image?.height) || image.width <= 0 || image.height <= 0 || !(image.rgba instanceof Uint8Array) || image.rgba.length !== image.width * image.height * 4) { throw new Error(`M3: invalid galaxy layer ${id}.`); }
            const texture = new THREE.DataTexture(image.rgba, image.width, image.height, THREE.RGBAFormat);
            textures.set(id, texture);
            texture.colorSpace = THREE.NoColorSpace;
            texture.flipY = false;
            texture.generateMipmaps = false;
            texture.minFilter = texture.magFilter = THREE.LinearFilter;
            texture.wrapS = texture.wrapT = id === 'color' ? THREE.ClampToEdgeWrapping : THREE.RepeatWrapping;
            texture.needsUpdate = true;
        }
        return textures;
    }
    catch (error) { for (const texture of textures.values()) { texture.dispose(); } throw error; }
}

export function createGalaxyLayerParameters(textures)
{
    if (!['core', 'dust', 'color'].every(id => textures.get(id)?.isDataTexture)) { throw new Error('M3: missing galaxy layer texture.'); }
    const vertexShader = 'varying vec2 sourceUv; varying vec3 worldPosition; void main(){sourceUv=uv;vec4 p=modelMatrix*vec4(position,1.0);worldPosition=p.xyz;gl_Position=projectionMatrix*viewMatrix*p;}';
    const output = '\n#include <tonemapping_fragment>\n#include <colorspace_fragment>\n}';
    // Browser calibration: linearize the filtered core RGB before intensity and sRGB output; native routing is unknown.
    const coreShader = 'uniform sampler2D source; varying vec2 sourceUv; void main(){vec4 sampled=texture2D(source,sourceUv);vec3 rgb=sampled.rgb;rgb=mix(rgb/12.92,pow((rgb+0.055)/1.055,vec3(2.4)),step(vec3(0.04045),rgb));gl_FragColor=vec4(rgb,sampled.a)*0.25;' + output;
    return {
        core: { uniforms: { source: { value: textures.get('core') } }, vertexShader, fragmentShader: coreShader, transparent: true, depthTest: false, depthWrite: false, side: THREE.DoubleSide, blending: THREE.CustomBlending, blendSrc: THREE.OneFactor, blendDst: THREE.OneFactor },
        dust: { uniforms: { source: { value: textures.get('dust') }, colorMap: { value: textures.get('color') }, galaxyCenter: { value: new THREE.Vector2() }, galaxyWidth: { value: 1 } }, vertexShader, fragmentShader: 'uniform sampler2D source; uniform sampler2D colorMap; uniform vec2 galaxyCenter; uniform float galaxyWidth; varying vec2 sourceUv; varying vec3 worldPosition; void main(){vec2 colorUv=(worldPosition.xz-galaxyCenter)/galaxyWidth+0.5;gl_FragColor=texture2D(source,sourceUv)*texture2D(colorMap,colorUv);gl_FragColor.a*=0.45;' + output, transparent: true, depthTest: false, depthWrite: false, side: THREE.DoubleSide, blending: THREE.NormalBlending }
    };
}

export function createGalaxyLayerGroup(materials, systems)
{
    if (!Array.isArray(systems)) { throw new Error('M3: missing authorized systems for galaxy decoration.'); }
    const bounds = new THREE.Box3();
    for (const system of systems)
    {
        const point = system.galaxyPosition;
        if (!point || ![point.x, point.y, point.z].every(Number.isFinite)) { throw new Error('M3: invalid authorized galaxy extent.'); }
        bounds.expandByPoint(new THREE.Vector3(point.x, point.y, point.z));
    }
    const group = new THREE.Group();
    const owned = [];
    if (bounds.isEmpty()) { return { group, owned }; }
    const center = bounds.getCenter(new THREE.Vector3());
    const size = bounds.getSize(new THREE.Vector3());
    const span = Math.max(100, size.x, size.z);
    const coreGeometry = new THREE.PlaneGeometry(1, 1);
    coreGeometry.rotateX(-Math.PI / 2);
    owned.push(coreGeometry);
    const core = new THREE.Mesh(coreGeometry, materials.get('core'));
    core.position.copy(center); core.position.y -= 1.5;
    core.scale.setScalar(span * 0.55);
    core.renderOrder = -800;
    group.add(core);
    const dustGeometry = new THREE.PlaneGeometry(1, 1);
    dustGeometry.rotateX(-Math.PI / 2);
    const positions = dustGeometry.attributes.position;
    for (let index = 0; index < positions.count; index++) { positions.setY(index, (positions.getX(index) + positions.getZ(index)) * 0.1); }
    dustGeometry.computeBoundingSphere();
    owned.push(dustGeometry);
    // Per-view uniforms belong to a per-view material; profile textures remain borrowed.
    const base = materials.get('dust');
    const dustMaterial = new THREE.ShaderMaterial({ vertexShader: base.vertexShader, fragmentShader: base.fragmentShader, uniforms: { source: base.uniforms.source, colorMap: base.uniforms.colorMap, galaxyCenter: { value: new THREE.Vector2() }, galaxyWidth: { value: 1 } }, transparent: true, depthTest: false, depthWrite: false, side: THREE.DoubleSide, blending: THREE.NormalBlending });
    owned.push(dustMaterial);
    dustMaterial.uniforms.galaxyCenter.value.set(center.x, center.z);
    dustMaterial.uniforms.galaxyWidth.value = span * 1.5;
    for (let arm = 0; arm < 3; arm++)
    {
        for (let step = 0; step < 8; step++)
        {
            const fraction = step / 7;
            const radius = span * (0.12 + fraction * 0.53);
            const angle = arm * Math.PI * 2 / 3 + fraction * 2.2;
            const patch = new THREE.Mesh(dustGeometry, dustMaterial);
            patch.position.set(center.x + Math.cos(angle) * radius, center.y - 2, center.z + Math.sin(angle) * radius);
            patch.rotation.y = -angle + Math.PI / 2;
            patch.scale.setScalar(span * 0.32);
            patch.renderOrder = -900;
            group.add(patch);
        }
    }
    return { group, owned };
}
