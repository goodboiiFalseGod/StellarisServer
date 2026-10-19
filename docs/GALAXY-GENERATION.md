# Первый этап генерации галактики 4.5.2

## Доработка от master — 16 октября 2026

Новое прямое поручение: [последовательно завершать игровые срезы генерации](GALAXY-CONTENT-COMPLETION.md),
включая имена/размеры/пояса и остальные объекты; без избыточных тестовых прогонов.

Прямой запрос пользователя разрешает доработку в отдельной ветке от master.
Первый срез — [процедурные туманности](GALAXY-NEBULAE.md): Core geometry,
source-derived имена, membership и metadata; UI/эффекты вне среза.
Исторические gates ниже не являются статусом этой новой реализации.

## Актуальный критерий готовности — уточнение пользователя

13 октября 2026. Прямое указание пользователя: идеальное воспроизведение игровой
логики генерации не требуется; достаточно полной поддержки ванильного контента,
некоторые отклонения допустимы. Это уточнение имеет приоритет над прежними
требованиями exact/native-bit certification в контрактах отдельных срезов.

Приоритет дальнейшей работы — покрытие ванильных определений, условий и связей,
завершённый работоспособный pipeline и проверки его инвариантов. Побитовое
совпадение, literal native allocator/FP mode и совпадение результата с игрой по seed
не являются обязательными gates. Реализацию делаем детерминированной по нашим
входам/seed; известные отклонения фиксируем. Нельзя выдавать неподдержанный контент
за реализованный или молча исключать его ради совпадения отдельных векторов.
Реверс нужен для закрытия пробелов покрытия и существенной семантики; уточнение
малозначимых native деталей не должно задерживать независимые этапы разработки.

Это критерий дальнейшей приёмки, а не заявление о готовой полной галактике.
Изоляция от планетного прототипа и независимое ревью сохраняются. Ниже сохранена
история первого среза; её ограничения описывают тогдашнюю реализацию.

GALAXY-GENERATION-001 attempt1, 13 октября 2026. Статус: реализован изолированный срез;
независимое ревью frozen slice, M01, M16/M20, pre-relaxation candidate и supplied M11/M13
operators и collision/clamp/retry journal пройдено без замечаний;
[результаты и ограничения](reports/galaxy-generation-attempt1-handoff.md).
[Evidence/provenance](reports/galaxy-generation-evidence-index.md).

## Граница API

Изолированный модуль `Core/Content/Galaxy` импортирует read-only сценарии и shapes;
`Core/Galaxy` подготавливает выбранную пару и предоставляет явные gates дальнейших стадий.
Никаких изменений планетного прототипа, Session, server/client, существующих fixtures или contracts.
Модуль не подключается автоматически к сборке мира.

Вход: корень установки, явно упорядоченные относительные пути файлов scenarios/shapes,
ID scenario и shape. Поддерживается только vanilla base content 4.5.2, без mod/DLC merge.
Metadata обязана содержать единственный `rawVersion = "v4.5.2"`.
Уникальные ID обязательны: M38 load/merge order не доказан. Порядок caller и записей
сохраняется; сортировка ID не заменяет native database order.

Выход: неизменяемый каталог с AST, source path/line, manifest SHA-256/byte length;
план с исходными scenario/shape и source-derived target stars/radius.
Неизвестные поля сохраняются в immutable AST и не исполняются. Подготовка не заявляет
полную валидацию пользовательских setup settings. Static scenario явно вне поддержанного среза.

Ошибки: missing/invalid files, другая версия, malformed script, повторный ID/одиночное поле,
неподдерживаемый operator/число в используемом поле, отсутствующая shape, неподдерживаемая
пара scenario/shape, неположительные stars/radius и нецелые stars отклоняются без defaults.
Абсолютные пути, traversal и reparse components отклоняются. Проверка стабильности
повторно читает все manifest files; изменение/исчезновение прерывает импорт.

## Детерминизм и native gates

Одинаковые bytes и порядок входов дают одинаковый каталог/план, без RNG draws.
`NativeRandom.FromSpecifiedSeed(int specifiedSeed)` реализует M01 только для явного
обязательного `0..INT_MAX`, возвращает локальный immutable state без draws.
Отрицательные значения отклоняются; auto-seed/UI и global RNG не поддерживаются.
Преобразование: SeedBase=0, Counter=Mix32(0x5EA6BA9F−p×0x4AD685B3) modulo 2^32;
старший бит counter сохраняется. Физический native порядок слов не задаёт API порядок.
`NativeRandom.Next` принимает immutable
`NativeRandomState(seedBase:uint32,counter:uint32)`, возвращает uint31 draw и новый state;
M02 mix-chain и counter wrap воспроизведены точно по [дополнению evidence](reports/galaxy-generation-native-primitives.md).
`EvaluateRawFixedRange(base:int64,span:int64,draw:uint31)` не расходует RNG;
M04 signed truncation подтверждён. Span передан явно: script min/max decoding остаётся M25.
Непредставимые int64 product/sum отклоняются через checked overflow;
это ограничение нашего API, native overflow не квалифицирован.

