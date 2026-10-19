# Planet construction — backend contracts and first checkpoint

## Authorized native probes — 17 October

Run v1 received: manual-owner ignored
`content-local/native-probes/scope-primitive-2026-10-17/runs/20261017-020001-919/`.
Cygnus 4.5.2, in-game date 2200.1.1; raw manifest of 4 files verified. Game SHA256
`A6C9B401353C5DFAEE7F619C5DD84F6B075E7A4C81FD4DF55A71F01FFF4F37A9`, error SHA256
`E84E9416B1EC5F47AEFDE84549F90909DC7FB07A8704CE59F4BEF4FD1BB4C3BB`.
The collector was run by root through pwsh 7/ExecutionPolicy Process Bypass; the system policy
was not changed. Windows PowerShell 5.1 exposed a UTF-8-without-BOM collector error; this was our defect.

Observations, verified by M and independent condition_source_audit: START contains
default/preFTL controls; native is_primitive is false/true, respectively. But names/types
in BEGIN are corrupted/collapsed, and actor identity/faction-state are absent—do not infer
a universal default=false rule or attribute a fact to an actual M3 country.
Separate ordinary potential/allow hooks recorded planet=seeded Earth and
owner=seeded planet owner/actor UNE. The whole identity test is INCOMPLETE:
Invalid macro ssprobe_identity:rev.GetName, overlord wrong-scope/invalid-scope errors,
THIS/ROOT/PREV are untrusted; there is no final reset. Do not treat negative results as facts.
The user explicitly reported holding unavailable: 0 hooks, Pending; do not request another run now.
The run does not resolve baseline 42, and runtime was not changed. Preserve the v1 bytes/manifest/raw logs.

M was assigned correction v2 under this authorization: a separate version/manifest,
two nonparameterized ordinary hooks for positive planet/owner identity only, without LABEL,
THIS/ROOT/PREV/direct overlord. The primitive effect links the result to the actual seeded
actor and a separate named preFTL control; each branch has an explicit name/type/result.
Reset→actor→planet→one catalog→reset, with no holdings, time, jobs/resources/owner
changes. Fix collector 5.1 encoding and English/Russian labels; include the builtin icon
by reference only, without copying bytes. Independent code review is required before rerunning;
the corrected package is not evidence of new behavior in the original game.

V2 prepared/independent condition_source_audit accepted for a controlled manual run:
manual-owner ignored `content-local/native-probes/scope-primitive-v2-2026-10-17/`.
Manifest `DAAE0E34B10892525A8DC9E69DE20DBC401F867DD8AC34DE8B842A7CC6757D32`;
the newly registered descriptor SHA256
`8D7B585F4D7D396C2B1CA84CF8F9DE7E5E477905EB4E36BC0C5B669ED0D6BABF` совпал с package.
Namespace ssprobe2/SSPROBE2; the v1 package/descriptor/raw data are preserved. Scenario: a separate
playset with v2 only, pause, with no planet selected run event ssprobe2.12 then ssprobe2.11;
select your colony → event ssprobe2.10 → one ordinary catalog → close/clear selection
→ event ssprobe2.12. Do not repeat the holding probe; after the user's message, the integrator
collects raw logs; do not use old collector v1. Runtime v2 NOT_RUN.
Collector tooling v2 fixes only v1 UTF8/BOM/read; PS 5.1/7 parse PASS,
independent review accepted; v1 manifest/run were not overwritten.

Direct instruction from the user: proceed through the existing manual owner for PLANET-SCOPE-002
and PRIMITIVE-FACT-001. A separate private diagnostic mod outside the installation is authorized;
the installation remains read-only, and the mod/source assets must not be published. Professions are separate.
The manual owner receives exact scenarios from registry 42/their queue. Raw
local evidence is required, with version/scenario and observations/conclusions recorded separately; having
a prepared mod is not a native verification result. Once evidence arrives, the integrator records its
runtime applicability, exact expected transitions, review, and source85-baseline regression. Unverified
scope/default/state remain Unknown.

Package prepared: manual-owner ignored `content-local/native-probes/scope-primitive-2026-10-17/`,
with its own generator/mod/README/collector and manifest SHA256
`E4582E626C187A5606EE0F21D5A05CD8358BA24F6CBF152527AD77D9C234776D`.
The new descriptor was registered separately; the playset was not changed. Grammar subset and
PowerShell syntax PASS; native API/runtime were not checked. Independent
condition_source_audit accepted the controlled manual probe after corrections:
reset ALL4/actor-before-planet, actor+planet gate, pause/one open/reset.
The mistaken initial finding that logging is prohibited in a trigger was withdrawn based on native print_scope_trigger
in 00_scripted_triggers.txt:16–34; PLANET hooks remain in the actual potential/allow.
The collector only copies raw logs and does not confirm a successful probe. Fresh
game.log/error.log, complete reset/seed/BEGIN/END, and positive identity controls are required.
An absent pre-FTL/holding subject or hook is incomplete, not False.
The manual owner's native UI was unavailable (Computer Use only had iab); the game was not run during preparation.
V1 preparation is described below; the actual run/limitations are above.
Manual-owner handoff: `da98255:docs/reports/native-scope-diagnostic-2026-10-17.md`;
this documents preparation/limitations, not native PASS or integrated gameplay.

## Verifying the next confirmed fact — 17 October

Before changing the evaluator, check cwd/branch/HEAD/dirty and the baseline in
PLANET-CATALOG-COVERAGE.md. Do not treat old counts from another context as the baseline.
For selected IDs, first emit real snapshots and expand transitive helpers;
record the source rule, runtime fact, and expected result separately.
Acceptance: exact before/after truth for each target ID and preservation of other gates,
complete potential/eligibility maps for all 648×3, and a list of every changed ID.
The comparison must show that transitions explain every aggregate change for each
truth and reject unexpected transitions/IDs, loss of known truth, or missing/duplicate IDs.
After the focused test, run one full source pass and only affected regressions.
Conditional assertions that skip checking expected outcomes are not accepted.
Pure transition-count checks are allowed; synthetic native ASTs and in-game
grants do not replace actual source evidence. Unknown requires refusing the quote/disabled DTO.
Read pending owners again only on a new response or necessary focused
status check; retain the turn ID. Link new evidence to a dependency group,
instead of rerunning the full audit. Professions are a separate scope.

TRANSITION-ACCOUNTING-001 completed on baseline46e30a4: source85/Release0/0 PASS.
The test independently records 8 potential transitions (farming/generator tiers 1–4),
2 eligibility transitions (tier 1) from Unknown→True, and the exact set of 42 Unknowns on each
preset. The 648×3 maps were compared with baseline aggregates; known-loss 0/hash unchanged.
Pure accounting 7 checks success and specific rejection reasons for unexpected
transitions, extra/missing/duplicate IDs, known-loss, and incorrect totals.
The self-comparison expected=actual was rejected in root review and corrected before the final run.
Author of tests actual_context_checks (Luna low); independent root review accepted;
docs/source audit condition_source_audit accepted. Production/economy/jobs were unchanged.
Logs: artifacts/catalog-transition-accounting-build.log and -source.log.
Broad regressions were not rerun: production has not changed since the last full368/system32.

## Completed districts in conditions — 17 October

DISTRICT-PRESENCE-001: native 00_scripted_triggers.txt:4354–4407 uses
literal has_district in helpers has_any_capped_planet_farming/mining/generator_district.
The complete immutable snapshot of completed districts from runtime.Installed
(Kind=District) is connected, rather than districts available to build or queued orders.
For an absent ID in the complete snapshot, count=0; an absent snapshot remains Unknown.
Accept only literal source IDs, without macros/comparisons/unsupported forms.
Preserve the snapshot when copying template context and adding the system graph.
Do not guess native uncapped/owner_or_space_owner or other helper dependencies;
False from proven has_district may resolve only the dependent AND/OR according to the AST.
Acceptance: actual source 3 presets before/after 648, source helpers/provenance,
no-known-loss, quotes/DTO, absent snapshot→Unknown, independent review/Release.
Do not change professions. Reference/manual checks found no new native evidence for
SPECIES-SCOPE/NOT-MULTI/SYSTEM-CONSTRUCTION; statuses remain Pending.

PRIMITIVE-FACT-001: reference confirmed only explicit faction.primitive=yes
on country_type primitive and the ability to change faction state. The field's absence
on default does not prove is_primitive=no; the native predicate/state connection is unestablished.
The manual owner added the default/pre-FTL startup probe to the deferred queue but did not run it.
The general metadata semantics of custom_tooltip.text in a trigger are also Pending.
The reviewed remaining list requires runtime snapshots/scopes or native evidence;
do not treat missing data as False or substitute founder for owner_species.

Result: actual source 3 presets have district_farming4/district_mining2/
district_generator3. Native literal membership is positive for these IDs and
negative for absent variants. Capped farming/generator helpers and
building_farming_districts_1/building_generator_districts_1 changed Unknown→True;
capped mining and building_mining_districts_1 remain Unknown due to other guards.
648×3: eligibility per preset 504F/44U/100T→504F/42U/102T; potential total
1187F/189U/568T→1187F/165U/592T. Known-loss=0; Unknown quotes are blocked,
DTO does not hide Unknown or issue a quote for it; EconomyWorld hash unchanged.
Production root → condition_source_audit accepted; Luna low source harness → root
accepted after correcting vacuous assertions and adding direct farming AST.
Release0/0, district-source72, full-catalog368, and system-source32 PASS;
artifacts/catalog-district-presence-source.log, catalog-district-*-regression.log.
No new districts/orders/stocks/tech were granted. Jobs closure59 unchanged;
positive construction lifecycle, HTTP/browser, and native uncapped rules were not verified.

## Структурный системный граф — текущий срез 16 октября

Связывать actual Generated home/body/SystemId со SpaceWorld.Colonies и текущими
construction runtimes, с проверкой root body/colony/owner. Передавать immutable
список известных тел системы, их materialized class и raw construction contexts
зарегистрированных колоний; здания/владельцы других тел остаются Unknown. exists=solar_system
доказывается явной связью, solar_system/solar_system? переходят в эту систему.
any_system_planet может доказать True по известному свидетелю; False разрешён
только при полном графе и известных отрицательных фактах всех его планет.
Текущий producer GameplayComplete=false/Deferred не сертифицирует полноту:
передавать partial graph. Не превращать декларативные Flags в applied star flags.
Синтетическая station SpaceWorld не является native starbase. has_star_flag,
starbase predicates, unknown per-body states и queued/active native construction
сохраняют Unknown. Scripted system helpers используют source closure/общие
depth/node/cycle guards. Никаких дополнительных сущностей/станций/флагов не выдавать.
Приёмка: actual source-generated мир без fixture/grants, immutable root bindings,
source exists/system helper chains, отсутствие ложного False при partial graph,
before/after catalogue и independent review/Release/source regressions.

any_system_planet охватывает также звёздные тела: native вызовы с is_star=yes
есть в common/inline_scripts/events/nomads_scientific_observation_internal_event.txt:10–11,
24–25 и events/cosmic_storms_events_1.txt:139–140. Включать все materialized Bodies
системы, явно хранить их Kind=Star и вычислять is_star для текущего тела обхода.
Это не has_star_flag; applied flags по-прежнему неизвестны.

Reference-owner: native common/starbase_modules/00_example.txt:492–498 явно
определяет is_starbase_building_building как присутствие в очереди; это не только
active order. Фактической starbase/queue модели здесь нет, predicate пока Unknown.
Смысл планетного has_building_construction источником не закрыт; не переносить
starbase правило. SYSTEM-CONSTRUCTION-001 передан existing manual owner:
при отсутствии установленного target сравнить primitive для pending второго
заказа, active заказа и после отмены. До evidence планетный predicate Unknown.

SYSTEM-GRAPH-001: production root, independent condition_source_audit accepted,
включая star membership/current-body restoration и строгую форму holding ID.
Source-generated три home allocations (без fixture/grants) проверены harness
actual_context_checks (запуск Luna low), final diff принят root. Ошибки тестового
scope/вызова helper и поля diagnostics исправлены без расширения production closure.
Release0/0, system-source32 и final full-catalog368 PASS; до этого среза HUM34/
coverage388 PASS. Логи artifacts/catalog-system-graph-source.log,
catalog-system-graph-build.log, catalog-system-full-catalog.log. Actual native
exists=solar_system Unknown→True, molten witness→True, is_star на home→False;
неполные building/starbase helpers остаются Unknown с соответствующими gates.
648×3 до/после: potential1187F/189U/568T, eligibility1512F/132U/300T; known-loss=0.
Количество Unknown этого seed не уменьшилось: прочие guards всё ещё блокируют.
DTO/quote/Fork/hash PASS. Live command/HTTP/browser не проверялись; native positive
lifecycle/полнота effects/flags/starbases и SPECIES-SCOPE/NOT-MULTI Pending.
Usage/runtime model недоступны; профессии и их inventory не менялись.

## Реестр филиалов и владений — текущий срез 16 октября

Хранить в authoritative мире immutable snapshot конкретной планеты: planet/owner
identity, отдельного владельца филиала, установленные holding instances с source
ID и владельцем. Snapshot отсутствует до явной регистрации полного реестра;
отсутствующий snapshot — Unknown. Source startup собственной новой колонии
регистрирует пустой реестр; изменения идут только через проверяемые операции
мира и сохраняются в Fork/hash. Не выводить native отсутствие из отсутствия кода.
Все foreign owner references должны указывать на существующие страны мира;
неверная planet/owner binding отклоняется до вычисления условий.

has_branch_office читает зарегистрированный филиал; has_holding проверяет exact
source ID и owner-link только для доказанной формы. Не подменять owner_type
контекстом и не переносить root. Условия неизвестного владельца/overlord/DLC и
графовые any_owned_planet сохраняют Unknown. Положительные native lifecycle и
правила открытия/закрытия филиала не создаются этим слоем фактов.
Приёмка: исходные predicates из 4.5.2, actual startup трёх пресетов, before/after
648×3, отсутствие snapshot→Unknown, immutable bindings/Fork/hash, quote/DTO и
повторные guards; independent actual-diff review, Release/source regressions.
Профессии не менять; после этого среза — фактический системный граф.

BRANCH-FACTS-001: production root независимо принят condition_source_audit;
harness actual_context_checks (запуск Luna low) принят root после добавления
actual оценки извлечённого native has_holding. Release0/0/source368/HUM34/
coverage388 PASS; artifacts/catalog-foreign-*.log. 648×3 before/after:
economic/scientific potential361F/98U/189T→396F/63U/189T;
military360F/98U/190T→395F/63U/190T. Eligibility у всех473F/75U/100T→504F/44U/100T.
Ранее известная truth не потеряна; все Unknown quotes blocked, DTO сохранён,
Fork/hash/binding/duplicate-init checks PASS. Нет искусственных filial/holding
состояний. Positive lifecycle, branch-owner predicates и live transport Pending.
Графовые/holding root scopes не переназначались. Это не полная готовность H01.

## Фактический контекст полной проверки — 16 октября

Повторить все 648 записей для economic/scientific/military с actual EconomyWorld,
source HUM recipe и связанными population groups. Snapshot включает текущего
владельца, founder, виды, исходные технологии и sapient population. Как в live
ConstructionEvaluationFor, положительный actual upkeep consumer_goods доказывает
country_uses_consumer_goods=true; отсутствие расходов не доказывает false.
Исходный неполный probe сохраняется отдельной базой сравнения. Проверять
potential/eligibility, DTO False/Unknown, неизменность мира и отказ quote для
зависимого Unknown. Live command/transport QA отдельно. Ни технологии, ни
состояния/AST для проверки не выдаются.

Read-only аудит condition_source_audit: branch_office_owner — отдельная страна
от владельца планеты (14_branch_office_buildings:24–28); has_holding требует
идентификатор holding и owner-link (01_scripted_triggers_jobs:813). Текущий
construction snapshot этих связей не хранит: отсутствие модели не означает
доказанное отсутствие филиала. Системные helpers также требуют реального графа:
00_scripted_triggers:5020 проверяет установленные/строящиеся здания всех планет
и starbase в системе, а не только локальную колонию. Эти операции остаются
Unknown до полного соответствующего snapshot. Профессии отдельно.
Reference-owner подтвердил NOT-MULTI-001 Pending: usages не различают NAND/NOR,
generated trigger docs 3.5.3 не доказывают 4.5.2. SPECIES-SCOPE-001 остаётся Pending.

