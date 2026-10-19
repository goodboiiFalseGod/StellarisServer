import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

// Владелец профиля освобождает texture и faces; pixel buffers остаются заимствованными.
export function createDdsCubeTexture(dds) {
  if (!dds?.isCubemap || !Number.isInteger(dds.width) || dds.width < 1 || dds.width !== dds.height || !Array.isArray(dds.faces) || dds.faces.length !== 6) {
    throw new Error('Cube adapter: требуется проверенный шестигранный RGBA8 DDS.');
  }
  // Check every own index before constructing textures: array predicates skip holes.
  for (let index = 0; index < 6; index++) {
    if (!Object.hasOwn(dds.faces, index) || !(dds.faces[index] instanceof Uint8Array) || dds.faces[index].length !== dds.width * dds.height * 4) {
      throw new Error('Cube adapter: требуется проверенный шестигранный RGBA8 DDS.');
    }
  }
  const faces = dds.faces.map(pixels => {
    const face = new THREE.DataTexture(pixels, dds.width, dds.height, THREE.RGBAFormat, THREE.UnsignedByteType);
    face.flipY = false; face.colorSpace = THREE.NoColorSpace; face.generateMipmaps = false;
    face.minFilter = face.magFilter = THREE.LinearFilter;
    face.wrapS = face.wrapT = THREE.ClampToEdgeWrapping;
    return face;
  });
  const texture = new THREE.CubeTexture(faces);
  texture.flipY = false; texture.colorSpace = THREE.NoColorSpace; texture.generateMipmaps = false;
  texture.minFilter = texture.magFilter = THREE.LinearFilter;
  texture.wrapS = texture.wrapT = THREE.ClampToEdgeWrapping;
  texture.needsUpdate = true;
  return texture;
}

// Оба адаптера получают уже проверенные массивы. Никаких сетевых загрузчиков ассетов.
export function normalRrxg(dds) {
  const pixels = new Uint8Array(dds.rgba.length);
  for (let i = 0; i < pixels.length; i += 4) {
    const x = dds.rgba[i + 1] / 127.5 - 1;
    const y = -(dds.rgba[i + 3] / 127.5 - 1);
    const z = Math.sqrt(Math.max(0, 1 - x * x - y * y));
    pixels[i] = Math.round((x + 1) * 127.5); pixels[i + 1] = Math.round((y + 1) * 127.5);
    pixels[i + 2] = Math.round((z + 1) * 127.5); pixels[i + 3] = 255;
  }
  return { ...dds, rgba: pixels };
}

export function centeredPositions(mesh) {
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < mesh.positions.length; i++) { const a = i % 3; min[a] = Math.min(min[a], mesh.positions[i]); max[a] = Math.max(max[a], mesh.positions[i]); }
  const center = min.map((v, i) => (v + max[i]) / 2);
  const radius = Math.max(...max.map((v, i) => v - min[i])) / 2;
  if (!(radius > 0)) throw new Error('Вырожденная модель: нулевой размер.');
  return Float32Array.from(mesh.positions, (v, i) => (v - center[i % 3]) / radius);
}

export function specularApprox(dds) {
  const rgba = new Uint8Array(dds.rgba.length);
  for (let i = 0; i < rgba.length; i += 4) {
    const spec = Math.round((dds.rgba[i + 1] / 255) ** 2 * 0.4 * 255);
    rgba[i] = rgba[i + 1] = rgba[i + 2] = spec; rgba[i + 3] = 255;
  }
  return { ...dds, rgba };
}

function placement(i, count) {
  return count === 1 ? [0, 0, 0, 2.5] : [(i % 20 - 9.5) * 2.8, 0, (Math.floor(i / 20) - 4.5) * 2.8, 0.85];
}

