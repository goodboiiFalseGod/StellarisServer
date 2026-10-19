# P1 handoff: `PrototypeBootstrapV1` между home body и startup on-actions

## 1. Назначение и граница утверждений

Этот документ закрывает узкий P1-разрыв между уже выбранным домашним объектом и запуском `empire_init_add_technologies`, `empire_init_capital_colony`, `empire_init_create_ships`. Он задаёт воспроизводимый адаптер для bounded-профиля `MPS-1`, достаточный, чтобы подготовить country/species/colony/default-district state для уже описанного startup interpreter.

Итог адаптера называется `BOOTSTRAPPED_START`. Это ещё не `GENERIC_PLAYABLE`: последнее состояние разрешено только после успешного выполнения всех трёх on-actions, появления положительного населения и стартовых science/constructor actors.

Используются три уровня доказательности:

- **N** — установленная native-дизассемблированием часть;
- **S** — установленная vanilla scripts/definitions часть;
- **P** — явно именованная политика `PrototypeBootstrapV1`, необходимая для P1, но не объявленная точным native конструктором.

`PrototypeBootstrapV1` не подменяет vanilla-complete P2. Он применим только к профилю `MPS-1`:

- одна regular biological non-gestalt, non-nomadic playable empire;
- `origin_default`;
- conventional regular authority;
- нет secondary species и civic/origin, меняющего стартовую carrier pipeline;
- home carrier — обычная planet, не Earth, не ship, без `special_start_planet`;
- initializer — один из `random_empire_init_01..06`;
- normal-tech start, не advanced, без guaranteed-colony extension;
- entitlement profile и content snapshot зафиксированы заранее.

Любое отличие от этих условий выбирает другой named profile либо завершает генерацию ошибкой до публикации мира.

## 2. Подтверждённая native/script граница

### 2.1 Что доказано

1. **N:** основной ordinary-empire loop вызывает `0x14097B6A0`. Внутри country object уже создан до вызовов `0x14097A180` и `0x14097A850`; объект передаётся обоим дальнейшим этапам.
2. **N:** `0x14097A180` выбирает home carrier: обычный путь использует ordered home-list; mobile/nomadic путь отделён. Непустой list всегда расходует один local galaxy draw, даже при одном элементе.
3. **N:** перед входом в `0x14097A850` выбранный home carrier уже связан с country; `0x14097A850` разрешает существующий carrier и существующую colony.
4. **N:** attach/create-colony chain `0x140268420 → 0x140DAB550 → 0x140DC8FE0` добавляет default district только при пустом списке district occurrences. Обычное добавление через `0x140DCA140` создаёт occurrence с level `1` либо увеличивает существующий occurrence того же type на `1`.
5. **N+S:** после подготовки country/home/colony native вызывает строго:
   1. `empire_init_add_technologies`;
   2. `empire_init_capital_colony`;
   3. diagnostic, если capital pop list остался пуст;
   4. `empire_init_create_ships`.
6. **S:** on-action scopes в 4.5.2 задокументированы самими definitions:
   - `empire_init_add_technologies`: country;
   - `empire_init_capital_colony`: colony, `from=founder species`, `fromfrom=secondary species` при наличии;
   - `empire_init_create_ships`: country.
7. **S:** `game_start.12` для `MPS-1` запускает в colony scope четыре эффекта в source order: deposits/blockers, buildings/districts, pops, final pass.
8. **S:** обычный `game_start.9` создаёт science fleet/ship/scientist и constructor fleet/ship; их location разрешается через country capital links.

### 2.2 Что native ещё не замкнуто

Дизассемблирование не даёт законченного переносимого контракта следующих операций:

- полного country factory, его allocator order и всех внутренних flags/caches;
- random empire synthesis и разрешения government/authority/civics/ethics;
- species factory, identity graph, rights, citizenship и exact owner/homeworld linking order;
- baseline `start_tech=yes` admission с каждым potential/DLC gate;
- native name generation и её RNG ledger;
- initial claims/control/starbase, capital-change notifications и дат;
- точного порядка ID относительно всех невидимых native allocations;
- secondary species и всех alternative origin/carrier branches.