Результат ACTUAL-CATALOG-001: по 648 оценок potential и eligibility на каждый
пресет, без пропуска potential=False. Начальные source snapshots:

| Пресет | Potential False / Unknown / True | Eligibility False / Unknown / True |
| --- | --- | --- |
| economic | 361 / 98 / 189 | 473 / 75 / 100 |
| scientific | 361 / 98 / 189 | 473 / 75 / 100 |
| military | 360 / 98 / 190 | 473 / 75 / 100 |

Это уточнение проверки полным фактическим контекстом, а не улучшение runtime:
старый частичный economic probe сохраняет 471 / 90 / 87. True означает лишь
условия каталога, не реализацию effects/место/цену/исполнение. Все actual Unknown
quote отклонены с eligibility-unresolved; DTO сохраняет каждую potential=Unknown
отключённой без quote и исключает False. Хеш EconomyWorld не изменился.
FullCatalogSourceChecks: 355 PASS, Release build 0 warnings / 0 errors;
лог artifacts/catalog-actual-context/run.log. Автор harness actual_context_checks,
запуск gpt-6-luna/low; независимый reviewer root: замечания по полноте подсчёта и
DTO исправлены, финальный actual diff accepted. Runtime-модель/usage недоступны.
Изменены только проверки и документы; HTTP/browser, совместимый реальный слот
для повторной команды и прочие lifecycle-сценарии в этом срезе не проверялись.

## Проверка scripted условий полного каталога — 16 октября

Подключить source closure scripted triggers из условий каталога к существующему
интерпретатору. Отдельный immutable реестр зависимостей не расширяет whitelist
typed facts и не меняет его validation. Вычислять поддержанные вызовы без
параметров с literal yes/no в текущем scope, через тот же boolean evaluator;
NOT/no инвертируют только известную truth. Пределы файлов/узлов/глубины/вызовов,
cancellation/provenance/hash, явное обнаружение циклов обязательны.
Параметризованные helpers, неподдержанные scope/macros/primitive либо отсутствующие
actual facts остаются Unknown; не приравнивать owner_species к founder и не
выдавать DLC/tech/perks/flags. Существующие typed checks сохраняют приоритет.
CapitalTier берётся из единственного фактически установленного capital=yes и
его literal native capital_tier. При отсутствии/неоднозначности — Unknown;
не вычислять максимум или tier по пресету. Проверяется native capital_tier>=N,
UpgradedCapital выводится из actual tier>=2; shelter сохраняет отдельный context.
Применять одинаковую проверку к видимости/доступности/командам; профессии отдельно.
Приёмка: реальные source dependency chains, actual3presets, до/после coverage,
повторная проверка commands, независимое ревью и source/HTTP без fixture/browser.

Реализовано: отдельное source замыкание121 helpers, только literal yes/no calls,
без structural key collisions и без изменения typed inventory64. Root diff и
source harness независимо проверены condition_source_review, включая actual
capital tier и polarity: accepted. Release0/0/source322/authority13/traditions27/
inline518/coverage388/HTTP4 PASS (`artifacts/catalog-condition-*.log`). На actual
economic startup диагностический context: potential353F/119U/176T →359F/112U/177T;
eligibility466F/95U/87T →471F/90U/87T (648 определений с четырьмя районами).
Настоящий HTTP context:287 строк/15quotes, свой сервер остановлен. Это расширение
реальной проверки, не закрытие всех условий: branch_office_owner/holding/часть
species/system facts остаются Unknown; multi-child NOT native semantics Pending,
targeted вопрос reference-owner направлен. Cycles/depth guards реализованы,
искусственные циклы/выданные состояния не создавались для проверки.

## Общий каталог — прямое решение 16 октября

Импортировать все top-level определения зданий и зон4.5.2 в один Construction
реестр с полным раскрытым AST/параметрами/provenance, без отбора по реализации
экономики, профессий или текущему пресету. Дубликаты, битые inline и изменившийся
source snapshot отклонять; неизвестные условия/числовые выражения сохранять
с явной диагностикой, не терять определение. Сохранять лимиты/cancellation.
Economy inventory и система профессий этим заданием не расширяются.

В текущем контексте potential=False скрывает определение; potential=Unknown
сохраняет видимую заблокированную запись с причиной. Отсутствующий potential
не является неизвестным условием. Технологии/allow/unlock/стоимость/место и
готовность исполнения отдельно определяют доступность команды. Не разрешать
строительство неподдержанного объекта только потому, что он импортирован.
Метаданные/локализация/upgrades исходят из общего реестра, не наличия профессии.
Все команды повторно проверяют условия; UI фильтрация не авторизует команду.

Приёмка: точное соответствие полному native inventory (сейчас498+146), раскрытие
inline и metadata для ранее исключённых определений, False/Unknown/True фильтр,
технологии отдельно от видимости, неизменённый job closure, source snapshots,
независимое ревью, Release и настоящий HTTP/WS. Без browser/fixture/grants.

Результат: full importer644 (498+146), M3 registry648 вместе с4districts.
Economy165+29 и job closure59(29covered/30gated) неизменны. В HTTP economic295
строк/15quotes после фильтрации. Root production и mixed Luna/root source harness
проверены amenity_source_review: accepted. Junior draft содержал ошибки счётчиков
и tech assertions; root исправил по actual source, без выданных технологий.
Release0/0/source190/authority13/traditions27/inline518/coverage388/HTTPcatalog4
PASS, frontend syntax2files PASS; `artifacts/full-catalog-*.log`. Своё HTTP/WS
окружение остановлено. Полный импорт не означает готовность неподдержанных
effects/lifecycle; профессии этим заданием не реализовывались. Browser/manual UI
проверка не выполнялась по действующему ограничению.

## Actual HUM species — решение человека 16 октября

Три текущих пресета создают отдельный founder species HUM. Импортировать native
HUM archetype и обязательные class traits, добавить выбранные traits пресета;
проверять существование каждого trait и source manifest. Recipe не является
ongoing snapshot: вид создаётся в EconomyWorld и actual pop group ссылается на
его entity. Сохранять identity/class/archetype/полный immutable trait set в Fork
и canonical hash; чужой owner/group binding отклонять до изменения состояния.
Без source recipe generic API сохраняет неизвестный вид, не known-empty.

Owner species контекст строить только по зарегистрированным видам и связанным
группам текущего владельца; expected country ID обязателен при передаче snapshot,
каждый вид должен принадлежать этой стране. При неполном binding или отсутствующем населении —
Unknown; не подменять snapshot template.Traits. Strict construction primitives
founder_species/any_owned_species в country scope читают actual genotype (has_trait/is_archetype),
boolean composition сохраняется, неподключённые helpers/rights остаются Unknown.
Никаких growth/assembly/auto-modding/gene-modification effects этим срезом не
создавать, job swaps отдельно. Quote/effect gates сохраняются. Приёмка: реальные
source пресеты, class/traits/group/owner/Fork/hash, actual native predicates,
сборка/independent diff review; без browser/fixture/grants.
Planet-scope iterators и species helpers этим prerequisite не разрешаются.

Реализовано: importer HUM/BIOLOGICAL/native traits, immutable species entity,
group binding, owner/founder context, Fork/hash. Root production проверен
`amenity_source_review`: P2 expected-owner binding исправлен, повторное ревью
accepted. Source harness Luna low проверен root; исправлена атрибуция native
`country_has_shackled_robots`, не подменялось тело helper. Release0/0, source29,
authority13/traditions27/coverage388 и настоящий HTTP/WS catalog4 PASS;
`artifacts/catalog-HUM-*.log`, свой сервер остановлен. Изолированный native
any_owned_species/has_trait даёт False для HUM, без binding — Unknown.
Дополнительный source harness34/Release0/0 проверил полный native
founder_species_is_biological из shroud08:663: actual HUM True, без binding
Unknown; lithoid helper Unknown из-за неподключённого uplift_is_archetype.
Логи `artifacts/catalog-HUM-founder-*.log`; harness Luna low/root review accepted.
Дополнительные виды и изменения генотипа не проверены.
Counts644/194/450 и economy gates68/65 прежние; это prerequisite, не readiness.

Source review и reference-owner `01a12f66-8bb2-7ee3-aced-76f5d06d2338`
подтвердили только цепочку helpers, не тождество scope: native triggers00:1389/
1771/1775 используют owner_species, jobs01:232 дополнительно species.
Не приравнивать их к founder/self по отсутствию трансформаций. Нужны отдельные
подтверждённые bindings; clone-vats/lithoid/infernal/organic helpers пока Unknown.
Native опыт SPECIES-SCOPE-001 добавлен original-game owner в Pending.

16.10, позднее поручение человека: дойти до играбельного состояния, сохранить
и выключить ПК. Текущий прототип проверен без browser/fixture/grants: настоящий
HTTP/WS старт и закрытый месяц44 PASS; оплаченные два mining district и новая
zone_minerals с atomic completion/тремя building slots/miner capacity —10 PASS.
Логи `artifacts/playable-HUM-{monthly,construction}.log`; свои серверы остановлены.
Сохранённый локальный launcher `artifacts/start-playable.ps1` независимо проверен
amenity_source_review; его настоящий запуск на5264 дал health protocol2/m3 и
HTTP200/source SHA трёх client files (playable-HUM-launch.log), затем остановлен.
Сборка `artifacts/catalog-HUM-founder-final`, production ca058d9/checks161a526.
Это воспроизводимый запуск текущего прототипа, не полный принятый RC/stable/main:
каталог/ручная UI parity остаются незавершёнными. Кода сохранения партий в M3
нет: сохраняются код/сборка/checkpoint, партии в памяти заканчиваются при shutdown.
Локальная инструкция — `artifacts/PLAYABLE-CHECKPOINT.txt`; remote push не делался.

## Inline TRIGGER — восстановление AST, 16 октября

Двадцать native вызовов nomadic_cost_switcher_with_additional_trigger передают
условие строкой: 18 ringworld yes/no и два owner?/lithoid-or-infernal yes/no.
Bare `$TRIGGER$` необходимо раскрывать как одно условие, а не имя с пустым
оператором; Construction и PlanetEconomy должны видеть одинаковые деревья.
Ограничить adapter этим inline и двумя исходными формами, сохранить provenance
аргумента и placeholder/callsite. Новые/неподдержанные формы остаются Unknown,
повреждённый фрагмент отклоняется; парсер/раскрытие имеют бюджеты и cancellation.
Не выбирать ни cost branch, ни значения ringworld/owner predicates: native facts
по-прежнему отдельные зависимости. Приёмка: все 20 настоящих вызовов и manifest,
одинаковый AST обоих импортёров, сохранённые Unknown/gates, Release и diff review;
без browser/fixture/grants.

Реализован shared adapter двух импортёров. Final Release0/0/source518 PASS:
artifacts/catalog-inline-trigger-final-{build,source}.log. Existing inline10 и
coverage388 PASS (catalog-inline-trigger-{binding-regression,coverage-source}.log),
counts644/194/450 и gates68/65 прежние. Проверены actual args, обе cost branches,
аргумент/placeholder provenance, manifest SHA bytes, Unknown ringworld на трёх
реальных пресетах; исключённые контексты не включались. Owner? проверен по AST,
без утверждения native truth. Root production / amenity_source_review independent
actual-diff accepted; harness inline_trigger_source_checks Luna low / root review
accepted после исправления source assumptions и compile syntax. Root исправил
case-only SHA comparison (ContentSession uppercase, другие импортёры lowercase),
финальный joint diff принят amenity_source_review. Usage недоступен.

## Faculty — отдельный special capacity consumer, 16 октября

Следующий prerequisite полного faculty: raw country modifier даёт minor_artifacts
+2000 и ещё +3000 при исходном ap_archaeoengineers potential. Суммировать вклады
включённых экземпляров через существующий immutable per-colony country snapshot;
generic silo не увеличивает special ресурс. Проверять source shape/provenance и
authorityv4, неизвестный potential блокирует только этот вклад. Снижение cap ниже
stock сохраняет прежний atomic отказ. Категорийная скорость, empire_limit и
artifact output ещё не потребляются этим срезом: country fields НЕ объявлять
полностью consumed в quote, faculty пока не открывать. Приёмка: real source/M3
contexts без выдачи perks/технологий/ресурсов, сборка и independent actual-diff review.

Storage-only реализован: Release0/0/source22 PASS, root author /
amenity_source_review independent actual-diff accepted после проверки exact
potential обеих ветвей и рекурсивного source provenance. Логи
artifacts/catalog-faculty-capacity-final-{build,source}.log. Три actual M3 пресета
без perks дают base +2000; неустановленный faculty не меняет cap, quote закрыт.
Ненулевой perk и installed-faculty lifecycle пока не проверены.
Silo regression на том же новом aggregation path: реальный HTTP/WS source26 PASS,
artifacts/catalog-faculty-silo-regression.log; два оплаченных silo, owner+10000,
off/on +5000/upkeep1, fixed/peer неизменны. Поздний faculty provenance fix этот
путь не менял; финальный resolver отдельно проверен source22. Свой test server
корректно остановлен. Категорийная математика faculty передана в manual очередь
владельцу vanilla экспериментов; взаимодействие с all/area пока Unknown.

## Condition closure: традиции владельца — 16 октября

Импортировать native has_cloning_tradition, has_genetic_stage_1_tradition,
has_purity_heightened_attributes_tradition (05_scripted_triggers_biogenesis)
и has_psi_corps_tradition (08_scripted_triggers_shroud), включая полные тела
зависимостей и source manifest. Использовать существующие OwnerHeldTraditions и
OwnerFlags: actual M3 уже передаёт пустые snapshots, generic null остаётся Unknown.
Новых defaults/флагов/традиций/профессий не выдавать, job swaps отдельно.
Версия authority profile меняется согласованно со strict opt-in memorial/silo.
Приёмка: actual source/M3 контекст, raw AST/provenance, false для текущих шаблонов,
сохранение остальных effect/permission gates; build и независимое review.
Реализован authority profilev4/64defs: source27/authority13/civic85/zone30/
coverage388 PASS, Release0/0. Логи artifacts/catalog-tradition-*-source.log и
catalog-tradition-helpers-source.log; output catalog-tradition-helpers-verified.
Автор inline_effect_bindings Luna low / root independent actual-diff accepted.
Первый запуск выявил старый validator count/dispatch: точечно исправлены kind/
source validation, count и Owner recursion, затем повторены затронутые проверки.
Static economy gates68/context65 прежние; shared effect consumers остаются pending.

## Resource silo — подтверждение человека 16 октября

Bounded native consumer: только building_resource_silo из03_resource_buildings,
строго один country_modifier и четыре triggered_country_modifier. Валидировать
raw shape/provenance, все potential через strict source evaluator. Generic +5000
и technology +5000 исполняются; nomadic situation/civic consumer-goods modifier
допускаются только при False, Unknown/True блокирует dependent operation.
Quote принимает только origin полей, полностью обработанных этим consumer.
Технологическое завершение немедленно пересчитывает вклад с новым research snapshot.
Human disable разрешён для silo в том же ordinary preset scope, что factory;
прочие permissions не расширять. Проверить реальные mining-zone → silo → cap/off/on,
без выдачи civic/tech/ресурсов и без browser/fixture.
Production review root/ready_interface_review accepted после двух fixes:
общий QuoteSource передаёт consumers также в pending revalidation; завершение
исследования обновляет каждую colony через её EconomyPlanetFor, не homeworld.
Release0/0/catalogHTTP4/source lifecycle26 PASS. Реальная mining-zone, оплата и
завершение двух silo, +10000 owner cap, fixed influence1000/peer unchanged,
off/on снимает/возвращает +5000 и energy upkeep1. Лог:
artifacts/catalog-silo-mining-source.log. Harness inline_effect_bindings Luna low /
root independent actual-diff accepted. Ненулевой tech bonus не исследован в этом
сценарии; grants не использовались. Полное vanilla dynamic storage ещё pending.
Source urban run показал корректный no-free-compatible-slot: zone_urban явно
исключает mining/generator/farming sets, хотя silo имеет и urban set. Нельзя
обходить native exclude. Сценарий перенесён в source zone_minerals (mining set).
Первый funding run остановлен при отрицательном mineral net; использованы обычные
WorkforceLimit. Preflight исправлен на полный native inline, прежде чем начался
mining gameplay. Urban отказ сохранён в artifacts/catalog-silo-urban-rejected.log.

