# Восстановленные операторы первого среза

GALAXY-GENERATION-001 attempt1, 13 октября 2026. Собственный конспект минимального
подмножества evidence, использованного реализацией. Источник: исследовательский чат
`01a1359a-86d4-78f3-a4d2-5dda015220f6`, `native-generation-reverse-addendum-452.md`,
SHA-256 текущего проверенного дополнения
`44E346A9E3A32DFC180CD62EB805E1C2CCAFD704DEBF2047FD405BEA590109C1`.
Полный внешний оригинал остаётся нетронутым в outputs research-чата;
[индекс provenance](galaxy-generation-evidence-index.md).
Исследованный executable SHA-256:
`400DF27C82DDC845AA9DCE79BD468D81F18F060D299CD263E3AFA93AEF4F7A83`.
Это static reverse evidence, без запуска игры. Здесь нет игровых scripts,
машинных bytes, dumps, disassembly и assets; не перенесены неиспользуемые сведения
об отдельных planet/deposit operators. Это описание математического поведения.

## M02

NextRandom `0x141A7BAD0–0x141A7BBD7`: два uint32 слова seedBase/counter.
Load setter `0x140A95CD0–0x140A95CEE` копирует их из +0x20/+0x24.
Все промежуточные действия modulo 2^32:

1. x = seedBase − counter × 0x4AD685B3.
2. x XOR= x shifted right 8; x += 0x68E31DA4.
3. x XOR= x shifted left 8; x *= 0x1B56C4E9.
4. x XOR= x shifted right 8; x *= 0x92D68CA2.
5. x XOR= x shifted right 8; result = x AND 0x7FFFFFFF.
6. counter увеличивается на 1 modulo 2^32; seedBase не меняется.

Параметр диагностической source/tag в формулу не входит.

| seedBase | counter | draw | следующий counter |
|---|---|---|---|
| 00000000 | 00000000 | 6FBE2FBA | 00000001 |
| 12345678 | 00000001 | 12DE7D1D | 00000002 |
| 89ABCDEF | 10203040 | 40AD9A83 | 10203041 |
| FFFFFFFF | FFFFFFFF | 450383EF | 00000000 |

Векторы получены от reverse-партнёра, не сгенерированы проверяемой C# реализацией.
M01 для explicit specifiedSeed описан ниже; auto/global state не смешивается с локальным.

## M01: явный specifiedSeed

Native `0x14098D35F–0x14098D39A`, потребление local state
`0x14098D7EA–0x14098D838`, addendum §10.1.
При p=0..INT_MAX: SeedBase=0, Counter=Mix32(0x5EA6BA9F−p×0x4AD685B3).
Mix32 — пункты смешивания M02 после начального вычитания без маски sign bit.
Все операции uint32 wrap, right shifts логические. Init не расходует draws.
Native physical local pair {counter,seedBase}; наш API возвращает именованные поля.

| p | initial Counter hex | first draw hex |
|---|---|---|
| 0 | EDE419C6 | 4BAE8956 |
| 1 | E5F10AFA | 65AF1864 |
| 123456789 | 52268A8E | 3E3059B9 |
| 2147483647 | 3A430740 | 49605B98 |

Независимые vectors сохранены в addendum и повторно подтверждены партнёром.
Проверен read-only SHA256 установленного executable: совпал с hash выше.
Native машинный reverse author/reviewer повторно не выполняли; evidence — static report.
Отрицательный native seed вызывает global RNG; наш API отвергает отрицательные значения.
Поздние два pruning passes используют отдельные global draws: explicit local seed
не достаточен для полного native совпадения (addendum §10.4).

## Raw M04

Evaluator `0x140A0AA20–0x140A0AC03` получает signed int64 base/span
и уже потраченный caller draw. Если span = 0, результат base; иначе
base + trunc_toward_zero(span × (draw mod 1000) / 1000).
Для положительного span верхняя точка base+span не достигается.
API проверяет uint31 draw, representable int64 product/sum и не вызывает RNG.
Отрицательный span использует усечение к нулю; native extreme-overflow не доказан.

Контрольные raw units: base=1000000, span=400000, draw=123456789 → 1315600.
Тот же base/draw, span=0 → 1000000.
При span=−400001 промежуточное произведение −315600789, вклад −315600,
результат 684400. Этот вектор различает truncation и floor.
Parser encoding script min/max в base/span остаётся M25;
raw evaluator не объявляется универсальным integer-range sampler (M03).