Поэтому нижеследующий bootstrap — **P**, а не реконструкция этих функций instruction-for-instruction.

## 3. Входной контракт

`PrototypeBootstrapV1` получает один неизменяемый `BootstrapRequestV1`.

### 3.1 Заголовок запуска

```text
BootstrapRequestV1 {
  contractVersion = "PrototypeBootstrapV1"
  profileId = "MPS-1"
  contentSnapshotHash
  entitlementProfileHash
  setupRulesHash
  empireOrdinal                 // для P1 обязан быть 0
  generationDate
}
```

Все hashes входят в итоговый world hash. Несовпадение с тем snapshot, для которого скомпилированы registries/scripts, является fatal error.

### 3.2 Выбранный дом

```text
HomeSelection {
  initializerId                // random_empire_init_01..06
  homeSystemId
  homeBodyId
  homeStarId
  selectionTraceId             // ссылка на native-like home-list trace
}
```

Bootstrap не выбирает body повторно и не расходует home-selection draw. Он проверяет, что trace действительно указывает на body из ordered home-list данного initializer execution.

### 3.3 Полностью разрешённый empire descriptor

```text
ResolvedEmpireDescriptor {
  countryTypeId
  playable
  name, adjective, shipPrefix
  flagDescriptor, colors, graphicalCultureId
  originId
  authorityId
  governmentId
  civicIds[]                    // в content/database order
  ethicIds[]                    // в descriptor order
  countryFlags[]
  lowTechStart
  advancedStart
  nomadic
  gestalt
  mainSpecies: ResolvedSpeciesDescriptor
  secondarySpecies?: ResolvedSpeciesDescriptor
  baselineTechnologyIds[]       // уже eligibility-resolved в DB order
  baselineResourceBalances{}    // обычно пусто/ноль до capital scripts
}

ResolvedSpeciesDescriptor {
  name, plural, adjective
  archetypeId
  speciesClassId
  portraitId
  nameListId
  traitIds[]
  idealPlanetClassId
  rightsProfileId
  citizenshipTypeId
  livingStandardId
  genderPolicy
}
```

Все семантические поля обязательны. Presentation-only строки могут использовать стабильные placeholders, но origin/authority/civic/ethic/species/trait/right/class IDs не могут быть выдуманы или пропущены.

`baselineTechnologyIds` разрешает setup layer, а не bootstrap. Для P1 допустима declared policy `EligibleStartTechV1`: пройти technology definitions в snapshot DB order, взять только `start_tech=yes`, whose DLC/potential/profile conditions истинны для окончательного descriptor. Этот resolver является отдельной аппроксимацией до закрытия native country factory. Простое добавление всех встреченных `start_tech=yes` запрещено.

### 3.4 Состояние мира и allocators

```text
BootstrapWorldInput {
  structuralWorld
  typeAllocators {
    nextCountryOrdinal
    nextSpeciesOrdinal
    nextColonyOrdinal
    nextDistrictOrdinal
  }
  registries
  compiledStartupProgram
}
```

IDs выделяются из отдельных typed spaces. Если runtime требует одно 32-битное поле, typed ID преобразуется его штатным typed-manager adapter; bootstrap не смешивает counters разных типов.

## 4. Обязательные registries

### 4.1 Нужны самому bootstrap

1. `country_types`.
2. origins/civics/authorities/governments и их resolved compatibility predicates.
3. ethics.
4. species archetypes, species classes, portraits, traits.
5. species rights, citizenship types, living standards.
6. planet classes, включая `district_set` и optional `starting_district`.
7. districts, включая actual DB traversal index, `default_starting_district`, potential/allow rules и occurrence schema.
8. technologies для уже выполненного `EligibleStartTechV1`/native resolver.
9. resources для проверки baseline balances.
10. name lists/graphical cultures/flags, если presentation layer не использует placeholders.

