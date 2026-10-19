# Дополнение к реверсу native-генерации Stellaris 4.5.2

Дата исследования: 13 октября 2026 года. Исследуемый executable: `stellaris.exe`, SHA-256 `400df27c82ddc845aa9dce79bd468d81f18f060d299cd263e3afa93aef4f7a83`. Исследование выполнено статически. Установка игры оставалась только для чтения; игра и UI не запускались.

Это дополнение не заменяет три исходных отчёта и не меняет проектный контракт планет. Оно фиксирует только новые результаты по ранее открытым пунктам `M01–M40`. Формулы изложены своими словами, без исходных машинных байтов и без программной реализации.

Обозначения уровня уверенности:

- **N** — подтверждено связанным native control flow и операциями над полями;
- **S** — подтверждено script-данными или документацией;
- **I** — вывод, для которого явно указаны предпосылки.

## 1. Изменение статуса открытых пунктов

| ID | Новый статус | Результат |
|---|---|---|
| M01 | Закрыт для local seed initialization | Восстановлены explicit и automatic paths до 31-битного `specifiedSeed` и точное преобразование в локальные `counter/seedBase`. Автоматическое значение зависит от global RNG; для совпадения позднего pruning дополнительно нужны ещё два global results либо соответствующее состояние/ledger. |
| M02 | Закрыт | Полностью восстановлена функция следующего 31-битного случайного значения и изменение счётчика. |
| M03 | Снят как общий блокер | Единого общего integer-range helper нет; нужные sampler’ы описываются у конкретных callers. Integer `count`, `size` и mandatory-neighbor distance закрыты ниже. |
| M04 | Закрыт для `0x140A0AA20` | Восстановлено вычисление fixed-point значения из пары `base/span` и уже полученного random draw. |
| M05–M10 | Закрыты для основного coordinate loop | Восстановлены generic ring/arms/no-arms branches, draw counts, collision predicate и точная семантика budget: 10000 разрешённых retries после rejected candidates. |
| M11 | Закрыт алгоритмически | Восстановлен bounded Lloyd/Voronoi pass, Triangle ABI/switches, cell stitching и staged float32 centroid. Bit identity зависит от совпадения embedded Triangle tie-breaks/FP mode. |
| M12 | Закрыт для ordinary systems | Objects alloc/register’ятся при принятии координаты; later pass `0x140977780` идентифицирован как procedural nebula placement. |
| M13 | Закрыт | Идентифицирован embedded Triangle 1.6, exact build/options/private RNG, input quantization, edge order, annulus filter, length и stable split. |
| M14 | Закрыт | Восстановлены partition sampling, adjacency filters, random frontier, recursive repair и fair-sweep variant. |
| M15 | Закрыт для штатного simple edge set | Восстановлены оба pass, сортировка, random ordering, degree predicates, invalidation и материализация surviving edges. Defensive consistency helper описан отдельно. |
| M16 | Закрыт для валидных scenario data | Восстановлены обе стратегии числа кластеров, cap и нижняя граница. |
| M17 | Закрыт для start/resource cluster geometry | Start clusters равноугольны и не используют RNG; resource sectors расходуют `N+2` local draws. |
| M18 | Закрыт как assignment solver | Старая трактовка distance placement снята; восстановлены signed-score greedy assignment, ties, column disable и unique cyclic fallback. |
| M20 | Закрыт | Восстановлены scaled chance, оба округления, отсутствие clamp и условие прохождения caller'а. |
| M19/M21 | Закрыты | Восстановлены pool filter, zero-total fallback, spawn roll, retry и оба места увеличения use-count. |
| M22 | Закрыт | Parent executor/effect завершается до neighbor traversal; новые neighbors обрабатываются следующим batch. |
| M23 | Закрыт по retry/acceptance и draw ledger | Восстановлены 15 attempts, три draws на попытку, buffer predicate и forced acceptance последней координаты. |
| M24/M26 | Закрыты для descriptor traversal | Восстановлены compiled category order, recursion и draw order planet/change_orbit/moons/orbital lines. |
| M25 | Закрыт для используемых initializer ranges | Fixed range half-open; integer `count/size/distance` используют signed inclusive span. |
| M27/M28 | Закрыты для dispatcher и random-class pool | Восстановлены token branches, random-list fallback, weights, orbit filter, selection и nomad ideal fallback. |
| M29 | Исправлен и закрыт для generation path | `planet+0x54/+0x55` — два байта embedded position/orbit descriptor, не suppress flags; `+0x55` доказан как `randomized`, имя `+0x54` не доказано. Все исследованные native planet-creation paths записывают туда нули; random-deposit gate находится в descriptor flags. |
| M30 | Закрыт по порядку стадий | Восстановлен random-modifier loop до attach и полный post-create порядок modifiers, deposits, blockers, anomaly, ring, flags/name, `init_effect`. |
| M31 | Закрыт | Восстановлена точная формула числа первичных попыток random deposits и inclusive верхняя граница случайной добавки. |
| M32 | Закрыт для native operator | Восстановлены фильтрация весов, обе ветви построения порога, строгая cumulative boundary и fallback. |
| M33 | Закрыт | Восстановлены DB traversal, min/max loops, score arithmetic, best-error selection, tie draws и blocker/unblocker repair. |
| M34 | Закрыт | Modifier/deposit mutations синхронно обновляют holder до следующего selector/effect. |
| M35 | Закрыт условно | Побеждает последнее определение с `default_starting_district=yes` в фактическом порядке database traversal. Сам load/merge order остаётся частью M38. |
| M36 | Закрыт для vanilla snapshot | Восстановлены empty/multiple home-list policy, arkship branch, порядок native on-actions и полный script startup pipeline. Script/global RNG остаётся входом interpreter layer, а не неизвестной ветвью native generator. |
| M37 | Закрыт для planet/deposit/district allocators | Восстановлены handle layout, grow/free/reuse/generation wrap, pre-allocation rejects, fallbacks и редкий deferred-free путь deposit. |
| M38 | Закрыт как external input boundary | Content loader/merge не является частью генератора. Воспроизводимый режим принимает уже разрешённый ordered catalog/manifest с применёнными duplicate, `replace_path`, DLC и mod rules; текущий vanilla snapshot зафиксирован в §17/§20. |
| M39 | Закрыт после triangulator edge vector | Восстановлены edge filtering/order, оба shuffles, cadence, boundary skip, caps и endpoint assignment. |
| M40 | Исправлен и разделён | Procedural nebulae — native early phase; gateways — generic initializer; wormholes — post-generator script `game_start.31`. |

## 2. Native PRNG: состояние и следующий результат

Native wrapper расположен в `0x141A7BAD0–0x141A7BBD7`. Та же арифметическая последовательность встроена в несколько других функций.

### 2.1 Состояние

**N:** минимальное синхронизированное состояние содержит два беззнаковых 32-битных слова:

1. `seedBase` — базовое слово последовательности;
2. `counter` — номер следующего draw.

Загрузчик состояния `0x140A95CD0–0x140A95CEE` читает эти значения из объекта по смещениям `+0x20` и `+0x24`, копирует их в активные globals и зеркальные диагностические поля. При выдаче значения меняется только `counter`; `seedBase` остаётся постоянным.

Преобразование явного seed в локальное состояние генератора закрыто в разделе 10. Синхронизированный глобальный RNG и локальный RNG генерации — разные состояния; смешивать порядок их полей нельзя.

### 2.2 Один draw

Все промежуточные операции этого пункта выполняются в 32 битах с отбрасыванием переполнения.

1. Умножить старый `counter` на `0x4AD685B3`.
2. Вычесть произведение из `seedBase`.
3. Смешать слово XOR-операцией с самим собой, сдвинутым вправо на 8 бит.
4. Прибавить `0x68E31DA4`.
5. Смешать слово XOR-операцией с самим собой, сдвинутым влево на 8 бит.
6. Умножить на `0x1B56C4E9`.
7. Снова выполнить XOR со сдвигом вправо на 8 бит.
8. Умножить на `0x92D68CA2`.
9. Ещё раз выполнить XOR со сдвигом вправо на 8 бит.
10. Очистить старший бит. Возвращаемое значение лежит в диапазоне от 0 до `2^31−1`.
11. Увеличить `counter` на единицу с 32-битным переполнением.

Контрольные векторы, записанные как `seedBase, oldCounter → result, newCounter`:

| seedBase | oldCounter | result | newCounter |
|---:|---:|---:|---:|
| `0x00000000` | `0x00000000` | `0x6FBE2FBA` | `0x00000001` |
| `0x12345678` | `0x00000001` | `0x12DE7D1D` | `0x00000002` |
| `0x89ABCDEF` | `0x10203040` | `0x40AD9A83` | `0x10203041` |
| `0xFFFFFFFF` | `0xFFFFFFFF` | `0x450383EF` | `0x00000000` |

Параметр source line/tag, передаваемый wrapper’у, используется диагностикой и логированием. Он не входит в арифметику результата.

## 3. Fixed-point range evaluator

Native функция: `0x140A0AA20–0x140A0AC03`. Она вызывается initializer executor’ом для орбитальных и размерных числовых полей.

Вход range object состоит из двух знаковых 64-битных fixed-point значений:

1. `base`;
2. `span`.

Сам evaluator не обращается к PRNG. Его callers получают draw заранее и передают его третьим аргументом. Поэтому draw уже потрачен даже тогда, когда `span` равен нулю.

Алгоритм:

1. Если `span` равен нулю, вернуть `base`.
2. Иначе взять остаток переданного draw по модулю 1000. Для штатного PRNG draw всегда неотрицателен.
3. Умножить `span` на этот остаток.
4. Разделить результат на 1000 с усечением к нулю.
5. Прибавить полученную величину к `base`.

Для положительного `span` функция выбирает одну из 1000 равномерных fixed-point ступеней и не достигает точки `base + span`. Например, при `base = 1 000 000`, `span = 400 000` и draw `123 456 789` остаток равен 789, а результат равен `1 315 600`.

Отрицательный `span` использует то же знаковое деление с усечением к нулю. Контрольный пример, различающий усечение к нулю и округление вниз: при `base = 1 000 000`, `span = −400 001` и том же draw произведение после применения остатка равно `−315 600 789`; после деления на 1000 вклад равен `−315 600`, итог — `684 400`.

Поддерживаемый numeric domain требует, чтобы `span×k` и итоговая сумма были представимы в signed `int64`; все vanilla generation ranges это выполняют. Native содержит overflow-safe arithmetic branches, но произвольные custom значения за пределами этого домена не входят в контракт реплики и должны отклоняться как invalid input.

Half-open правило относится именно к уже скомпилированному fixed range object. Field-specific integer ranges `count/size/distance` компилируются и sampled по отдельному inclusive contract §15.3. Для остальных script-конструкций resolved manifest обязан передавать фактические `{base,span}`, а не просить generator заново угадывать parser semantics. Это граница входного формата, а не неизвестная ветвь генератора.

## 4. Точная формула числа random deposits

Native функция: `0x140CCFE00–0x140CD00F8`. Caller передаёт размер планеты в fixed-point масштабе 100 000 и булев признак выбора colonizable набора defines.

Для выбранного набора defines используются четыре значения:

- fixed base;
- random base;
- fixed-from-size coefficient;
- random-from-size coefficient.

Алгоритм:

1. Умножить fixed-point размер на fixed-from-size coefficient.
2. Разделить произведение на `100 000 × 100 000` с усечением к нулю.
3. Прибавить fixed base. Это обязательная часть результата.
4. Аналогично вычислить усечённый вклад random-from-size.
5. Прибавить random base. Полученная величина является верхней границей случайной добавки.
6. Если верхняя граница меньше либо равна нулю, случайный draw не выполнять и добавить ноль.
7. Если она положительна, выполнить ровно один PRNG draw и взять остаток по модулю `верхняя граница + 1`.
8. Сложить обязательную и случайную части.

Следовательно, случайная добавка имеет inclusive диапазон от нуля до вычисленной верхней границы. Для обычной colonizable планеты размера 20 с vanilla defines обязательная часть равна 9, верхняя случайная добавка равна 6, а число попыток лежит от 9 до 15 включительно.

Эта функция определяет только число первичных попыток selector’а. Следующие min/max repair passes `0x140CD035A–0x140CD1027` описаны в разделе 15.5.

## 5. Точный weighted-index operator

Native функция: `0x141B50630–0x141B5072A`. Входы: число элементов, ordered array 64-битных weights, 31-битный random draw и fallback index.

### 5.1 Сумма и пустой набор

1. Обойти weights в исходном порядке.
2. В сумму включать только строго положительные значения.
3. Нулевые и отрицательные значения не получают интервала выбора.
4. Если итоговая сумма равна нулю, вернуть fallback index.

В deposit selector fallback равен `−1`, после чего caller получает sentinel/null definition.

### 5.2 Обычная ветвь

Если сумма не превышает `INT_MAX`, порог равен остатку random draw по модулю суммы.

Затем native снова обходит weights в исходном порядке, прибавляет только положительные значения и выбирает первый индекс, для которого накопленная сумма **строго больше** порога. Поэтому интервалы имеют обычную форму: первый вес покрывает пороги от нуля до `weight−1`, следующий начинается ровно с предыдущей cumulative sum.

