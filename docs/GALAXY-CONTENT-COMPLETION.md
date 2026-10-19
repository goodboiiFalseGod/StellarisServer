# Завершение генерации: последовательные игровые срезы

Прямое поручение пользователя 16 октября 2026: доделать генерацию галактики,
включая размеры, имена и остальные объекты, в том числе пояса астероидов.
Фичи выполнять последовательно до доступного игроку результата. Проверки
ограничивать изменённым поведением; не повторять широкие suites без причины.
Позднее указание 16 октября: браузерные тесты могут вызвать краш Codex;
после уже выполненного общего smoke новые браузерные прогоны не запускать.
Дальнейшую проверку проводить точечно через CLI/Node и независимое ревью.
Рабочая ветка `feature/galaxy-generation-content`, master не изменяется.

## Порядок

1. Туманности: настройки → генерация → авторитетный snapshot → карта и подписи.
2. Имена систем/звёзд/планет/лун: source catalogs, deterministic naming и читаемый UI.
3. Размеры: source-defined значения до UI/визуала с сохранением home/startup размеров.
4. Пояса и кольца: отличать декоративные belts, физические asteroid bodies и rings.
5. [Соседи/гарантированные миры](GALAXY-GUARANTEED-WORLDS.md), затем special initializers, gateways/wormholes
   и остальные content families в порядке разрешения зависимостей interpreter.

Завершён [MISC-01: редкие звёзды и Trappist](GALAXY-MISC-CONTENT.md):
целевые CLI/Node проверки и независимое ревью PASS; [отчёт](reports/galaxy-content-validation-2026-10-16.md).
Браузерная visual QA новых адаптеров не проводилась по указанию пользователя.
Завершён [VISUAL-01](GALAXY-VISUAL-CORRECTIONS.md): масштабы, атмосфера, фоны
и состав звёзд на карте; CLI/Node, review и Release PASS. Повторная ручная проверка
выявила слишком большой масштаб относительно орбит и потерю цвета на карте:
VISUAL-02 в том же контракте исправляет калибровку и маркеры; focused checks,
independent review и Release PASS. Повторная оценка вида остаётся пользователю.
Завершён [GATEWAY-01: заброшенные врата](GALAXY-GATEWAYS.md); после его проверки
и коммита продолжается очередь ниже. Прямое поручение подтверждено16 октября, браузер запрещён.
Дополнение пользователя: [границы стрелок перехода](GALAXY-NAVIGATION-BOUNDARIES.md)
завершены `99f2531` с focused checks/review/Release PASS. Завершена
[частота пригодных миров](GALAXY-HABITABLE-WORLDS.md), focused backend/client,
independent review и Release PASS. Завершён [WORMHOLE-01](GALAXY-WORMHOLES.md):
парная генерация, настройка частоты, локальный импорт и выбор объектов на карте;
focused CLI/Node, independent actual-diff review и Release PASS. Исследование и
перемещение через червоточины, восстановление врат остаются вне этого среза.
Shared renderer writers не пересекаются.

Этот список не объявляет фичи реализованными. Для очередной фичи до кода
записывать её контракт ниже либо в тематическом файле; только после приёмки
и коммита переходить к следующей. Факты реверса — [конспект](reports/galaxy-generation-rules-2026-10-16.md).

## Туманности: игровой срез NEBULA-02

