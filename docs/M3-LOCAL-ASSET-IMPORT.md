# M3 — Local asset import contract

Current clarification, October 16: [any positive number of players](M3-PLAYER-COUNT.md); two-player examples below do not limit lobby membership.

Status: I01–I04 agreed; file profile/digest and safety limits are described in M3-ASSET-MANIFEST. Limited BGRA8 reader checked; full import is not ready yet. Persistent cache rejected. Answers: [M3-QUESTIONS](M3-QUESTIONS.md), remaining questions: [M3-DISPUTES](M3-DISPUTES.md). Basis: DECISIONS, doc08, [experiment](ASSET-PROBE-REPORT.md).

## Confirmed boundaries

- Host/server and each browser use their own Stellaris installation. Server reads game content locally; browser imports its own graphics resources. Providing a server path does not grant browser access.
- Installation is read-only. Do not modify it, upload original/derived assets to the game server, include them in Git, or distribute the host's assets to participants. Importer code and technical manifests without personal paths are allowed.
- Three.js 0.186.1/WebGL2; DOM UI. Importer is separate from renderer and server simulation; missing graphics do not give the client permission to change mechanics.
- Limited PDX mesh 1.0 and DDS DXT1/3/5 have been checked; this is not support for all formats. Skin/skeleton, animation, original shaders, and VFX require separate processing and verification.

## Import flow

Later direct user clarification on 14 October 2026 supersedes the previous interpretation: host chooses shared settings, others see them without edit permission. All players explicitly click “Готов” (“Ready”) before generation. Only then does host click “Начать” (“Start”); settings are applied and the galaxy is generated. Readiness applies to the selected settings/presets/player roster, not a manifest that does not yet exist. After generation, another Ready and automatic host Ready are unnecessary. Current implementation 45fa87d used the wrong order and is not accepted against this requirement.

Correction contract: server publishes shared draft and monotonic Galaxy.LobbyRevision; ready{ready,expectedLobbyRevision} records player consent, start{expectedLobbyRevision} is permitted to host only when all connected/current-ready players have selected presets. Any draft/preset/roster/player connectivity or host change clears consent and pending-start; observer does not vote. First start atomically prepares the world, preserves consent, and publishes StartPending. After exact manifest/profile/fingerprint ACK from all players, a second internal start with the same revision and exact candidate atomically starts the world, with no second human click. Separate prepareGalaxy does not bypass this order in generated production. Generation error preserves prior draft/consent/revision/manifest; final startup error preserves prepared candidate for retry.

The folder may be selected before generation: its read-only handle exists only in the current tab, localization is checked, and unneeded graphics from an old fixture are not decoded ahead of time. After generation, the client automatically reads files from the current manifest in the selected folder if permission remains. A mismatched stale profile in the lobby may be explicitly released before staging, preserving the shared 512 MiB limit; this does not discard an active gameplay profile. Missing files, revoked permission, and cancellation produce an explicit stopped phase/reselection; incomplete File fallback is not considered a verified set. File acknowledgments and server-owned candidate guards are preserved. These are correction requirements, not proof of a completed implementation.

M3 is checked on one PC through localhost in Google Chrome. LAN and native helper are out of scope. Manifest and safety limits are in M3-ASSET-MANIFEST; exact worker lifecycle must be added before dependent code.

1. Server builds an M3 profile from its own installation: profile ID, schema and importer version, game version, required content/visual IDs and allowed relative dependencies, SHA-256. Profile contains only the required set, not a copy of the whole game. Host sees missing-content diagnostics; invalid profile does not start a new session.
2. Browser selects game root through a user action with read-only access; fallback is directory selection through file input if confirmed for the agreed launch mode. No native helper. Do not ask users to disable browser protection or bypass TLS warnings.
3. Client resolves only dependencies of the active profile (.gfx/.asset → .mesh/material → DDS and selected UI resources), normalizes relative paths, rejects paths escaping root and ambiguous duplicates. Do not scan/decode the entire catalog and all DLC in advance.
4. Verify profile, file presence, and hashes; prepare CPU data in browser, then create GPU resources. Progress shows checked/total, current phase, and reason for error. Heavy conversion is cancellable and must not block UI; concrete worker contract is fixed before code.
5. Publish new local set atomically only after all required dependencies are verified. On cancellation/error, previous valid set remains available; files from two profiles must not be mixed. Entering graphical session is allowed only for a compatible profile.
6. No persistent asset cache: do not store original/derived game resources in IndexedDB, Cache API, localStorage, or a disk package. CPU/GPU data is reused in current-page memory, including view switches and network reconnect; this is not a disk cache. Reload requires a new import. For a new game session, source is checked again even if the page remains open; without access, new session cannot start. Persisted directory permission is not promised. This prohibition does not cancel M2 session-token storage.

Client readiness tells the server only profile ID/digest and compatibility status, without file paths/bytes. This is a UX check, not proof of license, file ownership, or protection against a modified client. Server still validates commands and state independently.

## Compatibility and resources

Exact selected set, manifest/digest versions, new BGRA8/rigid skin requirements, and specific technical limits are in [M3-ASSET-MANIFEST](M3-ASSET-MANIFEST.md). Previously proposed limits are elaborated there; these are implementation safety boundaries, not measured performance.

- For M3, pin profile to installed 4.5.2. Compare required files; do not require byte-for-byte equality for unused DLC. Any mismatch in a required dependency is an explicit rejection, not silent fallback (I02).
- Pin exact G01 asset paths before implementation. Unsupported required shader/mesh needs an agreed handling path, not automatic replacement with a sphere/cube or omission of skin. Alternative requires a separate user decision.
- Safety limits are in M3-ASSET-MANIFEST: file ≤64 MiB, DDS side ≤8192, and decoded-payload limits; also check vertex/index counts before allocation. These are engineering guards for the selected set, not measured RAM/VRAM budgets. Do not automatically relax stricter limits from the historical test environment.
- Shared textures/geometry have an owner and reference count; changing view/set or closing the page releases no-longer-needed data. On WebGL context loss, show recovery/error without changing game world.

## UI states and acceptance

States: not selected → access granted → checking → converting → ready; separately cancelled, access lost, incompatible, corrupt, limit exceeded, WebGL unavailable. Revoked access is detected on the next read; immediate OS event is not promised. Valid loaded resources remain usable until current session ends; new reads require access again (I04).

Check: clean import, new import after reload, no persistent asset writes, memory reuse on view change/reconnect, profile/source change, cancellation in each phase, denial/revoked permission, missing/corrupt file, unknown format, path traversal, duplicates, limit exceeded, context loss. A set error does not publish partial results. Check a real folder in Google Chrome, not only File; network capture contains no files/local paths. Two clients import independently. Keep report separate from requirements.
