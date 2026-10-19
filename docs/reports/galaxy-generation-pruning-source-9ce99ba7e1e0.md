# Durable handoff: PruneAndMaterializeHyperlanes

## Scope

This contract starts with two M13 vectors and ends with the generated bidirectional system adjacency records and associated group lists. It combines main report §§11/14 with a repeated check of `0x140885000`, `0x140885580`, `0x140884C30`, `0x140887090`, `0x1408871A0`, `0x140887350`, and caller `0x140886020`.

Goal: a vanilla-compatible result given the explicit results of two global RNG calls. Bit-for-bit certification of the full global RNG state, allocator addresses, and error/OOM paths is not required.

## 1. Exact input

```text
V: int32 number of systems, vanilla V >= 3
systems[V]: ordered full system handles; point index -> handle/object
A: vector<Edge24> same-group M13 candidates
B: vector<Edge24> cross-group M13 candidates

Edge24:
  +0x00 int32 a
  +0x04 int32 b
  +0x08 int64 weight          // fixed scale 100000
  +0x10 byte  flag            // M13 initializes 0

systemSlotToGroup[slot]: int32 group index, slot=handle&0xFFFFFF
group[g]: stride 0xA8
  +0x98 int32 targetDegree
  +0x9C int32 currentCandidateDegree
  plus native bookkeeping vectors at +0x30,+0x48,+0x60,+0x78

h: int64 hyperlane multiplier fixed scale 100000
scenarioMin: int64 fixed
scenarioSpan: int64 fixed
scenarioMaximum = scenarioMin + scenarioSpan
maxDistance: int64 fixed scale 100000
removePerc = 15000
degreeBase = 4
removedMaxHops = 20

gA: uint32 exact result of global NextRandom(tag=0x1BC)
gB: uint32 exact result of global NextRandom(tag=0x220)
```

Preconditions inherited from M13:

- endpoints index the same unchanged ordered `systems` array;
- A contains same-group records, B cross-group records;
- weights are nonnegative whole-map-unit fixed values, hence divisible by `100000`;
- all flags are zero in the normal M13 path;
- M13 already incremented `group[g].currentCandidateDegree` once per incident B edge;
- vectors are independent owned copies; Triangle memory is gone.

Supported vanilla domain requires `50000<=h<=300000`, `h>0`, finite counts and products, valid handles/group mapping, nonnegative representable `maxDistance`, simple undirected candidate edges and enough storage. Safe ports should validate these conditions before mutation.

## 2. Supplied RNG policy

### 2.1 Global ledger

For every `V>=3` topology build, the native order is:

1. pass A calls global `NextRandom` once with source tag `0x1BC`;
2. A survivors are materialized without RNG;
3. pass B calls global `NextRandom` once with source tag `0x220`;
4. B survivors are materialized without RNG.

Both calls occur even when the corresponding vector is empty and even when A requests zero random deletions. No other global or galaxy-local RNG draw occurs in pruning/materialization.

Two supported integration policies:

- **stateful:** call the real/global-compatible RNG exactly at those two positions and retain the returned low 32-bit values;
- **supplied-results:** caller supplies `gA,gB`; pruning uses them exactly as returned values and does not own or advance any outer RNG state.

Tags identify the two native call sites. The supplied-results adapter must not hash the tag into `gA/gB` unless its outer RNG implementation already does so.

### 2.2 Seed conversion and local generator

All arithmetic below is wrapping `uint32`. Define:

```text
Hash32(x):
    x ^= x >> 8
    x += 0x68E31DA4
    x ^= x << 8
    x *= 0x1B56C4E9
    x ^= x >> 8
    x *= 0x92D68CA2
    x ^= x >> 8
    return x

SeedFromGlobal(g):
    return Hash32(0x5EA6BA9F - g*0x4AD685B3)

LocalNext(counter):
    value = Hash32(0 - counter*0x4AD685B3) & 0x7FFFFFFF
    counter = counter + 1
    return value, counter
```

Each pass initializes an independent `counter=SeedFromGlobal(gX)`. Modulo selection uses `LocalNext(counter) % activeCount`; modulo bias is preserved.

