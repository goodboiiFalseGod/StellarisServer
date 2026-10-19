import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { normalRrxg, specularApprox } from './adapters.mjs';
import { computeConvertedBytes, requireConvertedBudget, MAX_CONVERTED_BYTES } from './m3-memory.mjs';

// Статический preview: исходные координаты всех слоёв остаются общими.
export class M3Renderer
{
    constructor(canvas)
    {
        this.canvas = canvas;
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
        if (!this.renderer.capabilities.isWebGL2)
        {
            this.renderer.dispose();
            throw new Error('Для M3 требуется WebGL2.');
        }
        this.renderer.setPixelRatio(1);
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.NoToneMapping;
        this.camera = new THREE.PerspectiveCamera(45, 1, 0.01, 10000);
        this.controls = new OrbitControls(this.camera, canvas);
        this.resources = null;
    }

    load(sample, { maxConvertedBytes = MAX_CONVERTED_BYTES } = {})
    {
        const previous = this.resources;
        const convertedBytes = computeConvertedBytes(sample);
        requireConvertedBudget(convertedBytes, previous?.convertedBytes ?? 0, maxConvertedBytes);
        const candidate = { geometries: [], materials: [], textures: [], textureMap: new Map(), scene: new THREE.Scene(), convertedBytes };
        try
        {
            candidate.scene.background = new THREE.Color('#050c16');
            candidate.scene.add(new THREE.AmbientLight(0xffffff, 0.8));
            const light = new THREE.DirectionalLight(0xffffff, 2);
            light.position.set(10, 12, 8);
            candidate.scene.add(light);
            const group = new THREE.Group();
            group.scale.setScalar(sample.scale ?? 1);
            if (sample.position) { group.position.fromArray(sample.position); }
            if (sample.quaternion) { group.quaternion.fromArray(sample.quaternion).normalize(); }
            candidate.scene.add(group);
            for (const [index, mesh] of sample.meshes.entries())
            {
                const geometry = new THREE.BufferGeometry();
                candidate.geometries.push(geometry);
                geometry.setAttribute('position', new THREE.BufferAttribute(mesh.positions, 3));
                if (mesh.normals) { geometry.setAttribute('normal', new THREE.BufferAttribute(mesh.normals, 3)); }
                if (mesh.uv) { geometry.setAttribute('uv', new THREE.BufferAttribute(mesh.uv, 2)); }
                geometry.setIndex(new THREE.BufferAttribute(mesh.indices, 1));
                geometry.computeBoundingSphere();
                const material = this.material(mesh, sample, candidate);
                candidate.materials.push(material);
                const drawable = new THREE.Mesh(geometry, material);
                drawable.name = `${mesh.name}:${index}`;
                drawable.renderOrder = mesh.name.includes('shadow') ? 1 : mesh.name.includes('poles') ? 2 : mesh.name.includes('clouds') ? 3 : mesh.name.includes('atmosphere') ? 4 : 0;
                group.add(drawable);
            }
            candidate.bounds = new THREE.Box3().setFromObject(group);
            if (candidate.bounds.isEmpty()) { throw new Error('Нельзя показать модель без геометрии.'); }
            this.resources = candidate;
            this.resetCamera();
            this.render();
            this.release(previous);
        }
        catch (error)
        {
            this.release(candidate);
            // Ошибка подготовки не удаляет предыдущую валидную сцену.
            this.resources = previous;
            this.resetCamera();
            throw error;
        }
    }

    texture(path, mode, sample, candidate)
    {
        const key = `${path}:${mode}`;
        if (candidate.textureMap.has(key)) { return candidate.textureMap.get(key); }
        const image = sample.images.get(path);
        if (!image) { throw new Error(`Отсутствует декодированная текстура ${path}.`); }
        const converted = mode === 'normal' ? normalRrxg(image) : mode === 'specular' ? specularApprox(image) : image;
        const texture = new THREE.DataTexture(converted.rgba, converted.width, converted.height, THREE.RGBAFormat);
        candidate.textures.push(texture);
        texture.colorSpace = mode === 'diffuse' ? THREE.SRGBColorSpace : THREE.NoColorSpace;
        texture.flipY = false;
        texture.generateMipmaps = true;
        texture.minFilter = THREE.LinearMipmapLinearFilter;
        texture.magFilter = THREE.LinearFilter;
        texture.needsUpdate = true;
        candidate.textureMap.set(key, texture);
        return texture;
    }

    material(mesh, sample, candidate)
    {
        const shader = mesh.material.shader;
        if (shader === 'PdxMeshAtmosphere' || shader === 'PdxMeshAtmosphereStar')
        {
            return new THREE.ShaderMaterial({
                uniforms: { tint: { value: new THREE.Color(shader.endsWith('Star') ? '#ffbd55' : '#549ed5') } },
                vertexShader: 'varying vec3 n; varying vec3 v; void main(){vec4 p=modelViewMatrix*vec4(position,1.0); n=normalize(normalMatrix*normal); v=normalize(-p.xyz); gl_Position=projectionMatrix*p;}',
                fragmentShader: 'uniform vec3 tint; varying vec3 n; varying vec3 v; void main(){float rim=pow(1.0-abs(dot(normalize(n),normalize(v))),3.0); gl_FragColor=vec4(tint,rim*0.55);}',
                transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.BackSide
            });
        }
        if (shader === 'PdxMeshStar') { return new THREE.MeshBasicMaterial({ color: '#ffd685', side: THREE.DoubleSide }); }
        if (!['PdxMeshShip', 'PdxMeshPlanet', 'PdxMeshClouds'].includes(shader)) { throw new Error(`Нет статического адаптера shader ${shader}.`); }
        const clouds = shader === 'PdxMeshClouds';
        return new THREE.MeshPhongMaterial({
            map: this.texture(mesh.material.diff, 'diffuse', sample, candidate),
            normalMap: this.texture(mesh.material.n, 'normal', sample, candidate),
            specularMap: this.texture(mesh.material.spec, 'specular', sample, candidate),
            specular: 0xffffff, shininess: 30, side: THREE.DoubleSide, transparent: clouds, depthWrite: !clouds
        });
    }

    resetCamera()
    {
        if (!this.resources) { return; }
        const center = this.resources.bounds.getCenter(new THREE.Vector3());
        const radius = this.resources.bounds.getSize(new THREE.Vector3()).length() / 2;
        this.camera.position.copy(center).add(new THREE.Vector3(1, 0.7, 1).normalize().multiplyScalar(radius * 3));
        this.camera.near = Math.max(0.001, radius / 1000);
        this.camera.far = Math.max(100, radius * 100);
        this.camera.updateProjectionMatrix();
        this.controls.target.copy(center);
        this.controls.update();
    }

    resize()
    {
        const rect = this.canvas.getBoundingClientRect();
        const width = Math.max(1, Math.round(rect.width));
        const height = Math.max(1, Math.round(rect.height));
        this.renderer.setSize(width, height, false);
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
    }

    render() { if (this.resources) { this.renderer.render(this.resources.scene, this.camera); } }
    stats() { return { ...this.renderer.info.memory, ...this.renderer.info.render, viewport: [this.canvas.width, this.canvas.height], convertedBytes: this.resources?.convertedBytes ?? 0 }; }
    clear() { this.release(this.resources); this.resources = null; this.renderer.renderLists.dispose(); this.renderer.clear(); }
    release(resources)
    {
        if (!resources) { return; }
        for (const value of [...resources.geometries, ...resources.materials, ...resources.textures]) { value.dispose(); }
    }
    dispose() { this.clear(); this.controls.dispose(); this.renderer.dispose(); }
}
