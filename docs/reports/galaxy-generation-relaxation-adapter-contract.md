# GALAXY-RELAXATION-ADAPTER-001 attempt1

October 13, 2026. Contract written **before code**, after receiving independent vectors.
Scope: only the new Core/Galaxy adapter, its checks, and this report; no Triangle,
system materialization, Session/server/client, packages/installation/remote/commits.

## Frozen source checkpoint

Source: partner `01a1359a-86d4-78f3-a4d2-5dda015220f6`, addendum §16.5.
Full analytical snapshot verified and saved before coding:
[galaxy-generation-relaxation-adapter-source-0c16f201abb1.md](galaxy-generation-relaxation-adapter-source-0c16f201abb1.md),
SHA256 `0C16F201ABB132D10B29E4744F5FE7736D9351B7801D0F84A65AF5BC202BC38D`.
This is a byte-for-byte copy of the research Markdown, with no changes or deletions. The original
outputs file was not overwritten; the copy contains no game binary bytes/dumps/assets or raw disassembly.
The snapshot may contain information about other stages, but the implementation uses only §16.5.
As the external addendum grows, this file/hash remains unchanged.
Historical review artifacts/evidence snapshots are not replaced.

## API and supported boundary

`PrepareInput(ordinaryStored,coreRadius,settings)`:
ordinary stored points in accepted order, raw fixed scale100000, component bounds ±50000000;
explicit coreRadius>0, dummyCount>=0, iterations>=0; no defaults.
Output: immutable ordinary sites, then dummy sites; ordinary/dummy counts and iterations metadata.
Iterations are not run by the adapter. Capacity/intermediates/q-int32 are checked;
invalid count, custom zero/negative core, out-of-bbox/nonrepresentable values are rejected.

Ordinary component: FM(raw,10000000), then signed integer `/100000` trunc0,
checked int32 q, then float32(q). Not direct float(raw/1000).
DummyCount0 skips division/loop. Otherwise step=62831800000/(count×100000), angle=i×step;
sin/cos use existing recovered fixed helpers; component FM is applied sequentially with coreRadius,
90000,10000000; signed integer `/100000`, int32→float32. Store point=(qSin,qCos).
All resulting sites must be finite/in inclusive bbox±50000 in the supported vanilla domain.

`RestoreOrdinaryCoordinates(prepared,suppliedSites)`:
count equals prepared total; finite/inclusive-bbox output is checked **in full**, including dummy,
but only the ordinary prefix is returned in original order. NaN/Infinity/out-of-bbox
are not clamped or converted through the indefinite cvtt path. For ordinary component:
cvttss2si trunc0 float→int32 t, sign-extend, t×100000, FixedDiv(...,10000000).
Result t×1000; **no post-relax clamp**. Output collection is immutable.
Adapters do not change inputs, RNG/state, or build tessellation/objects/IDs/spatial caches.

## Independent vectors and acceptance

Ordinary raw→float bits:
999/−999→00000000;1000→3F800000;−1000→BF800000;
2500000→451C4000;−2500000→C51C4000;50000000→47435000.
Dummy count256/core2500000, step2454:

| i | point x/y | float bits x/y |
|---|---|---|
| 0 | 0 / 2260 | 00000000 / 450D4000 |
| 1 | 55 / 2258 | 425C0000 / 450D2000 |
| 255 | −57 / 2258 | C2640000 / 450D2000 |

Output ±1.999→±1000;±0.999→0;±50000→±50000000.
Checks: these independent vectors; order/count/immutability; count0/iterations metadata;
negative truncation; finite bbox/rejections/overflow; dummy exclusion; composition with existing
supplied pass; no RNG change; previous 28 checks + Core Release 0/0.
After final checks, freeze actual code/hash, then obtain independent review from the existing reviewer.
Adapters and supplied geometry are not full-native Triangle or a complete galaxy.
