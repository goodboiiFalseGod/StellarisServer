# GALAXY-M3-INTEGRATION-001

## Актуальный handoff — исправление wire overflow, 14 октября 2026

Ветка feature/galaxy-lobby-wiring, reviewed base/HEAD
820e35933e9e83ba176e6282876dccca3f60c6ad. Это прежний принятый wiring commit
(в RC перенесён как093131c); visual dependencies595b0c7/79cb0ad уже отдельно
приняты и находятся ниже base. Новые owned изменения пока не закоммичены.
Координатор/интегратор01a13770; текущий Galaxy writer01a13774-72a2-7a70-b693-7fd9ed17cf32.
Настройки переданы gpt-6.1-sol/medium, backend proof недоступен.

Owned paths текущего fix: Session/M3 M3DevelopmentProtocol.cs,
M3DevelopmentProjection.cs, M3GameSession.cs; tests/GalaxyM3Checks
GalaxySessionChecks.cs; tests/M3ServerChecks Program.cs; этот report.
UI/renderer/assets/csproj/research engine не менялись. UI decoder пишет отдельный
owner по согласованной схеме ниже. Original installation read-only;
main/master/push/сервер5192 не трогались. Own HTTP/WS smoke использует случайный
loopback port и самостоятельно останавливается; чужие процессы не затрагивает.

Точная причина: после реального production start publisher получает
InvalidDataException «Исходящее сообщение превышает 1 MiB.» и закрывает WS.
До исправления huge/maxdensity host1671243/peer1673048 bytes; canonical catalog
~854 KiB, sourceRecipe674573, compatibleSlotIds~65 KiB. Legacy colony gates=[],
не причина. Прежние849946/848172 замеры general observer НЕ доказывали player
budget: general development.colonies пусто. Предварительный851069 относился к
другому research draw. Red evidence: production-budget-red.txt;
first-chance cause: generated-transport-diagnostic.txt (ignored artifacts).

Fix только lossless wire metadata: таблицы terms/строк/slot sets в разрешённой
development проекции, индексы вместо повторов. Все definitions, source fields,
порядок/кратность terms и slot IDs, unknown reasons, quotes, eligibility, balances,
installed graph, queue/history сохранены. MAX_OUTGOING_BYTES остаётся1048576.
Общие source/economy/generation правила не менялись. Optional null metadata
sourceTier/canAfford/upgradeOperationGate/sourceRecipe/compatibleSlotIds может
быть omitted; nonnull и empty arrays сохранены, неизвестные term.value/expression
остаются explicit null. Preset-only human решение продолжает действовать.

Regression теперь использует ТОЧНЫЙ production constructor: legacy construction
projection + canonical construction/economy/research + real technology keys +
generated manifest, два разных ordinary presets. Измеряются all5POVs:
host/peer/general/observer-player1/observer-player2 с richest nonhome inspect.
Проверяются small400/density1 и huge1000/source maxdensity3, active research,
source-funded month, pending construction, настоящее completion и finite history64
на каждом owner. Без fake funding/grants/cost overrides.

Финальный interned прогон13 Session PASS: huge/maxdensity после history64+pending1
обоих owners максимум1007294 bytes (запас41282); small максимум564106.
Проверены aliases read→write, first-index map и paired-table guards. Final logs:
artifacts/galaxy-m3-integration/production-budget-final-freeze.txt,
artifacts/galaxy-m3-integration/generated-transport-final-interned.txt.
Финальный interned real HTTP/WS560 PASS/clean shutdown. Проверки privacy/replay/
rate/pending/month/navigation сохранены. Release0warnings/errors.

Finite proof НЕ покрывает runtime history bound4096. Orders/tombstones не удаляем.
Длительная сессия с тысячами orders требует отдельного budget/retention контракта;
нет гарантии любого состояния до4096. Текущий supported M3 limit2players+1observer
проверяется отказом third Join; configured maxParticipants10–20 пока отсутствует,
не заявляем его реализованным/измеренным. General измеряет текущий max2 economies.