Контракт consumer: неизменяемый вклад каждой колонии полностью заменяется после
изменения enabled graph; effective cap = прежний source base + сумма вкладов
колоний владельца. Базовый accessor остаётся source-only. SetBalance, monthly,
construction refunds/preview и DTO используют один effective cap; Fork/hash
сохраняют snapshot. Bank допускает copied per-payer storage overrides.
Снижение cap ниже текущего stock отклоняет staged изменение без публикации:
native момент списания превышения пока неизвестен, его не моделируем.
Ошибки неизвестного ресурса/overflow/неполного source consumer откатывают candidate.
Приёмка: сборка, реальные source/HTTP/WS проверки, независимое diff review;
полный silo lifecycle проверяется после подключения native effect consumer.
Infrastructure: Release0/0, monthlyHTTP44 PASS, artifacts/catalog-capacity-*.log.
Root author / ready_interface_review independent actual-diff accepted;
неподключённый silo resolver не считается готовым lifecycle.

На прямой вопрос о vanilla stacking/disable человек ответил: «Да, складываются,
да отключаются». Каждый enabled silo даёт свой country maximum bonus; выключенный
не даёт. Это новое подтверждение, не повторное permission. Native definition
задаёт +5000 и дополнительные +5000 при tech_construction_templates; civic и
nomadic effects оценивать отдельно, не менять их scope. World/bank/current limits
должны использовать одинаковый per-country snapshot, Fork/hash/atomicity сохранять.
Текущий M3 base cap — прежний source max; динамическая часть food/alloys остаётся
отдельной непроверенной границей. Новый additive channel не объявляет её нулём
и не обещает полное vanilla storage. Fixed/special не получают generic bonus.

## Storage source classification — 16 октября

Для resource silo нужен country-capacity consumer, не изменение глобального max.
Native03_resource_buildings:1720+ даёт +5000 generic max, ещё+5000 при technology
construction_templates; consumer-goods civic bonus и nomadic situation отдельно.
Сначала сохранить в PlanetEconomyResource тип maximum (Generic/Fixed/Special,
default Unknown для старых ручных конструкторов) и raw dynamic_capacity AST.
Storage inventory дополнительно включает influence (раньше baseline получал его
max через startup fallback) и minor_artifacts для будущего special-cap consumer;
это source metadata, не выдача запасов/производства и не регистрация в мире.
00_strategic_resources header прямо задаёт default no для fixed/special flags:
фиксированный cap не меняется, special игнорирует generic country max add.
Строгие unique scalar yes/no flags, одновременные fixed/special запрещены;
неизвестный storage max не превращается в unlimited. Сохранять source manifest/
provenance; dynamic expression не вычислять. Поведение мира/банка/quote пока прежнее.
Приёмка: actual resource definitions и header, fixed influence, special artifacts,
generic обычные ресурсы, dynamic food/alloys; build и независимый diff review.

Source8/monthlyHTTP44 PASS, Release0/0; artifacts/catalog-storage-*.log.
Автор inline_effect_bindings Luna low / independent root actual-diff accepted.
Первый source run выявил отсутствие influence в economy catalog (startup fallback
уже использовал max1000); добавлен отдельный classification inventory и повтор
успешен. Native source classifications не меняют баланс/registration/Bank caps.

## Контекстные условия installed economy — 16 октября

Первый ограниченный consumer: три building_galactic_memorial_1/2/3. Native
conditional ethic modifier проверяет конкретные pc_nuked/pc_relic; фактический
class известен из startup. Это сравнение class ID, не `is_ringworld` default.
Один tri-state resolver исходного дерева по immutable ConstructionContext и
strict helper catalog используется в runtime DefinitionGate, quote contribution
filter и RefreshInstalledGraph. Unknown остаётся gate, без fallback к legacy.
Остальные определения сохраняют прежний профиль; startup Initialize не расширяется.
Static RuntimeGates остаются консервативным source report; runtime пересчитывает
все независимые ограничения выбранного definition, не игнорирует первый blocker.
Не обходить costs/potential/prerequisites/unsupported effects/storage/job gates.
Context берётся по candidate graph; ошибка отклоняет staged transaction до publish.
Приёмка: raw memorial branch с exactclass False, согласованный runtime gate/quote
на реальном старте, отсутствие false eligibility для не-memorialist owner;
source HTTP/WS regression, build/review, без выданных civics/fixtures/browser.

Source52/coverage388/monthlyHTTP44/catalogHTTP4 PASS, Release0/0;
artifacts/catalog-context-*.log. Static runtime gates68, actual context gates65:
только три memorial definitions отличаются, их quote по-прежнему не доступен
текущему владельцу без memorialist civic/технологий. Browser/lifecycle не проверены.
Root production / ready_interface_review accepted после condition-aware cost
resource filter с сохранением legacy null-evaluator path. Harness/report Luna low
inline_effect_bindings / root accepted. Exact opt-in profile — authority v3;
другой profile возвращает прежнюю консервативную границу, не расширяет consumer.

## Членство владельца в галактическом сообществе — 16 октября

Каталог принимает отдельный nullable OwnerGalacticCommunityMember. В M3 исходный
ResearchFactsFor уже задаёт false; общий provider сохранит именно эту текущую
политику, без нового default для generic owner. Null остаётся Unknown; shelter
сохраняет свой прежний snapshot. Нельзя из этого выводить отсутствие эдиктов,
policy flags или country modifiers: таких доверенных наборов пока нет.
Приёмка: реальный owner snapshot, raw conditional modifier research building
с is_galactic_community_member=yes остаётся False; остальные gates не снимать.

Source34 PASS, Release0/0; artifacts/catalog-community-*.log. Автор Luna low
inline_effect_bindings, независимый root actual-diff accepted. Native branch
building_research_efficiency_1 остаётся False; resolution/modifier — отдельные
условия. Galactic community gameplay/эдикты/политики не добавлены.

## Полученные традиции владельца — 16 октября

`has_tradition` связывается с отдельным nullable immutable snapshot полученных
традиций, не с `has_active_tradition`. M3 пока не реализует получение традиций;
существующая ResearchFactsFor считает их отсутствующими. Обоим потребителям
передаётся один known-empty provider, без выдачи традиций и effects. Отсутствующий
snapshot остаётся Unknown; shelter использует свой прежний HeldTraditions.
Приёмка: raw cloned-vat owner has_tradition condition сохранён, фактический M3
snapshot пуст, исходные prerequisites/effect gates остаются; build и diff review.

Source30 PASS, Release0/0; artifacts/catalog-traditions-*.log. Автор Luna low
inline_effect_bindings, independent root actual-diff accepted. Build сначала
выявил отсутствующий using ContentSession; исправление проверено повторной сборкой.
Сценарий clone vats остаётся gated, не объявлен пройденным lifecycle.

## Tankbound source condition — 16 октября

Подключить native `is_tankbound_empire` (08_scripted_triggers_shroud:214):
OR двух has_valid_civic, civic_tankbound и civic_tankbound_corporate.
Расширить civic closure, сохранив различие selected/valid и null→Unknown.
Он должен разрешаться по переданным SourceFacts.ValidCivics до общего legacy
Tankbound enum; у трёх текущих шаблонов False, как исходный city modifier.
Это не поддержка Tankbound gameplay/job swap. Приёмка: raw AST, профиль,
три actual startup snapshots и source city job_production_overseer_add inactive.

Civic profile v2/authority v3 —60definitions. Civic85/authority13/zone30/
coverage388 PASS, Release0/0; artifacts/catalog-tankbound-*.log. Автор Luna low
inline_effect_bindings; independent root actual-diff accepted после исправления
поиска native district modifier (первый source run обнаружил Single на постороннем
effect, повтор после фильтрации успешен). Economy gates68, job swaps не добавлены.

## Machine authority condition — 16 октября

Native `is_machine_empire` из 00_scripted_triggers проверяет единственный
`has_authority = auth_machine_intelligence`. Добавить его в strict authority
closure и частичный доверенный map M3 из фактического template.AuthorityId,
как существующий ResearchFactsFor. Отсутствующий key/null остаётся Unknown;
это не поддержка machine startup/assembly и не вывод по species/gestalt.
Приёмка: source AST, импортированный helper, фактический ordinary snapshot,
остальные native technology/effect gates сохраняются. Без fixtures/browser.

Authority profile v2 содержит59 definitions; partial authority map — только
corporate/machine keys. Source13 +civic74 +zone30 +coverage388 PASS, Release0/0;
artifacts/catalog-machine-*.log. Количество runtime economy gates остаётся68;
helper не означает доступный machine gameplay. Автор inline_effect_bindings
(Luna low), независимый root actual-diff review accepted после C# local-name fix.

## Structural unity flow монументов — 16 октября

Отдельный immutable source snapshot в EconomyWorld содержит identity/definition/
economic category и рассчитанные прямые amounts. Он не является профессией,
не использует Workforce/InstalledJobEconomyEffects и идёт в отдельный report path,
общие Produced/Net и существующий атомарный monthly settlement. Snapshot полностью
заменяется по enabled installed graph (включая пустой), входит в Fork/StateHash.
Старый capacity aggregate без producer closure не принимает мир с этим каналом.

M3 первый consumer принимает только native unity produces в planet_buildings,
каждый leaf/множитель связан 1:1 с сохранённым resource_produces block/provenance.
Счётчик owner.trigger:num_ascension_perks — размер доверенного perk snapshot;
единый M3 provider хранит нынешнее известное пустое множество, как ResearchFactsFor.
Это base×count, не 1+count. Missing snapshot, inline/неоднозначный блок, иные
ресурсы/expressions и условия остаются gates. Источники startup/refresh заменяются
полностью; выключение/удаление убирает source, дважды выпуск не учитывается.

Reference01a12f66 подтвердил: 08_unity_buildings:54–55 задаёт base×count;
economic_categories/99_README:29–35 задаёт Add-exact/Mult-descendants и кратность,
но не native вычислительную фазу/агрегацию/округление structural modifiers.
Поэтому ненулевой Add/Mult, который относится к ресурсу и структурному source category,
блокирует зависимый расчёт, а не применяет job prototype. Текущий M3 research
проецирует modifiers только в synthetic job categories; этот канал их не получает.
Полный consumer с ненулевыми structural modifiers остаётся отдельным этапом.
Приёмка: native source linkage и actual M3 snapshots, build/independent review,
реальный research→build/month scenario при доступном prerequisite; никаких
вымышленных perks/tech/установленных buildings, fixtures или browser.

Проверки среза: source39 (raw9, шесть DefinitionGate pass, три отдельных
memorial gates, реальный cultural heritage prerequisite False), coverage388,
реальный HTTP/WS catalog4 и monthly44 PASS; Release0 warnings/errors.
Логи artifacts/structural-flow-*.log. Runtime economy gates74→68.
Root production / ready_interface_review actual-diff accepted после resource и
scope guards. Harness inline_effect_bindings (Luna low) / root accepted после
исправления недоказанного startup monument. Full build/lifecycle pending.

## Прямой выпуск: границы source blocks — 16 октября

Первый этап отдельного structural production channel сохраняет raw `resources.produces`
в immutable Definition.SourceFields[`resource_produces`] вместе с trigger, scalar
multiplier и точной provenance. Не связывать ResourceProduction и ResourceMultiplier
только по ресурсу/похожему условию: нужны исходный контейнер и 1:1 leaf provenance.
Inline/неоднозначные/неизвестные shapes не объявляются поддержанными. Этап только
метаданных; runtime gate прямого выпуска остаётся до отдельного consumer.
Приёмка: source-only сверка raw 4.5.2 monument/corporate/memorial уровней1–3 с
импортированным блоком/leaf provenance и прежними numeric terms, build/review,
без подмены perks/resources/research/world и без fixtures/browser.

Проверено: Release0/0, source27 PASS (`artifacts/structural-source-checks.log`)
для9 native зданий: raw дерево/line+column/terms и коэффициенты1–3 совпали.
Автор inline_effect_bindings Luna low; root actual-diff review accepted после
исправления lookup Buildings вместо отсутствующего Definitions. Runtime gate не снят.

## Корпоративные owner conditions — 16 октября

Новый conditions-authority profile = civic56 + native is_megacorp и
has_corporate_government из 00_scripted_triggers. Не заменять второй helper одним
типом authority: он также проверяет выбранную civic_galactic_sovereign_megacorp.
Core получает immutable partial snapshot результатов native has_authority по ID:
null snapshot/отсутствующий ID означает Unknown, не false. M3 передаёт только
auth_corporate по своему актуальному template.AuthorityId, ровно как уже делает
ResearchFactsFor; это не общий алгоритм наследования/совпадения authority. Старый
shelter native-authority-matching остаётся Unknown, выбранные/valid civics раздельны.
Приёмка: native raw helpers и реальные startup facts, старый civic/zone closure,
build/review, без fake corporate контекста/fixtures/browser/UI edits.

Проверено: Release0/0, authority source10/civic74/zone30/catalogue388 PASS,
`artifacts/catalog-authority-*.log`. Root production/ready_interface_review accepted;
harness inline_effect_bindings Luna low/root actual-diff accepted. Owner unknown
guards41 (до этого55), общий economy gate count74; full catalogue не завершён.

## Unsupported modifier gate в quote — 16 октября

Если raw contribution структурного planet/district modifier не False и содержит
собственные неподдержанные поля/контейнеры `catalog-modifier-*` в Gates, quote возвращает null с
dependencyId первого gate. Нельзя разрешить оплату с последующим silent effect drop.
False ветви не блокируются. Condition-only зависимости отдельно проверяет текущий
M3 term resolver, как и числовые зависимости: этот срез не смешивает его факты с неполным Core predicate set
и не отнимает поддержанные jobs из-за ещё не подключённого Core helper.
Пример: luxury residence содержит planet_amenities_add; без consumer не должно
быть quote. Приёмка: actual source luxury quote gated и реальные поддержанные
primary quotes сохраняются через HTTP/WS; no browser/fixtures/UI edits.
Housing multiplier `modifier:mining_district_housing_add` остаётся в прежнем
частичном housing channel (M3 его явно пропускает); этот срез не меняет его
политику и не объявляет numeric modifier/demand реализованными.

Проверено: Release0/0, source25 PASS (`artifacts/catalog-effect-gates-source.log`),
HTTP/WS catalog4 PASS (`catalog-effect-gates-http-final.log`). Сравнение с прежним
каталогом: удалён только ложный quote luxury residence; остальные15 quotes,
включая district_mining, сохранены. Root production/ready_interface_review accepted
после сужения numeric regression; harness inline_effect_bindings Luna low/root review
accepted. Consumer amenities по-прежнему отсутствует; это исправление gate.

## Civic-only owner helpers — 16 октября

Новый conditions-civics profile = zones42 + 14 native civic-only helpers из
00_scripted_triggers, 02_machine_age, 08_shroud и 08_infernals. Список ограничен
исследованными телами AND/OR/NOT/NOR и has_civic/has_valid_civic; исходные тела
импортируются, а не заменяются проверкой имени. has_civic читает выбранные Civics,
has_valid_civic — отдельные ValidCivics; null любого snapshot остаётся Unknown.
Hospitality-servitor с origin/country flag, authority/traits и runtime effects не
добавляются в этот срез. Старые профили/shelter остаются совместимыми. Приёмка:
native raw source OR/civic predicates против evaluator в реальном M3 старте,
сборка и независимое actual-diff review, без fake civic context/browser/fixture.

Проверено: Release0/0; civic source74, zone regression30, catalogue388 и реальный
HTTP/WS catalog4 PASS (`artifacts/catalog-civic-*`, `catalog-current-coverage.log`).
Root production/ready_interface_review accepted; harness inline_effect_bindings
Luna low/root accepted после исправления смешения количества helpers и civic страны.
Неизвестных owner guards в отчёте стало55 вместо91; runtime economy gates остаются74.
HTTP probe выявил отдельный дефект: luxury residence публикует quote, хотя raw
assessment сохраняет catalog-modifier-planet_amenities_add. Это не готовность здания;
следующий срез должен провести unsupported modifier gate до команды строительства.

## Наличие зон в условиях каталога — 16 октября

