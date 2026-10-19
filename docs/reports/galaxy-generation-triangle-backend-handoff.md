# GALAXY-TRIANGLE-BACKEND-001 attempt1 — handoff

October 13, 2026. **Stage implemented and accepted by independent review as a bounded native contract.**
Code/check/source hashes frozen; only this status/handoff changed after review.
The user's latest direct request explicitly includes the Triangle backend; the previous
adapters-only scope applies to the completed preceding stage.
feature/galaxy-generation, HEAD5a67a51a2e28609cfdcf861a1b25fc0bd840b573.
No commits/push/install/game launch/native-memory access/shared integration.

## Contract and alignment

[Contract](galaxy-generation-triangle-backend-contract.md),
SHA256 8803A659D818D74D034CA5C47B5401A4CC91B84563E0D0AB080E54D539B0F76E.
Reverse partner 01a1359a-86d4-78f3-a4d2-5dda015220f6 confirmed the bounded
Win64 behavioral port, pointer-width patch, per-call seed1, binary32 SSE/noFMA,
one-block domain3..2047, edge duality/ownership/degeneracies. Their new independently
accepted report snapshot is preserved byte-exact:
[source104AC035](galaxy-generation-triangle-backend-source-104ac0350392.md),
SHA104AC03503928196956AA769267B84E113A428E037340C4D3E2AD3EBE4466A61,
§19. Old 7C4C source and all earlier reports were preserved unchanged.

## Result

Own worker calls Triangle1.6/SINGLE/TRILIBRARY/NO_TIMER, literal switches
initial pczAevnQ / rebuild pczAevn, strictFP/RN/noFTZ/DAZ. Each call uses a fresh process:
private seed reset, pools and arrays are freed by the OS; outputs are copied to an immutable
managed snapshot. Caller supplies explicit absolute executable/hash/scratch/timeout;
there is no auto-discovery/build/fallback. Native fatal/nonzero/timeout and invalid output do not
publish topology. Process is hidden; fatal error dialogs are disabled inside the worker.
Counts/length/indices/finite/ABI/FP bits/unchanged input pointbits are checked.
Collinear input with >=2 unique sites returns zero topology; one unique site is safely rejected.

NativeGalaxyRelaxation performs initial tessellation even with zero iterations,
the supplied centroid pass simultaneously, and rebuild after each pass; final topology
belongs to final sites, ordinary prefix is restored through the existing adapter.
This does not write to systems/spatial caches and is not startup/runtime integration.

## External dependencies and Windows port

Only download: official https://www.netlib.org/voronoi/triangle.zip,
publisher page https://www.cs.cmu.edu/~quake/triangle.html.
Archive1766327ADD038495FA3499E9B7CC642179229750F7201B94F8E1B7BEE76F8480;
C F8164D23C7F4CECB58CA84784558E5BADEFD39CEF9A9AC142C752C482A238065;
H 4E945DCC26CD892BFBB0C71B2CBADC5882625291AF7C798F645E5E86DAD7C594.
Downloads/source/generated copies/objects/executables/build manifest are only in
ignored artifacts/triangle-backend; no vendor/nuget/package/install changes.
Triangle is copyrighted by Shewchuk; private/research use is permitted upstream. Commercial
embedding/distribution requires a separate arrangement; this task does not distribute it.
Full original copyright is preserved in official and generated C.

Untouched upstream crashes with C0000005 on Win64 LLP64. Build script creates a separate
adaptation with notice: 61 pointer casts, 7 alignment types, 19 diagnostic formats; LCG32,
REAL/predicates/geometry are unchanged. Generated C SHA:
19981A045D84892E51A1210D33822D2CB784FC33902C682012141FBF5D622251.
Compiler: existing MSVC14.44.35207 Hostx64/x64. Flags:
/TC /O2 /fp:strict /std:c11 /w /DTRILIBRARY /DSINGLE /DNO_TIMER
/DANSI_DECLARATORS /D_CRT_SECURE_NO_WARNINGS /link /Brepro /INCREMENTAL:NO.
Worker SHA CA2D48F5CDB782AE7E01518F72298F3030996B88912E38D3A0D5646E71585F71.
Failure fixture is custom C, /W4 /WX; it contains no Triangle/game code.

## Frozen code/check SHA256