Независимое affected review — galaxy_wiring_review через координатора;
автор не self-approves и не коммитит до приёмки. Затем explicit owned commit,
selective pickup только его, safe writer transfer. Context audit input324867/475000
(cached320512,compactions0,rotation=true); после atomic cycle нужна fresh-context
роль с теми же model/effort. Successor rare_resource_session пока read-only,
writer передаётся явно через root после commit. Новых chats/heartbeat здесь нет.

Следующий срез НЕ начат: all7 rare resources motes/gases/crystals/zro/darkmatter/
livingmetal/nanites через generic existing economy/storage/flows/cost/upkeep/UI,
ordinary source producers; пользователь запретил новые special acquisition
systems сверх реализованного. Growth/perks/orbital/anomalies/events не добавлять.
Genuine research→paid upgrade остаётся gate; найден minerals-only candidate
building_autochthon_monument→building_heritage_site, starting capital tier2 подходит,
нужны actual quote/effects/tier2 society prerequisites. Laser/space/power research
completion и foundry paid340/completion не подменяют paid upgrade. Source stocks
и precise heritage facts ещё не сохранены. Rare owner отдельно исследует closure;
Session writer передаётся только после текущего атомарного cycle.

## Согласованная lossless catalog wire схема

Без shared tables legacy inline recipe objects и compatibleSlotIds остаются valid.
При shared tables development содержит sourceRecipeTerms и catalogSlotSets (оба
arrays, включая empty), sourceRecipeEncoding="interned-strings-v1" и
sourceRecipeStrings:string[]. Term row ровно6:
[kindIndex,targetIdIndex,value,expressionIndex,conditionDispositionIndex,applicationScopeIndex].
Позиции0/1/4/5 — nonnegative integer index в strings;3 — index|null;2 — finite
number|null. Строки могут иметь aliases; writer выбирает первый ordinal index,
expanded semantics не меняется. Без encoding/string table старый tuple6
[string,string,number|null,string|null,string,string] явно поддерживается.
Unknown encoding, malformed row/type/index и incomplete paired tables — отказ.

Entry sourceRecipeIndices:int[] ссылается на terms, compatibleSlotSetIndex:int
на catalogSlotSets:string[][]. References OPTIONAL: absent/null ref означает
absent/null inline, не превращать unknown в[]. Empty recipe имеет explicit[],
empty slots — index на[]. Present refs decode строго; nonnull inline вместе с
shared tables rejected как ambiguous. Порядок, кратность и все6 полей восстанавливаются.
Таблицы строятся после POV filtering; general/pending не получают private metadata.
Codec roundtrip покрывает gated nullable/empty entries, duplicates, source unknowns,
legacy tuples, string aliases, invalid string/recipe/slot refs и encoding.

## Final wire freeze — 14 октября 2026

После clean bootstrap `feature/galaxy-lobby-wiring@d9178a4` координатор
`01a13770-e8f0-7df2-8002-e456e71a74f8` явно передал единственное владение
Session/Protocol/Server и exact lookup M3StartupCatalog текущему Galaxy writer.
UI/renderer/assets/csproj и stable/main не назначены. Коммит кода — только после
независимого actual-diff review; backend proof модели недоступен.

Wire (camelCase): `state.galaxy` = stage (`unprepared`, `awaitingAssets`,
`running`), settings (`seed`, `scenarioId`, `shapeId`, `density`), scenarios,
canPrepare, contentProfile, coverage, optional worldFingerprint/systemCount/
bodyCount/hyperlaneCount. `setGalaxySettings` принимает galaxySeed,
galaxyScenarioId, galaxyShapeId, galaxyDensity; только host+lobby, атомарная
валидация, пустой seed разрешается один раз. `prepareGalaxy` без аргументов:
host+lobby, два connected player с выбранными presets; staged exact world/home/
startup/actual economy IDs и dynamic manifest публикуются только вместе.
Settings/template снимают candidate и все ready/assetReady; каждый успешный
prepare также снимает все ACK/ready, даже при совпадении profile и world hash.

