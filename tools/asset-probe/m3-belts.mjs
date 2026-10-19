import * as THREE from 'three';
import { displayedSurfaceRadius, templateDisplayFactor } from './m3-presentation.mjs';

const VISUALS = new Map([['rocky_asteroid_belt', 'world.pc_asteroid'], ['icy_asteroid_belt', 'world.pc_ice_asteroid']]);
export function validateBelts(belts, systems)
{
    if (belts == null) { return; }
    if (!Array.isArray(belts)) { throw new Error('M3: неверные пояса.'); }
    const ids = new Set(); const permitted = new Set(systems.map(s => s.id));
    for (const belt of belts)
    {
        if (typeof belt?.id !== 'string' || !belt.id || ids.has(belt.id) || !permitted.has(belt.systemId) ||
            ![belt.radius, belt.width, belt.density].every(v => Number.isFinite(v) && v > 0) || VISUALS.get(belt.type) !== belt.visualId)
        { throw new Error('M3: неверная геометрия или binding пояса.'); }
        ids.add(belt.id);
    }
}
export const beltOuterRadius = belt => belt.radius + 6 * belt.width + 1.1;

// RepresentativeAsteroidBeltV1: собственная browser calibration, отдельный hash PRNG.
export function beltDecorations(belt)
{
    let state = 2166136261;
    for (const char of belt.id) { state = Math.imul(state ^ char.charCodeAt(0), 16777619) >>> 0; }
    const next = () => { state = (Math.imul(state, 1664525) + 1013904223) >>> 0; return state / 4294967296; };
    const count = Math.max(32, Math.min(160, Math.round(belt.radius * belt.density / 2)));
    return Array.from({ length: count }, () => {
        const angle = next() * Math.PI * 2;
        const radius = Math.max(0, belt.radius + (next() * 2 - 1) * 6 * belt.width);
        return { x: Math.cos(angle) * radius, z: Math.sin(angle) * radius, radius: .3 + next() * .8,
            rotation: [next() * Math.PI * 2, next() * Math.PI * 2, next() * Math.PI * 2] };
    });
}

// Clones заимствуют imported geometry/materials; releaseView удаляет только группу.
export function createBeltGroup(belt, template)
{
    const group = new THREE.Group(); group.name = belt.id;
    const unitRadius = displayedSurfaceRadius(template, belt.visualId, 1);
    const factor = templateDisplayFactor(template, belt.visualId);
    for (const item of beltDecorations(belt))
    {
        const stone = new THREE.Group();
        stone.position.set(item.x, 0, item.z); stone.rotation.set(...item.rotation);
        const clone = template.clone(true); const scale = factor * item.radius / unitRadius;
        clone.scale.multiplyScalar(scale); clone.position.multiplyScalar(scale);
        clone.traverse(child => { delete child.userData.pick; child.raycast = () => {}; });
        stone.add(clone); group.add(stone);
    }
    return group;
}