Координаты по-прежнему не исполняются: требуют M03/M05–M11/M25;
topology M13–M18; initializer selection/execution M19–M28; planet fill/startup
M29–M37; merge M38; resource regions/nebulae/wormholes/gateways M39–M40.
Операция генерации возвращает typed unsupported result с IDs, никогда пустую «успешную» галактику.
M01 закрыт для локального explicit seed entry, но не обещает native galaxy seed equivalence:
late hyperlane pruning дополнительно требует согласованного global RNG state/draws,
а shape/graph/order contracts остаются открытыми. `GenerateCoordinates` по-прежнему
всегда возвращает unsupported result; seed API не подключён к planet runtime.
M31/M32/M35 из нового evidence не реализованы этим модулем; их gate означает отсутствие
исполнителя здесь, а не утверждение, что партнёр не восстановил оператор.

## M16/M20

M16 принимает source-derived method/value/cap и caller empireCount>=0.
Поддержаны `one_every_x_empire` с value>0 и `constant` с int32 value;
cap передаётся явно (−1 значит без cap). Неизвестный method отвергается,
нет догадки о default отсутствующего cap. Выход int32 count>=1, RNG не расходуется.
Прочитанные поля обязаны быть уникальными присваиваниями и int32 literals;
непредставимая int32 промежуточная сумма raw-count отвергается checked gate.
`PrepareClusterCount(plan,empireCount)` соединяет projection и raw M16 operator,
не размещает кластеры и не исполняет M17.

M20 raw evaluator принимает int32 spawn/scaledChance, неотрицательный int32 totalSystems,
явный signed int64 fixed factor (scale100000). Выход int64 chance без clamp.
Первое округление применяется только при scaledChance>0, второе всегда;
оба — nearest с half away from zero. Отдельный predicate получает уже потраченный
uint31 draw и сравнивает draw%100 с chance, сам RNG не вызывает.
Отсутствующая setting должна разрешаться importer'ом в factor100000; сам raw API
не скрывает defaults и не читает settings/initializer scripts.
Первый scaled×N product обязан помещаться в int32 (native dword operands),
второй base×factor в int64; переполнение отклоняется, native wrap вне покрытия.
M19/M21/M22/M23 остаются gates, evaluator не выдаётся за полный initializer selection.

Acceptance: reverse-партнёр vectors, оба rounding stages/negative-half,
cap/min1/zero-empire, missing/malformed fields/unknown method/zero divisor,
uint31 roll boundary и overflow gates, genuine scenario cluster projection,
Core0/0 + own checks + independent diff review.

## Реализованный срез: координатный кандидат до relaxation

Вход — explicit prepared raw shape parameters, индекс принятой звезды, rotation и
ровно 4 (no arms) либо 7 (arms) уже потраченных uint31 draws. Выход — raw x/y,
radius/angle/arm index, **до** native clamp/allocation/collision/relaxation.
Отдельный wrapper берёт эти draws из immutable M02 state и возвращает next state;
rotation arms требует отдельно два draws, no-arms ноль.
Ring/arms dispatch data-driven по наличию блоков; нет switch по shape ID.
Fixed scale100000, wide intermediate Int128, truncation after each FM;
sin/cos используют recovered 3-term polynomial и strict folds, без libm/приближения.
Переполнение final int64/additions отвергается, native overflow не обещается.
Raw radius/span/ring width/offset должны быть неотрицательны; numArms положителен;
acceptedIndex неотрицателен. Source projection требует используемые поля явно,
fixed literals допускают только точное представление в scale100000.
Shape core percentage в 0..1, min distance scalar>=0; неизвестный descriptor не sample'ится.

Это candidate API, не координатная стадия целиком: collision acceptance, attempt loop,
native allocation/IDs/clamp и relaxation не реализованы. Имена APIs содержат PreRelaxation;
`GenerateCoordinates` сохраняет unsupported result. Никаких объектов planet/runtime.
Acceptance: independent angle/candidate A/B vectors §16.6, source projection всех 10
vanilla shapes, exact 4/7 draw counts, rotation0/2 draws, rejected-input/overflow gates,
core/ring branch invariants, M01/M02/M04/M16/M20 regression + independent review.

## Scalar collision/clamp и retry journal

