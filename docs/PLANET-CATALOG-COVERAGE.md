# Integrating the complete zone and building catalog

Latest instruction, 16 October: all buildings/zones in one catalog, filtered
by conditions; jobs remain separate. The registry is fully connected: 498 buildings + 146 zones.
Catalog import and execution support are described separately below. No browser/fixture.

## Current baseline and next step

Baseline `46e30a4`, 17.10.2026, Stellaris4.5.2. Working copy:
`planet-menu-catalog-continuation/StellarisServer`, branch
`feature/construction-condition-evaluation`; основной checkout не менять.
Before work: `Get-Location`, `git branch --show-current`, `git rev-parse HEAD`,
`git status --short`; preserve others' diffs. Handoff contains only exact current
state/owners/checks/next step; history is referenced by commit links.

Verification context: actual source startup seed 123, economic/scientific/military;
InitializeColony, HUM owner/founder/population, starting tech, positive CG upkeep,
complete foreign-ownership and completed-district registry. The system graph is
not passed here; its separate regression does not change this baseline's composition.
Inventory648 = buildings498 + zones146 + districts4, economy194, jobs closure59.

| Пресет | potential F/U/T | eligibility F/U/T |
| --- | --- | --- |
| economic | 396/55/197 | 504/42/102 |
| scientific | 396/55/197 | 504/42/102 |
| military | 395/55/198 | 504/42/102 |

Reproduction (isolated build, without servers/fixture/grants):

```powershell
dotnet build tests/Stellaris.M3ServerChecks/Stellaris.M3ServerChecks.csproj -c Release --artifacts-path artifacts/catalog-district-presence
dotnet artifacts/catalog-district-presence/bin/Stellaris.M3ServerChecks/release/Stellaris.M3ServerChecks.dll --planet-menu-source-only --catalog-district-presence-only --game-root <local-installation-4.5.2>
```

Evidence: `artifacts/catalog-district-presence-source.log`, district72,
full-catalog368/system32 PASS, Release0/0. Do not copy native scripts into Git.
The test prints actual district counts, ID transitions, and aggregates; numbers alone
do not confirm native mechanics or quote availability. The complete criterion is above
PLANET-CONSTRUCTION-RUNTIME.md.

Strengthened TRANSITION-ACCOUNTING-001 checks: source85/Release0/0 PASS,
`artifacts/catalog-transition-accounting-build.log` и `-source.log`.
For this run, replace the artifacts path with `artifacts/catalog-transition-accounting`
in both commands. The exact 42-ID baseline and 8 potential/2 eligibility transitions
are checked independently of actual rows; negative accounting 7 catches specific errors.
Root accepted the final test diff, independent audit accepted docs; production unchanged.

## Registry of the remaining 42 eligibility Unknowns

Below are exactly 42 IDs from this baseline on each preset. This is not a list of all unknown
native rules and not potential Unknown 55. The source row identifies the original condition,
not proof of a runtime fact. Dependency groups overlap;
closing one group does not guarantee that the entire row is resolved.

Owners: **I** — current integrator (ID in handoff), **R** — reference
`01a12f66-8bb2-7ee3-aced-76f5d06d2338`, **M** — manual
`01a12d73-b37b-7080-943c-684cc080b1f6`. Все группы Pending:

| Группа | Что отсутствует / действие / owner |
| --- | --- |
| VAR | Authoritative country variable astral_siphon_unlocked; не считать отсутствие нулём. I: найти реальный producer, R/M: native missing-variable только при необходимости |
| PLANET | potential обычного building документирован как planet scope; связь planet scope/exists, allow и holding roots не установлены. R/M: PLANET-SCOPE-002, затем I: привязать факт |
| SPECIES | owner_species/can_build_gaiaseeder и пригодность; founder не замена. R/M: SPECIES-SCOPE-001, затем I |
| MINING | has_any_capped_planet_mining_district → is_mining_district_uncapped, дополнительные ветви. I/R: раскрыть и связать только существующие факты |
| PRIMITIVE | is_primitive runtime state; default country_type не доказывает false. R/M: PRIMITIVE-FACT-001 probe, затем I |
| HOLDING | planet/overlord/root scopes, owner-links, host DLC и дополнительные conditions; пустой реестр holdings не делает доступность False. R: scope contracts; I: snapshots; M: только native пробелы |
| JOBS | has_trophy_hunter_job/has_hunter_drone_job и has_zone; профессии отдельно, здесь не реализовывать. I: отдельный scope после подтверждения |
| DESIGNATION | has_designation=col_resort; нет actual designation snapshot. I: искать явный startup effect; R/M: native defaults не выводить из capital |

