# Bounded handoff: post-relax coordinates -> M13 candidate vectors

## Назначение и граница

Этот документ задаёт переносимый контракт участка между завершённой Lloyd-релаксацией и входом в два pruning passes гиперлиний. Он сверен с разделами 16.5, 19 и 14 основного addendum и с `candidate_pairs_report.md` / `triangle_backend_contract.md`.

Цель — **полнота vanilla-логики** на штатном конечном входе, а не побитовая сертификация конкретного build. Совместимый Triangle 1.6 SINGLE backend с описанным порядком выдачи достаточен. Неизвестные literal compiler flags, исходный diff Win64-port и прямое runtime-сравнение embedded routine остаются certification dependencies и не являются blocker для этой цели.

Контракт заканчивается двумя ordered candidate vectors и изменёнными cross-group counters. Сортировка, density/max-distance deletions, RNG pruning, connectivity/degree decisions и материализация adjacency принадлежат следующему этапу.

## 1. Входное состояние после relaxation

Пусть `systems[0..N-1]` — ordered vector реальных систем в порядке acceptance/generation. Индекс этого массива является topology point index. После relaxation:

- у каждой системы есть валидный полный handle и fixed coordinates `P[i]=(xRaw,yRaw)` масштаба `100000`;
- координаты ordinary sites уже записаны обратно в system objects;
- spatial/index cache уже refresh/invalidate'нут helper'ом `0x140254880`;
- для штатной конверсии centroid float `tFloat` координата равна `trunc_toward_zero(tFloat) * 1000`;
- 256 dummy sites не входят в `systems` и не передаются в M13: они существовали только в holder предыдущей Lloyd tessellation;
- group mapping передаётся отдельно и должен позволять для каждого system handle получить mapped group ID;
- выходные adjacency систем и два candidate vectors изначально находятся в согласованном состоянии, ожидаемом caller'ом.

Минимальный переносимый input:

```text
systems[N]: ordered {
    handle: uint32,
    xRaw: int64 fixed(100000),
    yRaw: int64 fixed(100000)
}
groupIdBySystemSlot[...]: group ID indexed by low24(handle)
groupRecords[...]: records with boundary counter corresponding to native +0x9C
```

Для `N<2` group mapping не читается. Для `N=2` он также не нужен. Для `N>=3` каждый handle должен разрешаться в ту же system object, а `low24(handle)` должен адресовать валидный mapping entry.

## 2. Полный branch contract

### 2.1 `N < 2`

Ничего не создавать и сразу вернуть:

- Triangle не вызывается;
- adjacency не меняется;
- candidate vectors пусты;
- group counters не меняются;
- RNG не расходуется.

### 2.2 `N == 2`

Triangle, annulus filter, group split и оба pruning passes обходятся полностью.

Для `P[0]` и `P[1]`:

```text
dx = P[1].x - P[0].x
dy = P[1].y - P[0].y
lengthRaw = floor_sqrt(dx*dx + dy*dy)
```

Затем непосредственно вставить две adjacency entries:

```text
systems[0] += { neighborHandle=systems[1].handle, length=lengthRaw, flag=0 }
systems[1] += { neighborHandle=systems[0].handle, length=lengthRaw, flag=0 }
```

Здесь длина сохраняет fixed scale `100000` со всей целочисленной точностью корня. Нельзя применять whole-map-unit floor из ветки `N>=3`. Не применяются annulus, density, `max_hyperlane_distance`, group split или cross-group counter increments. Даже coincident points дают две прямые adjacency entries с нулевой длиной.

### 2.3 `N >= 3`

Выполнить шаги 3–7 ниже. Actual adjacency на этом этапе ещё не создаётся. Результат — два candidate vectors и side effect на counters для cross-group records.

## 3. Новый M13 Triangle input

M13 создаёт свежий holder из трёх `triangulateio` records размером `0xA8`:

```text
holder + 0x000 : input
holder + 0x0A8 : primal output
holder + 0x150 : Voronoi output
```

Он не переиспользует holder или meshes из relaxation. Input содержит `N` ordinary points, без dummy sites, segments, holes или regions. Для каждого `i` в неизменном `systems` order:

```text
xTri[i] = float32(trunc_toward_zero(P[i].x / 100000))
yTri[i] = float32(trunc_toward_zero(P[i].y / 100000))
```

Затем один раз вызвать:

```text
triangulate("pczAevnQ", &input, &primal, &voronoi)
```

Backend contract:

- behavioral Jonathan Shewchuk Triangle 1.6;
- `TRILIBRARY + SINGLE + NO_TIMER` equivalent, `REAL=float32`;
- default divide-and-conquer Delaunay, Dwyer alternating cuts;
- adaptive exact predicates enabled (`X` отсутствует);
- exact input order preserved in output numbering (`j` отсутствует);
- private pivot state resets to `1` for this call;
- pivot LCG is `state=(state*1366+150889)%714025`, `pivot=state/(floor(714025/choices)+1)`;
- no pre-dedup, epsilon, jitter, endpoint canonicalization or output sort;
- emit `primal.edgelist` by Triangle memory-pool allocation order, orientations `0,1,2`; an interior edge is emitted once by pointer ordering;
- duplicate survivor, cocircular diagonal and collinear behavior are those of Triangle 1.6 exact path.

M13 reads only `primal.numberofedges` and `primal.edgelist`. Voronoi output is allocated because it is requested by the switch string, but candidate extraction does not use it.

For the post-relax native conversion `P=t*1000`, the M13 quantizer is equivalently:

```text
float32(trunc_toward_zero(t / 100))
```

This second quantization is intentional and can merge distinct post-relax coordinates.

## 4. Precomputed annulus state

Define signed fixed helpers at scale `S=100000`:

```text
FM(a,b) = trunc_toward_zero((a*b) / S)
FD(a,b) = trunc_toward_zero((a*S) / b)
```

Use checked/wide intermediates in a safe replica. Vanilla coordinate bounds fit the observed signed `int64` domain.

Before scanning edges, compute from original post-relax fixed coordinates, never from `xTri/yTri`:

```text
r2[i] = FM(P[i].x,P[i].x) + FM(P[i].y,P[i].y)
rMin = min(r2[i])
rMax = max(r2[i])
```

These values are immutable throughout the scan.

## 5. Ordered scan, validation, annulus and record construction

Scan `primal.edgelist[k]` for `k=0..numberofedges-1` in returned order. For each directed pair `(a,b)`:

1. If either endpoint is negative or `>=N`, skip the edge with no record, counter change or RNG use.
2. Resolve `a,b` against ordered `systems`; read the original fixed `P[a],P[b]`.
3. Compute the fixed midpoint in this exact order:

   ```text
   midX = FD(P[a].x + P[b].x, 200000)
   midY = FD(P[a].y + P[b].y, 200000)
   midR2 = FM(midX,midX) + FM(midY,midY)
   ```

4. Keep the edge iff:

   ```text
   rMin <= 0 || (rMin < midR2 && midR2 < rMax)
   ```

   Both annulus bounds are strict. Equality to either positive bound rejects the edge. If any site has `r2<=0`, the filter is disabled by the first clause.

5. For a survivor compute from original fixed coordinates:

   ```text
   dx = P[b].x - P[a].x
   dy = P[b].y - P[a].y
   lengthRaw = floor_sqrt(dx*dx + dy*dy)
   lengthUnits = trunc_toward_zero(lengthRaw / 100000)
   storedLength = lengthUnits * 100000
   ```

6. Form a copied 24-byte logical record:

   ```text
   +0x00 int32 a
   +0x04 int32 b
   +0x08 int64 storedLength
   +0x10 byte  flag = 0
   +0x11..+0x17 padding/non-semantic here
   ```

`a,b` remain point indices, not handles. Direction remains Triangle's `org,dest`. Do not replace `(a,b)` with `(min,max)` and do not sort here.

## 6. Stable split and cross-group side effect

For each surviving record, in the same edge scan:

```text
groupA = groupIdBySystemSlot[low24(systems[a].handle)]
groupB = groupIdBySystemSlot[low24(systems[b].handle)]

if groupA == groupB:
    append record to vectorA
else:
    groupRecords[groupA].boundaryCount += 1
    groupRecords[groupB].boundaryCount += 1
    append record to vectorB
```