### 5.3 Ветка большой суммы

Если сумма больше `INT_MAX`, прямой modulo не используется. Native:

1. вычисляет целую величину `scaledTotal` как нижнюю целую часть `total × 1000 / 2^31`;
2. умножает `scaledTotal` на 31-битный draw;
3. делит произведение на 1000 с целочисленным усечением;
4. использует результат как порог для того же строгого cumulative comparison.

Эта ветвь является масштабированным отображением 31-битного draw на большую сумму. Из-за двух усечений она не тождественна математически идеальному `floor(total × draw / 2^31)` и может не покрывать небольшой хвост суммы.

Если обход по какой-либо причине не находит cumulative sum строго больше порога, функция возвращает fallback index.

## 6. Planet initializer: фактический gate random deposits

Связанная цепочка:

- planet descriptor executor: `0x140870DC0–0x140871E66`;
- создание planet occurrence: вызов `0x140E37110` на `0x140871787`;
- attach к system/host: вызов `0x1408637A0` на `0x1408717A5`;
- post-create content helper: `0x140E0B770–0x140E0C166`, вызванный на `0x14087199B`;
- `RandomizeDeposits_1`: вызов на `0x140E0BCAC`.

**N:** для planet из solar-system initializer порядок этой части таков:

1. разрешить class и size;
2. создать planet occurrence;
3. внутри factory выполнить цикл случайных modifiers `0x140E0B700`: один **глобальный** RNG draw на итерацию, selector `0x140D390A0`, add через `0x140E0B4E0`, sentinel завершает цикл;
4. завершить factory updates и присоединить planet к system или host planet;
5. получить orbit angle и войти в post-create helper с planet descriptor;
6. обработать descriptor modifiers: bit 5 удаляет ранее созданные random modifiers, иначе explicit list добавляется поверх них;
7. проверить bit 4: при очищенном бите вызвать `RandomizeDeposits_1`, при установленном полностью пропустить random deposits;
8. при установленном bit 3 выполнить blocker conversion;
9. назначить anomaly: bit 2 выбирает global null/default, иначе используется valid `descriptor+0x168`;
10. обработать ring: bit 6 означает explicit режим, bit 0 хранит значение; без bit 6 и без уже установленного planet flag bit 9 тратится один global draw modulo `100000` и сравнивается с class chance `+0x380`;
11. применить explicit flags и name/entity data;
12. последним выполнить planet `init_effect`; перед dispatch он расходует два global RNG draws.

Это исправляет прежнюю гипотезу о двух байтах `planet+0x54/+0x55`. В factory paths `0x1408B5840` и `0x1408B6030` эти байты копируются из временной структуры, где оба значения инициализируются нулём, после чего `RandomizeDeposits_1` всё равно вызывается. Следовательно, эти байты нельзя использовать как suppress flags deposits/modifiers/blockers без нового независимого доказательства.

Прямая parser token → flags цепочка теперь замкнута через общий token registry и parser switch `0x1405060E0–0x140506B99`:

| Script token | Token ID | Действие parser’а для значения `none` |
|---|---:|---|
| `deposit_blockers` | `0x3288` | установить descriptor bit 3 |
| `modifiers` | `0x32ED` | установить descriptor bit 5, если explicit modifier list ещё пуст |
| `deposits` | `0x35CB` | установить descriptor bit 4 |

Значение `none` идентифицируется parser enum `0x0165`. Это соответствие имеет уровень **N**.

Runtime использует эти биты в следующем порядке:

1. Bit 5 проверяется первым. Если он установлен, вызывается `0x140E0B560`, который проходит текущую коллекцию planet modifiers и удаляет подходящие occurrences; если bit 5 очищен, descriptor explicit modifier list обходится через `0x140E0B4E0`.
2. Bit 4 проверяется затем. Если он очищен, вызывается `RandomizeDeposits_1`; если установлен, вызов полностью пропускается.
3. Bit 3 проверяется после random deposits. Он соответствует `deposit_blockers=none`: native проходит созданные occurrences, находит blockers по type/category property `+0x49` и для каждого вызывает `0x1406AC830`, который weighted-select’ит подходящий non-blocker swap type и мутирует тот же occurrence. Поэтому randomization сначала может создать blocker, а этот gate сразу превращает его в обычный deposit; object ID сохраняется.

Таким образом, attach предшествует deposits и explicit post-create content, но случайные modifiers создаются ещё внутри factory до attach. Это уточнение имеет приоритет над более ранней сокращённой формулировкой.

## 7. Несколько default starting districts

Native post-load проход `0x1404933C0–0x140493412` линейно обходит district definitions в фактическом database order. Для каждого definition с флагом `default_starting_district=yes` он без проверки предыдущего значения записывает указатель в единственный слот `districtDatabase+0xA0`.

Следствие:

1. ноль подходящих definitions оставляет слот в его исходном состоянии;
2. одно подходящее definition становится общим default;
3. несколько подходящих definitions не вызывают отдельный priority selector и не дают first-wins;
4. побеждает последнее подходящее definition в фактическом порядке обхода базы.

`M35` тем самым закрыт на уровне выбора. Чтобы предсказать победителя для произвольного набора mods, всё ещё нужен `M38`: точный loader/merge order и duplicate-ID semantics.

## 8. Что всё ещё необходимо для первого полного native-identical этапа

Основной этап `specifiedSeed → candidate coordinates → ordinary system objects`, Lloyd relaxation, Triangle candidate graph, pruning и partition traversal теперь описан алгоритмически. Для numeric-compatible результата достаточно портировать fixed/trig formulas, использовать Triangle 1.6 SINGLE с указанными switches и соблюдать staged float32 centroid math.

Оставшиеся границы native-identical результата являются входными и средовыми: resolved content/script catalog (`M38`), global RNG state для random content и pruning, точный embedded Triangle tie/memory-pool behavior, compiler FP mode и scripted evaluator/effects. Object-manager failure/free-list semantics для planet/deposit/district закрыты в разделе 21. Это не скрытые формулы основного генератора, но без перечисленных inputs нельзя обещать byte-identical save.

## 9. Проверяемые контрольные точки

Новые минимальные проверки для будущей реализации:

1. Прогнать четыре PRNG-вектора из раздела 2 без обращения к world-seed parser.
2. Проверить, что fixed range с нулевым span возвращает base, хотя caller уже потратил draw.
3. Для fixed range с положительным span проверить точки при остатках 0 и 999; значение `base + span` не должно возникнуть.
4. Для weights `[5, 0, −3, 2]` проверить интервалы выбора первого и четвёртого элементов и отсутствие интервалов у второго и третьего.
5. Для total больше `INT_MAX` отдельно проверить масштабированную ветвь, не заменяя её modulo.
6. Для colonizable planet size 20 проверить диапазон 9–15 и ровно один draw на случайную часть.
7. Для initializer descriptor проверить, что установленный bit 4 пропускает `RandomizeDeposits_1`, а очищенный bit вызывает его после attach planet.
8. Для synthetic district catalog с двумя default definitions проверить last-in-database traversal, отдельно от loader order.
9. Для явного seed проверить начальное состояние и первый draw по таблице 10.1; сравнивать слова как unsigned `uint32`.

## 10. Явный seed и начальное состояние генератора галактики

### 10.1 Публичный вход воспроизводимой реализации

**N:** native entry `0x14098D250` принимает знаковый 32-битный `specifiedSeed`. Для независимой воспроизводимой реализации следует допускать только диапазон `0..2^31−1` и делать это значение обязательным, даже если vanilla lobby не показывает поле seed.

Определим `Mix32(x)` как ту же последовательность 32-битных операций, что используется внутри `NextRandom`, но без финального очищения старшего бита:

1. `x = x xor (x >> 8)`;
2. `x = x + 0x68E31DA4`;
3. `x = x xor (x << 8)`;
4. `x = x * 0x1B56C4E9`;
5. `x = x xor (x >> 8)`;
6. `x = x * 0x92D68CA2`;
7. `x = x xor (x >> 8)`.

Каждая операция выполняется над `uint32` с wrap modulo `2^32`; правые сдвиги логические.

Для явного `specifiedSeed = p` начальное локальное состояние равно:

- `counter = Mix32(0x5EA6BA9F − p × 0x4AD685B3)`;
- `seedBase = 0`.

В памяти этого локального объекта порядок слов — `{counter, seedBase}`. Это противоположно смысловому порядку, которым ранее описывался save/global state. Следующий draw затем вычисляется формулой раздела 2 из `seedBase − counter × 0x4AD685B3`, после чего `counter` увеличивается на единицу.

Доказанный участок преобразования — `0x14098D35F–0x14098D39A`; потребление локальной пары видно в `0x14098D7EA–0x14098D838`.

Контрольные векторы (`p`, начальный `counter`, первый 31-битный draw):

| `p` | `counter` hex | `counter` unsigned | first draw hex | first draw |
|---:|---:|---:|---:|---:|
| 0 | `0xEDE419C6` | 3991148998 | `0x4BAE8956` | 1269729622 |
| 1 | `0xE5F10AFA` | 3857779450 | `0x65AF1864` | 1705973860 |
| 123456789 | `0x52268A8E` | 1378257550 | `0x3E3059B9` | 1043356089 |
| 2147483647 | `0x3A430740` | 977471296 | `0x49605B98` | 1231051672 |

### 10.2 Автоматически выбранный native seed

Если native entry получает отрицательное значение, он один раз вызывает глобальный `NextRandom`, подставляет этот 31-битный результат вместо `p` и выполняет то же преобразование. Vanilla new-game flow до вызова entry строит положительный внутренний seed ещё одним детерминированным каскадом:

1. `g = GlobalNextRandom()`;
2. `h1 = Mix32(0x5EA6BA9F − g × 0x4AD685B3)`;
3. `p = Mix32(0 − h1 × 0x4AD685B3) & 0x7FFFFFFF`;
4. entry снова вычисляет локальный `counter` из `p` по формуле 10.1.

Автоматический путь расположен в `0x140262C07–0x140262CC2`. Его результат зависит от глобального RNG в момент начала партии, поэтому для внешнего генератора обязательным входом должен быть уже полученный `p`, а не попытка воспроизвести скрытую историю UI.

### 10.3 Save/global state

Serializer `0x140A95CF0` записывает два 32-битных поля с script-токенами `seed` (`0x3FB9`) и `count` (`0x3FBA`). Parser `0x140A95D70` восстанавливает их, а post-load callback `0x140A95CD0` копирует в live и mirror globals. Здесь смысловой порядок — `{seedBase, counter}`.

Штатные параметры запуска `rng_seed=` и `rng_pos=` позволяют задать глобальные слова напрямую. Они полезны для верификации native поведения, но не нужны API независимой реализации, если глобальная пара передаётся обычными входными данными.

### 10.4 Граница точности

Явный `p` полностью закрывает начальное состояние локального генератора `0x14098D250`. Поздние функции pruning гиперлиний `0x140885000` и `0x140885580` каждая получают seed локальной перестановки отдельным вызовом глобального RNG. Поэтому режим точного совпадения рёбер обязан дополнительно принять одно из двух:

- глобальные `seedBase/counter` в согласованной точке и полный ledger глобальных draws до pruning;
- два уже вычисленных 31-битных результата, которыми инициализируются перестановки двух pruning passes.

Без этих данных совпадают диапазоны, формулы числа рёбер и статистика, но не гарантируется идентичный набор удалённых рёбер.

## 11. Плотность гиперлиний: lobby → настройки → pruning

### 11.1 Script и runtime-поля

**S/N:** `common/gamesetup_settings/gamesetup_settings.txt` выставляет в lobby настройку `num_hyperlanes`. Её token ID — `0x3BCD`; `num_hyperlanes_default` — `0x3BCF`; `max_hyperlane_distance` — `0x3944`.

Setup-scenario parser `0x140510AC0–0x140511038` хранит:

- `scenario+0x138`: minimum множителя;
- `scenario+0x140`: span, так что maximum равен `minimum + span`;
- `scenario+0x148`: default multiplier;
- `scenario+0x150`: `max_hyperlane_distance`.

Множитель — знаковое fixed-point значение масштаба `100000`; следовательно `0.5 = 50000`, `1.0 = 100000`, `3.0 = 300000`. Все пять vanilla size templates задают диапазон `0.5..3.0`, default `1.0` и `max_hyperlane_distance = 50`.

Текущий выбор хранится в `settings+0x90`. Constructor `0x14050C7C0` сначала ставит `1.0`, затем копирует default выбранного scenario. Scenario-apply helper `0x14050F740` ограничивает значение включительно диапазоном `[minimum, minimum+span]`. Settings validator `0x14050F830` принимает значение только в этом диапазоне; при ошибке пишет diagnostic `Invalid Hyperlane multiplier for current template in settings.txt` и сохраняет предыдущее/default значение.

Функции `0x140885000` и `0x140885580` читают именно `settings+0x90` через текущий game object. Дополнительного скрытого преобразования lobby value нет.

### 11.2 Spatial cutoff