Пути ниже относительно установки: B=`common/buildings/`, Z=`common/zones/`.
Helpers: MINING `common/scripted_triggers/00_scripted_triggers.txt:4378,4427`;
SPECIES `common/scripted_triggers/01_scripted_triggers_buildings.txt:206`;
JOBS `common/scripted_triggers/05_scripted_triggers_grand_archive.txt:591,611`.

HOLDING различает зависимости, а не назначает общий False: aid_agency проверяет
`exists=owner.overlord` и его `is_homicidal/is_xenophobe=no`; orbital_assembly_complex
проверяет `has_overlord_dlc=yes` и `is_artificial=no`; knight_commandery требует
owner→overlord origin/country flag и planet capital/population. В таблице source
ведёт к конкретному AST; known факты не становятся Unknown только из-за этой группы.
MINING: uncapped helper содержит district_set, owner_or_space_owner/subterranean
и carrier flag. Проверка17.10 не установила native scope и нужные дополнительные
факты. DESIGNATION: в просмотренном startup path явное назначение не найдено;
это пробел данных, а не доказательство отсутствия col_resort.

| ID | Source condition | Группы |
| --- | --- | --- |
| building_astral_siphon_1 | B18_astral_planes_buildings.txt:15 | VAR |
| building_astral_siphon_2 | B18_astral_planes_buildings.txt:104 | VAR |
| building_astral_siphon_3 | B18_astral_planes_buildings.txt:193 | VAR |
| building_gaiaseeders_2 | B02_government_buildings.txt:1046 | PLANET, SPECIES |
| building_gaiaseeders_3 | B02_government_buildings.txt:1117 | PLANET, SPECIES |
| building_mine_generic | B03_resource_buildings.txt:642,684 | PLANET |
| building_mining_districts_1 | B03_resource_buildings.txt:1003,1044 | MINING |
| building_solarpunk_gaiaseeder | B11_primitive_buildings.txt:1258,1286 | PRIMITIVE |
| building_solarpunk_organic_paradise | B11_primitive_buildings.txt:1435,1468 | PRIMITIVE |
| building_solarpunk_ranger_lodge | B11_primitive_buildings.txt:1314,1346 | PRIMITIVE |
| building_solarpunk_sapling | B11_primitive_buildings.txt:1374,1407 | PRIMITIVE |
| holding_aid_agency | B15_overlord_holdings.txt:254,259 | HOLDING |
| holding_communal_housing_outreach | B15_overlord_holdings.txt:1180 | HOLDING |
| holding_distributed_processing | B15_overlord_holdings.txt:629 | HOLDING |
| holding_dread_outpost | B15_overlord_holdings.txt:1113,1122 | HOLDING |
| holding_emporium | B15_overlord_holdings.txt:205 | HOLDING |
| holding_energy_requisitorium | B15_overlord_holdings.txt:349,353 | HOLDING |
| holding_experimental_crater | B15_overlord_holdings.txt:1652 | HOLDING |
| holding_franchise_headquarters | B15_overlord_holdings.txt:1741,1748 | HOLDING |
| holding_idyllic_bloom | B15_overlord_holdings.txt:1262,1344 | HOLDING, SPECIES |
| holding_knight_commandery | B15_overlord_holdings.txt:2110,2120 | HOLDING |
| holding_material_requisitorium | B15_overlord_holdings.txt:406,410 | HOLDING |
| holding_noble_vacation_homes | B15_overlord_holdings.txt:1047 | HOLDING |
| holding_offspring_nest | B15_overlord_holdings.txt:697,708 | HOLDING |
| holding_offworld_foundry | B15_overlord_holdings.txt:737,743 | HOLDING |
| holding_orbital_assembly_complex | B15_overlord_holdings.txt:74 | HOLDING |
| holding_organic_sanctuary | B15_overlord_holdings.txt:1701,1709 | HOLDING |
| holding_overlord_vigil_command | B15_overlord_holdings.txt:844,850 | HOLDING |
| holding_parasitic_algorithms | B15_overlord_holdings.txt:955,959 | HOLDING |
| holding_park_ranger_lodge | B15_overlord_holdings.txt:1500,1514 | HOLDING |
| holding_produce_requisitorium | B15_overlord_holdings.txt:467,471 | HOLDING |
| holding_propaganda_office | B15_overlord_holdings.txt:111,117 | HOLDING |
| holding_recruitment_office | B15_overlord_holdings.txt:1438 | HOLDING |
| holding_reemployment_center | B15_overlord_holdings.txt:1374 | HOLDING |
| holding_sacrificial_shrine | B15_overlord_holdings.txt:1890 | HOLDING |
| holding_satellite_campus | B15_overlord_holdings.txt:161 | HOLDING |
| holding_splinter_hive | B15_overlord_holdings.txt:554,565 | HOLDING |
| holding_transcendental_retreat | B15_overlord_holdings.txt:2183 | HOLDING |
| holding_tree_of_life_sapling | B15_overlord_holdings.txt:1583,1599 | HOLDING |
| holding_wilderness_glade | B15_overlord_holdings.txt:2272,2284 | HOLDING |
| hunting_grounds | B21_grand_archive_buildings.txt:599 | JOBS |
| zone_default | Z00_zones.txt:8,11 | DESIGNATION |