Вход collision operator: raw candidate, ordered accepted stored coordinates,
scalar minDistance>=0. Для обычной distance ветви squared=FM(dx,dx)+FM(dy,dy), threshold=FM(minDist,minDist);
reject только при strict less-than, equality допускается. Clamp acceptance по каждой
raw coordinate в inclusive [−50000000,50000000]. Никаких RNG draws у этих operators.
Checked int64 subtraction/FM/sum отсекает неподдержанные extreme inputs.
Если одновременно |dx|<100000 и |dy|<100000, native rescale branch масштабирует
components ×100. При minDistance>=1000000 collision true; иначе сравниваются
scaled squared distance и square(minDistance×100), equality допускается.
Не объявлять два обычных FM универсальным native distance helper.
Retry budget10000 проверяется на collision **до** decrement: после10000 rejects
допускается ещё один sampled candidate; fail только на10001-м collision.
Каждое acceptance сбрасывает retrybudget. Не путать с maximum evaluation count10000.
Supplied-candidate journal не выделяет native handles и не регистрирует objects;
статусы target reached/input exhausted/retry exhausted различимы, partial результат явен.
Allocation в loop не тратит RNG, raw candidate сравнивается со stored clamped accepted objects;
это подтверждено партнёром. `GenerateJournal(plan,specifiedSeed)` соединяет source
shape projection, M01 state, rotation/candidates, collision/retry/clamp и возвращает
accepted raw+stored coordinates и state после последнего sampled candidate (включая failed).
Он не выделяет IDs, не создаёт systems и не вызывает Triangle/relaxation.
Composite coordinates/allocator/relaxation gates сохраняются.

## M11/M13: supplied-geometry срез

Exact Triangle1.6 TRILIBRARY/SINGLE/NO_TIMER с native memory-pool/writeedges order
в проекте отсутствует. M11/M13 composite gates сохраняются; generic geometry backend
и packages не устанавливаются. Поддержанный dependency-free M11 operator принимает
polygon vertices binary32 в заданном порядке и выполняет fan centroid:
staged float32 sub/add/mul/cross, затем double accumulation, final float32 division.
Input должен содержать минимум две конечные точки; zero-area guard отсутствует,
NaN от 0/0 — ожидаемый результат, не успешный relaxed world.
IEEE operations фиксируются отдельными NoInlining float helpers, без FMA.
Centroid API не сшивает edges. Отдельный single-pass API принимает immutable
supplied exact Triangle-output snapshot: sites, ordered primal/Voronoi edge pairs,
Voronoi vertices. Orient incident segments; unbounded rays/<=1 segment/missing
first-match stitch/out-of-inclusive-bbox pin site. Final endpoint проверяется bbox,
но безусловно исключается из centroid count; explicit closure check native не делает.
Output — новый immutable site array, все вычисления используют исходную tessellation.
Никакой tessellation rebuild, world update или готового relaxation pipeline.

M13 raw quantization — float32(trunc0(rawCoordinate/100000)); не строит candidate graph.
`FilterAndSplit` принимает supplied ordered Triangle edges при N>=3, original fixed points
и mapped group IDs. Strict annulus filter использует FM radii и midpoint=trunc0((a+b)/2);
целый Newton sqrt wide squared differences даёт raw length, stored length floored к map unit.
Direction/order survivors сохраняется, split по equal groups stable, flag0.
`DirectSmallGraph` при N<2 возвращает пустые adjacency, при N2 — ровно два opposite
adjacency с raw integer-root length и flag0. N2 не создаёт candidate record и не
применяет Triangle, annulus, density/max-distance pruning. N>=3 этот API отвергает.
Используемые sums/differences/FM/int64 distances checked; неподдержанные overflow inputs
отклоняются вместо обещания native extreme arithmetic.
Bbox inclusive и Triangle build/options сохраняются как contract будущего backend,
а не исполняемый заменитель. Input arrays/snapshots не меняются, output collections immutable.
Acceptance: centroid triangle/reverse/square/two-point и staged-float bits vectors,
negative quantization/midpoint, strict annulus equalities, edge order/stable split,
whole-unit vs N2 raw length, stitch/orientation/rays/bbox/unconditional endpoint exclusion,
full regression + independent review.

## Проверки первого среза

Текущий результат после расширения: Core Release build 0 warnings/errors,
28 PASS own checks, включая genuine import 5 scenarios/10 shapes/8 hashed files,
source projection и candidate всех 10 shapes, reverse vectors M01/M02/M04/M16/M20/trig/A/B,
collision/scaled/clamp/retry/finalfailedledger; genuine tinyellipse seed1 journal200coordinates.
Независимый reviewer принял все code deltas; тесты принадлежат author-run evidence.

Core Release build: 0 warnings/errors. Собственный no-package executable checks project:
genuine runtime import/provenance, immutable output, declared input order, стабильность,
missing/malformed/duplicate/version/path failures, корректная/некорректная пара setup,
typed gates. Проверки используют собственные synthetic fixtures; scripts установки не копируются.
Native operators допускаются только с сохранёнными точными evidence и vectors,
проверками границ и явной policy предоставленного state.
Независимый reviewer читает final diff/контракт; author не утверждает собственную приёмку.