Control ledger:

```text
g=00000000 -> seed=EDE419C6
local outputs: 4BAE8956,3DCEAEBB,2F07FD5D,3EDC55AB,1263E8B5,27B2CD32

g=00000001 -> seed=E5F10AFA
local outputs: 65AF1864,309818EA,583219B9,6AB4C15D,302E9C30,7507EF3D
```

For active pool `[0,1,2,3,4]`, `g=0` gives first two swap-with-last selections `[2,3]` and a full permutation `[2,3,1,4,0]`. `g=1` gives `[0,2]` and `[0,2,4,1,3]`.

## 3. Common sort and invalidation rules

Each pass first sorts its source vector in place by ascending lexicographic `(weight,a,b)`:

- `count<=32`: stable insertion sort, shift only on strict `>`;
- `count>32`: native large sort with the same full key;
- equal duplicate-key order in the large branch is not proven, but M13's normal simple edge set has no identical duplicate record.

Removal never erases or compacts source records. It copies the 24-byte record to a temporary rejected vector, then writes only `source[index].a=-1`. Physical count and all other fields remain. All later source scans skip `a<0` and, defensively, `b<0`.

Final materialization scans each already sorted physical vector forward and skips invalid records. Therefore final real-edge order is sorted A survivors, followed by sorted B survivors.

## 4. Pass A: same-group pruning (`0x140885000`)

Let `NA=A.count` before any invalidation.

### 4.1 Target and mandatory distance removal

Compute with signed fixed arithmetic/truncation toward zero:

```text
R = trunc_toward_zero(NA * 15000 / h)
```

For positive vanilla inputs this is floor. Examples: `h=50000 -> floor(0.30*NA)`, `h=100000 -> floor(0.15*NA)`, `h=300000 -> floor(0.05*NA)`.

Scan sorted A forward. Every valid record with `weight > maxDistance` is copied to `rejectedA`, invalidated in A, and increments `L`. Equality survives. Since A is weight-sorted, these records normally form a suffix, but the algorithm still performs the full scan.

### 4.2 Random selector

The global tag `0x1BC` call and local seed initialization happen unconditionally. Then call the selector with:

```text
start = L
requested = R - L
source = A
```

This `start=L` is observable: construct `pool` by scanning physical indices `L..NA-1` and append only records whose `a!=-1`. Thus the first `L` shortest physical records are not eligible for the random supplement. It is incorrect to build the pool from index zero.

Let `k=min(max(requested,0),pool.count)`. Repeat exactly `k` times:

```text
j = LocalNext() % pool.count
idx = pool[j]
pool[j] = pool[last]
pool.pop()
rejectedA.append(A[idx])
A[idx].a = -1
```

Exactly one local draw is consumed per selected record; a nonpositive request consumes none. The global draw was already consumed.

After the selector, sort the current `rejectedA` by `(weight,a,b)`. Later degree removals append after this sorted prefix and are not followed by another sort.

### 4.3 Scratch graph, threshold and degree removal

Build scratch adjacency from currently valid A records in forward physical order:

- degree array is `uint16 degree[V]`, initially zero;
- adjacency storage contains `V*(V-1)` 8-byte slots with row stride `V-1`;
- for each `(a,b,weight)`, append `(neighbor=b,cost=int32(weight/100000))` to row `a` and symmetric entry to row `b`, then increment both degrees.

Compute:

```text
T = 4                                      if h <= 100000
T = round_half_away_from_zero(4*h/100000)  if h > 100000
```

For positive vanilla `h>100000`, native implements `(4*h+50000)/100000`. If `h>=scenarioMaximum`, skip the degree helper entirely. Otherwise scan sorted A forward. For every valid `(a,b)`:

- if `degree[a] < T` or `degree[b] < T`, retain it;
- else append it to `rejectedA`, invalidate it, remove the symmetric adjacency entries by swap-with-last in each row, and decrement both scratch degrees.

Equality allows removal: both degrees must be `>=T`.

### 4.4 Defensive helper

Run the common defensive helper even when the degree helper was skipped. It scans valid A source records in reverse physical order. Full semantics are in section 6; on a normal simple scratch graph it moves zero records because each still-valid edge has its own one-hop reverse adjacency.