Последние проверенные ответы: R turn `01a146a8-d52b-7e30-9f03-5032bc693402`,
M turn `01a146ad-dbc7-72d1-94c5-b3761f2d106e`: PRIMITIVE и custom_tooltip.text
не подтверждены, primitive probe только поставлен в очередь. Повторно не считать
тот же ответ новым evidence. NOT-MULTI/SPECIES-SCOPE/SYSTEM-CONSTRUCTION Pending.

Новый R turn `01a146bd-b573-74b2-907d-1c09cbecafc2`, проверено17.10:
`common/buildings/00_example.txt:50–52` прямо документирует planet scope только
potential обычного building; allow:60–62 отдельно scope не задаёт. Это подтверждает
исходный scope potential, но не native переход planet=self. Holding
owner_type=subject_holding/категория planet_holdings не доказывают roots/owner/overlord.
PLANET-SCOPE-002 передан M в отложенную очередь: раздельная идентичность
THIS/ROOT/PREV/planet у potential/allow обычного building и holding с разными owners.
Probe не запускался; новых closure в реестре42 этот ответ не даёт.
M turn `01a146bf-10b9-73c2-8e00-3b52e9af23b3` подтвердил постановку в очередь;
на тот момент отдельного разрешения на диагностический мод не было.
Позднее17.10 человек прямо разрешил manual owner проверки PLANET-SCOPE-002 и
PRIMITIVE-FACT-001 через отдельный приватный мод вне read-only установки.
На этапе передачи разрешения результатов ещё не было, baseline42 сохранялся.
M подготовил ignored private package `content-local/native-probes/scope-primitive-2026-10-17/`
в своей рабочей копии: README/mod/collector/manifest. Independent review принял
исправленные reset/gating/paused scenario для manual probe. Native UI у M недоступен,
выполнялась пользовательская сессия; её ограничения ниже. Hash и критерии evidence —
верх runtime contract. Подготовка/регистрация descriptor не закрывают ни один Unknown.

Позднее получен run v1 `20261017-020001-919`,4.5.2/2200.1.1: aggregate primitive
false у observed default и true у primitive; ordinary hooks planet=seeded Earth,
owner=UNE. Ошибка macro/scope и неполный reset не позволяют принять whole identity
или привязать primitive к actual actor; pending universal/default/faction правила
не закрываются. Holding человек проверить не смог, hooks0, отдельный Pending.
Independent evidence review accepted только узкие observations. Отдельный
исправленный v2 без holdings/параметризованного logger prepared и принят independent
code review для controlled user run; до чистого run baseline42. V2 namespace ssprobe2,
сценарий/manifest в верхнем runtime и README private package.
Полные hash/ошибки/ограничения в верхнем runtime contract.