Новый conditions-zones profile расширяет deposits17 исходной closure: 22
has_any_*_zone, has_mixed_industrial_zone, has_volatile_motes_zone и has_zone.
Helpers исполняются из 4.5.2, не подменяются нашим списком эквивалентных зон.
has_zone(zone) сохраняет native num_zones(type=$zone$, value>0); доверенный
nullable snapshot InstalledZones приходит из M3ZoneGraph.Current, включая
завершённые замены и без заказов/резервирования. null остаётся Unknown.
Factory helper сохраняет owner.country_uses_consumer_goods и exists owner.
Прежние profiles/shelter scope сохраняются. Приёмка: actual-source closure и
helper evaluation на настоящем startup, independent review и сборка; UI/fixtures/
browser не затрагиваются. Это наличие зоны, не её доступность/вместимость.

Проверено: Release0/0, source30 PASS (`artifacts/catalog-zone-conditions-source.log`),
24 helper результата совпали с raw AST и реальным startup. Root production,
`ready_interface_review` accepted, включая исправление сохранения `$zone$` только
в has_zone; harness `inline_effect_bindings` Luna low, root final-diff review accepted
после перехода от imported-tree oracle к raw source и устранения повторных проверок.
Lifecycle смены зон этим readonly probe отдельно не проверен.

## Перки в условиях каталога — 16 октября

Owner `has_ascension_perk` проверяет полный доверенный snapshot полученных перков;
null означает Unknown, пустой набор — известное отсутствие. Snapshot копируется
с общей валидацией ID/размера. Текущий M3 передаёт пустой набор, как уже делает
ResearchFactsFor: отдельной системы получения перков пока нет. Это не выдача перков
и не реализация их effects/count multiplier; они остаются отдельными зависимостями.
Ветви Baol с ap_archaeoengineers должны стать False в реальном старте, сохранив
базовый бонус и prerequisite tech. Приёмка — readonly native-source/startup probe,
сборка и независимое ревью; UI, browser и fixtures вне этого среза.

Проверено: Release0/0, source10 PASS (`artifacts/catalog-perks-source.log`),
включая реальный пустой snapshot, две native perk-ветви False, baseline и tech.
Автор `inline_effect_bindings` Luna low; root actual-diff review accepted после
исправления отсутствующего namespace в source harness. Count/effects не реализованы.

## Inline параметры в идентификаторах effects — 16 октября

PlanetEconomy importer должен подставлять переданные inline параметры в TargetId,
включая embedded `$JOB$`/`$RESOURCE$`, так же как construction importer. Только
один проход замены имён из фактической таблицы вызова, без выполнения выражений,
динамического имени script и без догадок по неизвестному параметру. Неизвестный
placeholder сохраняется как unresolved, а не превращается в пустую строку.
SourceKey/provenance остаются исходными, TargetId — связанным именем; числа,
условия, storage/tech/контекст и прочие gates не меняются. Первый native случай:
building_baol_organic_plant → planet_farmers_food_produces_add, +0.25.
Приёмка: actual-source сравнение двух импортёров и raw/provenance, source coverage
и сборка; не добавлять fake installed/research/банки или fixture/browser прогоны.

Проверено: Release0/0, actual-source `--catalog-inline-sources-only` PASS6,
`artifacts/catalog-inline-source.log`: farmer food +.25 совпал между импортёрами,
raw SourceKey и inline provenance сохранены, tech_secrets_baol не выдан. Автор
`inline_effect_bindings` Luna low; root actual-diff review accepted после исправления
слишком строгого требования убрать все gates. Остальные Baol условия остаются
зависимостями; это не доказательство полного lifecycle здания.

## Origin и залежи для условий каталога — 16 октября

Owner `has_origin` получает реальный startup.Plan.Context.Origin; отсутствие snapshot
остаётся Unknown, shelter closure сохраняет собственный origin scope. Никакой origin
не назначается по аналогии с preset. Native три has_*_deposit helpers читаются из
`01_scripted_triggers_buildings.txt` вместе с habitat-equivalence dependency.
Ordinary ветвь использует видимые startup features; скрытые blocker-ом залежи не
считаются доступными. Habitat orbital/system branch без runtime graph остаётся
Unknown. Это conditions-only срез: цена/tech/slots/effects и lifecycle не обходятся.
Старые profiles совместимы; новый deposits profile содержит federation14 + три helpers.
Приёмка: readonly actual-source coverage/probe исходных helpers, visible deposits,
origin и сохранившихся guards; без fake startup/world/tech, browser/fixtures.

Проверено: Release0/0 и actual-source `--catalog-condition-sources-only` PASS24,
`artifacts/catalog-conditions-source.log`. Три zone potential совпали с native
ordinary deposit OR и видимым startup graph, unlock tech не выдан; luxury origin
guards разрешены, но amenity consumer остаётся gate. Root production code,
`ready_interface_review` accepted; harness `catalog_condition_sources` Luna low,
root final diff review accepted после исправления смешения potential/effect readiness.
Source blocker clearance пока возвращает clearance-lifecycle-unresolved; этот срез
не меняет hidden feature после удаления blocker и не закрывает T04/orbital graph.

## Общий потребитель job flow modifiers — 16 октября

По текущему поручению завершить каталог подключаем исходные flow effects группами,
включая native зоны добычи редких ресурсов; искусственные источники не вводятся.
Core assessment/installed aggregation и M3 baseline/refresh используют одну явную
таблицу category/resource/operation/Add-or-Mult. Не распознаём произвольные suffixes.
Exact Add действует на source category профессии; Mult — на неё и descendants.
Категорийный Mult без resource раскрывается на ресурсы текущей экономики, при этом
пустая база остаётся нулём. Отрицательный Add/Mult сохраняется; формула/clamp уже
существующего EconomyWorld не меняется. Выпуск и job upkeep раздельны; fixed
building/district/zone upkeep не получает модификаторы профессий.
Зоны проходят прежний source stager: planet modifier один раз на occurrence,
district modifier по attached level. Guards deposits/tech/conditions/slots сохраняются.
Неизвестные ресурсы/структурное производство/депозитные modifiers остаются зависимы
от своих потребителей. Вёрстка и исследовательская система не меняются.
Приёмка: readonly actual-source coverage по всем обнаруженным definitions, source
parameters/contexts/contributions и сборка; обычные not-eligible/can_build/slots
не считаются пропуском покрытия. Полные lifecycle сценарии отмечаются отдельно.

Проверено: shared table +42 native leaves, independent `ready_interface_review`
accepted обе части22+20. Root production code; source coverage harness
`catalog_coverage_check` Luna low, root actual-diff review accepted (включая Declared).
Release0/0; readonly coverage PASS388, настоящий HTTP/WS monthly job flows PASS44.
Логи `artifacts/catalog-flows-build/`; code build `artifacts/catalog-flows-verified/`.
Ошибочная relative generated директория исполнителя обратимо перенесена в ignored
`artifacts/catalog-flows-generated-archive/`; исходники/чужие правки не удалялись.
[Оставшееся покрытие](PLANET-CATALOG-COVERAGE.md): 644 найдено,194 included,
450 excluded context-specific,74 M3 economy gates; отсутствие gate не readiness.

## Федеративное условие административных зданий — 16 октября

Новый condition profile добавляет к city13 исходный `has_make_spiritualist_perk`
из `02_scripted_triggers_federations.txt`: exists federation AND perk make_spiritualist_1.
SourceFacts.OwnerFederationExists — nullable trusted факт, отсутствие snapshot остаётся
Unknown. Только owner `exists = federation` читает этот факт; наличие федерации
без реализованного perk-context остаётся Unknown. Перки не подменяются false.
M3 передаёт false в соответствии с текущим профилем сессии без федераций (тот же
явный факт уже используется ResearchFactsFor), не как общее правило origin_default.
Прежние condition profiles остаются совместимы. Исходные ethics/owner/technology/
modifier guards сохраняются. Приёмка: настоящий HTTP/WS build building_bureaucratic_1,
оплата340, reservation/completion314/capacity бюрократов200/месячный upkeep2 energy,
без fake ресурсов/технологий, browser/fixture и изменений вёрстки.

Проверено: Release0/0; единственный настоящий `--bureaucratic-building-only`
HTTP/WS прогон PASS10, включая native quote без gates, выбранный slot/reservation,
однократную оплату, восстановление artisan200, отсутствие установки до314дня,
capacity+200 и building upkeep+2 energy следующего месяца. Логи
`artifacts/bureaucratic-build/bureaucratic-source.log`/`.err.log`.
Root Core/Session/контракт, `ready_interface_review` accepted; общий source harness
`bureaucratic_source_scenario` Luna low, root final diff review accepted.
Native perk-context положительного членства всё ещё не реализован и не угадывается.

## Третьи уровни научных зданий: бонус Workforce — 16 октября

Native physicist/biologist/engineer_jobs_bonus_workforce_mult раскрываются через
`24_static_modifiers_jobs.txt` в ordinary, brain_drone и calculator варианты
соответствующей профессии. Материализуются только jobs текущего EconomyWorld;
неподдержанные recipes сохраняют свои gates. Исходные tier3 дают +0.1: ordinary Workforce и
capacity не растут; bonus Workforce = ordinary × (trait/research + installed global
+ installed job multiplier), effective = ordinary + bonus. Текущий общий pipeline
масштабирует effective и выпуск, и job upkeep; бонус выпуска не уменьшает upkeep.
Installed per-job snapshot хранится отдельно от trait/research, целиком заменяется
из enabled installed graph и не теряется при обновлении research/category effects.
Повторная пересборка не накапливает бонус. Clone/hash сохраняют полный immutable
snapshot. Известны только эти три leaves; неизвестные остаются заблокированы.
Приёмка: source import tier3 amounts/contributions и guards, consumer review,
сборка; полный research→upgrade→месячные потоки — отдельная ручная проверка.
UI/слоты/технологии/цена/upgrade chain этим срезом не меняются.

Проверено: isolated Release0/0; source tier3 PASS21 (включая точное раскрытие
static modifier в три pop leaves), source tier2 PASS27; настоящий HTTP/WS catalog
PASS4 на этой сборке. Три tier3 больше не блокируются workforce/economy contributions,
но can_build=no и prerequisites сохраняются. Root код, independent
`ready_interface_review` accepted, включая исправление SortedDictionary для hash;
source harness `science_tier3_sources` Luna low, root actual-diff review accepted.
Логи `artifacts/science-tier3-build/`. Browser/fixture не запускались; полный
research→upgrade→effective Workforce/monthly расход остаётся непроверенным.

## Наличие здания и голографический театр — 16 октября

Native `has_building = id` в planet scope проверяет наличие установленного
definition, включая выключенное здание; pending заказ ещё не установлен.
Evaluator читает явный BuildingCounts: отсутствующий snapshot/ключ остаётся Unknown.
M3 заполняет counts из полного текущего installed graph также для literal IDs,
упомянутых в импортированных `has_building`, даже если definition исключён из
строительного inventory. Отсутствующий в этом графе low-tech capital имеет 0,
а не неизвестное значение. Это открывает `building_holo_theatres` при выполненных
native условиях/tech; AI amenity-пороги не подменяются нулём. Source HTTP/WS приёмка:
quote/оплата/постройка/исходная entertainer capacity и energy upkeep без fake tech/
ресурсов; UI/масштаб неизменны. Федеративные/origin guards других зданий — отдельно.

Проверено: Release0/0, независимое Core/Session/contract и harness diff review
`ready_interface_review` accepted. Настоящий HTTP/WS `--holo-building-only` PASS8:
340 минералов, точная reservation, отсутствие ранней установки, completion314,
capacity+200, следующее месячное building upkeep+2 energy. Лог
`artifacts/holo-source-run.log`; ранее прерванные CG-дефицитом прогоны не PASS.
В финальном сценарии artisan0 при накоплении, artisan200 после оплаты через
обычные команды Workforce. Amenities и полный эффект профессий этим прогоном
не подтверждены; browser/fixture не запускались.

## Содержание улучшенных научных зданий — 16 октября

Вторые уровни physics/biology/engineering добавляют своим категориям профессий
исходное содержание: physicists/volatile_motes, biologists/exotic_gases,
engineers/rare_crystals — по +0.25 к базе на 100 effective Workforce.
Это job upkeep, отдельно от фиксированного содержания самого здания.
Оба consumers (Construction installed aggregation и M3 economy overlay) читают
ровно эти source leaves; при выключении/удалении contribution исчезает вместе
с остальными effects. Числа не зашиваются в runtime, неизвестные leaves блокируются.
Сохраняются native can_build=no, upgrade chain и prerequisites; исходный tier3
job-specific bonus Workforce остаётся зависимостью, его нельзя молча отбросить.
Приёмка: настоящий импорт 4.5.2, source amounts/scopes/contributions без gates
для tier2 и сохранённые technology/direct-build guards, без fake research/installed.
Полный research→upgrade→месячный расход остаётся ручной приёмкой.

Проверено: Release0/0, настоящий `--science-upgrades-only` PASS24
(`artifacts/science-upgrades-source.log`). Три tier2 проходят economy/contribution
gates, их tech/can_build/upgrade guards сохранены; каждый tier3 остаётся заблокирован
конкретным native bonus Workforce leaf. Независимый review Core/Session/контракта
`ready_interface_review` accepted; исправленный source harness Luna low проверен root.
Первый запуск выявил ошибку выбора проекции в harness, исправлен; он не PASS.

## Профильные научные здания — 16 октября

Физическая лаборатория, биолаборатория и инженерный комплекс используют исходные
Add к physics/society/engineering выпуску своих профессий. Те же точные leaves
распознаются importer assessment, installed aggregation и M3 economy overlay.
Значения читаются из definitions (первый уровень +0.25), а не хранятся рецептом UI.
Постройка даёт исходную capacity и бонус ко всем соответствующим учёным планеты;
удаление убирает оба эффекта пересборкой installed graph. Source-проверка сверяет
native Add/условия/requirements/quote и HTTP/WS startup без fake ресурсов/технологий.
Все три technology1 отсутствуют в starting techs; полный research→build→месячный
поток остаётся непроверенным, не подменяется установкой здания для теста.
Технологии, unique, compatible slots и неизвестные эффекты высших tiers сохраняются.

## Базовые ресурсные здания — 16 октября

Подключаем исходные `planet_miners_minerals_produces_add`,
`planet_farmers_food_produces_add`, `planet_technician_energy_produces_add`
к тому же installed economy overlay, который используется при старте и после
construction completion/removal. Это Add к базе соответствующей профессии,
затем effective Workforce и общие output modifiers; не отдельный доход здания.
Значение читается из 4.5.2, не зашивается в runtime. Технологии, условия,
лимит на планету, совместимость зоны, цена и содержание остаются source-derived.
Соседние множители, редкие ресурсы и неизвестные условия этим срезом не разрешаются.
Приёмка: native import/RuntimeGates и реальные условия трёх определений плюс
настоящий HTTP/WS startup. Полный lifecycle с изучением технологий остаётся
отдельной ручной проверкой; fixture и браузерные прогоны запрещены пользователем.

## Продолжение от master: весь каталог и условия

16.10 пользователь поручил подключить все возможные зоны, здания и их условия,
используя уже подключённую систему технологий. База нового цикла — master1bca87c
после завершённого merge числа игроков. Работа идёт последовательными играбельными
срезами, с independent review и коммитами. Пользовательские серверы и старые dirty
overlay-файлы не меняются. Технологии берутся из текущего ResearchCountry.Researched,
а не только из starting techs; команда, preview и ежедневный пересчёт используют
один актуальный строительный контекст. Условия 4.5.2 сохраняются; Unknown не True.

Первый срез: взаимная замена industrial/foundry/factory/research_unity в city01/02.
Все четыре source definitions включают urban и дают три building slots. Список
preview содержит остальные применимые цели; target potential/unlock/tech/effects
проверяются прежним assessor. Industrial/factory требуют подтверждённого CG use,
остальные — non-wilderness. Housing-терм у этих четырёх общий; новые housing-числа
этим срезом не вводятся. Сохраняются капитал/заказы/оплата/fingerprint/confirmation.
Зона и её здания привязаны к slot/ZoneInstanceId, повторные definition IDs допустимы.
Приёмка source HTTP/WS: industrial→factory сохраняет совместимую factory_1;
factory→research_unity удаляет её только при completion, пересчитывает jobs и
сохраняет три child slots/instance. Цена/скорость здания, штатное накопление,
без выдачи технологий/банков и без fixture/browser-прогонов. Остальной каталог
и его predicate coverage остаются следующими основными срезами, не закрыты этой четвёркой.

