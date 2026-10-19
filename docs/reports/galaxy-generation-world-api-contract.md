# GALAXY-WORLD-API-001

Цель — подключаемый producer в Core, без изменений Session/M3, server/client и
планетного прототипа. Пользователь разрешил документированные отклонения от native.
Это не разрешает скрывать неподдержанный контент или объявлять скелет играбельным.

## Вход и воспроизводимость

Lobby catalog импортирует все сценарии и поддержанные ими формы; размеры, радиусы,
диапазоны density/empires/gateways/wormholes и source defaults берутся из snapshot.
Immutable request содержит пользовательский seed, scenario/shape, density,
habitable multiplier, ordered player slots и явно выбранный profile.
Prototype profile использует раскрытый ordinary pool из 11 definitions и шесть
generic empire initializers; особые origins/advanced/fallen/marauder/nomad,
pre-FTL, gateways/wormholes и DLC events требуют другого profile и не пропускаются
при ненулевом запросе. VanillaComplete пока возвращает typed unsupported.

SeedBundleV1 использует SHA256 по UTF8 length-prefixed version/domain/userSeed,
little-endian int32 lengths и первые 4 hash bytes little-endian, mask0x7FFFFFFF.
Домены galaxy, prune-a, prune-b, script-0, script-1, names, content раздельны.
Пустой seed генерируется host один раз при resolve и возвращается для replay.
Settings fingerprint включает resolved seed/settings/ordered slots/profile и
content fingerprint; culture/порядок словарей не влияют на результат.

## Pipeline и обозначенные отклонения

Placement и relaxation используют принятые native stages/проверенный Triangle.
Для prototype group assignment разрешён отдельный deterministic spatial-BFS policy
по candidate adjacency; параметры размеров/bridges берутся из scenario, а mapping
не выдаётся за восстановленный native partitioning. Home selection использует
deterministic farthest-point spread с RNG tie/start, а не native empire clusters.
Pruning принимает sampled group targets; connectivity repair сохраняет каждый
восстановленный edge/reason. Все policies входят в manifest и fingerprint.

Ordinary и home initializers выбираются из полного раскрытого profile pool,
исполняются structural executor; class-less planets/moons сохраняются.
Каждая home allocation ссылается на реально созданное home body, включая nested
moon. Deferred effects/neighbors/post-content остаются явными обязанностями
consumer; producer не помечает их исполненными. Для test prototype допустимо
отдельное declared approximation policy без ванильных neighbor/resources, но
его acceptance не сертифицирует VanillaComplete.

## Handoff

Consumer-neutral immutable snapshot: stable IDs, fixed galaxy coordinates,
local polar/cartesian body coordinates, parent IDs, class/size/name/flags,
hyperlanes, home slots, ordered deferred content requests и manifest.
World publish атомарен: неизвестный класс/некорректный orbit/dangling reference,
отсутствующий home/звезда или разрыв graph блокирует результат.

Planet startup задаётся consumer hook по точному home body class/size/initializer,
а не через повторный random homeworld selection. Core/Galaxy не импортирует
Construction/Startup или Session. Integration harness может вызывать уже
существующий StartupBuildingImporter read-only, передавая generated class/size,
и проверять source-derived district/building/zone anchors. Это доказывает границу
подключения, а не выполнение всего game_start event interpreter.

ValidateForPlay принимает consumer receipts для country/species/colony/economy
и playable actors, проверяет ссылки и соответствие home bodies. Без receipts
статус только ReadyForBootstrap. Receipt с unsupported required effects не
принимается. Нельзя заменить startup одним bool/маркером: receipt содержит
реальные IDs/definitions/положительные population и actor composition.
Независимое preflight review дополнительно требует actual consumer entity snapshot:
lookup каждого receipt ID/definition, exact world/settings/content fingerprint,
unique bijection всех requested slots→country/species/capital/home. Каждый deferred
request получает Executed/AcceptedApproximation/Unsupported с profile/policy/evidence;
неразрешённый required request блокирует Playable. StartupImporter smoke сам по себе
сохраняет статус ReadyForBootstrap. Эти условия приняты до implementation.

## Приёмка

Source-backed tiny200 smoke: минимум два seeds, все tiny shapes, density0.5/1/3,
one/two slots, deterministic replay, unique IDs/acyclic orbits/connected graph,
home class/size, actual ordinary diversity. Отказы: несовместимая shape,
density вне bounds, repeated slot, unsupported profile/content, changed snapshot.
Отдельный mapping/bootstrapping smoke вызывает существующий startup importer и
проверяет anchors для generated home. Все code paths проходят независимое review
до commit; runtime assets/binaries остаются вне Git.
