import * as THREE from 'three';

// Калибровки прежних статических профилей; для generated worlds используется отдельная версия политики.
export const DISPLAY_RADII = Object.freeze({ 'star.g': 12, 'planet.continental': 7, 'station.outpost.mammalian': 2.8, 'ship.corvette.mammalian': 1.5 });

export function selectVisibleLabelKeys(candidates)
{
    if (!Array.isArray(candidates)) { throw new Error('M3: label candidates must be an array.'); }
    const keys = new Set();
    const ordered = candidates.map(candidate =>
    {
        if (!candidate || typeof candidate !== 'object' || typeof candidate.key !== 'string' || candidate.key.trim().length === 0 || !Number.isFinite(candidate.priority) || ![candidate.left, candidate.top, candidate.right, candidate.bottom].every(Number.isFinite) || candidate.right < candidate.left || candidate.bottom < candidate.top)
        {
            throw new Error('M3: label candidate requires a unique key, finite priority and valid finite CSS rectangle.');
        }
        if (keys.has(candidate.key)) { throw new Error(`M3: duplicate label candidate key ${candidate.key}.`); }
        keys.add(candidate.key);
        return candidate;
    });
    ordered.sort((a, b) => a.priority === b.priority ? a.key < b.key ? -1 : a.key > b.key ? 1 : 0 : a.priority > b.priority ? -1 : 1);

    const admitted = new Set();
    const rectangles = [];
    for (const candidate of ordered)
    {
        const overlaps = rectangles.some(rectangle => candidate.left < rectangle.right && candidate.right > rectangle.left && candidate.top < rectangle.bottom && candidate.bottom > rectangle.top);
        if (overlaps) { continue; }
        admitted.add(candidate.key);
        rectangles.push(candidate);
    }
    return admitted;
}

export function validateLabelInsets(insets)
{
    if (!insets || Object.keys(insets).length !== 4 || !['top', 'right', 'bottom', 'left'].every(key => Number.isFinite(insets[key]) && insets[key] >= 0)) { throw new Error('M3: label insets require four finite nonnegative CSS pixel values.'); }
    return { top: insets.top, right: insets.right, bottom: insets.bottom, left: insets.left };
}

export function labelFitsViewport(point, halfWidth, halfHeight, width, height, insets)
{
    if (point.z < -1 || point.z > 1 || !point.toArray().every(Number.isFinite)) { return false; }
    const x = (point.x + 1) * width * 0.5;
    const y = (1 - point.y) * height * 0.5;
    return x - halfWidth >= insets.left && x + halfWidth <= width - insets.right && y - halfHeight >= insets.top && y + halfHeight <= height - insets.bottom;
}

export function templateDisplayFactor(template, visualId)
{
    if(visualId.startsWith('world.'))
    {
        if(template.userData.sourceClassId!==visualId.slice(6)||template.userData.generatedDisplayPolicy!=='GeneratedMapUnitSurfaceV1'||!Number.isFinite(template.userData.generatedDisplayFactor)||template.userData.generatedDisplayFactor<=0) { throw new Error('M3: generated display/source binding mismatch.'); }
        return template.userData.generatedDisplayFactor;
    }
    const target = DISPLAY_RADII[visualId];
    if (!target) { throw new Error(`M3: unsupported display calibration ${visualId}.`); }
    template.updateMatrixWorld(true);
    let radius = 0;
    const surfaceShader = visualId === 'star.g' ? 'PdxMeshStar' : visualId === 'planet.continental' ? 'PdxMeshPlanet' : null;
    template.traverse(child =>
    {
        if (!child.isMesh || (surfaceShader && child.userData.sourceShader !== surfaceShader)) { return; }
        const sphere = child.geometry.boundingSphere.clone().applyMatrix4(child.matrixWorld);
        radius = Math.max(radius, sphere.center.length() + sphere.radius);
    });
    if (radius === 0 && surfaceShader) { throw new Error(`M3: ${visualId} requires its source surface shader for display calibration.`); }
    if (radius === 0)
    {
        const bounds = new THREE.Box3().setFromObject(template);
        if (!bounds.isEmpty()) { radius = bounds.getBoundingSphere(new THREE.Sphere()).radius; }
    }
    if (!Number.isFinite(radius) || radius <= 0) { throw new Error('M3: empty display bounds.'); }
    return target / radius;
}