`assetReady` = ready/profileId/profileDigest/galaxyWorldFingerprint;
`ready` = ready/profileId/profileDigest/galaxyWorldFingerprint;
`start` = profileId/profileDigest/galaxyWorldFingerprint. Все три требуют текущий
candidate и совпадение fingerprint/profile; missing/stale →
`STALE_GALAXY_CANDIDATE`. После ACK start использует сохранённый candidate,
без regeneration и `_startup.Select`; existing economy/construction/research
transaction сохраняется. GET `/m3/asset-manifest` возвращает текущий manifest.
До candidate asset ACK не принимается; template/settings/prepare доступны до ACK.
Fixture-only legacy dependency сохраняет прежний isolated protocol.

`inspectGalaxySystem` = systemId/galaxyWorldFingerprint, только running и
permitted POV; пустой systemId очищает detail. Reconnect/смена POV очищают detail.
`system.starClassId` = exact `sc_*`; body дополнительно worldClassId/parentBodyId,
kind = star/planet/moon. Внутренний moon-as-planet validator не меняется.
Новейшее human решение: production только preset, configure custom Traits/Buildings
отклоняется `PRESET_CONFIGURATION_ONLY`; legacy fixtures остаются изолированы.
Seam `Func<classIds,M3AssetManifest>` сохранён для tests; production default строит
real requirements manifest и проверяет all actual sc lighting до publish.
Ошибка не подменяет manifest старым профилем.

Gates: atomic failures startup/construction/research, stale/reconnect и
same-profile/new-world, observer/general/player privacy, полный serialized Session
huge/maxdensity < 1 MiB, реальные movement/month/construction/research;
browser two-player QA после visual acceptance. Статус ReadyForBootstrap,
без заявления native complete.

14 октября2026. Прямое поручение: согласовать интеграцию с координатором, подключить
seed/размер/форму/плотность лобби к реальному M3 миру, сохранить планетную логику,
выполнить независимое review и playable launch QA. Producer base07975d9; ветка
feature/galaxy-m3-integration. Актуальная shared source-world base/ownership запрошена
координатору01a134f5; общие Session/client/renderer файлы до согласования не меняются.
Координатор подтвердил base25d3cb6, serialization existing sharedpaths после
catalog/upgrade/research writer; UIclient owner отдельный, generatedvisual owner
01a1371f-c340-7270-a937-80e15e5cec55. Безопасно создана feature/galaxy-m3-rc от25d3cb6,
перенесены только пять reviewed producer commits: итоговый imported producer b01523c.
Текущие новые Session modules не пересекают existing sharedpaths.

## Поведение и границы до кода

Host меняет source-derived galaxy settings только в lobby. Изменение снимает player
ready; peer не может менять настройки. Seed разрешается один раз host и возвращается
в sanitized lobby view. Scenario ограничивает shape/density; invalid request
отклоняется без изменения settings/ready/мира. Start generation failure оставляет
lobby и existing economy state; fixed16 не является fallback в production route.

Start строит staged generated SpaceWorld и startup на exact generated home class/size.
Существующие source economy/construction/traits/research pipelines исполняются в
их прежнем порядке и транзакции; не переписываются ради Galaxy. Commit world/colony/
economy/settings hash только после успешной инициализации. Fixture-only session tests
могут явно использовать отдельный legacy dependency, production получает generator.

Generated systems/lanes/body IDs стабильны; class/parent/native entity visual binding
сохраняются. Moon отображается как planet body с semantic moon kind/parent. Class
связывается с локальными game assets; universal continental/star.g replacement
запрещена. Профиль PrototypeOrdinaryV1 и content omissions видны пользователю кратко;
raw script/source paths/RNG ledger в wire не передаются.

## Приёмка

- Source-backed two-player session starts from seed/scenario/shape/density; repeated
  seed+settings даёт тот же world fingerprint; изменение каждого control меняет
  соответствующие systems/coordinates/topology. Tiny200 и small400 проверены.
- Invalid/non-host/settings-after-start errors атомарны. Generation failure не
  публикует partial world и не меняет source economy/ready/startup.
- Exact generated home/body/colony matches class/initialsize/startup finalsize;
  реальное population/jobs/district/building/zone состояние используется существующим
  planet runtime. Дневной/месячный tick и construction path сохраняют регрессии.