В master `0x14098D4E0` значение `scenario+0x150` передаётся через `0x14097BC70` в topology wrapper `0x140886020`. Для двух систем wrapper соединяет пару напрямую и вычисляет целочисленное расстояние. Для трёх и более систем spatial candidate set строится exact процедурой раздела 19. Предел расстояния затем передаётся обоим pruning passes: в `0x140885000` через `r9`, в `0x140885580` пятым аргументом.

### 11.3 Первый pruning pass

Пусть:

- `N` — число рёбер во входном candidate list (`graph+0x14`);
- `h` — выбранный lobby multiplier в масштабе `100000`;
- `p = 15000` — vanilla `HYPERLANE_GEN_REMOVE_PERC = 0.15`.

Сначала `0x140885000` проходит все candidate edges. Каждое ребро со stored length строго больше `max_hyperlane_distance` сразу заносится в deletion set и помечается `−1`. Пусть `L` — число таких рёбер.

Затем функция вычисляет целевое суммарное число удалений:

`R = trunc_toward_zero(N × p / h)`.

Для валидных положительных vanilla значений это обычный `floor`. Примеры:

- `h=0.5`: `R=floor(0.30N)`;
- `h=1.0`: `R=floor(0.15N)`;
- `h=3.0`: `R=floor(0.05N)`.

Helper `0x140884C30` получает запрос `R−L`. При неположительном запросе он ничего не добавляет; иначе случайно без возвращения выбирает до `R−L` подходящих оставшихся candidate edges и помечает их удалёнными. Таким образом, после первого pass число удалений стремится к `max(L,R)`, но может быть меньше этой цели сверх обязательных `L`, если дополнительных подходящих рёбер недостаточно. Первый seed перестановки получается одним глобальным `NextRandom`, после чего pass использует локальное состояние и не расходует глобальный RNG на каждый кандидат.

Значение `h=0` невалидно для vanilla templates; его native арифметику не следует включать в поддерживаемый контракт.

### 11.4 Второй pruning/repair pass

Vanilla defines:

- `HYPERLANE_GEN_REMOVE_TOO_MANY_EDGES = 4`;
- `HYPERLANE_GEN_REMOVED_MAX_DIST = 20`.

`0x140885580` вычисляет degree threshold `T`:

- если `h <= 100000`, `T = 4`;
- если `h > 100000`, `T = trunc_toward_zero((4×h + 50000)/100000)` для положительных vanilla inputs, то есть округлённое к ближайшему `4h`.

После этого `0x1408871A0` применяет threshold к candidate graph, а `0x140887350` получает предел альтернативного пути `20`. Перед основным обходом функция один раз вызывает глобальный `NextRandom` (source tag `0x220`), преобразует результат через `Mix32` и случайно без возвращения обходит индексы рёбер. При удалении обновляются степени обоих концов.

В основном обходе edge является кандидатом на удаление, если выполняется хотя бы одно условие:

1. stored length ребра строго больше scenario `max_hyperlane_distance`; это безусловная ветвь удаления-кандидата;
2. степени обоих концов выше записанных для них minimum degree и выбранная плотность `h` строго меньше scenario maximum.

Полная механика обоих helpers, сортировки, invalidation и финальной materialization приведена в разделе 14. В штатном simple edge set defensive helper не удаляет дополнительных рёбер, поскольку direct adjacency даёт путь длиной один hop.

## 12. Дополнительные закрытые формулы

### 12.1 M16: число кластеров

В `0x140980782–0x1409807CC` scenario хранит method в `+0xC0`, value в `+0xC4`, optional cap в `+0xC8`, число империй `E` в `+0x80`.

- method `0` (`one_every_x_empire`): `count = (E−1+value)/value`, то есть `ceil(E/value)` для валидных `E>=0`, `value>0`;
- method `1` (`constant`): `count = value`;
- иной enum оставляет предварительный `count=0`.

Если cap не равен `−1`, затем применяется `count=min(count,cap)`. Последний шаг всегда `count=max(count,1)`. RNG не расходуется. `value=0` при method 0 приводит к native integer-division fault и считается невалидным mod input.

Все четыре поля и промежуточный numerator — signed `int32`; native `(E−1)+value` имеет low-32 wrap, затем выполняется `cdq/idiv`. Безопасный переносимый контракт должен требовать `E>=0`, `value>0` для method 0 и отклонять overflow numerator. Итоговый `int32` только после clamp sign-extends в `int64` для последующего fixed умножения. Parser defaults при отсутствии `method/value/max` не доказаны, поэтому resolved scenario обязан передать все три поля; cap sentinel равен `−1`.

Контрольные векторы `(method,E,value,cap → count)`:

- `(0,10,3,−1 → 4)`;
- `(0,10,3,2 → 2)`;
- `(0,0,3,−1 → 1)`;
- `(1,99,7,5 → 5)`;
- `(1,99,0,−1 → 1)`.

### 12.2 M20: scaled spawn chance initializer'а

Evaluator находится в `0x140508F60–0x1405090EF`. Поля initializer:

- `+0x3C0`: `spawn_chance`;
- `+0x3C4`: `scaled_spawn_chance`;
- `+0x3C8`: `scaled_spawn_settings`.

Если `scaled_spawn_chance <= 0`, базой служит `spawn_chance`. Иначе:

`base = round_half_away_from_zero(scaled_spawn_chance × totalSystems / 100)`.

Затем evaluator получает fixed-point factor `scaled_spawn_settings` либо `100000`, если настройка отсутствует, и возвращает:

`chance = round_half_away_from_zero(base × factor / 100000)`.

Clamp внутри evaluator отсутствует. Caller `0x14097FC10–0x14097FC26` берёт 31-битный draw modulo `100` и принимает candidate при `draw < chance`. Поэтому `chance<=0` никогда не проходит, а `chance>=100` всегда проходит. Для положительных значений первое округление эквивалентно `floor((product+50)/100)`.

`spawn_chance`, `scaled_spawn_chance` и `totalSystems` загружаются как signed `int32`. Native сначала выполняет low-32 signed product `scaled_spawn_chance×totalSystems`, затем **sign-extends этот уже усечённый int32** в `int64`; только после этого выполняются fixed преобразование, bias эквивалентный `±50` и signed rounding. Поэтому bias не является int32 addition к исходному product. Factor — signed fixed `int64`; final product и `chance` — signed `int64`. Для поддерживаемого контракта следует checked-проверять int32 product и int64 `base×factor`, а native overflow за пределами этого домена не обещать.

Контрольные векторы `(spawn,scaled,N,factor → chance)`:

- `(17,0,100,100000 → 17)`;
- `(0,25,2,50000 → 1)`: оба положительных half cases округляются от нуля;
- `(0,25,3,150000 → 2)`;
- `(5,0,10,−50000 → −3)`: `−2.5` округляется от нуля;
- `(0,10,4,100000 → 0)`.

## 13. Правила чтения пакета отчётов и текущая полнота

Этот addendum имеет приоритет над противоречащими фразами в трёх исходных отчётах. В частности:

1. Planet attach происходит до post-create helper и random deposits; старое описание обратного порядка заменено разделом 6 этого addendum.
2. `M31` и `M32` закрыты разделами 4 и 5; старые пометки «неизвестно» больше не действуют.
3. Старый адрес `M20` относился к progress interpolation; действующий evaluator и формула находятся в разделе 12.2.
4. `M29` как гипотеза о suppress flags снят; gate random deposits определяется descriptor bit 4, а `planet+0x55` является embedded `randomized` byte.
5. `M01` закрыт и для explicit, и для automatic native path. Внешняя реализация всё равно должна получить итоговый 31-битный `specifiedSeed`, потому что vanilla генерирует его из скрытого global RNG state.

Практические уровни воспроизведения:

- **structural approximate**: текущих отчётов достаточно, если координаты, tie-breaking и random ledger разрешено реализовать совместимыми эвристиками;
- **native-compatible density**: достаточно раздела 11, чтобы верно читать lobby value, строить те же target counts/thresholds и получать близкую структуру; конкретные удалённые рёбра могут отличаться;
- **supplied-seed deterministic**: разделы 2, 10 и 16 дают точный local draw ledger и shape formulas; fixed trig/relaxation могут дать малое численное расхождение координат;
- **native-identical**: дополнительно требуются global RNG state/results для automatic seed, pruning и random script content; exact Triangle 1.6 SINGLE behavior/FP mode; effect interpreter для описанных startup/wormhole scripts; и фактический resolved content catalog. Object-manager semantics для planet/deposit/district уже описаны в разделе 21.

Внутренних неизвестных control-flow ветвей, блокирующих воспроизведение исследованной vanilla генерации, больше нет. Остались конкретные **входные данные и runtime dependencies**, которые нельзя вывести из одной настройки плотности гиперлиний: итоговый `specifiedSeed` либо global state/ledger до его автоматического создания; выбранные setup scenario и shape; `num_hyperlanes`; два global results pruning; global/script RNG state для effects; immutable resolved ordered content manifest; exact Triangle 1.6 SINGLE behavior и согласованный FP mode. Плотность гиперлиний определяет только цели удаления и degree threshold и математически не кодирует seed. Любое отсутствие этих входов должно обозначаться как supplied-seed, supplied-topology или statistical mode, а не native-identical.

## 14. Полный pruning гиперлиний после построения candidate pairs

### 14.1 Представление и сортировка

Edge record занимает 24 байта:

- `+0x00`: endpoint `a`, signed `int32`; `−1` означает invalid/removed;
- `+0x04`: endpoint `b`, signed `int32`;
- `+0x08`: signed `int64` length/weight в fixed-point масштабе `100000`;
- `+0x10`: byte flag, переносимый вместе с record.

Vector хранит data pointer в `+0x08` и count в `+0x14`. Перед обоими pruning passes `0x140886F40` сортирует records по возрастанию лексикографического ключа `(weight,a,b)`. При `count<=32` используется stable insertion sort `0x140887B70`; сдвиг происходит только при строгом `>`. При большем count используется `0x140887CC0` с тем же полным ключом. Порядок полностью одинаковых duplicate records для большой ветви не доказан; для normal simple edge set ключ задаёт однозначный порядок.

### 14.2 Selector `0x140884C30`

Функция строит pool физических индексов от переданного `start` до конца vector, включая только records с `a!=-1`. Затем выполняет `k=min(requested,poolSize)` выборов:

1. `j = LocalNextRandom() mod poolSize`;
2. выбрать `index=pool[j]`;
3. заменить `pool[j]` последним активным элементом pool и уменьшить `poolSize`;
4. скопировать record в temporary removal vector;
5. записать `source[index].a=-1`.

Это partial Fisher–Yates без возвращения. Исходный pool упорядочен по возрастающим физическим индексам, тратится ровно один локальный draw на каждое реально выбранное ребро.

### 14.3 Degree helper `0x1408871A0`

Входы: destination removal vector, source edges, adjacency blocks, degree array, число вершин `V`, threshold. Adjacency имеет stride `V−1`.

Source сканируется вперёд, то есть после сортировки от меньшего `(weight,a,b)` к большему. Для каждого valid `(a,b)`:

1. если `degree[a] < threshold` или `degree[b] < threshold`, оставить record;
2. иначе перенести record в destination и invalidировать source;
3. в adjacency list обоих концов линейно найти противоположный endpoint, заменить найденный entry последним активным, последний слот записать `−1`;
4. уменьшить обе степени.

Равенство threshold допускает удаление: проверка использует `>=`.

### 14.4 Defensive helper `0x140887350`

Source сканируется в обратном порядке. Для каждого valid `(s,t,w)` helper создаёт `pred[V]=−1`, `dist[V]=−1`, ставит `dist[t]=0` и запускает FIFO worklist из `t`. Adjacency entry содержит `low int32 neighbor`, `high int32 edgeCost`. Relaxation выполняется только при первом пути или строго меньшей distance; равный путь сохраняет первого predecessor. При каждом улучшении vertex снова ставится в FIFO. Как только впервые relaxed `s`, поиск заканчивается.

Record переносится в discarded suffix и source invalidируется, если пути нет, либо если `w<=max_hyperlane_distance` и число predecessor hops от `s` до `t` строго больше `HYPERLANE_GEN_REMOVED_MAX_DIST=20`. При переносе пара временно добавляется в adjacency и обе степени увеличиваются для последующих обратных итераций. Это состояние проверки, а не возврат ребра в final graph.

В штатном pipeline adjacency непосредственно перед helper строится из всех valid source edges, а `0x1408871A0` удаляет adjacency entry синхронно с source record. Поэтому у каждого оставшегося record есть прямой `t→s` entry: `pred[s]=t`, hop count равен 1, helper ничего не переносит. В normal simple edge set это defensive consistency path; на итог pruning он не влияет.

### 14.5 Первый pass `0x140885000`

Точный порядок:

1. отсортировать vector `A`;
2. вычислить `R=floor(A.count×15000/h)`;
3. прямым проходом перенести в temporary vector и invalidировать все records с `weight>max_hyperlane_distance`; пусть их число `L`;
4. получить один global draw с tag `0x1BC`, преобразовать его в seed локального selector’а;
5. вызвать `0x140884C30` для `R−L` дополнительных records; неположительный запрос ничего не делает;
6. построить adjacency и degree из оставшихся valid `A`;
7. если `h<scenarioMaximum`, применить `0x1408871A0` с degree threshold из раздела 11.4;
8. вызвать defensive `0x140887350`;
9. материализовать как реальные гиперлинии все valid records, оставшиеся в `A`.