// Радиус поверхности шаблона; атмосфера, облака и прочие FX не определяют подписи.
export function displayedSurfaceRadius(template, visualId, scale)
{
    if (!template || !Number.isFinite(scale) || scale <= 0) { throw new Error('M3: нет шаблона или масштаба поверхности.'); }
    const surfaceShaders = new Set(['PdxMeshStar', 'PdxMeshPlanet', 'PdxMeshPlanetEmissive', 'PdxMeshGasGiant', 'PdxMeshMoltenPlanet', 'PdxMeshBlackHole', 'PdxMeshShip']);
    template.updateMatrixWorld(true);
    let radius = 0;
    template.traverse(child =>
    {
        if (!child.isMesh || !surfaceShaders.has(child.userData.sourceShader)) { return; }
        if (!child.geometry.boundingSphere) { child.geometry.computeBoundingSphere(); }
        const sphere = child.geometry.boundingSphere.clone().applyMatrix4(child.matrixWorld);
        radius = Math.max(radius, sphere.center.length() + sphere.radius);
    });
    if (!Number.isFinite(radius) || radius <= 0) { throw new Error(`M3: ${visualId} не содержит source surface mesh.`); }
    return radius * templateDisplayFactor(template, visualId) * scale;
}

export function orbitPositions(position, segments = 128)
{
    const radius = Math.hypot(position.x, position.z);
    if (!Number.isFinite(radius) || radius <= 0) { throw new Error('M3: invalid orbit context radius.'); }
    const values = [];
    for (let index = 0; index <= segments; index++)
    {
        const angle = index / segments * Math.PI * 2;
        values.push(Math.cos(angle) * radius, 0, Math.sin(angle) * radius);
    }
    return values;
}

export function createStarCoronaParameters(surfaceRadius, atmosphereRadius, { tint } = {})
{
    if (![surfaceRadius, atmosphereRadius].every(Number.isFinite) || surfaceRadius <= 0 || atmosphereRadius <= surfaceRadius) { throw new Error('M3: invalid star corona radii.'); }
    if(tint!==undefined&&(!Array.isArray(tint)||tint.length!==3||tint.some(v=>!Number.isFinite(v)||v<0))) { throw new Error('M3: invalid source corona tint.'); }
    return {
        uniforms: { tint: { value: tint?new THREE.Color().setRGB(...tint):new THREE.Color('#ffbd55') }, surfaceRadius: { value: surfaceRadius }, atmosphereRadius: { value: atmosphereRadius } },
        vertexShader: `varying vec3 localPosition; varying vec3 localCamera;
            void main(){ localPosition=position; localCamera=(inverse(modelMatrix)*vec4(cameraPosition,1.0)).xyz; gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0); }`,
        fragmentShader: `uniform vec3 tint; uniform float surfaceRadius; uniform float atmosphereRadius; varying vec3 localPosition; varying vec3 localCamera;
            void main(){ vec3 ray=normalize(localPosition-localCamera); float impact=length(cross(localCamera,ray));
            float outside=max(0.0,(impact-surfaceRadius)/(atmosphereRadius-surfaceRadius));
            float alpha=exp(-outside*7.0)*(1.0-smoothstep(0.7,1.0,outside))*0.48;
            gl_FragColor=vec4(tint,alpha);
            #include <tonemapping_fragment>
            #include <colorspace_fragment>
            }`,
        transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.BackSide
    };
}