- Local asset/profile validation; generated star/planet/moon visible; navigate fleet
  через generated lane; open own colony и выполнить действующий economy action.
- Независимое actual diff review; Release+focused session/client tests; isolated
  loopback server на согласованном свободном порту, browser two-player lobby/start,
  controls/map/colony/navigation/step QA. Existing server5192 не трогать.
- Owned reviewed commits; no stable/main push/merge без принятого общего RC route.

## Adapter candidate / UI contract

Новые owned files: `M3GalaxyRuntime.cs`, `SpaceWorld.Generated.cs`,
`M3GalaxyProtocol.cs`, отдельный `tests/Stellaris.GalaxyM3Checks`.
Prepare(settings, frozen player/economy/template bindings, exacthome→startupplan)
возвращает staged Space/Startup/Generated/ClassIds/BodySemantics. Базовый seed
выбирается один раз per runtime; host blank seed resolve возвращается для replay.
M3GeneratedDisplayV1 сохраняет semantic class/size/parent; localcoordinates1:1,
galaxyfixed/100000, visualscale=clamp(finalsize/20,0.1..5), radius covers bodies/actors
+40 и minimum EXIT_RADIUS+20. Это presentation policy, не native size formula.
Undirected lane endpoints canonicalized для existing M3 validator; geometry не меняется.

Координатор утвердил SourceFirstNumberedEntityV1 / requirements version1:
ClassIds sortedunique, VisualId=`world.`+exactclassId. Source entity/variant выбирает
asset profile, не Session. Descriptor entity override этим профилем явно rejected.
Следующий shared handshake: host setGalaxySettings(GalaxySeed,GalaxyScenarioId,
GalaxyShapeId,GalaxyDensity) → allready reset/invalidate candidate;
host prepareGalaxy() при двух selected presets → staged world+requirements+dynamic
manifest → allassetready/ready reset, stageawaitingAssets;
existing assetReady bound profileId/digest → ready → start publishes frozen candidate
and existing economy transaction. No prepare/regenerate inside finalstart послеACK.
UI owner получает M3GalaxyView в `state.galaxy`: Stage,Settings,Scenarios,CanPrepare,
ContentProfile,Coverage и optional fingerprint/counts, без rawsources/scripts/RNG.
Template selection/settings/prepare должны быть возможны до dynamic assetReady,
иначе возникает handshake cycle. Existing legacy tests без Galaxy dependency
изолированы, production route fixture fallback запрещён.

Проверено adapter candidate:5PASS (`artifacts/galaxy-m3-integration/adapter-checks.txt`):
tiny200/2251spatialbodies/288lanes/24classvisuals; actualseed/shape/density/size400
influence+replay; exactgenerated home→sourceplan; generatedlane travel; invalid
density beforestartup; existing sourceeconomy positivejobs/workforce and successful
real month. Browser/sharedSession integration ещё pending; это не общий playablePASS.

Adapter slice accepted независимым `/root/galaxy_review`, attempt1, открытых замечаний
нет. Review SHA `C307FC2307432625280F13D039D910AE2588A35CB9A8AD25B6A52E3E6D8D3273`.
5focused checks PASS, Release0warnings/errors. Shared handshake/actualbrowserstart
не входят в эту промежуточную приёмку и остаются обязательными следующими действиями.

## Wire budget follow-up

Actual full `M3Projection.Project` для generated small400 measured1149439bytes,
что превышает существующий `M3WireCodec.MAX_OUTGOING_BYTES=1048576` ещё безeconomy
иresearch. Нужен bounded detail projection, не повышение лимита/фиктивное удаление
тел из мира. Новый `M3GalaxyProjection` сохраняет all permitted systems/lanes/star
proxies, visible capital systems и выбранную inspected system с телами/exits.
Пересечение с существующими permittedSystems/permittedBodies обязательно.
Host keeps fullworld/fingerprint; perparticipant inspectGalaxySystem(SystemId) только
выбирает detail payload, не меняет мир. Shared command/projection wiring ждёт передачи
владения; UI owner связывает camera system entry с inspect command.
Follow-up independently accepted (no findings), report finalSHA
`714EFFA1C0F7A473D636DBE860B518BAA71EFF15FA0B8B3282F6C194ACD186C3`.
6focused PASS: small216281bytes/557projectedbodies of4517, huge514027bytes/
1287of11126. Это spatial payload, full Session transport ещё проверяется послеwire.
Generated assetReady/ready/start должны нести GalaxyWorldFingerprint и отклонять
stale candidate readiness; fixture-only legacy protocol остаётся изолированным.

