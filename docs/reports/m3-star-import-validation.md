# G-star import validation

## Galaxy core/dust extension — 12 October 2026

Approved pre-code contract: `9fb4f53`, [galaxy import](../M3-GALAXY-PRESENTATION-IMPORT.md). Implemented `m3-static-452-v5`: schema 1, importer/material 4, `m3-static-galaxy-v4`, 63 explicit paths. The three added 2D DDS inputs produce `profile.galaxyLayers` with `core`, `dust`, `color`, raw RGBA images and exact source paths. Existing models/layers, icons, backgrounds, sprites and star surface remain unchanged. Decoder unchanged; no CPU color conversion, image scratch copies or additional original dependencies.

- Full real-content Node regression: **114 PASS, 0 FAIL, 0 SKIP** with `STELLARIS_GAME_ROOT`; same command as the preceding checkpoint. Coverage extends strict dimensions/codec/mips/faces, complete payload/trailing/volume rejection, predecode budget refusal, raw-byte preservation, unique transfer/shared identity and cancellation preserving retained galaxy bytes.
- C# AssetChecks: **9 PASS, 0 FAIL, 1 SKIP** after elevated `--no-build` rerun. Windows junction safety tests pass; symlink creation remains unavailable, not PASS. Browser/server 63-path and version parity pass. Sandbox run initially skipped both junction and symlink; the elevated run resolves only junction.
- Real manifest digest: `bb1d2420552515480f987d1422f67696e6ae93d9578b79c5447d220112b66b34`. Encoded source bytes: **128,489,971**. Decoded payload: **337,550,852**; existing converted arrays: **117,473,408**; total accounted arrays: **455,024,260 bytes (433.94 MiB)**. No ceiling increase. Full active-profile replacement still refuses before outputs and advises reload/reimport.
- Independent implementation review found no actionable P1/P2 issues. Reviewer repeated real-source profile/manifest/pipeline/importer checks: **24 PASS, 0 FAIL, 0 SKIP**. Parent also directly reviewed production and test diffs against the approved pre-code contract.

Source filename-to-sampler binding, native transfer and patch distribution remain unverified. Renderer calibration and real GPU/Chrome/visual acceptance are not import PASS. No renderer/client/camera/UI/csproj/main edits, original images/assets in Git or uploads. Existing v4 checkpoint `ccd58f8` remains separately recoverable.

## Bounded presentation extension — 12 October 2026

Contract: [M3-PRESENTATION-IMPORT](../M3-PRESENTATION-IMPORT.md), reviewed reference baseline `7fe9c0b`, lifecycle clarification `abcd62d`. Decoder checkpoint `7dfa5a5`; profile/server extension follows in the next owned commit. Historical v3 evidence below remains historical, not the current profile.

Implemented `m3-static-452-v4`: 60 explicit original source paths, schema 1, importer 3, material 3 (`m3-static-presentation-v3`). Preserves the four models, star surface and five icons. Adds two shared background images and five UI sprite images with strictly validated source metadata. Stars select source mip 1; packed YCoCg sky uses direct nearest-center factor 2. Each cube has six 1024-square RGBA faces (25,165,824 bytes); decoder instrumentation verifies no full-resolution pixel scratch. No fonts, LUT, center/dust/galaxycolor, shader execution, cache or source uploads.

Validation in `artifacts/worktrees/star-import`:

- Full Node suite with `STELLARIS_GAME_ROOT` set to the local 4.5.2 installation: **114 PASS, 0 FAIL, 0 SKIP**. Command: `node --test --test-reporter=spec tools/asset-probe/*.test.mjs`.
- Independent review: no substantial findings. Reviewer ran **42 PASS, 0 FAIL, 0 SKIP** across decoder and profile/manifest/worker/owner tests. Integrator independently checked the committed decoder, including randomized LOD/default compatibility comparisons.
- C# real-source AssetChecks: **9 PASS, 0 FAIL, 1 SKIP** after the elevated rerun. Windows junction root/ancestor/internal-path guards pass; symlink creation remains unavailable, not PASS. Command: `dotnet run --project tests/Stellaris.AssetChecks -c Release -- --game-root <local-installation>`. Initial sandbox run additionally skipped junction creation; the rerun resolves only that skip.
- JS/C# exact 60-path/version parity and canonical digest pass. Real digest: `d1e226648c7504ba17445acde83f0bf0065f95ed1149e0458ffbf61623b49f44`.
- Real decoded CPU payload: **312,385,028 bytes**. With the existing converted texture arrays: **429,858,436 bytes**, below the unchanged 512 MiB accounted-array ceiling. This is not measured total RAM/VRAM. Retained-profile replacement is rejected before geometry/image decode and advises reload/reimport; cancellation preserves retained background/sprite bytes. Worker structured transfer proves unique buffers and shared identity across the new Maps.