### 14.6 Второй pass `0x140885580`

Точный порядок:

1. отсортировать vector `B`;
2. получить один global draw с tag `0x220`, применить `Mix32` и создать local RNG;
3. построить случайную перестановку всех физических индексов `B` тем же swap-with-last способом;
4. для каждого valid record в этом порядке перенести его в temporary vector, invalidировать и уменьшить степени, если `((degree[a]>targetDegree[a] && degree[b]>targetDegree[b] && h<scenarioMaximum) || weight>max_hyperlane_distance)`;
5. построить combined adjacency из surviving `B` и второго input vector `A`;
6. если `h<scenarioMaximum`, применить `0x1408871A0` к `B`;
7. вызвать defensive `0x140887350`;
8. материализовать все valid records, оставшиеся в `B`.

Итоговый набор гиперлиний — survivors `A`, затем survivors `B` в порядке materialization caller’а. После раздела 19 topology воспроизводим при наличии двух global seed draws и входного group mapping.

## 15. Solar-system initializer и планеты

### 15.1 Выбор initializer и use-count

`0x14050ADE0` строит eligible array, исключая только отрицательные weights; нулевые сохраняются. Selector всегда тратит один local RNG draw. Этот же draw задаёт fallback `draw mod eligibleCount` для `0x141B50630`:

- при положительной сумме выбирается обычный weighted index;
- при нулевой сумме равномерно выбирается fallback среди eligible entries;
- `eligibleCount=0` нарушает native invariant и доходит до integer division by zero, а не возвращает нормальный null.

После выбора caller всегда тратит второй local draw на spawn roll: `roll=draw mod 100`, успех iff `roll<chance`. Даже `chance=0` тратит draw. При неуспехе и выключенном debug bypass `0x14050B970` немедленно увеличивает use-count отвергнутого initializer, затем selection повторяется. При успехе executor `0x140870DC0` увеличивает use-count в самом начале. Следовательно failed spawn chance тоже расходует `max_instances`.

### 15.2 Batch и neighbors

`0x14097F360` сначала выполняет `0x140870DC0` для всех records текущего batch, включая object effects и system `init_effect`. Только отдельным следующим проходом вызывается `0x14097E810` для neighbor search. Найденные обязательные neighbors добавляются как новый batch. Parent effect поэтому завершён до поиска/инициализации child. Candidate с `system+0x137C` уже пропускается; этот byte выставляет executor.

Mandatory fallback `0x14097DC50` делает до `SPAWN_SYSTEM_ATTEMPTS=15` попыток. Каждая попытка до geometry check расходует три local draws:

1. signed inclusive integer distance из `base/span`;
2. два draws для 64-bit angle sample `((drawA & 0x7fffffff)<<32)|rawMixB32`, затем modulo inclusive interval.

Distance `D` — signed inclusive descriptor `+0x148/+0x14C`; angle — inclusive circular sample `+0x160/+0x168` с native period `0x9965F`, затем используется deterministic fixed sin/cos. Native строит `V=(sin×D,cos×D)` при trig scale `100000`. Затем `Rmax=abs((distance.base+distance.span)×100000)`; если integer norm `V` больше `Rmax`, vector нормализуется и clamp’ится к `Rmax`, иначе остаётся. Candidate равен `parentCoordinate+V`.

Helper `0x14097E5A0` сравнивает candidate со всеми существующими systems, кроме parent, и отвергает iff squared Euclidean distance строго меньше `SPAWN_SYSTEM_BUFFER_DISTANCE²`. Первый допустимый candidate завершает цикл. Если все 15 отвергнуты, функция пишет diagnostic и безусловно принимает последнюю sampled coordinate. `ALLOW_UNACCEPTABLE_SYSTEM_THRESHOLD` в этой функции не читается и к этому пути не относится.

### 15.3 Descriptor traversal и draw ledger

Top-level descriptors обходятся в compiled vector order `initializer+0x2C0`, count `+0x2CC`. Для каждого descriptor:

1. безусловно потратить local draw на integer `count`, даже если span нулевой; результат `base + sign(span)×(draw mod (abs(span)+1))` из `+0x0C/+0x10`;
2. для каждого instance вызвать class dispatcher; число его draws зависит от class mode;
3. безусловно потратить draw и вычислить `orbit_distance` через M04; orbit accumulator обновить до проверки class validity;
4. если class invalid/none, перейти к следующему instance без allocation и последующих draws;
5. безусловно потратить integer size draw: explicit `descriptor+0x138`, либо class default range `+0x3E0` для moon при bit 7, `+0x3E8` для planet;
6. allocate planet, затем attach;
7. если bit 1 (`home_planet`) установлен, добавить handle в context list;
8. безусловно потратить draw на M04 `orbit_angle` из `descriptor+0x150`;
9. выполнить post-create порядок раздела 6;
10. рекурсивно выполнить все moons из vector `+0x4F8/+0x504`;
11. после всех moons выполнить independent orbital-line vector `+0x510/+0x51C`; каждая line тратит один M04 draw.

`change_orbit` компилируется как class none: тратит count draw и orbit-distance draw, меняет accumulator, но не создаёт объект и не тратит size/angle/content draws. Категории moons и orbital lines выполняются в этом фиксированном порядке независимо от текстового interleaving.

`home_planet=yes` (descriptor bit 1) только добавляет каждый успешно созданный handle в caller list сразу после attach. Executor не создаёт fallback и не проверяет «ровно один». Если class даёт sentinel/none, allocation и append отсутствуют. Downstream `0x140979A30` закрывает malformed cases: при непустом list всегда тратится один local draw, включая count=1, затем выбирается `clearSign(draw) mod count` по insertion order; при пустом list пишется diagnostic и без RNG выбора берётся последний object из system planet vector. У fallback carrier отдельно может потребоваться local draw для generated name. Nomadic/mobile branch вызывает собственный arkship path и descriptor home-list не использует.

### 15.4 Random planet class

`0x140D383D0` обходит planet classes в database order. Definition включается, если:

1. class flag bit 9 очищен;
2. class bit 7 равен запрошенному colonizable flag;
3. orbit входит в class interval `+0x350..+0x358` включительно;
4. optional required flag bit 0 выполнен;
5. вычисленный weight строго положителен.

Weight берётся из star-class override table, если context valid и override неотрицателен; иначе из class default helper `0x140D35B30`. Для colonizable он дополнительно умножается на lobby habitable-world fixed multiplier с делением на `100000`. При positive total тратится один local draw, threshold=`draw mod total`; weights вычитаются в database order, выбирается первый entry, где остаток становится `<=0`. Пустой pool возвращает sentinel. Nomad-ideal branch сначала пробует random non-colonizable class, а при sentinel использует ideal class pointer.

Class dispatcher `0x140506C10–0x1405072DE` различает:

- `none` — sentinel;
- `star` — indexed class из star-class vector, out-of-range даёт sentinel;
- `ideal_planet_class` — context ideal planet; при отсутствии пробует random colonizable и затем generic random fallback;
- `random` — общий random-class helper;
- `ideal_design_class` — design ideal; nomad сначала пробует random non-colonizable;
- `random_colonizable`, `random_non_colonizable`, `random_non_ideal`, `random_asteroid` — соответствующие фильтрованные pools;
- default — сначала exact planet-class name, затем named `random_list`; последний тратит ровно один local draw modulo list count. Если не найдено ни то ни другое, возвращается sentinel и diagnostic.

Общий `random` обходит normal-class DB vector, применяет те же star/default weights, orbit interval, optional bit 0 и habitable multiplier для colonizable classes. При total zero он возвращает sentinel без draw; иначе тратит ровно один local draw. `random_non_ideal` добавочно исключает переданный ideal class pointer. `random_asteroid` обходит отдельный asteroid-class vector, требует class bit 9 и positive star/default weight, но не проверяет orbit interval, habitable multiplier или bit 0; он безусловно делает один local draw, а total zero нарушает native invariant и приводит к signed division by zero.

### 15.5 Deposit repair и наблюдаемость изменений

Repair `0x140CD035A–0x140CD0BC2` обходит district definitions в database order. Для каждого applicable district он сравнивает текущую deposit-provided capacity с `min_for_deposits_on_planet` (`+0xEE4`) и `max_for_deposits_on_planet` (`+0xEE8`).

При превышении maximum он повторно сканирует existing deposits с `use_for_min_max_adjustments` (`definition+0xA69`), оценивает вклад `0x14049B510(mode=1)` и выбирает occurrence, лучше всего уменьшающий абсолютную ошибку. Равный best score тратит один global RNG coin draw. Выбранный occurrence удаляется holder vcall `+0x40`.

При недоборе minimum он повторно сканирует deposit definitions DB, требует adjustment flag, положительный вклад `0x14049B510(mode=0)` и успешный potential `0x1404983A0`, затем выбирает best remaining error. Равенство также тратит global coin draw; выбранный deposit создаётся `0x140268CB0`.

После districts native обеспечивает `MIN_BLOCKED_DEPOSITS=1` и `MIN_UNBLOCKED_DEPOSITS=3`. Недостающий unblocked deposit получается преобразованием подходящего blocker occurrence: равный score тратит conditional global coin, затем `0x1406AC830` обязательно тратит ещё один global draw для weighted swap type. Недостающий blocker создаётся симметрично из unblocked occurrence: conditional tie coin, затем обязательный global draw и blocker-mode selector `0x14049BD90`. Occurrence мутируется, новый object ID не выделяется.

Score helper `0x14049B510` получает целые `X,Y` и deposit definition `D`. Из `D+0x50/+0x54` берутся два key; их текущие holder values `A,B` читаются из planet map. Отсутствующий key даёт 0. Если type соответствующего key имеет flag bit 5, value clamp’ится в `[0,100000]`, иначе остаётся raw.

- remove mode: `score = X×Y − trunc(((X×100000−A)×(Y×100000−B))/10000000000)`;
- add mode: `score = trunc(((X×100000+A)×(Y×100000+B))/10000000000) − X×Y`.

Деление знаковое с усечением к нулю; native overflow-safe branches дают эту математику для штатных ranges. Это закрывает последний арифметический пробел `M33`. Добавление modifier синхронно вызывает holder update до возврата; allocator deposit вызывает holder add до следующего выбора. Поэтому последующие selectors/effects видят все предыдущие изменения, что закрывает `M34`.

### 15.6 Точный global RNG ledger random content

Random modifier loop `0x140E0B700` тратит один global draw с tag `0xF9E` на каждую итерацию, включая последнюю итерацию, на которой selector возвращает sentinel. При `N` успешно добавленных random modifiers расход равен `N+1` global draws. Selector строит из draw локальные `Mix32` seeds, обходит definitions в DB order, вычисляет scripted weights и не вызывает global RNG дополнительно.

Число первичных deposit attempts из раздела 4 использует class flag bit 7 для выбора COLONY/NON_COLONY defines. Если random span положителен, расходуется один global draw с tag `0x455`; иначе draw нет. Затем каждая из `count` попыток безусловно тратит один global draw с tag `0x47A`, даже если selector возвращает sentinel. Deposit selector обходит DB order, фильтрует definition/potential/class/type, вычисляет scripted weight и делит вес уже использованной категории на `1 + categoryCount×DEPOSIT_USED_CATEGORY_WEIGHT`; переданный draw используется weighted chooser’ом без нового global draw. Это уточняет границу между local galaxy RNG и global content RNG.

Compiled deposit-definition byte `+0xA68` означает `is_null`. Для null-deposit scripted weight `W` заменяется на `fixedDiv(W,G)=W×100000/G` при `G>0`, где `G=*(*(game+0x9B0)+0xB8)` — текущая lobby Resource Abundance. Primary caller исключает null candidate, если planet class colonizable (bit 7), имеет special bit 10, либо `G==RESOURCE_ABUNDANCE_MAX`; иначе null участвует, и его выбор означает завершить попытку без создания deposit. Так Resource Abundance масштабирует вероятность пустого результата. Defines `RESOURCE_ABUNDANCE_MIN/DEFAULT/MAX` зарегистрированы по globals `0x143151568/570/578`.

## 16. Координаты систем для всех vanilla galaxy shapes

### 16.1 Подготовленный input

Master передаёт в `0x140975CF0–0x14097777F` структуру:

- `+0x00`: total target;
- `+0x04`: core count `floor(total×num_stars_core_perc)`;
- `+0x08`: outer count `total−coreCount`;
- `+0x10`: `coreRadius=fixedMul(galaxyRadius,core_radius_perc)`;
- `+0x18`: `outerSpan=galaxyRadius−coreRadius`;
- `+0x20`: galaxy radius;
- `+0x28`: number of arms;
- `+0x30/+0x38/+0x40/+0x48`: tightness, width, fuzz, separation;
- `+0x50`: one random rotation;
- `+0x58`: `stars_min_dist` descriptor;
- `+0x88/+0x90`: `galaxyRadius×ring.width` и `galaxyRadius×ring.offset`;
- `+0xD4`: ring-present flag.