Любой unresolved semantic reference — fatal. Registry iteration всегда использует зафиксированный traversal index, а не map/hash/alphabetical order.

### 4.2 Нужны следующему startup interpreter

После bootstrap должны быть доступны полные typed registries выбранной `MPS-1` closure: deposits/blockers/modifiers, buildings, districts, zones, resources, technologies, ship designs, fleets/ships, leader classes/subtypes/traits, starbase modules/buildings, on-actions, events, scripted effects/triggers и inline scripts. Bootstrap только preflight-проверяет их compile/link status; он не исполняет их.

## 5. Детерминированные IDs и mutation order

### 5.1 Reservation order

Bootstrap сначала резервирует, но ещё не публикует IDs в таком порядке:

1. country;
2. main species;
3. secondary species, если profile когда-либо разрешит его; в `MPS-1` отсутствует;
4. colony;
5. default district occurrence, только если input district list пуст и selector вернул type.

Каждый typed allocator выдаёт следующий свободный ordinal и увеличивается ровно один раз на реально создаваемый object. Presentation placeholders, index records и scope frames ID не расходуют.

Поскольку spaces разделены, порядок выше не заявляется native numeric-ID order. Он является стабильным `PrototypeBootstrapV1` order и сохраняется в trace.

### 5.2 Transaction rule

Все mutations выполняются в private builder. До завершения preflight, linking и invariant checks ни один object не публикуется в `WorldSnapshot`. Любая ошибка откатывает reservations и mutations целиком. Частично созданная playable country запрещена.

## 6. Алгоритм `PrototypeBootstrapV1`

### Шаг 0. Проверить профиль

Требовать одновременно:

- `profileId=MPS-1`, `empireOrdinal=0`, `playable=true`;
- `originId=origin_default`;
- regular, non-gestalt, non-nomadic, non-advanced, non-low-tech;
- main species biological и не требует special startup branch;
- `secondarySpecies=null`;
- ни один civic не отмечен profile compiler как special-start;
- carrier type planet; flags `special_start_planet`, `planet_earth`, `void_dweller_habitat_1` отсутствуют;
- initializer принадлежит `random_empire_init_01..06`.

Условие «civic не special» проверяется compiled profile proof, а не hard-coded short list.

### Шаг 1. Проверить home graph

1. Разрешить system/body/star по ID и проверить generation tags/тип.
2. Body обязан быть непосредственным членом system, colonizable, иметь положительный size и валидный planet class.
3. Body должен присутствовать в recorded ordered home-list; `selectionTraceId` должен содержать выбранный index и уже учтённый local RNG draw.
4. System/body не должны принадлежать другой country; body не должен иметь colony.
5. Для чистого `MPS-1` district list обязан быть пуст. Общий native adapter мог бы пропустить default add при непустом list, но этот случай вне bounded fixture и здесь fatal.
6. Home class должен быть совместим с resolved ideal class/species profile и иметь standard playable district semantics.
7. Home system должен принадлежать связной structural topology и иметь валидный star link.

### Шаг 2. Разрешить semantic definitions

Разрешить каждый ID descriptor. Проверить origin/authority/civic/government/ethic/species compatibility и entitlement gates. Проверить, что все `baselineTechnologyIds` eligibility-resolved, уникальны и даны в DB order. Проверить compile/link готовность трёх on-actions и events `game_start.8/.12/.9`.

Bootstrap не исправляет несовместимый descriptor и ничего не выбирает случайно.

### Шаг 3. Зарезервировать typed IDs

Выполнить reservation order из §5.1. Идентификатор default district пока резервируется условно после выбора его type в шаге 7; до публикации транзакция остаётся атомарной.

### Шаг 4. Создать detached country record

Создать:

```text
Country {
  id, countryTypeId, playable
  name/adjective/shipPrefix, flag/colors/graphicalCulture
  originId, authorityId, governmentId, civicIds, ethicIds
  flags, lowTechStart=false, advancedStart=false, nomadic=false, gestalt=false
  mainSpeciesId = reservedMainSpeciesId
  secondarySpeciesId = null
  speciesIds = [mainSpeciesId]
  homeSystemId, capitalBodyId, capitalColonyId
  ownedSystemIds = [homeSystemId]
  colonyIds = [capitalColonyId]
  fleetIds = []
  technologyIds = baselineTechnologyIds
  resources = baselineResourceBalances
}
```

Country is not yet visible from global indexes.

### Шаг 5. Создать detached main species record

Создать record из resolved species descriptor:

```text
Species {
  id
  identityRootId = self
  ownerCountryId = countryId
  homeworldBodyId = homeBodyId
  archetypeId, speciesClassId, portraitId, nameListId
  name/plural/adjective
  traitIds
  idealPlanetClassId
  rightsProfileId, citizenshipTypeId, livingStandardId
  founder = true
}
```

Не создавать pop или pop-group. Их обязан создать `generate_start_pops` внутри `game_start.12`, иначе последующий native-like diagnostic должен завершить P1 failure.

### Шаг 6. Установить ownership/capital links

В private builder записать двусторонние связи:

- `system.ownerCountryId = countryId` и country owns system;
- `body.ownerCountryId = countryId`;
- `body.homeworldSpeciesIds += mainSpeciesId`;
- country home/capital body/system links;
- main species owner/homeworld links.

Если runtime различает ownership, controller и claim, P1 policy ставит owner/controller country и один capital-system claim. Это **P**; exact native claim/starbase side effects остаются P2 gap. Создание starbase не входит в этот bootstrap и не должно симулироваться флагом.

### Шаг 7. Создать и прикрепить colony

Создать один colony record:

```text
Colony {
  id
  carrierKind = planet
  carrierBodyId = homeBodyId
  ownerCountryId = countryId
  founderSpeciesId = mainSpeciesId
  dominantSpeciesId = mainSpeciesId
  capital = true
  foundationDate = request.generationDate
  popIds = []
  buildingOccurrences = []
  districtOccurrences = []
  zoneOccurrences = []
  variables = {}
  flags = {}
}
```

Записать `body.colonyId`, `country.capitalColonyId`, `country.colonyIds`. Проверить, что все три ссылки указывают на один reserved colony ID.

### Шаг 8. Выбрать и добавить default district

Native-like selector contract:

1. Если colony district occurrence list не пуст, ничего не добавлять. Для чистого `MPS-1` это уже отвергнуто шагом 1.
2. Если planet class имеет валидный `starting_district`, выбрать его.
3. Иначе выбрать global default district: последний definition с `default_starting_district=yes` в фактическом registry traversal order.
4. Definition обязан существовать, быть разрешён entitlement profile и допускаться planet class/district set при уже установленном country/colony context.
5. Создать один occurrence выбранного type с `level=1`. Если occurrence того же type уже существует, native add увеличил бы level на 1; в `MPS-1` существующий occurrence запрещён.

В vanilla 4.5.2 snapshot единственный активный global default — `district_city`; обычные habitable classes используют `district_set=standard`, поэтому ожидаемый pre-on-action state для `MPS-1` — `district_city level 1`. Class overrides (`district_rw_city`, `district_hab_housing`, `district_cosmogenesis_goverment`, `district_ark_city`) подтверждают, почему global constant нельзя применять к другим profiles.

Default district создаётся до `game_start.12`. Для обычной authority subsequent startup script добавляет capital building и ещё два `district_city`; при отсутствии других изменений итоговый city level становится 3.

### Шаг 9. Подготовить scope links

Построить и проверить следующие resolvers:

```text
country.capital_scope      -> home body/planet carrier
country.capital_star       -> home star
country.owner_main_species -> main species
colony.owner               -> country
colony.species             -> main species
colony.planet?             -> home body
colony.ship?               -> null
body.owner                 -> country
body.solar_system          -> home system
homeSystem.system_star     -> home star
```