## Граница импорта и исторические замеры

Следующие числа сохранены как история отдельных срезов/контекстов. Текущий baseline
и его воспроизведение находятся выше; 90/75/44 не являются текущим остатком.

Полный Construction importer на настоящей установке4.5.2 сохраняет все644
определения с раскрытым inline AST/параметрами/provenance. M3 реестр содержит648
записей вместе с четырьмя районами. Отдельный Economy importer по-прежнему включает
194 определения (165 зданий/29 зон); его450 исключений больше не исключают запись
из общего каталога. `catalogImported` и `economyIncluded` разделены в JSON report.
Профессии не менялись: job closure59 (29 covered/30 gated).

potential=False скрывает запись; Unknown сохраняет заблокированную. Технологии,
allow/unlock/possible/possible_precalc, место и реализация отдельно ограничивают
команду. UI показывает совместимые недоступные карточки без команды и с причиной.
Метаданные идут из реестра при отсутствии economy recipe; фиктивных terms нет.
Release0/0, source190/authority13/traditions27/inline518/coverage388/HTTPcatalog4
PASS, independent actual-diff review accepted; `artifacts/full-catalog-*.log`.
В текущем HTTP economic контексте295 строк/15quotes; это результат фильтрации,
не число импортированных определений. Browser/manual UI recheck не выполнялся.

Проверка условий расширена отдельным source closure121 scripted helpers;
typed inventory64 сохранён, уровень столицы — из actual installed native capital.
Source322/Release0/0/HTTP4 и regression checks PASS, independent review accepted.
Диагностический economic context eligibility Unknown95→90; остальные90 требуют
неподдержанных фактов/scope/правил, не заменяются False или разрешением.
Детали и ограничения: верхний раздел PLANET-CONSTRUCTION-RUNTIME.md.

ACTUAL-CATALOG-001: полный начальный EconomyWorld context (HUM/groups/founder,
population/tech/positive CG upkeep) проверен для всех648×3preset. Eligibility у
каждого473F/75U/100T; partial probe90U сохранён для сравнения. Это новый замер,
runtime не менялся. Все Unknown quotes заблокированы; DTO сохраняет Unknown без
quote, скрывает False; world hash неизменён. Source355/Release0/0 PASS,
independent root review accepted; artifacts/catalog-actual-context/run.log.
Branch-office/holding ownership и системный граф требуют собственных snapshots;
SPECIES-SCOPE-001/NOT-MULTI-001 Pending, live command/transport не перепроверены.

После общей таблицы flow effects 120 included definitions не имеют M3 economy
gate, 74 всё ещё имеют. Отсутствие gate не доказывает readiness: отдельно остаются
conditions/contributions, native ограничения, runtime и lifecycle. `declared=false`
отличает отсутствующее исходное поле от реально неизвестного условия.

Повтор после civic56: `artifacts/catalog-current-coverage.log`, source388 PASS;
counts644/194/450 и74 economy gates прежние. Unknown owner guards55 вместо91.
Authority58 дополнительно сократил owner unknown до41; authority10/civic74/zone30/
catalogue388 PASS (`artifacts/catalog-authority-coverage.log`).
Structural unity channel: runtime economy gates74→68, source39/coverage388/
monthlyHTTP44/catalogHTTP4 PASS (`artifacts/structural-flow-*.log`); полный
research/build lifecycle остаётся pending.
Контекстный resolver трёх memorial tiers использует actual class в gate/quote/
installed economy: static gates68, context gates65, source52/coverage388/
monthlyHTTP44/catalogHTTP4 PASS (`artifacts/catalog-context-*.log`). Остальной
каталог сохраняет прежний consumer; current owner eligibility мемориалов False.
HTTP/WS каталог198 строк/16 quotes — только наблюдение, не readiness: обнаружен
пропуск unsupported raw effect gate для luxury residence (amenities consumer нет).
Пропуск gate исправлен: source25/HTTPcatalog4 PASS; только ложный luxury quote
удалён, остальные15 сохранены. Amenities consumer остаётся необходимым шагом.