Let `DAdef` be the returned count. Native writes the first `rejectedA.count-DAdef` rejected records into the same-group rejected bookkeeping list at group record `+0x60`, two directed 24-byte entries per edge:

```text
{systemA*,systemB*,weight}
{systemB*,systemA*,weight}
```

The `DAdef` defensive tail is excluded. Temporary rejected storage is then freed. A itself remains sorted with invalid holes for caller materialization.

The native pass return value is `rejectedA.count-DAdef`; the caller does not use it to choose survivors.

## 5. Materialize surviving A

Caller scans A forward. For every valid record:

1. resolve point indices through ordered system handles and validate full handles;
2. insert symmetric logical adjacency records:

   ```text
   systemA += {neighborHandle=handleB, weight, flag=0}
   systemB += {neighborHandle=handleA, weight, flag=0}
   ```

   Native storage is the per-system vector at `system+0x588`. Distinct full system handles are a required M13 invariant; the insertion helper is skipped for a self handle.

3. perform native per-system dirty/cache notifications, including OR of bit 0 at `system+0x137D` after insertion;
4. append two directed group bookkeeping entries at same group `+0x78`:

   ```text
   {systemA*,systemB*,weight}
   {systemB*,systemA*,weight}
   ```

No RNG is used. Materialization order is the forward sorted-survivor order.

## 6. Defensive path helper (`0x140887350`)

Inputs are rejected destination, source vector, scratch adjacency, scratch degree array, `V`, `maxHops=20`, and `maxDistance`.

For each valid source record `(s,t,w)` in reverse order:

1. allocate/reset `pred[V]=-1`, `dist[V]=-1`;
2. set `dist[t]=0`, enqueue `t` in a FIFO worklist;
3. scan each dequeued vertex's current adjacency row in stored order;
4. relax `v` iff unseen or `newDist<dist[v]`; equal distance retains the first predecessor;
5. re-enqueue on every strict improvement;
6. stop when `s` is first relaxed;
7. compute predecessor hop count from `s` back to `t` if reached.

Move the source record to the rejected destination and invalidate it iff:

```text
no path exists
OR
(w <= maxDistance AND hopCount > 20)
```

Negative/corrupt predecessor length also enters this branch. On move:

- copied record flag is set to `1`;
- the pair is added to scratch adjacency;
- both scratch degrees increment;
- returned count increments.

If `w>maxDistance` and a path exists, the hop-count condition does not move it here. In the normal pipeline every still-valid edge has its direct `t->s` entry, so `hopCount=1` and the helper is a no-op. A nonzero return indicates violated scratch/source consistency or a nonstandard graph; bounded vanilla acceptance requires return zero. This preserves the observed branch without treating corrupt-input behavior as a supported normal result.

## 7. Pass B: cross-group pruning (`0x140885580`)

### 7.1 Sort, permutation and current/target counters

Sort B. Build physical index pool `[0,1,...,NB-1]`. Call global RNG with tag `0x220` unconditionally, seed the independent local generator, then consume exactly `NB` local draws to visit every physical index once by swap-with-last:

```text
while pool not empty:
    j = LocalNext() % pool.count
    idx = pool[j]
    pool[j] = pool[last]
    pool.pop()
    inspect B[idx]
```

Invalid records still occupy a permutation slot and consume a draw, but are skipped after selection.

For a valid cross-group edge, resolve `ga,gb`. Native fields are:

```text
targetDegree[g] = group[g]+0x98
currentCandidateDegree[g] = group[g]+0x9C
```

Remove the edge in this randomized visit iff:

```text
(h < scenarioMaximum
 AND current[ga] > target[ga]
 AND current[gb] > target[gb])
OR weight > maxDistance
```

The degree comparison is strict `>`. Distance equality survives. On removal:

- copy to `rejectedB`;
- set `B[idx].a=-1`;
- decrement `current[ga]` and `current[gb]` once each.

For valid B input `ga!=gb`. The distance branch ignores target/current and density maximum. `current` is the mutable selection counter inherited from M13; it is not recomputed from actual adjacency during this loop.