`boundaryCount` is the native field at `groupRecords[groupId*0xA8]+0x9C`. Exactly two increments occur per cross-group candidate, one for each endpoint group. Same-group candidates do not change these counters.

The split is stable independently in each vector: relative order equals the subsequence order in `primal.edgelist` after invalid-endpoint and annulus rejection. Capacity reservation (native approximately `4*N` records for each vector) is an allocation detail and has no semantic effect. Vector records own their values and retain no pointer into Triangle outputs.

## 7. Output and ownership contract

For `N>=3`, return:

```text
vectorA: same-mapped-group candidate records, Triangle-relative order
vectorB: cross-mapped-group candidate records, Triangle-relative order
groupRecords: same object with +0x9C counters incremented for B records
systems: positions and adjacency unchanged by M13
```

Ownership/lifetime:

- `systems` and group mapping are borrowed/read-only during candidate extraction;
- group counters are caller-owned mutable state;
- the fresh Triangle holder owns its input and all primal/Voronoi allocations;
- after the scan, caller frees all Triangle input/output arrays and the holder;
- `vectorA/vectorB` own copied 24-byte records and survive holder destruction;
- no output record points into Triangle memory.

RNG ledger:

- no local galaxy RNG draw;
- no global game RNG draw;
- Triangle's private sort LCG is internal, resets to `1` for each call and is not visible in either game RNG ledger;
- the first game-global pruning draws occur only in the downstream passes (`0x1BC` for A and `0x220` for B).

## 8. Invariants required at the pruning boundary

After M13 and before pruning:

1. Every record endpoint is in `[0,N)` and indexes the unchanged ordered `systems` array.
2. Every record came from exactly one emitted Triangle edge that passed the strict annulus filter.
3. `storedLength>=0`, is divisible by `100000`, and is the floor of the original fixed Euclidean distance to a whole map unit.
4. Every record flag is zero.
5. Endpoint direction is preserved and need not satisfy `a<b`.
6. Vectors are unsorted; their only ordering guarantee is stable Triangle subsequence order.
7. A contains exactly same-group survivors; B contains exactly cross-group survivors.
8. Sum of all boundary-counter increments is `2*vectorB.count`; per group it equals the number of incident B records.
9. No system adjacency was materialized for `N>=3`.
10. No game RNG state changed.

Degenerate input behavior within the bounded backend:

- exact binary32 duplicates are handled inside Triangle; only its selected survivor can occur in edges, while indices retain original input numbering;
- all-collinear unique input produces zero primal edges and therefore two empty candidate vectors;
- cocircular ties use exact predicates and Triangle merge tie behavior;
- vanilla supported domain is finite coordinates and a one-block mesh (`3<=N<=2047`); NaN/Inf/OOM, corrupt handles/mapping and custom multi-block ordering are outside this contract.

## 9. Control vectors

### 9.1 End-to-end post-relax -> M13 quantization

These begin with relaxation centroid float, then post-relax fixed coordinate, then M13 Triangle coordinate:

```text
 123.9  -> t= 123 -> raw= 123000 -> xTri= 1.0
-123.9  -> t=-123 -> raw=-123000 -> xTri=-1.0
  99.9  -> t=  99 -> raw=  99000 -> xTri= 0.0
 -99.9  -> t= -99 -> raw= -99000 -> xTri= 0.0
   1.999-> t=   1 -> raw=   1000 -> xTri= 0.0
  -1.999-> t=  -1 -> raw=  -1000 -> xTri= 0.0
```

This detects implementations that feed relaxation floats directly to M13 or omit the second truncation.

### 9.2 Branch and length vectors

- `N=0` or `N=1`: no call, no candidate, no adjacency.
- `N=2`, `dx=dy=1000000`: `lengthRaw=1414213`; both direct adjacency entries store `1414213`.
- `N>=3`, raw separation `99999`: candidate `storedLength=0`.
- `N>=3`, raw separation `100000`: candidate `storedLength=100000`.
- `N>=3`, `dx=dy=1000000`: `lengthRaw=1414213`, candidate `storedLength=1400000`.

