# Завершение генерации — проверки игровых срезов

Рабочая ветка `feature/galaxy-generation-content`, [контракт](../GALAXY-CONTENT-COMPLETION.md).
Прямое поручение пользователя: последовательные фичи до игрового результата,
небольшое число целевых проверок. Не повторять весь прежний набор перед каждым коммитом.

## Туманности NEBULA-02

Коммит `fcf5cae`; [отдельный отчёт](galaxy-nebulae-validation-2026-10-16.md#nebula-02--подключение-к-игре-16-октября).
Core → settings/wire → filtered snapshot → карта/подписи/информация системы.
Один backend smoke, 6 wire assertions, 3 Node tests и Release0/0; root IAB QA
production лобби + procedural renderer на actual generated data. Транспортный
пропуск найден браузером и исправлен. Независимое ревью принято.

## Имена NAMES-01

База `c11d958`, автор `/root/galaxy_names`, reviewer `/root/galaxy_names_review`.
Actual runtime model/usage недоступны; настройки ответственных агентов не менялись.

Ordered deduplicated source `star_names`, literal overrides, отдельный Names RNG,
создание Roman/Latin/Arabic suffixes и metadata v3. Session передаёт key/suffix;
client переводит пространственные подписи и имя связанной economy-планеты.
Generated naming не меняет class/size/IDs/startup state. Native allocator/ties,
species namelists и ещё не исполняемые name-changing effects не заявляются.

Проверки автора:

- `GalaxyGenerationChecks --names-only game-root worker sha256` — synthetic
  overrides/order/policies/pool failures и один genuine tiny replay PASS:
  200 систем/2201 тело, fingerprint
  `ED91B149B224399129D30B544751EBC51F4DFF8CB881E20879E1DF439147FA92`.
- `node tools/asset-probe/m3-spatial-names.test.mjs` — PASS, повторён только после
  исправлений JS по ревью. Проверены единые подписи, локализация, повторный перевод,
  literal fallback, economy mapping и renderer refresh.
- Server Release build — 0 warnings/errors; сборка не повторялась после JS-only fixes.
- Один целевой `GalaxyM3Checks ... --names-budget-only` измерил actual FULLSession
  huge/density3, observer→host, densest nonhome inspected: **1 029 158 байт** при
  лимите 1 048 576, запас 19 418; spatial734050, 1304 видимых тела. Лимит и
  production представление не менялись. Два предварительных ошибочных ожидания
  fixture о general Development scope исправлены до первого измерения; широкие
  suites/все POV не прогонялись.

Independent actual-diff review выявил и закрыл два P2: same-sequence renderer
не обновлял подписи при reimport локализации; legacy economy pane сохраняла
прежнее имя. `refreshSpatialNames` меняет только имена уже owned IDs при exact
session/sequence, не ослабляет network stale guard и не сбрасывает камеру.
Второй fix связывает только уже разрешённые colony/economy/body IDs в local UI.
Итоговое review production diff — без новых замечаний.

Browser smoke подписей на этом этапе был отложен и позднее пройден в общем
сценарии ниже; полный two-client local-assets import отдельно не повторялся.

## Размеры SIZES-01

SourceClassRelativeScaleV1 сохраняет source entity_scale относительно baseline
звезды20/планеты11, включая gas giant14 и asteroid1.5. Size не выбирается повторно;
home использует FinalPlanetSize. Размер доступен в карточке, surface bounds
учитываются для подписей и общего вида без атмосферы/эффектов.

- Один `GalaxyM3Checks ... --sizes-only`: PASS, tiny200 систем/2338 тел;
  проверены source ratios, home final, limited DTO и отсутствие null в JSON.
- `node --test tools/asset-probe/m3-sizes.test.mjs`: 2 PASS. Первый запуск
  исправлен только в test lighting fixture. Проверены validator/card и реальные
  mesh bounds для labels/overview с исключением FX и сохранением picking.
- Server Release `--no-restore`: 0 warnings/errors; diff whitespace PASS.
- Independent actual-diff review `galaxy_sizes_review`: PASS, 10 файлов,
  без повторного запуска проверок. Browser smoke объединён с поясами.

## Пояса BELTS-01

Сохранённые Core belts доведены до detailed projection и system renderer.
Source registry hashed/stable, width/density defaults1; representative rocky/icy
используют импортированные шаблоны. Декорации не добавляют gameplay bodies.

- Один `GalaxyM3Checks ... --belts-only`: PASS, tiny200 систем/2338 тел/138 поясов;
  source defaults/errors/stability, unique IDs, requirements, privacy и empty JSON.
- `node --test tools/asset-probe/m3-belts.test.mjs`: 2 PASS; до успеха исправлены
  только test fixtures sourceClassId/callbacks. Проверены deterministic geometry,
  узкий annulus, отсутствие picking/galaxy decorations и shared lifecycle.
- Server Release `--no-restore`: 0 warnings/errors после nullable warning fix.
- Independent actual-diff review `galaxy_sizes_review`: PASS, 13 codefiles;
  проверки не повторялись. Gameplay radius/exits сохранены; пояса расширяют
  визуальные bounds камеры. Общий browser smoke позднее пройден, см. ниже.

## Кольца RINGS-01 и общий игровой smoke

- Focused synthetic/backend `--rings-only`: PASS, 2338 тел/259 колец/2085 draws.
  Explicit yes/no без draw, strict threshold, source variables, chance0/1,
  invalid chance и detailed privacy проверены.
- Targeted Node: 3 PASS, actual native ring mesh, C#/JS closure/manifest parity,
  conditional clones/materials/bounds/picking/lifetime, перевод «Спутник».
- Server Release `--no-restore`: 0 warnings/errors, diff whitespace PASS.
- Independent actual-diff review `galaxy_sizes_review`: PASS, 16 codefiles.

Общий реальный browser smoke на отдельном локальном стенде: seed
`galaxy-content-playable-2026-10-16`, tiny200 систем/2353 тела/281 hyperlane,
fingerprint `6B1C77E644FEBF395348075647DA4AD928DAD3B9839E3C191E45D7EE5E63553CD`.
Основной клиент импортировал 937 локальных файлов (473 graphics +464 EN/RU),
238.5 MiB, 5.78s; localisation diagnostics0, WebGL errors/warnings0.
Второй участник — явный WS QA fixture с синтетическим ACK, не второй asset import.
Подтверждены auto-start после ACK, русские имена, «Пелл I» размер19,
gas giant «Пелл X»25 и «Пелл XI»23, выбор/фокус/zoom, пояса и условное кольцо XI,
возврат на galaxy view и две локализованные туманности. Static material/placement
остаются объявленной presentation policy, native visual parity не заявляется.
Первый запуск стенда перезапущен до импорта из-за закрытого stdin QA helper;
рабочий сценарий выше завершён, временная вкладка закрыта, свои процессы остановлены.

Это закрывает отложенный browser gate NAMES/SIZES/BELTS. Полные suites, все POV,
новый huge budget и длительный GPU stress не повторялись. Позднее пользователь
предупредил о риске краша Codex от браузерных тестов: новые такие прогоны исключены.
Скриншоты в Git не добавлялись; постоянная локальная копия снимков не сохранена.

## Гарантированные миры GUARANTEED-01

Настройка0/1/2 проходит lobby→Session→Core; home batches формируют source
neighbor chains до ordinary fill. Spatial empire_cluster flags реально применены
и при0 без дополнительных draws; выбор/геометрия/Content и Names sequence при0
сохранены. Metadata/provenance отражают новые flags, fingerprint может измениться.
Новые миры не получают колонии/станции/флоты; прочие effects остаются Deferred.

- `GalaxyGenerationChecks --guaranteed-checks game-root`: compact source/graph
  fixture0/1/2 PASS, ideal contexts, source order, nearest/ties/hops, availability,
  underfill и отсутствие новых links.
- `GalaxyM3Checks ... --guaranteed-only`: один tiny двухигроковый smoke PASS,
  8 neighbor allocations при2, classes/ownership/provenance/settings/projection.
- Focused Node lobby test PASS: input bounds, command binding, confirmed value.
- Итоговая Server Release сборка: 0 warnings/errors. До неё были промежуточная
  успешная сборка и ошибочный путь отсутствующего StellarisServer.sln; затем
  использована корректная Server.csproj. Широкие suites не запускались.
- Независимое actual-diff review `guaranteed_review`: PASS; source/contract review
  предварительно `galaxy_sizes_review`: PASS. Проверки reviewer не повторял.

Автор `galaxy_guaranteed`, reviewer и root сохранили inherited настройки lead;
фактические model/effort/usage инструменты результата не сообщают, не оценивались.
Браузер и GPU smoke этого среза не запускались по прямому указанию пользователя.

## Редкие звёзды и Trappist MISC-01

К 11 ordinary definitions добавлены source black hole, neutron, pulsar и Trappist.
Admission учитывает source order, веса, finite uses, отдельный spawn draw и retry;
homes/spatial flags исполняются до ordinary fill. Для Trappist исполняются семь
планет и два бессрочных terraforming_candidate, модификатор доступен в карточке
лишь для разрешённого detailed body. Терраформирование как действие не добавлено.

Реальные локальные модели/текстуры подключены через строгий source graph. Чёрная
дыра и разрушенная планета используют объявленные static policies: без нативного
линзирования, анимации разрушения и particle events. Для pc_broken отсутствие
source chance_of_ring трактуется как0 с одним draw; native default не заявляется.
Source RGB24 texture читается bounded decoder; исходные ассеты в Git не добавлены.

- Focused M3 `--misc-only`: admission/ring fixtures, genuine Trappist
  (8 bodies, 2 modifiers) и один tiny runtime smoke — PASS.
- C# source graph/lighting: 55 PASS; Node profile/material/card: 17/17;
  targeted actual-source manifest: 1/1. Повторные проверки после review fixes
  записываются ниже отдельно; широкие suites не запускались.
- Первая итоговая Server Release сборка `--no-restore`: 0 warnings/errors.
- Independent review `guaranteed_review` нашёл и передал автору no-progress guard,
  недостающие discovery/memory registrations, typed errors/approximation metadata
  и пробелы focused fixture. Исправления внесены; финальный re-review — PASS,
  все замечания закрыты. Последний zero-total fixture отдельно различает negative
  exclusion; после его уточнения M3 `--misc-only` повторно 10 PASS.
- После fix batch: M3 `--misc-only` — 10 PASS, включая weighted boundaries,
  zero exclusion, finite success/failure/draws, scaling/overflow/unknown grammar,
  Trappist weight contexts и detailed/no-detail modifier projection. Node — 12 PASS:
  actual discovery→resolver→texture decode→memory plan новых классов и importer
  typed-error propagation. Общие бюджеты памяти не повышались.
- Повторная Server Release после затронутых правок — 0 warnings/errors;
  `git diff --check` PASS. Повторялись только затронутые проверки.

Автор `galaxy_misc`, reviewer `guaranteed_review`, root — интеграция/сервер.
Настройки lead сохранены; фактические runtime model/effort/usage недоступны.
Браузер, GPU и ручной visual smoke новых адаптеров не выполнялись по прямому
предупреждению пользователя о возможном краше Codex. Полная VanillaComplete,
врата/червоточины и остальные special/story families остаются незавершёнными.

## Совместимость с актуальным master: число игроков

После передачи сервера пользователь обнаружил старое ограничение двумя игроками:
ветка генерации начиналась с5e8f5af и не включала последующие4a00d6b/1bca87c.
Master объединён в feature-ветку; master и чужие checkouts не изменялись.
Конфликты settings/test entrypoints разрешены с сохранением обеих функций:
M3 ≥1 player, Nebulae/GuaranteedColonies и все targeted modes. Подпись ожидания
импорта теперь «всеми игроками». Independent merge/diff review — PASS.

- Реальный HTTP/WebSocket: один игрок и наблюдатель, Ready→generation→ACK→running
  →day1, приватность и общий snapshot — 13 PASS. ACK синтетический, браузера нет.
- Focused Node lobby1/2/3/10/20 и research hydration — 6 PASS. Первый client fixture
  выявил устаревший mock без refreshSpatialNames; mock исправлен, production метод
  уже существовал. Широкие suites и прежние большие source-сценарии не повторялись.
- Server Release — 0 warnings/errors. Прежний внешний worker был удалён вместе
  с чужим worktree; первоначальный запуск проверки не состоялся. Worker с тем же
  проверенным SHA перенесён из другого готового build в собственный ignored
  artifacts/triangle-backend/build; последующая проверка выше прошла.

## Исправления по скриншотам VISUAL-01

Пользовательские снимки подтвердили нарушения display scale, одинаковый system sky,
одноточечные galaxy glyphs и резкую внешнюю атмосферную оболочку. Исправления выполнены
по [новому контракту](../GALAXY-VISUAL-CORRECTIONS.md), без браузерных прогонов.

Локальные ignored свидетельства (не оригинальные игровые ассеты для распространения):

- [Обзор](../../content-local/references/galaxy-visual-20261016/overview.png),
  SHA256 `66803F653C37529F22D08B49BE0D4D4F63F3F1754E29D778EC6185DF7AE0B643`.
- [Крупный план](../../content-local/references/galaxy-visual-20261016/close.png),
  SHA256 `6658EED3E1198FB5B1BCBE51372549413F240B0DDB31B977257A868FF2220FBA`.

Source research: `galaxy_misc` — scale/atmosphere; `guaranteed_review` — background
bands/HSV и stellar glyph metadata. Диагностика actual source geometry подтвердила
radius4.4547 для G/continental, G entity scale1.3, gas sphere4.4884/entity1.2,
atmosphere sphere5.2001. Метрики не доказывают native size→radius conversion.
Generated тела используют общий source class baseline и display factor; legacy
fixture сохраняет прежнюю калибровку. Планетная атмосфера затухает на silhouette,
сохраняя source tint/depth occlusion; звёздная corona не изменена. Фон выбирается
из трёх source bands и десяти stellar profiles; три общие cubemaps занимают42MiB
вместо48MiB. Один GalaxyRadius в spatial DTO задаёт клиентскую distance policy.
Galaxy glyph показывает разрешённые1/2/3 звезды, отдельно BH/neutron/pulsar,
с общим system picking и освобождением ресурсов.

- Source-backed profile/manifest/background/renderer/worker:38/38 PASS.
- После финальных изменений renderer/background:8/8 PASS; actual-source profile
  с legacy renderer integration:1/1 PASS; camera fixture:1/1 PASS.
- Backend `--misc-only`:11 PASS на одном tiny runtime sample, включая common scale,
  radius/coordinates, bands и приватность subset.
- Автор ошибочно запустил полный GalaxyM3Checks: huge snapshot превысил wire limit
  (789148 при750000) из-за повторённого per-system distance field. Поле заменено
  одним GalaxyRadius; единственный целевой повтор `--projection-only` PASS:
  746384 bytes,1000 systems,1282 visible bodies,radius450. Другие stress не повторялись.
- Итоговая Server Release `--no-restore`:0 warnings/errors.
- Final independent actual-diff review `guaranteed_review`:PASS; reviewer не
  повторял проверки. Все замечания закрыты; автор `galaxy_visual_fix` заморожен.
- Existing generated31-class integration fixture пропущен: нет отдельного
  GENERATED_VISUAL_SERVER_MANIFEST/lighting fixture; это не PASS.

Браузер/WebGL/GPU и ручная оценка новых shaders не выполнялись. Native size formula,
distance metric и exact visual parity не заявлены; f/m LUT остаётся явным HSV-only
static omission. Пользователь оценивает вид после обновления сервера и импортаv12.

## Повторная ручная проверка VISUAL-02

Пользователь обнаружил слишком большие тела относительно орбит и потерю узнаваемых
цветов/типов на галактической карте. Локальные ignored снимки:

- [Кратная система](../../content-local/references/galaxy-visual-20261016/binary-scale.png),
  SHA256 `587C528582515F8DAFC6BAA4E19B49EC035A26B12BF0236BECB057A7718E76DF`.
- [Компактная кратная система](../../content-local/references/galaxy-visual-20261016/compact-binary-scale.png),
  SHA256 `E1511DCC82E14452AB30488FC65C9171EFE659E26ACFD2749BD5AC62F675C2B5`.

Read-only диагностика `galaxy_visual_fix`: source orbit accumulator→parent polar
coordinates→Session local positions сохраняют единицы; unsupported R7 calibration
даёт43 отрицательных parent-child clearance из530 на ранее сохранённом snapshot.
Source пример binary_init_02: moon orbit10+3=13, gas30+molten12 имеют оценку17.56.
`guaranteed_review` подтвердил ошибочный выбор system-light RGB вместо body HSV,
ошибку giant world-tag lookup и общее warm-white glow поверх glyph. Исправление
ограничено [VISUAL-02](../GALAXY-VISUAL-CORRECTIONS.md).

Preflight с настоящими source meshes/Three templates на том же ignored snapshot
(`artifacts/galaxy-nebulae/world.json`, SHA256
`63BAA18CE39EE2665861C796236816DC740F1F1F3D48DFD1E14F02B4B6748B35`):
11990 пар в200 системах. ПриR3.5 остаются лишь два отрицательных enclosing-sphere
зазора у астероидов. Проекции всех surface vertices на ось центров дают разделяющие
интервалы с положительными зазорами0.4796 и0.6893, то есть это ложные срабатывания
сферической оценки. Gas30/moon12 имеют radius clearance2.8053; близкая binary
system/0158 — surface-support clearance4.3000. Совпадающих центров не найдено.
Это измерение сохранённого мира, не новая генерация и не доказательство всех seeds.

Generated continental size20 теперь имеет локальный surface radius3.5 вместо7;
орбиты, source sizes/classes и RNG не меняются. Legacy fixture R7/R12 сохранён.
Generated identity получила суффикс `-gmu1-gw1-`, materialProfileVersion7 и
`GeneratedMapUnitSurfaceV1` в обоих manifest implementations; source paths прежние.
Маркеры на карте используют body HSV, цветной glow позади glyph; BH/neutron/pulsar
различаются формой. Дочерний glyph имеет минимум12 CSS pixels при изменении камеры,
без изменения центра системы и navigation. Необоснованный cap64 удалён по review.

- Узкие impacted Node checks:7/7 PASS (manifest/requirements, calibration, glyph,
  surface sizes и belts). После изменения projection scale — focused actual-source
  VISUAL-02:2/2 PASS, включая настоящие Three templates и near/far projection.
- Optional `GENERATED_VISUAL_WORLD_FIXTURE` указывает на сохранённый ignored world;
  стандартный source-profile check работает без него. Финальная проверка2122 тел/
  11990 пар даёт минимальный directional clearance+0.4796069006; один консервативный
  max-vertex-radius overlap у астероидов0025/0003↔0005 имеет положительный зазор.
  Это другой bound, чем enclosing Three spheres в предварительном расчёте выше.
- Промежуточный harness сначала пропускал class-relative scale, затем включал
  pulsar outburst FX в поверхности. Оба дефекта проверки исправлены; source
  `pulsar_outbursts_mesh` использует PdxMeshAlphaAdditiveAnimateUVNoDissolve и не
  является твёрдой поверхностью. Review также потребовал runtime size clamp.
- Server Release `--no-restore`:0 warnings/errors. Браузер/GPU не запускались;
  original31-class server-manifest fixture по-прежнему отсутствует и не заявлен PASS.

Автор `galaxy_visual_fix`, independent reviewer `guaranteed_review`, root — docs/
интеграция/сервер. Final actual-diff review PASS, оба замечания закрыты; reviewer
не запускал проверки. После clamp повторён только optional saved-world fixture:
1/1 PASS, метрики не изменились. Запуск из корня: при STELLARIS_GAME_ROOT и
GENERATED_VISUAL_WORLD_FIXTURE, `node --test --test-name-pattern="VISUAL-02:"
tools/asset-probe/generated-world-visual-integration.test.mjs`; последняя проверка
использовала только pattern `optional saved-world`. В Git нет ассетов и снимков.

## GATEWAY-01

GATEWAY-01/NAV-BOUNDARY-01/HABITABLE-01: настройки модели/effort автора и reviewer
унаследованы без override; фактическая статистика usage недоступна.

Автор `galaxy_gateways`, независимый actual-diff/contract reviewer `root`; root
менял только документацию. Review PASS после исправления exact effect grammar,
source bounds/частоты, source closure и animation uniqueness, общего definition
budget, SourceMip512 для объектов и включения врат в обзор камеры.

Session Release PASS; focused Node source/view/renderer/UI44/44, worker pipeline8/8
PASS. Реальный `GalaxyM3Checks --gateway-only` с сохранённым triangle worker:
200 систем, 3 врата; проверены host settings, source admission, object requirements
и приватность detailed projection. После последнего исправления повторён только
`node --test --test-name-pattern="gateway source closure rejects"
tools/asset-probe/generated-world-visual-profile.test.mjs`:1/1 PASS.
Server Release `--no-restore`:0 warnings/errors. Ранее M3ServerChecks был остановлен
DLL lock живого сервера и не заявлен PASS; callback fixture обновлён механически.
GatewayEuclideanMapDistanceV1, внешняя placement proxy и StaticGatewayBindPoseV1
явно локальные политики; native distance conversion/random_pos неизвестны.
Браузер/GPU QA не запускались. Врата неактивны, без команд восстановления/перехода.

## NAV-BOUNDARY-01

Автор `galaxy_gateways`, independent actual-diff reviewer `root`: PASS.
SpaceWorld.Exit использует radius−10 своей системы; этот helper уже обслуживает
approach, charge и прибытие. Клиент берёт ring/arrow/route из received exits,
manual limit остаётся system.radius, при отсутствии exits внутреннего кольца нет.
Новые wire fields не добавлены. Source native boundary formula не заявлена.

Focused `GalaxyM3Checks --navigation-only`: PASS, system/0087 radius199.408303992177,
выход189.408303992177; system/0183 radius384.4045116321935,
прибытие374.4045116321935. Actual движение прошло approach→charge→transit→idle;
fixture150/140 и сохранение предыдущего приказа при отказе permissions проверены.
Actual renderer `--test-name-pattern="Received routes/exits/rings replace atomically"`:
1/1 PASS. Server Release `--no-restore`:0 warnings/errors. Браузер не запускался.

## HABITABLE-01

Автор `galaxy_gateways`, независимый actual-diff reviewer `root`: PASS.
M3 HabitableMultiplier0..5/precision5/default1 проходит host command→resolved
request→существующий Core allocator→confirmed snapshot/client. Core domain0..100,
source class/star/inline weights не изменены; отдельное GuaranteedColonies сохранено.

Focused `GalaxyM3Checks --habitable-only`: PASS, одна tiny200 генерация при0,
два явных home мира, у каждого игрока есть guaranteed body исходного ideal class;
четыре записи neighbor allocations включают также размещения без guaranteed body.
Actual source exiled_system_2 проверен при0/1/5; при0 random_colonizable не делает
class draw, при положительных значениях делает. Source sc_b coefficient0.4 прочитан;
sc_g имеет default factor1. Production weight multiplication отдельно проверен чтением
неизменённого allocator; seed-parity/монотонность counts не заявляются.
В том же режиме host/peer snapshot, отказ peer и invalid bounds/precision PASS.
Headless actual-client `--test-name-pattern="Habitable multiplier validates"`:
1/1 PASS, включая fractional0.29, ожидание подтверждения и readonly peer.
Server Release `--no-restore`:0 warnings/errors. Браузер и широкие suites не запускались.

## WORMHOLE-01

Автор `galaxy_gateways`, независимый actual-diff/contract reviewer `root`: PASS.
Root менял только документацию. Model/effort унаследованы без override; usage
недоступен. Замечания source grammar, transforms/closure, приватности, geometry
memory accounting и mip policy устранены и повторно проверены.

Проверены on_game_start → game_start.31, вложенный выбор двух систем, scalar
link_wormholes и bypass definition исходной4.5.2. Native setup count и distance
conversion не установлены; reference owner подтвердил пробел. Применены явные
локальные WormholeAttemptsFactorFiveV1, WormholeEuclideanMapDistanceV1 и
WormholeOutsideOrbitalEnvelopeV1. Проекция не раскрывает PairId/партнёра; отображение
StaticWormholeRingsV1 включает source root и child, без particles/UV animation.
WormholeNativeMip0V1 разрешает только две точные source DDS с единственным mip;
общий512MiB budget сохранён, object geometry теперь учитывается в общей памяти.

Целевые проверки, без повторения широких suites:

- Core `--wormhole-only`: PASS. Synthetic успешные пары,0/1/5, отсутствие orphan,
  уникальность/home/cluster, inclusive500 и непустые body/gateway envelopes.
  Одна actual tiny200 генерация:5 попыток,0 пар; underfill допустим, fallback нет.
- Actual mixed gateway/wormhole worker import:1/1 PASS, root2+child1 layers,
  native textures и отрицательные mip/dimensions/object policy cases.
- Focused glyph/view/UI fixtures:3/3 PASS; actual client wormhole+habitable:2/2 PASS;
  actual system renderer root/attachment picking/card/bounds/lifecycle:1/1 PASS.
- Session `--wormhole-only`: PASS, host/peer/confirmed settings и bounds; без генерации.
- C# `--wormhole-assets-only`: PASS,415 files/7 visuals. Независимое JS построение
  из тех же source файлов дало точное равенство files/visuals/profile identity.
  Digest `51ef0f5bd3104e13d696c1c2219861759113978cb67a162ae0904f0735f75636`;
  ignored metadata `artifacts/galaxy-wormholes/mixed-manifest.json`.
- Final Server Release `--no-restore`:0 warnings/errors.

Браузер/GPU, huge world и полный31-class import не запускались. Генерация/выбор
объектов доступны; exploration/technology/travel червоточин и repair/travel врат
не реализованы этим срезом. Native parity по неизвестным правилам не заявляется.