| Path | SHA256 |
|---|---|
| src/Stellaris.Core/Galaxy/NativeTriangleBackend.cs | E8392502173FC2ABC376BFAA3E27918F1F52ACC738E2C31BE3ED2CD0E4285D20 |
| src/Stellaris.Core/Galaxy/NativeGalaxyRelaxation.cs | 7E287A54CA8C56BC332C439352849F035F0E6FED975DA0EAC8A6407060A057D4 |
| tools/galaxy-triangle/Worker.c | A08A37FD7A642A8985F7D3A874E2B9F64B1E4D86D8A08935696DA602CFC274C1 |
| tools/galaxy-triangle/Build-TriangleBackend.ps1 | 1ABB28A9012B93DE37625C388A9C7FCBA1486A2C444C9BC71B3ED6980B7F35E0 |
| tests/Stellaris.GalaxyGenerationChecks/TriangleBackendChecks.cs | 6045AA67E84A0054491EF830C66B85BA7F9E8929EDC62454CC36BD4B776B818A |
| tests/Stellaris.GalaxyGenerationChecks/TriangleFailureWorker.c | 40602C055271B0C831F4F2491C535319AC92607032BB9102D9B63160D9076BAF |
| tests/Stellaris.GalaxyGenerationChecks/Program.cs | E8370B328BA6993EBC91B6803EFC76E3AF568AB8FB34DBA34C28CE4108F98E33 |

Previous Program snapshot artifacts/triangle-backend/baseline/Program.cs,
SHAEE206C0B1B12D219D69A063D523BDBB9B4175A746F3B12A471385BDB581C0AF2.
Actual Program delta: 24 lines added/0 removed; previous 39 checks preserved.
17previous Content/Galaxy+Core/Galaxy sourcefiles SHA checked unchanged.
Tracked gitdiff empty (galaxy work historically untracked); no broad add/commit.

## Checks

Author-run:

```powershell
pwsh -NoProfile -File tools/galaxy-triangle/Build-TriangleBackend.ps1 -SourceDirectory artifacts/triangle-backend/upstream -OutputDirectory artifacts/triangle-backend/build -WithTestFixtures
dotnet build src/Stellaris.Core/Stellaris.Core.csproj -c Release --no-restore
dotnet run --project tests/Stellaris.GalaxyGenerationChecks/Stellaris.GalaxyGenerationChecks.csproj -c Release --no-restore -- GAME_ROOT --triangle-worker ABSOLUTE_WORKER CA2D48F5CDB782AE7E01518F72298F3030996B88912E38D3A0D5646E71585F71
```

Core0warnings/0errors, **49PASS**: old39+9backend groups+genuine composition.
Frozen source full arrays triangle/square/5point: triangles/neighbors/edge direction,
paired dual endpoints/normals, sensitive Vorbits409FFFFF/C0200001/41036DB7.
Duplicate survivor3; reverse-square; collinear zero; cocircular ±1ulp strict-sign
diagonals; repeat/initialvsrebuild/4parallel requests; input/output immutability.
Own failure fixture verifies exact nonzero42 after draining both pipes, real
access-violation exit−1073741819, timeout250ms with kill and bounded waits,
corrupt counts/protocol rejection. Scratch cleaned after each failure.
Zero/multiple passes verify callcount/finalsites/finaltopology; genuine tinyellipse
seed1:200ordinary+256dummy→456sites/200restoredcoordinates/2tessellations, no gameRNG.
Vanilla defines read-only verified00_defines.txt780–781:1iteration/256dummy.

## Acceptance boundary

**native-contract**, not native-bit-certified. Independent numeric vectors are
source-side, not runtime captures from the game. Native compiler flags/MXCSR/full fork diff,
multi-block allocator behavior, fatal/OOM semantics, and bit identity of the embedded routine
are not certified. Worker supports Windows x64 and an explicit built dependency.
Planet prototype, world/session/server/client/M3/shared protocols,
original installation, other worktrees, and previous source/fixtures/evidence were unchanged.
Final independent implementation review was completed by existing /root/galaxy_review:
**accepted, no actionable findings**. Actual 7 code/check files, contract, source,
generated pointer port, and Program 24+/0− were checked. Reviewer did not rerun tests;
49PASS/Core0/0 — author-run evidence. Durable reviewer-authored
[review](galaxy-generation-triangle-backend-review.md),
ID GALAXY-TRIANGLE-BACKEND-001-independent-review-attempt1,
SHA256 BB39F125C7F84C97C516DA7B9EB02EF8FE0E9E9DD5139EE20495343E551150D2.
Reviewed handoff before the documentation-only acceptance status update had SHA256
45FF6DAD46544F9926E7327774F19122F9F85848FFA326E2A3F6FC36A9830245.
Code/check/contract/source were unchanged after review.