`capital_scope` по native `scopes.log` ведёт из empire в capital **planet**, а не в colony. `carrier` также не является неявным alias colony. В `game_start.12` `this` — colony; переход `planet?` явно входит в body/carrier scope.

### Шаг 10. Сформировать action frames

Queue имеет ровно три записи в таком порядке:

```text
1. empire_init_add_technologies
   frame: this=country, root=country, from=null, fromfrom=null, prev=null

2. empire_init_capital_colony
   frame: this=colony, root=colony,
          from=mainSpecies, fromfrom=null, prev=null

3. empire_init_create_ships
   frame: this=country, root=country, from=null, fromfrom=null, prev=null
```

После записи 2 dispatcher обязан проверить `colony.popIds`/equivalent pop-group list. Пустой список — fatal P1 diagnostic; запись 3 не превращает такой мир в playable.

Правила стека interpreter:

- scope transition помещает прежний `this` в `prev` и сохраняет `root/from/fromfrom`;
- scripted effect call сохраняет frame, меняя `this` только при явном scoped call;
- event entry устанавливает `root` равным event target из queue и получает явно переданные `from/fromfrom`;
- creation callback входит в новый object scope, `prev` указывает на caller scope, `root/from/fromfrom` сохраняются;
- nullable `planet?`/`ship?` с null target делает scoped block no-op согласно grammar, но не меняет frame;
- handles `from/fromfrom` разрешаются при каждом обращении: script может заменить species object/identity, нельзя кэшировать его содержимое вместо handle.

`PrototypeBootstrapV1` сам не исполняет queue.

### Шаг 11. Публикация

После всех checks атомарно добавить objects и indexes в WorldSnapshot, зафиксировать allocator counters и append-only trace. Bootstrap не расходует galaxy-local, pruning или script/global RNG. Любая generated presentation name должна быть заранее частью resolved descriptor либо стабильным несемантическим placeholder; bootstrap не делает random-name draw.

## 7. Выходной контракт

```text
BootstrapResultV1 {
  status = BOOTSTRAPPED_START
  countryId
  mainSpeciesId
  colonyId
  defaultDistrictOccurrenceId
  homeSystemId, homeBodyId, homeStarId
  actionQueue[3]
  bootstrapTrace {
    contract/profile/snapshot hashes
    input object IDs
    definition traversal indices
    reserved IDs and mutation order
    default district decision
    invariant results
    approximationIds[]
    rngLedgerBefore, rngLedgerAfter
  }
}
```

Обязательные `approximationIds`:

- `prototype-country-species-constructor-v1`;
- `prototype-typed-id-order-v1`;
- `prototype-ownership-control-claim-v1`;
- `eligible-start-tech-v1`, если не используется доказанный native resolver;
- `presentation-placeholder-v1`, если применён.

`rngLedgerBefore` должен быть byte-for-byte равен `rngLedgerAfter` для всех streams.

## 8. Invariants непосредственно перед первым on-action

### 8.1 Ссылочная целостность

1. Все typed IDs уникальны и разрешаются в object правильного типа.
2. Country ↔ species, country ↔ colony, country ↔ system, colony ↔ body, species ↔ homeworld links двусторонне согласованы.
3. `country.capitalColonyId == body.colonyId == colony.id`.
4. `country.capitalBodyId == colony.carrierBodyId == body.id`.
5. `country.homeSystemId == body.systemId == homeSystem.id`.
6. Home star принадлежит home system.
7. Нет dangling index entries или второго owner/colony.

### 8.2 Семантическое состояние