export function updateGalaxyGlyphScale(glyph, camera, viewportHeight, { minPixels = 12 } = {})
{
    const bound = glyph?.userData?.screenBoundRadius;
    if (!glyph?.isObject3D || !camera?.isPerspectiveCamera || ![bound, viewportHeight, minPixels].every(Number.isFinite) || bound <= 0 || viewportHeight <= 0 || minPixels <= 0)
    { throw new Error('M3: invalid galaxy glyph projection parameters.'); }
    camera.updateMatrixWorld();
    const center = glyph.getWorldPosition(new THREE.Vector3()).applyMatrix4(camera.matrixWorldInverse);
    const depth = -center.z;
    if (!Number.isFinite(depth) || depth <= 0) { glyph.scale.setScalar(1); return 1; }
    const pixelsPerWorldUnit = viewportHeight / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov) * 0.5) * depth);
    const requested = minPixels * 1.08 / (2 * bound * pixelsPerWorldUnit);
    if(!Number.isFinite(requested)||requested<=0) { throw new Error('M3: galaxy glyph scale is not finite.'); }
    const scale = Math.max(1, requested);
    glyph.scale.setScalar(scale);
    if(glyph.userData.separateFromGlyph)
    {
        const other=glyph.userData.separateFromGlyph,otherRadius=(other.userData.screenBoundRadius??0)*other.scale.x;
        const distance=otherRadius+bound*scale+1;
        glyph.position.set(distance/Math.sqrt(2),0,-distance/Math.sqrt(2));
    }
    else if(glyph.userData.isWormholeGlyph)
    {
        const distance=(glyph.userData.separationClearance??1.5)+bound*scale;
        glyph.position.set(distance/Math.sqrt(2),0,-distance/Math.sqrt(2));
    }
    return scale;
}

export function createGalaxyGlow(components)
{
    if(!Array.isArray(components)||components.length===0) { throw new Error('M3: galaxy glow requires stellar components.'); }
    const positions=[],colors=[];
    for(const component of components)
    {
        if(!component||!Array.isArray(component.color)||component.color.length!==3||![component.x,component.y,component.z,...component.color].every(Number.isFinite)) { throw new Error('M3: invalid stellar glow component.'); }
        positions.push(component.x,component.y,component.z); colors.push(...component.color);
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
    const material = new THREE.ShaderMaterial({
        vertexShader: 'attribute vec3 color; varying vec3 glowColor; void main(){vec4 p=modelViewMatrix*vec4(position,1.0);glowColor=color;gl_PointSize=clamp(8000.0/max(1.0,-p.z),8.0,12.0);gl_Position=projectionMatrix*p;}',
        fragmentShader: 'varying vec3 glowColor; void main(){float r=length(gl_PointCoord-0.5)*2.0;float a=exp(-r*r*5.0)*(1.0-smoothstep(0.65,1.0,r))*0.22;gl_FragColor=vec4(glowColor,a);}',
        transparent: true, blending: THREE.AdditiveBlending, depthTest: false, depthWrite: false
    });
    const points = new THREE.Points(geometry, material);
    points.renderOrder = 1;
    return points;
}

export function createObjectLabel(document, text)
{
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 64;
    const context = canvas.getContext('2d');
    if (!context) { throw new Error('M3: label canvas is unavailable.'); }
    context.font = '32px sans-serif';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.lineWidth = 5;
    context.strokeStyle = '#081014';
    context.strokeText(text, 256, 32, 500);
    context.fillStyle = '#d2ddd7';
    context.fillText(text, 256, 32, 500);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.generateMipmaps = false;
    texture.minFilter = THREE.LinearFilter;
    const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false, depthWrite: false, sizeAttenuation: false });
    const sprite = new THREE.Sprite(material);
    sprite.scale.set(0.25, 0.03125, 1);
    sprite.renderOrder = 10;
    sprite.userData.textWidth = Math.min(500, context.measureText(text).width) / canvas.width;
    return sprite;
}