Проверено16.10: Release0/0; независимое ревью workforce_header_review accepted.
Настоящий HTTP/WS `--city-zones-only`: PASS17, штатные цена850/314дней,
совместимость factory_1, slots/instance, jobs delta и две research occurrence.
Повторный отдельный процесс завершился; первый прерванный запуск не засчитан.
Локальный лог: `artifacts/city-zones-background.log`; browser/fixture не запускались.

Следующий срез: городской каталог выводится из импортированных zone_sets=urban,
unconditional per-instance slot modifier +3 и отсутствия отдельных cap/scaling
полей. Неизвестная/условная/дублированная форма мест не допускается в этот executor.
Обычные research/physics/society/engineering/unity/spiritualist/fortress/trade
попадают в preview вместе с прежними четырьмя, но target eligibility и runtime
economy gates сохраняются. zone_urban со scaling/max_buildings и исключённые
specific-context definitions не маскируются обычной тройкой мест. Создание пустой
городской зоны использует тот же quote/slot/conditions путь. Приёмка: настоящий
source HTTP/WS snapshot содержит все 12 ordinary форм и quotes исследовательских
целей без выдачи ресурсов/tech; завершённый city4 lifecycle повторно не прогонять.

Проверено: Release0/0, source `--city-catalog-only` PASS19 после исправления review.
urban_catalog_review: вложенные cap-поля тоже отклонять; исправлено, повторное
ревью accepted. Лог `artifacts/city-catalog-final.log`. Research x4 quotes850;
unity/spiritualist — eligibility-unresolved, trade — trader recipe, fortress —
source economy unresolved. Это сохранённые gates, не завершённые операции.
Создание пустой городской зоны отдельно не прогонялось; browser/fixture не запускались.

Условия единства: импортировать native is_spiritualist/is_fanatic_spiritualist
(00_scripted_triggers:1659–1668), исполнять их через owner has_ethic по полному
доверенному startup.Plan.Context.Ethics. Отсутствие snapshot остаётся Unknown;
shelter-контекст сохраняет свой прежний fallback. Технологии не дублируются.
Приёмка source economic preset: обычная unity имеет quote850, духовная unity
запрещена известным native potential, а не неизвестным предикатом; другие gates
и действующие materialist presets неизменны. Это не реализация смены этик империи.

Первый source-прогон обнаружил startup ArgumentException: старый rural validator
требовал ровно11 definitions. Он не засчитан. Расширение вынесено в отдельный
`construction-conditions-city-catalog-452-v1` с13 definitions; прежний rural11
сохранён. City проходит тот же rural baseline, проверку helper IDs/kinds и owner?.
После исправления Release0/0, повторное независимое unity_condition_review accepted,
source HTTP/WS PASS21 (`artifacts/unity-condition-final.log`): unity quote850,
spiritualist not-eligible. Browser/fixture не запускались; trade/fortress pending.

## Крепость и торговля — следующий играбельный срез

16.10: сначала снять обе блокировки, без browser/fixture-прогонов. Крепость:
native zone_soldiers_add различает habitat/ecu/ring-world; ordinary continental
использует standard district set (01_scripted_triggers_buildings:1148–1165).
Baseline evaluator и live contribution facts должны одинаково разрешать эти
три проверки для поддержанного preset. Остальные неизвестные контексты не открывать.
Приёмка: source HTTP/WS industrial→fortress, pay850/314дней, те же три child slots,
soldier capacity = quote × фактический district level, содержание профессий.
Создание army instances и набор армий остаются отдельным T06, без demo-гарнизона.
Позднее прямое решение16.10: job swaps — отдельная система для всего набора подмен,
не локальное правило двух профессий. Текущие правки сохранить, продолжать зоны и
здания; реализацию подмен сюда не встраивать. Торговля пока зависит от этой системы:
условные +2 trade/+1CG торговца требуют underground contact deposit snapshot.
Нельзя снимать gate глобальным false или терять условные потоки. Никакого кода
подмен профессий в текущий срез не внесено.

Крепость: Release0/0, независимое fortress_slice_review accepted. Source HTTP/WS
повторный прогон PASS11 (`artifacts/fortress-final.log`): pay850, 314дней,
slots/instance/jobs, 300 soldier WF и фактический alloys upkeep по рецепту.
Build изолирован от сервера5257; код `730a912`, harness fix `45aeab8`.
Первый прогон завершил замену крепости и проверил slots/instance/jobs, затем
test setup получил INVALID_TARGET при сбросе лимита отсутствующей zero-capacity
профессии. Фильтр actual capacity>0 исправлен и повторно reviewed; первый прогон
полным PASS не считается. Browser/fixture не запускались.

Следующий независимый срез zone_urban: native 99_HOW_TO_ZONE определяет max_buildings
как число мест; здесь ровно3, districts_per_building/class/modifier-cap отсутствуют.
Допускается только эта фиксированная source-форма: один корневой max_buildings=3
в дополнение к уже проверенному unconditional per-instance slot modifier +3.
Числа не складываются в шесть мест: используется прежняя startup policy fixed3,
а modifier остаётся отдельным исходным вкладом. Иные cap/scaling формы закрыты.
Include all/excluded building sets остаются authoritative; factory_1 сохраняется.
Приёмка: source industrial→urban, 850/314дней, те же три child slots; затем
building_stronghold в свободном слоте этой occurrence, native tech prerequisite,
340 минералов/209дней/200 soldier WF и energy upkeep1. Без новых job swaps.

Проверено: root Graph; urban_building_scenario (Luna low) — source harness,
root исправил/проверил C# shadowing и выбор свободного слота; затем отдельный
urban_fixed_cap_review accepted, включая root fix monthly-report timing.
Release0/0. Source HTTP/WS: catalog13 PASS22 (`artifacts/city13-catalog.log`),
urban→stronghold PASS18 (`artifacts/urban-building-final.log`): paid850/314дней,
factory сохранена, occurrence/slots/jobs проверены; stronghold paid340, work1,15,
completion209 в том же child slot, capacity+200 и upkeep+1 в следующем месяце.
Ранний процесс остановлен до completion из-за исправленной timing-проверки,
полным PASS не считается. Browser/fixture не запускались. Это construction/economy
срез; T06 и отдельная система job swaps не реализованы и сохраняют свои зависимости.

## B02: обычная фабрика — первый живой сценарий

15.10.2026. После требования снять production-блокер пользователь ответил на точный
вопрос об отключении собственной исправной `building_factory_1` без ожидающего
заказа: «Да, мы уже отвечали на все эти вопросы в чате по ревью». Принятый ранее
ответ чата `01a13af2-d879-77d0-99fc-5beccaa2dfa5` подтверждает отсутствие effects,
jobs/upkeep при выключении с сохранением instance/slot.

Для этого точного ordinary/default-origin немобильного случая доступен toggle
при отсутствии source `can_be_disabled`. Явные no и неизвестные формы поля сохраняют
запрет; это human capability для фабрики, не доказательство native-default всех
153 definitions. Pending building/zone operations в этом первом сценарии не входят
в разрешение. Цена/очередь не создаются. Session пересчитывает installed economy и
WF атомарно; обратное включение восстанавливает эффекты ровно один раз.

Приёмка: штатный economic preset и локальный импорт 4.5.2, inspector фабрики,
toggle off → исходный instance/slot и банк сохранены, factory jobs/upkeep удалены;
toggle on → восстановлены, повторное открытие показывает authoritative состояние.
Без synthetic permissions, stocks, all-tech и модификации установки.
Неактивные отдельные overlay-правки в этот срез не включаются.

Приёмка `15b84b2`, 15.10.2026: PASS на exact archive Release (0 warnings/errors),
локальный сервер 5247, protocol2/m3; served client SHA256
`09dbda98faaaf76d645c7bdc2773cc3f480c03b0b617801c8bd4ef6c08eb9a82` совпал с commit.
Штатные два игрока, economic/scientific, tiny200/elliptical, seed
`e8f3617c9feb42cf81d77cbd14c6a275`; UI start → оба локально импортировали918файлов
(454 assets +464 EN/RU, русский,0диагностик) → автоматический запуск.
Собственная Планета7/Система41, дата2200.01.01 на паузе: factory off/on через
инспектор, вместимость artisan350→150→350; factory jobs200→0→200,
energy upkeep2→0→2; все видимые запасы неизменны, очередь пуста.
Закрытие/повторное открытие сохраняет включённую фабрику и её эффекты.
Ни stocks, ни permissions, ни technology fixture не подменялись.
DOM checkpoints локально: `artifacts/factory-live-15b84b2-dom.txt`.
Локальные ignored screenshots (не включать original/derived assets в Git):

- [До](../content-local/references/factory-live-before-15b84b2.png), SHA256 `833d835b42ce948d7a726c1c220a4cc52cdb9cb476a513c1adc79242d4192001`.
- [Выключено](../content-local/references/factory-live-off-15b84b2.png), SHA256 `0fa82d8d258b21831a040d887b34c3c00075977c93a1bfda0da731c3478f0f60`.
- [Повторное открытие](../content-local/references/factory-live-reopened-15b84b2.png), SHA256 `35cb686d8025eba866e05473331ebf5c7b83d006a0a4be0c3a28261eb500522c`.

Это приёмка одного production toggle-сценария, не полного плана. Native-default
всех зданий/repair и отдельный negative pending-order test остаются непроверенными.

## Зоны: принятое правило скорости и стоимости

16.10.2026, прямой ответ пользователя: «зоны используют скорость строительства.
А также стоимость». Создание и замена зоны используют тот же текущий модификатор
скорости, что здания, и общий модификатор стоимости строительства. Базовые цена
и работа остаются source4.5.2. В частности, functional_architecture применяется
к зонам и по стоимости, и по скорости; research-изменение скорости действует
только на оставшуюся работу, оплаченная quote не пересчитывается.
Это закрывает ZONE-SPEED-01 как принятое игровое правило; отдельный native-опыт
не объявляется выполненным. Прежние записи Unknown ниже — исторические.
Права, exact preview/confirm, prerequisites, slots, однократное удаление зданий
и settlement сохраняются. Отдельная зона не принимает клиентские numerics.

### Приёмка обычных транзакций без браузера

Для Q03/R01 используется отдельный focused режим реального HTTP/WebSocket-сервера:
две обычные империи, штатные Ready/start, текущий generated manifest и проверка
его файлов локальной установки потоковым SHA256 до assetReady. Без reflection,
подмены банков/permissions/технологий, synthetic quotes/worlds и browser/WebGL.
Ресурсы на район накапливаются обычными WF-командами и stepDay. Приёмка включает
оплату/резерв → дневной progress → ручную отмену → точный refund/release, повтор
того же commandId без второго возврата и возврат того же участника после reconnect
с новым подтверждением текущего manifest. Сервер/порт принадлежат проверке,
останавливаются в finally; это transport/source приёмка, не визуальный PASS.

16.10.2026, production zone-speed diff от `f3a5e70`: PASS12 на настоящем HTTP/WS.
Economic/scientific, два источниковых mining district построены за обычные255;
ресурсы накоплены без подмены банков. Industrial→foundry: quote850 минералов,
360/1,15 дней, debit ровно850, после дня work1,15 и старая фабрика сохранена.
Отмена вернула850 и сняла reservation; replay не дал второго возврата.
После token reconnect повторный заказ завершился за314дней: foundry установлен,
несовместимая factory удалена, заказ completed. Лог `artifacts/zone-source-http.log`.
Первый запуск выявил гонку проверочного клиента Abort→Join до серверного disconnect;
заменён на bounded ожидание публичного connected=false. Итоговый cached-snapshot
helper отдельно проверен коротким настоящим source reconnect: PASS4,
`artifacts/zone-source-reconnect.log`. Полный прогон проверял предыдущий helper,
ожидавший свежий публичный snapshot; production-код одинаков в обоих.
Release ServerChecks0/0; два затронутых test-проекта скомпилированы0/0, fixture
сценарии не запускались. Независимый actual-diff review workforce_header_review
принял production, assertions и окончательный helper. Временные серверы остановлены.
Native game/UI render, остальные зоны и live zone creation этим не подтверждены.

Карточки замены зоны и inspector установленной зоны показывают authoritative
InfrastructureUpkeep/JobCapacity через общий ресурсный форматтер. Профессии
локализуются по публичным jobDefinitions/jobs и обозначаются WF; descriptionKey
карточки разрешается в текстовую подсказку. Команды и права не меняются.
Root, independent workforce_header_review accepted, node --check PASS;
визуальная приёмка отложена по требованию избегать тяжёлых browser-прогонов.

### Обратная промышленная специализация

Следующий основной переход — foundry→industrial в тех же city01/02. Source
`common/zones/00_zones.txt:132–168` и shared_industrial{,_foundry}_zone подтверждают
общие urban sets и три building slots, с пересчётом целевой jobs recipe и точным
удалением несовместимых зданий. Само наличие исходной зоны не доказывает unlock.
Industrial требует country_uses_consumer_goods: `00_scripted_triggers_ai.txt:62+`.
Для текущего живого владельца положительный CG upkeep его authoritative economy
report достаточен для true независимо от uninitialized/civic ветвей trigger.
Без такого доказательства факт остаётся Unknown; false по отсутствию расходов
не выводится. Owner facts не принимаются с клиента, UsesFood не переиспользуется.
Приёмка: обычные ресурсы/права, свежий reverse preview с quote, pay/reserve,
progress и завершение без воскрешения удалённой фабрики; остальные пары не открываются. Сохраняются
capital guard, pending locks, fingerprint, фиксированная оплата и atomic completion.

16.10, reverse diff от9954f75: independent workforce_header_review accepted.
Source HTTP/WS PASS16: штатные WF/накопление1700, forward cancel/refund/replay/
reconnect/completion, затем reverse quote850, однократная оплата и completion314.
Промышленная зона вернулась, удалённая фабрика не восстановилась. Временный сервер
остановлен, fixture/browser не запускались. Лог `artifacts/zone-roundtrip-http.log`.
Release ServerChecks0/0; чистый reviewed Git tree
`9f3a9bab7c89a81f98bf4b22c5b93b55be708195` без трёх чужих dirty-путей собран
Release0/0 с существующими локальными npm dependencies (без установки).
Лог `artifacts/zone-exact-tree-build.log`; runtime source совпадает с коммитом,
последующая запись результатов меняет только документацию. Другие пары остаются pending.

### Создание ресурсной зоны в текущем пресете

Экономический preset имеет functional_architecture. Source helper
`00_scripted_triggers.txt:5595–5608` подтверждает has_functional_civic и
has_tech_or_functional_civic; slot_minerals не требует дополнительно выданной
технологии при этом civic. Для C02 используется тот же настоящий source-сервер
с отдельным --zone-create-only: штатное накопление, quote/pay, work1,15,
completion314, три новых building slots и увеличение miner capacity ровно на
authoritative quote.JobCapacity при исходном attached district level.
До completion эффекты и слоты не появляются. Технологии/банки не подменяются.

16.10, HEAD398ed41/runtime unchanged: C02 source HTTP/WS PASS10.
Economic/scientific, 462 manifest files проверены локально потоковым SHA256;
profileDigest `05316a96b0cb1bd612dc11c1209ed99b65684242bf9184105367313a1f56ab23`.
Два mining district завершены штатно,850 минералов накоплены. Zone quote/pay850,
после1дня work1,15 без новых slots/installed effects; после314дней zone_minerals
в том же оплачиваемом slot, ровно3новых связанных building slots, installed miner
contribution совпал с quote и economy capacity увеличилась ровно на него.
Заказ completed, собственный сервер остановлен. Independent workforce_header_review
accepted; Release0/0; лог `artifacts/zone-create-http.log`. Fixture/browser не запускались.
Это первая live create-приёмка economic/minerals, остальные классы/варианты и UI pending.

### Результат живого заказа района

15.10.2026, exact `15b84b2`, два обычных игрока economic/scientific,
seed `05ca841bbc7d4f508d720ee8b7084961`, tiny200/elliptical;925 локальных файлов,
русский/464 EN/RU,0диагностик. Собственная Планета3/Система98.
Каталог при200 минералах показывает карточку340 недоступной. Через собственные
WF-команды накоплены298,4 минерала; mining district принят за255 →43,4,
pool12+1/18, free5, work0/240. Дневной шаг дал1/240; далее наблюдены53/240,
200/240,233/240. На233/240 дефицит минералов второго игрока атомарно остановил
день; после его штатной балансировки WF следующие7дней завершили заказ.
На2201.06.07: pool13/18, free5, mining level3/4, очередь пуста,
ёмкость/занятость miner400→600, без повторной оплаты/установки. PASS обычного
district happy path; это не доказывает zone-speed/native defaults или весь план.
Промежуточный дефицит consumer_goods первого игрока также устранён WF-командой.
Отдельное последующее human-решение разрешает минералам оставаться0 при дефиците;
оно не входило в этот runtime и проверяется следующим срезом.
DOM: ignored `artifacts/district-live-15b84b2-dom.txt`.