export class ThreeAdapter {
  constructor(canvas, onPick) {
    this.canvas = canvas; this.onPick = onPick;
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, preserveDrawingBuffer: false });
    this.renderer.setPixelRatio(1); this.renderer.setSize(600, 440, false);
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.NoToneMapping;
    this.camera = new THREE.PerspectiveCamera(45, 600 / 440, 0.01, 1000);
    this.controls = new OrbitControls(this.camera, canvas); this.controls.enableDamping = false;
    this.click = (event) => {
      if (!this.object) return;
      const rect = canvas.getBoundingClientRect();
      const ray = new THREE.Raycaster();
      ray.setFromCamera(new THREE.Vector2((event.clientX - rect.left) / rect.width * 2 - 1, 1 - (event.clientY - rect.top) / rect.height * 2), this.camera);
      const hit = ray.intersectObject(this.object)[0];
      onPick(hit ? `fleet:${hit.instanceId ?? 0}` : 'нет попадания');
    };
    canvas.addEventListener('click', this.click);
    this.resetCamera(1);
  }
  texture(image, color = false) {
    const texture = new THREE.DataTexture(image.rgba, image.width, image.height, THREE.RGBAFormat);
    texture.colorSpace = color ? THREE.SRGBColorSpace : THREE.NoColorSpace;
    texture.flipY = false; texture.generateMipmaps = true;
    texture.minFilter = THREE.LinearMipmapLinearFilter; texture.magFilter = THREE.LinearFilter;
    texture.needsUpdate = true; this.textures.push(texture); return texture;
  }
  load(sample, count) {
    const previous = { scene: this.scene, object: this.object, geometry: this.geometry, material: this.material, textures: this.textures ?? [], count: this.count };
    this.scene = null; this.object = null; this.geometry = null; this.material = null; this.count = count; this.textures = [];
    try {
    this.scene = new THREE.Scene(); this.scene.background = new THREE.Color('#080f18');
    const ambient = new THREE.AmbientLight(0xffffff, 1.0); this.scene.add(ambient);
    const light = new THREE.DirectionalLight(0xffffff, 2); light.position.set(6, 8, 5); this.scene.add(light);
    const geometry = new THREE.BufferGeometry();
    this.geometry = geometry;
    geometry.setAttribute('position', new THREE.BufferAttribute(centeredPositions(sample.mesh), 3));
    geometry.setAttribute('normal', new THREE.BufferAttribute(sample.mesh.normals, 3));
    geometry.setAttribute('uv', new THREE.BufferAttribute(sample.mesh.uv, 2));
    geometry.setIndex(new THREE.BufferAttribute(sample.mesh.indices, 1)); geometry.computeBoundingSphere();
    const material = new THREE.MeshPhongMaterial({ map: this.texture(sample.diffuse, true), normalMap: this.texture(normalRrxg(sample.normal)), specularMap: this.texture(specularApprox(sample.specular)), specular: 0xffffff, shininess: 30, side: THREE.DoubleSide });
    this.material = material;
    this.object = new THREE.InstancedMesh(geometry, material, count);
    const matrix = new THREE.Matrix4();
    for (let i = 0; i < count; i++) {
      const [x, y, z, scale] = placement(i, count);
      matrix.makeScale(scale, scale, scale); matrix.setPosition(x, y, z); this.object.setMatrixAt(i, matrix);
    }
    this.object.instanceMatrix.needsUpdate = true; this.object.computeBoundingSphere(); this.scene.add(this.object);
    this.resetCamera(count); this.render();
    this.release(previous);
    } catch (error) {
      this.release({ object: this.object, geometry: this.geometry, material: this.material, textures: this.textures });
      Object.assign(this, previous); this.resetCamera(previous.count); throw error;
    }
  }
  resetCamera(count = this.count ?? 1) {
    const factor = count === 1 ? 1 : 5;
    this.camera.position.set(8 * factor, 6 * factor, 8 * factor); this.camera.lookAt(0, 0, 0);
    this.controls.target.set(0, 0, 0); this.controls.update();
  }
  render() { if (this.scene) this.renderer.render(this.scene, this.camera); }
  resize(width, height) { this.renderer.setSize(width, height, false); this.camera.aspect = width / height; this.camera.updateProjectionMatrix(); }
  stats() {
    const gl = this.renderer.getContext(), debug = gl.getExtension('WEBGL_debug_renderer_info');
    return { geometries: this.renderer.info.memory.geometries, textures: this.renderer.info.memory.textures, drawCalls: this.renderer.info.render.calls, triangles: this.renderer.info.render.triangles, webgl: gl.getParameter(gl.VERSION), gpu: debug ? gl.getParameter(debug.UNMASKED_RENDERER_WEBGL) : 'masked', extensions: { s3tc: !!gl.getExtension('WEBGL_compressed_texture_s3tc'), rgtc: !!gl.getExtension('EXT_texture_compression_rgtc'), bptc: !!gl.getExtension('EXT_texture_compression_bptc') } };
  }
  clear() {
    this.release({ object: this.object, geometry: this.geometry, material: this.material, textures: this.textures ?? [] });
    this.object = null; this.geometry = null; this.material = null; this.scene = null; this.textures = [];
    this.renderer.renderLists.dispose(); this.renderer.clear();
  }
  release(state) { state.object?.dispose(); state.geometry?.dispose(); state.material?.dispose(); for (const texture of state.textures ?? []) texture.dispose(); }
  dispose() { this.clear(); this.controls.dispose(); this.canvas.removeEventListener('click', this.click); this.renderer.dispose(); }
}

