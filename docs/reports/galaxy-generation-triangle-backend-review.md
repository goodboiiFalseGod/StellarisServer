# Independent review of Triangle backend

Report ID: `GALAXY-TRIANGLE-BACKEND-001-independent-review-attempt1`.
Reviewer: `/root/galaxy_review`, agent separate from the implementation author.
Date: October 13, 2026. Branch `feature/galaxy-generation`, base HEAD
`5a67a51a2e28609cfdcf861a1b25fc0bd840b573`.

Result: **accepted as a bounded native-contract; no actionable findings**.
Acceptance is not native-bit certification. Checked actual implementation,
contract/handoff, frozen source §19, and final test delta after freeze. Previously
read full generated pointer-port diff against official upstream C;
final generated hash matches the reviewed port. Program delta: 24 added lines,
0 removed against the saved baseline. Previous acceptance of source modules is not replaced.

## Reviewed SHA-256

Actual file hashes were computed read-only by the reviewer; source §19 was read from the same bytes
whose SHA was checked. Paths are relative to the assigned checkout.

| File | SHA-256 |
|---|---|
| src/Stellaris.Core/Galaxy/NativeTriangleBackend.cs | E8392502173FC2ABC376BFAA3E27918F1F52ACC738E2C31BE3ED2CD0E4285D20 |
| src/Stellaris.Core/Galaxy/NativeGalaxyRelaxation.cs | 7E287A54CA8C56BC332C439352849F035F0E6FED975DA0EAC8A6407060A057D4 |
| tools/galaxy-triangle/Worker.c | A08A37FD7A642A8985F7D3A874E2B9F64B1E4D86D8A08935696DA602CFC274C1 |
| tools/galaxy-triangle/Build-TriangleBackend.ps1 | 1ABB28A9012B93DE37625C388A9C7FCBA1486A2C444C9BC71B3ED6980B7F35E0 |
| tests/Stellaris.GalaxyGenerationChecks/TriangleBackendChecks.cs | 6045AA67E84A0054491EF830C66B85BA7F9E8929EDC62454CC36BD4B776B818A |
| tests/Stellaris.GalaxyGenerationChecks/TriangleFailureWorker.c | 40602C055271B0C831F4F2491C535319AC92607032BB9102D9B63160D9076BAF |
| tests/Stellaris.GalaxyGenerationChecks/Program.cs | E8370B328BA6993EBC91B6803EFC76E3AF568AB8FB34DBA34C28CE4108F98E33 |
| artifacts/triangle-backend/baseline/Program.cs (previous reviewed baseline) | EE206C0B1B12D219D69A063D523BDBB9B4175A746F3B12A471385BDB581C0AF2 |
| artifacts/triangle-backend/build/triangle-windows-x64.c | 19981A045D84892E51A1210D33822D2CB784FC33902C682012141FBF5D622251 |
| artifacts/triangle-backend/build/stellaris-triangle-worker.exe | CA2D48F5CDB782AE7E01518F72298F3030996B88912E38D3A0D5646E71585F71 |
| docs/reports/galaxy-generation-triangle-backend-contract.md | 8803A659D818D74D034CA5C47B5401A4CC91B84563E0D0AB080E54D539B0F76E |
| docs/reports/galaxy-generation-triangle-backend-source-104ac0350392.md | 104AC03503928196956AA769267B84E113A428E037340C4D3E2AD3EBE4466A61 |
| docs/reports/galaxy-generation-triangle-backend-handoff.md | 45FF6DAD46544F9926E7327774F19122F9F85848FFA326E2A3F6FC36A9830245 |

Baseline content was previously accepted in materialization review; actual Program diff in this
task was checked directly. Upstream C/H pinned hashes in build script match
frozen source: C `F8164D23C7F4CECB58CA84784558E5BADEFD39CEF9A9AC142C752C482A238065`,
H `4E945DCC26CD892BFBB0C71B2CBADC5882625291AF7C798F645E5E86DAD7C594`.

## Properties checked

Generated LLP64 port is limited to 61 pointer/mask/alignment/diagnostic casts,
7 alignptr types and 19 diagnostic formats, include/notice. LCG unsigned-long32,
REAL/predicates and geometry control flow are unchanged. Official source is preserved;
generated copy explicitly identifies the port. Per-call process isolates upstream global seed
and pools, equivalent to the required local private-seed boundary for the supported path.

Worker uses literal initial/rebuild switches, SINGLE ABI 0xA8, strict FP build,
round-nearest/noFTZ/DAZ and hidden process/error-dialog suppression. Protocol layout
is consistent: length32+8N+32T+24E; counts/indices/finite/FP/ABI/inputbits are checked
before publishing immutable topology. Adapter does no input sorting/canonicalization/dedup.
Supported domain3..2047 and one-unique rejection are explicit; collinear zero topology is allowed.

Processes/files are separated by a unique scratch child per request; stdout/stderr are drained
in parallel. Timeout kill/exit/drain are bounded; cleanup errors on failure do not hide
the original backend error. Worker path/hash are explicit, with no fallback/autobuild/game RNG.
Initial review findings about unbounded post-timeout waits were fixed before final freeze.

Relaxation performs initial tessellation even at 0 iterations, simultaneous supplied
centroid pass, rebuild after each pass, final topology for final sites, and ordinary
restore through the previously accepted adapter. No world/system/spatial/prototype writes.

Final tests include frozen source-side full primal/neighbor/dual/Voronoi arrays,
sensitive floatbits, duplicate/reverse-square/collinear/cocircular±1ulp, repeat/phase/
parallel isolation, immutability/domain/provenance failures. Negative fixture distinguishes
exit42, access violation−1073741819, timeout, and corrupt counts; checks cleanup.
Zero/multiple passes and genuine 456-site composition check final sites/topology/counts/RNG.

## Validation and certification limits

Author-run handoff: Core Release **0 warnings/errors**, **49 PASS** (old39 +9 backend
groups + genuine composition). Reviewer checked actual checks and expected evidence,
but did not rerun runtime build/tests. This is not reviewer-run validation.

Independent partner vectors are source-side conformance evidence, not direct
capture of the embedded routine. Literal native compiler flags/MXCSR, full Win64 fork diff,
embedded runtime comparison, multi-block allocator order, OOM/fatal equivalence, and full
galaxy seed identity are unconfirmed. FTZ/DAZ policy is a conformance choice, not measured
native runtime state. Adversarial filesystem/process-launch TOCTOU was not investigated.
Build/worker applies to an explicit Windows x64 dependency; upstream distribution/commercial
embedding is outside acceptance. Reviewer did not touch native installation/game/UI.

Bounded native-contract accepted; **native-bit-certified/full-native-game-state** is not claimed.
Review does not authorize stable integration/push. Reviewer's only write in the final stage is
this Markdown; implementation/tests/install were unchanged, and no new agents were created.