- [Принят](../content-local/references/district-live-accepted-15b84b2.png), SHA256 `932508d5528a2fe3101befd8bc5fe2f90442dee9a8e0b8c37739d51c496ba051`.
- [Прогресс](../content-local/references/district-live-progress-15b84b2.png), SHA256 `ec0d1f7c04d29db5b7d656a4bcd381141f5f606aba4fe739bdbd430d28825458`.
- [Завершён](../content-local/references/district-live-complete-15b84b2.png), SHA256 `76fbf3df0cc98e52230756239047e38e88371f184b17c71b7bab4313f3dfd7a4`.

## V03: выбор колонизированного body карты

Открытое окно при выборе body, который planetPanelModel однозначно разрешает в
читаемую колонию текущего POV, обновляет ту же colony window как при выборе colony.
Та же колония сохраняет inspection/handlers; другая очищает target-specific состояние,
сохраняя общую геометрию и вкладку. Недоступный/чужой body не открывает private model;
fleet/прочие объекты сохраняют прежнее закрытие окна. Пустой selection/движение карты
не закрывают окно. Выбор body сам по себе не создаёт move command/не меняет POV.

Root/Luna low, /root/zone_contract_review принял actual diff и исправленные тесты:
stale callback проверен до смены tab при одинаковых revision/profile/slot IDs.
Node311/311, Release0/0 PASS; controlled browser5246 проверил same-body/open,
C1→C2/jobs/geometry14,22/no commands, null selection/open и foreign/close.
Fixture использует production select/resolver/window/view с synthetic DTO и
упрощёнными transport/render-control stubs, не full controller/game. Fixture/tab закрыты.
[Локальный снимок](../content-local/references/planet-body-selection.png), SHA256
`4fa4897effc143ebc2864cb1e42889cb39fa8086b54e628b18b71b7a9af0f8a2`.
Логи artifacts/body-selection-{ui,build}.log.

## Q04: непредставимый подтверждённый темп

Если известный live bonus делает 1+bonus непредставимым decimal, заказ уже
неработоспособен: отменить с calculation-failed и общим capped refund, включая
waiting/N=0/Unknown остальных условий. Проверка не начисляет работу, не затрагивает
другие installed instances и не создаёт credit при replay. Factor=decimal.MaxValue
сам по себе допустим: work ограничен остатком. Null tempo остаётся Unknown,
неполный bank/storage откатывает всю candidate transaction. Приоритеты payer-missing
и non-positive-speed сохраняются. Отдельное UI-уведомление называет ошибку расчёта,
без перечня excess. Это закрывает численный failure; ошибки внешнего completion/
economy candidate остаются отдельной зависимостью, не перехватываются вслепую.

SourceLive3presets/Core, Session --condition-refresh-only, Release0/0 и Node308/308
PASS. Root/Luna low, /root/zone_contract_review принял actual diff без замечаний.
Первая waiting fixture ошибочно оставляла Unknown у head; исправлен только тест.
Логи artifacts/calculation-failure-{live,session,build,ui}.log.

## Q02: текущий темп и оценка очереди

Pending order публикует executionState active/waiting/paused по текущему N и порядку;
поле не подменяет terminal status. DailyWork и RemainingWorkDays считаются сервером
по принятому prototype daily-work=1+live bonus, ceil(remaining/dailyWork). Это дни
собственной работы после активации, а не обещание даты завершения waiting-заказа.
При N=0/Unknown/nonpositive/непредставимом вычислении срок null; quote/paid/required
work и накопленная работа не меняются от проекции или смены скорости. Policy ID
отличает принятую модель от не подтверждённой native aggregation/rounding.

SourceLive (3 пресета), Session --queue-only, Release 0/0 и Node 302/302 PASS;
независимый /root/zone_contract_review принял весь diff. Near−1 overflow проверен:
непредставимая quote duration даёт arithmetic-overflow, snapshot не падает.
Controlled browser5244: live 254→380 дней при неизменных 100/480 work,
waiting «Собственная работа», N=0 paused/Unknown; production view/CSS,
не полный controller/game/native. Fixture остановлена, tab закрыт.
[Локальный снимок](../content-local/references/planet-queue-live-estimate.png),
SHA256 `65420b7985b6f90e5d3ccfa9f7f9ea6a1804f55392219290d02582ad3b9543cc`.
Логи: artifacts/queue-estimate-{live,session,build,ui}.log.

## Q04: подтверждённое исчезновение payer

Отсутствующий account сам по себе означает неполный bank context, а не исчезновение
страны. Доверенный bank snapshot отдельно передаёт AbsentPayerIds, несовместимые с
существующими accounts. Заказы такого payer отменяются без refund/перенаправления,
с причиной payer-missing, включая waiting/N=0 и Unknown остальных условий. Stage
сохраняет подтверждённое отсутствие; повтор не создаёт credit. Передача другому
владельцу также не перенаправляет credit; уничтожение имеет приоритет destroyed.
Session producer исчезновения страны/colony и vanished-colony event ещё pending.

## B01: замена отдельного здания — доверенный Core

Queue DTO передаёт BuildingReplacement с точными
SourceDefinitionId/ZoneInstanceId отдельно от upgrade/zone replacement. View
проверяет единственный pending order, installed source и slot/zone binding;
только тогда показывает 50/50 старое/новое изображение и progress. Старое enabled
остаётся текущим, tooltip/ARIA называют обе стороны. Cancel снимает split;
completion сохраняет inspector по slot и показывает новый включённый instance.
Повреждённый/чужой tuple не получает destructive preview. Это проекция принятого
Core primitive, не native permission: новые wire/adapter capabilities не открыты.

SourceLive3presets, Release0/0 и Node306/306 PASS; root/Luna low,
/root/zone_contract_review принял backend и исправленный UI diff. Ревью обнаружило
лишнее требование runtime zone-slot: default/government уже имеет canonical startup
zone/building slots без Core zone-slot, это покрыто отдельной регрессией.
Controlled browser5245 production view/CSS: обе zone bindings, 50/50 (14/14 px),
disabled badge, cancel old state, completion new enabled и inspector same slot PASS.
Synthetic DTO/images/toolbar, не full controller/native command. Fixture/tab закрыты.
[Локальный снимок](../content-local/references/planet-building-replacement-pending.png),
SHA256 `23d455618a5205b45c9e427d6ea551b6bb0e68b668eb7676841d363708e2ac23`.
Логи artifacts/building-replacement-projection-{live,build,ui}.log.

Доверенный Core принимает точные source definition/zone instance/slot и полный quote
другого здания без требования upgrade edge. Пока заказ pending, работает старый
instance в текущем enabled-состоянии; toggle разрешён, остальные действия этого
места требуют отдельной отмены. Cancel сохраняет старый graph; completion атомарно
заменяет его новым включённым building. Source mismatch ведёт к condition-lost;
цена фиксируется при enqueue, live speed/условия пересчитываются по Q04. Native
разрешения на замену старого здания проверяются отдельно (B01-REPLACE-01): отсутствие
can_demolish и его связь с replacement не интерпретируются как разрешение.
Приёмка: source/slot/zone/authority/stale/replay, no early effects, toggle во время
pending, новое always enabled, cancel/refund, exact completion, inspector same slot.

Core primitive проверен synthetic fixture; SourceLive regression и Release PASS.
Adapter/wire/UI activation и inspector ещё не реализованы для этой операции.
B01-REPLACE-01: reference подтверждает Unknown permission/default; LOC main_1
1191/1226/1239–1240 и buildings/00_example8–12 не связывают can_demolish с replace.
Минимальный опыт absent/yes/no одного здания, затем отдельно capital/is_essential
передан прежнему manual owner; запуск/результат не заявляются.

## C03: точная замена городской специализации

Поддержанные city slots 01/02 уже имеют source occurrence IDs и три собственных
building slots; перед заменой они становятся частью текущего Core graph без смены
идентичности и двойной экономики. Government/default остаётся отдельно: source
set mismatch не исправляется догадкой. Проверять slot/district/zone instance отдельно.

Первый target-переход: industrial→foundry в той же occurrence. Сервер вычисляет
совместимость по include/exclude/building sets; factory несовместима, foundry
совместима. Preview содержит точный список slot/definition IDs, имена и fingerprint
source/target/revision/состава. Stale согласие не оплачивает заказ. Непустой список
требует подтверждения без возврата; пустой заказывается обычным кликом. До completion
старая зона и здания работают; completion сохраняет совместимые instances/enabled,
удаляет только подтверждённые несовместимые без refund, меняет zone binding трёх
мест атомарно. Cancel сохраняет старый graph. Действия блокируются только в этой
occurrence; чужая зона не затрагивается. Цена/work fixed, live speed и условия Q04.
ZONE-SPEED-01 по-прежнему Unknown: успешный lifecycle fixture не открывает production.
Core хранит immutable replacement payload: source definition, zone instance и точные
пары несовместимых building slot/definition. Он блокирует building enqueue/upgrade/
demolition этой occurrence до отдельной отмены; toggle остаётся доступен. Adapter
повторно сверяет source compatibility перед каждым днём. Completion меняет binding
всех building slots этой occurrence вместе с installed graph; чужие места не меняет.
Приёмка: canonical graph/no double jobs, exact preview/stale/no pay, scope locks,
compatible survivor/disabled state, no early effects, cancel/replay/atomic completion,
непредусмотренная дополнительная потеря → auto-cancel, UI 50/50+warning/confirm.

Подключены Core/adapter/Session/wire и UI первого перехода industrial→foundry.
Сведения открывают каталог замены в том же контейнере. Карточка показывает стоимость,
срок и точные потери; отдельное согласие называет здания и отсутствие возврата.
Пустой список — один клик. Новый snapshot/POV/session/tab закрывает старое согласие.
Pending показывает 50/50 и предупреждает только согласованные exact tuples, не
выключая здания. Cancel возвращает старый вид; completion показывает текущий graph.
Native скорость и остальные переходы остаются pending, fixture не снимает gates.

UI QA: production view/CSS + извлечённые функции modal, controlled fixture 5243,
синтетические картинки и send; не full controller/game. Cancel→0/confirm→1,
720px halves91,359/91,375, warning ровно1; completion новая зона/3слота/factory gone.
Controller guards отдельно покрыты Node; всего 298/298 в семи client suites.
Локальные снимки (не загружаются/не входят в Git):
[confirm](../content-local/references/planet-zone-replacement-confirm.png),
SHA256 `15cfda280049c7d40a5cf12fbb410911acff897ecb7b93b5d9c6eb144eb15bef`;
[pending](../content-local/references/planet-zone-replacement-pending.png),
SHA256 `a1d5cc909e2f295b42fba8472c52a01567b850a5600925f83128fd65ba5fb43a`.
Fixture process/tab закрыты после проверки; пользовательские процессы сохранены.

## Q04: пересчёт pending условий в инфраструктурной транзакции

После принятого изменения installed graph Session заново строит scoped context по
candidate и проверяет pending orders до публикации банка/экономики. Известная потеря
условий или скорость ≤0 отменяет заказ с исходным payer/capped refund и причиной;
Unknown отклоняет зависимый candidate. Проверка не добавляет дневную работу, receipt
или revision. Цена принятого заказа неизменна. Exact-slot demolition lock сохраняется.
После дневного completion повторная проверка видит уже завершённый graph; refund
ограничивается банком после предыдущих deltas. Приёмка: active/waiting/N=0, loss/reuse,
неизменная работа/quote, один общий revision, исходный payer/cap, rollback/replay,
новый installed context и atomic Session publish. Native permissions не расширяются.

## B03: демонтаж здания — контракт

`demolishBuilding` принимает colony/revision/definition/slot и обязательное
`confirmDemolition=true`. Только владелец с актуальным снимком; отдельное клиентское
подтверждение называет здание и явно сообщает отсутствие возврата. Операция сразу
удаляет точный installed instance, сохраняет место, не создаёт очередь и ресурсные
deltas. Pending order этого места требует отдельной отмены. Stale/replay/conflict
проверяются до мутации; экономика пересчитывается в существующем Session candidate.

Разрешение читается из явного `can_demolish=yes/no`; отсутствие остаётся Unknown
(B03-DEMOLISH-01). `is_essential` само по себе не подменяет permission. Pending orders
других мест проверяются по новому candidate до публикации (Q04); общий временный
pending gate снят, exact-slot prerequisite сохранён. Проверены все 165 ordinary definitions: 0 yes,
9 no, 156 absent. Столица явно no; `00_example.txt` не документирует default.
Приёмка: точный target/authority/strict wire/confirmation, pending lock, отсутствие
refund/очереди, повтор без двойного удаления, освобождение места и снятие jobs/upkeep,
актуализация inspector при исчезновении instance; fixture не доказывает native default.

## B02: enabled и наследование улучшения — 15 октября

Installed building хранит enabled; обычная новая постройка включена. Toggle —
немедленная owner/revision/definition/slot команда без платы и очереди. Pending
upgrade не блокирует toggle; completion наследует enabled текущего старого instance,
cancel оставляет его состояние. Повтор команды не меняет состояние/экономику второй
раз. Disabled сохраняет принадлежность и полную яркость, но не даёт jobs, effects,
содержание и capacity modifiers; перерасчёт экономики и WorkforceLimit атомарен.

Разрешение отключения читается только из явного literal `can_be_disabled=yes/no`
source definition. `is_essential` не универсальный запрет (memory vault явно yes).
Отсутствующий field остаётся Unknown: DISABLE-N01 у original-game owner pending.
Ни новая команда, ни клиент не подставляют default. Ruin/repair/replacement — отдельные
операции; этот срез не создаёт разрушенных зданий и не объявляет их готовыми.

Приёмка: authority/stale/replay/no-op/точный target, explicit yes/no/absent;
toggle pending upgrade→completion/cancel, немедленное удаление/возврат source jobs/
upkeep/modifiers без двойного вычета, текущая DTO/capability, strict wire/client
guards. Trusted fixtures поведения отделяются от native permission и full-game QA.

Source audit 4.5.2 / 165 included ordinary definitions: disable yes/no/absent =
0/12/153; ruin = 0/13/152. Явный disable=no: шесть capitals, Gaia Seeder tiers и
Order Castle. Контрпример essential+disable=yes — исключённый по civic memory vault,
`common/buildings/17_paragon_buildings.txt:9–16`. Repair defines
`common/defines/00_defines.txt:648–649` дают только 0.5, не доказанную формулу;
REPAIR-N01 у manual owner pending. Пересчёт source условий/пула после toggle и
демонтажа выполняет Q04. Native default permissions, repair, ownership/removal
events и ещё не реализованные системы условий остаются отдельными зависимостями.

## C02: ресурсная зона и её места — 15 октября

Для поддержанных трёх regular/default-origin пресетов сервер читает `zone_slots`
района, совместимые `zone_sets`, `potential`/`unlock` зоны и места из original
4.5.2. Отсутствующий необязательный potential не заменяет неизвестное условие;
существующее условие вычисляется tri-state. Functional Architecture открывает
технологический helper у всех трёх пресетов. Requirement сохраняет исходный LOC key.
Идентичность места привязана к district occurrence и source slot, а не уровню.

Пустая зона использует обычный enqueue/pay/reserve/cancel/replay. До completion нет
зоны, её effects и building slots. Completion одним candidate добавляет зону и ровно
три принадлежащих ей места (принятое правило плана); effects зоны масштабируются
текущим завершённым уровнем своего района. Повтор дня не дублирует места/effects.
Чужой район, несовместимый zone set, stale revision и занятое место отклоняются.
Смена существующей зоны относится к C03; kind-sharing gate сохраняется.

Native zone speed остаётся Unknown: `base_buildtime=360` и наследование категории
`planet_zones` не доказывают применение building speed. Production не подставляет
ноль или +15%; зависимый enqueue gated. Доверенные тестовые numerics могут явно
задавать скорость для проверки lifecycle, но не доказывают native поведение.
ZONE-SPEED-01 передан владельцу original-game экспериментов: одинаковый save/zone,
бонус 0/+0.15, точный прогресс за равные дни и смена бонуса во время заказа.