export class BabylonAdapter {
  constructor(canvas, onPick) {
    const B = window.BABYLON;
    if (!B) throw new Error('Babylon.js не загружен. Установите фиксированные зависимости.');
    this.B = B; this.canvas = canvas; this.onPick = onPick;
    this.engine = new B.Engine(canvas, true, { preserveDrawingBuffer: false, stencil: true, disableWebGL2Support: false });
    if (this.engine.webGLVersion !== 2) { this.engine.dispose(); throw new Error('Для эксперимента требуется WebGL2.'); }
    this.engine.setHardwareScalingLevel(1); this.engine.setSize(600, 440);
    this.click = (event) => {
      if (!this.scene) return;
      const rect = canvas.getBoundingClientRect();
      const hit = this.scene.pick((event.clientX - rect.left) / rect.width * this.engine.getRenderWidth(), (event.clientY - rect.top) / rect.height * this.engine.getRenderHeight());
      onPick(hit?.hit ? hit.pickedMesh.name : 'нет попадания');
    };
    canvas.addEventListener('click', this.click);
  }
  texture(image, color = false) {
    const B = this.B;
    const texture = new B.RawTexture(image.rgba, image.width, image.height, B.Constants.TEXTUREFORMAT_RGBA, this.scene, true, false, B.Texture.TRILINEAR_SAMPLINGMODE);
    texture.gammaSpace = color; return texture;
  }
  load(sample, count) {
    const previous = { scene: this.scene, camera: this.camera, instrumentation: this.instrumentation, count: this.count };
    this.scene = null; this.camera = null; this.instrumentation = null; this.count = count;
    try {
    const B = this.B; this.scene = new B.Scene(this.engine); this.scene.useRightHandedSystem = true;
    this.instrumentation = new B.SceneInstrumentation(this.scene);
    this.scene.clearColor = new B.Color4(8 / 255, 15 / 255, 24 / 255, 1);
    this.camera = new B.ArcRotateCamera('camera', Math.PI / 4, Math.acos(6 / Math.sqrt(164)), Math.sqrt(164), B.Vector3.Zero(), this.scene);
    this.camera.fov = Math.PI / 4; this.camera.minZ = 0.01; this.camera.maxZ = 1000;
    this.camera.inertia = 0; this.camera.attachControl(this.canvas, true);
    this.scene.ambientColor = B.Color3.White();
    const light = new B.DirectionalLight('key', new B.Vector3(-6, -8, -5), this.scene); light.intensity = 2;
    const material = new B.StandardMaterial('approximated-pdx', this.scene);
    material.ambientColor = B.Color3.White(); material.diffuseTexture = this.texture(sample.diffuse, true);
    material.bumpTexture = this.texture(normalRrxg(sample.normal)); material.specularTexture = this.texture(specularApprox(sample.specular));
    material.specularPower = 30; material.backFaceCulling = false; material.useAlphaFromDiffuseTexture = false;
    const mesh = new B.Mesh('fleet:0', this.scene);
    const vertex = new B.VertexData(); vertex.positions = centeredPositions(sample.mesh); vertex.normals = sample.mesh.normals; vertex.uvs = sample.mesh.uv; vertex.indices = sample.mesh.indices;
    vertex.applyToMesh(mesh); mesh.material = material;
    for (let i = 0; i < count; i++) {
      const item = i === 0 ? mesh : mesh.createInstance(`fleet:${i}`);
      const [x, y, z, scale] = placement(i, count); item.position.set(x, y, z); item.scaling.setAll(scale);
    }
    this.resetCamera(count); this.render();
    previous.instrumentation?.dispose(); previous.scene?.dispose();
    } catch (error) {
      this.instrumentation?.dispose(); this.scene?.dispose(); Object.assign(this, previous); throw error;
    }
  }
  resetCamera(count = this.count ?? 1) {
    if (!this.camera) return;
    this.camera.alpha = Math.PI / 4; this.camera.beta = Math.acos(6 / Math.sqrt(164));
    this.camera.radius = Math.sqrt(164) * (count === 1 ? 1 : 5); this.camera.setTarget(this.B.Vector3.Zero());
  }
  render() { if (this.scene) this.scene.render(); }
  resize(width, height) { this.engine.setSize(width, height); }
  stats() { return { geometries: this.scene?.geometries.length ?? 0, textures: this.scene?.textures.length ?? 0, drawCalls: this.instrumentation?.drawCallsCounter.current ?? 0, triangles: this.scene ? this.scene.getActiveIndices() / 3 : 0, webgl: this.engine.webGLVersion, meshes: this.scene?.meshes.length ?? 0 }; }
  clear() { this.instrumentation?.dispose(); this.instrumentation = null; this.scene?.dispose(); this.scene = null; this.camera = null; this.engine.clear(new this.B.Color4(8 / 255, 15 / 255, 24 / 255, 1), true, true); }
  dispose() { this.clear(); this.canvas.removeEventListener('click', this.click); this.engine.dispose(); }
}
