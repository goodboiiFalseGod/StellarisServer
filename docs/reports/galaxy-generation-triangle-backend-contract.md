# GALAXY-TRIANGLE-BACKEND-001 attempt1

October 13, 2026. Pre-code contract. Isolated feature/galaxy-generation,
without planet prototype, Session/server/client, packages, installation writes, or integration.

## Confirmed basis

Clarification before final freeze: partner independently accepted new checkpoint §19,
preserved byte-exact as `galaxy-generation-triangle-backend-source-104ac0350392.md`,
SHA256 104AC03503928196956AA769267B84E113A428E037340C4D3E2AD3EBE4466A61.
It replaces the earlier claim of unmodified embedding: this is a Win64/thread-safe
behavioral port of the in-game behavior. Confirmed: pointer-width tags/alignment and local uint32 seed1,
strict-zero tie rules, full primal/Voronoi array order, ABI/ownership, and source-side
full-array vectors. Literal compiler flags/runtime MXCSR/full fork diff and embedded
runtime captures were not recovered. Result is labeled **native-contract**,
not **native-bit-certified**.

Frozen analytical source `galaxy-generation-system-materialization-source-7c4c181256fd.md`,
SHA256 7C4C181256FD94B50866BB84F985F2DDD258BE846313DBC7A424A7DB6ED359FE,
§16.5/§19: Triangle1.6, TRILIBRARY+SINGLE+NO_TIMER, robust predicates,
default divide-and-conquer/Dwyer, private seed1 per call. Initial switches
`pczAevnQ`, after pass `pczAevn`. No input canonicalization/sorting/deduplication.
Primal/Voronoi edge arrays correspond index-to-index; direction is preserved.
A follow-up reverse-engineering request was sent before implementation: FP mode, independent Voronoi
bits, degeneracies, and upstream differences. Unconfirmed details are not called exact.

Official source: https://www.cs.cmu.edu/~quake/triangle.html →
https://www.netlib.org/voronoi/triangle.zip.
Archive SHA256 1766327ADD038495FA3499E9B7CC642179229750F7201B94F8E1B7BEE76F8480;
triangle.c F8164D23C7F4CECB58CA84784558E5BADEFD39CEF9A9AC142C752C482A238065;
triangle.h 4E945DCC26CD892BFBB0C71B2CBADC5882625291AF7C798F645E5E86DAD7C594.
Upstream is retained unmodified only in ignored artifacts; it is not vendored/packaged.
Build generates a separate compiled copy with explicit LLP64 adaptation: 61 casts
`(unsigned long)`→`(uintptr_t)`, 7 `alignptr` declarations, 19 diagnostic `%lx`→`%llx`,
stdint include, and clear modification notice. These casts concern addresses/tags/masks/
alignment/diagnostics. randomseed/randomnation, predicates/REAL/geometry are untouched.
Source copyright is preserved in the generated copy.
Copyright/license are preserved. Private use is permitted upstream; distribution/
commercial embedding are outside this task. Build script accepts an explicit source path,
checks pinned hashes, uses existing MSVC x64, and does not download/install.

## Execution and API

Own worker C calls Triangle with the specified pointer port. A separate process per tessellation:
native exit/crash/OOM does not terminate the host; private globals are not shared across calls.
Windows x64, IEEE binary32 round-to-nearest, FTZ/DAZ off, /fp:strict without FMA.
struct triangulateio 0xA8 is checked at compile time. No segments/holes/regions/attributes,
input points remain in supplied order. Input is immutable; no game RNG.

Managed caller receives explicit absolute worker path + expected SHA256 and scratch
directory. No hidden DLL search, fallback, or automatic build. Bounded
worker timeout returns an error; invalid/missing/hash-mismatch worker is not launched.
Worker binary files contain only supplied/generated coordinates/topology.
Preserve ordered primal edges, triangles/neighbors, Voronoi vertices/edges/normals.
Output is immutable; size/count/indices/finite are checked before publication. Domain
`3<=N<=2047` is the guaranteed single-4092-triangle-pool bound. N<3, N>2047,
nonfinite/out-of-bbox and one unique site are rejected before native invocation; this is a safe domain,
not invented native semantics. Two-or-more unique collinear sites yield zero topology;
normal2D centroid is not run (no incident segments), and sites remain in place.
Duplicates are not removed by the adapter; Triangle preserves original output numbering.
Exact cocircular zero/one-ulp perturbations receive no epsilon/jitter.

Relaxation pipeline: initial tessellation even for iterations0; each pass uses
existing SuppliedTessellationPass and then tessellates again without Q. After the last
pass, return final topology and sites; restore ordinary prefix through existing adapter.
Do not modify system objects or spatial caches. Native RNG is not consumed.

## Acceptance

Frozen source edge vectors for triangle/square/reverse-square/duplicate, independent
Voronoi float bits from reverse engineering, cocircular/collinear/error domain, repeat determinism,
unchanged inputs, concurrency/process isolation, initial+rebuild iteration order,
genuine 200 ordinary + 256 dummy composition. Old 39 checks, Core Release 0/0.
New native checks must run explicitly with the built worker, without silent skip.
Independent actual code/source/diff review before completion. Full native game-state
equivalence is not claimed without game observations.
