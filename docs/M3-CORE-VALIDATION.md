# M3 core and metadata checks

October 12, 2026. These results do not mean the graphical multiplayer game is ready.

## Space and calendar

Root read the three original Session/M3 files and the checks. Independent review found automatic rerouting around a lost route when an alternate route was available. Fixed: validate the accepted route, stop at the approach/charging/transit boundary; run a new BFS after transit only for an explicit pending destination. Regressions were added for all three phases with an available detour.

`Stellaris.M3Checks`: 68 checks PASS after the fix, including immutability/rejection without mutation, calendar, fractional phases, commands, visibility, and pending state. The production JSON union and atomic day update with economy are for the next stage.

## Server manifest

Root read the builder/DTO and tests. The difference in visual ID ordering was fixed; it is now ordinal, as in JS. `AssetChecks` on real 4.5.2: 9 suites PASS, 1 SKIP, 0 errors. PASS includes the actual JS validator for server JSON via stdin and matching canonical digest, fixed44 parity, and synthetic immutability/privacy/limits/cancellation. Game bytes are not passed to JS parity.

The deterministic internal seam between hash passes checks an unchanged control, substitution of one byte without changing length, and cancellation. On failure, the manifest is not published; all 45 files are available exclusively after the pass completes or is cancelled. The public API was not expanded; the production builder retains two hash passes and three version checks.

Windows junction as root, ancestor, and internal directory of a required path — PASS, including ordinary directories as a positive control and preservation of target sentinels after unlink. The helper uses PowerShell 7 (`pwsh.exe`) and synthetic temp folders only; it removes the link without `-Recurse`, then performs a no-follow check before cleaning the fixture. In the sandbox, junction creation returns Access denied (8 PASS/2 SKIP); the authorized run outside the sandbox was repeated by the main agent — 9 PASS/1 SKIP. The only remaining SKIP: Windows did not allow a file/directory symlink; a junction does not replace that check. Independent review found no material issues; Release build — 0 warnings/errors.

## Regressions

M2 SessionChecks after explicit Release build: 7/7 suites PASS; build 0 warnings/errors. Direct `dotnet run` previously ended with a build failed message without diagnostics; separate build/run --no-build succeeded, and the cause of the tooling failure is unknown. M1 Checks with real content: 7/7 PASS. Full Node run: see M3-WORKER-VALIDATION.
