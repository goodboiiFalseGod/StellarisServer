# `group[g]+0x98`: producer contract before `0x140885580`

Build: `stellaris.exe` SHA-256 `400DF27C82DDC845AA9DCE79BD468D81F18F060D299CD263E3AFA93AEF4F7A83`. The installation was read only. This note is bounded to the `0xA8`-byte group/partition record used by `0x140982320` and consumed by hyperlane pruning at `0x140885580`.

## Result

`group+0x98` is an integer per-group cross-group-edge target. It is not a constant and is not computed by the pruning routine. It is sampled when the group is created, then may only increase when a later consolidation merges another group into it.

The resolved setup-scenario object passed as argument 1 to `0x140982320` contains two 20-byte partition specifications:

- first spec at `scenario+0x40`, method byte at `scenario+0x50`;
- second spec at `scenario+0x54`, method byte at `scenario+0x64`.

For each spec define the four signed `int32` values, without assigning guessed names:

```
u = cfg[+0x00]
l = cfg[+0x04]
s = cfg[+0x08]
b = cfg[+0x0C]
method = uint8 cfg[+0x10]   // 0 breadth-first, 1 depth-first, other: no call
```

The script-facing semantic names of these four already-resolved slots remain opaque in this bounded proof. Vanilla files spell `max_systems`, `min_systems`, `max_bridges`, `min_bridges`; the runtime slot-to-name transpose was not proven here and must not be guessed. Supplying the resolved four integers is sufficient and exact. Every shipped vanilla setup scenario inspected supplies all four names for both specs, so no missing-key default participates in vanilla generation.

The scale `h` is signed fixed-point, scale 100000, read by both producers from `globalGalaxySettings(+0x9B0)->+0x90`. This is the resolved lobby hyperlane-density value used by this stage.

## Initialization and all semantic writes

The record constructor `0x140A37130` initializes:

```
+0x90 = group index/id argument
+0x94 = -1
+0x98 = -1
+0x9C = 0
+0xA0 = -1
```

The `qword` store at `0x140A371E2` writes `0xFFFFFFFFFFFFFFFF`, hence both `+0x94` and `+0x98` start at signed `-1`.

Creation writes are:

- breadth-first producer `0x140A384C0`, store at `0x140A3871D`;
- depth-first/fair-sweep producer `0x140A38790`, store at `0x140A389AE`.

Both execute the identical sampling contract below. Record relocation/copy (`0x1409910B4..0x1409910BA`, reached through vector growth) copies the dword unchanged and consumes no RNG.

After both specs have been applied, `0x140982320` unconditionally calls consolidation `0x140A37200` at `0x14098283A`. Its merge primitive `0x140A36E70` performs:

```
destination.target98 = max(destination.target98, source.target98)
source.memberCount = 0
source.field94 = 0
```

at `0x140A37011..0x140A37042`; it consumes no RNG. Empty/source records therefore remain in the vector but are inactive. No later semantic producer of this field was found on the traced path before the pruning consumer.

## Exact sampling

All integer arithmetic in the four config fields is signed `int32` where shown; the product is signed `int64`.

```
width94 = int32(u - l)
p = int64(s) * int64(h)
scaled = trunc0((p + (p >= 0 ? 50000 : -50000)) / 100000)
span98 = int32(scaled - b)

if width94 > 0:
    field94 = l + NextLocal31() % width94
else:
    field94 = l

if span98 > 0:
    target98 = b + NextLocal31() % (span98 + 1)
else:
    target98 = b
```

`trunc0` is signed division toward zero. The bias makes the fixed conversion round halves away from zero. The positive branch makes `target98` uniform over the inclusive integer interval `[b, scaled]`; when `scaled <= b`, it is exactly `b`.

The `+0x94` draw is always tested and, if enabled, consumed first. The `+0x98` draw is tested second. A disabled branch consumes no draw. Sampling happens once for every seed system that is still unassigned when scanned; claimed systems are skipped and consume neither creation draw.

Both producer variants receive the same mutable local RNG pointer as argument 4 from `0x140982320`. They do not reseed it between the first and second scenario specs. Its exact draw is:

