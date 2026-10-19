# CAM-001 — fix validation

October 12, 2026. Contract: [client controls](../M3-CLIENT-INTERACTIONS.md). Scope: camera only; no materials, UI, server simulation, or asset-profile changes.

## Implemented

Separate pivot for the selected displayed object, with fallback to the view origin. Alt-drag rotates camera and look-at together, preserves pan-offset, and does not jump on pointerdown. The pivot for a moving fleet is resolved before each gesture. Reset accounts for bounds relative to origin; explicit focus preserves offset/zoom.

Alt cancellation and ordinary native pan are cleared on cancel, capture/focus loss, pagehide, and dispose. Independent review found a stale native gesture; the fix using public OrbitControls disconnect/connect is covered by regression. No substantial review findings remain.

## Results

- Node camera + world-renderer + world-view: 20/20 PASS, no SKIP. Real Three/OrbitControls; GPU was substituted only in the constructor-wiring test. Checked no-jump, quaternion/distances, pan/dolly after rotation, colony/fleet/fallback, pole guard, reset/focus, cancellation, and disposal.
- Node m3-app: 11/11 PASS, no SKIP; regressions for import, atomic replacement, pagehide/pageshow, and context loss.
- Release build Stellaris.Server: PASS, 0 warnings / 0 errors. New module included in output/publish; source and output SHA256 match.
- `git diff --check`: PASS.

Not performed in this step: real browser Alt-drag, repeat user inspection, GPU/FPS/Chrome permissions. Node dispatch is not physical Chrome input. CAM-001 remains open pending visual confirmation. FOV, native pitch/zoom, materials, and relative scales were not changed in this step.

Integrator rechecked `4b387c7` after merge: independent read-only review with no P1/P2, 20 camera/world-renderer/world-view and 11 static-app tests PASS, Release 0 warnings / 0 errors. Production include was checked through `wwwroot/m3-modules/m3-camera.mjs`; source and output SHA256 matched. These are agent checks, without physical browser input.
