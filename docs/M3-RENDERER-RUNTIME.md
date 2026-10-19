# M3 — game renderer API

Pre-implementation sub-contract for the [interface](M3-CLIENT-INTERACTIONS.md). A new module world renderer, separate from the static preview; Three.js 0.186.1, same static material adapter. No WebSocket/authentication/BFS/local movement calculation.

`new M3WorldRenderer(canvas,{onSelect,onOpenSystem,onPointOrder,onSystemOrder,onHint,onContextState})` owns WebGLRenderer/OrbitControls, canvas handlers, view resources, and one validated GPU profile. The callback passes intents to the UI; it does not send commands itself.

- `setProfile(profile,{maxConvertedBytes})`: atomically prepare all four template groups and shared `(path,mode)` textures. Account for old+new converted memory and the caller's CPU budget. An error preserves the previous set. Repeating the same object does not reconvert it.
- `applySnapshot(state,{receivedAt,resetInterpolation=false})`: complete allowed v2; current sessionId/increasing sequence only. The first snapshot after a new sessionId clears selection/commandFleet; a new session requires a new setProfile, with re-verification handled by the UI. The `kind` union must match SESSION-RUNTIME exactly. Foreign movement may be absent.
- `showGalaxy()`, `showSystem(systemId)`, `showStress200()`: explicit views. SystemId is resolved only from the snapshot. Stress uses local instancing200 and issues no orders.
- `select({kind,id}|null)`, `setCommandFleet(fleetId|null)`, `setConnectionFresh(bool)`: visibility/availability are checked against the current snapshot; the selected fleet must belong to you/player. Disappearance clears selection, and callbacks notify the UI.
- `resetCamera()`, `focusSelection()`, `resize()`, `render(now)`, `stats()`, `dispose()`. render is called by the sole external RAF; the renderer does not start a second loop. stats includes draw calls/geometry/textures/converted bytes/frame time and viewport, without claiming an SLA.

onSelect receives `{kind:'system'|'body'|'fleet'|'colony',id}` or null; onOpenSystem/onSystemOrder receive systemId, onPointOrder receives `{systemId,x,z}`, onHint receives safe Russian text, and onContextState receives lost/restored. Gameplay intents require fresh + asset profile + running + own selected fleet + not stress/contextLost. A transit point is invalid. The UI additionally checks connection/assetReady freshness and creates commandId.

All template-clone models share geometry/material/texture; the source origin is preserved, and entity×DTO scale is applied once. Decorative offsets for the three corvettes follow CLIENT-INTERACTIONS. View-only markers/lines are released on switching; profile assets only when the set changes/dispose. Custom markers are allowed for geometry picking, but system models are not replaced with primitives. Context restore rebuilds GPU resources from CPU, does not reread the source or send intents; commands remain blocked until state is fresh.

Pure helpers live in a separate module: interpolation/position validity, visible view-object collection, owner/phase command gating, and selection reconciliation; Node tests need no WebGL. Check actual browser rendering, mouse gestures, 100 switches/resource count, context restoration, and stress200 separately. Node helpers do not establish driver/FPS/OS-permissions behavior.