After all `NB` visits, sort current `rejectedB` by `(weight,a,b)`. Degree-helper additions later form an unsorted appended suffix.

### 7.2 Combined scratch graph and degree helper

Build a fresh scratch graph with the same layout as pass A:

1. scan currently valid B in forward sorted order and add symmetric entries/degrees;
2. then scan currently valid A in forward sorted order and add symmetric entries/degrees.

Thus scratch adjacency row order is B survivors before A survivors, and within each source follows physical order. Use the same threshold `T`.

If `h<scenarioMaximum`, apply the degree helper to B only, scanning B forward. It moves a B record when both scratch endpoint degrees are `>=T`, invalidates it and updates scratch adjacency/degrees exactly as in pass A. If `h>=scenarioMaximum`, skip it.

This helper does not receive group records and does not decrement `group+0x9C`. Consequently `currentCandidateDegree` is a randomized-pass selection counter, not a guaranteed final actual degree after later degree pruning.

Run the defensive helper on B. Vanilla simple input requires `DBdef=0`. Native materializes the first `rejectedB.count-DBdef` records into cross-group rejected lists at group record `+0x30`, two directed 32-byte logical entries per edge:

```text
group ga: {otherGroup=gb, systemA*,systemB*,weight}
group gb: {otherGroup=ga, systemB*,systemA*,weight}
```

If the defensive return is nonzero, native additionally assumes the affected reverse-scan records occupy the corresponding physical B suffix and increments `group+0x9C` for both endpoint groups of that suffix. This branch is unreachable under the normal direct-edge invariant and is outside the supported vanilla-clean contract; a safe port may surface `DefensiveInvariantViolation` instead of emulating malformed bookkeeping.

The native pass return value is `rejectedB.count-DBdef`; the caller does not use it to choose survivors.

## 8. Materialize surviving B

Caller scans sorted B forward and skips invalid records. For each survivor:

```text
outFlag = ((-int8(record.flag)) & 8) | 1
```

M13 records have `flag=0`, so vanilla normal survivors get `outFlag=1`. Insert:

```text
systemA += {neighborHandle=handleB, weight, flag=outFlag}
systemB += {neighborHandle=handleA, weight, flag=outFlag}
```

Native adjacency storage is again `system+0x588`. Then perform the same native system dirty/cache notifications and append cross-group accepted bookkeeping records at group `+0x48`:

```text
group ga: {otherGroup=gb, systemA*,systemB*,weight}
group gb: {otherGroup=ga, systemB*,systemA*,weight}
```

After the complete B scan, caller runs the global hyperlane/index refresh helper and advances the global topology dirty/state marker to `2`. No RNG is consumed.

## 9. Outputs and count identities

Logical output:

```text
finalEdges = valid sorted A records, then valid sorted B records
system adjacency = two directed entries per final edge
A entry flag = 0
B entry flag = 1 on normal M13 input
```

Mutated diagnostic/bookkeeping output:

- A and B remain allocated/sorted with `a=-1` holes;
- rejected group lists `+0x60` (A) and `+0x30` (B);
- accepted group lists `+0x78` (A) and `+0x48` (B);
- B `group+0x9C` counters decremented only by randomized B removals in the normal path;
- system dirty/cache state and final topology refresh/state marker.

For normal `Ddef=0`:

```text
Ainvalid = L + Arandom + Adegree
Asurvive = NA - Ainvalid

Brandom = number removed by randomized target/distance loop
Bdegree = number removed by B degree helper
Binvalid = Brandom + Bdegree
Bsurvive = NB - Binvalid

final undirected hyperlanes = Asurvive + Bsurvive
directed system adjacency entries added = 2*(Asurvive+Bsurvive)
```

`R` is a requested total for the initial A distance/random phase only. Degree-helper removals may increase total A invalidations beyond `max(L,R)`. Pool exhaustion may keep random-phase invalidations below `R`.

## 10. Branch and error semantics