Все fixed операции имеют scale `100000` и truncate toward zero. `Next64` расходует ровно два M02 draws и образует `((draw0&0x7fffffff)<<32)|uint32(draw1)`. Далее `U=Next64() mod 100000`.

Shape dispatch полностью data-driven:

- no ring, no arms: elliptical;
- ring, no arms: ring;
- no ring, arms: spiral, bar, starburst, spoked;
- ring and arms: cartwheel.

Отдельного switch по именам нет; mod shape с той же комбинацией optional blocks идёт по той же ветви.

### 16.2 Rotation и radial component

Если arms существуют, master до coordinate loop один раз тратит два draws:

`rotation = 2×fixedMul(Next64()%100000,314200)`.

Для no-arms rotation равен нулю. На каждом candidate radius строится так:

- ring: `coreRadius + ringOffset + fixedMul(ringWidth,U)`;
- без ring, пока accepted index меньше outer count: `coreRadius + fixedMul(outerSpan,U)`;
- для оставшихся core points: `fixedMul(coreRadius,U)`.

Каждая radial ветвь расходует два draws.

### 16.3 Angle и coordinates

При arms:

1. `twist=fixedMul(tightness,fixedDiv(radius,outerSpan))`, если `outerSpan!=0`;
2. `w=fixedMul(width,Next64()%100000)` — два draws;
3. `arm=NextRandom()%numArms` — один draw;
4. `f=2×fixedMul(fuzz,Next64()%100000)−fuzz` — два draws;
5. `degrees=twist+w+arm×separation+f`;
6. staged fixed conversion умножает degrees на `pi=314159`, делит на 180, добавляет rotation и нормализует modulo `628319`;
7. native fixed sin/cos helpers дают `x=fixedMul(radius,cos)`, `y=fixedMul(radius,sin)`.

Trig deterministic и не использует `libm`. Sin wrapper нормализует angle modulo `628319`; к отрицательному остатку прибавляет `628319`; при значении больше `314159` вычитает `314159` и меняет знак; при reduced angle больше `157080` заменяет его на `314160−angle`. Core polynomial выполняет ровно три fixed terms с scale `100000`: `x − x³/3! + x⁵/5!`; каждое умножение и signed division сохраняет fixed `int64` и усекается к нулю. Cosine использует тот же wrapper с phase shift `π/2`.

Итого arms candidate расходует 7 draws вместе с radius. При no-arms angle равен `fixedMul(36000000,Next64()%100000)`, затем используется та же conversion/trig цепочка; всего 4 draws вместе с radius. Rejected candidate расходует все свои draws.

### 16.4 Collision, acceptance и IDs

Candidate сравнивается со всеми уже принятыми objects в insertion order. Текущий raw candidate ещё не clamped; предыдущие accepted coordinates уже clamped при materialization. `stars_min_dist` копируется из shape без sampling и не расходует RNG.

Distance helper сохраняет точность для малых deltas. Если одновременно `abs(dx)<100000` и `abs(dy)<100000`, оба компонента fixed-умножаются на `10000000` (`100.0`) и record получает scale flag. После squared-distance helper comparator действует так: при `minDistance>=1000000` сразу сообщает collision; иначе умножает `minDistance` на integer `100`, возводит его в fixed square и сравнивает с scaled squared distance. Без scale flag он сравнивает обычный fixed squared distance с fixed square `minDistance`. Во всех branches отвергается только строгое `<`; equality разрешена.

Контроль ordinary branch при `minDistance=1000000`: для осевого `dx=999999` fixed square равен `9999980`, поэтому candidate отвергается; при `dx=1000000` square равен threshold `10000000` и candidate принимается; при `dx=1000001` получается `10000020`, candidate также принимается. Контроль scaled branch при `minDistance=50000`: `dx=49999,dy=0` масштабируется до `4999900`, squared distance `249990000`, threshold после `minDistance×100` равен `250000000`, поэтому reject; `dx=50000` даёт equality и принимается.

Retry budget начинается с 10000 и заново ставится после каждого принятого object. На collision native сначала проверяет `budget<1`: если false, уменьшает budget и повторяет тот же accepted index; если true, пишет ошибку и прекращает создание последующих systems. Поэтому разрешены ровно 10000 rejected candidates с повтором, а failure наступает на 10001-м collision. После 10000 rejects следующий, 10001-й sampled candidate ещё может быть принят. Minimum distance не уменьшается.

При принятии система немедленно alloc/register’ится через manager `0x140267FF0`, получает raw `x/y`, затем каждая координата clamped в `[−50000000,50000000]`; arm index либо `−1` записывается в object. Boundary vectors: `−50000001→−50000000`, обе границы сохраняются без изменения, `50000001→50000000`. Pointer добавляется в generation order. Поэтому ordinary system ID выделяется при acceptance, а не после построения всего массива координат.

System allocator `0x140267FF0` использует manager `world+0x930+0x1078`; system object имеет размер `0x13B0`, handle находится в `system+0x08`. Manager layout free-head/capacity/slot-table и stop flags `+0x34/+0x35` совпадает с common handle scheme §21. Chunk ensure `0x14030B850` выделяет 1024 objects и явно записывает `FFFFFFFF` в handle words до первого использования; затем allocator вызывает constructor `0x1408606B0` и записывает новый handle.

Allocator берёт free slot `i`, адресует chunk как `chunks[i>>10]+(i&0x3FF)×0x13B0`, снимает следующий free index, увеличивает active count, повышает generation byte handle на `0x01000000` и формирует handle `i|generation`. Поэтому для fresh manager с ascending free chain первый allocation slot `i` преобразует старший byte `FF→00` и даёт handle ровно `i`; например fresh free-head `0` даёт handles `00000000,00000001,…`. Фактические начальные free-head/capacity и уже занятые slots являются входным состоянием world manager. Rejected candidates allocator не вызывают. System-specific free entry и дополнительные side effects удаления отдельно не квалифицированы и не нужны ordinary generation phase, где принятые systems не освобождаются. После relaxation `0x140254880` инвалидирует/перестраивает world spatial/index caches без дополнительных RNG draws.

`capacity` в manager `+0x20` является числом slot records, то есть exclusive верхней границей индекса. При пустом free-list system allocator вычисляет `target=min_unsigned(2×capacity,0x00FFFFFE)` и отказывает, если `target==capacity`; затем передаёт target generic grow `0x1402A28C0`. Следовательно, native-supported maximum capacity равен `0x00FFFFFE`, а максимальный выделяемый slot index — `0x00FFFFFD`; `0x00FFFFFE` недостижим как allocated slot в этом allocator, `0x00FFFFFF` служит sentinel. Snapshot с capacity больше `0x00FFFFFE` объявляется invalid input.

`highestActive` находится в manager `+0x24` и трактуется как `uint32` со специальным empty sentinel `0xFFFFFFFF`. После allocation slot `i` он обновляется ровно если прежнее значение равно sentinel либо unsigned `i>highestActive`; иначе сохраняется. Для fresh snapshot начальное значение должно быть `0xFFFFFFFF`: allocation slot 0 даёт `highestActive=0`, затем slot 1 — `1`; allocation повторно используемого slot 0 при active slot 1 сохраняет `1`. Signed `max(-1,i)` здесь неверен.

#### 16.4.1 Материализация accepted system

Для каждого accepted candidate native выполняет строго такую последовательность:

1. вызвать `0x140267FF0(world,nameInput,creationContext)`; до этого шага candidate не имеет ID;
2. allocator проверить оба stop flags, при необходимости расширить slot table generic grow’ом, обеспечить object chunk, снять free-head, зарегистрировать `slot.ptr`, увеличить active count, обновить dirty/highest-active;
3. вызвать system constructor `0x1408606B0`, затем записать вычисленный handle в raw allocated object `system+0x08`;
4. **заново разрешить** full handle через global system manager `*0x143114E30`: проверить manager, range low-24 index, ненулевой `slot.ptr` и полное совпадение `slot.ptr->handle`; при любой ошибке заменить returned pointer на global sentinel `*0x1431146E8`;
5. только в resolved либо sentinel pointer скопировать `nameInput` в `+0x438`, записать `creationContext` в `+0x430`, поставить `world+0x9C8=1` и `world+0x950=returned->handle`;
6. caller без дополнительной проверки записывает raw candidate в position descriptor returned pointer `+0x408`: `x` в `+0x410`, `y` в `+0x418`, sentinel `FFFFFFFF` в `+0x428`;
7. component-wise clamp обеих координат в `[−50000000,50000000]`, затем всегда вызвать position helper `0x14034A500`; на normal resolver-success constructor текущего allocation оставил `randomized=0`, поэтому helper идёт по no-op ветви. На sentinel path helper читает уже существующие `descriptor+0x25` и `+0x18` global sentinel; эти значения текущим allocation не инициализировались. Helper не регистрирует spatial object: его true-branch только вычисляет descriptor `+0x18`;
8. если arm index неотрицателен, записать его в returned pointer `+0x5F0`; на normal resolver-success constructor заранее установил `−1`, поэтому no-arms branch сохраняет `−1`. На sentinel path no-arms branch ничего не пишет и сохраняет прежнее значение `globalSentinel+0x5F0`;
9. append returned pointer в локальный generation vector; порядок vector равен порядку acceptance и дальнейшему ordinary-site order relaxation.

Constructor raw newly allocated object также доказывает только нужные этому этапу defaults: handle `−1`; position coordinates/auxiliary fields zero с position sentinel `−1`; `system+0x430` временно получает global default до overwrite allocator’ом; name/container fields создаются пустыми; arm index `system+0x5F0=−1`. Эти defaults не распространяются на возвращённый global sentinel. Остальные многочисленные system fields принадлежат более поздним initializer/star/planet стадиям и не должны заполняться догадками.

Allocation и перечисленные registration/append operations RNG не расходуют. До финального `0x140254880` доказаны manager slot registration и world dirty/last-returned-handle fields; построение world index/cache выполняется final refresh. Семантика `nameInput`, `creationContext`, global sentinel system и самих runtime cache structures остаётся внешним world-layer контрактом, а не частью pure coordinate generator.

Allocator snapshot должен передаваться явно как `{capacity,freeHead,highestActive,activeCount,stop34,stop35,slot records,chunks/object handle words,globalResolverManager,globalSentinel}`. Для поддерживаемого generation adapter требуются оба stop flags zero, `1<=capacity<=0x00FFFFFE`, корректный acyclic free chain, `freeHead<capacity`, slot indices `0x00FFFFFE/0x00FFFFFF` не используются, нужный chunk storage доступен, а global resolver после allocation обязан вернуть тот же raw `slot.ptr` по полному handle. `highestActive` равен `0xFFFFFFFF` только при empty manager, иначе это максимальный active slot и он меньше capacity. Handles трактуются как `uint32`; generation `80..FF` не означает invalid.

Контрольные allocator vectors при supplied fresh snapshot с достаточной capacity, ascending chain `0→1→…` и каждым object handle `FFFFFFFF`:

- allocations 0 и 1 дают handles `00000000`, `00000001`;
- allocation slot 1023 адресует `chunks[0]+1023×0x13B0` и даёт `000003FF`;
- следующий slot 1024 адресует `chunks[1]+0×0x13B0` и даёт `00000400`;
- reuse-vector для slot 7 с сохранённым object handle `12FFFFFF` даёт generation `13` и handle `13000007`; это arithmetic vector, system-specific free path не заявляется;
- `stop34!=0`, `stop35!=0`, исчерпанная максимальная capacity, grow failure или chunk-storage failure происходят **до slot pop**, не расходуют active slot/generation и возвращают sentinel path. Native после этого способен продолжить mutations над global sentinel object; безопасная реплика должна считать такой результат typed pre-allocation failure и остановить materialization;
- отдельный post-allocation resolver failure возникает после slot registration, constructor и записи handle: real slot/ID уже consumed, но returned pointer заменяется sentinel, который caller затем мутирует и append’ит. Это hidden/leaked allocated-system path. Safe adapter обязан проверять resolver invariant заранее либо завершаться typed fatal error с уже изменённым allocator snapshot; его нельзя ошибочно отчётить как no-ID failure;
- corrupt snapshot (`freeHead>=capacity`, цикл/несогласованные links, отсутствующий slot record) является invalid input, а не native-compatible failure case.

Если slot-table grow уже успел расширить capacity/free chain, а последующий chunk ensure завершился неудачно, metadata grow остаётся committed, хотя ID не выдан. Это редкое OOM-состояние совпадает с общей схемой §21; для обычной генерации рекомендуется deterministic preflight всех требуемых chunks и отказ до первого accepted object.

### 16.5 Relaxation

После coordinate loop создаётся relaxation object вместимостью `acceptedCount+GALAXY_GENERATION_CENTER_DUMMY_POINTS`. Vanilla добавляет 256 dummy points и делает одну итерацию.

Ordinary sites добавляются первыми, строго в accepted/generation order. Для каждой raw fixed coordinate `r` input adapter выполняет не алгебраическое `float(r/1000)`, а точную последовательность:

1. `scaled=FM(r,10000000)`, то есть fixed multiplication на `100.0`;
2. `q=trunc_toward_zero(scaled/100000)` как signed integer;
3. `point=float32(q)` через `cvtdq2ps`.

