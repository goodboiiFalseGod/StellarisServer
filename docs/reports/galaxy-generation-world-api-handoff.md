# GALAXY-WORLD-API-001 — frozen candidate

14 октября2026; base `090d1bc`; author `/root`. Независимое attempt2 code review accepted.
Модель/effort не менялись, usage unavailable. Core producer и CLI остаются в
изолированной feature/galaxy-generation; Session/M3/планетный модуль не изменены.

## Реализованная граница

`GalaxyLobbyCatalog.Import(root,entitlements)` → options для5сценариев, source
shape/density/empire/gateway/wormhole ranges и defaults, priority order.
`Resolve(request)` → immutable resolved settings/seedbundle/content+settings hashes
либо typed failure. Null/empty/whitespace seed генерируется host один раз при
resolve и возвращается в resolved settings для replay. Entitlement profile задаётся явно.

`GalaxyWorldGenerator.Generate(resolved)` → атомарный `ReadyForBootstrap` snapshot
или typed stage failure. Pipeline: native M12 placement+handles → Triangle
relaxation → explicit spatial-BFS partitions/home spread → pruning+connected
completion → ordinary/home structural execution → exact home body selection.
В world есть системы, тела/иерархия/локальные координаты, звёзды, lanes/repair
reasons, home allocations, deferred requests и stage/RNG manifest.

PrototypeOrdinaryV1 раскрывает pool11ordinary+6generic home initializers.
Ordinary usage_odds source-weighted; generic homes uniform либо explicitID.
SpatialBfsPartitionsV1 использует source min/max systems/bridges, но не заявляет
native partitioning: home seeds первыми; open groups растут BFS по shortest
candidate neighbors, home sites защищены, undersized groups не объединяются.
FarthestHomeSpreadV1 выбирает первый site через content stream, далее maximin
по squared galaxy distance, tie bysite. ContentLocalStreamV1 отделён от geometry;
script0/script1/names seeds зарезервированы дляconsumer, не считаются выполненными.
Каждое отличие/неисполненный content path остаётся в metadata.

`GalaxyBootstrapValidation.ValidateForPlay(world,actualConsumerSnapshot)` проверяет
fingerprints, bijection slots/country/species/colony/economy, exact generated
class/initialsize, ownership, actual entity IDs/definition refs, positive population,
district/building/zone anchors, fleets/ships и disposition каждого deferred request.
Профиль явно требует `PrototypeNavigationActorsV1`; accepted approximations только
`PrototypeContentOmissionsV1` с evidence. Validator не создаёт сущности и не
доказывает исполнение startup. Synthetic acceptance fixture проверяет протокол,
не считается runtime gameplay QA.

## Проверено

Combined genuine+Triangle checks **78 PASS**, log
`artifacts/galaxy-integration-ready/world-final-checks.txt`:

- все4tiny shapes; density0.5/1/3; one/two slots; deterministic repeated world hash;
- independent Python SHA256/length-prefix/UTF8 seed golden vector;
- nested home moon random_empire_init_03;
- huge1000/spiral4:11091bodies/1407lanes/1home;
- source startup importer для generated homes random_empire_init_02/04,
  classpc_continental/size20: finalsize20,4districts/4buildings/3zones,0gates;
- invalidshape/density/duplicateplayers/specialcontent/strictvanilla profile;
- stale consumerhash, zeropop, danglingfleet, missing/unsupporteddisposition reject.

CLI `tools/Stellaris.GalaxyHarness`: buildRelease0warnings/errors, options export
и actual generate accepted. User sample seedgalaxy-prototype-452-v1:
200systems/2232bodies/279lanes/2homes/6710explicitrequests.
Worldhash `ADEF50DEE52516BBB9A42721549458BD93191A7B42253555A887B388203867AB`;
contenthash `90D5F7C6C9EDD7DBBC952D97B2A9BDA1996A8AEA8A7729E835EDF8D4C6CA445A`.
CLI экспортирует metadata; оригинальные script blocks/assets не сериализуются.

## Подключение и реальные остатки

See `galaxy-generation-prototype-mapping.md`: consumer factory произвольного
SpaceWorld, route session start, точный lookup/generation startup plan по generated
class/size вместо повторного M3StartupCatalog.Select, применение существующего
startup/economy state и создание navigation actors в одной transaction.
Это будущие consumer edits; stable не менялся, gameplay launch не выполнен.

Полный vanilla runtime ещё не поддержан: специальные misc/origin/custom/fallen/
marauder/nomad/preFTL content, neighbors/guaranteed colonies, script effects,
random deposits/modifiers/anomalies/rings/naming, nebula/gateway/wormhole stages,
full native clusters/partitions и complete empire startup interpreter.
360/360 structural compile не означает их runtime execution. Strict profile
VanillaComplete и ненулевой special-content request отклоняются до world publish.
В prototype deferred omissions допустимы только с явным consumer disposition;
без actual consumer state status остаётся ReadyForBootstrap.

## Итоговая приёмка и зависимости запуска

Review `galaxy-generation-world-api-review.md` SHA256
`ABD46C8BBC639DE89F2CF60AD35E69FCED2F2D379A89B56A7A17527AC753DDC8`:
две P2 исправлены и повторно приняты; открытых замечаний нет. Final combined
**78 PASS**: `artifacts/galaxy-integration-ready/world-reviewed-checks.txt`.
CLI rebuiltRelease0/0; special strict request exit1/no output; valid CLI rerun
сохранил приведённый world hash. Actual gameplay не выполнялся.

Для запуска нужны .NET8 SDK, локальный read-only Stellaris4.5.2 snapshot и внешний
Win64 Triangle helper. Его build recipe: `tools/galaxy-triangle/Build-TriangleBackend.ps1`
с already-installed MSVCx64, pinned official Triangle1.6 C/H в ignored directory.
LLP64 port только pointer/alignment/diagnostic casts; SINGLE /fp:strict /O2,
worker SHA `CA2D48F5CDB782AE7E01518F72298F3030996B88912E38D3A0D5646E71585F71`.
Helper/binaries/upstream/game assets не входят в Git. Upstream Triangle license
разрешает private/research use; commercial distribution требует отдельного
разрешения — см. принятую analytical handoff `galaxy-generation-triangle-backend-handoff.md`.
Это dependency последующей release packaging, а не новая native reconstruction задача.
