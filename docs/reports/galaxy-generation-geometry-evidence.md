# M11/M13: dependency boundary и raw geometry operators

13 октября 2026. Исследовательский чат `01a1359a-86d4-78f3-a4d2-5dda015220f6`
сообщил о восстановлении M11 и M13. Addendum прочитан единым bytesnapshot:
SHA256 `703AAA62FC1FEBCAE1C8657141BB1D01F6E6994D1EFF8C64069A0CB05DBF5CB9`,
903 строки, §16.5 и §19. Это static reverse evidence того же executable hash,
не повторное машинное исследование author'ом. Никакие scripts/bytes/assets не копируются.

## Точная dependency, ещё не присутствующая в модуле

Embedded Triangle 1.6, build TRILIBRARY/SINGLE/NO_TIMER, REAL IEEE binary32,
switches первой tessellation `pczAevnQ`, после итераций `pczAevn`.
Private LCG начинается с seed1 на каждый вызов; allocation/writeedges order и
robust predicates определяют endpoints/order/ties, которые generic Delaunay не воспроизводит.
Geometry backend не установлен и не подменён; M11/M13 composite gates сохраняются.

## Последующие durable уточнения и реализация

Проверен единый bytesnapshot SHA
`43F6B335058F5E0231FF7115A01621E7F72646EE71E24EB00508728FCBEFA0FB`.
§16.5 теперь явно исключает closure check; §19.1 описывает N2 raw adjacency;
§19.4 содержит annulus equality/midpoint/length vectors. Эти уточнения использованы
`SuppliedTessellationPass` и `SuppliedTopologyPostprocess`, исходные scripts не копировались.

Supplied pass ориентирует incident edges, first-match stitching swap'ами, pin rays,
<=1 segment, failed chain и все vertices вне inclusive bbox. Проверяется final endpoint,
но он безусловно исключается из centroid count; last.end==first.start не проверяется.
Новый immutable site array рассчитан по неизменному supplied tessellation (simultaneous).
Backend rebuild, dummy input construction, float→native-system conversion/IDs/world update
не входят в pass. Validate finite inputs/valid indices — explicit boundary нашего raw API.

Distinguishing centroid polygon (−784,−889),(842,157),(−400,621),(−580,322):
X bits C313A93A, Y C2046071, area1105290. Double-only pipeline даёт Y bits C2046072;
наш vector совпадает с staged native pipeline.

Postfilter supplied writeedges order: rMin/rMax из FM squares; strict midpoint annulus;
midpoint signed sum/2 trunc0. Native float-quantized topology использует original fixed
coordinates для этой стадии. Survivors сохраняют endpoint direction и order,
flag0; одинаковые mapped IDs идут в same-group vector, остальные в different-group.

Независимые annulus vectors:
- (100000,100000),(100000,−100000),(−100000,0), edge0–1: midR2=rMin100000 → reject;
- (200000,100000),(200000,−100000),(100000,0),(300000,0), edge0–1:
  rMin100000<midR2400000<rMax900000 → keep;
- (300000,0),(300000,0),(100000,0), edge0–1: midR2=rMax900000 → reject;
- midpoint endpoints X−100001,0 → −50000, не −50001.

N>=3 stored lengths: separation99999→0,100000→100000,
dx=dy1000000 integer root1414213→stored1400000.
N2 возвращает два opposite-handle adjacency flag0 с raw length1414213,
без whole-unit floor и без candidate records/Triangle/annulus/pruning.
Arithmetic checked domain не обещает extreme native overflow.

Relaxation bbox точно [-50000,50000]² inclusive, incident primal/Voronoi edges
ориентируются по site, сшиваются first-match+swap, unbounded/hull sites pinned.
Raw centroid — независимая часть pass, которой не нужна сама tessellation.
Указанные вопросы по postprocess/supplied pass разрешены последующими durable уточнениями выше.

## Реализованный M11 centroid

Fan от P0, triangles (P0,Pi,Pi+1) в input order.
Все coordinate subtraction/addition/multiplication и cross — staged float32.
Cross и float32 weighted coordinate products переводятся в double и суммируются.
Final centroid cast float32(sumX/(3×sumCross)), аналогично Y.
SignedArea=sumCross/2 — диагностический выход.
Отдельные NoInlining float helpers фиксируют rounding stages, FMA не используется.
Centroid helper сам не делает stitching/pinning; отдельный supplied pass реализует их,
но tessellation и world updates отсутствуют.

Контрольные независимые vectors:

- triangle (0,0),(6,0),(0,3) → centroid(2,1), area9;
- reverse orientation → centroid(2,1), area−9;
- square (−1,−1),(1,−1),(1,1),(−1,1) → centroid(0,0), area4;
- two points → area0, centroid(NaN,NaN): native zero-area guard отсутствует.

Наш raw API требует >=2 finite input vertices; нечисловые outputs zero-area не
маскируются fallback'ом и не выдаются за готовую relaxed galaxy.

## Реализованный M13 input quantization

По каждому original fixed int64 coordinate:
float32(trunc_toward_zero(raw/100000)).
Это не прямой fixed→float division: raw99999/−99999 превращается в0,
raw199999/−199999 в1/−1. World/game RNG не расходуется.
Сам Triangle не реализован. Postfilter/order-preserving length/split и N2 special case
реализованы отдельно выше. Наличие raw helpers не закрывает composite M13.
