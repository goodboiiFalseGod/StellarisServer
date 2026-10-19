# M3 — Defined graphical slice

Status: main boundaries, M01–M03, and N01 are agreed; subcontracts are documented. The playable M3 is not ready yet; the limited BGRA8 reader has been checked (M3-IMPORT-VALIDATION). [Answers](M3-QUESTIONS.md) and [remaining questions](M3-DISPUTES.md). The content plan remains in force: this defines an intermediate delivery, not the entire first release.

## Agreed result

Run one server, two independent Google Chrome clients, and an observer on one PC. For acceptance, use three separate profiles with independent tokens and asset memory; all may choose the same physical installation. There is no persistent cache. Each player selects an installation and joins the lobby; the host starts a fixed world paused. A player sees the galaxy, opens a system, selects a planet/fleet, orders movement within a system, observes server execution, and returns after a connection drop. M2 economy is available through the colony card, with formulas and separate upkeep preserved. This is not a full Stellaris session.

## Agreed test world and visual set G01/G03

- Fixed reproducible map: 16 systems with specified hyperlanes; two players, each with one colony and a fleet of three identical corvettes. Each system has one star and one planet; starting systems have one station each. This is not a 200–600 system generator.
- One mammalian ship set, a real corvette from the experiment; one star class, continental planet, one station, minimal resource/object icons. Specific .gfx/.asset/.mesh/DDS and UI files are listed in M3-ASSET-MANIFEST. A planet/station that has not yet been visualized cannot be considered ready based on the corvette results.
- Ship/planet/star/station use original geometry and selected material channels. For the test, an agreed static shape with a correct bind pose is used if needed; bone animation, particles, combat VFX, and full PDX lighting are out of scope (G02). Skin cannot simply be discarded without checking the shape.
- In the galaxy, show systems and connections in 3D, with empire and fleet labels; do not draw full ship models for every system simultaneously. In a system, show the selected set, camera, and markers. Spatial coordinates and object positions belong to the server.
- Arkship need not be shown in this slice; stable IDs and fleet/colony/starbase links must not prevent a future mobile colony. This does not exclude the agreed Voidfarers from the first release.

## Controls, UI, and server boundaries

G06 agreed: Russian dark UI with a layout close to Stellaris, without pixel-level matching. DOM: connection/import, lobby, date/resources/pause/speed HUD, outliner, selection card, network status/pending commands/errors. Planet shows M2 economy with separate JobUpkeep and BuildingUpkeep; job editor is clearly labeled as a laboratory. Another player's economy is not shown.

Left-click selects; double left-click on a system opens it; Esc closes the top card, then returns to the galaxy; wheel zooms; drag moves the camera; a separate modifier rotates it. Right-click with one's own fleet selected sets a destination; an equivalent visible button is available. Specific rotation/pan controls, focus, and conflicts with DOM must be recorded in a UI subcontract after G06. Only server-authorized objects are shown on the map; UI does not decide ownership or command validity.

G04/M01 agreed: movement toward a point in the system plane at constant speed, without acceleration or turn limits. Fleet position and command execution are authoritative on the server; the client only interpolates authorized state and visually places ships, without physics-based formation simulation. One active command; a new command replaces the previous one; stop halts at the current position. On disconnect, an accepted command continues, but no new commands are allowed.

M02 agreed: inter-system route includes approach to the exit, preparation, hyperlane jump, and appearance at the next system's entrance. Approach cannot be replaced by teleportation. During a hyperlane jump, stop/replacement applies after arrival. Speeds, phase durations, preparation cancellation, routing, and validation are described in M3-TIME-AND-MOVEMENT; entrances/exits are in M3-WORLD-FIXTURE. Parameters 7.2 units/day and 15 days are derived from 4.5.2; radii and a 5-day transition were separately agreed in N01 for M3 only.

G05 agreed: the test world is known in advance from public spatial information; economy is private. This is a scenario, not disabling filtering or implementing exploration. A test entity is hidden from one player: its ID/coordinates do not appear in that player's projection/errors. Once approved, the observer sees the whole world without controls.

G07/M03 agreed: start 2200.01.01, 12 months of 30 days each, speeds 1/2/4 game days per real second; one-day step only while paused. Date is tracked exactly by day, and monthly economy is calculated once at the month boundary. This is M3 laboratory pace, not a promise of vanilla balance. Pause stops game time, not real-time reconnect/timeouts. Daily phases, date changes, and month boundary are described in M3-TIME-AND-MOVEMENT. User authorized M3 without saves: process restart ends the world, old tokens become invalid; normal reconnect preserves return. Stable IDs/versions remain required; target saves in D27 are not canceled.

Preserve M2 session authority, commandId/idempotency, ownership checks, host transfer, and current snapshot recovery. Do not switch transport to binary/deltas without measurement. Protocol version, spatial DTO (system/body/colony/fleet/route, phase/position/tick), movement commands, and errors are described in M3-SPATIAL-PROTOCOL; do not send asset paths/source files. The card for a removed or no-longer-visible object closes; stale state after a disconnect is clearly marked.

## Implementation order and acceptance

Subcontracts: [exact assets/reader](M3-ASSET-MANIFEST.md), [time and movement](M3-TIME-AND-MOVEMENT.md), [spatial network](M3-SPATIAL-PROTOCOL.md), [world fixture](M3-WORLD-FIXTURE.md). These are requirements; current implementation and verification status are tracked separately in the [integration report](M3-INTEGRATION-VALIDATION.md). Numeric profile N01 is agreed for M3 only.

1. Answers I01–I04/G01–G08, M01–M03, and N01 are recorded. Manifest, movement, daily phases, and DTO/commands are described in subcontracts; exact UI interactions and unresolved technical budgets must be added before dependent code. Checks and remaining questions are in M3-DISPUTES.
2. Local import and errors → static original set in Three → server fixed world and projections → UI/galaxy shell → system/cards → movement/return → integration acceptance. Do not add research, construction, or combat along the way.
3. Two clients + observer: import → lobby → start → switch views → select/move to point/stop/replace command → approach and hyperlane jump → pause/speed/day-step/month boundary → disconnect/AFK/reconnect → host transfer. Check deferred cancellation during hyperlane jump, no control of another player's assets, duplicate commands, hidden objects, and errors without data leaks.
4. Repeat M1/M2 checks; importer/unit/protocol/browser tests, manual camera/selection in Chrome. 100 galaxy↔system switches: no accumulating live GPU resources/handlers. Check resize, empty selection, context loss, missing asset. Keep report separate from specification.
5. G08 agreed: 1920×1080/DPR=1, ≥60 FPS on current RTX 4070 in base map and system; stress case of 200 corvettes, frame p95 ≤33.3 ms. Measure one active visible Chrome client while server and two other clients are connected on the same PC; do not require 60 FPS in background. Three 60-second runs after 10-second warmup; record viewport, scene composition, speed, CPU/GPU, and browser. Set import ≤60 seconds. Previous target “from persistent cache ≤10 s” does not apply: cache was explicitly rejected in I03, so reload requires a new import. These are criteria for the current PC, not measurements or a minimum-hardware guarantee. Measure RAM/VRAM, network, and server tick and agree on thresholds before load acceptance.

M3 closes after review, report, and user's manual test. LAN, other GPUs/browsers, full materials, saves, and a large map are not claimed as checked if excluded by the agreed scope.