В штатном диапазоне это `float32(trunc_toward_zero(r/1000))`, но порядок важен для отрицательных дробных тысячных и overflow policy. Векторы `(raw fixed → q, float bits)`: `999→0/00000000`, `1000→1/3F800000`, `−999→0/00000000`, `−1000→−1/BF800000`, `2500000→2500/451C4000`, `−2500000→−2500/C51C4000`, `50000000→50000/47435000`.

Dummy angle step:

`step=trunc(62831800000/(dummyCount×100000))`.

Для `i=0..dummyCount−1` берётся `angle=i×step`. Native вычисляет fixed `sin(angle)` и `cos(angle)`, образует внутреннюю пару `(cos,sin)`, component-wise последовательно применяет `FM` с `coreRadius`, `90000` (`0.9`) и `10000000` (`100.0`), затем к каждому component применяет тот же signed integer conversion `q=trunc(component/100000)` и `cvtdq2ps`. В `pointlist` записывается `(x=qSin,y=qCos)`: порядок компонент при записи меняется относительно внутренней пары. Это дискретизированная окружность радиуса около `0.9×coreRadius`; native polynomial amplitude `100453` сохраняется.

Контроль для `dummyCount=256`, `coreRadius=2500000`: `step=2454`.

| i | angle | sin/cos fixed | final fixed перед `/100000` (`x/y`) | point float (`x/y`) | float32 bits (`x/y`) |
|---:|---:|---:|---:|---:|---:|
| 0 | 0 | `0 / 100453` | `0 / 226019200` | `0 / 2260` | `00000000 / 450D4000` |
| 1 | 2454 | `2454 / 100375` | `5521500 / 225843700` | `55 / 2258` | `425C0000 / 450D2000` |
| 255 | 625770 | `−2549 / 100372` | `−5735200 / 225837000` | `−57 / 2258` | `C2640000 / 450D2000` |

`dummyCount=0` добавляет zero dummy points и не выполняет division/loop. Negative count может уменьшить allocation capacity, хотя loop будет пропущен, и объявляется invalid input. Поддерживаемый adapter требует `dummyCount>=0`, `relaxIterations>=0`, представимый `acceptedCount+dummyCount`, а также representable signed fixed intermediates. `coreRadius` передаётся явно из resolved setup/shape input; generator не подставляет default. Vanilla metadata snapshot задаёт `GALAXY_GENERATION_CENTER_DUMMY_POINTS=256` и `GALAXY_GENERATION_RELAX_ITERATIONS=1`; другие snapshots обязаны передать оба defines явно. Zero/negative/custom core radius не нормализуется этим adapter; для native-compatible vanilla режима требуется положительное валидное значение из scenario.

Kernel `0x141B4BCF0(holder,minX,maxX,minY,maxY)` вызывается `GALAXY_GENERATION_RELAX_ITERATIONS` раз. Static initializer задаёт lower global `−50000000`; преобразование caller’а даёт точный bounding square `[-50000,50000]²`. Bounds inclusive.

Holder содержит три последовательных `triangulateio`: input `+0x00`, primal output `+0xA8`, Voronoi output `+0x150`. Embedded Triangle собран с `REAL=float`. До первой итерации вызывается `triangulate("pczAevnQ",...)`; после каждой итерации старые output arrays освобождаются/обнуляются и вызывается `triangulate("pczAevn",...)`.

Для каждого site `i` в input order native сканирует primal edges в их returned order. Если edge `(u,v)` incident к `i`, соответствующий Voronoi edge `(a,b)` добавляется как `(a,b)` при `u==i` или `(b,a)` при `v==i`. Любой отрицательный endpoint означает unbounded ray: site остаётся без движения. Directed segments сшиваются in-place: для позиции `k` выбирается первый дальнейший segment, чей start равен предыдущему end, и swap’ится на `k`; отсутствующая связь также оставляет site без движения. Требуется больше одного segment. Отдельной проверки `last.end==first.start` нет: корректное замыкание предполагается от Triangle output.

Все Voronoi vertices, включая closing endpoint, должны попадать в inclusive bbox. Polygon centroid получает первый start и затем ends всех segments, кроме duplicate closing endpoint. Tessellation во время site loop неизменна, поэтому обновления логически simultaneous.

Centroid разбивает polygon fan’ом от `P0`. Для каждого triangle все coordinate sub/add/mul и cross выполняются staged в float32; `cross` и произведения `(x0+xi+xj)×cross`, `(y0+yi+yj)×cross` переводятся в double и суммируются в triangle order. Итоговые coordinates — float32 `sumX/(3×sumCross)` и `sumY/(3×sumCross)`. Zero-area guard отсутствует. После всех sites tessellation строится заново.

После kernel каждая ordinary-site coordinate преобразуется `cvttss2si` в signed `int32 t`, затем становится fixed `t×100000` и проходит `FixedDiv(...,10000000)`, то есть деление на fixed `100.0`. Для представимых штатных значений итог в точности равен `t×1000`. Вызов `0x140308ED0` является overflow-safe component-wise fixed division, а не clamp helper. Дополнительного post-relax clamp нет; диапазон `±50000000` следует из inclusive kernel bbox `±50000` до conversion. Затем координаты записываются в systems и `0x140254880` инвалидирует/перестраивает spatial/index caches без RNG.

Post-relax conversion vectors: float `1.999→1000`, `−1.999→−1000`, `0.999→0`, `−0.999→0`, `50000.0→50000000`, `−50000.0→−50000000`. Fraction отбрасывается до fixed division, поэтому алгебраическая замена на `trunc(float×1000)` неверна для отрицательных и положительных дробных значений.

Контроль centroid: triangle `(0,0),(6,0),(0,3)` даёт `(2,1)`, signed area `9`; обратная ориентация даёт тот же centroid и `−9`; square `(-1,-1),(1,-1),(1,1),(-1,1)` даёт `(0,0)`. Две точки приводят к `(NaN,NaN)` из `0/0`.

Чувствительный numeric vector, который обнаруживает ошибочную замену промежуточных `float32` на `double`: для выпуклого polygon `(-784,-889),(842,157),(-400,621),(-580,322)` native staged pipeline возвращает centroid `(-147.66104125976562,-33.094181060791016)` и signed area `1105290`. Биты результатов: `C313A93A C2046071 4986EC50`. Если те же входные binary32 значения считать полностью в double и округлить только итог, Y становится `-33.09418487548828`, bits `C2046072`.

Итак, M06–M11 закрыты алгоритмически. Numeric-compatible reproduction требует Triangle с float32 `REAL`, указанными switches, edge order и staged centroid math. Полная bit identity дополнительно зависит от совпадения embedded Triangle implementation/tie-breaks и floating-point mode.

### 16.6 Точный порядок fixed trig и контрольные векторы

Определим `FM(a,b)=trunc_toward_zero((a×b)/100000)` с wide intermediate. Порядок нельзя алгебраически сворачивать, потому что каждый `FM` усекает отдельно.

Для folded `x∈[0,157080]` polynomial `P` выполняет ровно три итерации:

1. начать `power=x`, `denominator=1`, `acc=0`, sign positive;
2. прибавить/вычесть `trunc(power/denominator)`;
3. `tmp=FM(x,power)`, затем `power=FM(x,tmp)`;
4. denominator последовательно принимает `6`, затем `120`; sign меняется;
5. вернуть `x−x³/6+x⁵/120` с указанными промежуточными truncations.

`Sin(a)` берёт signed remainder modulo `628319`, отрицательному добавляет период; при `r>314159` вычитает `314159` и меняет знак результата; при `r>157080` заменяет `r` на `314160−r`; сравнения строгие. `Cos(a)=Sin(a+157080)`. Degrees переводятся строго в два шага: `q=FM(degrees,314159)`, затем `radians=trunc(q/180)`.

Контрольные angle vectors:

| angle fixed | sin fixed | cos fixed |
|---:|---:|---:|
| 0 | 0 | 100453 |
| 157080 | 100453 | −1 |
| 314159 | 1 | −100453 |
| −157080 | −100453 | 0 |

Амплитуда `100453` является результатом native трёхчленного polynomial, а не ошибкой нормализации.

Candidate A, no-arms: `coreRadius=2500000`, `outerSpan=7500000`, draws radial `(11,12)`, angle `(13,14)`. Получаются `Ur=40268`, `Ua=74862`, radius `5520100`, degrees `26950320`, radians `470371`, cos `−868`, sin `−100432`, итог `x=−47914`, `y=−5543946`.

Candidate B, arms: `coreRadius=2000000`, `outerSpan=8000000`, `numArms=2`, tightness `110000`, width `8000000`, fuzz `2500000`, separation `18000000`. Rotation draws `(8,9)` дают `Urot=38377`, rotation `241160`. Candidate draws: radial `(1,2)`, width `(3,4)`, arm `5`, fuzz `(6,7)`. Получаются `Ur=67298`, radius `7383840`, ratio `92298`, twist `101527`, `Uw=1892`, width contribution `151360`, arm `1`, `Uf=3783`, fuzz contribution `−2310850`, degrees `15942037`, final angle `519400`, cos `46321`, sin `−88661`, `x=3420268`, `y=−6546586`.

## 17. Конкретные внешние данные, без которых результат не определён

Алгоритм не должен читать «vanilla вообще». Он должен получать один immutable resolved input snapshot. Минимально в него входят:

1. setup scenario со всеми числовыми полями и fixed scales;
2. galaxy-shape record, включая optional ring/arms blocks и `stars_min_dist`;
3. ordered solar-system initializer catalog с уже скомпилированными descriptor vectors;
4. ordered star-class и planet-class catalogs со всеми override weights;
5. ordered deposit, deposit-category, district и planet-modifier catalogs;
6. resolved scripted triggers/weights/effects либо детерминированный evaluator той же версии;
7. все используемые defines;
8. duplicate resolution, `replace_path` и DLC gates, уже применённые до передачи catalog генератору.

Для каждого ordered catalog snapshot обязан хранить stable definition ID, source identity/hash и фактический traversal index. Это необходимо, потому что несколько native selectors разрешают равенства именно database order, а default district использует last matching definition.

На исследованной машине launcher state в `dlc_load.json` содержит `disabled_dlcs=[]` и два enabled mods. Один меняет только localisation, второй — fonts/interface; они не содержат generation catalogs. Это позволяет считать текущий наблюдаемый профиль близким к base+всем доступным DLC для исследуемых каталогов, но не доказывает native merge semantics и не заменяет resolved snapshot. Данные профиля изменяемы пользователем, поэтому они не должны быть скрытой частью seed.

## 18. Partitions, clusters и поздние region stages

### 18.1 Partition sampling и traversal

Partition spec:

- `+0x00/+0x04`: `max_systems/min_systems`;
- `+0x08/+0x0C`: `max_bridges/min_bridges`;
- `+0x10`: method enum.

Для каждого ещё не назначенного root:

1. если `maxSystems<=minSystems`, target равен minimum; иначе потратить один local draw и взять `minSystems + draw mod (maxSystems−minSystems)`, maximum exclusive;
2. вычислить `scaledMaxBridges=round_half_away_from_zero(maxBridges×h/100000)`;
3. если scaled maximum не больше minimum, bridges равен minimum; иначе потратить один local draw и взять `minBridges + draw mod (scaledMaxBridges−minBridges+1)`, maximum inclusive;
4. method 0 вызывает `0x140A384C0`, method 1 — `0x140A38790`; оба создают partition record и используют traversal `0x140A36870`.

Traversal перечисляет adjacency в stored order. Candidate допускается, если partition unassigned либо уже текущий, visited byte не равен 1, `system+0x5F0` совпадает с partition discriminator, а squared distance до current не больше `max_hyperlane_distance²`; equality допускается.

Пока candidates и remaining положительны, тратится один local draw, выбирается `draw mod activeCount`, selected удаляется swap-with-last. Candidate, уже принадлежащий partition, всё равно расходует draw, но не уменьшает remaining. Новый candidate назначается, добавляется в partition list и уменьшает remaining.

После исчерпания one-hop pool recursive repair идёт round-robin не по исходному adjacency, а по полной candidate-перестановке, оставшейся после random swap-with-last removals. Candidate помечается visited, рекурсивный вызов получает `remaining=1`. Failure возвращает 1 и удаляет candidate swap-with-last; success возвращает 0, уменьшает caller remaining и оставляет frontier candidate, поэтому он может быть выбран снова для углубления. Следующая позиция `(position+1) mod activeCount`. Возвращается незаполненный remaining.

Method 0 сохраняет visited array на весь bulk pass и сразу вызывает grow(seed,primary).

Method 1 сначала создаёт все seed partitions без bulk growth, запоминает их creation indices, затем делает fair sweeps в обратном порядке indices. Partition, достигший target, удаляется. Иначе visited bytes очищаются, first system помечается и traversal вызывается с remaining 1. Failure удаляет partition из дальнейших sweeps; success оставляет до следующего sweep. Поэтому каждый partition растёт максимум на одну system за sweep.

### 18.2 Start и resource clusters

Start-cluster constructor `0x140980750` не расходует RNG. Для count из M16:

`step=fixedDiv(628319,count)`, `angle_i=i×step`,