No renderer, client, camera, UI, csproj or main-checkout edits. No original game files or screenshots committed. Renderer/UI consumer integration, real GPU orientation/color/seams, physical Chrome permissions/resource behavior and user visual acceptance remain pending. This import checkpoint is not stable-main or visual readiness.

12 October 2026. Contract: [M3-STAR-IMPORT](../M3-STAR-IMPORT.md). Isolated branch `feature/m3-star-import`, based on `4b387c7`. This is an import dependency, not a completed material or visual fix.

## Implemented

Profile `m3-static-452-v3`: 49 fixed paths, schema1, importer2, material2 (`m3-static-star-v2`). Both browser and server hash the same explicit paths. Strict worldgfx and pc_g_star fields produce `star.g.starSurface`; unsupported/missing/duplicate/type/path/nonfinite inputs fail before decoding. Old metadata is rejected.

Full DDS preflight validates all face/mip payloads before pixel allocations. The cube has six unique base-mip RGBA buffers, no rgba alias, and no duplicated pixel copies in the Three adapter. The profile owns CPU arrays; the caller owns CubeTexture and its DataTexture face wrappers. Worker transfer deduplicates buffers and preserves shared references. Existing cancellation terminates the worker, ignores stale replies and preserves the previously published profile.

## Evidence

- Full Node suite with the real local 4.5.2 source: 92/92 PASS, no skips. Includes legacy decoder/profile/camera/view/app regressions, malformed cubemaps, strict uniforms, budgets, unique transfers and cancellation.
- After merging coordinator policy/contracts and the isolated material module from `f5d6bbf`: 95/95 Node tests PASS, no skips. The material module is inherited integration work, not implemented by the import owner.
- Independent review: no substantial issues; independently repeated focused real-content suite 41/41 PASS, no skips.
- C# AssetChecks with real source: 9 PASS, 0 FAIL, 1 SKIP. Windows junction root/ancestor/internal-path tests pass with elevation. Symlink creation remains unavailable and is not claimed as PASS.
- Real profile: 49 paths, decoded CPU payload 261677756 bytes. Noise accounts for 24 MiB; lava/stone base arrays add 8 MiB. This is not total browser RAM or GPU memory. Old + staged budget checks pass, including rejection before decode when retained resources leave insufficient space.
- Real manifest digest: `ba6b2f478a0d8a7e3b81393782241dd436943cb3bfb104965fe5e68c1299c6aa`.

No renderer, world-view, camera, shared client, UI or csproj implementation changes. No game bytes, screenshots, upload or persistent cache were added. Construction remains untouched. GPU face orientation/seams, Chrome folder permissions, shader output and user visual acceptance are pending separate integration.

## Integrator verification

Import and sparse-face fix `b706c97` were independently reviewed and merged at `ec743db`, alongside the completed UI visibility corrections. Full real-content Node suite: **136 PASS, 0 FAIL, 0 SKIP**. AssetChecks: **9 PASS, 0 FAIL, 1 symlink SKIP**; junction tests passed. M3ServerChecks: **164 PASS**, including the real 49-file manifest, protocol/privacy/readiness/movement/reconnect and clean shutdown. Release build: 0 warnings, 0 errors. The UI label is 49; the server check derives its expected count from ProfilePaths instead of duplicating a literal. Physical TCP slow-reader, symlink permissions, real-model shader/browser fidelity and manual acceptance remain pending.
