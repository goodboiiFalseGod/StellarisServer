# M3 reference-driven visual revision

## Authority and status

Later direct user amendment: [personal outliner and observer perspectives](M3-PLAYER-PERSPECTIVES.md). Its server-projected POV and personal membership rules supersede the generic outliner assumptions below; acceptance remains open.

User turns `01a1302b-dc7f-79c1-8f43-12bc2b5c059f`, `01a1302f-e4f0-7a00-8955-bb09b53e1a7f` and `01a13030-6537-7ca1-bbf3-b039112f599f` in the orchestrator authorize matching the supplied interface and maximum practicable 3D similarity with minimal content. This supersedes the earlier prohibition on galaxy presentation changes in M3-VISUAL-FIDELITY and MAP-001. Delivery `e7fb05c` failed visual acceptance; functional test results remain valid only within their recorded limits. Reviewed feedback baseline: `f5600bb`.

Galaxy topology, authoritative coordinates, simulation, movement, filtered visibility and participant permissions remain unchanged. Do not create fictitious selectable systems, resources, territories, ships or UI statuses. Decorative background stars must be noninteractive and cannot imply undisclosed world data. Preserve the four models and existing game content. No construction work or shared stash is included.

## Reference inventory

All seven images were inspected by the integrator, UI owner and graphics owner before this contract. Local clipboard images are evidence only: never copy them or original game assets into Git or upload them. Filenames below identify local evidence, not distributable dependencies.

| ID | Image filename | Comparison purpose |
|---|---|---|
| R1 | codex-clipboard-d7d25f52-eedf-4dc1-8451-d942113ac5de.png | Original galaxy overview and HUD |
| R2 | codex-clipboard-5781ba71-9082-43e8-966e-4c092cf1ed8d.png | Original whole system, sky, orbits and labels |
| R3 | codex-clipboard-1ecd506f-5ef8-4f77-9736-8603f07326c8.png | Original close star/planet, corona and terminator |
| R4 | codex-clipboard-2b6a0242-3cf7-408d-a2c7-4e610af3908b.png | Rejected prototype galaxy and shell |
| R5 | codex-clipboard-a775ea5b-aa97-430c-a1c0-eb04f849907c.png | Rejected prototype system and shell |
| R6 | codex-clipboard-f4c126ca-6d8a-40a6-ba12-9a766364956a.png | Revealed original galaxy, far view |
| R7 | codex-clipboard-94b79904-f4cf-4b65-945c-c7a3948f02dd.png | Revealed original galaxy, near view |

Normalize comparisons to the game viewport, excluding browser chrome. Original images and prototype images differ in dimensions and scroll position. Screenshot ratios are presentation targets, not proof of native formulas. Record viewport, DPR, profile/digest, view, selection and camera preset for each new screenshot.

## Required presentation and checks

| Component | Required revision | Review and observable acceptance |
|---|---|---|
| Viewport and HUD | Map fills the available viewport behind compact anchored overlays. Thin resource/time bar, left navigation, compact right outliner, bottom identity/navigation. Setup and diagnostics become explicitly openable panels. | At 1920x1080 and 1280x720 game viewports: no document scrolling; map remains dominant; connection, command feedback and essential controls remain accessible. Compare R1/R2, not raw browser screenshot dimensions. |
| Real-data interface | Dense hierarchical outliner using filtered objects; contextual selection/orders cards; bottom current-view/identity plaque; restrained borders, typography and icon hierarchy. | No raw JSON as primary selection UI, no empty permanent orders column, no fake reference categories/counters. Selecting foreign objects never changes the owned command fleet. Observer remains read-only. |
| Galaxy | Source-backed core/dust/starfield, varied luminous star markers, thin cyan lanes and zoom-dependent readable labels. | Compare far and near presets against R1/R6/R7. Existing 16 systems and 24 lanes retain exact topology/positions and picking identity; background decoration is nonpickable. No artificial content density. |
| System | Source-backed sky, subtle orbit context, readable labels and better overview/close framing. Correct relative apparent sizes of existing bodies and models. | Compare R2 overview and R3 close separately. Station cannot dominate the planet by accidental model-unit treatment. Record model bounds, transform chain and presentation calibration; do not change movement coordinates to improve framing. No fabricated asteroid belts or objects. |
| Surface, light and corona | Source-backed star material with contiguous rim/corona, planet day/night terminator and coherent stellar illumination. Remove unexplained detached oversized aura through a documented adapter correction. | Source/channel/blend/culling evidence plus real GPU screenshots. Preserve required model/material layers; do not silently substitute generic assets. State approximations to native renderer explicitly. |
| Camera | Reproducible galaxy far/near and system overview/close presets, with normal pan/zoom/selection afterward. | CAM-001 no-selection galaxy rotation after pan is confirmed FAIL in f5600bb: investigate physical galaxy center versus origin. Selected and no-selection system cases were user PASS; retain regression coverage. Selection changes must update pivot without a jump. |