`center_i=(fixedMul(clusterDistance,sin(angle_i+157080)), fixedMul(clusterDistance,−sin(angle_i)))`.

Regions называются `start_cluster_i`, получают scenario cluster radius и регистрируются в двух containers.

Resource clusters `0x140981260` создают `N` угловых sector regions. Сначала строится permutation names `[0..N−1]` forward Fisher–Yates: для каждого `i`, включая последний, тратится один local draw, `j=i+draw mod (N−i)`, затем swap. Ещё два local draws образуют `Next64()%100000` и задают angular phase. `step=fixedDiv(628319,N)`; sector `i` получает boundaries `phase+i×step` и `phase+(i+1)×step`, а последний замыкается к `phase`. Геометрический порядок остаётся `i`, permutation меняет suffix `resource_cluster_<perm[i]>`. Общий расход равен `N+2` local draws.

### 18.3 Исправление M18

`0x140988D10` вызывается до partitions/start-initializer stages. Он обходит scenario entries `+0x210/+0x21C`, использует flags `+0xD4/+0xF8` и строит matrix `candidateCount×eligibleEntry`.

Для каждого candidate безусловно тратится один local draw; через `Mix32` он становится seed отдельного sub-RNG, на котором считаются scripted weights всех eligible entries. Затем каждая matrix row сканирует columns по возрастанию. Начальные `bestScore=0`, `bestColumn=−1`; замена только при signed `int64 score>bestScore`. Нулевые/отрицательные значения не выбираются, равный positive score сохраняет меньший column. Выбранный column отображается через candidate-index vector и записывается; этот column во всех rows заменяется на signed `−100000`.

Финальный fallback тратит один local draw только для каждого всё ещё не назначенного row. Он начинает с `draw mod systemCount` и циклически linear-probe’ит до index, которого нет в уже записанном prefix. Probe дополнительных draws не тратит и сохраняет уникальность, пока свободная system существует.

Это assignment procedure, а не distance relaxation империй. Solver и ties закрыты; внешними входами остаются ordered rows/candidates и signed score matrix, полученная scripted eligibility/weight evaluator’ом.

### 18.4 Resource-region assignment

`0x14097C280` сначала превращает systems в generation order в points, вызывает native Triangle и обходит returned edge array без перестановки. Invalid endpoints пропускаются. Edge принимается, только если squared radius его midpoint строго между minimum и maximum squared radii систем; границы исключены. Records сохраняют triangulator order и оба generation-vector indices.

`0x14097CAC0` один раз тратит local draw и создаёт isolated RNG для scripted evaluation. Затем в DB order перечисляет `common/resource_regions`, исключает уже присутствующие в scenario vector, применяет potential и вычисляет:

- `maxUses=max(1,max(def.count))`;
- `desiredSlots=max(eligibleDefinitionCount,floor(edgeCount/(2×maxUses)))`;
- `cadence=max(1,floor(edgeCount/max(1,desiredSlots)))`.

Slot indices дополняются sentinel `−1`, затем slots и definitions shuffle’ятся одной private substream. Каждый shuffle делает два Fisher–Yates passes: forward `i=0..n−2`, swap с `i+draw mod (n−i)`; затем backward `i=n−1..1`, swap с `draw mod (i+1)`. Расход — `2×(n−1)` draws для массива длины больше 1. После одного внешнего seed draw M39 расходует `2×(S−1)+2×(D−1)` private draws, не считая RNG scripted potential.

Ordered edge pairs обходятся последовательно. При `i>0` и `i mod cadence==0` native переключает slot, сбрасывает inserted count и **полностью пропускает boundary edge**. Иначе definition marker `uint16` из `+0x48` применяется сначала к endpoint A, затем B, если validity virtual predicate успешен и marker отсутствует. Поле `+0x4C` — script `amount`. Cap проверяется до пары endpoints, а не между ними, поэтому значение `cap−1` может стать `cap+1`, если оба endpoints добавлены.

`0x14097D540` является precursor-civilization assignment, а не вторым resource-region repair. Поэтому его нельзя включать в M39. Для M39 после получения exact Triangle edge vector все последующие order, shuffles, draw counts и assignments закрыты.

### 18.5 Исправленная карта nebulae, gateways и wormholes

Старое предположение, что random-master tail отдельно создаёт nebulae/wormholes/gateways, неверно. В частности, `0x140987180` создаёт fallen empires, `0x14097FA50` выполняет ordinary/misc initializers, `0x14097CAC0` назначает resource regions, а `0x14097D540` назначает precursor civilizations.

Procedural nebulae создаются раньше: `0x14098DD2F → 0x140977780`. Inputs: desired count `config+0x68`, radius `+0x70`, minimum center separation `+0x78`.

Для каждой nebula:

1. поставить budget 10000;
2. потратить один local draw и выбрать `systems[draw mod systemCount]` в manager generation order;
3. сравнить center с существующими nebula centers в creation order; reject при distance строго меньше minimum, equality принять;
4. после сравнения, но до materialization проверить `budget<1`; если true, остановить всю фазу независимо от результата сравнения; иначе reject уменьшает budget и повторяет тот же ordinal;
5. name picker тратит один **global** draw с tag `0x34C`, выбирает entry modulo pool count, удаляет swap-with-last; пустой pool даёт diagnostic и пустое имя;
6. создать nebula с center, radius и sentinel `−1`;
7. пройти systems в generation order, пропустить уже attached; attach iff squared distance до center строго меньше radius squared, equality исключена;
8. после acceptance budget следующей nebula снова 10000.

Следствие для draw ledger: первые 10000 candidates ещё могут быть приняты. После 10000 consecutive rejects budget становится нулём, native всё равно тратит local draw и вычисляет/сравнивает 10001-й candidate, затем выходит до acceptance даже если этот candidate геометрически допустим.

Ruined gateways идут через generic initializer `abandoned_gateways_01`: `max_instances=15`, `scaled_spawn_chance=5`, `scaled_spawn_settings=num_gateways`. Его scripted weights запрещают слишком близкий gateway; `init_effect` создаёт `gateway_ruined`. Отдельного native gateway-tail нет.

Vanilla wormhole pairs создаёт post-generator event `game_start.31`. Он читает `num_wormhole_pairs` с `scale_by=5`, выбирает две systems через script `random_system` с exclusions, требует distance не меньше 500, создаёт natural wormhole на random position в каждой и связывает их. Exact wormhole reproduction требует script-engine order/RNG и effect semantics; это внешний post-native stage, а не скрытая ветвь random-master.

## 19. M13: построение исходных candidate edges

`0x141B832F0` — statically embedded Jonathan Shewchuk Triangle 1.6 `triangulate()`. Build эквивалентен `TRILIBRARY + SINGLE + NO_TIMER`; `REAL` — IEEE binary32. Вызов использует `triangulate("pczAevnQ",input,output,voronoi)`, три `triangulateio` records по `0xA8` байт. Это default divide-and-conquer Delaunay с Dwyer alternating cuts и robust predicates.

### 19.1 Input и private determinism

При `N<2` adjacency не создаётся. При `N=2` wrapper не создаёт candidate-edge record: он вычисляет по original fixed coordinates `length=floor_sqrt(dx²+dy²)` без округления к целой map unit, затем вставляет по одному adjacency record `{neighborHandle,length,flag=0}` в каждую систему, с противоположным handle. После двух вызовов insertion helper сразу выходит; Triangle, annulus, density pruning и max-distance pruning не вызываются. При `N>=3` ordered input index совпадает с index caller system-handle array. Каждая original fixed coordinate преобразуется:

`xTri=float32(trunc_toward_zero(xRaw/100000))`, аналогично `y`.

Topology тем самым строится по целым map units в binary32; дальнейшие filters и lengths используют original fixed coordinates.

Triangle сбрасывает private seed в 1 на каждый вызов. Pivot generator:

`state=(state×1366+150889) mod 714025`,

`result=state/(floor(714025/choices)+1)`.

Game RNG не расходуется. Duplicate quantized points и cocircular ties должны обрабатываться exact Triangle 1.6. Survivor duplicate не обязан иметь минимальный input index.

### 19.2 Edge order

Triangle `writeedges()` идёт по triangle memory pool в allocation order, затем orientations 0,1,2. Edge emitted, если противоположный triangle exterior dummy либо pointer текущего triangle меньше pointer соседа. Записываются `org,dest` без canonicalization. Поэтому endpoint direction и order детерминированы exact Triangle, но не лексикографичны; generic Delaunay library их не воспроизводит.

### 19.3 Native post-filter

По original fixed positions `P[i]` заранее вычисляются:

`r2[i]=FM(Px,Px)+FM(Py,Py)`, `rMin=min(r2)`, `rMax=max(r2)`.

Для каждого emitted `(a,b)` midpoint считается через fixed division на `2.0`, затем `midR2=FM(midX,midX)+FM(midY,midY)`. Edge сохраняется iff:

`rMin<=0` либо одновременно `rMin<midR2` и `midR2<rMax`.

Обе границы строгие. Для survivor:

1. `dx=P[b].x−P[a].x`, `dy=P[b].y−P[a].y`;
2. `lengthRaw=floor_sqrt(dx²+dy²)` native integer Newton loop;
3. `storedLength=trunc(lengthRaw/100000)×100000`.

Record имеет `{int32 a,int32 b,int64 storedLength,byte flag=0}`. Затем caller stable-split’ит records: одинаковый mapped group ID endpoints идёт в vector A, различный — в B; относительный Triangle order сохраняется.

### 19.4 Контрольные vectors

Операторные векторы annulus, не зависящие от Triangle edge order:

- lower equality: points `(100000,100000),(100000,-100000),(-100000,0)` дают `rMin=100000`, `rMax=200000`; midpoint edge `0–1` равен `(100000,0)`, его `midR2=100000`, поэтому edge отвергается;
- strict interior: при points `(200000,100000),(200000,-100000),(100000,0),(300000,0)` получаются `rMin=100000`, `rMax=900000`; midpoint edge `0–1` имеет `midR2=400000`, поэтому edge сохраняется;
- upper equality: при endpoints `(300000,0),(300000,0)` и дополнительной point `(100000,0)` получаются `rMax=900000` и `midR2=900000`, поэтому edge отвергается;
- signed midpoint: для X endpoints `−100001` и `0` fixed division возвращает `−50000`, а не `−50001`; сначала складываются raw coordinates, затем применяется division by fixed `2.0` с truncation toward zero.

Stored-length vectors для ветки `N>=3`: raw separation `99999` даёт `storedLength=0`; `100000` даёт `100000`; diagonal `dx=dy=1000000` даёт integer root `1414213`, после whole-unit floor — `1400000`. Для special case `N=2` последний diagonal сохранил бы raw `length=1414213`, потому что эта ветвь не выполняет whole-unit floor.

При наличии `(0,0)` annulus filter отключён, потому что `rMin=0`:

- points `(0,0),(10,0),(0,10)` → edges `(0,1),(1,2),(2,0)`, lengths `10,14,10`;
- `(0,0),(10,0),(10,10),(0,10)` → `(3,0),(0,1),(1,3),(1,2),(2,3)`, lengths `10,10,14,10,10`;
- reverse square `(0,10),(10,10),(10,0),(0,0)` → `(0,3),(3,2),(2,0),(2,1),(1,0)`;
- duplicate `(0,0),(10,0),(0,10),(0,0)` → `(3,1),(1,2),(2,3)`.

Exact contract требует Triangle 1.6 с указанным SINGLE build, memory-pool/writeedges order и robust predicates. Generic triangulator допустим только для structural approximation.

## 20. M36: homeworld и vanilla startup pipeline

### 20.1 Выбор home object и arkship design

После initializer непустой home-list обрабатывается по insertion order. Native **всегда** тратит один local M02 draw, даже при единственном element, и берёт `(draw & 0x7fffffff) mod count`. Это modulo selection с обычным bias, а не идеальное равномерное распределение, когда count не делит `2^31`. При пустом list native пишет diagnostic, берёт последний handle native planet-vector системы и разрешает его через global planet table; invalid handle даёт global null/sentinel planet. Проверки, что system vector непуст, перед `count−1` нет, поэтому полностью пустой custom system находится вне подтверждённого безопасного контракта. Сам fallback RNG не тратит, но generated naming затем может потратить один local draw.

Mobile/nomadic country с native bit 11 идёт в отдельный arkship path и home-list не читает. Eligible ship designs фильтруются по `is_starting_arkship`; непустой vector требует ровно один local draw и выбор `(draw & 0x7fffffff) mod count`, пустой даёт diagnostic и оставляет fallback design.

### 20.2 Native/on-action order

Default starting district/colony создаётся до `0x14097A850`. Затем native вызывает named on-actions строго так:

1. `empire_init_add_technologies`;
2. `empire_init_capital_colony`;
3. diagnostic, если после capital stage pop list пуст;
4. `empire_init_create_ships`.

В исследованном resolved vanilla snapshot `empire_init_capital_colony` содержит только `game_start.12`. Его outer gate пропускает ship либо planet без `special_start_planet`. Внутри порядок фиксирован: deposits/blockers, `generate_start_buildings_and_districts`, `generate_start_pops`, `generate_home_planet_final_pass`. После outer block независимо выполняются void-dweller setup для planet с `void_dweller_habitat_1` и `home_arkship_initial_setup` для каждого ship.

