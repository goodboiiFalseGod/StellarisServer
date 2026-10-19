# M3 Import Worker Validation

12 October 2026. Implemented the metadata validator, cancellable owner and worker pipeline in `tools/asset-probe/m3-{manifest,importer,import-worker,worker-pipeline}.mjs`. This is a library for integration, not a finished game client.

- Independent review by the root agent: four modules and three test files reviewed; no significant defects found.
- Full Node run with `STELLARIS_GAME_ROOT`: 74/74 PASS, 0 SKIP, including existing readers/profile/app/memory coverage and 10 M2 client regressions.
- Tested real 4.5.2 content from 44 files: metadata/SHA verification, full decode and repeated snapshot validation; four model layer counts `[1,3,5,2]`.
- Tested cancellation/replacement/dispose, late messages, worker errors and progress-callback exceptions; unique transfer buffers and shared typed-array views are preserved.
- DirectoryHandle mocks cover fixed paths, repeated reads, file changes and access denial. Mocks do not confirm browser structured cloning or real OS permissions.

Later integration: a real module Worker using 44 Files was run in the embedded browser; server `assetReady` and GPU publication order were checked by network/DOM tests. DirectoryHandle cloning/grant/revoke in Chrome and the full gameplay GPU/FPS scenario remain pending. The current combined result is in [M3-INTEGRATION-VALIDATION](M3-INTEGRATION-VALIDATION.md); this report preserves the library-stage results.
