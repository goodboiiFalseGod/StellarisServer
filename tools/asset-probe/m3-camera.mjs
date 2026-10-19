import * as THREE from 'three';

export const ORBIT_RADIANS_PER_PIXEL = 0.004;
export const CAMERA_PRESET_IDS = Object.freeze(['galaxy-far', 'galaxy-near', 'system-overview', 'system-close']);

export function viewCenter(state, view)
{
    if (view?.kind === 'system' || !state) { return new THREE.Vector3(); }
    if (view?.kind !== 'galaxy' || !Array.isArray(state.spatial?.systems)) { throw new Error('M3: invalid camera view or authorized systems.'); }
    const bounds = new THREE.Box3();
    for (const system of state.spatial.systems)
    {
        const point = system.galaxyPosition;
        if (!point || ![point.x, point.y, point.z].every(Number.isFinite)) { throw new Error('M3: invalid authorized system position.'); }
        bounds.expandByPoint(new THREE.Vector3(point.x, point.y, point.z));
    }
    return bounds.isEmpty() ? new THREE.Vector3() : bounds.getCenter(new THREE.Vector3());
}

// Caller-supplied calibration uses perspective geometry, not a recovered native camera formula.
export function createCameraPreset(preset, { bounds, aspect, fov, direction, padding, distanceScale })
{
    if (!CAMERA_PRESET_IDS.includes(preset) || !bounds?.isBox3 || bounds.isEmpty() || ![...bounds.min.toArray(), ...bounds.max.toArray(), aspect, fov, padding, distanceScale].every(Number.isFinite) || aspect <= 0 || fov <= 0 || fov >= 180 || padding < 1 || distanceScale <= 0)
    {
        throw new Error('M3: invalid camera preset framing parameters.');
    }
    if (!direction?.isVector3 || !direction.toArray().every(Number.isFinite) || direction.lengthSq() === 0) { throw new Error('M3: invalid camera preset direction.'); }
    const backward = direction.clone().normalize();
    const right = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 1, 0), backward);
    if (right.lengthSq() < 1e-12) { throw new Error('M3: camera preset direction is parallel to camera up.'); }
    right.normalize();
    const up = new THREE.Vector3().crossVectors(backward, right);
    const target = bounds.getCenter(new THREE.Vector3());
    const tanVertical = Math.tan(THREE.MathUtils.degToRad(fov) * 0.5);
    const tanHorizontal = tanVertical * aspect;
    let distance = 0;
    let nearest = -Infinity;
    for (const x of [bounds.min.x, bounds.max.x])
    {
        for (const y of [bounds.min.y, bounds.max.y])
        {
            for (const z of [bounds.min.z, bounds.max.z])
            {
                const offset = new THREE.Vector3(x, y, z).sub(target);
                const depth = offset.dot(backward);
                nearest = Math.max(nearest, depth);
                distance = Math.max(distance, depth + padding * Math.max(Math.abs(offset.dot(right)) / tanHorizontal, Math.abs(offset.dot(up)) / tanVertical));
            }
        }
    }
    distance *= distanceScale;
    if (!Number.isFinite(distance) || distance <= nearest) { throw new Error('M3: camera preset intersects its framing bounds.'); }
    return { id: preset, target, position: target.clone().addScaledVector(backward, distance), distance };
}

export function createSystemOverviewPreset(radius, aspect)
{
    if (!Number.isFinite(radius) || radius <= 0 || !Number.isFinite(aspect) || aspect <= 0) { throw new Error('M3: невозможно определить обзор системы без корректных радиуса и пропорций.'); }
    const bounds = new THREE.Box3(new THREE.Vector3(-radius, -8, -radius), new THREE.Vector3(radius, 8, radius));
    return createCameraPreset('system-overview', { bounds, aspect, fov: 45, direction: new THREE.Vector3(0, 0.8, 1), padding: 1.15, distanceScale: 0.8 });
}

// Camera и look-at вращаются вместе: pan-offset не становится центром орбиты.
export function orbitAboutPivot(camera, target, pivot, dx, dy)
{
    if (![dx, dy].every(Number.isFinite)) { throw new Error('M3: неверное смещение камеры.'); }
    const up = camera.up.clone().normalize();
    const yaw = new THREE.Quaternion().setFromAxisAngle(up, -dx * ORBIT_RADIANS_PER_PIXEL);
    const direction = camera.position.clone().sub(target).applyQuaternion(yaw);
    if (direction.lengthSq() < 1e-12) { return; }
    const polar = Math.acos(THREE.MathUtils.clamp(direction.clone().normalize().dot(up), -1, 1));
    const pitch = THREE.MathUtils.clamp(polar - dy * ORBIT_RADIANS_PER_PIXEL, 1e-4, Math.PI - 1e-4) - polar;
    const right = new THREE.Vector3().crossVectors(up, direction).normalize();
    const rotation = new THREE.Quaternion().setFromAxisAngle(right, pitch).multiply(yaw);
    camera.position.sub(pivot).applyQuaternion(rotation).add(pivot);
    target.sub(pivot).applyQuaternion(rotation).add(pivot);
    camera.lookAt(target);
    camera.updateMatrixWorld();
}

export function radiusAboutOrigin(bounds)
{
    if (!bounds || bounds.isEmpty()) { return 150; }
    return Math.max(10, new THREE.Vector3(Math.max(Math.abs(bounds.min.x), Math.abs(bounds.max.x)), Math.max(Math.abs(bounds.min.y), Math.abs(bounds.max.y)), Math.max(Math.abs(bounds.min.z), Math.abs(bounds.max.z))).length());
}