`special_start_planet` полностью пропускает обычные deposits/buildings/pops/final-pass stages. Два vanilla occurrences — void habitats — затем попадают в `void_dweller_home_planet_setup`, который является альтернативным полным pipeline: carrier flags `habitat_level_2`/timed-ignore; habitat tech и research option; orbital/mining-station/neighbor conversions; собственная pop branch matrix; habitat entity; rural defaults; habitat capital, две housing, industrial/foundry choice, science district, research/unity zone; shipsets, species variants, ethics, civics, tankbound; rural placement; size 6; starting resources. Это не post-fix поверх обычного pipeline.

Внутри прошедшего outer gate generic starting-deposit generator вызывается iff `(origin_remnants OR ship OR non-Earth planet) AND NOT origin_shattered_ring AND class!=pc_ark`. Иначе Earth-planet без shattered-ring/remnants/`pc_ark` получает Earth generator и entity. Следовательно ship с class `pc_ark` пропускает generic deposits, но не stages buildings, pops и final pass; ship другого class может вызвать generic generator.

### 20.3 Script order, который нельзя переставлять

Generic deposit stage сначала удаляет все deposits. Взаимоисключающий blocker chain идёт в порядке: apocalyptic; machine servitor; environmental architects; terminator без `origin_machine`; assimilator без `origin_machine`; generic machine без `origin_machine`; unplugged; ordinary non-gestalt без `origin_machine/synthetic_fertility/cosmic_dawn`; hive без `cosmic_dawn`. После него независимо добавляются unity, lithoid, apocalyptic, life-seeded, class-specific, ocean-paradise и wilderness элементы. Если не сработала более ранняя branch, ordinary получает `d_decrepit_dwellings+2×d_failing_infrastructure`, hive — `d_collapsed_burrows+2×d_failing_infrastructure`, generic machine без `origin_machine` — `d_strip_mine+d_machine_empire_ruins`. Resource Consolidation позднее получает `d_machine_prototype_pc_machine+2×d_machine_ongoing_construction`, плюс organic slurry для assimilator. Servitor с Environmental Architects получает unsupervised settlement и random natural blocker; без этого civic — unsupervised settlement и strip mine.

Buildings/district interpreter исполняет inline blocks в таком порядке: timed-ignore setup; rural defaults `E=2,M=2,F=3`, для ark `cap=2,basic=1`; authority regular/hive/machine; соответствующий nomad authority; species lithoid/infernal/individual-machine/subspecies; shipsets; ethics spiritualist, materialist, pacifist, militarist, egalitarian, authoritarian, xenophile, xenophobe; wilderness; origins `default, forever_cruise, sacred_path, heirs_of_khan, shroud_forged, mindwarden, primal_calling, cybernetic_creed, synthetic_fertility, arc_welders, payback, broken_shackles, toxic_knights, progenitor_hive, subterranean, ocean_paradise, necrophage, shattered_ring, remnants, clone_army, machine, mechanist`; civics; tankbound; optional volcanic setup; rural placement; starting resources; validation. Все blocks выполняются текстуально, а limit делает неподходящий block no-op.

До origin modifiers regular non-nomad получает capital, две city districts, zones/factory; hive — hive capital, spawning pool либо offspring nest, warren и две hive districts; machine — machine capital, assembly, storage, две nexus districts и rural vars `E=3,M=4,F=0`. Nomad variants используют две ark-city districts; regular добавляет zones/factory, hive — gestalt buildings, machine — три buildings и те же machine rural vars. Native starting district для `pc_ark` — `district_ark_city`.

Rural placement строго идёт energy, mining, для `pc_ark` basic deck и food/harvest zone, затем farming, затем удаление temporary vars/flags. На `pc_ark` energy превращается в ark generators, basic count — в deck, mining/farming отсутствуют. `origin_default` добавляет `E+1` и `F+1`, иначе `M+1`; nomad также увеличивает ark cap/basic. `origin_forever_cruise`, выполняемый позже default, удаляет ark-city, добавляет две cruise districts и делает `E−1`.

Pop stage выполняется только после всех buildings/districts: special secondary species; mechanist; hive; servitor; shroud; machine; megacorp; broken-shackles; regular; payback; fear; cosmic; default; eager; toxic; nascent; permanent-employment; civilians; wilderness; synthetic; deep-sleep. Final pass затем применяет life-seeded cleanup, shattered-ring/origin-machine star energy, tree/lithoid/lost-colony/progenitor/default modifiers, `origin_machine` system conversion и exotic-metabolism deposit.

Arkship после общих stages создаёт/настраивает starbase, переключает graphical culture и owner start flag по ship size, создаёт commander, случайное имя и один `random_common` trait, назначает leader флоту, затем вычисляет mutually-exclusive blocker chain: servitor получает unsupervised; иначе non-gestalt — decrepit dwellings; иначе hive — collapsed burrows; generic machine не получает blocker. Пары failing infrastructure здесь нет.

Точные native selections home и arkship design используют local galaxy RNG; empty-home generated naming и другие native arkship operations могут расходовать дополнительные local draws. При входе в `empire_init_capital_colony` local state в script interpreter не передаётся: `random_list`, `random_scope`, names и traits внутри effects используют script/global RNG. Поэтому exact interpreter обязан сохранять текстовый operator order. Известные variable script draws: environmental-architect natural blocker — один draw для apocalyptic/servitor и два в general branch; volcanic class — пять `random_list` draws; arkship setup — имя leader и `random_common` trait. Другие counts зависят от активных origins/civics и resolved script.

Зафиксированный content snapshot: `game_start.txt` SHA-256 `8171B1806103AC96EB82CB5A8ECA2712232764EA7712B1AA16049724268DB528`; `00_on_actions.txt` `7693E64FBE974F37DB0C68969D10F7BDEEC585CE59CA497B5E27ECD69FF8F306`; `01_start_of_game_effects.txt` `232925D6D7269532B8E146E60AF4AD2AD33ED60B75AF16888514685A1D6C4CB5`; sorted 82-file inline bundle `D55B69B47797134A4AAAD4685E7BE004B80B3245548F7B75269751D5B87CA0EF`.

## 21. M29/M37: handles и allocator semantics

### 21.1 Общий handle contract

Planet, deposit и district используют независимые managers, но одинаковую схему: `handle=(generationByte<<24)|slotIndex`, low 24 bits — slot, high 8 — generation. `FFFFFFFF` — sentinel, slot `FFFFFF` зарезервирован. Slot table record равен `{next:uint32,prev:uint32,ptr:uint64}`. Capacity не превышает `00FFFFFE`; grow создаёт новые slots в ascending chain и добавляет её в tail free-list. Fresh object storage явно инициализирует handle word значением `FFFFFFFF`, поэтому первое выделение делает wrap generation `FF→00` и даёт handle, равный slot index.

Allocate снимает free-list head, чинит `prev` следующего slot, увеличивает active count, ставит dirty, вычисляет `generation=(oldGeneration+1) mod 256` и записывает handle. Free проверяет полный handle, вызывает destructor, уменьшает active count, очищает `slot.ptr`, сохраняет в object старшую generation вместе с low `FFFFFF` и ставит освобождённый slot в head free-list. `highestActive` пересчитывается вниз только если освобождён именно текущий highest slot; иначе значение не меняется. Поэтому reuse LIFO: первый handle slot `i` равен `i`, после free/reuse — `01000000|i`, через 256 reuse cycles generation оборачивается. Handles со старшим byte `80..FF` знаково отрицательны, но остаются валидными; проверять их как `signed>=0` нельзя.

Manager bytes `+0x34/+0x35` — allocation-stop flags: любой ненулевой byte немедленно отправляет planet/deposit/district allocation в fallback. Scalar planet/deposit free проверяет только `+0x34`, а не `+0x35`.

Scalar planet/deposit free при существующем global manager записывает `FFFFFFFF` в caller handle также для stale/invalid handle и allocation-stop branch; при null manager выходит, не меняя caller. District deferred bulk cleanup caller vector не переписывает: в нём остаётся старый handle, а очищенный object получает `generation|00FFFFFF`.

Если free-list пуст, grow slot-table/free-chain может успешно увеличить capacity до того, как отдельный chunk-storage ensure завершится ошибкой. В таком случае active slot и generation не расходуются, но enlarged capacity/free chain сохраняются; следующая allocation повторяет storage ensure без ещё одного grow. Это точная failure semantics, хотя для штатной генерации без OOM ветвь не активна.

### 21.2 Planet, deposit, district

Planet manager выделяет object `0xC90`, handle находится в `planet+0x18`. Invalid/none class, `change_orbit` и rejected descriptor allocator не вызывают. Accepted planet allocates сразу; moons затем идут depth-first. При manager/capacity/storage failure native возвращает fallback object и не расходует active slot/generation, хотя grow metadata мог уже сохраниться и post-init fallback всё равно вызывается.

Deposit object имеет размер `0x228`, handle `+0x10`. Selector sentinel отвергается до allocator. Allocation failure возвращает fallback/sentinel без active slot. Есть редкий post-allocation reject: если holder virtual add/register возвращает false, object помечается deferred-delete (`+0x220=1`), основные поля сбрасываются и caller получает fallback ID, но slot пока остаётся active. Когда deferred cleanup выполнит free, slot попадает в free-list head; следующий deposit повторно использует его уже с generation+1. Невидимая rejected попытка поэтому способна изменить последующие deposit IDs.

District object имеет размер `0x1B8`, handle `+0x08`. Eligibility reject не расходует ID; если district type уже существует, увеличивается occurrence count без allocation. Только новый type вызывает allocator. Failure не создаёт настоящий handle, хотя патологический fallback может дальше мутироваться и добавляться в list. Deferred free включается sign bit поля `district+0x1B4`, после чего действует общий LIFO contract.

Байты `planet+0x54/+0x55` принадлежат embedded position/orbit descriptor (`planet+0x20`, descriptor offsets `+0x34/+0x35`). Serializer и token registry доказывают имя `+0x55`: `randomized`. Соседний token `randomizable` не доказывает имя `+0x54`, поэтому оно оставлено opaque. Ordinary, synthetic и другие исследованные creation paths явно записывают оба нуля до `RandomizeDeposits`; generic cloning сохраняет их. Для воспроизведения генерации правило точное: новым planet писать `0,0` и не использовать эти байты как gate modifiers/deposits/blockers.

## 22. Конкретные данные, которые должен предоставить воспроизводящий запуск

Эта таблица отделяет отсутствующие **данные конкретной партии** от уже восстановленного алгоритма.

| Данные | Точный формат | Зачем нужны | Что делать, если отсутствуют |
|---|---|---|---|
| `specifiedSeed` | `uint31 p` | Инициализирует весь local galaxy RNG по §10.1 | Для новой реплики задать явно. Для существующей партии получить из pre-generation trace/global state; из `num_hyperlanes` он не выводится. |
| Global state до automatic seed | два `uint32 {seedBase,counter}` плюс ledger позиции | Позволяет вычислить скрытый `p` по §10.2 | Если известен `p`, этот ранний state не нужен. |
| Pruning seeds | два 31-битных результата global RNG | Определяют конкретные удаляемые hyperlane edges | Без них сохранить target counts/thresholds, но пометить edge identity как statistical-compatible. |
| Setup/shape | exact resolved scenario record, shape record и все значения lobby fixed scale `100000` | Размер, число систем, geometry, clusters, distance limits | Не угадывать по названию galaxy size; передавать resolved record. |
| Content snapshot | ordered resolved definitions, stable IDs, source/hash, traversal index, DLC/mod gates | Initializers, classes, weights, deposits, districts, modifiers и script order | Использовать immutable manifest §17; unordered map недостаточен. |
| Empire/start context | origins, authorities, civics, species, ethics, country flags, ideal classes/designs | Выбирает branches §20 | Без него planet generation возможна, но стартовая колония не определена. |
| Script/global RNG | state либо результаты в textual operator order | Startup effects, names, traits, wormholes и другой script random | Разрешён supplied-results mode; local galaxy RNG сюда не подставлять. |
| Triangle runtime | Triangle 1.6, `TRILIBRARY+SINGLE+NO_TIMER`, switches из §§16/19, совместимый FP mode | Relaxation topology, degeneracy choices и edge order | Generic Delaunay даёт только structural approximation. |
| System allocator snapshot | поля manager и slot/chunk state из §16.4.1 | Нужен только для совпадения конкретных system handles и редких failure states | Для новой standalone реплики использовать проверенный fresh ascending snapshot; для повтора live world передать фактическое состояние. |

Для приемлемой точности без доступа к скрытому global state достаточно выбрать и зафиксировать собственные `p`, два pruning seeds и script seed, затем использовать все exact local formulas и immutable vanilla snapshot. Такой запуск будет полностью детерминирован и статистически совпадёт с native distribution, но не обязан повторить неизвестную уже сыгранную галактику. Для повтора конкретной существующей галактики обязательны её `p` и pruning results/state; одной плотности гиперлиний и числа звёзд недостаточно, потому что множеству seeds соответствуют одинаковые lobby settings и одинаковые target counts.