1. Country соответствует exact `MPS-1` profile proof.
2. Main species founder/homeworld/owner настроены; secondary species отсутствует.
3. Colony помечена capital, принадлежит country и использует main species.
4. До `game_start.12` pop list пуст; это ожидаемое промежуточное состояние.
5. До startup buildings/zones отсутствуют.
6. Ровно один default district occurrence `district_city level 1` существует.
7. Baseline technologies уже установлены; special additions оставлены `game_start.8`.
8. Baseline resource balances валидны; normal starting resources оставлены `give_starting_resources_effect` внутри capital stage.
9. `capital_scope`, `capital_star`, `owner_main_species`, carrier links разрешаются без implicit coercion.
10. Action queue и scope frames совпадают с §6 шагом 10.

### 8.3 Детерминизм и честность результата

1. Bootstrap RNG draws = 0 во всех streams.
2. Повтор с теми же snapshot/request/allocator inputs создаёт тот же object graph, IDs, trace и world hash.
3. Unknown definition, invalid scope, отсутствующая baseline-tech closure или неготовый startup program — fatal до публикации.
4. Output маркируется `BOOTSTRAPPED_START`, не `PLAYABLE` и не `VANILLA_COMPLETE`.

## 9. Post-bootstrap gates до `GENERIC_PLAYABLE`

1. Выполнить `game_start.8` в country frame.
2. Выполнить `game_start.12` в colony frame с `from=main species`, `fromfrom=null`.
3. Проверить положительное население и отсутствие dangling pop/species/colony refs.
4. Выполнить `game_start.9` в country frame.
5. Проверить science и constructor fleet/ship, scientist ownership/location/assignment.
6. Проверить capital building/district/zone/resource anchors, connected topology и navigable capital system.
7. Только после общего `ValidateForPlay` повысить status до `GENERIC_PLAYABLE`.

## 10. Что остаётся для vanilla-complete P2

`PrototypeBootstrapV1` устраняет P1 algorithmic blocker, но не закрывает:

1. exact native country/species/colony allocator и numeric IDs;
2. random/prescripted empire construction, governments, names и flag generation;
3. exact start-tech evaluator и native database/override order при mods;
4. native rights/identity/dominant-species initialization callbacks;
5. secondary species и `fromfrom` mutation branches;
6. gestalt, machine, hive, lithoid, individual-machine и synthetic starts;
7. Earth, void habitat, arkship, shattered ring, remnants и другие special carriers/origins;
8. advanced empires, guaranteed colonies, fallen/marauder/nomad/pre-FTL passes;
9. exact ownership/controller/claim/starbase creation and notifications;
10. hidden date/modifier/cache/event-target side effects native constructors;
11. exact cross-type allocation chronology и failure/OOM behavior.

Эти пункты не являются blockers для declared `MPS-1`, потому что профиль исключает их либо получает значения явными resolved inputs. Они остаются обязательными до утверждения `VANILLA_COMPLETE`.

## 11. Источники snapshot

Stellaris Cygnus 4.5.2 (9776):

- `common/on_actions/00_on_actions.txt`, SHA-256 `7693E64FBE974F37DB0C68969D10F7BDEEC585CE59CA497B5E27ECD69FF8F306`;
- `events/game_start.txt`, SHA-256 `8171B1806103AC96EB82CB5A8ECA2712232764EA7712B1AA16049724268DB528`;
- `common/scripted_effects/01_start_of_game_effects.txt`, SHA-256 `232925D6D7269532B8E146E60AF4AD2AD33ED60B75AF16888514685A1D6C4CB5`;
- `common/solar_system_initializers/empire_initializers.txt`, SHA-256 `B344E38EE6A2C66C1A2C3BB0F90C5E52BE21CF6E8396C00C27EAC8EBE36F269E`;
- `common/districts/00_urban_districts.txt`: `district_city`, `default_starting_district=yes`;
- `common/planet_classes/00_planet_classes.txt` и `06_planet_classes_nomads.txt`: class-specific `starting_district` overrides;
- native ranges `0x14097B6A0`, `0x14097A180`, `0x14097A850`, colony attach `0x140268420 → 0x140DAB550 → 0x140DC8FE0`, district add `0x140DCA140`.