- `V<3` never enters this contract: M13 handles `V<2` as empty and `V=2` by direct adjacency without either draw.
- For `V>=3`, empty A/B are valid and still consume both global draws.
- `h==scenarioMaximum` skips both degree helpers and disables the target-degree clause in B; strict `<` is required.
- `h>scenarioMaximum` is rejected earlier by vanilla settings validation and is invalid adapter input.
- `h<=0` is outside the supported contract; do not emulate native division failure/sentinel arithmetic.
- `weight==maxDistance` is retained by both mandatory distance tests.
- A nonnegative endpoint outside `[0,V)` is not safely bounds-checked by all native helpers. Treat it as invalid input before mutation.
- Stale/unresolvable full handles can route native materialization to a global sentinel and mutate unrelated state. Safe reproduction must fail validation rather than emulate this path.
- Duplicate/simple-graph violations can break fixed row capacity/linear edge removal assumptions. M13's Delaunay candidate set satisfies the simple-edge invariant.
- `uint16` scratch degrees and `int32` costs/distances are safe in the bounded vanilla domain; overflow/corruption is invalid input.
- Native allocation failure/fatal diagnostic paths are not transactional. A safe adapter should preflight memory and return a typed failure before consuming supplied draws or mutating records.

## 11. Acceptance vectors

### 11.1 A random target

Input: ten sorted valid A records, all `weight<=maxDistance`, `h=100000`, `scenarioMaximum=100000`, `gA=0`.

```text
R=floor(10*15000/100000)=1
L=0
pool=[0..9]
first local=4BAE8956; 4BAE8956 mod 10 = 2
```

Physical record 2 is invalidated. Degree helper is skipped because `h==scenarioMaximum`; other nine materialize in sorted order. The global draw is one regardless of `R`.

### 11.2 A start offset and mandatory excess

Input: sorted A with `NA=20`, `h=100000`, `R=3`, exactly `L=1` over-distance record at the suffix.

```text
requested=2
eligible scan starts at physical index 1
physical index 0 is protected from random selection
invalid suffix over-distance record is omitted from pool
```

If instead `L=4`, `requested=-1`; no local draw occurs and all four mandatory records remain invalid, but tag `0x1BC` global draw still occurs.

### 11.3 B target counters

Two sorted cross-group edges `e0,e1` join groups `7,9`. Initially `current[7]=current[9]=2`, `target[7]=target[9]=1`, `h<scenarioMaximum`, both weights in range, `gB=0`.

For pool `[0,1]`, first local value is even, so visit `e0` first. Both counters are `2>1`: remove `e0`, then counters become 1. Visiting `e1` fails strict target comparison and retains it. With scratch degrees below `T`, final B survivor is `e1` and its materialized flag is 1.

### 11.4 Distance overrides target

One B edge has `current==target` at both ends but `weight=maxDistance+100000`. It is removed on visit and decrements both current counters. At `weight==maxDistance` it survives this clause.

### 11.5 Degree equality

For any valid source edge with scratch degrees `(T,T)`, degree helper removes it, updates both rows by swap-with-last and produces `(T-1,T-1)`. With `(T-1,T)` it retains it. This detects an incorrect strict `>T` implementation.

### 11.6 Final ordering and flags

After sorting/invalidation:

```text
A physical: [A0 valid, A1 invalid, A2 valid]
B physical: [B0 invalid, B1 valid, B2 valid]
```

Final materialization order is `A0,A2,B1,B2`; A entries use flag 0, B entries use flag 1. Invalid records do not compact either vector.

### 11.7 Defensive normal invariant

Scratch contains every valid source edge in both directions. For record `(s,t,w)`, BFS from `t` immediately relaxes `s` via the direct entry; predecessor chain has one hop. The helper returns zero for any `w`, including `w>maxDistance`, and changes neither source nor rejected tail.

## 12. Vanilla completeness boundary

Given M13 A/B vectors, valid group mapping/targets/counters, `h`, scenario maximum, max distance and supplied `gA/gB`, this contract contains every normal vanilla branch needed to reproduce exact selected hyperlanes and their materialization order. Remaining non-bitcert dependencies are engine object-resolution/cache notification implementations and pathological allocation/corruption behavior; neither changes the pure accepted-edge set on validated vanilla input.