Приёмка: source unlock/locked/unknown, все три rural типа, pay/reserve/cancel/reuse,
completion→3 места, повтор дня, отсутствие ранних effects, attached-level jobs и
месячный upkeep ровно один раз, приватность/права и независимое ревью. Fixture и
реальная source/browser QA указываются раздельно. Неизвестная скорость блокирует
только зависимую активацию, остальные пункты полного плана продолжаются.

Источники: `common/districts/00_DOCUMENTATION.txt:21`,
`02_rural_districts.txt:17–19,133–135,267–269`; `common/zone_slots/00_zone_slots.txt:67–132`;
`common/zones/00_zones.txt:947,1063,1098`, `99_HOW_TO_ZONE.txt:64,73`;
`common/inline_scripts/zones/shared_{energy,minerals,food}_zone.txt` и транзитивные
job helpers; `common/scripted_variables/100_scripted_variables_zones.txt:6–7,20`.

## Обычные ресурсные районы: общий и type pool — 15 октября

Срез ограничен уже поддержанными regular/default-origin пресетами economic/scientific/
military. `shared_capacity_modifier` и `is_uncapped` читаются из определений rural;
транзитивные uncapped triggers импортируются из 4.5.2. Неизвестный узел/контекст
блокирует только соответствующий тип. Native исключения volcanic/ring/subterranean/
anglers/carrier не подменяются `false`; evaluator использует явные trusted facts.

Type capacity = видимые startup deposit modifiers + применимые PlanetModifier terms
завершённых installed definitions, включая масштабы attached-zone districts. Pending
не даёт modifiers/jobs/upkeep. Все definitions одного shared pool расходуют его вместе;
отдельно расходуется общий canonical district slot pool. Зарезервированные уровни
учитываются при quote/каталоге. Отмена освобождает оба пула, completion переводит
reservation в установленный уровень и пересчитывает экономику/лимиты. Условия/caps
проверяются перед дневной фазой; при уменьшении type cap сохраняется допустимый префикс
заказов, лишние отменяются по общему lifecycle. Никакого нового arbitrary queue cap.

DTO `districtTypePools` сообщает source binding/uncapped/cap/installed/reserved/free;
UI показывает полученный type limit и pending segments, кнопка на cap видима disabled
без tooltip причины. Новые zone occurrences/разблокировка остаются отдельным C02:
заказ обычного rural level сам не создаёт зону или её building slots.
Приёмка: все три source presets, все rural types, общий/type cap и reservations,
mixed shared definitions при поддержанном binding, cancel/reuse, pending без effects,
completion jobs/upkeep и cap-building effect; неизвестный источник не разрешает заказ.

Источники C01: `common/districts/00_DOCUMENTATION.txt:8,10` (uncapped/shared pool),
`02_rural_districts.txt:5,122,255` (bindings/quotes),
`common/scripted_triggers/00_scripted_triggers.txt:3888,3948,4407,4427,4452`
(Anglers/subterranean/uncapped closure),
`common/buildings/03_resource_buildings.txt:314,1037,1530` (type cap additions).
В vanilla technology definitions прямых этих cap keys нет; research hook не подставлен
пустым новым эффектом. Новые shared alternatives geothermal/melting/Wilderness не
входят в поддержанный default-origin catalog. Cap-building graph fixture проверяет
пересчёт, но не сертифицирует ещё не реализованные allow/can_build этого здания.

## Ручной refund: preview и подтверждение — 15 октября

`RefundQuote` использует тот же `bank.Refund`, что отмена; дополнительно передаёт
положительные `losses = paid − credited` и непрозрачный `previewId`. ID связывает
colony revision, точный pending order/payer/quote, текущие запасы payer и storage
policy. Это fingerprint входов расчёта вместо отсутствующей отдельной bank revision.
Он не доверяет суммам клиента. `cancelConstruction` принимает `refundPreviewId` и
`confirmRefundLoss`: перед mutation сервер повторяет preview; изменённый ID даёт
`REFUND_PREVIEW_CHANGED`, потери без явного true — `REFUND_CONFIRMATION_REQUIRED`.
Для совместимости прежняя команда без обоих полей разрешена только при нулевых
потерях; неполная пара невалидна. Новый UI всегда передаёт оба поля. Replay проверяется
до нового preview; прежний commandId с другим подтверждением — conflict.

При нулевых потерях UI отправляет сразу. При положительных — модальное подтверждение
с иконкой/числом каждого ресурса, без видимых resource IDs. Подтверждение сверяет
живой target/POV/доступность/preview; обновление снимка обновляет расчёт, старый callback
не отправляет команду. Закрытие сохраняет заказ. ACK содержит только фактические
положительные потери, показанные один раз; полный возврат отдельного сообщения не даёт.
Проверки: 0/частичная/полная потеря, unlimited, изменение запасов/cap/revision, отказ
без mutation, подтверждение/replay/authority и client modal/stale/no-loss/no-command.

## Обычная очередь: текущий срез 15 октября

### Отмена из-за неположительной скорости

По §8 плана известный live bonus≤−1 отменяет соответствующий pending-заказ
в ближайшей construction-фазе, включая waiting/N=0. Это индивидуальная
неработоспособность, не пауза всей очереди. Проверка не требует разрешения
от неизвестных условий: известная неположительная скорость достаточна.
Reservation освобождается, работа/installed не растут, actual paid refund идёт
исходному payer по существующим caps. Продвижение следующего заказа начинается
со следующей штатной фазы, без остатка дня. Неизвестная скорость не считается
нулевой: прежний dependency gate остаётся. DTO cancelled order получает
`cancellationReason=non-positive-speed`; manual/condition/ownership/destroyed
причины различаются. Projection не порождает completion/full-refund сообщений.
UI уведомлений — следующий срез; этим Q04 целиком не закрывается. Приёмка:
active/waiting/N=0, сохранённая работа/банк, refund caps/replay, source adapter
при speed −1 не останавливает день, reason DTO; независимое review.

Сопутствующий district-pool срез: обычный live runtime проецирует
`districtPool={policyId:ordinary-district-slot-pool-v1,capacity,installed,reserved,free}`
из канонических district slots, installed и pending; `free=capacity-installed-reserved`.
Это текущий общий пул, не type/shared deposit limit. Его исходная реализация —
M3ConstructionContent.Create: source final size + context modifier + effective
planet_max_districts_add (включая blockers); новые лимиты/слоты здесь не создаются.
UI показывает общий счётчик и предел city как capacity минус остальные installed/
reserved районы только при согласованном DTO. Rural/type caps остаются unknown.
После каждого заказа/отмены/reorder/completion projection пересчитывается;
на cap city action остаётся видимой disabled без постоянной причины.
Изменения native cap после новых effects и rural zone lifecycle остаются отдельными
зависимостями. Приёмка: повторный city enqueue до полного пула, rejection без
оплаты/ревизии при занятых slots, cancel освобождает ровно одно место, DTO/UI
различают installed/reserved/free и unknown; эффекты от pending отсутствуют.

По §8 полного плана `reorderConstruction` принимает `colonyId`,
`expectedColonyRevision`, полный `orderIds` pending-очереди и обычный commandId.
Только owner/player с готовыми assets в running; строгий wire envelope16KiB.
IDs непустые, уникальные, до160 символов, без control. Множество совпадает точно,
первый активный заказ остаётся первым; минимум два ожидающих для изменения.
No-op отклоняется `queue-unchanged`, неправильный состав —
`invalid-queue-membership`, перенос active — `active-order-locked`.
Повтор ID/того же payload идемпотентен; другой payload конфликтует; stale не
изменяет state. Сохраняются completedWork, quote, payer, reservations и bank;
новая дневная фаза следует новому порядку, без переноса остатка дня.
Projection даёт `queuePolicy` с policyId `ordinary-sequential-v1`,
activeCapacity1/canReorder=true только для подключённого ordinary runtime.
Клиент выводит active/waiting и стрелки соседней перестановки по этому policy;
старый callback сверяет полный fingerprint/revision/POV. UTF8 oversize
отклоняется до добавления transport pending. Mobile/Wilderness N этим не закрыт.
Приёмка: состав/active/no-op/stale/replay, непотерянная работа/деньги, строгий
wire/oversize, click ровно одной команды, независимое review actual diff.

Concrete next operation/closure order and exact residual activation gates: [minimum activation plan](PLANET-CONSTRUCTION-ACTIVATION.md). A gated catalog is intermediate, not stage completion.

## Explicit lobby template protocol

Integrator authorization `c455843`: additive command `{commandId,type:"selectEmpireTemplate",templateId:"economic"|"scientific"|"military"}`. Only these fields are permitted; notably no targetParticipantId, numerical effects or client context. Existing active-connection/cache/asset-readiness guards apply. Require own player and lobby before lookup; unsupported/missing/null/control/oversized ID returns INVALID_COMMAND, observer OUT_OF_SCOPE, running INVALID_PHASE. Success stores immutable chosen configuration and resets own ready even when reselecting; exact retry returns cached ACK without resetting readiness again. Reconnect retains selection. No clearing/default selection or mandatory-selection start requirement in this slice.

Add optional `empireSetup` on session view: `{catalog:[{templateId,authorityId,originId,ethics,civics,traits}],selectedTemplateId:string|null}`. Catalog is public bounded accepted configuration; lists sorted ordinally. Selection follows own player or matching observer POV; general/pending observer has null selection, never another player's value. Do not add selection to the public participants list. Browser selector belongs to UI owner. Existing clients may ignore the additive field and keep laboratory start without selection.

Selection supplies construction identity/prerequisite configuration only, **not** applied ethic/civic/trait numerical effects or a replacement of M2 laboratory workforce/building controls. Preserve existing balances, jobs, traits, infrastructure and reports unchanged. Running identity is frozen by lobby-only mutation. Assess each actual owned colony from its participant's selected initial template; no shared global assessment. With no choice preserve unknown context/authority. With choice authority projects individualist and researched prerequisites use its accepted 31-tech list; owner exists and human-player AI=false are explicit runtime facts. Other owner predicates, `from?`, planet class/modifiers/carrier flags/counts/layout remain unknown until evidenced. Thus template choice alone cannot produce a final quote, free slots or executable construction. Tests cover strict fields/readiness/phase/role, invalid atomic refusal, different player choices, retry/conflict/reconnect, observer POV filtering, no default and unchanged M2 economic configuration.

12 October 2026. Implements the authorized [construction stage](PLANET-CONSTRUCTION-STAGE.md). This companion owns backend contracts; asset DTOs and browser implementation remain with their owners. Unknown native policies below are explicit dependency gates, not laboratory defaults.

## First production checkpoint

Audit/adapt only the three diagnostic importer files and its test project from the preserved stash, never apply/pop it. Read-only `construction-hydroponics-452-v1` follows [the definition import contract](CONSTRUCTION-DEFINITION-IMPORT.md): exact 4.5.2, bounded reached files, stable hashes, immutable expanded fields/cost branches/provenance and explicit unsupported/context coverage. No successful import implies executable content. Preserve M1's BuildingDefinition and all existing economy behavior. Historical Russian diagnostics in the unfinished stash are adapted to English; source identifiers remain unchanged. Tests cover source privacy, reparse, change/cancel, limits and real content.

Importer hardening: generated substitution strings have a separate conservative cumulative 8 MiB UTF-8 budget, including inline argument fanout even when the callee expands no fields. Compute complete substituted size before allocating its builder/result. This is additional defensive accounting, not a change to any game rule or the existing expanded DTO budget.

## Transaction machinery, not an invented native scheduler