Use local Stellaris 4.5.2 interface definitions, gfx/worldgfx, shader, sprite/texture and transitive dependencies. Reuse completed source audits; ask the reference chat precise unresolved questions. Visual calibration against supplied screenshots is permitted when native composition is unavailable, but label it as browser presentation calibration rather than a recovered native formula. Unknown gameplay rules remain pending and go to the manual queue if needed.

## Dependencies, failures and lifecycle

Import owner must first record exact additional local paths, source relationships, formats, channels/color space, limits and allowlist/profile changes in the owned import contract. Only the minimum dependencies needed for the above presentation may be added. Preserve strict hash/version checks, no upload/persistent cache, bounded allocation and disposal on failed staging. Missing or unsupported files must yield an actionable import failure, never a silent substitute or false ready state. Graphics may develop independent composition against current imported content while dependencies are prepared.

Keep overlay keyboard/pointer capture isolated from map controls; close/cancel must release capture. Reconnect and view changes must reconcile stale selection/orders. Dispose superseded view/DOM resources and failed staged import resources; ordinary view switches retain shared profile assets, and failed replacement preserves the previous valid profile as required by M3-RENDERER-RUNTIME and M3-STAR-IMPORT. Repeated galaxy/system/stress transitions must not grow retained resources. Preserve ACK/error feedback, setup/economy access, connection state and participant approval behavior.

## Single-writer assignment for this revision

These assignments supersede the earlier ownership table only for this revision. Owners start from the committed contract after preserving their own completed work. No owner edits another owner's files, even in a separate worktree.

| Owner chat | Exclusive production scope | First checkpoint |
|---|---|---|
| UI, 01a12fe9-8211-7952-af60-6b03ae0f742a | src/Stellaris.Server/wwwroot/m3.html, m3.css, m3-client.mjs; new UI-only modules/tests; docs/M3-UI-VISUALS.md | Implement viewport shell and real-data contextual UI independently; report source-backed layout and screenshots. m3-client.mjs is explicitly assigned to UI. |
| Graphics, 01a12fe9-6e0a-7722-b51f-f1a8b59b5956 | tools/asset-probe/m3-world-renderer.mjs, existing/new graphics material/presentation modules, m3-camera.mjs and their tests; docs/M3-SYSTEM-VISUALS.md | Implement overview/close composition, galaxy/system presentation and bounded CAM-001 triage. No client, world-view, static preview renderer, import or profile edits. |
| Import, 01a12c39-bdd9-70e0-a723-b8dc05e0285b | Existing importer/readers/adapters/profile/server manifest files and their tests; docs/M3-STAR-IMPORT.md and dependency manifest contract | Targeted UI/background/sprite source dependency inventory and minimal explicit allowlist first, then implementation. No renderer, client, camera, csproj or main edits. |
| QA, 01a12d6b-8e8f-7723-a719-e75c3b77eb14 | New qa-visual tests and docs/reports/visual-qa-validation.md | Prepare comparison matrix/presets and baseline evidence; independently test ready commits on port 5093. No production edits. |
| Integrator, 01a12d71-f1d2-7e31-b692-caaeebd67ace | Shared contracts, decisions/backlog, world-view integration if needed, csproj, reviewed merges and final main handoff | Review every finished chunk and dependency interface; integrate only ready commits. |

New shared interfaces must be sent to the integrator with exact exported names/data before consumers depend on them. Graphics camera presets may be exported for UI consumption; UI must not independently implement renderer logic. Graphics and UI report any import requirements to the import owner/integrator. File paths not actually present require correction by the integrator before edits.

## Delivery gate

First checkpoint is independently reviewable UI shell and scene composition commits with tests and component screenshots, not another broad research report. Final gate includes applicable Node/server/asset checks, real GPU import/render inspection, four preset comparisons, controls/privacy/reconnect regression checks and resource lifecycle checks. Unit PASS alone cannot close visual gaps. Record remaining physical browser/GPU/native-fidelity conditions without blocking independent implementation. Notify the user only after the reviewed stable main checkout, exact commit and updated launch/manual checklist are ready. User visual acceptance remains open until their subsequent test.