## Оставшиеся группы

DISTRICT-PRESENCE-001: полный снимок actual завершённых районов подключён к literal
has_district; отсутствие snapshot остаётся Unknown. На каждом source preset
farming4/mining2/generator3: farming/generator helper и соответствующие здания
Unknown→True, mining остаётся Unknown из-за других зависимостей. Eligibility44U→42U,
potential63U→55U; known truth не потеряна, quote/DTO сохраняют guards. Профессии
отдельно, closure59 не менялся. Точные проверки и ограничения — верх runtime contract.
PRIMITIVE-FACT-001/custom_tooltip.text Pending по reference; manual primitive probe
отложен. Новых native подтверждений для остальных Unknown owners не предоставили.

SYSTEM-GRAPH-001: structural Generated body→system/colony bindings, materialized
class и star kind подключены; any_system_planet включает звёзды. Partial graph
не доказывает отсутствие объекта; applied Flags/starbase/queued facts остаются
Unknown. Source-generated3home: system32/full-catalog368/Release0/0 PASS;
root production проверен condition_source_audit, Luna low harness проверен root.
Actual exists/system molten witness/is_star подтверждены. 648×3 eligibility
в сумме1512F/132U/300T до/после; known truth не потеряна. Логи и пределы в runtime
contract. Native has_building_construction queued/active — SYSTEM-CONSTRUCTION-001
в manual queue; starbase predicate документирован как queued, но его runtime нет.

BRANCH-FACTS-001: полный authoritative реестр новой source колонии и отдельные
owner-links подключены к строгим has_branch_office/has_holding. Snapshot null
остаётся Unknown. Actual eligibility75U→44U у каждого из3пресетов, potential98U→63U;
известная truth сохранена. Source368/HUM34/coverage388/Release0/0 PASS, production
condition_source_audit и harness root review accepted. Positive lifecycle,
branch-office owner predicates, holding root/overlord/DLC и system graph Pending;
профессии не менялись. Логи artifacts/catalog-foreign-*.log, runtime contract.

HUM prerequisite: три source пресета создают actual HUM/BIOLOGICAL species с
native class traits и выбранными traits, группы населения ссылаются на вид.
Owner context/Fork/hash/отклонение чужой привязки: source29, Release0/0,
authority13/traditions27/coverage388/HTTPcatalog4 PASS; independent review.
Species helpers и другие contexts ещё Unknown; counts/gates прежние.

Inline TRIGGER: 20 native вызовов разобраны как AST (18 ringworld и2owner),
source518/inline10/coverage388/Release0/0 PASS, независимый diff review.
Это source prerequisite, не разрешение неизвестного ringworld default: counts
644/194/450 и gates68/65 сохранены; excluded contexts не открывались.

Tradition helper closure4: has_cloning_tradition, has_genetic_stage_1_tradition,
has_purity_heightened_attributes_tradition, has_psi_corps_tradition подключены с
native телами/manifest. Authorityv4/64defs; source27/authority13/civic85/zone30/
coverage388 PASS, Release0/0, независимое review. Current held traditions/flags
пусты; новые эффекты/профессии не выдаются. Counts644/194/450 и gates68/65 прежние.

Silo country capacity consumer: mining source26/catalogHTTP4 PASS,
Release0/0 и independent review. Два реально оплаченных silo дают +10000 только
владельцу, off/on меняет +5000/upkeep1; fixed influence1000 не меняется.
Generic/fixed/special source metadata и effective DTO/Bank разделены. Tech bonus
ветвь подключена к research refresh, но не исследована в этом lifecycle.
Статические economy gate counts этим срезом не уменьшаются: silo раньше
блокировался отдельным Core country-scope gate и native slot compatibility.