Статус: реализован, reviewed и проверен целевыми smoke; [отчёт](reports/galaxy-nebulae-validation-2026-10-16.md#nebula-02--подключение-к-игре-16-октября).

База: `6bff94a`, чистая. Core geometry/names/membership уже реализованы
по [контракту](GALAXY-NEBULAE.md). Теперь разрешены Session/protocol,
server/client/renderer и необходимые целевые проверки в этой изолированной ветке.

- Host в лобби выбирает count `0..scenario.num_nebulas`. Первоначально выбрать
  source default для tiny. Смена размера карты ограничивает ввод новым диапазоном;
  отрицательный/дробный/превышенный count сервер отклоняет атомарно.
- Добавить optional `Nebulae` к settings/команде. Отсутствующее значение старого
  caller означает source default выбранного сценария; сервер публикует resolved int.
  Смена count инвалидирует кандидат и ready так же, как остальные настройки.
  Host-only, lobby-only, fingerprint/ACK и запуск из сохранённого кандидата сохраняются.
- В пространственную проекцию добавить optional immutable nebula DTO: stable ID,
  center/radius в тех же galaxy units, name key, только разрешённые member system IDs.
  Отдавать туманность лишь при наличии разрешённого member; не раскрывать скрытые IDs.
  General/observer/player permissions остаются прежними. Fixture без данных — пустой набор.
- На galaxy view рисовать мягкое полупрозрачное облако в реальном center/radius
  позади звёзд/гиперлиний и читаемую подпись. На system view показывать принадлежность
  выбранной системы в обычной информации о ней. Геометрия и подпись не перехватывают
  выбор систем/флотов, map navigation работает. При count0 облаков/подписей нет.
- Для первого отображения использовать собственный procedural shader без новых
  игровых bitmap assets; это представление восстановленной геометрии, не заявление
  о совпадении нативного shader. Имена разрешаются локальной локализацией, при
  отсутствии перевода отображается читаемый ключ с диагностикой существующего слоя.
- Валидатор snapshot проверяет finite coordinates, positive radius, unique nebula IDs,
  membership только в переданных systems, отсутствие повторной принадлежности.
  Перестроение вида/смена мира освобождает owned GPU resources; stale snapshot не смешивается.
- Сенсоры/боевая симуляция не добавляются: они не реализованы текущим пространственным
  прототипом и не нужны для создания/отображения объектов этого среза.

Приёмка: один focused backend smoke (settings/prepare/start/projection/privacy/count0),
целевые JS assertions на DTO и geometry/cleanup, один browser сценарий карты/перехода
в систему. Одна Release сборка после изменений. Independent actual-diff review;
повторять только провалившиеся или затронутые последующим исправлением проверки.

## Имена: игровой срез NAMES-01

Статус: реализован, независимое ревью принято; [проверки](reports/galaxy-content-validation-2026-10-16.md#имена-names-01).

База `fcf5cae`, дерево чистое. Каталог `star_names` находится в том же source file,
который уже fingerprinted для nebula names. Native naming enum описан в
`initializer-descriptor-defaults-handoff-452.md` §2; `example.txt` документирует
явные имена/namelist. Native allocator/ties полного naming не восстановлены;
следующая deterministic presentation policy не обещает seed-identical имён.

- Имена систем: сохранить explicit system name; иначе explicit primary-star name;
  иначе выбрать из ordered `star_names` отдельным Names RNG без повторения
  (swap-with-last). Explicit имена заранее исключить из случайного пула.
  Дубликаты pool keys свести к первому entry, чтобы не создавать одинаковые
  случайные названия. Не расходовать Content/geometry stream.
- На основном star имени системы не добавлять номер; другие stars получают
  буквенный suffix в generation order. Планеты получают системное имя и Roman
  orbital ordinal I/II/III, moons — имя родителя и satellite suffix. Обходить
  siblings в generation order; не сортировать строковые IDs и не считать stars
  планетами. Для explicit satellite_naming_policy поддержать Roman/Arabic/
  lower/upper Latin tokens; missing default lower Latin. Неизвестный active token
  диагностировать, не скрывать. Explicit body names сохраняются.
- При исчерпании пула назначить уникальное читаемое `Система N` и явный diagnostic;
  не возвращать classId или null. Пустой/missing/невалидный star_names source для
  активной world naming операции — typed failure до публикации.
- Сохранить name key и suffix отдельно в generated metadata и wire DTO для
  систем/тел. Читаемый `Name` fallback сохраняет совместимость старых consumers.
  Для сгенерированного compositional name локализуется исходный key, затем suffix;
  не искать несуществующий ключ вида `Altair III` в localisation.
- Client централизованно разрешает spatial names при приёме snapshot и после
  загрузки локализации: map/selection/outliner/colony/destination получают одинаковый
  Name. Не локализовать IDs и не менять command targets. Повторная локализация
  не удваивает suffix. Неизвестный перевод использует читаемый fallback key.
- Home class/size/ownership/IDs не меняются, эффектные origin/namelist renames
  не выдаются за исполненные. Текущие 11 ordinary и 6 generic home profiles получают
  имена; поддержка всех species namelists — следующий interpreter contract.

Приёмка: один focused Core/Session check на genuine world (все системы/тела имеют
имя, повтор seed стабилен, literal overrides/sibling suffixes/empty pool проверены
синтетически); один targeted JS check на единообразную локализацию без повторного
suffix; одна Release сборка; independent review. Browser visual smoke затронутых
подписей по возможности объединить со следующим размерным/поясным срезом, не
повторяя импорт ассетов для каждой подписи.

## Размеры: игровой срез SIZES-01

Историческая калибровка ниже заменяется [VISUAL-01](GALAXY-VISUAL-CORRECTIONS.md)
после выявленного пользователем нарушения соотношения размеров. Source sizes,
startup FinalPlanetSize и показ значения в карточке остаются действующими.

Статус: реализован и принят независимым ревью; [проверки](reports/galaxy-content-validation-2026-10-16.md#размеры-sizes-01).

База `fe1098d`, чистая. Core уже выбирает size из descriptor/class range;
повторно выбирать размеры или менять размерные распределения запрещено.
Пробел — отсутствие численного размера на обычной карте и потерянный
`planet_classes.entity_scale` при отображении generated bodies.

- Добавить optional positive integer `PlanetSize` в body DTO для planets/moons
  только при передаче разрешённых detailed bodies. Для home использовать
  `startup.Plan.FinalPlanetSize`, для остальных — exact generated size.
  Stations/fixture без значения остаются совместимыми; лишние null не писать в JSON.
  В карточке обычной планеты/луны показывать «Размер». Class и имена сохраняются.
- Читать `entity_scale` выбранных классов через существующий source numeric evaluator,
  включая file/global `@variables`; значения positive finite. Missing/invalid active
  field диагностировать с class/source, не подставлять догадку о native default.
- Сохранить существующую M3 display calibration и `clamp(size/20,.1,5)`.
  Для generated bodies дополнительно применять отношение source entity_scale класса
  к source baseline того же kind: `pc_g_star` для stars и `pc_continental` для planets/moons.
  Baseline values тоже читать из источника (в текущей установке20 и11).
  Это `SourceClassRelativeScaleV1`, калибровка браузерного рендера, не нативная формула
  радиуса. Например gas giant14/11, asteroid1.5/11 сохраняют source различия классов.
- Коэффициент применяется один раз при создании BodyDefinition.Scale; не дублировать
  его в visual profile. Не менять mesh data, размеры колоний, экономику или орбиты.
- Положение подписей и system overview bounds учитывать по реально отображаемому
  surface radius шаблона (без атмосферных glow/effects), а не нулевому fallback
  DISPLAY_RADII для `world.*`. Picking продолжает использовать отрисованные меши.

Приёмка: один focused runtime/source check для continental/gas giant/asteroid и
home final size, один JS check для размеров подписи/bounds/optional DTO; одна сборка.
Browser system smoke объединить с поясами следующим срезом. Не повторять huge
payload suite: новый nullable размер передаётся только limited detailed bodies;
вернуться к bytes-проверке лишь при фактическом росте набора или признаке overflow.

## Пояса: игровой срез BELTS-01

Статус: код реализован, focused проверки и independent review пройдены;
общий browser smoke names/sizes/belts/rings пройден, см. отчёт.

База `9411e58`, чистая. Core уже генерирует belt type/radius в source порядке;
теряется только содержимое на границе Session/client. Кольца планет — следующий
отдельный срез. Этот срез не создаёт дополнительные gameplay bodies.

- Читать `common/asteroid_belts/00_asteroid_belts.txt` read-only с fingerprint,
  проверкой стабильности и запретом reparse paths, аналогично random names source.
  Missing source допустим при неактивных belts синтетического fixture; active belt
  требует unique type, positive finite width/density. Документированный source
  default обоих полей1. Сохранять geometry/Content/Names RNG и порядок радиусов.
- В generated belt metadata сохранить width/density, deterministic ID дать по
  system/belt ordinal. Source входит в content fingerprint, неизменность проверяется
  до публикации. Не сохранять исходный файл в Git/ассеты клиента.
- Передавать optional belts только для разрешённых detailed systems (собственная
  колония или текущая inspected система). Пустые/fixture belts не увеличивают JSON.
  Данные: ID/system/type/radius/width/density/visualId. Клиент проверяет finite
  positive geometry, уникальные IDs и принадлежность переданным systems.
- В текущих ordinary/home profiles активны rocky/icy. Представление
  `RepresentativeAsteroidBeltV1`: локальные импортированные templates
  `world.pc_asteroid`/`world.pc_ice_asteroid`, добавить binding в manifest requirements
  при наличии соответствующего belt даже без физических asteroid bodies.
  Не обещать exact native mesh mixture: source содержит несколько mesh вариантов.
  Остальные active belt types до отдельного visual binding дают typed failure,
  не превращаются молча в rocky. Source registry при этом может содержать все типы.
- На system view построить ограниченное детерминированное кольцо декораций вокруг
  source центра системы на source radius, width/density влияют на annulus/count.
  Собственная калибровка: half-width `6*width` local units, count
  `clamp(round(radius*density/2),32,160)`, радиус каждого камня `.3..1.1` local units.
  Локальный hash PRNG по belt ID не расходует world RNG. Это browser presentation,
  не восстановленная нативная плотность/форма. Декорации не pickable, без labels,
  орбит/экономики/commands; реальные pc_asteroid остаются обычными selectable bodies.
- Учитывать внешний край пояса в overview bounds. Смена системы/снапшота/profile
  не оставляет дубликатов и не освобождает shared imported geometry/materials.
  Galaxy view не рисует декоративные belts. Навигацию/камеру/флоты сохранить.

Приёмка: один tiny runtime/source/projection check (rocky/icy, privacy, unchanged
body count, fingerprint/source stability); targeted Node check geometry/count,
determinism, nonpicking и lifecycle; одна Server Release сборка. Независимое ревью
actual diff. Browser smoke совместно проверяет имена, размеры и пояса; не повторять
широкие suites. Проверить payload только если существенно расширен набор данных.

## Кольца: игровой срез RINGS-01

Статус: реализован, independent review и общий local-import browser smoke пройдены.

База `54ae5a7`, чистая. Сейчас explicit has_ring и class-ring-probability остаются
deferred. Источник: reverse addendum §6 и work/post_create.txt того же пакета,
адреса `0x140E0BD6E..0x140E0BDF2`. Native `cmovg` после сравнения class chance с
остатком draw даёт строго `draw % 100000 < chanceRaw`, не inclusive вероятность.

- Для planet/moon explicit `has_ring=yes/no` имеет приоритет и не тратит draw.
  Иначе читать chance_of_ring класса source numeric evaluator, допустимый fixed
  диапазон0..1 с precision5; active missing/invalid field — typed failure.
  Один NativeRandom draw тратится даже при chance0/1. Stars не участвуют.
- `RingSuppliedSeedV1`: отдельный существующий Script0 stream, обход в порядке
  созданных bodies. Это выбранная детерминированная staging policy: нативный
  global RNG interleaving c deposits/modifiers не воспроизводится. Geometry,
  Content/Names streams, class/size/orbit не меняются. Добавить stage ledger/policy.
  Применённые ring requests больше не deferred; прочие effects остаются явными.
- Сохранить HasRing в immutable generated metadata, включить в fingerprint.
  Optional bool wire передавать только для уже разрешённых detailed bodies;
  отсутствие означает false, пустые значения не увеличивают JSON. Fixture совместим.
- Generated visual import получает одну shared native ring closure:
  gas_giant_rings_01_entity → gas_giant_rings_01.mesh и ring_tiling diffuse/normal/spec.
  Для минимальной совместимости допустимо включать closure во все generated
  profiles без изменения class-only requirements; точный closure и SHA входят в
  обычный manifest digest. Не включать original bytes в Git/серверные ответы.
  C#/JS resolver/allowlists/validation должны согласованно проверять binding.
- Renderer создаёт кольцо условно per-body, class template shared не мутируется.
  `SourceMeshRingDisplayV1`: исходные локальные vertex coordinates в центре тела,
  source entity/gfx scale, наследование body Scale/display factor ровно один раз.
  Без недоказанного skeleton anchor и формулы солнечного наклона. Это статический
  browser adapter, не обещание native visual parity. Материал PdxMeshPlanetRings
  двусторонний, alpha blend, без depthWrite; сохранить прозрачность и текстуры.
- Декоративное кольцо не создаёт body/колонию/орбиту, не перехватывает picking,
  учитывается отдельным внешним bounds для overview, исключается из surface
  radius подписей. Смена snapshot/profile/system освобождает только owned ресурсы.

Приёмка: focused Core synthetic probability thresholds/explicit/no draw и один
tiny runtime projection check; targeted Node реальный ring mesh/closure, conditional
clones/material/bounds/picking/lifecycle; одна Release сборка, independent diff review.
Один browser import/smoke объединяет names/sizes/belts/rings. Broad suites не повторять.
