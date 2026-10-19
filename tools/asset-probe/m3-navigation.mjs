import * as THREE from 'three';

export const SYSTEM_HIT_RADIUS = 18;
export const ZOOM_THRESHOLDS = Object.freeze({ galaxy: Object.freeze({ near: 1.7, far: 1.95 }), system: Object.freeze({ near: 2.2, far: 2.5 }) });
const finitePoint = point => point && Number.isFinite(point.x) && Number.isFinite(point.z);
const fail = message => { throw new Error(`M3 navigation: ${message}`); };

export function nearestProjectedTarget(targets, camera, rect, clientX, clientY, radius = SYSTEM_HIT_RADIUS)
{
    if (![rect.left, rect.top, rect.width, rect.height, clientX, clientY, radius].every(Number.isFinite) || rect.width <= 0 || rect.height <= 0 || radius <= 0) { return null; }
    const x = clientX - rect.left, y = clientY - rect.top;
    if (x < 0 || y < 0 || x > rect.width || y > rect.height) { return null; }
    camera.updateMatrixWorld();
    let nearest = null, distance = radius * radius;
    for (const target of targets)
    {
        const p = new THREE.Vector3(target.position.x, target.position.y ?? 0, target.position.z).project(camera);
        if (![p.x, p.y, p.z].every(Number.isFinite) || p.z < -1 || p.z > 1 || Math.abs(p.x) > 1 || Math.abs(p.y) > 1) { continue; }
        const d = ((p.x + 1) * rect.width / 2 - x) ** 2 + ((1 - p.y) * rect.height / 2 - y) ** 2;
        if (d < distance || d === distance && (!nearest || target.id < nearest.id)) { nearest = target; distance = d; }
    }
    return nearest;
}

export function zoomDetail(kind, normalizedDistance, previous = 'far')
{
    const thresholds = ZOOM_THRESHOLDS[kind];
    if (!thresholds || !Number.isFinite(normalizedDistance) || normalizedDistance < 0) { fail('Invalid zoom distance/view.'); }
    if (normalizedDistance <= thresholds.near) { return 'near'; }
    if (normalizedDistance >= thresholds.far) { return 'far'; }
    return previous === 'near' ? 'near' : 'far';
}

export function navigationScale(state, view)
{
    if (view.kind === 'system') { return state.spatial.systems.find(system => system.id === view.systemId).radius; }
    const bounds = new THREE.Box3();
    for (const system of state.spatial.systems) { bounds.expandByPoint(new THREE.Vector3(system.galaxyPosition.x, system.galaxyPosition.y, system.galaxyPosition.z)); }
    return bounds.isEmpty() ? 1 : Math.max(1, bounds.getSize(new THREE.Vector3()).length() / 2);
}

export function validateNavigationSnapshot(state)
{
    const systems = new Set(state.spatial.systems.map(system => system.id));
    const connected = (a, b) => state.spatial.hyperlanes.some(lane => lane.fromSystemId === a && lane.toSystemId === b || lane.toSystemId === a && lane.fromSystemId === b);
    if (state.spatial.exits !== undefined)
    {
        if (!Array.isArray(state.spatial.exits)) { fail('Exit array is required.'); }
        const keys = new Set();
        for (const exit of state.spatial.exits)
        {
            const key = `${exit.systemId}\0${exit.destinationSystemId}`;
            if (!systems.has(exit.systemId) || !systems.has(exit.destinationSystemId) || !connected(exit.systemId, exit.destinationSystemId) || !finitePoint(exit.position) || keys.has(key)) { fail('Invalid or duplicate received exit.'); }
            keys.add(key);
        }
    }
    for (const fleet of state.spatial.fleets)
    {
        const movement = fleet.movement;
        if (!movement?.target) { continue; }
        const target = movement.target, route = movement.routeSystemIds;
        if (!systems.has(target.systemId)) { fail('Target system is unavailable.'); }
        if (target.kind === 'point')
        {
            if (!finitePoint(target.position) || !Array.isArray(route) || route.length !== 0) { fail('Invalid point route.'); }
        }
        else if (target.kind === 'system')
        {
            if (!Array.isArray(route) || route.length < 2 || route.at(-1) !== target.systemId || new Set(route).size !== route.length || route.some(id => !systems.has(id)) || route.slice(1).some((id, index) => !connected(route[index], id))) { fail('Invalid received system route.'); }
            const location = fleet.location;
            if (location.kind === 'system' ? route[0] !== location.systemId : route[0] !== location.fromSystemId || route[1] !== location.toSystemId) { fail('Route does not start at received location.'); }
        }
        else { fail('Unsupported received target.'); }
    }
}

export function receivedRoute(state, fleet, view)
{
    const movement = fleet.movement, target = movement?.target;
    if (!target || fleet.phase === 'idle') { return null; }
    const location = fleet.location;
    if (view.kind === 'system')
    {
        if (location.kind !== 'system' || location.systemId !== view.systemId) { return null; }
        let destination;
        if (target.kind === 'point' && fleet.phase === 'movingLocal' && target.systemId === view.systemId) { destination = target.position; }
        else if (target.kind === 'system' && ['approachingExit', 'charging'].includes(fleet.phase)) { destination = state.spatial.exits?.find(exit => exit.systemId === view.systemId && exit.destinationSystemId === movement.routeSystemIds[1])?.position; }
        return destination ? { points: [new THREE.Vector3(location.position.x, 0, location.position.z), new THREE.Vector3(destination.x, 0, destination.z)] } : null;
    }
    if (view.kind !== 'galaxy' || target.kind !== 'system') { return null; }
    const points = movement.routeSystemIds.map(id => { const p = state.spatial.systems.find(system => system.id === id).galaxyPosition; return new THREE.Vector3(p.x, p.y, p.z); });
    if (location.kind === 'transit') { points[0].lerp(points[1], location.progress); }
    return { points };
}

export function routeGeometry(points, width = 0.8)
{
    const vertices = [];
    const segment = (a, b) => vertices.push(a.x, a.y, a.z, b.x, b.y, b.z);
    for (let index = 1; index < points.length; index++)
    {
        const a = points[index - 1], b = points[index], vector = b.clone().sub(a), length = vector.length();
        if (length < 1e-8) { continue; }
        const direction = vector.normalize(), side = new THREE.Vector3(-direction.z, 0, direction.x).multiplyScalar(width);
        const count = Math.max(1, Math.min(256, Math.ceil(length / (width * 7))));
        for (let dash = 0; dash < count; dash++)
        {
            const start = a.clone().addScaledVector(direction, length * dash / count), end = a.clone().addScaledVector(direction, length * (dash + 0.72) / count);
            segment(start.clone().add(side), end.clone().add(side)); segment(start.clone().sub(side), end.clone().sub(side)); segment(start.clone().add(side), start.clone().sub(side));
        }
        const arrow = b.clone().addScaledVector(direction, -Math.min(length * 0.3, width * 5));
        segment(arrow.clone().addScaledVector(side, 2.5), b); segment(arrow.clone().addScaledVector(side, -2.5), b);
    }
    return vertices;
}

export function withinManualRadius(point, radius)
{
    return finitePoint(point) && Number.isFinite(radius) && radius > 0 && Math.hypot(point.x, point.z) <= radius;
}