| Группа | Результат / следующий необходимый шаг |
| --- | --- |
| Общие job flow effects | Одна Core/M3 таблица; +42 native leaves, включая зоны. Source scope/scalar/attached scaling сохранены. |
| Незнакомые flow resources | Trade, advanced_logic, minor_artifacts, nanites и отдельный deposit consumer; storage/конверсию не угадывать. |
| Generic definitions | Embedded inline TargetId binding подключён; Baol farmer food +.25 сверён между импортёрами, source6 PASS. Неизвестные параметры и прочие условия остаются gates. |
| Профессии | Wrangler/augmentor/colonist/polytechnic_mentor и source JobClosure gates; job swaps для trader/прочих подмен — отдельная система. |
| Прямое производство | Raw produces/provenance и отдельный immutable structural flow channel для unity×perk-count подключены. Шесть monument definitions проходят economy gate; три memorial сохраняют условные зависимости. Полный lifecycle и ненулевые structural modifiers pending. |
| Автоматизация/рост/сборка/медицина | Source modifiers/scopes и соответствующие потребители ещё не реализованы полностью. |
| Conditions | Origin/deposit helpers (source24 PASS), perks (source10), 24 zone helpers+has_zone (source30), 14 civic-only owner helpers (source74) подключены. Inline TRIGGER source518 PASS; flags/scopes/system graph и отдельные destroy/allow ещё зависимы. Native разрешения не подменять defaults. |
| Lifecycle зон | Anglers и специальные зависимости; compatibility и atomic replacement сохраняются. |
| Специальные контексты | Все определения уже в общем реестре. Habitat/gestalt/mobile/Wilderness/branch office/holding/event/primitive требуют собственного контекста и executor; фильтруются условиями, неизвестное остаётся заблокированным. |
| Приёмка | Full research→build/upgrade→monthly→removal и варианты империй открыты; source audit не заменяет приёмку. |

## Подтверждённые зависимости следующих срезов

- Прямой выпуск monument — `resources.produces unity=1`, multiplier
  `owner.trigger:num_ascension_perks`; отдельный structural production channel ещё
  подключён отдельно от job output. Ненулевой perk count не выдан; native порядок
  структурных Add/Mult не подтверждён, зависимый расчёт блокируется.
- `minor_artifacts`: special max 2000 плюс resource-specific capacity modifiers.
  Native05_research_buildings:779–781 даёт faculty +2000 безусловно, 757–764 ещё
  +3000 при ap_archaeoengineers. Faculty также добавляет category research speed,
  empire_limit=1 и условный выпуск артефактов биологами. Storage-only consumer
  подключён к enabled per-colony snapshot: source22/Release0/0, independent review;
  base +2000/условные+3000 сохраняются, generic silo special не увеличивает.
  Faculty quote остаётся закрытым: category math/empire_limit/output ещё pending,
  ненулевой perk и installed lifecycle пока не проверены. Детали в runtime contract.
  Фиксированный cap2000 неверен; generic silo bonus special cap не увеличивает.
- `nanites` и `advanced_logic`: source не задаёт max; это не доказательство unlimited
  reserve. Nanolab также платит nanites за строительство и содержание.
- Housing/amenities: native структурные additions известны, но demand зависит от
  habitability/pop modifiers. Значения потребления/баланса не подменяются константой.
  Metadata `fake_pop_growth_mod` не доказывает отсутствие engine consumer.
- Civic-only helpers: исследованы 14 native OR predicates по selected/valid civics;
  hospitality-servitor отдельно требует origin/country flag и не входит в этот срез.

Источники этих границ: `common/buildings/08_unity_buildings.txt`,
`common/strategic_resources/00_strategic_resources.txt`,
`common/buildings/05_research_buildings.txt`, `20_machine_age_buildings.txt`,
`common/defines/00_defines.txt`, `common/static_modifiers/00_static_modifiers.txt`.
Readonly source review `ready_interface_review`, 16 октября; это ограничения
реализации, не завершённая игровая приёмка.

[Runtime и завершённые срезы](PLANET-CONSTRUCTION-RUNTIME.md).
[Полный UI/командный scope](PLANET-MENU-INTEGRATION-PLAN.md).