```
c = uint32(rng[0])
k = uint32(rng[4])
x = uint32(k - uint32(c * 0x4AD685B3))
x ^= x >> 8
x += 0x68E31DA4
x ^= x << 8
x *= 0x1B56C4E9
x ^= x >> 8
x *= 0x92D68CA2
x ^= x >> 8
rng[0] = uint32(c + 1)
return x & 0x7FFFFFFF
```

Every operation on `x` wraps modulo `2^32`. Divisors in the taken branches are positive, so native signed `idiv` gives the same remainder as unsigned modulo.

## Producer order and ownership

`0x140982320` calls the first spec (`scenario+0x40`) first, then the second (`scenario+0x54`), threading one RNG state and one output group vector through both. Method 0 dispatches to `0x140A384C0`; method 1 to `0x140A38790`; other method bytes skip that spec completely and consume no creation draws.

The breadth-first variant samples immediately before creating each group and may claim further systems before the scan resumes. The depth-first variant samples all seed groups during its initial scan; its later fair growth sweeps do not resample `+0x98`. Consolidation follows both producers and preserves the maximum target across every merge chain.

The pruning stage must therefore receive the post-consolidation value:

```
finalTarget98[root] = max(sampledTarget98[g] for every g consolidated into root)
```

with inactive merged records excluded by their zero member count/zero `+0x94` state.

## Control vectors

These vectors use synthetic resolved slots; they test the producer independently of the unresolved script-name transpose.

### V1: both draws enabled

```
cfg = [15, 8, 4, 2]
h = 100000
initial RNG = { counter=0, key=0 }
width94=7, scaled=4, span98=2
D0=0x6FBE2FBA=1874735034
D1=0x7B2719EF=2066160111
field94 = 8 + D0%7 = 12
target98 = 2 + D1%3 = 2
final counter = 2
```

### V2: first branch disabled; target uses the first draw

```
cfg = [8, 8, 4, 2]
h = 100000
initial RNG = { counter=0, key=0 }
width94=0, scaled=4, span98=2
field94=8                         // no draw
target98=2 + 0x6FBE2FBA%3 = 2
final counter=1
```

### V3: target branch disabled

```
cfg = [15, 8, 4, 5]
h = 100000
initial RNG = { counter=0, key=0 }
width94=7, scaled=4, span98=-1
field94=12                        // consumes D0
target98=5                        // no second draw
final counter=1
```

### V4: fixed-point half and clamp

```
cfg = [8, 8, 4, 2]
h = 12500
p=50000, scaled=1 (half rounds away from zero), span98=-1
target98=2; no draws at all; counter unchanged
```

### V5: merge

Sampled active targets `[2, 4, 3]`; if consolidation merges group 1 into group 0 and then group 2 into group 0, final group 0 target is `max(max(2,4),3)=4`. Both merges consume zero draws.

## Acceptance checks

An implementation passes this bounded producer contract when:

1. a fresh record exposes `+0x98 == -1` before creation assignment;
2. it matches V1-V4 including final RNG counters;
3. the second scenario spec starts from the exact RNG state left by the first;
4. skipped methods and skipped already-assigned systems consume no creation draws;
5. record relocation leaves the field unchanged;
6. consolidation uses signed `max`, is order-associative for the final target, and consumes no RNG;
7. pruning reads the post-consolidation dword at `group+0x98`.

## Evidence map and remaining bounded uncertainty

- `work/partition_bfs.txt`: config loads/rounding `0x140A3852B..0x140A38591`, draw order `0x140A385D5..0x140A386A8`, store `0x140A3871D`.
- `work/partition_dfs.txt`: config loads/rounding `0x140A387C5..0x140A3882D`, draw order `0x140A38873..0x140A38953`, store `0x140A389AE`.
- `work/partition_walk.txt`: merge/max `0x140A36E70..0x140A37042`, constructor `0x140A37130..0x140A371FC`.
- `work/hyper_982320.txt`: scenario offsets, method dispatch/order, shared RNG, unconditional consolidation call.
- `work/topology_records.txt`: relocation copy of `+0x98`.

The sole unresolved item in this narrow contract is the script-key-to-resolved-slot transpose/default behavior for malformed or modded setup scenarios. It does not block vanilla completeness because all shipped vanilla scenarios provide the four values and a method for both records. A loader should preserve its resolved integer layout explicitly instead of inferring slot names from file presentation order.