13 October human lifecycle answers in [DECISIONS](DECISIONS.md#human-construction-lifecycle-clarification--13-october-2026) supersede earlier unresolved queue/refund/phase wording only for their exact stated rules. Base capacity 1, reordering and capacity-drop pausing are accepted; kind sharing and installed Ark/Wilderness additions remain pending. Fixed order quote, live speed changes, actual-paid cancellation with cap loss, no destroyed-planet refund, boundary completion-before-month and no daily carry are accepted. The reported speed example is not an established formula. Zone/building construction exclusions and incompatible-building removal are accepted; removal refunds remain pending. Existing lower-level explicitly supplied synthetic-policy machinery is not automatically native activation. The first accepted numerical lifecycle implementation is bounded in the aggregate contract; other source/realization/eligibility gates remain.

Core state is immutable: colony identity/owner/revision, installed instances, per-slot reservations, orders with immutable paid price/progress/completion state and resource balances by original payer. Authority/mode/mobile carrier/location are independent explicit identity/context fields, not inferred from species names. Typed operation kind distinguishes district, zone, building and blocker clearance. Passive features cannot be cleared through a generic building operation.

An operation receives a source-profile ID and definition ID, resolved eligibility with evidence, exact slot ID, authoritative quote and explicit policy revision. Unknown eligibility, slot, price/time arithmetic, effects or policy yields `dependency-unresolved`; no zero-price/time or slot fallback. Validated enqueue checks ownership, expected revision, duplicate command identity, unique order ID, positive duration, nonnegative finite decimal amounts, resource sufficiency and vacant slot. Candidate debit/reservation/order changes publish together. A failed candidate leaves the prior state intact.

Retries use command ID plus exact payload identity. Same retry returns the original outcome without mutation; conflicting payload reuse is rejected. Revisions are checked; overflows fail atomically. Single-owner command serialization is an engineering boundary, not native scheduling evidence. Collections are copied/read-only. No external mutable callback may mutate an existing world.

Progression requires an explicit validated plan naming active order IDs and per-order work, plus an explicit evidenced completion/economy phase policy. No default FIFO, executor count, rate or phase ordering. Cancellation requires an explicit validated refund amount/cap policy and reason. Ownership change refunds the original payer; destruction cancels, but its numerical refund outcome is not inferred from that wording. Condition loss uses the same refund gate. Installed-object/upkeep/job capacity contributions stay distinct; no new Pops/Workforce or leaked job-upkeep multiplier. Completion is idempotent and releases reservation exactly once; effect candidates and resource deltas commit atomically with order completion.

The first Core transaction component is a pure immutable ledger, not automatically attached to the live day loop. Explicit trusted backend plans carry resolved content/rule policy IDs, cost/work/effect values and any unresolved gates. Enqueue fails on gates before charging. Progress names every affected order and exact positive work increment; an increment beyond remaining work rejects the whole batch rather than inventing overflow/carry behavior. Completion installs the quoted definition and separate infrastructure-upkeep/job-capacity contribution snapshots. Cancellation supplies exact nonnegative refunds no larger than the original paid amounts and explicit resource storage caps; missing refund/cap policy is unresolved for every reason, including destruction. A candidate never reads client-supplied numerical values. Ownership change must atomically cancel all active orders with explicit original-payer refund plans. Frozen lifecycle is gated until its queue behavior is established.

Engineering memory guards: at most 4096 slots, stored orders and successful command receipts per ledger; exhaustion is a visible `capacity-limit` failure, never automatic receipt eviction or a native queue-capacity claim. Command fingerprints cover the complete typed payload, actor and expected revision. Replayed successful commands return their recorded applied revision; conflicting ID reuse fails. Resource accounts are keyed by payer, independent of colony owner, so no refund transfers to a new owner by accident. Mobile carrier/location and authority mode are preserved by candidate construction; this does not establish mobile queue lifecycle.

Identifiers are bounded to 160 printable characters; each quantity/gate collection to 256; account count to 4096. Enumerable inputs are bounded before materialization. Current ownership is checked before receipt lookup, so former/foreign owners cannot bypass authority with an old command ID. Replay guarantees apply only while the caller remains authorized.

Validate operation identifiers and nonnegative expected revision before payload serialization/fingerprinting, after the authority guard. Invalid/null/control/oversized operation IDs return `invalid-identifier`; negative revision returns `invalid-revision`, with unchanged state. Missing phase policy remains `dependency-unresolved`.

## Additive development projection and commands

The additive wire field `development` is optional while the backend is not activated; absence means unavailable, never an empty executable catalog. Its C# types are declared in `M3DevelopmentProtocol.cs`; JSON uses the existing camelCase serializer. No source-backed executable subset is established yet: hydroponics is diagnostic only. All operations remain unavailable until mandatory rules/context are resolved. UI may implement adapters against these fields, but cannot fabricate live colony state.

- `development`: `{ contentProfileId, colonies, gates }`.
- Each colony: `{ colonyId, economyPlanetId: int|null, ownerParticipantId, revision, authorityMode, isMobile, carrierId: string|null, locationSystemId: string|null, slots, catalog, installed, queue, features, jobs, gates }`. Authority strings: `individualist`, `hive`, `machineIntelligence`, or explicitly `unresolved` when the fixture does not configure authority; mobile is orthogonal.
- Each slot: `{ slotId, kind, reservedByOrderId: string|null, installedDefinitionId: string|null, gates }`. Kinds: `district`, `zone`, `building`, `blockerClearance`. No native slot maximum is inferred.
- Catalog: `{ definitionId, kind, imageDefinitionKey: string|null, eligibility, slotIds, quote: object|null, gates }`. Eligibility: `available`, `unavailable`, `unresolved`. Quote: `{ policyId, costs, durationDays, infrastructureUpkeep, jobCapacity }`; maps are resource/job ID to decimal quantities. A non-null quote requires all arithmetic/duration resolved; `durationDays > 0`. Core abstract work is not automatically days. Missing image keys are diagnosed by asset coverage, not fabricated.
- Installed: `{ slotId, definitionId, kind, imageDefinitionKey: string|null, infrastructureUpkeep, jobCapacity }`.
- Queue: `{ orderId, slotId, definitionId, kind, status, paidCosts, completedWork, requiredWork, workUnit, refundQuote: object|null, gates }`. Status: `pending`, `completed`, `cancelled`; `workUnit` is explicit (currently internal ledger `policyWork`, not native days). Refund quote: `{ policyId, amounts }`; null disables actionable cancellation until resolution.
- Features: `{ instanceId, definitionId, imageDefinitionKey: string|null, clearable, clearanceDefinitionId: string|null, gates, clearanceSlotId: string|null }`. Passive features have `clearable=false`; a clearable flag alone does not grant eligibility. Additive nullable clearanceSlotId defaults null for older constructors/snapshots. Only a server-projected exact instance-to-slot binding can enable the row: the same colony must contain that blockerClearance slot and clearanceDefinitionId catalog entry whose slotIds includes it, with available eligibility, resolved quote and no blocking gates/reservation. No client derivation from instance ID, definition, index or image key. Server resolves the received slot/definition to exactly one current feature instance and revalidates revision/ownership/full effects; forged or stale pairs fail unchanged. Missing mapping keeps clearance unavailable. This declaration creates no live feature, clearance slot or effect executor.
- Jobs: `{ jobId, categoryId, imageDefinitionKey: string|null, capacity, assigned, workforce, gates }`. The last three numbers are nullable independently when unavailable; never synthesize Pops/Workforce. Existing authoritative workforce commands remain unchanged.
- Every gate: `{ code, dependencyId }`, containing safe IDs only. No AST, source bytes, credentials or local absolute paths.

Client command envelope retains existing fields and adds optional `colonyId`, `expectedColonyRevision`, `definitionId`, `slotId`, `orderId`. Types are `enqueueConstruction` and `clearBlocker` (all five fields required; order ID supplied for retry identity) and `cancelConstruction` (colony/revision/order required, definition/slot absent). Identifiers are bounded printable strings of at most 160 characters; revision is a nonnegative integer. Server derives actor, costs, eligibility, duration, refund and effects. Declaration checkpoint reserves these names; the source-assessment slice registers strict fields and refusal-only handling, not mutating order execution. Command results retain existing accepted/code/sequence semantics; no optimistic charge/progress in UI.

Concrete command shapes must reuse existing `commandId`, ownership, asset-ready and dedup checks; add/cancel/clear carry colony, expected revision, exact selected definition/slot/order IDs. No client-supplied costs, progress, eligibility or effects. Foreign or observer mutations are rejected before evaluating private details. Player sees own colony; observer matching POV receives that player's filtered projection; general observer follows existing read-only visibility policy. Source import failure or unresolved mandatory native rules are visible catalog gates and do not silently activate dummy controls.

## Verification and remaining gates

## Source assessment and refusal wiring — next bounded slice

Implement an assessor for the imported `building_hydroponics_farm` only, retaining its actual expanded tree and source provenance. This is the first selected building's evaluated operator coverage, not the complete district/zone inventory. Exact 4.5.2 source: `common/buildings/03_resource_buildings.txt:5–64`; `jobs/farmers_add` independent regular/spiritualist-FE/gestalt blocks; `buildings/nomadic_cost_switcher` two separate `from?` branches. Preserve every job block independently; never first-match them. No zone is selected/evaluated by this checkpoint and no zone conditional block is flattened.

Context is an immutable explicit snapshot: owner existence, owner boolean predicates, separately bound `from` nomadic fact, planet class, known modifier/carrier-flag/technology sets and building counts. Null means unknown, not absent/false/regular. `from?` without an explicitly supplied binding remains unresolved; owner/mobile never substitutes. Supported syntax: implicit conjunction, `AND`, `OR`, single-child `NOT`, owner transition, `exists=owner`, the reached boolean owner predicates, `is_planet_class`, `has_modifier`, `has_carrier_flag`, and `num_buildings { type=... value < decimal }`. Other operators/shapes are unresolved with bounded reason codes. Evaluate all siblings for diagnostic coverage; false/true logical results may be decisive, but unresolved reasons remain visible and prevent an executable quote. No native short-circuit/order claim.

Assessment returns potential/destroy/prerequisite results, every cost branch's condition/raw quantities/separate multiplier and every conditional job-capacity block. It never multiplies/rounds prices or turns abstract work into days. Missing/duplicate required blocks or unsupported effects prevent execution. Final authoritative quote remains null until slot/context, job closure, price/time/modifier/rounding, queue and daily/monthly completion policies are evidenced. Specifically: raw regular minerals 400 and nomadic raw motes 400/mult .1 are not final payable quotes; raw buildtime 360 is not a promised completion date. Unknown refund/caps separately prevent cancellation, including destruction.

Parent modifier assignment must be `=`; duplicate job-capacity scalars within one block are unsupported, never summed by an inferred rule. Until the asset owner confirms the exact image key mapping, catalog `imageDefinitionKey` stays null with `construction-image-binding`; no concatenated key is advertised as available.

Reviewed v6 image binding supersedes that temporary gate for hydroponics only: exact key `building.building_hydroponics_farm` from asset checkpoint `f80f895`/PLANET-ASSETS. Require current manifest v6/importer 5, actual selected definition/root `common/buildings/03_resource_buildings.txt`, presence of its reviewed DDS path and matching byte length/SHA-256 for the root and every construction dependency also present in the manifest. Missing root/image or inconsistent shared source snapshots retains null key and image-binding gate. Non-overlapping gameplay inline dependencies are not image dependencies. Publish only the key, not paths/hashes; browser asset-ready importer still validates/decodes the complete profile atomically. Removing the image gate does not remove layout/quote/execution gates. Tests use the real manifest/import plus missing root/image and changed shared hashes; matching observer POV retains privacy and source data stays out of JSON.

Server bootstrap imports this optional construction profile read-only and passes its immutable result into the session. A safe import failure logs a bounded generic warning and supplies an explicit unavailable-profile development gate; never silently disappears or aborts the working M3 economy. Existing direct test/session constructors default to no construction profile. While running, project actual already-received colony IDs/owner/economy links only to own player and matching observer POV; general/pending observers receive no development colonies. Profile/layout/rule gates explain that empty slots/installed/queue/features/jobs mean unprojected coverage, not an authoritative empty vanilla layout. Existing fixture has no authority configuration: allow `authorityMode=unresolved` rather than infer one. Fixture colonies are stationary; carrier is null. No queue state or ledger gets attached to the live day/month loop yet.

Register reserved command field validation and refusal handling: readiness and existing connection/cache guard remain; require player/running/own currently received colony before exposing definition/revision details. Validate bounded IDs/nonnegative revision; unknown definitions yield `UNKNOWN_DEFINITION`, stale colony revision `STALE_REVISION`, unsupported layout/native dependencies `DEPENDENCY_UNRESOLVED`. Initial projected colony revision is 0 because this checkpoint creates no construction mutation. Foreign/observer requests yield `OUT_OF_SCOPE` before catalog lookup. Cancel/clear also refuse unresolved dependencies without charging/reserving/cancelling. Rejected retries reuse existing exact ACK cache; no sequence/day/economy changes. No client numerical quote/progress/refund is accepted.

Transport closure correction: the separate M3WireCodec whitelist must register the exact declared construction command fields too; a session-only command handler is insufficient evidence of browser reachability. `expectedColonyRevision` is an Int64 JSON integer, not an Int32/string/decimal; wire type/range validation precedes DTO deserialization. Missing/null required fields follow existing transport behavior and reach session INVALID_COMMAND; negative Int64 revision also reaches session semantic refusal. Extra fields, duplicate properties, wrong types and out-of-Int64 numbers fail strict transport validation. This exposes only the existing refusal path, never live construction. Focused codec checks cover all three command shapes, missing fields and integer boundaries; existing session refusal/authority/retry checks remain unchanged.

Real transport verification extends the existing isolated loopback harness (ephemeral port, two players/observer, cancellation-owned shutdown): explicit differing lobby choices and filtered observer setup, own construction refusal/exact retry, foreign/observer refusal, negative revision, clear/cancel refusal and reconnect-retained choice. Require actual ACK codes and snapshots rather than treating wire parse as full WebSocket evidence. No existing user server is reused or stopped; licensed source stays read-only. This is not a browser visual or playable-construction acceptance test.

## Isolated economy candidate seam

Before live completion integration, add `EconomyWorld.Fork()` to create a separate mutable candidate preserving entity IDs/generations/store links, month, current balances, groups, assignments, bonus workforce, modifiers, infrastructure upkeep and already published reports. Copy every mutable collection; immutable definition/report snapshots may be shared. The caller must use existing single-owner serialization; this is not concurrent mutation support, native phase policy or a persisted save. IDs are intentionally compatible between a world and its fork only while their lineage/layout stays aligned; do not use this to exchange IDs between unrelated worlds.

Construction completion can later apply **explicitly resolved** contributions and settlement to this isolated candidate, then publish it with the staged ledger/spatial state after all validation succeeds. No completion effects or ledger are attached to the live loop in this seam checkpoint. No capacity-to-employed-job conversion or new workforce is invented. Acceptance: identical initial state hash/report, independent modifications of balances/jobs/modifiers/bonus/upkeep/groups/entities/month, discarded candidate failure leaves original state byte-for-byte hash-identical, existing economy/M3 regressions preserved. A future swap must additionally publish participant configuration and ledger references atomically; merely cloning economy does not establish that integration.

## Received system-exit coordinates

Under fleet/navigation contract `8611c786`, add optional `spatial.exits:[{systemId,destinationSystemId,position:{x,z}}]`. Real M3 projections supply a read-only array with both directional entries for each **received** hyperlane, using the same authoritative SpaceWorld exit calculation used by movement/arrival; existing radius 140, geometry, phases and commands do not change. Include only when both endpoints pass existing permitted-system filtering; pending/empty projection has an empty list. Destination labels resolve only from received systems. Missing optional array in an older snapshot means unavailable, not permission for client endpoint reconstruction. Expose no internal neighbor maps, hidden IDs, local paths or ETA.

`SpaceWorld.GetExitPosition(from,to)` validates an actual adjacent pair before returning its existing exit calculation; arbitrary/nonadjacent/unknown pairs fail visibly. Acceptance: exact received endpoints versus authoritative positions, two directions per visible lane, fixed radius and finite coordinates, no entries for a hidden endpoint, immutable arrays/empty projection and unchanged existing M3 navigation/session checks. Renderer/UI owners consume only reviewed fields; no new movement authority or route computation is delegated to clients.

## Reviewed planet-module packaging

Reuse reviewed asset commits `4c9532c`/`f80f895` without reauthoring their importer/profile. Server content links must copy `m3-planet-assets.mjs` and `m3-planet-images.mjs` under existing `/m3-modules/` in both build and publish output; no original asset endpoint is introduced. Existing relative module imports resolve there. Real isolated HTTP checks require successful JavaScript MIME responses byte-identical to the packaged authored modules, and actual JSON snapshots carry finite received exit pairs. Renderer-requested `m3-navigation.mjs` is queued separately until its reviewed ready commit exists; do not include a nonexistent draft or infer implementation from that queue entry. Local integration checks do not establish stable-main/UI readiness.

Reviewed renderer checkpoint `77da79d` now supplies `m3-navigation.mjs`. Include this authored dependency under the same build/publish path and exact HTTP byte/MIME verification. Reuse the reviewed renderer without reauthoring its geometry or interaction rules; this packaging change does not establish combined browser or live cross-client acceptance.

## Trusted completion/settlement candidate

Add a pure single-colony staging helper for the aggregate owner, not a new client endpoint or default execution policy. Inputs: current EconomyWorld and immutable ConstructionLedger from the **same authoritative aggregate revision**, validated actual colony-to-economy-planet binding, existing ledger command/actor/revision, explicit named phase policy, explicit phase enum (completion before settlement or after settlement), resolved positive per-order work increments and an explicit settle-month flag. Missing phase policy/unsupported enum fails unchanged; no default phase or elapsed-day inference. The caller owns authority/binding validation and final atomic swap of all candidates; this helper cannot certify an arbitrary planet belongs to a participant or choose native active orders.

Stage ledger progression using existing guards, fork economy, add only newly installed ledger contributions to the planet's current **infrastructure** upkeep, and optionally settle its existing whole-world monthly economy at the explicitly chosen phase. Add a read-only copied `GetBuildingUpkeep(planet)` accessor for this baseline. Receipt identity includes explicit policy ID, phase enum, settle flag and planet binding, using a bounded internal digest in the ledger policy field; altered retry context conflicts rather than silently reusing a completion. Exact receipt replay returns the original input references without repeating upkeep or monthly settlement. Never overwrite existing M2 infrastructure, create workforce or convert raw capacity to employed jobs. Capacity remains in installed ledger snapshots; unsupported job-definition/employment integration is still gated. Unknown resource/planet, overflow or settlement deficit returns failure with original ledger/economy references unchanged; no broad exception suppression. Fresh successful result owns isolated candidates and does not mutate either input. This is engineering composition of already trusted values, not evidence for the proposed C04/C05 policy and not live activation.

Acceptance uses explicit synthetic trusted quotes/plans: infrastructure baseline+completion sum exactly once, before/after settlement effects distinguishable, capacity not employed, receipt replay no double upkeep, invalid phase/work/source resource/planet and failed monthly settlement roll back both candidates, bidirectional isolation and relevant regressions. No supported-blocker claim: existing ledger intentionally refuses clearance until its complete effect context is resolved. Multiple colony candidates must later be composed under one session day transaction, not independently published.

Ledger resource accounts and EconomyWorld country balances remain separate stores at this checkpoint. The aggregate owner must reconcile their authoritative snapshots before command debit/refund and after monthly settlement; this helper does not synchronize them. Binding/ownership validation and a whole-day atomic swap remain integration responsibilities, not capabilities established by a successful Stage result.

Next bounded engineering integration is specified separately in [trusted aggregate](CONSTRUCTION-AGGREGATE.md): validated bindings, shared-country account reconciliation, multi-colony completion and one settlement, immutable publication and aggregate retries. It is not activation of a proposed native policy.

Acceptance: source-based synthetic and real assessments cover ordinary/hive/machine predicate branches, independent multiple conditional blocks, separate unknown `from?`, malformed/unsupported operators, researched/missing/unknown prerequisites and immutable context. Focused session checks cover player/observer POV/general/pending privacy, readiness/ownership before details, strict fields, malformed/stale/unknown commands, rejected retries/reconnect and unchanged state. Existing M3 session regressions remain mandatory. Independently review actual production/test diff. Source-native gaps route to reference/manual queue with reproducible scenarios; do not treat this assessment/wiring as playable completion.

Before activation, publish exact source-backed executable IDs/prerequisites/slots/operator coverage for all three authority modes and mobile capabilities. Initial diagnostic anchor is only hydroponics, not a replacement for accepted district/zone/building/feature sets. Unknown native queue/refund/phases and runtime-context closure block dependent live execution, not the importer or pure transaction validation. Integration must preserve daily/month atomicity, connection/idempotency/privacy, D38/D43, AFK/frozen semantics and existing M3 tests. Independent actual-diff review is mandatory before integrating any code. Report component evidence separately from playable completion.