### 9.3 Annulus operators

These test the filter independently of whether Triangle emits the named pair:

- lower equality: `(100000,100000),(100000,-100000),(-100000,0)` gives `rMin=100000`, `rMax=200000`; midpoint of `0-1` has `midR2=100000`, reject;
- strict interior: `(200000,100000),(200000,-100000),(100000,0),(300000,0)` gives `rMin=100000`, `rMax=900000`; midpoint of `0-1` has `midR2=400000`, keep;
- upper equality: endpoints `(300000,0),(300000,0)` with another point `(100000,0)` give `midR2=rMax=900000`, reject;
- signed midpoint: X endpoints `-100001` and `0` give `midX=-50000`, not `-50001`;
- any point `(0,0)` gives `rMin=0`, so every valid emitted edge passes the annulus predicate.

### 9.4 Triangle order vectors

Lift listed Triangle coordinates to raw fixed by multiplying each component by `100000`; origin disables annulus filtering. Endpoint order is material:

```text
[(0,0),(10,0),(0,10)]
edges: (0,1),(1,2),(2,0)
storedLength: 1000000,1400000,1000000

[(0,0),(10,0),(10,10),(0,10)]
edges: (3,0),(0,1),(1,3),(1,2),(2,3)
storedLength: 1000000,1000000,1400000,1000000,1000000

[(0,10),(10,10),(10,0),(0,0)]
edges: (0,3),(3,2),(2,0),(2,1),(1,0)

[(0,0),(10,0),(0,10),(0,0)]
edges: (3,1),(1,2),(2,3)

[(0,0),(10,0),(20,0),(30,0)]
edges: empty
```

These are source-side conformance vectors from the audited compatible Triangle 1.6 Win64 port, not a black-box capture of the embedded game routine. That distinction affects bit-certification only; the behavioral contract is sufficient for vanilla completeness.

### 9.5 Stable split vector

Given emitted survivors in order:

```text
E = [(3,0,w0),(0,1,w1),(1,3,w2),(1,2,w3),(2,3,w4)]
groupByPoint = [7,7,9,9]
```

the result is:

```text
A = [(0,1,w1),(2,3,w4)]
B = [(3,0,w0),(1,3,w2),(1,2,w3)]
counter[7] += 3
counter[9] += 3
```

Neither vector is sorted during this split.

## 10. Что передать следующему pruning этапу

Передать без преобразований:

- ordered `systems` array for point-index -> full-handle resolution;
- unsorted `vectorA` и `vectorB` с исходным direction/order;
- уже обновлённые group boundary counters;
- `num_hyperlanes h`, scenario maximum, `max_hyperlane_distance`, degree threshold/target arrays;
- два внешних global RNG results/seeds, если требуется exact supplied-state run.

Следующий этап, а не M13, обязан:

1. отдельно отсортировать A и B по `(weight,a,b)`;
2. выполнить compulsory max-distance removals;
3. получить global draw tag `0x1BC` и local selector для pass A;
4. построить adjacency/degree scratch state, выполнить degree and defensive connectivity helpers и материализовать survivors A;
5. получить global draw tag `0x220`, перемешать physical indices B, применить target/max-distance pruning;
6. строить combined adjacency из surviving B и A, выполнить оставшиеся helpers и материализовать survivors B.

Любая canonicalization, pre-sort, ранняя materialization или перенос pruning RNG в M13 меняет observable native order/ledger и нарушает этот handoff.

## 11. Реально оставшаяся недоказанность

Для vanilla-complete реализации скрытых ветвей между relaxation и pruning больше нет. Остались только:

- предоставить валидные actual system handles и native group mapping/counter state;
- использовать behavioral Triangle 1.6 SINGLE port с указанными ties/output order;
- для native-bit-certified label отдельно подтвердить embedded routine runtime vectors, literal floating environment/compiler behavior и полный Win64 fork diff.

Последний пункт не нужен для заявленной vanilla completeness и не должен блокировать следующий pruning этап.