## Согласование shared integration

Единственный координатор общей экспериментальной RC — чат
`01a1372f-401f-75d2-90b3-9a9306ae881c`. Producer, adapter и bounded projection
перенесены выборочно; projection в RC — `89569cf`. Общие Session/Protocol/Server
пути передаются Galaxy после принятия численных research effects; до явной
передачи их здесь не меняем. Следующая wiring ветка создаётся от точного нового
канонического RC, без переноса целиком базы `25d3cb6` с другим research runtime.

Уточнения согласованного wire proposal:

- `inspectGalaxySystem` содержит `systemId` и `galaxyWorldFingerprint`; пустой
  `systemId` очищает детали при возврате на карту. Устаревшее подтверждение или
  запрос деталей отклоняется `STALE_GALAXY_CANDIDATE`.
- `system.starClassId` передаёт exact `GalaxyWorldSystem.StarClassId` по stable
  system ID. Это исходный `sc_*` класс системы, отдельный от `pc_*` класса тела;
  выбирать свет по первому звёздному телу нельзя.
- Body DTO сохраняет `worldClassId`, `parentBodyId` и semantic `kind`
  (`star`, `planet`, `moon`). Legacy fixture может не иметь этих metadata.
- Settings/template/prepare доступны до asset ACK; изменение bindings снимает
  готовность и инвалидирует подготовленный candidate. После prepare клиент
  получает новый asset profile и запрашивает актуальный manifest, затем
  подтверждает профиль и fingerprint. Start публикует сохранённый snapshot.
- Новый seed может сохранить тот же набор классов и profileId/digest при новом
  worldFingerprint. Каждый успешный prepare сбрасывает ACK/ready; подтверждение
  нового fingerprint обязательно даже при повторном использовании уже
  валидированного локального профиля. UI не отслеживает готовность только по
  assetProfile. Нужен regression same-profile/new-world: старый ACK отклоняется,
  валидный локальный профиль переиспользуется с новым ACK текущего candidate.

Чат реверса выполнил узкую сверку consumer seam: новых native blockers нет.
Обязательны full-world hash, exact recorded home body binding и stale lifecycle
regressions. Current fingerprint включает schema/settings/content/SeedBundle,
systems/bodies/lanes/homes, policies, deferred identities и stage RNG continuation;
не является hash ограниченной wire projection. Core readiness сохраняется
`ReadyForBootstrap`; actual experimental M3 gameplay не подменяет строгую
native/bootstrap сертификацию. Species marker ради формального PASS не создаётся.

Независимый reviewer подготовил observable regression сценарии для actual wiring:
atomic startup/construction/research failure, реальные economy planet IDs,
pending/general/player observer privacy, очистка inspected details при смене POV,
полный serialized session payload с economy/research менее 1 MiB и навигация
по generated lane. Это подготовка к будущему ревью, не приёмка ещё отсутствующего
shared integration и не browser playable PASS.

Дополнительно проверен source-derived верхний предел плотности `huge` (3):
1000 систем, наиболее насыщенная телами nonhome inspected system, spatial payload
628578 байт; density1 — 516443 байт. Small400 с тем же выбором деталей — 218928 байт.
Все 6 adapter checks PASS, process exit0; log
`artifacts/galaxy-m3-integration/projection-boundary-checks.txt`.
Test-only diff независимо принят `/root/galaxy_review` без замечаний; `<750000`
и privacy checks сохранены. Full-session budget с новыми metadata/economy/research
по-прежнему проверяется после actual shared wiring.
