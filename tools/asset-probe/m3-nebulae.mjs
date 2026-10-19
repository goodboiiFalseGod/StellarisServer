import * as THREE from 'three';
import { createObjectLabel } from './m3-presentation.mjs';

// Собственное процедурное представление авторитетной окружности, без игровых текстур.
export function createNebulaGroup(nebulae, document = null, resolveName = key => key)
{
    const group = new THREE.Group(); const owned = []; const labels = [];
    for (const nebula of nebulae ?? [])
    {
        const geometry = new THREE.PlaneGeometry(nebula.radius * 2, nebula.radius * 2);
        geometry.rotateX(-Math.PI / 2);
        const material = new THREE.ShaderMaterial({
            vertexShader: 'varying vec2 vUv; void main(){vUv=uv; gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}',
            fragmentShader: `varying vec2 vUv;
                void main(){vec2 p=vUv*2.0-1.0; float r=length(p);
                float haze=0.72+0.18*sin(p.x*12.0+sin(p.y*9.0))*cos(p.y*13.0-p.x*3.0);
                float alpha=(1.0-smoothstep(0.15,1.0,r))*haze*0.46;
                gl_FragColor=vec4(mix(vec3(0.20,0.28,0.62),vec3(0.58,0.32,0.68),vUv.x),alpha);}`,
            transparent: true, depthWrite: false, depthTest: false, side: THREE.DoubleSide
        });
        const cloud = new THREE.Mesh(geometry, material);
        cloud.name = `nebula:${nebula.id}`;
        cloud.position.set(nebula.center.x, nebula.center.y, nebula.center.z);
        cloud.renderOrder = -800; cloud.raycast = () => {};
        group.add(cloud); owned.push(geometry, material);
        if (document)
        {
            const label = createObjectLabel(document, resolveName(nebula.nameKey) || 'Туманность');
            label.position.set(nebula.center.x, nebula.center.y + 2, nebula.center.z);
            label.userData.kind = 'nebula'; label.userData.objectId = nebula.id;
            label.raycast = () => {}; group.add(label); labels.push(label);
            owned.push(label.material.map, label.material);
        }
    }
    return { group, owned, labels };
}
